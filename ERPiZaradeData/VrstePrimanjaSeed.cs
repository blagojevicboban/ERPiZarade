using System.Collections.Generic;
using ERPiZaradeData.Models;

namespace ERPiZaradeData;

/// <summary>
/// Podrazumevani sadržaj šifarnika vrsta primanja.
///
/// Sistemske vrste odgovaraju jedna-na-jedan komponentama iz kojih se danas sastavlja bruto
/// iznos u <c>ObracunService</c>. Zahvaljujući tome uvođenje šifarnika ne menja nijedan
/// obračunati iznos — stavke su verno razlaganje istog zbira.
///
/// Šifre su deo ugovora sa kodom: engine traži vrstu po šifri, pa se one ne smeju menjati.
/// </summary>
public static class VrstePrimanjaSeed
{
    // ── Šifre sistemskih vrsta ───────────────────────────────────────
    public const string OsnovnaZarada = "ZAR";
    public const string MinuliRad = "MIN";
    public const string Prekovremeni = "PRE";
    public const string NocniRad = "NOC";
    public const string RadPraznikom = "PRA";
    public const string NeradniPraznik = "NPR";
    public const string RadNedeljom = "NED";
    public const string GodisnjiOdmor = "GOD";
    public const string Bolovanje = "BOL";
    public const string Bolovanje100 = "B10";
    public const string BolovanjePreko30 = "B60";
    public const string Porodiljsko = "POR";
    public const string PlacenoOdsustvo = "PLO";
    public const string PlacenoZakonski = "PLZ";
    public const string Stimulacija = "STI";
    public const string TopliObrok = "TOP";
    public const string Regres = "REG";
    public const string BrutoDodatak = "VAR";

    private const string SvpZarada = "101101000";
    private const string SvpBolovanje = "109101000";

    /// <summary>Konto troškova zarada; menja se u šifarniku ako firma koristi drugu analitiku.</summary>
    private const string KontoZarade = "520";
    private const string KontoNaknade = "521";

    public static List<VrstaPrimanja> Podrazumevane() =>
    [
        // ── Zarada za obavljeni rad ──────────────────────────────────
        Sistemska(OsnovnaZarada,   "Osnovna zarada",                SvpZarada,    KontoZarade,  10),
        Sistemska(MinuliRad,       "Minuli rad",                    SvpZarada,    KontoZarade,  20),
        Sistemska(Prekovremeni,    "Prekovremeni rad",              SvpZarada,    KontoZarade,  30),
        Sistemska(NocniRad,        "Noćni rad",                     SvpZarada,    KontoZarade,  40),
        Sistemska(RadPraznikom,    "Rad državnim praznikom",        SvpZarada,    KontoZarade,  50),
        Sistemska(RadNedeljom,     "Rad nedeljom",                  SvpZarada,    KontoZarade,  60),
        Sistemska(Stimulacija,     "Stimulacija",                   SvpZarada,    KontoZarade,  70),
        Sistemska(BrutoDodatak,    "Bruto dodatak",                 SvpZarada,    KontoZarade,  80),

        // ── Naknade zarade (ne radi se, a plaća se) ──────────────────
        Sistemska(GodisnjiOdmor,   "Godišnji odmor",                SvpZarada,    KontoNaknade, 110),
        Sistemska(NeradniPraznik,  "Neradni državni praznik",       SvpZarada,    KontoNaknade, 120),
        Sistemska(PlacenoOdsustvo, "Plaćeno odsustvo",              SvpZarada,    KontoNaknade, 130),
        Sistemska(PlacenoZakonski, "Plaćeno odsustvo po zakonu",    SvpZarada,    KontoNaknade, 140),
        Sistemska(Bolovanje,       "Bolovanje do 30 dana",          SvpBolovanje, KontoNaknade, 150),
        Sistemska(Bolovanje100,    "Bolovanje 100%",                SvpBolovanje, KontoNaknade, 160),
        Sistemska(BolovanjePreko30,"Bolovanje preko 30 dana",       SvpBolovanje, KontoNaknade, 170),
        Sistemska(Porodiljsko,     "Porodiljsko odsustvo",          SvpBolovanje, KontoNaknade, 180),

        // ── Ostala primanja koja ulaze u zaradu ──────────────────────
        Sistemska(TopliObrok,      "Topli obrok",                   SvpZarada,    KontoZarade,  210),
        Sistemska(Regres,          "Regres za godišnji odmor",      SvpZarada,    KontoZarade,  220),

        // ── Neoporeziva primanja ─────────────────────────────────────
        // Nisu sistemska: engine ih još ne obračunava, ali stoje u šifarniku da se mogu
        // uneti ručno i da se vidi kako se novo primanje dodaje bez izmene šeme baze.
        // Limiti su promenljivi propisom, pa se održavaju kroz šifarnik.
        Neoporeziva("PRV", "Naknada troškova prevoza", 300),
        Neoporeziva("JUB", "Jubilarna nagrada",        310),
        Neoporeziva("SOL", "Solidarna pomoć",          320),
        Neoporeziva("POK", "Poklon deci zaposlenih",   330)
    ];

    private static VrstaPrimanja Sistemska(string sifra, string naziv, string svp, string konto, int redosled)
        => new()
        {
            Sifra = sifra,
            Naziv = naziv,
            Svp = svp,
            Konto = konto,
            Oporezivo = true,
            UlaziUOsnovicuDoprinosa = true,
            Redosled = redosled,
            Aktivna = true,
            JeSistemska = true
        };

    private static VrstaPrimanja Neoporeziva(string sifra, string naziv, int redosled)
        => new()
        {
            Sifra = sifra,
            Naziv = naziv,
            Svp = "",
            Konto = "529",
            Oporezivo = false,
            UlaziUOsnovicuDoprinosa = false,
            // Nula znači „limit nije unet"; unosi se u šifarniku prema važećem propisu.
            NeoporeziviLimit = 0m,
            Redosled = redosled,
            Aktivna = true,
            JeSistemska = false
        };
}
