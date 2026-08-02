using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Šifarnik vrsta primanja.
///
/// Do sada je svako novo primanje značilo novu kolonu u <see cref="ObracunPlate"/> i novu
/// migraciju — tabela je zato narasla na preko šezdeset kolona. Sa šifarnikom se novo
/// primanje dodaje kao <b>red</b>, bez izmene šeme baze.
///
/// Ovde stoji i sve što se o primanju mora znati da bi se obračunalo i proknjižilo: da li
/// je oporezivo, ulazi li u osnovicu doprinosa, do kog iznosa je neoporezivo i na koji
/// konto ide.
/// </summary>
[Table("VrstePrimanja")]
public class VrstaPrimanja
{
    [Key]
    public int VrstaPrimanjaId { get; set; }

    /// <summary>Kratka šifra za prepoznavanje u kodu i izveštajima (npr. „ZAR", „PRE").</summary>
    [Required, MaxLength(10)]
    public string Sifra { get; set; } = "";

    [Required, MaxLength(80)]
    public string Naziv { get; set; } = "";

    /// <summary>
    /// Šifra vrste prihoda za PPP-PD. Prazno znači da primanje ne ide u prijavu zasebno,
    /// nego ulazi u zbir zarade.
    /// </summary>
    [MaxLength(9)]
    public string Svp { get; set; } = "";

    public bool Oporezivo { get; set; } = true;

    public bool UlaziUOsnovicuDoprinosa { get; set; } = true;

    /// <summary>
    /// Mesečni neoporezivi iznos; preko njega primanje postaje oporezivo. Nula znači da
    /// neoporezivog dela nema.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal NeoporeziviLimit { get; set; }

    /// <summary>Konto za automatsko knjiženje u ERPiFinansije (Faza 3.1).</summary>
    [MaxLength(10)]
    public string Konto { get; set; } = "";

    /// <summary>Redosled prikaza na platnom listiću i u izveštajima.</summary>
    public int Redosled { get; set; }

    public bool Aktivna { get; set; } = true;

    /// <summary>
    /// Vrste koje obračunski engine popunjava sam. Ne smeju se brisati niti im se sme
    /// menjati šifra — kod ih traži po njoj.
    /// </summary>
    public bool JeSistemska { get; set; }

    public ICollection<ObracunStavka> Stavke { get; set; } = [];
}

/// <summary>
/// Jedno primanje unutar obračuna — zamena za „široku" tabelu sa kolonom po primanju.
/// </summary>
[Table("ObracunStavke")]
public class ObracunStavka
{
    [Key]
    public int ObracunStavkaId { get; set; }

    [ForeignKey(nameof(Obracun))]
    public int ObracunPlateId { get; set; }

    [ForeignKey(nameof(VrstaPrimanja))]
    public int VrstaPrimanjaId { get; set; }

    /// <summary>Sati na koje se primanje odnosi; nula za primanja koja se ne mere satima.</summary>
    public int Sati { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Iznos { get; set; }

    public ObracunPlate Obracun { get; set; } = null!;
    public VrstaPrimanja VrstaPrimanja { get; set; } = null!;
}
