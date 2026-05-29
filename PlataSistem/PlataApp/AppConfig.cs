using System;
using System.IO;
using System.Windows;

namespace PlataApp;

public static class AppConfig
{
    public static string DefaultDbPath => @"C:\PlataApp\plata.db";
    public static string BazeDir => Path.Combine(Path.GetDirectoryName(DefaultDbPath)!, "Baze");

    private static string? _dbPath = null;

    private static void PrilagodiNazivZajednickeBaze()
    {
        try
        {
            var bazeDir = BazeDir;
            var zajednickaDb = Path.Combine(bazeDir, "plata_zajednicka.db");
            if (File.Exists(zajednickaDb))
            {
                // Privremeno otvori bazu i pročitaj podatke o firmi
                using var db = PlataData.PlataDbContext.Create(zajednickaDb);
                var f = db.Firme.FirstOrDefault();
                if (f != null && !string.IsNullOrWhiteSpace(f.Pib))
                {
                    var pib = f.Pib.Trim();
                    var nazivClean = string.Concat(f.Naziv.Trim().Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                    var noviNaziv = $"firma_{pib}_{nazivClean}.db";
                    var novaPutanja = Path.Combine(bazeDir, noviNaziv);

                    // Zatvori konekcije i oslobodi lockove
                    db.Dispose();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                    // Preimenuj fajl na disku
                    if (!File.Exists(novaPutanja))
                    {
                        File.Move(zajednickaDb, novaPutanja);
                    }
                    else
                    {
                        File.Delete(zajednickaDb);
                    }

                    // Ažuriraj aktivnu putanju u podešavanjima
                    if (UserSettings.Instance.ActiveDbPath == zajednickaDb || string.IsNullOrEmpty(UserSettings.Instance.ActiveDbPath))
                    {
                        UserSettings.Instance.ActiveDbPath = novaPutanja;
                        UserSettings.Instance.Save();
                        _dbPath = novaPutanja;
                    }
                }
            }
        }
        catch { }
    }

    public static string DbPath
    {
        get
        {
            if (_dbPath == null)
            {
                // Prvo proveri i prilagodi naziv zajedničke baze ako postoji
                PrilagodiNazivZajednickeBaze();

                var savedPath = UserSettings.Instance.ActiveDbPath;
                if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
                {
                    _dbPath = savedPath;
                }
                else
                {
                    // Inicijalna migracija postojećeg fajla u Baze folder
                    var bazeDir = BazeDir;
                    try
                    {
                        Directory.CreateDirectory(bazeDir);

                        var defaultDest = Path.Combine(bazeDir, "plata_zajednicka.db");
                        var oldDb = DefaultDbPath;

                        if (!File.Exists(defaultDest) && File.Exists(oldDb))
                        {
                            File.Copy(oldDb, defaultDest);
                        }

                        if (File.Exists(defaultDest))
                        {
                            _dbPath = defaultDest;
                        }
                        else if (File.Exists(oldDb))
                        {
                            _dbPath = oldDb;
                        }
                        else
                        {
                            _dbPath = defaultDest; // Kreiraće se automatski
                        }
                    }
                    catch
                    {
                        _dbPath = DefaultDbPath;
                    }

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

