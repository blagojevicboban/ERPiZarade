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
/// SVP se za zaradu i dalje izvodi iz teksta u <see cref="Radnik.Radno_Mesto"/>. To je poznato
/// ograničenje modela (tačka 4.1.2 analize) — trajno rešenje je šifarnik `VrstaPrimanja`
/// iz Faze 2.1. Dok se ne uvede, bar postoji jedno mesto koje treba izmeniti.
///
/// Za naknade van radnog odnosa (Faza 2.3) šifra se <b>sastavlja</b> po propisanoj strukturi,
/// iz dva podatka koja se zna gde stoje: oznake vrste prihoda iz šifarnika vrsta ugovora i
/// statusa osiguranja primaoca iz samog ugovora.
/// </summary>
public static class SvpService
{
    /// <summary>Redovna zarada iz radnog odnosa.</summary>
    public const string RedovnaZarada = "101101000";

    /// <summary>Naknada zarade za bolovanje na teret poslodavca.</summary>
    public const string Bolovanje = "109101000";

    /// <summary>Zarada zaposlenog penzionera.</summary>
    public const string ZaposleniPenzioner = "101109000";

    /// <summary>Verzija Kataloga vrste prihoda — prva pozicija šifre; propisana je kao „1".</summary>
    private const string VerzijaKataloga = "1";

    public static string Odredi(ObracunPlate obracun)
    {
        // Naknada van radnog odnosa nije zarada i ne izvodi se iz radnog mesta: njen prihod
        // opisuje ugovor, a ne karton.
        if (obracun.Ugovor?.VrstaUgovora is { } vrstaUgovora)
            return Sastavi(obracun.Ugovor.TipPrimaoca, vrstaUgovora.Ovp);

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

    /// <summary>
    /// Sastavlja devetocifrenu šifru vrste prihoda po strukturi <c>V-PP-OVP-OL-B</c>:
    /// verzija kataloga (1 cifra), tip primaoca prihoda (2), oznaka vrste prihoda (3),
    /// oznaka poreske olakšice (2) i oznaka beneficiranog staža (1).
    ///
    /// Za prihode van radnog odnosa olakšica i beneficirani staž ne postoje, pa se popunjavaju
    /// nulama. Kad <paramref name="ovp"/> nije unet, vraća se prazno — bolje nego izmišljena
    /// šifra koja bi prošla generisanje a pala kod Poreske uprave; kontrolne provere na
    /// prazan OVP upozoravaju.
    /// </summary>
    public static string Sastavi(TipPrimaocaPrihoda tipPrimaoca, string? ovp, string ol = "00", string b = "0")
    {
        string ovpDeo = (ovp ?? "").Trim();
        if (ovpDeo.Length != 3 || !ovpDeo.All(char.IsDigit)) return "";

        return VerzijaKataloga
               + ((int)tipPrimaoca).ToString("D2")
               + ovpDeo
               + (string.IsNullOrWhiteSpace(ol) ? "00" : ol.Trim().PadLeft(2, '0'))
               + (string.IsNullOrWhiteSpace(b) ? "0" : b.Trim());
    }

    /// <summary>Devetocifrena šifra u polju radnog mesta je unet SVP, a ne opis posla.</summary>
    public static bool JeSvpSifra(string? tekst)
        => !string.IsNullOrWhiteSpace(tekst)
           && tekst.Length == 9
           && tekst.All(char.IsDigit);
}
