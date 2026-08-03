using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Šablon teksta ugovora van radnog odnosa (Faza 2.3).
///
/// Tekst ugovora nije podatak o novcu nego <b>dokument</b>: propis mu određuje obavezne
/// elemente, ali formulacije bira firma. Zato stoji kao šablon koji se uređuje iz programa,
/// a ne kao tekst ugrađen u kod — isti razlog iz kog su vrste ugovora šifarnik.
///
/// U tekstu se koriste polja u vitičastim zagradama (<c>{PrimalacIme}</c>, <c>{Iznos}</c>…),
/// koja se pri generisanju zamenjuju podacima ugovora, primaoca i firme. Nepoznato polje se
/// <b>ne briše</b> — ostaje vidljivo u dokumentu, da se greška u šablonu primeti pri čitanju,
/// a ne tek kad je ugovor potpisan.
/// </summary>
[Table("SabloniUgovora")]
public class SablonUgovora
{
    [Key]
    public int SablonUgovoraId { get; set; }

    [Required, MaxLength(10)]
    public string Sifra { get; set; } = "";

    [Required, MaxLength(80)]
    public string Naziv { get; set; } = "";

    /// <summary>
    /// Vrsta ugovora za koju se šablon podrazumeva. <c>null</c> je opšti šablon, upotrebljiv
    /// uz svaku vrstu — tako se ne mora praviti kopija za svaku stopu.
    /// </summary>
    [ForeignKey(nameof(VrstaUgovora))]
    public int? VrstaUgovoraId { get; set; }

    public VrstaUgovora? VrstaUgovora { get; set; }

    /// <summary>Tekst dokumenta sa poljima u vitičastim zagradama.</summary>
    public string Tekst { get; set; } = "";

    public int Redosled { get; set; }

    public bool Aktivan { get; set; } = true;

    /// <summary>
    /// Šablon isporučen uz program. Sme se menjati — zato i postoji — ali se ne briše, da
    /// nadogradnja ne bi vratila obrisani red i time poništila odluku korisnika.
    /// </summary>
    public bool JeSistemski { get; set; }

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    [NotMapped]
    public string NazivSaSifrom => $"{Sifra} — {Naziv}";
}
