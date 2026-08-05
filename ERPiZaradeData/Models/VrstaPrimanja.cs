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
    /// Mesečni neoporezivi iznos; prekoračenje po zakonu postaje oporezivo. Nula znači da
    /// gornje granice nema, pa je kod neoporezive vrste ceo iznos neoporeziv — na takvu
    /// vrstu u upotrebi upozoravaju kontrolne provere, da limit ne bi ostao neunet.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal NeoporeziviLimit { get; set; }

    /// <summary>Konto za automatsko knjiženje u ERPiFinansije (Faza 3.1).</summary>
    [MaxLength(10)]
    public string Konto { get; set; } = "";

    /// <summary>
    /// Naknada koja pada na teret RFZO i refundira se poslodavcu (Faza 2.6). Iznos takvih
    /// stavki ulazi u obrazac OZ-10.
    ///
    /// Stoji ovde, a ne u kodu, iz istog razloga iz kog tu stoji i <see cref="Svp"/>: koja
    /// naknada ide na teret Fonda propisuje Zakon o zdravstvenom osiguranju, a program vodi
    /// zarade za više firmi. Podrazumevano je označeno samo „bolovanje preko 30 dana"; ko
    /// refundira i naknadu za povredu na radu ili negu člana porodice, označi i njih.
    /// </summary>
    public bool NaTeretFonda { get; set; }

    /// <summary>
    /// Iznos je radniku već isplaćen van ovog obračuna (npr. prekoračenje dnevnice, isplaćeno
    /// gotovinom ili na račun kroz putni nalog u ERPiFinansije — Faza 3.2). Ulazi u bruto,
    /// poresku osnovicu i osnovicu doprinosa kao i svako drugo primanje, ali se <b>ne</b>
    /// isplaćuje ponovo kroz platni spisak — <see cref="ObracunService"/> ga oduzima od neto
    /// isplate posle što uveća poresku osnovicu, jer bi inače taj novac otišao radniku dvaput.
    /// </summary>
    public bool VecIsplacenoVanObracuna { get; set; }

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

    /// <summary>Ukupno isplaćeno po ovoj vrsti primanja.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal Iznos { get; set; }

    /// <summary>
    /// Deo iznosa koji je ušao u poresku osnovicu. Kod zarade je jednak <see cref="Iznos"/>;
    /// kod neoporezivih primanja je to samo <b>prekoračenje</b> neoporezivog limita, koje po
    /// zakonu postaje oporezivo.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal OporeziviDeo { get; set; }

    /// <summary>Deo koji je ostao neoporezovan — isplaćuje se radniku, ali ne ulazi u osnovicu.</summary>
    public decimal NeoporeziviDeo => Iznos - OporeziviDeo;

    public ObracunPlate Obracun { get; set; } = null!;
    public VrstaPrimanja VrstaPrimanja { get; set; } = null!;
}

/// <summary>
/// Primanje uneto za radnika u obračunskom periodu — ulaz iz kog obračun pravi stavku.
///
/// Postoji da bi se novo primanje moglo <b>uneti</b> bez izmene baze, isto kao što se u
/// šifarnik dodaje bez izmene baze. Ranije je svako primanje moralo da dobije kolonu u
/// <see cref="RadniSat"/> i u <see cref="ObracunPlate"/>.
/// </summary>
[Table("UnetaPrimanja")]
public class UnetoPrimanje : IPripadaIsplati
{
    [Key]
    public int UnetoPrimanjeId { get; set; }

    [ForeignKey(nameof(Radnik))]
    public int RadnikId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    /// <summary>
    /// Isplata kojoj primanje pripada (Faza 3.2). <c>null</c> znači <b>prvu isplatu svog
    /// perioda</b> — isto pravilo kao <see cref="ObracunPlate.IsplataId"/>, primenjeno na
    /// jednom mestu u <c>IsplataService.Obuhvat</c>. Bez ovoga bi isti unos ušao i u akontaciju
    /// i u konačnu zaradu istog meseca — dvaput obračunat.
    /// </summary>
    public int? IsplataId { get; set; }

    public Isplata? Isplata { get; set; }

    [ForeignKey(nameof(VrstaPrimanja))]
    public int VrstaPrimanjaId { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Iznos { get; set; }

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    public Radnik Radnik { get; set; } = null!;
    public VrstaPrimanja VrstaPrimanja { get; set; } = null!;
}
