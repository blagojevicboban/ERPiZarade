using System;
using System.IO;
using System.Text.Json;

namespace PlataApp;

/// <summary>
/// Korisničke postavke programa — čuvaju se u JSON fajlu u AppData.
/// </summary>
public class UserSettings
{
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PlataSistem",
        "settings.json");

    private static UserSettings? _instance;
    public static UserSettings Instance => _instance ??= Load();

    // ── Postavke ───────────────────────────────────────────
    public bool PokretanjeMaximizovano { get; set; } = false;
    public int? ActiveFirmaId { get; set; }
    public string? ActiveDbPath { get; set; }

    // ── Load / Save ────────────────────────────────────────
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch { }
        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
}
