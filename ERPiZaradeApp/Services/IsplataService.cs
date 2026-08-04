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
/// Ovde stoji <b>jedino</b> mesto koje zna šta znači „zapisi ove isplate". Pravilo je
/// jednostavno ali se lako razilazi po upitima ako se ponovi: prva isplata meseca obuhvata
/// i zapise bez <see cref="IPripadaIsplati.IsplataId"/>, jer su takvi svi zatečeni i svi koje
/// naprave ekrani koji za isplate ne znaju (doprinosi, porezi). Zbog toga program radi isto
/// kao pre Faze 2.2 sve dok korisnik ne napravi drugu isplatu.
/// </summary>
public class IsplataService
{
    private readonly PlataDbContext _db;

    public IsplataService(PlataDbContext db) => _db = db;

    /// <summary>
    /// Zapisi koji pripadaju datoj isplati — obračuni, radni sati ili arhivirane verzije,
    /// svejedno koji, jer za sve važi isto pravilo. Kad je <paramref name="isplata"/>
    /// <c>null</c>, obuhvat je ceo period; tako se ponašaju svi pozivi koji isplatu ne zadaju.
    /// </summary>
    public static IQueryable<T> Obuhvat<T>(
        IQueryable<T> upit, int godina, int mesec, Isplata? isplata)
        where T : class, IPripadaIsplati
    {
        var uPeriodu = upit.Where(o => o.Godina == godina && o.Mesec == mesec);

        if (isplata == null) return uPeriodu;

        int id = isplata.IsplataId;

        return isplata.JePrva
            ? uPeriodu.Where(o => o.IsplataId == null || o.IsplataId == id)
            : uPeriodu.Where(o => o.IsplataId == id);
    }

    /// <summary>
    /// Isplate perioda, po rednom broju. Kad je <paramref name="rod"/> zadat, vraća samo
    /// isplate tog roda — tako ekrani zarade ne nude isplate naknada i obrnuto.
    /// </summary>
    public IReadOnlyList<Isplata> Isplate(int godina, int mesec, RodIsplate? rod = null)
        => _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec)
            .Where(i => rod == null || i.Rod == rod)
            .OrderBy(i => i.RedniBroj)
            .ToList();

    /// <summary>
    /// Prva isplata <b>zarade</b> u periodu; pravi je ako je nema. Poziva se sa svakog ekrana
    /// koji radi nad periodom, pa se isplata pojavljuje sama za sve mesece koji su postojali
    /// ranije.
    ///
    /// Traži se izričito rod <see cref="RodIsplate.Zarada"/> zato što ova isplata nosi i sve
    /// zapise bez <c>IsplataId</c> (vidi <see cref="Isplata.JePrva"/>), a oni su uvek zarade.
    /// Isplata naknada se ovom metodom <b>ne pravi</b>: njen datum plaćanja deli prijavu od
    /// prijave i program ga ne može pogoditi.
    /// </summary>
    public Isplata Obezbedi(int godina, int mesec)
    {
        var prva = _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec && i.Rod == RodIsplate.Zarada)
            .OrderBy(i => i.RedniBroj)
            .FirstOrDefault();

        if (prva != null) return prva;

        // Broj 1 pripada zaradi jer Dodaj i DodajNaknadu pozivaju ovu metodu pre nego što
        // upišu bilo šta. Sledeći slobodan broj se ipak traži, da upis mimo servisa ne bi
        // oborio jedinstveni indeks (Godina, Mesec, RedniBroj).
        int zauzet = _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec)
            .Max(i => (int?)i.RedniBroj) ?? 0;

        prva = new Isplata
        {
            Godina = godina,
            Mesec = mesec,
            RedniBroj = zauzet + 1,
            Rod = RodIsplate.Zarada,
            Vrsta = VrstaIsplate.KonacnaZarada,
            DatumIsplate = PoslednjiDanMeseca(godina, mesec)
        };

        _db.Isplate.Add(prva);
        _db.SaveChanges();
        return prva;
    }

    /// <summary>
    /// Dodaje narednu isplatu <b>zarade</b> u mesecu. Redni broj se dodeljuje sam — on je
    /// istovremeno veza ka PPP-PD prijavi, pa se ne prepušta unosu.
    /// </summary>
    public RezultatIsplate Dodaj(int godina, int mesec, VrstaIsplate vrsta, string opis, DateTime datumIsplate)
    {
        if (godina <= 0 || mesec is < 1 or > 12)
            return new RezultatIsplate { Poruka = "Period nije ispravan." };

        // Prva isplata mora postojati pre druge — inače bi druga dobila redni broj 2 nad
        // periodom u kom prvog broja nema, pa bi zatečeni obračuni ostali bez svoje isplate.
        Obezbedi(godina, mesec);

        // Obustave se skidaju na konačnoj zaradi. Dve konačne zarade u istom mesecu značile
        // bi da se ista rata kredita skine dvaput, pa se druga ne dozvoljava. Provera gleda
        // samo rod zarade: isplata naknada vrstu ne koristi i ne sme da je blokira.
        if (vrsta == VrstaIsplate.KonacnaZarada
            && _db.Isplate.Any(i => i.Godina == godina && i.Mesec == mesec
                                    && i.Rod == RodIsplate.Zarada
                                    && i.Vrsta == VrstaIsplate.KonacnaZarada))
        {
            return new RezultatIsplate
            {
                Poruka = "Mesec već ima konačnu zaradu. Druga isplata je akontacija, bonus, " +
                         "13. plata ili „ostalo“ — na njima se obustave ne skidaju, da rata " +
                         "kredita ne bi bila naplaćena dvaput."
            };
        }

        return Upisi(godina, mesec, RodIsplate.Zarada, vrsta, opis, datumIsplate);
    }

    /// <summary>
    /// Dodaje isplatu <b>naknada po ugovorima van radnog odnosa</b>.
    ///
    /// Ovo je zasebna isplata, a ne vrsta isplate zarade, zato što joj je obračunski period
    /// drugačije određen: član 11 Pravilnika za zaradu traži mesec <i>za koji</i> se isplaćuje,
    /// a honorar takvog meseca nema — njegov period je mesec isplate. Zato
    /// <paramref name="godina"/> i <paramref name="mesec"/> ovde znače <b>mesec isplate</b> i
    /// izvode se iz <paramref name="datumIsplate"/> ako se razilaze.
    ///
    /// Mesec ih sme imati koliko treba: svaki datum isplate je svoja prijava, jer prijava nosi
    /// jedno polje 1.4. Ograničenje „jedna konačna zarada mesečno" se na njih ne odnosi —
    /// obustave one ne nose nikada.
    /// </summary>
    public RezultatIsplate DodajNaknadu(int godina, int mesec, string opis, DateTime datumIsplate)
    {
        if (godina <= 0 || mesec is < 1 or > 12)
            return new RezultatIsplate { Poruka = "Period nije ispravan." };

        if (datumIsplate == default)
        {
            return new RezultatIsplate
            {
                Poruka = "Isplata naknada mora imati datum isplate — on je datum plaćanja na " +
                         "PPP-PD prijavi (polje 1.4) i deli jednu prijavu od druge."
            };
        }

        // Period naknade JESTE mesec isplate, pa se ne prepušta izboru na ekranu: pogrešan
        // period je prijava sa pogrešnim poljem 1.2, a to se vidi tek kad je odbijena.
        if (datumIsplate.Year != godina || datumIsplate.Month != mesec)
        {
            godina = datumIsplate.Year;
            mesec = datumIsplate.Month;
        }

        // Broj 1 ostaje zaradi; vidi Obezbedi i Isplata.JePrva.
        Obezbedi(godina, mesec);

        return Upisi(godina, mesec, RodIsplate.VanRadnogOdnosa, VrstaIsplate.Ostalo, opis, datumIsplate);
    }

    /// <summary>Upis isplate; zajednički za oba roda, da se dodela rednog broja piše jednom.</summary>
    private RezultatIsplate Upisi(
        int godina, int mesec, RodIsplate rod, VrstaIsplate vrsta, string opis, DateTime datumIsplate)
    {
        int sledeci = _db.Isplate
            .Where(i => i.Godina == godina && i.Mesec == mesec)
            .Max(i => (int?)i.RedniBroj) ?? 0;

        var isplata = new Isplata
        {
            Godina = godina,
            Mesec = mesec,
            RedniBroj = sledeci + 1,
            Rod = rod,
            Vrsta = vrsta,
            Opis = Skrati(opis ?? "", 80),
            DatumIsplate = datumIsplate == default ? PoslednjiDanMeseca(godina, mesec) : datumIsplate
        };

        _db.Isplate.Add(isplata);
        _db.SaveChanges();

        string sta = rod == RodIsplate.VanRadnogOdnosa
            ? $"naknade po ugovoru ({isplata.DatumIsplate:dd.MM.yyyy})"
            : Isplata.NazivVrste(vrsta);

        AuditService.Zabelezi(_db, godina, mesec, AkcijaObracuna.IsplataDodata,
            $"{isplata.RedniBroj}. isplata — {sta}" +
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

        // Radni sati upisani na ovu isplatu odlaze sa njom (Faza 2.2). Oni nisu dokaz kao
        // obračun, nego unos iz kog se obračun tek pravi — a strani ključ bi ih inače
        // zadržao uz isplatu koje više nema. Traže se po ključu, ne po obuhvatu: redovi bez
        // isplate pripadaju periodu, ne ovom zapisu, i ostaju gde jesu.
        var sati = _db.RadniSati.Where(s => s.IsplataId == isplataId).ToList();
        if (sati.Count > 0) _db.RadniSati.RemoveRange(sati);

        _db.Isplate.Remove(isplata);
        _db.SaveChanges();

        AuditService.Zabelezi(_db, isplata.Godina, isplata.Mesec, AkcijaObracuna.IsplataObrisana,
            $"{isplata.RedniBroj}. isplata — {Isplata.NazivVrste(isplata.Vrsta)}" +
            (sati.Count > 0 ? $"; obrisano i {sati.Count} unosa radnih sati" : ""));

        return new RezultatIsplate
        {
            Uspesno = true,
            Poruka = sati.Count > 0
                ? $"Isplata je obrisana, zajedno sa {sati.Count} unosa radnih sati koji su joj pripadali."
                : "Isplata je obrisana."
        };
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
    /// Vezuje obračune i radne sate bez isplate za prvu isplatu perioda. Ne menja nijedan
    /// iznos ni sat — samo upisuje ono što se do sada podrazumevalo, da bi se u tabeli videlo
    /// kojoj isplati zapis pripada.
    /// </summary>
    /// <returns>Broj povezanih zapisa, obračuna i sati zajedno.</returns>
    public int PoveziZatecene(int godina, int mesec)
    {
        var prva = Obezbedi(godina, mesec);

        // Naknada po ugovoru se ovde ne dira: ona svoju isplatu upisuje izričito i pripada
        // isplati roda VanRadnogOdnosa. Da se neka zatekne bez isplate, vezivanje za prvu
        // isplatu zarade bi je uvuklo u pogrešnu prijavu — pa je bolje da je uhvati provera.
        var obracuni = _db.ObracuniPlata
            .Where(o => o.Godina == godina && o.Mesec == mesec && o.IsplataId == null && o.UgovorId == null)
            .ToList();

        foreach (var o in obracuni) o.IsplataId = prva.IsplataId;

        var sati = _db.RadniSati
            .Where(s => s.Godina == godina && s.Mesec == mesec && s.IsplataId == null)
            .ToList();

        foreach (var s in sati) s.IsplataId = prva.IsplataId;

        int povezano = obracuni.Count + sati.Count;
        if (povezano > 0) _db.SaveChanges();
        return povezano;
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
            var uIsplati = Obuhvat(_db.ObracuniPlata, godina, mesec, isplata)
                .Where(o => !o.Storniran)
                .Select(o => new { o.UgovorId })
                .ToList();

            int broj = uIsplati.Count;

            // Rod isplate određuje obračunski period i oznaku K/A njene prijave. Naknada na
            // isplati zarade dobila bi period meseca ZA KOJI se zarada isplaćuje umesto meseca
            // isplate, a zarada na isplati naknada obrnuto — u oba slučaja prijava sa pogrešnim
            // poljem 1.2, što se vidi tek kad je Poreska uprava odbije.
            int nesvrstanih = isplata.JeVanRadnogOdnosa
                ? uIsplati.Count(o => o.UgovorId == null)
                : uIsplati.Count(o => o.UgovorId != null);

            if (nesvrstanih > 0)
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    Provera = "Pomešani rodovi u istoj isplati",
                    Opis = isplata.JeVanRadnogOdnosa
                        ? $"„{isplata.Naziv}“ je isplata naknada, a nosi {nesvrstanih} obračuna zarade. " +
                          "Zarada i naknada ne mogu u istu prijavu — obračunski period im se razlikuje."
                        : $"„{isplata.Naziv}“ je isplata zarade, a nosi {nesvrstanih} naknada po ugovoru. " +
                          "Prebacite ih na isplatu naknada; njihov obračunski period je mesec isplate, " +
                          "a ne mesec za koji se zarada isplaćuje."
                });
            }

            if (broj == 0)
            {
                // Prvu isplatu zarade pravi Obezbedi sam, čim se otvori bilo koji ekran nad
                // periodom. U mesecu u kom su isplaćene samo naknade ona ostaje prazna, i to
                // nije greška nego tačan opis stanja — pa se i kaže tako.
                bool samoNaknade = isplata is { JePrva: true, Rod: RodIsplate.Zarada }
                                   && isplate.Any(i => i.JeVanRadnogOdnosa);

                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Upozorenje,
                    Provera = samoNaknade ? "Mesec bez isplate zarade" : "Isplata bez obračuna",
                    Opis = samoNaknade
                        ? $"„{isplata.Naziv}“ nema nijedan obračun — u ovom mesecu su isplaćene samo " +
                          "naknade po ugovoru. Za nju se ne formira ni prijava ni nalog."
                        : $"„{isplata.Naziv}“ nema nijedan obračun — za nju se ne formira ni prijava ni nalog."
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
