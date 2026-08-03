using System.ComponentModel.DataAnnotations;

namespace ERPiZaradeData.Models;

/// <summary>Radnja nad obračunom koja se beleži u revizioni trag.</summary>
public enum AkcijaObracuna
{
    Kreiran = 0,
    Prekalkulisan = 1,
    Zakljucan = 2,
    Otkljucan = 3,
    Obrisan = 4,
    Storniran = 5,
    PppPdGenerisan = 6,

    /// <summary>Dodata isplata u mesecu (Faza 2.2).</summary>
    IsplataDodata = 7,

    /// <summary>Obrisana isplata u mesecu; obračune nije nosila.</summary>
    IsplataObrisana = 8
}

/// <summary>
/// Revizioni trag nad obračunima — isti obrazac kao <c>NalogAudit</c> u ERPiFinansije.
/// Period i korisničko ime su namerno denormalizovani da zapis ostane čitljiv i pošto
/// se obračun obriše ili korisnik ukloni.
/// </summary>
public class ObracunAudit
{
    [Key]
    public int ObracunAuditId { get; set; }

    /// <summary>Godina obračuna nad kojim je radnja izvršena.</summary>
    public int Godina { get; set; }

    /// <summary>Mesec obračuna nad kojim je radnja izvršena.</summary>
    public int Mesec { get; set; }

    /// <summary>Broj radnika kad se radnja tiče jednog radnika; null za radnju nad celim periodom.</summary>
    public int? BrojRadnika { get; set; }

    /// <summary>Ime radnika u trenutku radnje — ostaje čitljivo i posle brisanja kartona.</summary>
    [MaxLength(60)]
    public string? ImeRadnika { get; set; }

    public AkcijaObracuna Akcija { get; set; }

    public int? KorisnikId { get; set; }

    [MaxLength(100)]
    public string? KorisnickoIme { get; set; }

    /// <summary>Slobodan opis (npr. razlog otključavanja ili broj obuhvaćenih obračuna).</summary>
    [MaxLength(300)]
    public string? Detalji { get; set; }

    public DateTime Vreme { get; set; } = DateTime.Now;
}
