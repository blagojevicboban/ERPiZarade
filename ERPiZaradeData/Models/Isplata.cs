using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Vrsta isplate unutar obračunskog meseca.
///
/// Vrsta nije ukras — od nje zavisi <b>oznaka za konačnu isplatu</b> na PPP-PD prijavi
/// (element <c>OznakaZaKonacnu</c>): akontacija nije konačna isplata prihoda, pa se
/// prijavljuje sa „A", a sve ostalo sa „K".
/// </summary>
public enum VrstaIsplate
{
    /// <summary>Redovna mesečna zarada; kad je isplata jedna, ovo je ona.</summary>
    KonacnaZarada = 0,

    /// <summary>Isplata dela zarade pre konačnog obračuna meseca.</summary>
    Akontacija = 1,

    Bonus = 2,

    TrinaestaPlata = 3,

    Ostalo = 9
}

/// <summary>
/// Jedna isplata unutar obračunskog meseca (Faza 2.2).
///
/// Do sada je sve bilo vezano za par (godina, mesec), pa je mesec mogao imati tačno jednu
/// isplatu. Akontacija pa konačna isplata, bonus i 13. plata su međutim <b>zasebne isplate
/// istog meseca</b>: svaka ima svoj datum, svoju PPP-PD prijavu sa svojim BOP-om i svoj
/// paket naloga za prenos. Bez ovog entiteta druga isplata je mogla samo da pregazi prvu.
///
/// Veza ka prijavi ide preko <see cref="RedniBroj"/>, koji <see cref="PppPdPrijava"/> nosi
/// od Faze 1.1 upravo zbog ovoga — zato se ovde ne uvodi još jedna, duplirana veza.
///
/// Zaključavanje i storniranje ostaju na <see cref="ObracunPlate"/>: isplata je obuhvat,
/// ne stanje. Drugo mesto koje kaže „ovo je zaključano" bilo bi isti duplikat kao nekadašnji
/// <c>Zakljucan</c>/<c>Zakljucen</c>.
/// </summary>
[Table("Isplate")]
public class Isplata
{
    [Key]
    public int IsplataId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    /// <summary>
    /// Redni broj isplate u mesecu; 1 je prva. Isti broj nosi i PPP-PD prijava te isplate,
    /// pa je on veza između njih.
    /// </summary>
    public int RedniBroj { get; set; } = 1;

    public VrstaIsplate Vrsta { get; set; } = VrstaIsplate.KonacnaZarada;

    /// <summary>Slobodan opis („Akontacija za mart", „Bonus za projekat X").</summary>
    [MaxLength(80)]
    public string Opis { get; set; } = "";

    /// <summary>
    /// Datum kada novac ide radniku. Merodavan je za datum plaćanja na PPP-PD prijavi i za
    /// datum valute na nalozima — a on se kod akontacije razlikuje od konačne isplate, što
    /// je jedan od razloga zašto isplata mora biti zaseban zapis.
    /// </summary>
    public DateTime DatumIsplate { get; set; }

    public DateTime DatumKreiranja { get; set; } = DateTime.Now;

    public ICollection<ObracunPlate> Obracuni { get; set; } = [];

    /// <summary>
    /// Prva isplata meseca. Njoj pripadaju i obračuni bez <see cref="ObracunPlate.IsplataId"/> —
    /// svi zatečeni, i svi koje napravi kod koji za isplate ne zna.
    /// </summary>
    [NotMapped]
    public bool JePrva => RedniBroj <= 1;

    /// <summary>
    /// Oznaka za konačnu isplatu na PPP-PD prijavi (element <c>OznakaZaKonacnu</c>).
    /// Akontacija je „A", jer posle nje sledi konačan obračun istog prihoda; sve ostalo je „K".
    /// </summary>
    [NotMapped]
    public string OznakaZaKonacnuIsplatu => Vrsta == VrstaIsplate.Akontacija ? "A" : "K";

    /// <summary>
    /// Da li se na ovoj isplati skidaju obustave — rate kredita i samodoprinos.
    ///
    /// Skidaju se <b>samo na konačnoj zaradi</b>, i to je jedini način da rata ostane skinuta
    /// tačno jednom u mesecu: akontacija, bonus i 13. plata idu bez obustava, jer bi inače
    /// radnik u istom mesecu platio istu ratu dva ili tri puta. Zato mesec i sme imati samo
    /// jednu isplatu vrste <see cref="VrstaIsplate.KonacnaZarada"/>.
    /// </summary>
    [NotMapped]
    public bool NosiObustave => Vrsta == VrstaIsplate.KonacnaZarada;

    [NotMapped]
    public string PeriodStr => $"{Mesec:D2}/{Godina}";

    /// <summary>Naziv bez rednog broja — za svrhu na virmanu, gde redni broj ništa ne znači.</summary>
    [NotMapped]
    public string NazivKratki => string.IsNullOrWhiteSpace(Opis) ? NazivVrste(Vrsta) : Opis.Trim();

    /// <summary>Naziv za padajuće liste; opis ima prednost nad vrstom kad je unet.</summary>
    [NotMapped]
    public string Naziv => $"{RedniBroj}. {NazivKratki}";

    public static string NazivVrste(VrstaIsplate vrsta) => vrsta switch
    {
        VrstaIsplate.KonacnaZarada => "Konačna zarada",
        VrstaIsplate.Akontacija => "Akontacija",
        VrstaIsplate.Bonus => "Bonus",
        VrstaIsplate.TrinaestaPlata => "13. plata",
        _ => "Ostalo"
    };
}
