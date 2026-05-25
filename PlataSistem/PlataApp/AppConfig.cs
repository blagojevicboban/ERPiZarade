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
}

