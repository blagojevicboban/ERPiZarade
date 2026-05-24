using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbfDataReader;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

// ── Registrujemo DOS CP852 encoding (srpska latinica u Clipper-u) ──
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var cp852 = Encoding.GetEncoding(852);

// ── Putanje ────────────────────────────────────────────────────────
var dbfDir = args.Length > 0 ? args[0] : @"C:\PLATA\KOR28";
var sqliteDb = args.Length > 1 ? args[1] : @"C:\PLATA\PlataSistem\plata.db";

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║     PLATA — DBF → SQLite migracija       ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"  DBF izvor : {dbfDir}");
Console.WriteLine($"  SQLite:     {sqliteDb}");
Console.WriteLine();

if (!Directory.Exists(dbfDir))
{
    Console.WriteLine($"[GREŠKA] Direktorijum ne postoji: {dbfDir}");
    return 1;
}

// ── Čitamo aktivni mesec i godinu iz MESEC.DBF ──
var mesecDbf = Path.Combine(dbfDir, "MESEC.DBF");
int aktivnaGodina = DateTime.Now.Year;
int aktivniMesec = DateTime.Now.Month;
if (File.Exists(mesecDbf))
{
    try
    {
        var optsMesec = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var readerMesec = new DbfDataReader.DbfDataReader(mesecDbf, optsMesec);
        var colsMesec = Enumerable.Range(0, readerMesec.FieldCount).Select(i => readerMesec.GetName(i).ToUpper().Trim()).ToList();
        if (readerMesec.Read())
        {
            int g = GetInt(readerMesec, colsMesec, "GODINA");
            int m = GetInt(readerMesec, colsMesec, "MESEC");
            if (g > 0) aktivnaGodina = g;
            if (m > 0) aktivniMesec = m;
        }
        Console.WriteLine($"[OK] Otkriven aktivni obračunski period iz MESEC.DBF: {aktivniMesec}.{aktivnaGodina}.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[!] Upozorenje: Greška pri čitanju MESEC.DBF ({ex.Message}). Koristim sistemsko vreme.");
    }
}
else
{
    Console.WriteLine($"[!] Nema MESEC.DBF u {dbfDir}. Koristim tekući mesec/godinu.");
}

// ── Kreiramo SQLite bazu ───────────────────────────────────────────
if (File.Exists(sqliteDb))
{
    Console.Write($"Baza već postoji. Obrišem i počnem ispočetka? (da/ne): ");
    if (Console.ReadLine()?.Trim().ToLower() == "da")
    {
        try
        {
            File.Delete(sqliteDb);
            Console.WriteLine("[OK] Stara baza obrisana.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GREŠKA] Ne mogu obrisati staru bazu: {ex.Message}");
            return 1;
        }
    }
}

using var db = PlataDbContext.Create(sqliteDb);
Console.WriteLine("[OK] SQLite baza kreirana.\n");

// ══════════════════════════════════════════════════════════════════
// UVOZ RADNICI.DBF (aktivni radnici)
// Uvozi se direktno koristeći RED_BROJ kao primarni ključ (Radnik.Id).
// ══════════════════════════════════════════════════════════════════
var radniciDbf = Path.Combine(dbfDir, "RADNICI.DBF");
var uvezeniRadnikIds = new HashSet<int>();

if (File.Exists(radniciDbf))
{
    Console.Write("Uvoz RADNICI.DBF ... ");
    int cnt = 0, skipped = 0;

    var optionsAll = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = false };
    using var reader = new DbfDataReader.DbfDataReader(radniciDbf, optionsAll);
    var columns = Enumerable.Range(0, reader.FieldCount)
                            .Select(i => reader.GetName(i).ToUpper().Trim())
                            .ToList();

    Console.WriteLine($"\n  Kolone: {string.Join(", ", columns)}");

    int fizickaPos = 0;
    while (reader.Read())
    {
        fizickaPos++;
        try
        {
            int redBroj = GetInt(reader, columns, "RED_BROJ", "BR_RADNIK", "SIFRA");
            string imeIPrezime = GetString(reader, columns, "RADNIK", "IME", "IME_I_PRE", "NAZIV");

            int idZaRadnika = redBroj > 0 ? redBroj : fizickaPos;

            var radnik = new Radnik
            {
                Id               = idZaRadnika,
                BrojRadnika      = idZaRadnika,
                ImeIPrezime      = string.IsNullOrWhiteSpace(imeIPrezime)
                                   ? $"[Bivši zaposleni #{idZaRadnika}]"
                                   : imeIPrezime,
                MaticniBroj      = GetString(reader, columns, "MAT_BROJ", "MAT_BR"),
                Jmbg             = GetString(reader, columns, "JMBG") is string j && !string.IsNullOrWhiteSpace(j)
                                   ? j.Trim()
                                   : (GetString(reader, columns, "MAT_BROJ", "MAT_BR") is string m && m.Trim().Length == 13 ? m.Trim() : ""),
                Koeficijent      = GetDecimal(reader, columns, "KOEFIC", "KOEFICIJE", "KOEF"),
                OsnovnaPlata     = GetDecimal(reader, columns, "MIN_PLATA", "OSNOVA", "OSN_PLATA"),
                BankovniRacun    = GetString(reader, columns, "BROJ_TR", "ZIRO", "RACUN"),
                NazivBanke       = GetIntAsString(reader, columns, "BANKA"),
                Radno_Mesto      = GetString(reader, columns, "RADNO_M", "RADNO_MES"),
                BrojRadneJedinice= GetInt(reader, columns, "RAD_JED"),
                Kategorija       = GetString(reader, columns, "RAZRED", "KAT", "KATEGORIJ"),
                Aktivan          = GetString(reader, columns, "AKTIVAN").ToUpper() == "DA",
                DatumZaposlenja  = GetDate(reader, columns, "MIN_RAD"),
            };

            db.Radnici.Add(radnik);
            await db.SaveChangesAsync();
            uvezeniRadnikIds.Add(idZaRadnika);
            cnt++;
        }
        catch (Exception ex)
        {
            skipped++;
            Console.WriteLine($"\n  [!] Pos={fizickaPos} preskočen: {ex.Message}");
        }
    }

    Console.WriteLine($"  [OK] Uvezeno {cnt} aktivnih radnika koristeći RED_BROJ kao ID (preskočeno: {skipped})");
}
else
{
    Console.WriteLine($"[!] Nema RADNICI.DBF u {dbfDir}");
}

// ══════════════════════════════════════════════════════════════════
// SKENIRANJE RADNICII.DBF (istorija bivših radnika)
// Čitamo istoriju radnika da izvučemo njihova stvarna imena i detalje.
// ══════════════════════════════════════════════════════════════════
var radniciiDbf = Path.Combine(dbfDir, "RADNICII.DBF");
if (File.Exists(radniciiDbf))
{
    Console.Write("\nSkeniranje RADNICII.DBF za bivše zaposlene... ");
    var historicalProfiles = new Dictionary<int, (int periodKey, Radnik radnik)>();

    try
    {
        var optsHistory = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var reader = new DbfDataReader.DbfDataReader(radniciiDbf, optsHistory);
        var columns = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.GetName(i).ToUpper().Trim())
                                .ToList();

        while (reader.Read())
        {
            int redBroj = GetInt(reader, columns, "RED_BROJ", "BR_RADNIK", "SIFRA");
            if (redBroj <= 0 || uvezeniRadnikIds.Contains(redBroj)) continue;

            int god = GetInt(reader, columns, "GODINA");
            int mes = GetInt(reader, columns, "MESEC");
            int periodKey = god * 12 + mes;

            string imeIPrezime = GetString(reader, columns, "RADNIK", "IME", "IME_I_PRE", "NAZIV");
            if (string.IsNullOrWhiteSpace(imeIPrezime)) continue;

            // Zadržavamo najnoviji profil za bivšeg zaposlenog
            if (!historicalProfiles.TryGetValue(redBroj, out var existing) || periodKey > existing.periodKey)
            {
                var radnik = new Radnik
                {
                    Id               = redBroj,
                    BrojRadnika      = redBroj,
                    ImeIPrezime      = imeIPrezime,
                    MaticniBroj      = GetString(reader, columns, "MAT_BROJ", "MAT_BR"),
                    Jmbg             = GetString(reader, columns, "JMBG") is string j && !string.IsNullOrWhiteSpace(j)
                                       ? j.Trim()
                                       : (GetString(reader, columns, "MAT_BROJ", "MAT_BR") is string m && m.Trim().Length == 13 ? m.Trim() : ""),
                    Koeficijent      = GetDecimal(reader, columns, "KOEFIC", "KOEFICIJE", "KOEF"),
                    OsnovnaPlata     = GetDecimal(reader, columns, "MIN_PLATA", "OSNOVA", "OSN_PLATA"),
                    BankovniRacun    = GetString(reader, columns, "BROJ_TR", "ZIRO", "RACUN"),
                    NazivBanke       = GetIntAsString(reader, columns, "BANKA"),
                    Radno_Mesto      = GetString(reader, columns, "RADNO_M", "RADNO_MES"),
                    BrojRadneJedinice= GetInt(reader, columns, "RAD_JED"),
                    Kategorija       = GetString(reader, columns, "RAZRED", "KAT", "KATEGORIJ"),
                    Aktivan          = false, // Svi u istoriji su neaktivni po defaultu
                    DatumZaposlenja  = GetDate(reader, columns, "MIN_RAD"),
                };
                historicalProfiles[redBroj] = (periodKey, radnik);
            }
        }

        int histCnt = 0;
        foreach (var entry in historicalProfiles.OrderBy(k => k.Key))
        {
            db.Radnici.Add(entry.Value.radnik);
            await db.SaveChangesAsync();
            uvezeniRadnikIds.Add(entry.Key);
            histCnt++;
        }

        Console.WriteLine($"Pronađeno i uvezeno {histCnt} bivših radnika sa stvarnim profilima.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n  [!] Greška pri čitanju RADNICII.DBF: {ex.Message}");
    }
}
else
{
    Console.WriteLine($"\n[!] Nema RADNICII.DBF na putanji: {dbfDir}");
}

// ══════════════════════════════════════════════════════════════════
// SKENIRANJE OBRACUNA ZA FALLBACK GHOST RADNIKE
// Ukoliko postoji neki obračun za radnika koji ne postoji ni u RADNICI ni u RADNICII.
// ══════════════════════════════════════════════════════════════════
Console.Write("\nSkeniranje OBRACUNI.DBF i OBRACUN.DBF za preostale ghost zaposlene... ");
{
    var missingPos = new HashSet<int>();

    // 1. Skeniranje OBRACUNI.DBF
    var obracuniDbfTemp = Path.Combine(dbfDir, "OBRACUNI.DBF");
    if (File.Exists(obracuniDbfTemp))
    {
        var optsTemp = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var rTemp = new DbfDataReader.DbfDataReader(obracuniDbfTemp, optsTemp);
        var colsTemp = Enumerable.Range(0, rTemp.FieldCount).Select(i => rTemp.GetName(i).ToUpper().Trim()).ToList();
        while (rTemp.Read())
        {
            int rb = GetInt(rTemp, colsTemp, "RED_BROJ");
            if (rb > 0 && !uvezeniRadnikIds.Contains(rb))
                missingPos.Add(rb);
        }
    }

    // 2. Skeniranje OBRACUN.DBF
    var obracunDbfTemp = Path.Combine(dbfDir, "OBRACUN.DBF");
    if (File.Exists(obracunDbfTemp))
    {
        var optsTemp = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var rTemp = new DbfDataReader.DbfDataReader(obracunDbfTemp, optsTemp);
        var colsTemp = Enumerable.Range(0, rTemp.FieldCount).Select(i => rTemp.GetName(i).ToUpper().Trim()).ToList();
        while (rTemp.Read())
        {
            int rb = GetInt(rTemp, colsTemp, "RED_BROJ");
            if (rb > 0 && !uvezeniRadnikIds.Contains(rb))
                missingPos.Add(rb);
        }
    }

    int ghostCnt = 0;
    foreach (var pos in missingPos.OrderBy(p => p))
    {
        var ghost = new Radnik
        {
            Id          = pos,
            BrojRadnika = pos,
            ImeIPrezime = $"[Bivši zaposleni #{pos}]",
            Aktivan     = false,
        };
        db.Radnici.Add(ghost);
        await db.SaveChangesAsync();
        uvezeniRadnikIds.Add(pos);
        ghostCnt++;
    }
    Console.WriteLine($"Kreirano {ghostCnt} dodatnih ghost zapisa.");
}

// ── Popunjavanje rečnika naziva samodoprinosa/obustava iz SAMODOP.DBF i SAMODOPI.DBF ──
var generalNames = new Dictionary<int, string>();
try
{
    var samodopDbfPath = Path.Combine(dbfDir, "SAMODOP.DBF");
    if (File.Exists(samodopDbfPath))
    {
        var opts = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var readerCatalog = new DbfDataReader.DbfDataReader(samodopDbfPath, opts);
        var cols = Enumerable.Range(0, readerCatalog.FieldCount).Select(i => readerCatalog.GetName(i).ToUpper().Trim()).ToList();
        while (readerCatalog.Read())
        {
            int code = GetInt(readerCatalog, cols, "RED_BROJ");
            string name = GetString(readerCatalog, cols, "NAZIV");
            if (code > 0 && !string.IsNullOrWhiteSpace(name))
            {
                generalNames[code] = name;
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Upozorenje] Greška pri čitanju SAMODOP.DBF: {ex.Message}");
}

try
{
    var samodopiDbfPath = Path.Combine(dbfDir, "SAMODOPI.DBF");
    if (File.Exists(samodopiDbfPath))
    {
        var opts = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var readerCatalog = new DbfDataReader.DbfDataReader(samodopiDbfPath, opts);
        var cols = Enumerable.Range(0, readerCatalog.FieldCount).Select(i => readerCatalog.GetName(i).ToUpper().Trim()).ToList();
        while (readerCatalog.Read())
        {
            int code = GetInt(readerCatalog, cols, "RED_BROJ");
            string name = GetString(readerCatalog, cols, "NAZIV");
            if (code > 0 && !string.IsNullOrWhiteSpace(name))
            {
                generalNames[code] = name;
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Upozorenje] Greška pri čitanju SAMODOPI.DBF: {ex.Message}");
}
Console.WriteLine($"[OK] Učitan katalog sa {generalNames.Count} naziva obustava/samodoprinosa.");

// ══════════════════════════════════════════════════════════════════
// UVOZ OBRACUNI.DBF I OBRACUN.DBF (svi obračuni)
// ══════════════════════════════════════════════════════════════════
var obracuniDbf = Path.Combine(dbfDir, "OBRACUNI.DBF");
var obracunDbf = Path.Combine(dbfDir, "OBRACUN.DBF");

await ImportObracuniDbf(obracuniDbf, "OBRACUNI.DBF (istorija)", aktivnaGodina, aktivniMesec);
await ImportObracuniDbf(obracunDbf, "OBRACUN.DBF (aktivni/tekući)", aktivnaGodina, aktivniMesec);

// ══════════════════════════════════════════════════════════════════
// UVOZ POREZA (sistemski parametri i stope)
// ══════════════════════════════════════════════════════════════════
var poreziiDbf = Path.Combine(dbfDir, "POREZII.DBF");
var poreziDbf = Path.Combine(dbfDir, "POREZI.DBF");

await ImportPoreziDbf(poreziiDbf, "POREZII.DBF (istorija)", isHistory: true);
await ImportPoreziDbf(poreziDbf, "POREZI.DBF (aktivni/tekući)", isHistory: false);

// ══════════════════════════════════════════════════════════════════
// UVOZ KOMPANIJE / KORISNIC.DBF (podešavanja firme)
// ══════════════════════════════════════════════════════════════════
var korisnicDbf = Path.Combine(dbfDir, "KORISNIC.DBF");
await ImportKorisnicDbf(korisnicDbf);

// ══════════════════════════════════════════════════════════════════
// UVOZ DOPRINOSA (sistemske stope doprinosa)
// ══════════════════════════════════════════════════════════════════
var doprinoiDbf = Path.Combine(dbfDir, "DOPRINOI.DBF");
var doprinosDbf = Path.Combine(dbfDir, "DOPRINOS.DBF");

await ImportDoprinosiDbf(doprinoiDbf, "DOPRINOI.DBF (istorija)", isHistory: true);
await ImportDoprinosiDbf(doprinosDbf, "DOPRINOS.DBF (aktivni/tekući)", isHistory: false);



// ── Reusable uvoz obračuna ──
async Task ImportObracuniDbf(string dbfPath, string label, int defaultGodina, int defaultMesec)
{
    if (!File.Exists(dbfPath))
    {
        Console.WriteLine($"[!] Nema {label} na putanji: {dbfPath}");
        return;
    }

    Console.Write($"\nUvoz {label} ... ");
    int cnt = 0, skipped = 0;

    var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
    using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
    var columns = Enumerable.Range(0, reader.FieldCount)
                            .Select(i => reader.GetName(i).ToUpper().Trim()).ToList();

    Console.WriteLine($"\n  Kolone: {string.Join(", ", columns)}");

    var batch = new List<ObracunPlate>();

    while (reader.Read())
    {
        try
        {
            int brRadnika = GetInt(reader, columns, "RED_BROJ");
            
            // Provera da li kolone sadrže GODINA/MESEC; ako ne (tekući obracun.dbf), koristimo učitane vrednosti iz MESEC.DBF
            int godina = columns.Contains("GODINA") ? GetInt(reader, columns, "GODINA") : defaultGodina;
            int mesec = columns.Contains("MESEC") ? GetInt(reader, columns, "MESEC") : defaultMesec;

            if (godina <= 0) godina = defaultGodina;
            if (mesec <= 0) mesec = defaultMesec;

            // Provera postojanja radnika
            if (!uvezeniRadnikIds.Contains(brRadnika))
            {
                skipped++;
                continue;
            }

            // Deduplikacija po radniku, godini i mesecu
            var postojeci = await db.ObracuniPlata
                .AnyAsync(o => o.RadnikId == brRadnika && o.Godina == godina && o.Mesec == mesec);
            if (postojeci)
            {
                skipped++;
                continue;
            }

            batch.Add(new ObracunPlate
            {
                RadnikId          = brRadnika, // Direktno mapiranje jer je Radnik.Id == brRadnika == RED_BROJ
                Godina            = godina,
                Mesec             = mesec,
                BrutoZarada       = GetDecimal(reader, columns, "BRUTO_ZAR", "BRUTO"),
                BrutoBolovanje    = GetDecimal(reader, columns, "BRUTO_BOL"),
                BrutoNaknade      = GetDecimal(reader, columns, "BRUTO_NAK"),
                BrutoStimulacija  = GetDecimal(reader, columns, "STIMULACIJ"),
                DoprinosPioRadnik = GetDecimal(reader, columns, "DOP_ZAR1", "PIO"),
                DoprinosZdravstvoRadnik     = GetDecimal(reader, columns, "DOP_ZAR2"),
                DoprinosNezaposlenostRadnik = GetDecimal(reader, columns, "DOP_ZAR3"),
                PorezNaDohodak    = GetDecimal(reader, columns, "UKUP_POR", "POREZ_IZ"),
                PoreskaOsnovica   = GetDecimal(reader, columns, "BRUTO_POR"),
                Samodoprinosi     = GetDecimal(reader, columns, "SAMODOP1") +
                                    GetDecimal(reader, columns, "SAMODOP2") +
                                    GetDecimal(reader, columns, "SAMODOP3") +
                                    GetDecimal(reader, columns, "SAMODOP4"),
                KreditObustava    = (GetDecimal(reader, columns, "OBUST_LIN1") + GetDecimal(reader, columns, "OBUST_PLIN") > 0)
                                    ? (GetDecimal(reader, columns, "OBUST_LIN1") + GetDecimal(reader, columns, "OBUST_PLIN"))
                                    : (GetDecimal(reader, columns, "KR_IZ1") + GetDecimal(reader, columns, "KR_IZ2") + GetDecimal(reader, columns, "KR_IZ3") + GetDecimal(reader, columns, "KR_IZ4") + GetDecimal(reader, columns, "KR_IZ5")),
                NetoIsplata       = GetDecimal(reader, columns, "ZA_ISPLATU", "NETO"),
                RedovniSati       = GetInt(reader, columns, "RADN_SATI"),
                BolovanjeSati     = GetInt(reader, columns, "BOL_DO_60"),
                PrekovremeneSati  = GetInt(reader, columns, "PREKOVREME"),
                DatumObracuna     = new DateTime(godina, mesec, 1)
            });

            // ── Uvoz detaljnih obustava / samodoprinosa u SQLite ──
            var postojeciDetalji = await db.Samodoprinosi
                .Where(s => s.RadnikId == brRadnika && s.Godina == godina && s.Mesec == mesec)
                .ToListAsync();
            if (postojeciDetalji.Count > 0)
            {
                db.Samodoprinosi.RemoveRange(postojeciDetalji);
            }

            for (int i = 1; i <= 4; i++)
            {
                decimal iznos = GetDecimal(reader, columns, $"SAMODOP{i}");
                int sifra = GetInt(reader, columns, $"SIF_SAM{i}");
                if (iznos > 0 && sifra > 0)
                {
                    string opis = generalNames.TryGetValue(sifra, out var name) ? name : $"Doprinos/Obustava #{sifra}";
                    db.Samodoprinosi.Add(new Samodoprinosi
                    {
                        RadnikId = brRadnika,
                        Godina = godina,
                        Mesec = mesec,
                        Iznos = iznos,
                        Opis = opis
                    });
                }
            }

            for (int i = 1; i <= 5; i++)
            {
                decimal iznos = GetDecimal(reader, columns, $"KR_IZ{i}");
                int sifra = GetInt(reader, columns, $"KREDIT{i}");
                if (iznos > 0 && sifra > 0)
                {
                    string opis = generalNames.TryGetValue(sifra, out var name) ? name : $"Kredit #{sifra}";
                    db.Samodoprinosi.Add(new Samodoprinosi
                    {
                        RadnikId = brRadnika,
                        Godina = godina,
                        Mesec = mesec,
                        Iznos = iznos,
                        Opis = opis
                    });
                }
            }

            cnt++;
            if (batch.Count >= 500)
            {
                try
                {
                    db.ObracuniPlata.AddRange(batch);
                    await db.SaveChangesAsync();
                }
                catch
                {
                    db.ChangeTracker.Clear();
                    foreach (var o in batch)
                    {
                        try
                        {
                            var postojeciPojedinacni = await db.ObracuniPlata
                                .AnyAsync(x => x.RadnikId == o.RadnikId && x.Godina == o.Godina && x.Mesec == o.Mesec);
                            if (!postojeciPojedinacni)
                            {
                                db.ObracuniPlata.Add(o);
                                await db.SaveChangesAsync();
                            }
                            else
                            {
                                skipped++;
                                cnt--;
                            }
                        }
                        catch { db.ChangeTracker.Clear(); skipped++; cnt--; }
                    }
                }
                batch.Clear();
                Console.Write($"\r  Uvezeno: {cnt}...  ");
            }
        }
        catch { skipped++; }
    }

    if (batch.Count > 0)
    {
        try
        {
            db.ObracuniPlata.AddRange(batch);
            await db.SaveChangesAsync();
        }
        catch
        {
            db.ChangeTracker.Clear();
            foreach (var o in batch)
            {
                try
                {
                    var postojeciPojedinacni = await db.ObracuniPlata
                        .AnyAsync(x => x.RadnikId == o.RadnikId && x.Godina == o.Godina && x.Mesec == o.Mesec);
                    if (!postojeciPojedinacni)
                    {
                        db.ObracuniPlata.Add(o);
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch { db.ChangeTracker.Clear(); skipped++; }
            }
        }
    }
    Console.WriteLine($"\r  [OK] Uvezeno {cnt} obračuna iz {label} (preskočeno: {skipped})");
}

// ── Reusable uvoz poreskih parametara ──
async Task ImportPoreziDbf(string dbfPath, string label, bool isHistory)
{
    if (!File.Exists(dbfPath))
    {
        Console.WriteLine($"[!] Nema {label} na putanji: {dbfPath}");
        return;
    }

    Console.Write($"\nUvoz {label} ... ");
    int cnt = 0, skipped = 0;

    try
    {
        var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
        var columns = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.GetName(i).ToUpper().Trim()).ToList();

        while (reader.Read())
        {
            int godina = isHistory ? GetInt(reader, columns, "GODINA") : aktivnaGodina;
            int mesec = isHistory ? GetInt(reader, columns, "MESEC") : aktivniMesec;
            int redBroj = GetInt(reader, columns, "RED_BROJ");

            if (godina <= 0) godina = aktivnaGodina;
            if (mesec <= 0) mesec = aktivniMesec;

            // Izbegavamo duplikate po godina/mesec/redBroj
            var postojeci = await db.Porezi
                .AnyAsync(p => p.Godina == godina && p.Mesec == mesec && p.RedniBroj == redBroj);
            if (postojeci)
            {
                skipped++;
                continue;
            }

            var p = new Porezi
            {
                Godina      = godina,
                Mesec       = mesec,
                RedniBroj   = redBroj,
                Zarada      = GetDecimal(reader, columns, "ZARADA"),
                AkPorez     = GetDecimal(reader, columns, "AKPOREZ"),
                AkPorez2    = GetDecimal(reader, columns, "AKPOREZ2"),
                AkPorez3    = GetDecimal(reader, columns, "AKPOREZ3"),
                AkPorez4    = GetDecimal(reader, columns, "AKPOREZ4"),
                Prvast      = GetDecimal(reader, columns, "PRVAST"),
                Drugast     = GetDecimal(reader, columns, "DRUGAST"),
                Trecast     = GetDecimal(reader, columns, "TRECAST"),
                LinPorez3   = GetDecimal(reader, columns, "LINPOREZ3"),
                SifPlac1    = GetString(reader, columns, "SIF_PLAC1"),
                ZiroR1      = GetString(reader, columns, "ZIRO_R1"),
                PozivNa1    = GetString(reader, columns, "POZIV_NA1"),
                PozivNa3    = GetString(reader, columns, "POZIV_NA3"),
                Svrha1      = GetString(reader, columns, "SVRHA1"),
                Svrha2      = GetString(reader, columns, "SVRHA2"),
                Primalac1   = GetString(reader, columns, "PRIMALAC1"),
                Primalac2   = GetString(reader, columns, "PRIMALAC2"),
                SifPlac2    = GetString(reader, columns, "SIF_PLAC2"),
                ZiroR2      = GetString(reader, columns, "ZIRO_R2"),
                PozivNa2    = GetString(reader, columns, "POZIV_NA2"),
                PozivNa4    = GetString(reader, columns, "POZIV_NA4"),
                PosPorez    = GetDecimal(reader, columns, "POSPOREZ"),
                Svrha3      = GetString(reader, columns, "SVRHA3"),
                Svrha4      = GetString(reader, columns, "SVRHA4"),
                Primalac3   = GetString(reader, columns, "PRIMALAC3"),
                Primalac4   = GetString(reader, columns, "PRIMALAC4"),
                ProcDrzav   = GetDecimal(reader, columns, "PROC_DRZAV"),
                ProcNocni   = GetDecimal(reader, columns, "PROC_NOCNI"),
                ProcPreko   = GetDecimal(reader, columns, "PROC_PREKO"),
                ProcMinul   = GetDecimal(reader, columns, "PROC_MINUL"),
                ProcNedel   = GetDecimal(reader, columns, "PROC_NEDEL"),
                ProcBolov   = GetDecimal(reader, columns, "PROC_BOLOV"),
                ProcPlac    = GetDecimal(reader, columns, "PROC_PLAC"),
                ProcPlZa    = GetDecimal(reader, columns, "PROC_PL_ZA"),
                ProcInval   = GetDecimal(reader, columns, "PROC_INVAL"),
                FondCasova  = GetInt(reader, columns, "FONDCASOVA"),
                CasZaOb     = GetInt(reader, columns, "CAS_ZA_OB"),
                VrBoda      = GetDecimal(reader, columns, "VR_BODA"),
                ProcIzdrz   = GetDecimal(reader, columns, "PROC_IZDRZ"),
                Akont       = GetString(reader, columns, "AKONT") is string a && !string.IsNullOrWhiteSpace(a) ? a : "DA",
                ProsBrut    = GetDecimal(reader, columns, "PROS_BRUT")
            };

            db.Porezi.Add(p);
            await db.SaveChangesAsync();
            cnt++;
        }
        Console.WriteLine($"\r  [OK] Uvezeno {cnt} zapisa o porezima iz {label} (preskočeno: {skipped})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\r  [GREŠKA] Neuspešan uvoz {label}: {ex.Message}");
    }
}

// ── Reusable uvoz doprinosa ──
async Task ImportDoprinosiDbf(string dbfPath, string label, bool isHistory)
{
    if (!File.Exists(dbfPath))
    {
        Console.WriteLine($"[!] Nema {label} na putanji: {dbfPath}");
        return;
    }

    Console.Write($"\nUvoz {label} ... ");
    int cnt = 0, skipped = 0;

    try
    {
        var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
        var columns = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.GetName(i).ToUpper().Trim()).ToList();

        while (reader.Read())
        {
            int godina = isHistory ? GetInt(reader, columns, "GODINA") : aktivnaGodina;
            int mesec = isHistory ? GetInt(reader, columns, "MESEC") : aktivniMesec;
            int redBroj = GetInt(reader, columns, "RED_BROJ");

            if (godina <= 0) godina = aktivnaGodina;
            if (mesec <= 0) mesec = aktivniMesec;

            // Izbegavamo duplikate po godina/mesec/redBroj
            var postojeci = await db.Doprinosi
                .AnyAsync(d => d.Godina == godina && d.Mesec == mesec && d.RedniBroj == redBroj);
            if (postojeci)
            {
                skipped++;
                continue;
            }

            var d = new Doprinos
            {
                Godina      = godina,
                Mesec       = mesec,
                RedniBroj   = redBroj,
                Naziv       = GetString(reader, columns, "NAZIV"),
                ProcRadn    = GetDecimal(reader, columns, "PROC_RADN"),
                ProcPosl    = GetDecimal(reader, columns, "PROC_POSL"),
                B60ProcR    = GetDecimal(reader, columns, "B60_PROC_R"),
                B60ProcP    = GetDecimal(reader, columns, "B60_PROC_P"),
                Bp60ProcP   = GetDecimal(reader, columns, "BP60PROC_P"),
                Bp60FProcP  = GetDecimal(reader, columns, "BP60FPROCP"),
                PorProcP    = GetDecimal(reader, columns, "POR_PROC_P"),
                NepProcP    = GetDecimal(reader, columns, "NEP_PROC_P"),
                InvProcP    = GetDecimal(reader, columns, "INV_PROC_P"),
                Svrha1      = GetString(reader, columns, "SVRHA1"),
                Svrha2      = GetString(reader, columns, "SVRHA2"),
                Primalac1   = GetString(reader, columns, "PRIMALAC1"),
                Primalac2   = GetString(reader, columns, "PRIMALAC2"),
                ZiroRacun   = GetString(reader, columns, "ZIRO_RACUN"),
                ZiroRacP    = GetString(reader, columns, "ZIRO_RAC_P"),
                PozivNaB    = GetString(reader, columns, "POZIV_NA_B"),
                PozivNa2    = GetString(reader, columns, "POZIV_NA_2"),
                SifPlac     = GetString(reader, columns, "SIF_PLAC"),
                SifPlacP    = GetString(reader, columns, "SIF_PLAC_P")
            };

            db.Doprinosi.Add(d);
            await db.SaveChangesAsync();
            cnt++;
        }
        Console.WriteLine($"\r  [OK] Uvezeno {cnt} zapisa o doprinosima iz {label} (preskočeno: {skipped})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\r  [GREŠKA] Neuspešan uvoz {label}: {ex.Message}");
    }
}


// ══════════════════════════════════════════════════════════════════
// IZVEŠTAJ
// ══════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("══════════════════════════════════════════");
Console.WriteLine("  REZULTAT MIGRACIJE");
Console.WriteLine("══════════════════════════════════════════");
Console.WriteLine($"  Radnika:   {await db.Radnici.CountAsync()}");
Console.WriteLine($"  Obračuna:  {await db.ObracuniPlata.CountAsync()}");
Console.WriteLine($"  Poreza:    {await db.Porezi.CountAsync()}");
Console.WriteLine($"  Doprinosa: {await db.Doprinosi.CountAsync()}");
Console.WriteLine($"  Baza:      {sqliteDb}");
Console.WriteLine($"  Veličina:  {new FileInfo(sqliteDb).Length / 1024} KB");
Console.WriteLine("══════════════════════════════════════════");
Console.WriteLine("  Migracija završena! ✓");

return 0;

// ── Pomoćne funkcije ───────────────────────────────────────────────
static string GetString(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
{
    foreach (var n in names) { int i = cols.IndexOf(n); if (i >= 0) try { return r.GetString(i).Trim(); } catch { } }
    return "";
}
static string GetIntAsString(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
{
    foreach (var n in names) { int i = cols.IndexOf(n); if (i >= 0) try { return r.GetDecimal(i).ToString(); } catch { try { return r.GetString(i).Trim(); } catch { } } }
    return "";
}
static int GetInt(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
{
    foreach (var n in names)
    {
        int i = cols.IndexOf(n);
        if (i >= 0) try { return Convert.ToInt32(r.GetValue(i)); } catch { }
    }
    return 0;
}
static decimal GetDecimal(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
{
    foreach (var n in names) { int i = cols.IndexOf(n); if (i >= 0) try { return r.GetDecimal(i); } catch { } }
    return 0m;
}
static DateTime? GetDate(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
{
    foreach (var n in names)
    {
        int i = cols.IndexOf(n);
        if (i >= 0) try { var d = r.GetDateTime(i); if (d.Year > 1900) return d; } catch { }
    }
    return null;
}

async Task ImportKorisnicDbf(string dbfPath)
{
    if (!File.Exists(dbfPath))
    {
        // Pokušaj u roditeljskom folderu ako je prosleđen poddirektorijum (kao npr. KOR28)
        var dirName = Path.GetDirectoryName(dbfPath);
        if (!string.IsNullOrEmpty(dirName))
        {
            var parent = Directory.GetParent(dirName);
            if (parent != null)
            {
                var fallbackPath = Path.Combine(parent.FullName, "KORISNIC.DBF");
                if (File.Exists(fallbackPath))
                {
                    dbfPath = fallbackPath;
                }
            }
        }
    }

    if (!File.Exists(dbfPath))
    {
        Console.WriteLine($"[!] Nema KORISNIC.DBF na putanji: {dbfPath}");
        return;
    }

    Console.Write($"\nUvoz KORISNIC.DBF ... ");
    int cnt = 0, skipped = 0;

    try
    {
        var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
        var columns = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.GetName(i).ToUpper().Trim()).ToList();

        // Očekujemo jedan zapis sa podacima o firmi
        if (reader.Read())
        {
            var f = new Firma();

            // Mapa najčešćih imena kolona u KORISNIC.DBF
            f.Naziv = GetString(reader, columns, "IME", "NAZIV", "FIRMA", "NAZIV_FIR");
            f.Adresa = GetString(reader, columns, "UL", "ADRESA", "ULICA", "ADRES");
            f.Grad = GetString(reader, columns, "BR", "GRAD", "MESTO");
            f.Pib = GetString(reader, columns, "PORESKI_BR", "PIB", "PIB_FIR");
            f.Mb = GetString(reader, columns, "MB", "MAT_BR");
            f.BankovniRacun = GetString(reader, columns, "Z", "BROJ_TR", "ZIRO", "RACUN");
            f.SifraPlacanja = GetString(reader, columns, "SIFRA_PLAC", "SIF_PLAC");
            f.Telefon = GetString(reader, columns, "TEL", "TELEFON");
            f.Email = GetString(reader, columns, "FAX", "EMAIL", "E_MAIL");
            f.Napomena = GetString(reader, columns, "NAPOMENA", "NAPOM");

            // Upsert: ako postoji zapis, zameniti; inače dodati
            var existing = await db.Firme.FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.Naziv = f.Naziv;
                existing.Adresa = f.Adresa;
                existing.Grad = f.Grad;
                existing.Pib = f.Pib;
                existing.Mb = f.Mb;
                existing.BankovniRacun = f.BankovniRacun;
                existing.SifraPlacanja = f.SifraPlacanja;
                existing.Telefon = f.Telefon;
                existing.Email = f.Email;
                existing.Napomena = f.Napomena;
                await db.SaveChangesAsync();
            }
            else
            {
                db.Firme.Add(f);
                await db.SaveChangesAsync();
            }

            cnt = 1;
        }

        Console.WriteLine($"\r  [OK] Uvezeno {cnt} zapisa iz KORISNIC.DBF (preskočeno: {skipped})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\r  [GREŠKA] Neuspešan uvoz KORISNIC.DBF: {ex.Message}");
    }
}
