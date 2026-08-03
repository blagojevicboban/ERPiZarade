using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>Strana naloga na koju stavka ide.</summary>
public enum StranaKnjizenja
{
    Duguje = 0,
    Potrazuje = 1
}

/// <summary>
/// Šifarnik konta na koja se knjiži obračun zarada (Faza 3.1).
///
/// Konta troškova već stoje uz vrstu primanja (<see cref="VrstaPrimanja.Konto"/>) i uz vrstu
/// ugovora (<see cref="VrstaUgovora.Konto"/>) — tamo im je i mesto, jer se trošak deli po
/// tome <b>šta</b> je isplaćeno. Protivstavu tog troška, međutim, ne određuje vrsta primanja
/// nego <b>uloga iznosa u nalogu</b>: neto obaveza prema radniku, porez na teret zaposlenog,
/// doprinosi na teret poslodavca, obustava. Tih uloga ima konačno mnogo i kod ih traži po
/// imenu, pa svaka ovde ima svoj red sa <see cref="Kljuc"/>.
///
/// Brojevi konta su <b>početna vrednost iz Kontnog okvira</b>, a ne pravilo u kodu: firma
/// koja vodi analitiku (npr. 520-1 po poslovnoj jedinici) ih menja ovde, bez nove verzije.
/// Isto pravilo po kome su uvedene <see cref="VrstaPrimanja"/> i <see cref="PoreskaOlaksica"/>.
/// </summary>
[Table("KontaKnjizenja")]
public class KontoKnjizenja
{
    [Key]
    public int KontoKnjizenjaId { get; set; }

    /// <summary>
    /// Sistemski ključ po kome kod traži konto (npr. „OBAVEZA_NETO_ZARADA"). Ne menja se i
    /// ne prevodi — naziv je taj koji korisnik čita.
    /// </summary>
    [Required, MaxLength(40)]
    public string Kljuc { get; set; } = "";

    [Required, MaxLength(120)]
    public string Naziv { get; set; } = "";

    /// <summary>
    /// Broj konta iz kontnog plana firme. Prazno znači da knjiženje ne sme da se izveze —
    /// kontrolna provera to javlja, jer bi nalog bez konta pao tek pri uvozu u glavnu knjigu.
    /// </summary>
    [MaxLength(20)]
    public string Konto { get; set; } = "";

    /// <summary>
    /// Strana na koju iznos ide. Nije ukras: po njoj se sabira kontrola ravnoteže, pa
    /// zamena strane odmah obara nalog umesto da tiho izokrene stavku.
    /// </summary>
    public StranaKnjizenja Strana { get; set; }

    /// <summary>Redosled prikaza u šifarniku i u nalogu.</summary>
    public int Redosled { get; set; }

    [MaxLength(250)]
    public string Napomena { get; set; } = "";

    [NotMapped]
    public string StranaTekst => Strana == StranaKnjizenja.Duguje ? "Duguje" : "Potražuje";
}
