using System.Linq;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Određivanje šifre vrste prihoda (SVP) za obračun.
///
/// Ista logika je ranije stajala u tri kopije — u izvozu PPP-PD, u prikazu na ekranu prijave
/// i u godišnjem obrascu. Kopije su se već razišle, pa je isti obračun mogao dobiti jednu
/// šifru u prijavi a drugu u potvrdi radniku.
///
/// SVP se i dalje izvodi iz teksta u <see cref="Radnik.Radno_Mesto"/>. To je poznato
/// ograničenje modela (tačka 4.1.2 analize) — trajno rešenje je šifarnik `VrstaPrimanja`
/// iz Faze 2.1. Dok se ne uvede, bar postoji jedno mesto koje treba izmeniti.
/// </summary>
public static class SvpService
{
    /// <summary>Redovna zarada iz radnog odnosa.</summary>
    public const string RedovnaZarada = "101101000";

    /// <summary>Naknada zarade za bolovanje na teret poslodavca.</summary>
    public const string Bolovanje = "109101000";

    /// <summary>Zarada zaposlenog penzionera.</summary>
    public const string ZaposleniPenzioner = "101109000";

    public static string Odredi(ObracunPlate obracun)
    {
        var radnik = obracun.Radnik;
        string radnoMesto = radnik?.Radno_Mesto?.Trim() ?? "";

        // Bolovanje veće od zarade menja vrstu prihoda bez obzira na šifru u kartonu.
        if (obracun.BrutoBolovanje > obracun.BrutoZarada)
            return Bolovanje;

        if (JeSvpSifra(radnoMesto))
            return radnoMesto;

        if (radnoMesto.StartsWith("109", System.StringComparison.Ordinal))
            return ZaposleniPenzioner;

        return RedovnaZarada;
    }

    /// <summary>Devetocifrena šifra u polju radnog mesta je unet SVP, a ne opis posla.</summary>
    public static bool JeSvpSifra(string? tekst)
        => !string.IsNullOrWhiteSpace(tekst)
           && tekst.Length == 9
           && tekst.All(char.IsDigit);
}
