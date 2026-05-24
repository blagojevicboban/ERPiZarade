using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using PlataData;
using PlataData.Models;

namespace PlataApp.Services;

public class ObracunService
{
    // Standard defaults for Serbian tax laws in 2026/recent years
    public const decimal DefaultTaxRate = 0.10m;
    public const decimal DefaultPoreskoOslobodjenje = 28423.00m;
    public const decimal DefaultMinContributionBase = 51297.00m;

    public const decimal DefaultEmployeePioRate = 0.1400m;
    public const decimal DefaultEmployeeZdravstvoRate = 0.0515m;
    public const decimal DefaultEmployeeNezaposlenostRate = 0.0075m;

    public const decimal DefaultEmployerPioRate = 0.1000m;
    public const decimal DefaultEmployerZdravstvoRate = 0.0515m;
    public const decimal DefaultEmployerNezaposlenostRate = 0.0000m;

    private readonly PlataDbContext _db;

    public ObracunService(PlataDbContext db)
    {
        _db = db;
    }

    public ObracunPlate Calculate(Radnik radnik, RadniSat sati, int godina, int mesec, decimal vrednostBoda, int fondCasova)
    {
        // 1. Calculate tenure (minuli rad)
        int yearsOfTenure = 0;
        if (radnik.DatumZaposlenja.HasValue)
        {
            var calculatedDate = new DateTime(godina, mesec, 1);
            yearsOfTenure = (int)((calculatedDate - radnik.DatumZaposlenja.Value).TotalDays / 365.0);
            if (yearsOfTenure < 0) yearsOfTenure = 0;
            if (yearsOfTenure > 99) yearsOfTenure = 99;
        }

        // 2. Determine base hourly wage
        decimal hourlyBase = 0m;
        if (radnik.Koeficijent > 0)
        {
            hourlyBase = (radnik.Koeficijent * vrednostBoda) / fondCasova;
        }
        else if (radnik.OsnovnaPlata > 0)
        {
            hourlyBase = radnik.OsnovnaPlata / fondCasova;
        }
        else
        {
            // Fallback default
            hourlyBase = (1.5m * vrednostBoda) / fondCasova;
        }

        // Load system parameters / tax rates from database
        var pParams = _db.Porezi
            .Where(p => p.Godina == godina && p.Mesec == mesec)
            .OrderBy(p => p.RedniBroj)
            .FirstOrDefault();

        // Fallback: search for closest past month's parameters if current doesn't exist yet
        if (pParams == null)
        {
            pParams = _db.Porezi
                .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                .OrderByDescending(p => p.Godina)
                .ThenByDescending(p => p.Mesec)
                .ThenBy(p => p.RedniBroj)
                .FirstOrDefault();
        }

        // Define calculation rates and coefficients
        decimal procMinul = pParams != null ? pParams.ProcMinul : 0.40m;
        decimal procPreko = pParams != null ? pParams.ProcPreko : 26.00m;
        decimal procNocni = pParams != null ? pParams.ProcNocni : 26.00m;
        decimal procDrzav = pParams != null ? pParams.ProcDrzav : 110.00m;
        decimal procBolov = pParams != null ? pParams.ProcBolov : 65.00m;

        // 3. workedHours and Minuli Rad calculation
        decimal workedHours = sati.RedovniSati + sati.PrekovremeneSati + sati.DrzavniPraznikSati + sati.NocniSati;
        decimal neto_zar = workedHours * hourlyBase;
        decimal brutoMinuliRad = Math.Round(neto_zar * (procMinul / 100m) * yearsOfTenure, 0);
        decimal min_po_cas = workedHours > 0 ? brutoMinuliRad / workedHours : 0m;

        // 12-month average hourly rate from database (or calculated dynamically)
        decimal prosek = sati.Prosek > 0 ? sati.Prosek : IzracunajProsekRadnika(radnik.Id, godina, mesec);

        // 4. Wage lines (gross parts)
        decimal brutoRedovni = sati.RedovniSati * hourlyBase;
        decimal brutoBolovanje = sati.BolovanjeSati * prosek * (procBolov / 100m); // sick leave base
        decimal brutoPrekovremeni = sati.PrekovremeneSati * (1m + procPreko / 100m) * (hourlyBase + min_po_cas); // overtime bonus + base
        decimal brutoGodisnji = sati.GodisnjiOdmorSati * prosek; // Paid at Prosek
        decimal brutoPraznik = sati.DrzavniPraznikSati * (1m + procDrzav / 100m) * (hourlyBase + min_po_cas); // state holiday bonus + base
        decimal brutoNocni = sati.NocniSati * (1m + procNocni / 100m) * (hourlyBase + min_po_cas); // night shift bonus + base

        decimal totalBruto = brutoRedovni + brutoBolovanje + brutoPrekovremeni + brutoGodisnji + brutoPraznik + brutoNocni + brutoMinuliRad;

        // 5. Tax parameters
        decimal taxRate = DefaultTaxRate;
        decimal taxExemption = DefaultPoreskoOslobodjenje;
        decimal minBase = DefaultMinContributionBase;

        if (pParams != null)
        {
            taxRate = pParams.AkPorez > 0 ? pParams.AkPorez / 100m : taxRate;
            taxExemption = pParams.Prvast > 0 ? pParams.Prvast : taxExemption;
        }
        else
        {
            // Try reading actual settings from SQLite (if populated)
            var poreskaStopa = _db.PoreskeStope.FirstOrDefault();
            if (poreskaStopa != null)
            {
                taxRate = poreskaStopa.Stopa > 0 ? poreskaStopa.Stopa : taxRate;
                taxExemption = poreskaStopa.GranjaOd > 0 ? poreskaStopa.GranjaOd : taxExemption;
            }
        }

        // Scale tax exemption to hours worked relative to month fund
        decimal totalHours = sati.RedovniSati + sati.BolovanjeSati + sati.PrekovremeneSati + sati.GodisnjiOdmorSati + sati.DrzavniPraznikSati + sati.NocniSati;
        decimal workFactor = fondCasova > 0 ? totalHours / fondCasova : 1.0m;
        if (workFactor > 1.0m) workFactor = 1.0m;

        decimal scaledExemption = taxExemption * workFactor;
        decimal poreskaOsnovica = Math.Max(0, totalBruto - scaledExemption);
        decimal porez = poreskaOsnovica * taxRate;

        // 6. Social security contributions bases on Employee class
        decimal razredLimit = minBase;
        if (int.TryParse(radnik.Kategorija, out int razredVal))
        {
            if (razredVal == 9) razredLimit = 0m;
        }
        else if (radnik.Kategorija == "9")
        {
            razredLimit = 0m;
        }

        decimal granica = razredLimit * workFactor;
        decimal granicaPIO = razredLimit * workFactor;

        decimal brutoOsn = totalBruto;
        decimal brutPioOsn = totalBruto;

        // Clamping by akontacija = "DA" matching OBRAC.PRG
        if (totalBruto <= granica / 2m)
        {
            brutoOsn = granica / 2m;
        }
        else if (totalBruto < granica)
        {
            brutoOsn = granica;
        }

        if (totalBruto <= granicaPIO / 2m)
        {
            brutPioOsn = granicaPIO / 2m;
        }
        else if (totalBruto < granicaPIO)
        {
            brutPioOsn = granicaPIO;
        }

        // Load contribution rates from database
        var dbDoprinosi = _db.Doprinosi
            .Where(d => d.Godina == godina && d.Mesec == mesec)
            .ToList();

        // Fallback: load closest past month's rates if current doesn't exist
        if (!dbDoprinosi.Any())
        {
            var closestPeriod = _db.Doprinosi
                .Where(d => d.Godina < godina || (d.Godina == godina && d.Mesec < mesec))
                .OrderByDescending(d => d.Godina)
                .ThenByDescending(d => d.Mesec)
                .FirstOrDefault();

            if (closestPeriod != null)
            {
                dbDoprinosi = _db.Doprinosi
                    .Where(d => d.Godina == closestPeriod.Godina && d.Mesec == closestPeriod.Mesec)
                    .ToList();
            }
        }

        // Standard rates variables initialized to defaults
        decimal empPio = DefaultEmployeePioRate;
        decimal empZdr = DefaultEmployeeZdravstvoRate;
        decimal empNez = DefaultEmployeeNezaposlenostRate;

        decimal bossPio = DefaultEmployerPioRate;
        decimal bossZdr = DefaultEmployerZdravstvoRate;
        decimal bossNez = DefaultEmployerNezaposlenostRate;

        // Overlay with database rates if found
        if (dbDoprinosi.Any())
        {
            var pioRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 1);
            if (pioRec != null)
            {
                empPio = pioRec.ProcRadn / 100m;
                bossPio = pioRec.ProcPosl / 100m;
            }

            var zdrRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 2);
            if (zdrRec != null)
            {
                empZdr = zdrRec.ProcRadn / 100m;
                bossZdr = zdrRec.ProcPosl / 100m;
            }

            var nezRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 3);
            if (nezRec != null)
            {
                empNez = nezRec.ProcRadn / 100m;
                bossNez = nezRec.ProcPosl / 100m;
            }
        }

        if (radnik.StopaPio > 0) empPio = radnik.StopaPio;
        if (radnik.StopaZdravstvo > 0) empZdr = radnik.StopaZdravstvo;
        if (radnik.StopaNezaposlenost > 0) empNez = radnik.StopaNezaposlenost;

        decimal dopPioRadnik = brutPioOsn * empPio;
        decimal dopZdrRadnik = brutoOsn * empZdr;
        decimal dopNezRadnik = brutoOsn * empNez;

        decimal dopPioPoslodavac = brutPioOsn * bossPio;
        decimal dopZdrPoslodavac = brutoOsn * bossZdr;
        decimal dopNezPoslodavac = brutoOsn * bossNez;

        // 7. Fetch active credits and deductions
        decimal kreditiObustava = 0m;
        var targetDate = new DateTime(godina, mesec, 1);
        var activeKrediti = _db.Krediti.Where(k => k.RadnikId == radnik.Id && k.Aktivan && k.DatumPocetka <= targetDate).ToList();
        foreach (var k in activeKrediti)
        {
            decimal rata = Math.Min(k.MesecnaRata, k.OstatakDuga);
            kreditiObustava += rata;
        }

        // Fetch samodoprinosi details
        decimal samodoprinosiIznos = 0m;
        var generated = new List<Samodoprinosi>();
        
        // Attempt to calculate dynamically from Clipper DBFs if they exist
        string dbfDir = @"C:\PLATA\KOR28";
        if (Directory.Exists(dbfDir))
        {
            // Dynamically calculate from DBF assignments and catalog definitions
            decimal netoBase = totalBruto - (dopPioRadnik + dopZdrRadnik + dopNezRadnik + porez) - kreditiObustava;
            samodoprinosiIznos = CalculateDynamicSamodoprinosi(radnik.Id, godina, mesec, netoBase, out generated);
            
            // Re-write to SQLite to keep it synced
            var oldSam = _db.Samodoprinosi
                .Where(s => s.RadnikId == radnik.Id && s.Godina == godina && s.Mesec == mesec)
                .ToList();
            _db.Samodoprinosi.RemoveRange(oldSam);
            _db.Samodoprinosi.AddRange(generated);
        }
        else
        {
            // Fallback: Read existing records from SQLite
            var activeSamodoprinosi = _db.Samodoprinosi
                .Where(s => s.RadnikId == radnik.Id && s.Godina == godina && s.Mesec == mesec)
                .ToList();
            foreach (var s in activeSamodoprinosi)
            {
                samodoprinosiIznos += s.Iznos;
            }
        }

        // 8. Net salary calculation
        decimal totalEmployeeDeductions = dopPioRadnik + dopZdrRadnik + dopNezRadnik + porez;
        decimal netoIsplata = totalBruto - totalEmployeeDeductions - kreditiObustava - samodoprinosiIznos;
        if (netoIsplata < 0m) netoIsplata = 0m;

        return new ObracunPlate
        {
            RadnikId = radnik.Id,
            Radnik = radnik,
            Godina = godina,
            Mesec = mesec,
            BrutoZarada = Math.Round(totalBruto - brutoBolovanje, 2),
            BrutoBolovanje = Math.Round(brutoBolovanje, 2),
            BrutoNaknade = Math.Round(brutoPrekovremeni + brutoPraznik + brutoNocni, 2),
            BrutoStimulacija = 0m,
            BrutoMinuliRad = Math.Round(brutoMinuliRad, 2),
            
            DoprinosPioRadnik = Math.Round(dopPioRadnik, 2),
            DoprinosZdravstvoRadnik = Math.Round(dopZdrRadnik, 2),
            DoprinosNezaposlenostRadnik = Math.Round(dopNezRadnik, 2),

            DoprinosPioPoslodavac = Math.Round(dopPioPoslodavac, 2),
            DoprinosZdravstvoPoslodavac = Math.Round(dopZdrPoslodavac, 2),
            DoprinosNezaposlenostPoslodavac = Math.Round(dopNezPoslodavac, 2),

            PorezNaDohodak = Math.Round(porez, 2),
            PoreskaOsnovica = Math.Round(poreskaOsnovica, 2),
            KreditObustava = Math.Round(kreditiObustava, 2),
            Samodoprinosi = Math.Round(samodoprinosiIznos, 2),
            OstaliOdbici = 0m,
            NetoIsplata = Math.Round(netoIsplata, 2),

            RedovniSati = sati.RedovniSati,
            BolovanjeSati = sati.BolovanjeSati,
            PrekovremeneSati = sati.PrekovremeneSati,
            GodisnjioOdmorSati = sati.GodisnjiOdmorSati,
            Prosek = Math.Round(prosek, 2),
            Zakljucen = false,
            DatumObracuna = DateTime.Now,
            Napomena = $"Obračun kreiran {DateTime.Now:dd.MM.yyyy HH:mm}"
        };
    }

    public decimal IzracunajProsekRadnika(int radnikId, int godina, int mesec)
    {
        var targetPeriods = new List<(int Year, int Month)>();
        int currentYear = godina;
        int currentMonth = mesec;

        for (int i = 1; i <= 12; i++)
        {
            int m = currentMonth - i;
            int y = currentYear;
            while (m <= 0)
            {
                m += 12;
                y -= 1;
            }
            targetPeriods.Add((y, m));
        }

        decimal psumbr = 0m;
        decimal psumcas = 0m;

        // Učitaj sve obračune i radne sate za tog radnika u tom opsegu
        var obracuni = _db.ObracuniPlata
            .Where(o => o.RadnikId == radnikId)
            .ToList()
            .Where(o => targetPeriods.Any(p => p.Year == o.Godina && p.Month == o.Mesec))
            .ToList();

        var satiLista = _db.RadniSati
            .Where(s => s.RadnikId == radnikId)
            .ToList()
            .Where(s => targetPeriods.Any(p => p.Year == s.Godina && p.Month == s.Mesec))
            .ToDictionary(s => (s.Godina, s.Mesec));

        foreach (var ob in obracuni)
        {
            decimal casovi = 0;
            if (satiLista.TryGetValue((ob.Godina, ob.Mesec), out var s))
            {
                casovi = s.RedovniSati + s.PrekovremeneSati + s.DrzavniPraznikSati + s.NocniSati;
            }
            else
            {
                casovi = ob.RedovniSati + ob.PrekovremeneSati;
            }

            decimal netPay = ob.NetoIsplata + ob.KreditObustava + ob.Samodoprinosi;
            
            // Razdvajanje neto redovnog dela (bez bolovanja)
            decimal totalGross = ob.BrutoZarada + ob.BrutoBolovanje + ob.BrutoNaknade + ob.BrutoMinuliRad;
            decimal regularGross = ob.BrutoZarada + ob.BrutoNaknade + ob.BrutoMinuliRad;
            decimal netRegular = totalGross > 0 ? netPay * (regularGross / totalGross) : netPay;

            psumbr += netRegular;
            psumcas += casovi;
        }

        if (psumcas > 0)
        {
            return Math.Round(psumbr / psumcas, 4);
        }

        // Fallback na trenutnu osnovnu satnicu ako nema istorije
        var radnik = _db.Radnici.Find(radnikId);
        if (radnik != null)
        {
            decimal fondSati = 176m;
            decimal hourlyBase = 0m;
            if (radnik.Koeficijent > 0)
            {
                hourlyBase = (radnik.Koeficijent * 1860.34m) / fondSati;
            }
            else if (radnik.OsnovnaPlata > 0)
            {
                hourlyBase = radnik.OsnovnaPlata / fondSati;
            }
            return Math.Round(hourlyBase, 4);
        }

        return 0m;
    }

    private decimal CalculateDynamicSamodoprinosi(int radnikId, int godina, int mesec, decimal netoBase, out List<Samodoprinosi> generated)
    {
        generated = new List<Samodoprinosi>();
        decimal sum = 0m;
        
        string dbfDir = @"C:\PLATA\KOR28";
        string radSamPath = Path.Combine(dbfDir, "RAD_SAM.DBF");
        string radSamiPath = Path.Combine(dbfDir, "RAD_SAMI.DBF");
        string samodopPath = Path.Combine(dbfDir, "SAMODOP.DBF");
        string samodopiPath = Path.Combine(dbfDir, "SAMODOPI.DBF");
        
        var assignedCodes = new List<int>();
        
        // 1. Try RAD_SAMI first (archive history)
        if (File.Exists(radSamiPath))
        {
            try
            {
                var opts = new DbfDataReader.DbfDataReaderOptions { Encoding = Encoding.GetEncoding(852), SkipDeletedRecords = true };
                using var reader = new DbfDataReader.DbfDataReader(radSamiPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                while (reader.Read())
                {
                    int rId = Convert.ToInt32(reader.GetValue(cols.IndexOf("RADNIK")));
                    int god = Convert.ToInt32(reader.GetValue(cols.IndexOf("GODINA")));
                    int mes = Convert.ToInt32(reader.GetValue(cols.IndexOf("MESEC")));
                    if (rId == radnikId && god == godina && mes == mesec)
                    {
                        assignedCodes.Add(Convert.ToInt32(reader.GetValue(cols.IndexOf("SAMODOPRIN"))));
                    }
                }
            }
            catch {}
        }
        
        // If not found in archive, try active RAD_SAM
        if (assignedCodes.Count == 0 && File.Exists(radSamPath))
        {
            try
            {
                var opts = new DbfDataReader.DbfDataReaderOptions { Encoding = Encoding.GetEncoding(852), SkipDeletedRecords = true };
                using var reader = new DbfDataReader.DbfDataReader(radSamPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                while (reader.Read())
                {
                    int rId = Convert.ToInt32(reader.GetValue(cols.IndexOf("RADNIK")));
                    if (rId == radnikId)
                    {
                        assignedCodes.Add(Convert.ToInt32(reader.GetValue(cols.IndexOf("SAMODOPRIN"))));
                    }
                }
            }
            catch {}
        }
        
        if (assignedCodes.Count == 0) return 0m;
        
        // 2. Load catalog definitions for these codes
        var catalog = new Dictionary<int, (string Name, decimal Percent, decimal Linear)>();
        
        // Read SAMODOP
        if (File.Exists(samodopPath))
        {
            try
            {
                var opts = new DbfDataReader.DbfDataReaderOptions { Encoding = Encoding.GetEncoding(852), SkipDeletedRecords = true };
                using var reader = new DbfDataReader.DbfDataReader(samodopPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                while (reader.Read())
                {
                    int code = Convert.ToInt32(reader.GetValue(cols.IndexOf("RED_BROJ")));
                    if (assignedCodes.Contains(code))
                    {
                        string name = reader.GetString(cols.IndexOf("NAZIV")).Trim();
                        decimal proc = reader.GetDecimal(cols.IndexOf("PROCENAT"));
                        decimal lin = reader.GetDecimal(cols.IndexOf("LIZNOS"));
                        catalog[code] = (name, proc, lin);
                    }
                }
            }
            catch {}
        }
        
        // Read SAMODOPI (history) if not fully loaded
        if (catalog.Count < assignedCodes.Count && File.Exists(samodopiPath))
        {
            try
            {
                var opts = new DbfDataReader.DbfDataReaderOptions { Encoding = Encoding.GetEncoding(852), SkipDeletedRecords = true };
                using var reader = new DbfDataReader.DbfDataReader(samodopiPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                while (reader.Read())
                {
                    int code = Convert.ToInt32(reader.GetValue(cols.IndexOf("RED_BROJ")));
                    int god = Convert.ToInt32(reader.GetValue(cols.IndexOf("GODINA")));
                    int mes = Convert.ToInt32(reader.GetValue(cols.IndexOf("MESEC")));
                    if (assignedCodes.Contains(code) && god == godina && mes == mesec)
                    {
                        string name = reader.GetString(cols.IndexOf("NAZIV")).Trim();
                        decimal proc = reader.GetDecimal(cols.IndexOf("PROCENAT"));
                        decimal lin = reader.GetDecimal(cols.IndexOf("LIZNOS"));
                        catalog[code] = (name, proc, lin);
                    }
                }
            }
            catch {}
        }
        
        // 3. Calculate dynamic amounts
        foreach (int code in assignedCodes)
        {
            if (catalog.TryGetValue(code, out var def))
            {
                decimal iznos = 0m;
                if (def.Percent > 0)
                {
                    iznos = Math.Round(netoBase * def.Percent / 100m, 2);
                }
                else
                {
                    iznos = def.Linear;
                }
                
                if (iznos > 0)
                {
                    sum += iznos;
                    generated.Add(new Samodoprinosi
                    {
                        RadnikId = radnikId,
                        Godina = godina,
                        Mesec = mesec,
                        Iznos = iznos,
                        Opis = def.Name
                    });
                }
            }
        }
        
        return sum;
    }
}
