using System;
using System.Collections.Generic;
using System.Linq;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Ishod radnje nad isplatom.</summary>
public sealed class RezultatIsplate
{
    public bool Uspesno { get; init; }
    public Isplata? Isplata { get; init; }
    public string Poruka { get; init; } = "";
}

/// <summary>
/// Isplate unutar obračunskog meseca (Faza 2.2).
///
/// Ovde stoji <b>jedino</b> mesto koje zna šta znači „obračuni ove isplate". Pravilo je
/// jednostavno ali se lako razilazi po upitima ako se ponovi: prva isplata meseca obuhvata
/// i obračune bez <see cref="ObracunPlate.IsplataId"/>, jer su takvi svi zatečeni i svi koje
/// naprave ekrani koji za isplate ne znaju (radni sati, doprinosi, porezi). Zbog toga
/// program radi isto kao pre Faze 2.2 sve dok korisnik ne napravi drugu isplatu.
/// </summary>
public class IsplataService
{
    private readonly PlataDbContext _db;

    public IsplataService(PlataDbContext db) => _db = db;

    /// <summary>
    /// Obračuni koji pripadaju datoj isplati. Kad je <paramref name="isplata"/> <c>null</c>,
    /// obuhvat je ceo period — tako se ponašaju svi pozivi koji isplatu ne zadaju.
    /// </summary>
    public static IQueryable<ObracunPlate> Obuhvat(
        IQueryable<ObracunPlate> upit, int godina, int mesec, Isplata? isplata)
    {
        var uPeriodu = upit.Where(o => o.Godina == godina && o.Mesec == mesec);

        if (isplata == null) return uPeriodu;

        int id = isplata.IsplataId;

        return isplata.JePrva
            ? uPeriodu.Where(o => o.IsplataId == null || o.IsplataId == id)
            : uPeriodu.Where(o => o.IsplataId == id);
    }

    /// <summary>Isplate perioda, po rednom broju.</summary>
    public IReadOnlyList<Isplata> Isplate(int godina, int mesec)
        => _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec)
            .OrderBy(i => i.RedniBroj)
            .ToList();

    /// <summary>
    /// Prva isplata perioda; pravi je ako je nema. Poziva se sa svakog ekrana koji radi nad
    /// periodom, pa se isplata pojavljuje sama za sve mesece koji su postojali ranije.
    /// </summary>
    public Isplata Obezbedi(int godina, int mesec)
    {
        var prva = _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec)
            .OrderBy(i => i.RedniBroj)
            .FirstOrDefault();

        if (prva != null) return prva;

        prva = new Isplata
        {
            Godina = godina,
            Mesec = mesec,
            RedniBroj = 1,
            Vrsta = VrstaIsplate.KonacnaZarada,
            DatumIsplate = PoslednjiDanMeseca(godina, mesec)
        };

        _db.Isplate.Add(prva);
        _db.SaveChanges();
        return prva;
    }

    /// <summary>
    /// Dodaje narednu isplatu u mesecu. Redni broj se dodeljuje sam — on je istovremeno
    /// veza ka PPP-PD prijavi, pa se ne prepušta unosu.
    /// </summary>
    public RezultatIsplate Dodaj(int godina, int mesec, VrstaIsplate vrsta, string opis, DateTime datumIsplate)
    {
        if (godina <= 0 || mesec is < 1 or > 12)
            return new RezultatIsplate { Poruka = "Period nije ispravan." };

        // Prva isplata mora postojati pre druge — inače bi druga dobila redni broj 2 nad
        // periodom u kom prvog broja nema, pa bi zatečeni obračuni ostali bez svoje isplate.
        Obezbedi(godina, mesec);

        // Obustave se skidaju na konačnoj zaradi. Dve konačne zarade u istom mesecu značile
        // bi da se ista rata kredita skine dvaput, pa se druga ne dozvoljava.
        if (vrsta == VrstaIsplate.KonacnaZarada
            && _db.Isplate.Any(i => i.Godina == godina && i.Mesec == mesec && i.Vrsta == VrstaIsplate.KonacnaZarada))
        {
            return new RezultatIsplate
            {
                Poruka = "Mesec već ima konačnu zaradu. Druga isplata je akontacija, bonus, " +
                         "13. plata ili „ostalo“ — na njima se obustave ne skidaju, da rata " +
                         "kredita ne bi bila naplaćena dvaput."
            };
        }

        int sledeci = _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec)
            .Max(i => (int?)i.RedniBroj) ?? 0;

        var isplata = new Isplata
        {
            Godina = godina,
            Mesec = mesec,
            RedniBroj = sledeci + 1,
            Vrsta = vrsta,
            Opis = Skrati(opis ?? "", 80),
            DatumIsplate = datumIsplate == default ? PoslednjiDanMeseca(godina, mesec) : datumIsplate
        };

        _db.Isplate.Add(isplata);
        _db.SaveChanges();

        AuditService.Zabelezi(_db, godina, mesec, AkcijaObracuna.IsplataDodata,
            $"{isplata.RedniBroj}. isplata — {Isplata.NazivVrste(vrsta)}" +
            (string.IsNullOrWhiteSpace(isplata.Opis) ? "" : $" ({isplata.Opis})"));

        return new RezultatIsplate
        {
            Uspesno = true,
            Isplata = isplata,
            Poruka = $"Dodata {isplata.RedniBroj}. isplata za {isplata.PeriodStr}."
        };
    }

    /// <summary>
    /// Briše isplatu. Dozvoljeno je samo nad <b>poslednjom</b> isplatom meseca i samo dok je
    /// prazna: brisanje isplate iz sredine bi pomerilo redne brojeve onih iza nje, a redni
    /// broj je ono po čemu se prijava vezuje za isplatu — podnete prijave bi ostale uz pogrešnu.
    /// </summary>
    public RezultatIsplate Obrisi(int isplataId)
    {
        var isplata = _db.Isplate.FirstOrDefault(i => i.IsplataId == isplataId);
        if (isplata == null)
            return new RezultatIsplate { Poruka = "Isplata nije pronađena." };

        bool imaKasnijih = _db.Isplate.Any(i =>
            i.Godina == isplata.Godina && i.Mesec == isplata.Mesec && i.RedniBroj > isplata.RedniBroj);

        if (imaKasnijih)
        {
            return new RezultatIsplate
            {
                Poruka = "Briše se samo poslednja isplata u mesecu — redni brojevi vezuju " +
                         "isplate za podnete PPP-PD prijave i ne smeju se pomerati."
            };
        }

        int brojObracuna = Obuhvat(_db.ObracuniPlata, isplata.Godina, isplata.Mesec, isplata).Count();
        if (brojObracuna > 0)
        {
            return new RezultatIsplate
            {
                Poruka = $"Isplata nosi {brojObracuna} obračuna i ne može se obrisati. " +
                         "Prvo obrišite ili prevežite obračune te isplate."
            };
        }

        if (PrijavaZa(isplata) != null)
        {
            return new RezultatIsplate
            {
                Poruka = "Za ovu isplatu postoji PPP-PD prijava. Isplata koja je prijavljena " +
                         "Poreskoj upravi ne briše se iz evidencije."
            };
        }

        _db.Isplate.Remove(isplata);
        _db.SaveChanges();

        AuditService.Zabelezi(_db, isplata.Godina, isplata.Mesec, AkcijaObracuna.IsplataObrisana,
            $"{isplata.RedniBroj}. isplata — {Isplata.NazivVrste(isplata.Vrsta)}");

        return new RezultatIsplate { Uspesno = true, Poruka = "Isplata je obrisana." };
    }

    public RezultatIsplate Sacuvaj(Isplata isplata)
    {
        if (isplata == null) return new RezultatIsplate { Poruka = "Nema isplate za snimanje." };

        isplata.Opis = Skrati(isplata.Opis ?? "", 80);
        _db.SaveChanges();

        return new RezultatIsplate { Uspesno = true, Isplata = isplata, Poruka = "Isplata je sačuvana." };
    }

    /// <summary>
    /// PPP-PD prijava te isplate. Veza je redni broj — prijava ga nosi od Faze 1.1 upravo
    /// zbog ovoga, pa se ne uvodi druga, duplirana veza.
    /// </summary>
    public PppPdPrijava? PrijavaZa(Isplata isplata)
        => _db.PppPdPrijave.FirstOrDefault(p =>
            p.Godina == isplata.Godina && p.Mesec == isplata.Mesec && p.RedniBroj == isplata.RedniBroj);

    /// <summary>
    /// Vezuje obračune bez isplate za prvu isplatu perioda. Ne menja nijedan iznos — samo
    /// upisuje ono što se do sada podrazumevalo, da bi se u tabeli videlo kojoj isplati
    /// obračun pripada.
    /// </summary>
    public int PoveziZatecene(int godina, int mesec)
    {
        var prva = Obezbedi(godina, mesec);

        var bezIsplate = _db.ObracuniPlata
            .Where(o => o.Godina == godina && o.Mesec == mesec && o.IsplataId == null)
            .ToList();

        foreach (var o in bezIsplate) o.IsplataId = prva.IsplataId;

        if (bezIsplate.Count > 0) _db.SaveChanges();
        return bezIsplate.Count;
    }

    /// <summary>
    /// Kontrolne provere nad isplatama meseca. Traže ono što se vidi tek kad novac ne stigne
    /// ili kad prijava bude odbijena.
    /// </summary>
    public IReadOnlyList<NalazProvere> Proveri(int godina, int mesec)
    {
        var nalazi = new List<NalazProvere>();
        var isplate = Isplate(godina, mesec);

        // Dok je isplata jedna, sve radi kao pre Faze 2.2 i nema šta da se proverava.
        if (isplate.Count <= 1) return nalazi;

        foreach (var isplata in isplate)
        {
            int broj = Obuhvat(_db.ObracuniPlata, godina, mesec, isplata).Count(o => !o.Storniran);

            if (broj == 0)
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Upozorenje,
                    Provera = "Isplata bez obračuna",
                    Opis = $"„{isplata.Naziv}“ nema nijedan obračun — za nju se ne formira ni prijava ni nalog."
                });
                continue;
            }

            var prijava = PrijavaZa(isplata);

            if (prijava == null)
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    Provera = "Isplata bez PPP-PD prijave",
                    Opis = $"„{isplata.Naziv}“ nosi {broj} obračuna, a nema svoju prijavu. " +
                           "Svaka isplata se prijavljuje zasebno i dobija svoj BOP."
                });
            }
            else if (string.IsNullOrWhiteSpace(prijava.Bop))
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Upozorenje,
                    Provera = "Prijava isplate nema BOP",
                    Opis = $"Prijava za „{isplata.Naziv}“ još nema BOP, pa se porezi i doprinosi te isplate ne mogu uplatiti."
                });
            }
        }

        // Dva BOP-a ista znače da je jedna uplata pokrila dve prijave — novac bi otišao
        // na pogrešnu deklaraciju i ostao neraspoređen na drugoj.
        var bopovi = isplate
            .Select(PrijavaZa)
            .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Bop))
            .GroupBy(p => p!.Bop, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var g in bopovi)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Isti BOP na više isplata",
                Opis = $"BOP {g.Key} stoji uz {g.Count()} prijave. Svaka prihvaćena prijava dobija svoj BOP."
            });
        }

        return nalazi;
    }

    private static DateTime PoslednjiDanMeseca(int godina, int mesec)
        => new(godina, mesec, DateTime.DaysInMonth(godina, mesec));

    private static string Skrati(string tekst, int maxDuzina)
        => tekst.Length <= maxDuzina ? tekst : tekst[..maxDuzina];
}
