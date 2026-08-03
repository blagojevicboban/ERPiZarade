using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Arhiva prethodnih verzija obračuna (Faza 2.7).
///
/// Prekalkulacija briše zatečene obračune i računa iznova. Do sada je time nepovratno
/// nestajalo ono što je već isplaćeno i prijavljeno, pa se posle nije moglo utvrditi ni
/// šta se promenilo ni za koliko. Snimak se pravi <b>pre</b> brisanja.
///
/// Sadržaj snimka nije izbor iznosa nego ceo obračun u JSON obliku — u trenutku arhiviranja
/// se ne zna koje će polje kasnije biti sporno, a legacy kolone iz DBF-a ne prikazuje
/// nijedan ekran, ali od njih zavisi ponovni obračun.
/// </summary>
public static class VerzijeObracunaService
{
    private static readonly JsonSerializerOptions Opcije = new()
    {
        WriteIndented = false,
        // Navigacija na radnika i stavke bi povukla pola baze u snimak, a i sama je ciklična.
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    /// <summary>
    /// Arhivira zatečene obračune i vraća broj upisanih verzija. Ne poziva
    /// <c>SaveChanges</c> — zapis mora da uđe u istu transakciju sa brisanjem koje sledi,
    /// da arhiva ne ostane bez para ili obrnuto.
    /// </summary>
    /// <param name="razlog">Zašto se preračunava; ide u zapis i u revizioni trag.</param>
    public static int Arhiviraj(PlataDbContext db, IEnumerable<ObracunPlate> obracuni, string razlog)
    {
        var korisnik = AppSession.TrenutniKorisnik;
        DateTime sada = DateTime.Now;
        int upisano = 0;

        foreach (var o in obracuni)
        {
            db.ObracunVerzije.Add(new ObracunVerzija
            {
                Godina = o.Godina,
                Mesec = o.Mesec,
                RadnikId = o.RadnikId,
                IsplataId = o.IsplataId,
                BrojRadnika = o.Radnik?.BrojRadnika ?? 0,
                ImeRadnika = Skrati(o.Radnik?.ImeIPrezime ?? "", 60),
                Verzija = o.Verzija <= 0 ? 1 : o.Verzija,
                Razlog = Skrati(razlog, 300),
                KorisnickoIme = korisnik?.KorisnickoIme,
                Vreme = sada,
                BioZakljucan = o.Zakljucan,
                BioStorniran = o.Storniran,
                Bruto = o.BrutoZarada + o.BrutoBolovanje,
                PorezNaDohodak = o.PorezNaDohodak,
                DoprinosiRadnik = o.UkupniDoprinosi,
                DoprinosiPoslodavac = o.UkupniDoprinosiPoslodavca,
                NetoIsplata = o.NetoIsplata,
                Snimak = Snimi(o)
            });

            upisano++;
        }

        return upisano;
    }

    /// <summary>
    /// Redni broj koji nova verzija obračuna treba da nosi za datu isplatu i radnika:
    /// za jedan veći od najveće do sada arhivirane, odnosno 1 kad arhive nema.
    /// </summary>
    /// <param name="isplata">
    /// Isplata za koju se računa (Faza 2.2). Verzije se broje po isplati, jer prekalkulacija
    /// akontacije ne menja konačnu isplatu istog meseca. <c>null</c> je ceo period, kao pre
    /// uvođenja isplata.
    /// </param>
    public static int SledecaVerzija(
        PlataDbContext db, int godina, int mesec, int radnikId, Isplata? isplata = null)
    {
        var upit = db.ObracunVerzije
            .Where(v => v.Godina == godina && v.Mesec == mesec && v.RadnikId == radnikId);

        if (isplata != null)
        {
            int id = isplata.IsplataId;

            // Prva isplata obuhvata i arhivu bez upisane isplate — nastalu pre Faze 2.2 —
            // po istom pravilu po kom je obuhvata i među obračunima. Bez toga bi prvi
            // obračun posle nadogradnje ponovo dobio verziju 1, koja je već potrošena.
            upit = isplata.JePrva
                ? upit.Where(v => v.IsplataId == null || v.IsplataId == id)
                : upit.Where(v => v.IsplataId == id);
        }

        var arhivirane = upit.Select(v => (int?)v.Verzija).ToList();

        int najveca = arhivirane.Count == 0 ? 0 : arhivirane.Max(v => v ?? 0);
        return najveca + 1;
    }

    private static string Snimi(ObracunPlate o)
    {
        try
        {
            // Navigacione osobine se odvajaju za vreme serijalizacije: `Radnik` i `Stavke`
            // se čuvaju u svojim tabelama, a ovde bi snimak samo napunile duplikatima.
            var radnik = o.Radnik;
            var stavke = o.Stavke;
            o.Radnik = null!;
            o.Stavke = [];

            try
            {
                return JsonSerializer.Serialize(o, Opcije);
            }
            finally
            {
                o.Radnik = radnik;
                o.Stavke = stavke;
            }
        }
        catch (Exception ex)
        {
            // Snimak koji se ne može napraviti ne sme da obori prekalkulaciju — kolone sa
            // iznosima su i dalje upisane, pa se zna šta je bilo, samo ne i sve pojedinosti.
            return $"{{\"greska\":\"{ex.GetType().Name}\"}}";
        }
    }

    private static string Skrati(string tekst, int maxDuzina)
        => string.IsNullOrEmpty(tekst) || tekst.Length <= maxDuzina ? tekst : tekst[..maxDuzina];
}
