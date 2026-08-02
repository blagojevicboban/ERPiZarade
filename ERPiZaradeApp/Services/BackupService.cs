using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Servis za upravljanje rezervnim kopijama (Backup & Restore) SQLite baza podataka.
/// </summary>
public class BackupService
{
    private static BackupService? _instance;
    public static BackupService Instance => _instance ??= new BackupService();

    /// <summary>
    /// Direktorijum gde se čuvaju automatski i sigurnosni backup-i.
    /// </summary>
    public string BackupDir => Path.Combine(AppConfig.BazeDir, "RezervneKopije");

    /// <summary>
    /// Pravi ručnu rezervnu kopiju na proizvoljnu putanju (koju korisnik odabere).
    /// </summary>
    public void NapraviRucniBackup(string destPath)
    {
        var dbPath = AppConfig.DbPath;
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException("Aktivna baza podataka ne postoji na navedenoj putanji!");
        }

        // Osiguraj da destinacioni direktorijum postoji
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Kopiranje
        File.Copy(dbPath, destPath, true);
    }

    /// <summary>
    /// Kreira automatsku kopiju trenutne baze podataka i vrši rotaciju starih kopija.
    /// </summary>
    /// <param name="preVracanja">Da li je kopija napravljena neposredno pre operacije vraćanja (safe restore).</param>
    /// <returns>Putanja kreirane kopije.</returns>
    public string NapraviAutomatskiBackup(bool preVracanja = false)
    {
        var dbPath = AppConfig.DbPath;
        if (!File.Exists(dbPath))
        {
            return string.Empty;
        }

        try
        {
            // Osiguraj direktorijum za rezervne kopije
            Directory.CreateDirectory(BackupDir);

            var dbName = Path.GetFileNameWithoutExtension(dbPath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tipSuffix = preVracanja ? "pre_vracanja" : "auto";
            var backupFileName = $"{dbName}_{tipSuffix}_{timestamp}.db";
            var backupPath = Path.Combine(BackupDir, backupFileName);

            // Kopiraj bazu podataka
            File.Copy(dbPath, backupPath, true);

            // Izvrši rotaciju i brisanje starijih kopija kako se ne bi gomilale
            RotirajStareKopije();

            return backupPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Vraća bazu podataka iz izabrane kopije.
    /// Pre vraćanja pravi automatsku sigurnosnu kopiju trenutne baze.
    /// </summary>
    /// <param name="sourcePath">Putanja kopije iz koje se vraćaju podaci.</param>
    /// <returns>True ako je vraćanje uspešno.</returns>
    public bool VratiBackup(string sourcePath, out string errorMsg)
    {
        errorMsg = string.Empty;

        if (!File.Exists(sourcePath))
        {
            errorMsg = "Izabrana rezervna kopija ne postoji!";
            return false;
        }

        try
        {
            var destPath = AppConfig.DbPath;

            // 1. Napravi automatsku sigurnosnu kopiju pre nego što prepišemo bazu
            NapraviAutomatskiBackup(preVracanja: true);

            // 2. Oslobodi sve SQLite konekcije iz pool-a kako bismo otključali fajl na disku
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // 3. Kopiraj rezervnu kopiju preko aktivne baze podataka
            File.Copy(sourcePath, destPath, true);

            return true;
        }
        catch (Exception ex)
        {
            errorMsg = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Učitava sve rezervne kopije iz Backup foldera i parsira njihove detalje.
    /// </summary>
    public List<BackupItem> UcitajIstorijuKopija()
    {
        var list = new List<BackupItem>();
        if (!Directory.Exists(BackupDir))
        {
            return list;
        }

        try
        {
            var files = Directory.GetFiles(BackupDir, "*.db");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var filename = fileInfo.Name;
                
                string tip = "Automatski";
                if (filename.Contains("pre_vracanja"))
                {
                    tip = "Pre vraćanja (Sigurnosni)";
                }
                else if (filename.Contains("_rucni_") || (!filename.Contains("_auto_") && !filename.Contains("pre_vracanja")))
                {
                    tip = "Ručni / Ostalo";
                }

                list.Add(new BackupItem
                {
                    NazivFajla = filename,
                    Putanja = file,
                    DatumKreiranja = fileInfo.LastWriteTime,
                    VelicinaMB = (double)fileInfo.Length / (1024 * 1024),
                    Tip = tip
                });
            }
        }
        catch { }

        // Sortiraj najnovije na početak
        return list.OrderByDescending(b => b.DatumKreiranja).ToList();
    }

    /// <summary>
    /// Briše stare automatske i sigurnosne kopije.
    /// Čuvamo poslednjih 15 automatskih i poslednjih 5 sigurnosnih kopija pre vraćanja.
    /// </summary>
    private void RotirajStareKopije()
    {
        if (!Directory.Exists(BackupDir)) return;

        try
        {
            var files = Directory.GetFiles(BackupDir, "*.db")
                .Select(f => new FileInfo(f))
                .ToList();

            // 1. Rotacija za automatske kopije
            var autoBackups = files
                .Where(f => f.Name.Contains("_auto_"))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (autoBackups.Count > 15)
            {
                for (int i = 15; i < autoBackups.Count; i++)
                {
                    try { autoBackups[i].Delete(); } catch { }
                }
            }

            // 2. Rotacija za sigurnosne kopije pre vraćanja
            var safetyBackups = files
                .Where(f => f.Name.Contains("pre_vracanja"))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (safetyBackups.Count > 5)
            {
                for (int i = 5; i < safetyBackups.Count; i++)
                {
                    try { safetyBackups[i].Delete(); } catch { }
                }
            }
        }
        catch { }
    }
}

/// <summary>
/// Model za stavku u istoriji rezervnih kopija.
/// </summary>
public class BackupItem
{
    public string NazivFajla { get; set; } = string.Empty;
    public string Putanja { get; set; } = string.Empty;
    public DateTime DatumKreiranja { get; set; }
    public double VelicinaMB { get; set; }
    public string Tip { get; set; } = string.Empty;

    public string VelicinaPrikaz => $"{VelicinaMB:F2} MB";
    public string DatumPrikaz => DatumKreiranja.ToString("dd.MM.yyyy HH:mm:ss");
}
