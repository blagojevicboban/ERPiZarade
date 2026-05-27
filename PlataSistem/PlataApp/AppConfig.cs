using System;
using System.IO;
using System.Windows;

namespace PlataApp;

public static class AppConfig
{
    private static string? _dbPath = null;
    public static string DbPath
    {
        get
        {
            if (_dbPath == null)
            {
                // 1. Centralna razvojna / produkcijska baza (uvek preferovana)
                var centralDb = @"C:\PLATA\PlataSistem\plata.db";
                if (File.Exists(centralDb))
                {
                    _dbPath = centralDb;
                }
                else
                {
                    // 2. Fallback: plata.db u istom direktorijumu gde je pokrenut .exe
                    var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    _dbPath = Path.Combine(exeDir, "plata.db");
                }
            }
            return _dbPath;
        }
        set => _dbPath = value;
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

