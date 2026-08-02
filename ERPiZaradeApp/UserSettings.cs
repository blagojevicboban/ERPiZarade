using System;
using System.IO;
using System.Text.Json;

namespace ERPiZaradeApp;

/// <summary>
/// Korisničke postavke programa — čuvaju se u JSON fajlu u LocalAppData.
///
/// Lokacija je usklađena sa ERPiFinansijeApp i ERPiSredstvaApp: %LOCALAPPDATA%\ERPiZaradeApp\.
/// Ranije se koristio %APPDATA%\ERPiZarade\ (Roaming), što je bilo i neusklađeno sa
/// ostalim modulima i zbunjujuće jer Velopack pod imenom "ERPiZarade" drži sasvim
/// drugi folder (%LOCALAPPDATA%\ERPiZarade\) koji se briše pri svakom ažuriranju.
/// </summary>
public class UserSettings
{
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ERPiZaradeApp",
        "settings.json");

    /// <summary>Zatečena lokacija iz ranijih verzija — čita se jednom, pa se briše.</summary>
    private static readonly string _staraPutanja = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ERPiZarade",
        "settings.json");

    private static UserSettings? _instance;
    public static UserSettings Instance => _instance ??= Load();

    // ── Postavke ───────────────────────────────────────────
    public bool PokretanjeMaximizovano { get; set; } = false;
    public bool ValidacijaJmbgOmogucena { get; set; } = true;
    public int? ActiveFirmaId { get; set; }
    public string? ActiveDbPath { get; set; }

    // ── PPP-PD podaci o isplatiocu (pamte se za sledeće otvaranje) ──
    public string? PppPdSediste { get; set; }
    public string? PppPdTelefon { get; set; }
    public string? PppPdAdresa { get; set; }
    public string? PppPdEmail { get; set; }
    public string? PppPdVrstaPrijave { get; set; }
    public string? PppPdOznakaZaKonacnu { get; set; }
    public string? PppPdNajnizaOsnovica { get; set; }
    public string? PppPdTipIsplatioca { get; set; }

    // ── SMTP za slanje platnih listića ─────────────────────
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpKoristiSsl { get; set; } = true;
    public string? SmtpKorisnik { get; set; }

    /// <summary>
    /// Lozinka SMTP naloga, šifrovana Windows DPAPI-jem i vezana za nalog na ovom računaru.
    /// Ne upisuje se u čitljivom obliku — <c>settings.json</c> stoji u profilu korisnika i
    /// čita ga svako ko dođe do fajla. Čita se i piše preko <see cref="SmtpLozinka"/>.
    /// </summary>
    public string? SmtpLozinkaZasticena { get; set; }

    /// <summary>Adresa pošiljaoca; ako je prazna, koristi se e-mail firme.</summary>
    public string? SmtpAdresaPosiljaoca { get; set; }
    public string? SmtpImePosiljaoca { get; set; }

    /// <summary>Da li se PDF listića štiti lozinkom pre slanja.</summary>
    public bool ListiciZastitaLozinkom { get; set; } = true;

    /// <summary>
    /// SMTP lozinka u čitljivom obliku. Nije deo JSON zapisa — serijalizuje se samo
    /// šifrovani oblik iz <see cref="SmtpLozinkaZasticena"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SmtpLozinka
    {
        get => TajnaZastita.Otkrij(SmtpLozinkaZasticena);
        set => SmtpLozinkaZasticena = TajnaZastita.Zastiti(value);
    }

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

            // Jednokratno preuzimanje podešavanja sa stare Roaming lokacije, da se ne
            // izgube aktivna firma, izbor baze i zapamćeni PPP-PD podaci.
            if (File.Exists(_staraPutanja))
            {
                var stariJson = File.ReadAllText(_staraPutanja);
                var preuzeto = JsonSerializer.Deserialize<UserSettings>(stariJson);
                if (preuzeto != null)
                {
                    preuzeto.Save();
                    try { File.Delete(_staraPutanja); } catch { }

                    Serilog.Log.Information(
                        "Podešavanja preuzeta sa stare lokacije {Stara} u {Nova}", _staraPutanja, _settingsPath);
                    return preuzeto;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Greška pri učitavanju podešavanja");
        }
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
