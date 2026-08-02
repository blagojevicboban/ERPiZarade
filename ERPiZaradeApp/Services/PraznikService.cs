using System;
using System.Collections.Generic;
using System.Linq;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Kalendar neradnih dana i mesečni fond sati.
///
/// Praznici se računaju po Zakonu o državnim i drugim praznicima u Republici Srbiji, a
/// zatim upisuju u bazu, gde ih korisnik može menjati i dopunjavati. Računanje bez upisa
/// ne bi pokrilo sopstvene neradne dane firme (slava, kolektivni godišnji odmor).
/// </summary>
public class PraznikService
{
    /// <summary>Puno radno vreme po danu; osnov za mesečni fond sati.</summary>
    public const int SatiPoRadnomDanu = 8;

    private readonly PlataDbContext _db;

    public PraznikService(PlataDbContext db) => _db = db;

    // ── Zakonski praznici ────────────────────────────────────────────

    /// <summary>
    /// Državni praznici po zakonu. Za njih važi pravilo da se, ako padnu u nedelju, ne
    /// radi prvog narednog radnog dana — za verske praznike to pravilo ne važi.
    /// </summary>
    private static readonly (int Mesec, int Dan, string Naziv)[] DrzavniPraznici =
    [
        (1, 1, "Nova godina"),
        (1, 2, "Nova godina — drugi dan"),
        (2, 15, "Sretenje — Dan državnosti"),
        (2, 16, "Dan državnosti — drugi dan"),
        (5, 1, "Praznik rada"),
        (5, 2, "Praznik rada — drugi dan"),
        (11, 11, "Dan primirja u Prvom svetskom ratu")
    ];

    /// <summary>Verski praznici sa nepokretnim datumom.</summary>
    private static readonly (int Mesec, int Dan, string Naziv)[] VerskiNepokretni =
    [
        (1, 7, "Božić")
    ];

    /// <summary>Zakonski neradni dani za godinu, uključujući pomeranja zbog nedelje.</summary>
    public static List<Praznik> ZakonskiPraznici(int godina)
    {
        var praznici = new List<Praznik>();

        // Prvo svi dani sa poznatim datumom. Pomeranja se računaju tek nad potpunom listom:
        // ako se računaju usput, „prvi naredni radni dan" ispadne dan koji je i sam praznik,
        // a još nije dodat (npr. 16. februar, dok se obrađuje 15.).
        foreach (var (mesec, dan, naziv) in DrzavniPraznici)
            praznici.Add(new Praznik { Datum = new DateTime(godina, mesec, dan), Naziv = naziv });

        foreach (var (mesec, dan, naziv) in VerskiNepokretni)
            praznici.Add(new Praznik { Datum = new DateTime(godina, mesec, dan), Naziv = naziv });

        var uskrs = PravoslavniUskrs(godina);
        praznici.Add(new Praznik { Datum = uskrs.AddDays(-2), Naziv = "Veliki petak" });
        praznici.Add(new Praznik { Datum = uskrs.AddDays(-1), Naziv = "Velika subota" });
        praznici.Add(new Praznik { Datum = uskrs, Naziv = "Uskrs" });
        praznici.Add(new Praznik { Datum = uskrs.AddDays(1), Naziv = "Uskrsni ponedeljak" });

        // Zakon: ako DRŽAVNI praznik padne u nedelju, ne radi se prvog narednog radnog dana.
        // Za verske praznike to pravilo ne važi.
        var pomereni = new List<Praznik>();
        foreach (var (mesec, dan, naziv) in DrzavniPraznici)
        {
            var datum = new DateTime(godina, mesec, dan);
            if (datum.DayOfWeek != DayOfWeek.Sunday) continue;

            var slobodan = SlediciRadniDan(datum, [.. praznici, .. pomereni]);
            pomereni.Add(new Praznik { Datum = slobodan, Naziv = $"{naziv} — neradni dan" });
        }
        praznici.AddRange(pomereni);

        return praznici
            .GroupBy(p => p.Datum.Date)
            .Select(g => g.First())
            .OrderBy(p => p.Datum)
            .ToList();
    }

    private static DateTime SlediciRadniDan(DateTime od, IReadOnlyList<Praznik> vecDodati)
    {
        var datum = od.AddDays(1);
        while (datum.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
               || vecDodati.Any(p => p.Datum.Date == datum.Date))
        {
            datum = datum.AddDays(1);
        }
        return datum;
    }

    /// <summary>
    /// Datum pravoslavnog Uskrsa u gregorijanskom kalendaru.
    ///
    /// Računa se julijanskim računom (Meeus), pa se dodaje 13 dana koliko iznosi razlika
    /// između kalendara od 1900. do 2099. Van tog opsega razlika je drugačija, pa se metoda
    /// ne sme koristiti bez ispravke.
    /// </summary>
    public static DateTime PravoslavniUskrs(int godina)
    {
        if (godina is < 1900 or > 2099)
            throw new ArgumentOutOfRangeException(nameof(godina), "Podržane su godine od 1900. do 2099.");

        int a = godina % 4;
        int b = godina % 7;
        int c = godina % 19;
        int d = (19 * c + 15) % 30;
        int e = (2 * a + 4 * b - d + 34) % 7;

        int mesec = (d + e + 114) / 31;
        int dan = ((d + e + 114) % 31) + 1;

        return new DateTime(godina, mesec, dan).AddDays(13);
    }

    // ── Kalendar u bazi ──────────────────────────────────────────────

    /// <summary>
    /// Popunjava zakonske praznike za godinu ako još nisu upisani. Ručno unete dane ne dira,
    /// niti prepisuje izmene koje je korisnik napravio nad zakonskim danima.
    /// </summary>
    public int ObezbediGodinu(int godina)
    {
        var pocetak = new DateTime(godina, 1, 1);
        var kraj = new DateTime(godina, 12, 31);

        var postojeci = _db.Praznici
            .Where(p => p.Datum >= pocetak && p.Datum <= kraj)
            .Select(p => p.Datum)
            .ToHashSet();

        var novi = ZakonskiPraznici(godina)
            .Where(p => !postojeci.Contains(p.Datum))
            .ToList();

        if (novi.Count > 0)
        {
            _db.Praznici.AddRange(novi);
            _db.SaveChanges();
        }

        return novi.Count;
    }

    public List<Praznik> Praznici(int godina, int mesec)
    {
        var pocetak = new DateTime(godina, mesec, 1);
        var kraj = pocetak.AddMonths(1).AddDays(-1);

        return _db.Praznici
            .Where(p => p.Datum >= pocetak && p.Datum <= kraj)
            .OrderBy(p => p.Datum)
            .ToList();
    }

    // ── Fond sati ────────────────────────────────────────────────────

    /// <summary>
    /// Broj radnih dana u mesecu: svi dani osim subote, nedelje i neradnih dana iz kalendara.
    /// Praznik koji padne u vikend se ne oduzima dvaput.
    /// </summary>
    public int RadniDani(int godina, int mesec)
    {
        var neradni = Praznici(godina, mesec)
            .Where(p => p.Neradni)
            .Select(p => p.Datum.Date)
            .ToHashSet();

        int dana = DateTime.DaysInMonth(godina, mesec);
        int radnih = 0;

        for (int dan = 1; dan <= dana; dan++)
        {
            var datum = new DateTime(godina, mesec, dan);
            if (datum.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (neradni.Contains(datum)) continue;
            radnih++;
        }

        return radnih;
    }

    /// <summary>Mesečni fond sati — radni dani × puno radno vreme.</summary>
    public int FondSati(int godina, int mesec, int satiPoDanu = SatiPoRadnomDanu)
        => RadniDani(godina, mesec) * satiPoDanu;
}
