using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PlataApp;

public static class AppConfig
{
    /// <summary>
    /// Folder sa bazama. Namerno je IZVAN Velopack stabla (%LOCALAPPDATA%\PlataSistem\),
    /// čiji se sadržaj briše i ponovo raspakuje pri svakom ažuriranju programa.
    /// Isti obrazac koriste AccountingApp i SredstvaApp, zbog čega im baze preživljavaju update.
    /// </summary>
    public static string BazeDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlataApp", "Baze");

    /// <summary>Stara lokacija iz Inno Setup instalacije — koristi se samo kao izvor za migraciju.</summary>
    private static string StariBazeDir => @"C:\PlataApp\Baze";

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
            // (npr. C:\ERP\PlataSistem\Baze ako je putanja tako sačuvana u podešavanjima).
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

