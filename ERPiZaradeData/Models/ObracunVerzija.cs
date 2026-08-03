using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Arhivirana prethodna verzija obračuna (Faza 2.7).
///
/// Prekalkulacija briše zatečeni rezultat i računa iznova. Do sada je time nestajalo ono
/// što je već isplaćeno i prijavljeno, pa se posle nije moglo utvrditi šta se tačno
/// promenilo. Zapis se pravi <b>pre</b> brisanja i sadrži i pojedinačne iznose i pun snimak
/// obračuna.
///
/// Veza ka <c>ObracuniPlata</c> je namerno <b>bez stranog ključa</b>: red koji se arhivira
/// upravo nestaje, pa bi ključ pao ili povukao arhivu za sobom. Period, broj i ime radnika
/// su denormalizovani, isto kao u <see cref="ObracunAudit"/>, da zapis ostane čitljiv i
/// pošto se karton radnika obriše.
/// </summary>
[Table("ObracunVerzije")]
public class ObracunVerzija : IPripadaIsplati
{
    [Key]
    public int ObracunVerzijaId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    public int RadnikId { get; set; }

    /// <summary>
    /// Isplata kojoj je arhivirani obračun pripadao (Faza 2.2). Verzije se broje po isplati,
    /// pa bi bez ovoga prekalkulacija akontacije podigla redni broj i konačnoj isplati.
    /// Bez stranog ključa, iz istog razloga kao i ostatak zapisa; <c>null</c> je prva isplata.
    /// </summary>
    public int? IsplataId { get; set; }

    public int BrojRadnika { get; set; }

    [MaxLength(60)]
    public string ImeRadnika { get; set; } = "";

    /// <summary>Redni broj arhivirane verzije — onaj koji je obračun nosio pre zamene.</summary>
    public int Verzija { get; set; } = 1;

    /// <summary>Zašto je obračun preračunat; slobodan opis kao u revizionom tragu.</summary>
    [MaxLength(300)]
    public string Razlog { get; set; } = "";

    [MaxLength(100)]
    public string? KorisnickoIme { get; set; }

    public DateTime Vreme { get; set; } = DateTime.Now;

    /// <summary>Da li je arhivirana verzija bila zaključana u trenutku zamene.</summary>
    public bool BioZakljucan { get; set; }

    /// <summary>Da li je arhivirana verzija bila stornirana.</summary>
    public bool BioStorniran { get; set; }

    // ── Iznosi zbog kojih se verzija i čuva ─────────────────────────
    // Stoje kao kolone da bi poređenje „šta se promenilo" bilo upit, a ne raspakivanje snimka.
    [Column(TypeName = "decimal(14,2)")]
    public decimal Bruto { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PorezNaDohodak { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosiRadnik { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosiPoslodavac { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoIsplata { get; set; }

    /// <summary>
    /// Pun snimak obračuna u JSON obliku. Kolone iznad pokrivaju ono što se gleda,
    /// a snimak čuva i sve ostalo — uključujući legacy kolone koje nijedan izveštaj
    /// ne prikazuje, ali od kojih zavisi ponovni obračun.
    /// </summary>
    public string Snimak { get; set; } = "";
}
