using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DbfDataReader;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

namespace PlataInspect
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string dbfDir = @"C:\PLATA\PLATA\KOR28";
            string bazeDir = @"C:\PLATA\PlataSistem\Baze";

            Console.WriteLine($"DBF Directory: {dbfDir}");
            Console.WriteLine($"Baze Directory: {bazeDir}");

            if (!Directory.Exists(dbfDir) || !Directory.Exists(bazeDir))
            {
                Console.WriteLine("Required directories do not exist!");
                return;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cp852 = Encoding.GetEncoding(852);
            var opts = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };

            // 1. Read active period from MESEC.DBF
            int aktivnaGodina = DateTime.Now.Year;
            int aktivniMesec = DateTime.Now.Month;
            string mesecDbf = Path.Combine(dbfDir, "MESEC.DBF");
            if (File.Exists(mesecDbf))
            {
                using var reader = new DbfDataReader.DbfDataReader(mesecDbf, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                if (reader.Read())
                {
                    int g = Convert.ToInt32(reader.GetValue(cols.IndexOf("GODINA")));
                    int m = Convert.ToInt32(reader.GetValue(cols.IndexOf("MESEC")));
                    if (g > 0) aktivnaGodina = g;
                    if (m > 0) aktivniMesec = m;
                }
            }
            Console.WriteLine($"Active period from MESEC.DBF: {aktivniMesec}.{aktivnaGodina}");

            // 2. Load RadniSati Varijabila data from DBF
            var radniSatiVarijabile = new Dictionary<(int BrojRadnika, int Godina, int Mesec), decimal>();
            
            // RAD_SATI.DBF (active)
            string radSatiPath = Path.Combine(dbfDir, "RAD_SATI.DBF");
            if (File.Exists(radSatiPath))
            {
                using var reader = new DbfDataReader.DbfDataReader(radSatiPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                while (reader.Read())
                {
                    int redBroj = Convert.ToInt32(reader.GetValue(cols.IndexOf("RED_BROJ")));
                    decimal varVal = reader.GetDecimal(cols.IndexOf("VARIJABILA"));
                    if (redBroj > 0 && varVal > 0)
                    {
                        radniSatiVarijabile[(redBroj, aktivnaGodina, aktivniMesec)] = varVal;
                    }
                }
            }

            // RADSATII.DBF (historical)
            string radsatiiPath = Path.Combine(dbfDir, "RADSATII.DBF");
            if (File.Exists(radsatiiPath))
            {
                using var reader = new DbfDataReader.DbfDataReader(radsatiiPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                bool hasGodina = cols.Contains("GODINA");
                bool hasMesec = cols.Contains("MESEC");
                int nonZeroCount = 0;
                int recordCount = 0;

                while (reader.Read())
                {
                    recordCount++;
                    int redBroj = Convert.ToInt32(reader.GetValue(cols.IndexOf("RED_BROJ")));
                    int god = hasGodina ? Convert.ToInt32(reader.GetValue(cols.IndexOf("GODINA"))) : aktivnaGodina;
                    int mes = hasMesec ? Convert.ToInt32(reader.GetValue(cols.IndexOf("MESEC"))) : aktivniMesec;
                    decimal varVal = reader.GetDecimal(cols.IndexOf("VARIJABILA"));

                    if (varVal != 0)
                    {
                        nonZeroCount++;
                        if (nonZeroCount <= 10)
                        {
                            Console.WriteLine($"  Non-zero match: Radnik={redBroj}, Period={mes}.{god}, Varijabila={varVal}");
                        }
                    }

                    if (redBroj > 0 && varVal > 0 && god > 0 && mes > 0)
                    {
                        radniSatiVarijabile[(redBroj, god, mes)] = varVal;
                    }
                }
                Console.WriteLine($"\nRADSATII.DBF Statistics:");
                Console.WriteLine($"  Total records: {recordCount}");
                Console.WriteLine($"  Non-zero VARIJABILA records: {nonZeroCount}");
            }
            Console.WriteLine($"Loaded {radniSatiVarijabile.Count} Varijabila values from RAD_SATI / RADSATII.DBF");

            // 3. Load ObracuniPlata Varijabila data from DBF
            var obracuniVarijabile = new Dictionary<(int BrojRadnika, int Godina, int Mesec), decimal>();
            
            // OBRACUN.DBF (active)
            string obracunPath = Path.Combine(dbfDir, "OBRACUN.DBF");
            if (File.Exists(obracunPath))
            {
                using var reader = new DbfDataReader.DbfDataReader(obracunPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                while (reader.Read())
                {
                    int redBroj = Convert.ToInt32(reader.GetValue(cols.IndexOf("RED_BROJ")));
                    decimal varVal = reader.GetDecimal(cols.IndexOf("VARIJABILA"));
                    if (redBroj > 0 && varVal > 0)
                    {
                        obracuniVarijabile[(redBroj, aktivnaGodina, aktivniMesec)] = varVal;
                    }
                }
            }

            // OBRACUNI.DBF (historical)
            string obracuniPath = Path.Combine(dbfDir, "OBRACUNI.DBF");
            if (File.Exists(obracuniPath))
            {
                using var reader = new DbfDataReader.DbfDataReader(obracuniPath, opts);
                var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
                bool hasGodina = cols.Contains("GODINA");
                bool hasMesec = cols.Contains("MESEC");
                while (reader.Read())
                {
                    int redBroj = Convert.ToInt32(reader.GetValue(cols.IndexOf("RED_BROJ")));
                    int god = hasGodina ? Convert.ToInt32(reader.GetValue(cols.IndexOf("GODINA"))) : aktivnaGodina;
                    int mes = hasMesec ? Convert.ToInt32(reader.GetValue(cols.IndexOf("MESEC"))) : aktivniMesec;
                    decimal varVal = reader.GetDecimal(cols.IndexOf("VARIJABILA"));
                    if (redBroj > 0 && varVal > 0 && god > 0 && mes > 0)
                    {
                        obracuniVarijabile[(redBroj, god, mes)] = varVal;
                    }
                }
            }
            Console.WriteLine($"Loaded {obracuniVarijabile.Count} Varijabila values from OBRACUN / OBRACUNI.DBF");

            // 4. Update SQLite databases
            var dbFiles = Directory.GetFiles(bazeDir, "*.db");
            foreach (var dbFile in dbFiles)
            {
                Console.WriteLine($"\nProcessing database: {Path.GetFileName(dbFile)}");
                try
                {
                    using var db = PlataDbContext.Create(dbFile);

                    // A. Update RadniSati
                    int radniSatiUpdated = 0;
                    var radniSati = await db.RadniSati.Include(rs => rs.Radnik).ToListAsync();
                    foreach (var rs in radniSati)
                    {
                        if (rs.Radnik != null && radniSatiVarijabile.TryGetValue((rs.Radnik.BrojRadnika, rs.Godina, rs.Mesec), out var val))
                        {
                            rs.Varijabila = val;
                            radniSatiUpdated++;
                        }
                    }

                    // B. Update ObracuniPlata
                    int obracuniUpdated = 0;
                    var obracuni = await db.ObracuniPlata.Include(o => o.Radnik).ToListAsync();
                    foreach (var o in obracuni)
                    {
                        if (o.Radnik != null && obracuniVarijabile.TryGetValue((o.Radnik.BrojRadnika, o.Godina, o.Mesec), out var val))
                        {
                            o.Varijabila = val;
                            obracuniUpdated++;
                        }
                    }

                    int saved = await db.SaveChangesAsync();
                    Console.WriteLine($"  Successfully updated RadniSati: {radniSatiUpdated} rows");
                    Console.WriteLine($"  Successfully updated ObracuniPlata: {obracuniUpdated} rows");
                    Console.WriteLine($"  Saved changes count: {saved}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: {ex.Message}");
                }
            }
        }

        static async Task SimulateLoadAsync(PlataDbContext _db, int godina, int mesec)
        {
            Console.WriteLine($"\n--- Simulating LoadAsync for {mesec}.{godina} ---");
            try
            {
                // 1. Deactivations
                var bivsiAktivni = await _db.Radnici
                    .Where(r => r.ImeIPrezime.Contains("Bivši zaposleni") && r.Aktivan)
                    .ToListAsync();
                Console.WriteLine($"Found {bivsiAktivni.Count} active bivsi zaposleni");
                if (bivsiAktivni.Count > 0)
                {
                    foreach (var b in bivsiAktivni)
                    {
                        b.Aktivan = false;
                    }
                    int saved = await _db.SaveChangesAsync();
                    Console.WriteLine($"Deactivated {saved} bivsi zaposleni");
                }

                // 2. Cleanups
                var bivsiTekuci = await _db.Radnici
                    .Where(r => r.Godina == godina && r.Mesec == mesec && r.ImeIPrezime.Contains("Bivši zaposleni"))
                    .ToListAsync();
                Console.WriteLine($"Found {bivsiTekuci.Count} bivsi zaposleni in current period {mesec}.{godina}");
                if (bivsiTekuci.Count > 0)
                {
                    var toDelete = new List<Radnik>();
                    foreach (var bt in bivsiTekuci)
                    {
                        var imaSate = await _db.RadniSati.AnyAsync(s => s.RadnikId == bt.Id && (s.RedovniSati > 0 || s.BolovanjeSati > 0 || s.PrekovremeneSati > 0 || s.GodisnjiOdmorSati > 0 || s.DrzavniPraznikSati > 0 || s.NocniSati > 0 || s.Stimulacija > 0));
                        var imaObracun = await _db.ObracuniPlata.AnyAsync(o => o.RadnikId == bt.Id);
                        if (!imaSate && !imaObracun)
                        {
                            Console.WriteLine($"Adding placeholder {bt.ImeIPrezime} (ID: {bt.Id}) to deletion list");
                            toDelete.Add(bt);
                        }
                    }
                    if (toDelete.Count > 0)
                    {
                        await SafeDeleteWorkersAsync(_db, toDelete);
                        int saved = await _db.SaveChangesAsync();
                        Console.WriteLine($"Saved cleanup of {saved} items");
                    }
                }

                // 3. Rollover check
                var imaAktivnihRadnika = await _db.Radnici.AnyAsync(r => r.Godina == godina && r.Mesec == mesec && r.Aktivan);
                var imaObracunaUMesecu = await _db.ObracuniPlata.AnyAsync(o => o.Godina == godina && o.Mesec == mesec);
                Console.WriteLine($"imaAktivnihRadnika = {imaAktivnihRadnika}, imaObracunaUMesecu = {imaObracunaUMesecu}");

                if (!imaAktivnihRadnika && !imaObracunaUMesecu)
                {
                    var neaktivniTekuci = await _db.Radnici.Where(r => r.Godina == godina && r.Mesec == mesec).ToListAsync();
                    Console.WriteLine($"Removing {neaktivniTekuci.Count} inactive workers for clean re-import");
                    if (neaktivniTekuci.Count > 0)
                    {
                        await SafeDeleteWorkersAsync(_db, neaktivniTekuci);
                        await _db.SaveChangesAsync();
                    }

                    var sourcePeriod = await _db.Radnici
                        .Where(r => r.Godina < godina || (r.Godina == godina && r.Mesec < mesec))
                        .OrderByDescending(r => r.Godina)
                        .ThenByDescending(r => r.Mesec)
                        .Select(r => new { r.Godina, r.Mesec })
                        .FirstOrDefaultAsync();

                    if (sourcePeriod != null)
                    {
                        Console.WriteLine($"Found source period: {sourcePeriod.Mesec}.{sourcePeriod.Godina}");
                        var sourceRadnici = await _db.Radnici
                            .Where(r => r.Godina == sourcePeriod.Godina && r.Mesec == sourcePeriod.Mesec && r.Aktivan)
                            .ToListAsync();

                        Console.WriteLine($"Copying {sourceRadnici.Count} active workers from source period");
                        foreach (var sr in sourceRadnici)
                        {
                            var newRadnik = new Radnik
                            {
                                Godina = godina,
                                Mesec = mesec,
                                BrojRadnika = sr.BrojRadnika,
                                ImeIPrezime = sr.ImeIPrezime,
                                Jmbg = sr.Jmbg,
                                MaticniBroj = sr.MaticniBroj,
                                DatumRodjenja = sr.DatumRodjenja,
                                MestoRodjenja = sr.MestoRodjenja,
                                AdresaStanovanja = sr.AdresaStanovanja,
                                Mesto = sr.Mesto,
                                SifraOpstine = sr.SifraOpstine,
                                DatumZaposlenja = sr.DatumZaposlenja,
                                DatumPrestanka = sr.DatumPrestanka,
                                Kategorija = sr.Kategorija,
                                Radno_Mesto = sr.Radno_Mesto,
                                BrojRadneJedinice = sr.BrojRadneJedinice,
                                MinuliRadGodine = sr.MinuliRadGodine,
                                Koeficijent = sr.Koeficijent,
                                Koeficijent1 = sr.Koeficijent1,
                                OsnovnaPlata = sr.OsnovnaPlata,
                                StopaPio = sr.StopaPio,
                                StopaZdravstvo = sr.StopaZdravstvo,
                                StopaNezaposlenost = sr.StopaNezaposlenost,
                                BankovniRacun = sr.BankovniRacun,
                                NazivBanke = sr.NazivBanke,
                                Aktivan = sr.Aktivan,
                                LicnoOslobodjenje = sr.LicnoOslobodjenje,
                                Operativni = sr.Operativni
                            };
                            _db.Radnici.Add(newRadnik);
                        }
                        int saved = await _db.SaveChangesAsync();
                        Console.WriteLine($"Rolled over and saved {saved} workers");
                    }
                    else
                    {
                        Console.WriteLine("No source period found!");
                    }
                }

                // 4. Query workers
                var query = _db.Radnici.Where(r => r.Godina == godina && r.Mesec == mesec);
                query = query.Where(r => r.Aktivan); // Simulating PrikazujeSamoAktivne = true
                var list = await query.OrderBy(r => r.BrojRadnika).ToListAsync();
                Console.WriteLine($"Loaded {list.Count} active workers for {mesec}.{godina}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION THROWN: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
            }
        }

        static async Task SafeDeleteWorkersAsync(PlataDbContext _db, List<Radnik> workersToDelete)
        {
            if (workersToDelete == null || workersToDelete.Count == 0) return;
            var ids = workersToDelete.Select(w => w.Id).ToList();

            var dpRows = await _db.DoprinosiPoslodavca.Where(dp => ids.Contains(dp.RadnikId)).ToListAsync();
            if (dpRows.Count > 0) _db.DoprinosiPoslodavca.RemoveRange(dpRows);

            var rsRows = await _db.RadniSati.Where(rs => ids.Contains(rs.RadnikId)).ToListAsync();
            if (rsRows.Count > 0) _db.RadniSati.RemoveRange(rsRows);

            var opRows = await _db.ObracuniPlata.Where(op => ids.Contains(op.RadnikId)).ToListAsync();
            if (opRows.Count > 0) _db.ObracuniPlata.RemoveRange(opRows);

            var kRows = await _db.Krediti.Where(k => ids.Contains(k.RadnikId)).ToListAsync();
            if (kRows.Count > 0) _db.Krediti.RemoveRange(kRows);

            var sdRows = await _db.Samodoprinosi.Where(sd => ids.Contains(sd.RadnikId)).ToListAsync();
            if (sdRows.Count > 0) _db.Samodoprinosi.RemoveRange(sdRows);

            _db.Radnici.RemoveRange(workersToDelete);
        }
    }
}

