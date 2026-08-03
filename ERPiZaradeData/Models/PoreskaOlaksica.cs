using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>Način na koji olakšica deluje — razlika koja se ne sme pomešati.</summary>
public enum MehanizamOlaksice
{
    /// <summary>
    /// Poslodavac plati pun iznos pa traži povraćaj dela plaćenog poreza i doprinosa
    /// (npr. čl. 21v ZPDG). Obračun i PPP-PD prijava ostaju <b>nepromenjeni</b>.
    /// </summary>
    Povracaj = 0,

    /// <summary>
    /// Umanjuje se ono što se plaća. Umanjenje ulazi u obračun i deklariše se kroz MFP
    /// u PPP-PD prijavi.
    /// </summary>
    Oslobodjenje = 1
}

/// <summary>Šta se upisuje u vrednost MFP polja.</summary>
public enum IzvorMfp
{
    UmanjenjePoreza = 0,
    UmanjenjeDoprinosa = 1,
    OsnovicaPoreza = 2,
    OsnovicaDoprinosa = 3,
    ProcenatOlaksice = 4,
    FiksnaVrednost = 5
}

/// <summary>
/// Šifarnik poreskih olakšica.
///
/// Nijedna konkretna olakšica nije ugrađena u kod: program vodi zarade za više firmi, pa mora
/// da podrži i olakšice koje danas niko ne koristi, kao i one koje propis tek uvede. Zato je
/// olakšica <b>red u šifarniku</b>, isto kao vrsta primanja u Fazi 2.1.
///
/// <see cref="Sifra"/> je ista dvocifrena oznaka koja već stoji na pozicijama 7–8 SVP šifre u
/// <c>Radnik.Radno_Mesto</c>. Radnik zato ne dobija novo polje — veza se izvodi iz onoga što
/// se već unosi padajućom listom u kartonu.
/// </summary>
[Table("PoreskeOlaksice")]
public class PoreskaOlaksica
{
    [Key]
    public int PoreskaOlaksicaId { get; set; }

    /// <summary>Dvocifrena OL oznaka iz SVP šifre („01", „24", „32"…).</summary>
    [Required, MaxLength(2)]
    public string Sifra { get; set; } = "";

    [Required, MaxLength(100)]
    public string Naziv { get; set; } = "";

    [MaxLength(100)]
    public string PravniOsnov { get; set; } = "";

    public MehanizamOlaksice Mehanizam { get; set; } = MehanizamOlaksice.Povracaj;

    /// <summary>Procenat umanjenja odnosno povraćaja poreza (npr. 70.00 za 70%).</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcenatPoreza { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcenatDoprinosa { get; set; }

    /// <summary>
    /// Period važenja same olakšice po propisu — nezavisan od roka koji radnik ima u kartonu.
    /// Olakšica se primenjuje samo ako oba roka pokrivaju obračunski period.
    /// </summary>
    public DateTime? VaziOd { get; set; }
    public DateTime? VaziDo { get; set; }

    public bool Aktivna { get; set; } = true;

    [MaxLength(300)]
    public string Napomena { get; set; } = "";

    public ICollection<OlaksicaMfp> MfpDeklaracije { get; set; } = [];
}

/// <summary>
/// Kako se olakšica prijavljuje kroz multifunkcionalno polje PPP-PD prijave.
///
/// Oznaka uzima vrednosti <c>MFP.1</c>–<c>MFP.12</c>, ali <b>šta koje polje znači zavisi od
/// SVP šifre</b> — definisano je katalogom vrsta prihoda Poreske uprave, ne fiksnim pravilom.
/// Zato se mapiranje ne ugrađuje u kod nego unosi ovde.
/// </summary>
[Table("OlaksicaMfp")]
public class OlaksicaMfp
{
    [Key]
    public int OlaksicaMfpId { get; set; }

    [ForeignKey(nameof(Olaksica))]
    public int PoreskaOlaksicaId { get; set; }

    /// <summary>Oznaka polja: „MFP.1" do „MFP.12".</summary>
    [Required, MaxLength(10)]
    public string Oznaka { get; set; } = "";

    public IzvorMfp Izvor { get; set; } = IzvorMfp.UmanjenjePoreza;

    /// <summary>Vrednost koja se upisuje kada je izvor <see cref="IzvorMfp.FiksnaVrednost"/>.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal FiksnaVrednost { get; set; }

    public PoreskaOlaksica Olaksica { get; set; } = null!;
}
