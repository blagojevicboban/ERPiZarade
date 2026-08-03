using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

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

    /// <param name="saObustavama">
    /// Da li se od neta odbijaju rate kredita i samodoprinos (Faza 2.2). Netačno je samo za
    /// isplate koje nisu konačna zarada — akontaciju, bonus i 13. platu — jer bi radnik inače
    /// istu ratu platio više puta u istom mesecu. Podrazumevano je tačno, pa se obračun
    /// meseca sa jednom isplatom ne menja.
    /// </param>
    public ObracunPlate Calculate(Radnik radnik, RadniSat sati, int godina, int mesec, decimal vrednostBoda, int fondCasova, bool saObustavama = true)
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

        // Koristi veću vrednost između dinamički izračunate i one koja je eksplicitno uneta u karton radnika (npr. prenesene iz DBF ili prethodnih perioda)
        if (radnik.MinuliRadGodine > yearsOfTenure)
        {
            yearsOfTenure = radnik.MinuliRadGodine;
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
        decimal procNedel = pParams != null ? pParams.ProcNedel : 0.00m;

        // 3. workedHours — ukupno efektivnih sati (za ostale doplatke)
        decimal workedHours = sati.RedovniSati + sati.PrekovremeneSati + sati.RadPraznikomSati + sati.NocniSati + sati.RadNedeljomSati;

        // Minuli rad se po Zakonu o radu čl. 108 obračunava ISKLJUČIVO na osnovnu zaradu
        // (redovni sati × cena sata). Prekovremeni, noćni i praznik NE ulaze u osnov.
        decimal regularHoursForMinuli = sati.RedovniSati;
        decimal netoZarOsnovica = regularHoursForMinuli * hourlyBase;
        decimal brutoMinuliRad = Math.Round(netoZarOsnovica * (procMinul / 100m) * yearsOfTenure, 2);
        decimal min_po_cas = regularHoursForMinuli > 0 ? brutoMinuliRad / regularHoursForMinuli : 0m;

        // 12-month average hourly rate from database (or calculated dynamically)
        decimal prosek = sati.Prosek > 0 ? sati.Prosek : IzracunajProsekRadnika(radnik.Id, godina, mesec);

        // 4. Wage lines (gross parts)
        decimal brutoRedovni = sati.RedovniSati * hourlyBase;
        decimal brutoBolovanje = sati.BolovanjeSati * prosek * (procBolov / 100m); // sick leave base
        decimal brutoPrekovremeni = sati.PrekovremeneSati * (1m + procPreko / 100m) * (hourlyBase + min_po_cas); // overtime bonus + base
        decimal brutoGodisnji = sati.GodisnjiOdmorSati * prosek; // Paid at Prosek
        decimal brutoPraznik = sati.RadPraznikomSati * (1m + procDrzav / 100m) * (hourlyBase + min_po_cas); // worked holiday (DRZAVNI) paid at hourly base + premium + minuli rad
        decimal brutoNeradniPraznik = sati.DrzavniPraznikSati * prosek; // neradni holiday (NERDRZAVNI) paid at prosek
        decimal brutoNocni = sati.NocniSati * (1m + procNocni / 100m) * (hourlyBase + min_po_cas); // night shift bonus + base
        decimal brutoNedelja = sati.RadNedeljomSati * (1m + procNedel / 100m) * hourlyBase; // Sunday premium + hourly base

        // Obračun stimulacije (procentualni bonus na bazi redovnog rada, prekovremenog, praznika, noćnog rada i rada nedeljom)
        decimal brutoStimulacija = Math.Round((brutoRedovni + brutoPrekovremeni + brutoPraznik + brutoNocni + brutoNedelja) * (sati.Stimulacija / 100m), 2);

        // Naknade koje su se isplaćivale odvojeno, a zapravo su bruto (topli obrok, regres)
        decimal topliObrokIznos = Math.Round((decimal)sati.TopliObrokDani, 2);
        decimal regresIznos = sati.RegresIznos;

        // Ostali bruto elementi vezani za radne sate (bolovanje 100%, plaćeno odsustvo, plaćeno zakonski, bolovanje preko 60 dana, porodiljsko)
        decimal brutoBolovanje100 = sati.Bolovanje100Sati * prosek;
        decimal brutoPlacenoOdsustvo = sati.PlacenoOdsustvoSati * prosek;
        decimal brutoPlacenoZakonski = sati.PlacenoZakonskiSati * prosek;
        decimal brutoBolovanjePreko60 = sati.BolovanjePreko60Sati * prosek;
        decimal brutoPorodiljsko = sati.PorodiljskoOdsustvoSati * prosek;

        decimal totalBruto = brutoRedovni + brutoBolovanje + brutoPrekovremeni + brutoGodisnji + brutoPraznik + brutoNeradniPraznik + brutoNocni + brutoNedelja + brutoMinuliRad + brutoStimulacija
                           + topliObrokIznos + regresIznos
                           + brutoBolovanje100 + brutoPlacenoOdsustvo + brutoPlacenoZakonski + brutoBolovanjePreko60 + brutoPorodiljsko
                           + sati.Varijabila;

        // Primanja uneta kroz šifarnik (prevoz, jubilarna nagrada, solidarna pomoć…).
        // Prekoračenje neoporezivog limita po zakonu postaje oporezivo, pa se dodaje u
        // osnovicu; neoporezivi deo se samo isplaćuje i ne ulazi ni u porez ni u doprinose.
        var unetaPrimanja = UcitajUnetaPrimanja(radnik.Id, godina, mesec);

        decimal neoporezivoZaIsplatu = unetaPrimanja.Sum(p => p.NeoporeziviDeo);
        decimal dodatnoUOsnovicuDoprinosa = unetaPrimanja.Where(p => p.UlaziUOsnovicuDoprinosa).Sum(p => p.OporeziviDeo);
        decimal dodatnoSamoOporezivo = unetaPrimanja.Where(p => !p.UlaziUOsnovicuDoprinosa).Sum(p => p.OporeziviDeo);

        totalBruto += dodatnoUOsnovicuDoprinosa;

        // Load contribution rates and bases from database
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

        // Try to read NajnizaOsnovica and NajvisaOsnovica from database doprinosi (e.g. PIO / RedniBroj 1)
        decimal minBase = DefaultMinContributionBase;
        decimal maxBase = 0m;

        if (dbDoprinosi.Any())
        {
            var pioRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 1);
            if (pioRec != null)
            {
                if (pioRec.NajnizaOsnovica > 0) minBase = pioRec.NajnizaOsnovica;
                if (pioRec.NajvisaOsnovica > 0) maxBase = pioRec.NajvisaOsnovica;
            }
        }

        // 5. Tax parameters
        decimal taxRate = DefaultTaxRate;
        decimal taxExemption = DefaultPoreskoOslobodjenje;

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
        decimal totalHours = sati.RedovniSati + sati.BolovanjeSati + sati.PrekovremeneSati + sati.GodisnjiOdmorSati + sati.DrzavniPraznikSati + sati.RadPraznikomSati + sati.NocniSati + sati.RadNedeljomSati
                             + sati.Bolovanje100Sati + sati.PlacenoOdsustvoSati + sati.PlacenoZakonskiSati + sati.BolovanjePreko60Sati + sati.PorodiljskoOdsustvoSati;
        decimal workFactor = fondCasova > 0 ? totalHours / fondCasova : 1.0m;
        if (workFactor > 1.0m) workFactor = 1.0m;

        decimal scaledExemption = taxExemption * workFactor;
        // Primanja koja se oporezuju a ne ulaze u osnovicu doprinosa dodaju se samo poreskoj osnovici.
        decimal poreskaOsnovica = Math.Max(0, totalBruto + dodatnoSamoOporezivo - scaledExemption);
        decimal porez = poreskaOsnovica * taxRate;

        // 6. Social security contributions bases on Employee class (platni razredi - minimum gross bases)
        decimal razredLimitNormal = minBase;
        decimal razredLimitPio = minBase;

        var platniRazredi = _db.PlatniRazredi.FirstOrDefault();

        if (int.TryParse(radnik.Kategorija, out int razredVal))
        {
            if (razredVal == 9)
            {
                razredLimitNormal = 0m;
                razredLimitPio = 0m;
            }
            else if (razredVal >= 1 && razredVal <= 8 && platniRazredi != null)
            {
                // Dynamic lookup matching Clipper: R{step} and P{step}
                razredLimitNormal = razredVal switch
                {
                    1 => platniRazredi.R1,
                    2 => platniRazredi.R2,
                    3 => platniRazredi.R3,
                    4 => platniRazredi.R4,
                    5 => platniRazredi.R5,
                    6 => platniRazredi.R6,
                    7 => platniRazredi.R7,
                    8 => platniRazredi.R8,
                    _ => minBase
                };

                razredLimitPio = razredVal switch
                {
                    1 => platniRazredi.P1,
                    2 => platniRazredi.P2,
                    3 => platniRazredi.P3,
                    4 => platniRazredi.P4,
                    5 => platniRazredi.P5,
                    6 => platniRazredi.P6,
                    7 => platniRazredi.P7,
                    8 => platniRazredi.P8,
                    _ => minBase
                };
            }
        }
        else if (radnik.Kategorija == "9")
        {
            razredLimitNormal = 0m;
            razredLimitPio = 0m;
        }

        decimal granica = razredLimitNormal * workFactor;
        decimal granicaPIO = razredLimitPio * workFactor;

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

        // Apply highest base clamp (Najviša bruto osnovica) from above
        if (maxBase > 0)
        {
            if (brutoOsn > maxBase) brutoOsn = maxBase;
            if (brutPioOsn > maxBase) brutPioOsn = maxBase;
        }

        // Standard rates variables initialized to defaults
        decimal empPio = DefaultEmployeePioRate;
        decimal empZdr = DefaultEmployeeZdravstvoRate;
        decimal empNez = DefaultEmployeeNezaposlenostRate;

        decimal bossPio = DefaultEmployerPioRate;
        decimal bossZdr = DefaultEmployerZdravstvoRate;
        decimal bossNez = DefaultEmployerNezaposlenostRate;

        // Dinamička inicijalizacija stopa za poslodavca na osnovu perioda ukoliko nema vrednosti u bazi
        if (godina >= 2023)
        {
            bossPio = 0.1000m;
            bossNez = 0.0000m;
        }
        else if (godina == 2022)
        {
            bossPio = 0.1100m;
            bossNez = 0.0000m;
        }
        else if (godina >= 2020 || (godina == 2019 && mesec == 12))
        {
            bossPio = 0.1150m;
            bossNez = 0.0000m;
        }
        else
        {
            bossPio = 0.1200m;
            bossNez = 0.0075m;
        }

        // Overlay with database rates if found
        if (dbDoprinosi.Any())
        {
            var pioRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 1);
            if (pioRec != null)
            {
                empPio = pioRec.ProcRadn / 100m;
                if (pioRec.ProcPosl > 0) bossPio = pioRec.ProcPosl / 100m;
            }

            var zdrRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 2);
            if (zdrRec != null)
            {
                empZdr = zdrRec.ProcRadn / 100m;
                if (zdrRec.ProcPosl > 0) bossZdr = zdrRec.ProcPosl / 100m;
            }

            var nezRec = dbDoprinosi.FirstOrDefault(d => d.RedniBroj == 3);
            if (nezRec != null)
            {
                empNez = nezRec.ProcRadn / 100m;
                if (nezRec.ProcPosl > 0) bossNez = nezRec.ProcPosl / 100m;
            }
        }

        if (radnik.StopaPio > 0) empPio = radnik.StopaPio;
        if (radnik.StopaZdravstvo > 0) empZdr = radnik.StopaZdravstvo;
        if (radnik.StopaNezaposlenost > 0) empNez = radnik.StopaNezaposlenost;

        // Penzioneri (radno mesto počinje sa "109") — nema doprinosa za nezaposlenost
        bool jePenzioner = !string.IsNullOrWhiteSpace(radnik.Radno_Mesto)
                           && radnik.Radno_Mesto.TrimStart().StartsWith("109");
        if (jePenzioner)
        {
            empNez = 0m;
            bossNez = 0m;
        }

        decimal dopPioRadnik = brutPioOsn * empPio;
        decimal dopZdrRadnik = brutoOsn * empZdr;
        decimal dopNezRadnik = brutoOsn * empNez;

        decimal dopPioPoslodavac = brutPioOsn * bossPio;
        decimal dopZdrPoslodavac = brutoOsn * bossZdr;
        decimal dopNezPoslodavac = brutoOsn * bossNez;

        // 7. Fetch active credits and deductions
        // Obustave su mesečne, pa ih nosi samo konačna isplata; vidi `saObustavama`.
        decimal kreditiObustava = 0m;
        decimal samodoprinosiIznos = 0m;

        if (saObustavama)
        {
            var targetDate = new DateTime(godina, mesec, 1);
            var activeKrediti = _db.Krediti.Where(k => k.RadnikId == radnik.Id && k.Aktivan && k.DatumPocetka <= targetDate).ToList();
            foreach (var k in activeKrediti)
            {
                decimal rata = Math.Min(k.MesecnaRata, k.OstatakDuga);
                kreditiObustava += rata;
            }

            var activeSamodoprinosi = _db.Samodoprinosi
                .Where(s => s.RadnikId == radnik.Id && s.Godina == godina && s.Mesec == mesec)
                .ToList();
            foreach (var s in activeSamodoprinosi)
            {
                samodoprinosiIznos += s.Iznos;
            }
        }

        // 8. Topli obrok i regres su već uključeni u totalBruto

        // 9. Net salary calculation (doprinosi i porez se odbijaju od ukupnog bruto iznosa)
        // Poreska olakšica se prepoznaje po OL oznaci u SVP šifri iz kartona radnika.
        var olaksica = new OlaksicaService(_db).Utvrdi(
            radnik, radnik.Radno_Mesto, godina, mesec,
            porez, dopPioRadnik + dopZdrRadnik + dopNezRadnik);

        // Samo oslobođenje umanjuje ono što se plaća. Kod povraćaja se plaća pun iznos, pa se
        // posebnim zahtevom traži natrag — iznosi se beleže, ali se ništa ne odbija.
        if (olaksica is { UmanjujeUplatu: true })
        {
            porez = Math.Max(0m, porez - olaksica.Porez);

            decimal ukupnoDoprinosa = dopPioRadnik + dopZdrRadnik + dopNezRadnik;
            if (ukupnoDoprinosa > 0m)
            {
                // Umanjenje se raspoređuje srazmerno, da odnos među doprinosima ostane isti.
                decimal faktor = Math.Max(0m, ukupnoDoprinosa - olaksica.Doprinosi) / ukupnoDoprinosa;
                dopPioRadnik *= faktor;
                dopZdrRadnik *= faktor;
                dopNezRadnik *= faktor;
            }
        }

        decimal totalEmployeeDeductions = dopPioRadnik + dopZdrRadnik + dopNezRadnik + porez;

        // Neoporezivi deo se isplaćuje radniku u punom iznosu — nije bio ni u bruto iznosu
        // ni u osnovicama, pa se dodaje tek ovde. Deo koji se samo oporezuje već je oporezovan
        // gore, a isplaćuje se uz zaradu.
        decimal netoIsplata = totalBruto + dodatnoSamoOporezivo + neoporezivoZaIsplatu
                              - totalEmployeeDeductions - kreditiObustava - samodoprinosiIznos;
        if (netoIsplata < 0m) netoIsplata = 0m;

        var obracun = new ObracunPlate
        {
            RadnikId = radnik.Id,
            Radnik = radnik,
            Godina = godina,
            Mesec = mesec,
            BrutoZarada = Math.Round(totalBruto - brutoBolovanje, 2),
            BrutoBolovanje = Math.Round(brutoBolovanje, 2),
            BrutoNaknade = Math.Round(brutoPrekovremeni + brutoPraznik + brutoNocni + brutoNedelja, 2),
            BrutoStimulacija = brutoStimulacija,
            BrutoMinuliRad = Math.Round(brutoMinuliRad, 2),
            
            // Legacy detaljne stavke koje su falile i resetovale se na 0
            NetoZar = Math.Round(brutoRedovni, 2),
            NetoNerd = Math.Round(brutoNeradniPraznik, 2),
            NetoGOd = Math.Round(brutoGodisnji, 2),
            NetoBol = Math.Round(brutoBolovanje, 2),
            NetoNocni = Math.Round(brutoNocni, 2),
            NetoPrek = Math.Round(brutoPrekovremeni, 2),
            NetoDrza = Math.Round(brutoPraznik, 2),
            NetoNede = Math.Round(brutoNedelja, 2),
            
            NetoB100 = Math.Round(brutoBolovanje100, 2),
            NetoPlac = Math.Round(brutoPlacenoOdsustvo, 2),
            NetoPlZ = Math.Round(brutoPlacenoZakonski, 2),
            
            Neto = Math.Round(totalBruto, 2),
            
            DoprinosPioRadnik = Math.Round(dopPioRadnik, 2),
            DoprinosZdravstvoRadnik = Math.Round(dopZdrRadnik, 2),
            DoprinosNezaposlenostRadnik = Math.Round(dopNezRadnik, 2),

            DoprinosPioPoslodavac = Math.Round(dopPioPoslodavac, 2),
            DoprinosZdravstvoPoslodavac = Math.Round(dopZdrPoslodavac, 2),
            DoprinosNezaposlenostPoslodavac = Math.Round(dopNezPoslodavac, 2),

            PorezNaDohodak = Math.Round(porez, 2),
            PoreskaOsnovica = Math.Round(poreskaOsnovica, 2),
            LicniOdbitak = Math.Round(scaledExemption, 2),
            KreditObustava = Math.Round(kreditiObustava, 2),
            Samodoprinosi = Math.Round(samodoprinosiIznos, 2),
            OstaliOdbici = 0m,
            NetoIsplata = Math.Round(netoIsplata, 2),

            // Naknade (topli obrok, regres) — prikazane odvojeno u listiciću
            NetoTo = Math.Round(topliObrokIznos, 2),
            TopliObrokIznos = Math.Round(topliObrokIznos, 2),
            NetoReg = Math.Round(regresIznos, 2),

            RedovniSati = sati.RedovniSati,
            BolovanjeSati = sati.BolovanjeSati,
            PrekovremeneSati = sati.PrekovremeneSati,
            GodisnjioOdmorSati = sati.GodisnjiOdmorSati,
            DrzavniPraznikSati = sati.DrzavniPraznikSati,
            NocniSati = sati.NocniSati,
            RadPraznikomSati = sati.RadPraznikomSati,
            NedeljaSati = sati.RadNedeljomSati,
            
            SmenskiSati = sati.SmenskiSati,
            NocniRadPraznikomSati = sati.NocniRadPraznikomSati,
            PlacenoOdsustvoSati = sati.PlacenoOdsustvoSati,
            PlacenoOdsustvoSatiLegacy = sati.PlacenoOdsustvoSati,
            PlacenoZakonskiSatiLegacy = sati.PlacenoZakonskiSati,
            BolovanjePreko60SatiLegacy = sati.BolovanjePreko60Sati,
            PorodiljskoOdsustvoSatiLegacy = sati.PorodiljskoOdsustvoSati,
            Bolovanje100SatiLegacy = sati.Bolovanje100Sati,
            
            Prosek = Math.Round(prosek, 2),
            CenaSataRedovan = Math.Round(hourlyBase, 5),
            CenaSataMinuliRad = Math.Round(min_po_cas, 5),
            Varijabila = Math.Round(sati.Varijabila, 2),
            DatumObracuna = DateTime.Now,
            Napomena = $"Obračun kreiran {DateTime.Now:dd.MM.yyyy HH:mm}",

            // Mapped legacy columns
            Koeficijent = radnik.Koeficijent,
            MinuliRadGodine = yearsOfTenure,
            Kategorija = radnik.Kategorija,
            BrojRadneJedinice = radnik.BrojRadneJedinice,
            FondSatiMesecni = fondCasova,
            BrutoOsnovica = Math.Round(brutoOsn, 2),
            BrutoPioOsnovica = Math.Round(brutPioOsn, 2),
            Operativni = radnik.Operativni,
            MinimalnaPlataOsnovica = Math.Round(granica, 2),

            OlaksicaOznaka = olaksica?.Olaksica.Sifra ?? "",
            OlaksicaPorez = olaksica?.Porez ?? 0m,
            OlaksicaDoprinosi = olaksica?.Doprinosi ?? 0m,
            OlaksicaUmanjujeUplatu = olaksica?.UmanjujeUplatu ?? false
        };

        // Razlaganje bruto iznosa po vrstama primanja (Faza 2.1). Iznosi iznad se ne
        // menjaju — stavke su verno razlaganje istog zbira, pa obračun daje identičan
        // rezultat kao pre uvođenja šifarnika.
        PopuniStavke(obracun, new BrutoKomponente
        {
            OsnovnaZarada = brutoRedovni,
            MinuliRad = brutoMinuliRad,
            Prekovremeni = brutoPrekovremeni,
            NocniRad = brutoNocni,
            RadPraznikom = brutoPraznik,
            NeradniPraznik = brutoNeradniPraznik,
            RadNedeljom = brutoNedelja,
            GodisnjiOdmor = brutoGodisnji,
            Bolovanje = brutoBolovanje,
            Bolovanje100 = brutoBolovanje100,
            BolovanjePreko30 = brutoBolovanjePreko60,
            Porodiljsko = brutoPorodiljsko,
            PlacenoOdsustvo = brutoPlacenoOdsustvo,
            PlacenoZakonski = brutoPlacenoZakonski,
            Stimulacija = brutoStimulacija,
            TopliObrok = topliObrokIznos,
            Regres = regresIznos,
            BrutoDodatak = sati.Varijabila
        }, sati);

        // Uneta primanja dobijaju svoju stavku sa vidljivom podelom na neoporezivi deo i
        // prekoračenje limita, koje je oporezovano.
        foreach (var primanje in unetaPrimanja)
        {
            obracun.Stavke.Add(new ObracunStavka
            {
                VrstaPrimanjaId = primanje.VrstaPrimanjaId,
                Iznos = Math.Round(primanje.Iznos, 2),
                OporeziviDeo = Math.Round(primanje.OporeziviDeo, 2)
            });
        }

        return obracun;
    }

    /// <summary>
    /// Koliko od iznosa ulazi u poresku osnovicu.
    ///
    /// Oporeziva vrsta se oporezuje u punom iznosu. Kod neoporezive, oporezivo je samo
    /// prekoračenje limita — a limit <b>nula znači da gornje granice nema</b>, pa je ceo
    /// iznos neoporeziv. Bez tog izuzetka bi `Iznos − 0` dalo suprotno od onoga što polje
    /// „neoporezivo" znači.
    /// </summary>
    internal static decimal OporeziviDeo(decimal iznos, VrstaPrimanja vrsta)
    {
        if (vrsta.Oporezivo) return iznos;
        if (vrsta.NeoporeziviLimit <= 0m) return 0m;

        return Math.Max(0m, iznos - vrsta.NeoporeziviLimit);
    }

    /// <summary>Uneto primanje sa već izvršenom podelom na neoporezivi i oporezivi deo.</summary>
    private sealed class PodeljenoPrimanje
    {
        public required int VrstaPrimanjaId { get; init; }
        public required decimal Iznos { get; init; }
        public required decimal OporeziviDeo { get; init; }
        public required bool UlaziUOsnovicuDoprinosa { get; init; }

        public decimal NeoporeziviDeo => Iznos - OporeziviDeo;
    }

    /// <summary>
    /// Učitava primanja uneta za radnika u periodu i deli svako na neoporezivi i oporezivi
    /// deo prema šifarniku.
    ///
    /// Pravilo: kod oporezive vrste ceo iznos je oporeziv. Kod neoporezive, oporezivo je samo
    /// <b>prekoračenje</b> neoporezivog limita. Limit nula znači da gornje granice nema —
    /// takva vrsta se prijavljuje u kontrolnim proverama, da se ne bi tiho izostavio limit
    /// koji propis predviđa.
    /// </summary>
    private List<PodeljenoPrimanje> UcitajUnetaPrimanja(int radnikId, int godina, int mesec)
    {
        try
        {
            return _db.UnetaPrimanja
                .AsNoTracking()
                .Include(p => p.VrstaPrimanja)
                .Where(p => p.RadnikId == radnikId && p.Godina == godina && p.Mesec == mesec)
                .ToList()
                .Where(p => p.VrstaPrimanja != null && p.Iznos != 0m)
                .Select(p => new PodeljenoPrimanje
                {
                    VrstaPrimanjaId = p.VrstaPrimanjaId,
                    Iznos = p.Iznos,
                    OporeziviDeo = OporeziviDeo(p.Iznos, p.VrstaPrimanja),
                    UlaziUOsnovicuDoprinosa = p.VrstaPrimanja.UlaziUOsnovicuDoprinosa
                })
                .ToList();
        }
        catch
        {
            // Baza starije verzije još nema tabelu unetih primanja — obračun radi kao i pre.
            return [];
        }
    }

    /// <summary>
    /// Bruto komponente iz kojih se sastoji obračun, imenovane onako kako ih zove šifarnik.
    /// Postoji da razlaganje na stavke ne zavisi od redosleda argumenata.
    /// </summary>
    private sealed class BrutoKomponente
    {
        public decimal OsnovnaZarada { get; init; }
        public decimal MinuliRad { get; init; }
        public decimal Prekovremeni { get; init; }
        public decimal NocniRad { get; init; }
        public decimal RadPraznikom { get; init; }
        public decimal NeradniPraznik { get; init; }
        public decimal RadNedeljom { get; init; }
        public decimal GodisnjiOdmor { get; init; }
        public decimal Bolovanje { get; init; }
        public decimal Bolovanje100 { get; init; }
        public decimal BolovanjePreko30 { get; init; }
        public decimal Porodiljsko { get; init; }
        public decimal PlacenoOdsustvo { get; init; }
        public decimal PlacenoZakonski { get; init; }
        public decimal Stimulacija { get; init; }
        public decimal TopliObrok { get; init; }
        public decimal Regres { get; init; }
        public decimal BrutoDodatak { get; init; }
    }

    /// <summary>
    /// Razlaže bruto iznos na stavke po vrstama primanja. Ne menja nijedan obračunati iznos —
    /// zbir stavki jednak je ukupnom bruto iznosu obračuna.
    ///
    /// Vrste se traže po šifri iz šifarnika; ako šifarnik nije popunjen, stavke se preskaču
    /// i obračun ostaje ispravan kao i pre uvođenja Faze 2.1.
    /// </summary>
    private void PopuniStavke(ObracunPlate obracun, BrutoKomponente komponente, RadniSat sati)
    {
        Dictionary<string, int> sifarnik;
        try
        {
            sifarnik = _db.VrstePrimanja
                .AsNoTracking()
                .ToDictionary(v => v.Sifra, v => v.VrstaPrimanjaId, StringComparer.Ordinal);
        }
        catch
        {
            return;
        }

        if (sifarnik.Count == 0) return;

        void Dodaj(string sifra, decimal iznos, int satiStavke = 0)
        {
            // Primanje bez iznosa i bez sati ne stoji na listiću — nula redova nema šta da kaže.
            if (iznos == 0m && satiStavke == 0) return;
            if (!sifarnik.TryGetValue(sifra, out int vrstaId)) return;

            decimal zaokruzen = Math.Round(iznos, 2);
            obracun.Stavke.Add(new ObracunStavka
            {
                VrstaPrimanjaId = vrstaId,
                Sati = satiStavke,
                Iznos = zaokruzen,
                // Komponente zarade su oporezive u punom iznosu.
                OporeziviDeo = zaokruzen
            });
        }

        Dodaj(VrstePrimanjaSeed.OsnovnaZarada,    komponente.OsnovnaZarada,    sati.RedovniSati);
        Dodaj(VrstePrimanjaSeed.MinuliRad,        komponente.MinuliRad);
        Dodaj(VrstePrimanjaSeed.Prekovremeni,     komponente.Prekovremeni,     sati.PrekovremeneSati);
        Dodaj(VrstePrimanjaSeed.NocniRad,         komponente.NocniRad,         sati.NocniSati);
        Dodaj(VrstePrimanjaSeed.RadPraznikom,     komponente.RadPraznikom,     sati.RadPraznikomSati);
        Dodaj(VrstePrimanjaSeed.NeradniPraznik,   komponente.NeradniPraznik,   sati.DrzavniPraznikSati);
        Dodaj(VrstePrimanjaSeed.RadNedeljom,      komponente.RadNedeljom,      sati.RadNedeljomSati);
        Dodaj(VrstePrimanjaSeed.GodisnjiOdmor,    komponente.GodisnjiOdmor,    sati.GodisnjiOdmorSati);
        Dodaj(VrstePrimanjaSeed.Bolovanje,        komponente.Bolovanje,        sati.BolovanjeSati);
        Dodaj(VrstePrimanjaSeed.Bolovanje100,     komponente.Bolovanje100,     sati.Bolovanje100Sati);
        Dodaj(VrstePrimanjaSeed.BolovanjePreko30, komponente.BolovanjePreko30, sati.BolovanjePreko60Sati);
        Dodaj(VrstePrimanjaSeed.Porodiljsko,      komponente.Porodiljsko,      sati.PorodiljskoOdsustvoSati);
        Dodaj(VrstePrimanjaSeed.PlacenoOdsustvo,  komponente.PlacenoOdsustvo,  sati.PlacenoOdsustvoSati);
        Dodaj(VrstePrimanjaSeed.PlacenoZakonski,  komponente.PlacenoZakonski,  sati.PlacenoZakonskiSati);
        Dodaj(VrstePrimanjaSeed.Stimulacija,      komponente.Stimulacija);
        Dodaj(VrstePrimanjaSeed.TopliObrok,       komponente.TopliObrok);
        Dodaj(VrstePrimanjaSeed.Regres,           komponente.Regres);
        Dodaj(VrstePrimanjaSeed.BrutoDodatak,     komponente.BrutoDodatak);
    }

    public decimal IzracunajProsekRadnika(int radnikId, int godina, int mesec)
    {
        var targetRadnik = _db.Radnici.Find(radnikId);
        if (targetRadnik == null) return 0m;
        int targetBrojRadnika = targetRadnik.BrojRadnika;

        int targetVal = godina * 12 + mesec;
        int minVal = targetVal - 12;

        decimal psumbr = 0m;
        decimal psumcas = 0m;

        // Učitaj samo obračune i radne sate za tog radnika u tačnom opsegu 12 meseci direktno iz baze (bez tracking-a)
        var obracuni = _db.ObracuniPlata
            .AsNoTracking()
            .Where(o => o.Radnik.BrojRadnika == targetBrojRadnika && 
                        (o.Godina * 12 + o.Mesec >= minVal && o.Godina * 12 + o.Mesec < targetVal))
            .ToList();

        var satiLista = _db.RadniSati
            .AsNoTracking()
            .Where(s => s.Radnik.BrojRadnika == targetBrojRadnika && 
                        (s.Godina * 12 + s.Mesec >= minVal && s.Godina * 12 + s.Mesec < targetVal))
            .ToList()
            .GroupBy(s => new { s.Godina, s.Mesec })
            .ToDictionary(g => (g.Key.Godina, g.Key.Mesec), g => g.First());

        foreach (var ob in obracuni)
        {
            decimal casovi = 0;
            if (satiLista.TryGetValue((ob.Godina, ob.Mesec), out var s))
            {
                casovi = s.RedovniSati + s.PrekovremeneSati + s.RadPraznikomSati + s.NocniSati + s.RadNedeljomSati;
            }
            else
            {
                casovi = ob.RedovniSati + ob.PrekovremeneSati + ob.RadPraznikomSati + ob.NocniSati + ob.NedeljaSati;
            }

            // Razdvajanje bruto redovnog dela (bez bolovanja, odmora, neradnih praznika i plaćenih odsustava)
            decimal totalGross = ob.BrutoZarada + ob.BrutoBolovanje;
            decimal nonWorkedGross = ob.BrutoBolovanje + ob.NetoGOd + ob.NetoNerd + ob.NetoB100 + ob.NetoPlac + ob.NetoPlZ 
                                     + (ob.BolovanjePreko60SatiLegacy * ob.Prosek) 
                                     + (ob.PorodiljskoOdsustvoSatiLegacy * ob.Prosek);
            decimal regularGross = Math.Max(0, totalGross - nonWorkedGross);

            psumbr += regularGross;
            psumcas += casovi;
        }

        if (psumcas > 0)
        {
            return Math.Round(psumbr / psumcas, 4);
        }

        // Fallback na trenutnu osnovnu satnicu ako nema istorije
        decimal fondSati = 176m;
        decimal hourlyBase = 0m;
        if (targetRadnik.Koeficijent > 0)
        {
            hourlyBase = (targetRadnik.Koeficijent * 1860.34m) / fondSati;
        }
        else if (targetRadnik.OsnovnaPlata > 0)
        {
            hourlyBase = targetRadnik.OsnovnaPlata / fondSati;
        }
        return Math.Round(hourlyBase, 4);
    }

}
