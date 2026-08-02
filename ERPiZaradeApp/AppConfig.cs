using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ERPiZaradeApp;

public static class AppConfig
{
    /// <summary>
    /// Folder sa bazama. Namerno je IZVAN Velopack stabla (%LOCALAPPDATA%\ERPiZarade\),
    /// čiji se sadržaj briše i ponovo raspakuje pri svakom ažuriranju programa.
    /// Isti obrazac koriste ERPiFinansijeApp i ERPiSredstvaApp, zbog čega im baze preživljavaju update.
    /// </summary>
    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ERPiZaradeApp");

    public static string BazeDir => Path.Combine(AppDataDir, "Baze");

    /// <summary>Stara lokacija iz Inno Setup instalacije — koristi se samo kao izvor za migraciju.</summary>
    private static string StariBazeDir => @"C:\ERPiZaradeApp\Baze";

    /// <summary>
    /// Folderi sa podacima pod starim imenima aplikacije (pre preimenovanja u ERPi liniju).
    /// Koriste se isključivo kao izvor jednokratnog preuzimanja podataka.
    /// </summary>
    private static string[] StariAppDataDirs => new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlataApp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlataSistemApp")
    };

    /// <summary>Marker da je preuzimanje iz starog foldera već obavljeno.</summary>
    private static string MarkerPreuzimanja => Path.Combine(AppDataDir, "preuzeto_iz_starog_foldera.txt");

    /// <summary>
    /// Jednokratno preuzimanje SVIH zatečenih podataka iz foldera pod starim imenom
    /// aplikacije (%LOCALAPPDATA%\PlataApp) u novi (%LOCALAPPDATA%\ERPiZaradeApp) —
    /// baze, rezervne kopije, podešavanja i logove.
    ///
    /// Preimenovanje u ERPi liniju promenilo je i ime foldera sa podacima, pa bi bez ovoga
    /// nova verzija startovala sa praznim spiskom firmi iako baze i dalje postoje na disku.
    ///
    /// Podaci se KOPIRAJU, ne premeštaju — stara instalacija ostaje upotrebljiva dok se
    /// korisnik ne uveri da je sve preneto. Da se obrisana baza ne bi vraćala pri svakom
    /// pokretanju, uspešno preuzimanje se beleži marker fajlom.
    ///
    /// Mora da se pozove PRE prvog pristupa <see cref="UserSettings.Instance"/>, jer se
    /// odmah po kopiranju premapira putanja aktivne baze.
    /// </summary>
    public static void PreuzmiStariFolderPodataka()
    {
        try
        {
            var izvori = StariAppDataDirs.Where(Directory.Exists).ToArray();
            if (izvori.Length == 0) return;

            Directory.CreateDirectory(AppDataDir);
            if (File.Exists(MarkerPreuzimanja)) return;

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            int kopirano = 0;
            foreach (var izvor in izvori)
            {
                kopirano += KopirajFolder(izvor, AppDataDir);
            }

            PremapirajAktivnuBazu();

            File.WriteAllText(MarkerPreuzimanja,
                $"Podaci su preuzeti iz: {string.Join(", ", izvori)} dana {DateTime.Now:dd.MM.yyyy. HH:mm:ss}.{Environment.NewLine}" +
                $"Kopirano fajlova: {kopirano}. Original je ostao netaknut i može se obrisati ručno.{Environment.NewLine}" +
                $"Brisanje ovog fajla ponovo pokreće preuzimanje pri sledećem startu.{Environment.NewLine}");

            Serilog.Log.Information(
                "Preuzeto {Broj} fajlova iz starih foldera {Izvori} u {Odrediste}",
                kopirano, izvori, AppDataDir);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Greška pri preuzimanju podataka iz starog foldera aplikacije");
        }
    }

    /// <summary>
    /// Rekurzivno kopira ceo sadržaj foldera. Fajl koji na odredištu već postoji se ne dira —
    /// novi podaci uvek pobeđuju nad zatečenim.
    /// </summary>
    private static int KopirajFolder(string izvor, string odrediste)
    {
        int kopirano = 0;
        Directory.CreateDirectory(odrediste);

        foreach (var fajl in Directory.GetFiles(izvor))
        {
            try
            {
                var cilj = Path.Combine(odrediste, Path.GetFileName(fajl));
                if (File.Exists(cilj))
                {
                    // Sudar imena: prazna podrazumevana baza, koju nova verzija napravi pri
                    // prvom pokretanju, ne sme da proguta istoimenu zatečenu bazu sa podacima —
                    // takva se preuzima pod sufiksom. Ostali fajlovi se preskaču.
                    if (!fajl.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) continue;

                    // Ako je i zatečena baza prazna podrazumevana, nema šta da se spasava;
                    // kopija bi se samo pojavila kao lažna firma u spisku.
                    if (JePraznaPodrazumevanaBaza(fajl)) continue;

                    cilj = Path.Combine(odrediste, Path.GetFileNameWithoutExtension(fajl) + "_stara.db");
                    if (File.Exists(cilj)) continue;
                }

                File.Copy(fajl, cilj);
                kopirano++;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Fajl {Fajl} nije kopiran iz starog foldera", fajl);
            }
        }

        foreach (var podfolder in Directory.GetDirectories(izvor))
        {
            kopirano += KopirajFolder(podfolder, Path.Combine(odrediste, Path.GetFileName(podfolder)));
        }

        return kopirano;
    }

    /// <summary>
    /// Vraća aktivnu bazu na firmu koja je bila otvorena pre preimenovanja — sada iz kopije
    /// u novom folderu.
    ///
    /// Nije dovoljno proveriti samo da li aktivna baza postoji: ako je nova verzija već
    /// jednom pokrenuta, ona je napravila praznu podrazumevanu bazu i upisala je kao aktivnu.
    /// Takva baza postoji, ali je prazna i ne sme da pobedi nad zatečenim podacima.
    /// </summary>
    private static void PremapirajAktivnuBazu()
    {
        try
        {
            var aktivna = UserSettings.Instance.ActiveDbPath;
            if (!string.IsNullOrWhiteSpace(aktivna) && File.Exists(aktivna) &&
                !JePraznaPodrazumevanaBaza(aktivna))
            {
                return;
            }

            var staraAktivna = StariAppDataDirs
                .Select(dir => Path.Combine(dir, "settings.json"))
                .Where(File.Exists)
                .Select(putanja => System.Text.Json.JsonSerializer
                    .Deserialize<UserSettings>(File.ReadAllText(putanja))?.ActiveDbPath)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? aktivna;

            if (string.IsNullOrWhiteSpace(staraAktivna)) return;

            var kandidat = Path.Combine(BazeDir, Path.GetFileName(staraAktivna));

            // Ako je zatečena baza preuzeta pod sufiksom (zbog sudara imena), tu je i tražimo.
            if (!File.Exists(kandidat) || JePraznaPodrazumevanaBaza(kandidat))
            {
                var suSufiksom = Path.Combine(BazeDir,
                    Path.GetFileNameWithoutExtension(staraAktivna) + "_stara.db");
                if (File.Exists(suSufiksom)) kandidat = suSufiksom;
            }

            if (!File.Exists(kandidat)) return;

            UserSettings.Instance.ActiveDbPath = kandidat;
            UserSettings.Instance.Save();
            _dbPath = kandidat;

            Serilog.Log.Information("Aktivna baza premapirana na {Baza}", kandidat);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Aktivna baza iz starih podešavanja nije premapirana");
        }
    }

    /// <summary>
    /// Tačno kada je reč o podrazumevanoj bazi (plata.db) u kojoj još nema nijedne firme —
    /// takvu aplikacija sama napravi pri prvom pokretanju na praznom folderu.
    /// </summary>
    private static bool JePraznaPodrazumevanaBaza(string putanja)
    {
        if (!string.Equals(Path.GetFileName(putanja), "plata.db", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = putanja,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Firme;";
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) == 0;
        }
        catch
        {
            // Baza ne postoji ili nema tabelu Firme => sveže napravljena i prazna.
            return true;
        }
    }

    public static string DefaultDbPath => Path.Combine(BazeDir, "plata.db");

    private static string? _dbPath = null;

    /// <summary>
    /// Jednokratno preseljenje zatečenih baza u BazeDir.
    ///
    /// Baze su ranije završavale na dva mesta koja nisu bezbedna: u folderu Inno Setup
    /// instalacije (deinstalacija briše ceo folder) i u folderu sa izvornim kodom
    /// (čišćenje repozitorijuma briše podatke). Metoda ih premešta na jedino mesto koje
    /// preživljava i ažuriranje i deinstalaciju.
    ///
    /// Idempotentna je — kada nema šta da se preseli, ne radi ništa.
    /// </summary>
    private static void MigrirajZatecenBaze()
    {
        try
        {
            Directory.CreateDirectory(BazeDir);

            // Izvori: stara instalaciona lokacija + folder trenutno aktivne baze
            // (npr. C:\ERPi\ERPiZarade\Baze ako je putanja tako sačuvana u podešavanjima).
            var aktivna = UserSettings.Instance.ActiveDbPath;
            var izvori = new List<string>();

            if (!string.IsNullOrWhiteSpace(aktivna) && File.Exists(aktivna))
            {
                var folderAktivne = Path.GetDirectoryName(aktivna);
                if (!string.IsNullOrWhiteSpace(folderAktivne)) izvori.Add(folderAktivne);
            }
            izvori.Add(StariBazeDir);

            bool nestoPreseljeno = false;

            foreach (var izvor in izvori)
            {
                if (!Directory.Exists(izvor)) continue;
                if (string.Equals(Path.GetFullPath(izvor).TrimEnd('\\'),
                                  Path.GetFullPath(BazeDir).TrimEnd('\\'),
                                  StringComparison.OrdinalIgnoreCase)) continue;

                if (Directory.GetFiles(izvor, "*.db").Length > 0)
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }

                foreach (var izvorniFajl in Directory.GetFiles(izvor, "*.db"))
                {
                    var naziv = Path.GetFileName(izvorniFajl);
                    var odrediste = Path.Combine(BazeDir, naziv);

                    // Prvi izvor je folder aktivne baze — ona pobeđuje pri sudaru imena.
                    // Zatečena istoimena baza se čuva pod sufiksom radi poređenja.
                    if (File.Exists(odrediste))
                    {
                        var oznaka = SanitizujZaNazivFajla(
                            new DirectoryInfo(izvor).Parent?.Name ?? "stara");
                        odrediste = Path.Combine(BazeDir,
                            $"{Path.GetFileNameWithoutExtension(naziv)}_stara_{oznaka}.db");

                        if (File.Exists(odrediste))
                        {
                            Serilog.Log.Warning(
                                "Preskačem {Izvor} — odredište {Odrediste} već postoji", izvorniFajl, odrediste);
                            continue;
                        }
                    }

                    PremestiBazuSaPratecimFajlovima(izvorniFajl, odrediste);
                    nestoPreseljeno = true;

                    if (!string.IsNullOrWhiteSpace(aktivna) &&
                        string.Equals(Path.GetFullPath(izvorniFajl), Path.GetFullPath(aktivna),
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        UserSettings.Instance.ActiveDbPath = odrediste;
                        UserSettings.Instance.Save();
                        _dbPath = odrediste;
                    }
                }

                PremestiRezervneKopije(izvor);
            }

            if (nestoPreseljeno)
            {
                Serilog.Log.Information("Preseljenje baza u {Odrediste} je završeno", BazeDir);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Greška pri preseljenju zatečenih baza u {Odrediste}", BazeDir);
        }
    }

    /// <summary>
    /// Premešta bazu zajedno sa -wal i -shm fajlovima. Bez njih se gube transakcije
    /// koje SQLite još nije upisao u glavni fajl.
    /// </summary>
    private static void PremestiBazuSaPratecimFajlovima(string izvor, string odrediste)
    {
        File.Move(izvor, odrediste);
        Serilog.Log.Information("Preseljena baza {Izvor} -> {Odrediste}", izvor, odrediste);

        foreach (var nastavak in new[] { "-wal", "-shm" })
        {
            var prateciIzvor = izvor + nastavak;
            if (!File.Exists(prateciIzvor)) continue;

            var prateciCilj = odrediste + nastavak;
            if (File.Exists(prateciCilj)) File.Delete(prateciCilj);
            File.Move(prateciIzvor, prateciCilj);
        }
    }

    private static void PremestiRezervneKopije(string izvorniFolder)
    {
        try
        {
            var izvor = Path.Combine(izvorniFolder, "RezervneKopije");
            if (!Directory.Exists(izvor)) return;

            var cilj = Path.Combine(BazeDir, "RezervneKopije");
            Directory.CreateDirectory(cilj);

            foreach (var fajl in Directory.GetFiles(izvor, "*.db"))
            {
                var odrediste = Path.Combine(cilj, Path.GetFileName(fajl));
                if (File.Exists(odrediste)) continue;
                File.Move(fajl, odrediste);
            }

            if (Directory.GetFileSystemEntries(izvor).Length == 0) Directory.Delete(izvor);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Rezervne kopije iz {Izvor} nisu preseljene", izvorniFolder);
        }
    }

    private static string SanitizujZaNazivFajla(string s)
    {
        var nevalidni = Path.GetInvalidFileNameChars();
        return new string(s.Select(c => nevalidni.Contains(c) || c == ' ' ? '_' : c).ToArray());
    }

    public static string DbPath
    {
        get
        {
            if (_dbPath == null)
            {
                // Preseljenje zatečenih baza sa starih lokacija (radi samo prvi put)
                MigrirajZatecenBaze();

                if (_dbPath != null) return _dbPath;

                var savedPath = UserSettings.Instance.ActiveDbPath;
                if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
                {
                    _dbPath = savedPath;
                }
                else
                {
                    Directory.CreateDirectory(BazeDir);

                    var baze = Directory.GetFiles(BazeDir, "*.db");
                    _dbPath = baze.Length > 0 ? baze[0] : DefaultDbPath;

                    UserSettings.Instance.ActiveDbPath = _dbPath;
                    UserSettings.Instance.Save();
                }
            }
            return _dbPath;
        }
        set
        {
            _dbPath = value;
            UserSettings.Instance.ActiveDbPath = value;
            UserSettings.Instance.Save();
        }
    }

    private static int? _activeFirmaId;
    public static int? ActiveFirmaId
    {
        get => _activeFirmaId ??= UserSettings.Instance.ActiveFirmaId;
        set
        {
            _activeFirmaId = value;
            UserSettings.Instance.ActiveFirmaId = value;
            UserSettings.Instance.Save();

            // Osveži ime firme u glavnom prozoru
            try
            {
                var mainWin = Application.Current?.MainWindow as MainWindow;
                mainWin?.UcitajImeFirme();
            }
            catch { }
        }
    }

    private static int? _activeGodina;
    public static int? ActiveGodina
    {
        get => _activeGodina;
        set
        {
            _activeGodina = value;
            OsveziMainWindowActivePeriod();
        }
    }

    private static int? _activeMesec;
    public static int? ActiveMesec
    {
        get => _activeMesec;
        set
        {
            _activeMesec = value;
            OsveziMainWindowActivePeriod();
        }
    }

    private static void OsveziMainWindowActivePeriod()
    {
        try
        {
            var mainWin = Application.Current?.MainWindow as MainWindow;
            mainWin?.OsveziAktivniPeriodPrikaz();
        }
        catch { }
    }
}

