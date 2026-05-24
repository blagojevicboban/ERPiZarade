using System;
using System.IO;

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
                // 1. Provera da li plata.db postoji u istom direktorijumu gde je pokrenut .exe
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var localDb = Path.Combine(exeDir, "plata.db");
                if (File.Exists(localDb))
                {
                    _dbPath = localDb;
                }
                else
                {
                    // 2. Fallback na razvojnu / Clipper lokaciju
                    var defaultDb = @"C:\PLATA\PlataSistem\plata.db";
                    _dbPath = defaultDb;
                }
            }
            return _dbPath;
        }
        set => _dbPath = value;
    }

    private static string? _dbfDir = null;
    public static string DbfDir
    {
        get
        {
            if (_dbfDir == null)
            {
                // 1. Podrazumevana lokacija za Clipper podatke
                var defaultDbf = @"C:\PLATA\KOR28";
                if (Directory.Exists(defaultDbf))
                {
                    _dbfDir = defaultDbf;
                }
                else
                {
                    // 2. Lokalni folder KOR28 u direktorijumu aplikacije
                    var localDbf = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KOR28");
                    _dbfDir = localDbf;
                }
            }
            return _dbfDir;
        }
        set => _dbfDir = value;
    }
}
