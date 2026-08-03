using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Deo jednog obračuna koji pada na teret RFZO i refundira se poslodavcu.
///
/// Isti broj ide na dva mesta — u obrazac OZ-10 i u nalog za knjiženje — pa se računa
/// <b>jednom</b>, u <see cref="RfzoService.DeoNaTeretFonda"/>. Kad bi se računao na dva
/// mesta, iznos koji se traži od Fonda i iznos na kontu 225 bi se s vremenom razišli, a
/// razliku bi neko morao da namiruje ne znajući odakle je došla.
/// </summary>
public readonly record struct DeoObracunaNaTeretFonda(
    decimal Bruto,
    decimal Porez,
    decimal DoprinosiZaposleni,
    decimal DoprinosiPoslodavca)
{
    /// <summary>Neto naknada — ostatak bruta, da zbir izlazi i posle zaokruživanja.</summary>
    public decimal Neto => Bruto - Porez - DoprinosiZaposleni;

    /// <summary>
    /// Ono što Fond refundira: bruto naknada uvećana za doprinose na teret poslodavca.
    /// To je kolona 19 obrasca OZ-10 i iznos potraživanja na kontu 225.
    /// </summary>
    public decimal ZaRefundaciju => Bruto + DoprinosiPoslodavca;

    public bool Postoji => Bruto > 0;

    public static DeoObracunaNaTeretFonda Nema => new(0m, 0m, 0m, 0m);
}

/// <summary>Jedan mesec u osnovu za obračun naknade — red tabele obrasca OZ-7.</summary>
public sealed class Oz7Red
{
    public required int Godina { get; init; }
    public required int Mesec { get; init; }

    /// <summary>Kolona 2 — ukupan broj časova za koje je ostvarena zarada ili naknada zarade.</summary>
    public int Casovi { get; set; }

    /// <summary>Kolona 3 — iznos bez poreza i doprinosa (neto).</summary>
    public decimal Neto { get; set; }

    /// <summary>Kolona 4 — isti iznos sa obračunatim porezom i doprinosima (bruto).</summary>
    public decimal Bruto { get; set; }

    /// <summary>Kolona 5 — datum poslednje (konačne) isplate za taj mesec.</summary>
    public DateTime? DatumIsplate { get; set; }

    /// <summary>Za mesec bez obračuna se po uputstvu upisuje minimalna zarada — a nju program nema.</summary>
    public bool BezObracuna => Casovi == 0 && Bruto == 0;

    public string PeriodStr => $"{Mesec:D2}/{Godina}";
}

/// <summary>
/// Obrazac <b>OZ-7</b> — potvrda o ostvarenoj zaradi za utvrđivanje osnova za obračun
/// naknade zarade, za jedno bolovanje.
/// </summary>
public sealed class Oz7Obrazac
{
    public required Bolovanje Bolovanje { get; init; }
    public required Radnik Radnik { get; init; }
    public required IReadOnlyList<Oz7Red> Redovi { get; init; }

    public int UkupnoCasova => Redovi.Sum(r => r.Casovi);
    public decimal UkupnoNeto => Redovi.Sum(r => r.Neto);
    public decimal UkupnoBruto => Redovi.Sum(r => r.Bruto);

    /// <summary>Prosečan neto po času — ukupno kolona 3 podeljeno sa ukupno kolona 2.</summary>
    public decimal ProsekNetoPoCasu => UkupnoCasova > 0 ? Math.Round(UkupnoNeto / UkupnoCasova, 4) : 0m;

    /// <summary>Prosečan bruto po času — ukupno kolona 4 podeljeno sa ukupno kolona 2.</summary>
    public decimal ProsekBrutoPoCasu => UkupnoCasova > 0 ? Math.Round(UkupnoBruto / UkupnoCasova, 4) : 0m;

    public int BrojMeseciBezObracuna => Redovi.Count(r => r.BezObracuna);
}

/// <summary>Jedan red spiska OZ-10 — jedno bolovanje jednog osiguranika.</summary>
public sealed class Oz10Red
{
    public int RedniBroj { get; set; }

    public required Bolovanje Bolovanje { get; init; }
    public required Radnik Radnik { get; init; }

    /// <summary>Kolona 2 — „М" ili „Ж"; izvodi se iz JMBG-a.</summary>
    public string Pol { get; init; } = "";

    /// <summary>Kolona 3 — „да" za prvu isplatu iz sredstava Fonda, inače crtica.</summary>
    public string PrvaIsplataStr => Bolovanje.PrvaIsplata ? "да" : "-";

    public DateTime DatumOd => Bolovanje.DatumOd;
    public DateTime DatumDo => Bolovanje.DatumDo;

    public OsnovSprecenosti Osnov => Bolovanje.Osnov;

    /// <summary>Broj dana; upisuje se u kolonu 6–13 koja odgovara osnovu.</summary>
    public int BrojDana => Bolovanje.BrojDana;

    /// <summary>Kolona 14 — bruto naknada; jednaka je zbiru kolona 15, 17 i 18.</summary>
    public decimal BrutoNaknada { get; set; }

    /// <summary>Kolona 15 — doprinosi iz naknade (na teret osiguranika).</summary>
    public decimal DoprinosiIzNaknade { get; set; }

    /// <summary>Kolona 16 — doprinosi na naknadu (na teret isplatioca).</summary>
    public decimal DoprinosiNaNaknadu { get; set; }

    /// <summary>Kolona 17 — porez.</summary>
    public decimal Porez { get; set; }

    /// <summary>
    /// Kolona 18 — neto naknada. Računa se kao ostatak bruta, da bi kontrola obrasca
    /// (14 = 15 + 17 + 18) izlazila i posle zaokruživanja.
    /// </summary>
    public decimal NetoNaknada => BrutoNaknada - DoprinosiIzNaknade - Porez;

    /// <summary>Kolona 19 — za isplatu, zbir kolona 15, 16, 17 i 18; to Fond refundira.</summary>
    public decimal ZaIsplatu => BrutoNaknada + DoprinosiNaNaknadu;

    /// <summary>Broj dana u koloni datog osnova; nula u ostalima.</summary>
    public int DaniZa(OsnovSprecenosti osnov) => Osnov == osnov ? BrojDana : 0;
}

/// <summary>Spisak OZ-10 za jedan obračunski period, sa kontrolama.</summary>
public sealed class Oz10Spisak
{
    public required int Godina { get; init; }
    public required int Mesec { get; init; }
    public IReadOnlyList<Oz10Red> Redovi { get; init; } = [];
    public IReadOnlyList<NalazProvere> Nalazi { get; init; } = [];

    public decimal UkupnoBruto => Redovi.Sum(r => r.BrutoNaknada);
    public decimal UkupnoDoprinosiIz => Redovi.Sum(r => r.DoprinosiIzNaknade);
    public decimal UkupnoDoprinosiNa => Redovi.Sum(r => r.DoprinosiNaNaknadu);
    public decimal UkupnoPorez => Redovi.Sum(r => r.Porez);
    public decimal UkupnoNeto => Redovi.Sum(r => r.NetoNaknada);
    public decimal UkupnoZaIsplatu => Redovi.Sum(r => r.ZaIsplatu);

    public int BrojGresaka => Nalazi.Count(n => n.Tezina == TezinaNalaza.Greska);

    public bool SmeSeIzvesti => Redovi.Count > 0 && BrojGresaka == 0;
}

/// <summary>
/// Obrasci za refundaciju naknade zarade iz sredstava obaveznog zdravstvenog osiguranja
/// (Faza 2.6) — <b>OZ-7</b> i <b>OZ-10</b>.
///
/// Oba obrasca su <b>izvedena</b> iz onoga što u bazi već postoji, isto kao nalog za
/// knjiženje: OZ-7 iz obračuna dvanaest meseci pre sprečenosti, OZ-10 iz stavki obračuna
/// meseca u kome je naknada isplaćena. Nijedan iznos se ovde ne unosi rukom — kad bi se
/// unosio, poslodavac bi RFZO-u prijavio jedno, a Poreskoj upravi kroz PPP-PD drugo.
///
/// Koje su naknade na teret Fonda kaže <see cref="VrstaPrimanja.NaTeretFonda"/>, a ne kod:
/// podrazumevano je označeno samo „bolovanje preko 30 dana", a šta još filijala refundira
/// zavisi od slučaja.
/// </summary>
public class RfzoService
{
    private readonly PlataDbContext _db;

    public RfzoService(PlataDbContext db) => _db = db;

    // ── Evidencija bolovanja ─────────────────────────────────────────────

    public IReadOnlyList<Bolovanje> Bolovanja(int godina, int mesec) =>
        _db.Bolovanja
            .Where(b => b.Godina == godina && b.Mesec == mesec)
            .OrderBy(b => b.BrojRadnika).ThenBy(b => b.DatumOd)
            .ToList();

    /// <summary>Karton radnika iz traženog perioda; ako ga tamo nema, poslednji zatečeni.</summary>
    private Radnik? Karton(int brojRadnika, int godina, int mesec) =>
        _db.Radnici.AsNoTracking().FirstOrDefault(r => r.BrojRadnika == brojRadnika && r.Godina == godina && r.Mesec == mesec)
        ?? _db.Radnici.AsNoTracking()
            .Where(r => r.BrojRadnika == brojRadnika)
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .FirstOrDefault();

    // ── OZ-10: spisak obračunatih – isplaćenih naknada zarada ────────────

    /// <summary>
    /// Sastavlja spisak za period u kome je naknada isplaćena.
    ///
    /// Iznosi se uzimaju iz <b>stavki</b> obračuna, a ne iz bruta: bruto obračuna nosi i
    /// zaradu za odrađene dane, a Fond refundira samo naknadu. Porez i doprinosi se dele
    /// srazmerno udelu naknade u ukupnom bruto iznosu — obračun ih ne vodi po stavkama, a
    /// srazmerna podela je jedina koja i za pun mesec bolovanja i za mešovit mesec daje zbir
    /// jednak onome što je prijavljeno kroz PPP-PD.
    /// </summary>
    public Oz10Spisak Pripremi(int godina, int mesec)
    {
        var nalazi = new List<NalazProvere>();
        var redovi = new List<Oz10Red>();

        var bolovanja = Bolovanja(godina, mesec);

        if (bolovanja.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Upozorenje,
                Provera = "Nema evidentiranih bolovanja",
                Opis = $"Za period {mesec:D2}/{godina} nije uneto nijedno bolovanje na teret Fonda."
            });

            return new Oz10Spisak { Godina = godina, Mesec = mesec, Nalazi = nalazi };
        }

        // Stornirani obračun nije isplaćen, pa se ni ne refundira.
        // SUM nad decimal kolonom SQLite odbija, pa se sve sabira u memoriji posle ToList().
        var obracuni = _db.ObracuniPlata
            .AsNoTracking()
            .Include(o => o.Radnik)
            .Include(o => o.Stavke).ThenInclude(s => s.VrstaPrimanja)
            .Where(o => o.Godina == godina && o.Mesec == mesec && !o.Storniran && o.UgovorId == null)
            .ToList();

        // Više bolovanja istog radnika u istom mesecu dele isti obračun; iznos se među njima
        // deli srazmerno broju dana, jer obračun ne zna koji je dan po kom osnovu izostao.
        var poRadniku = bolovanja.GroupBy(b => b.BrojRadnika);

        int redniBroj = 1;

        foreach (var grupa in poRadniku.OrderBy(g => g.Key))
        {
            var radnik = Karton(grupa.Key, godina, mesec);

            if (radnik == null)
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    BrojRadnika = grupa.Key,
                    Provera = "Nepoznat radnik",
                    Opis = $"Za broj radnika {grupa.Key} ne postoji karton, pa se red spiska ne može sastaviti."
                });
                continue;
            }

            var naknada = NaknadaNaTeretFonda(obracuni, grupa.Key);

            if (naknada.Bruto <= 0)
            {
                nalazi.Add(Nalaz(TezinaNalaza.Greska, radnik, "Nema obračunate naknade",
                    $"U obračunu za {mesec:D2}/{godina} nema nijedne stavke označene kao naknada na teret Fonda. " +
                    "Proveriti da li su uneti sati bolovanja preko 30 dana i da li je vrsta primanja označena u šifarniku."));
            }

            int ukupnoDana = grupa.Sum(b => b.BrojDana);

            foreach (var bolovanje in grupa.OrderBy(b => b.DatumOd))
            {
                // Srazmera po danima; kad je bolovanje jedno, udeo je ceo iznos bez zaokruživanja.
                decimal udeo = ukupnoDana > 0 && grupa.Count() > 1
                    ? (decimal)bolovanje.BrojDana / ukupnoDana
                    : 1m;

                var red = new Oz10Red
                {
                    RedniBroj = redniBroj++,
                    Bolovanje = bolovanje,
                    Radnik = radnik,
                    Pol = JmbgValidator.Pol(radnik.Jmbg),
                    BrutoNaknada = Math.Round(naknada.Bruto * udeo, 2),
                    Porez = Math.Round(naknada.Porez * udeo, 2),
                    DoprinosiIzNaknade = Math.Round(naknada.DoprinosiZaposleni * udeo, 2),
                    DoprinosiNaNaknadu = Math.Round(naknada.DoprinosiPoslodavca * udeo, 2)
                };

                redovi.Add(red);
                ProveriRed(red, nalazi);
            }

            if (grupa.Count() > 1)
            {
                nalazi.Add(Nalaz(TezinaNalaza.Upozorenje, radnik, "Više bolovanja u istom mesecu",
                    $"Radnik ima {grupa.Count()} evidentirana bolovanja u {mesec:D2}/{godina}, a obračun nosi jedan iznos naknade. " +
                    "Iznos je podeljen srazmerno broju dana — proveriti podelu pre slanja."));
            }
        }

        ProveriFirmu(nalazi);

        return new Oz10Spisak { Godina = godina, Mesec = mesec, Redovi = redovi, Nalazi = nalazi };
    }

    /// <summary>
    /// Deo <b>jednog</b> obračuna koji pada na teret Fonda: bruto iz stavki čija je vrsta
    /// primanja označena sa <see cref="VrstaPrimanja.NaTeretFonda"/>, a porez i doprinosi
    /// srazmerno njegovom udelu u zbiru stavki.
    ///
    /// Zbir stavki, a ne <c>UkupnoBruto</c>: stavke nose i neoporeziva primanja, pa bi udeo
    /// po brutu ispao veći nego što jeste. Isto pravilo po kome nalog za knjiženje uzima
    /// osnovicu troška.
    ///
    /// Obračun bez stavki — zatečen pre Faze 2.1 — nema po čemu da se prepozna naknada na
    /// teret Fonda, pa za njega ovde nema ničega. To je i namera: pre nego što se izmisli
    /// udeo, bolje je da obrazac ostane prazan i da to prijavi kontrolna provera.
    /// </summary>
    public static DeoObracunaNaTeretFonda DeoNaTeretFonda(ObracunPlate o)
    {
        if (o.Stavke.Count == 0) return DeoObracunaNaTeretFonda.Nema;

        decimal naknada = o.Stavke
            .Where(s => s.VrstaPrimanja != null && s.VrstaPrimanja.NaTeretFonda)
            .Sum(s => s.Iznos);

        if (naknada <= 0) return DeoObracunaNaTeretFonda.Nema;

        decimal osnovica = o.Stavke.Sum(s => s.Iznos);
        decimal udeo = osnovica > 0 ? naknada / osnovica : 0m;

        return new DeoObracunaNaTeretFonda(
            naknada,
            Math.Round(o.PorezNaDohodak * udeo, 2),
            Math.Round(o.UkupniDoprinosi * udeo, 2),
            Math.Round(o.UkupniDoprinosiPoslodavca * udeo, 2));
    }

    /// <summary>Zbir po radniku, za red spiska OZ-10.</summary>
    private static DeoObracunaNaTeretFonda NaknadaNaTeretFonda(List<ObracunPlate> obracuni, int brojRadnika)
    {
        decimal bruto = 0m, porez = 0m, doprinosiIz = 0m, doprinosiNa = 0m;

        foreach (var o in obracuni.Where(o => o.Radnik != null && o.Radnik.BrojRadnika == brojRadnika))
        {
            var deo = DeoNaTeretFonda(o);

            bruto += deo.Bruto;
            porez += deo.Porez;
            doprinosiIz += deo.DoprinosiZaposleni;
            doprinosiNa += deo.DoprinosiPoslodavca;
        }

        return new DeoObracunaNaTeretFonda(bruto, porez, doprinosiIz, doprinosiNa);
    }

    // ── OZ-7: potvrda o ostvarenoj zaradi ────────────────────────────────

    /// <summary>
    /// Sastavlja potvrdu za jedno bolovanje: dvanaest meseci koji prethode mesecu u kome je
    /// privremena sprečenost nastupila.
    /// </summary>
    public (Oz7Obrazac? Obrazac, IReadOnlyList<NalazProvere> Nalazi) PripremiOz7(int bolovanjeId)
    {
        var nalazi = new List<NalazProvere>();

        var bolovanje = _db.Bolovanja.AsNoTracking().FirstOrDefault(b => b.BolovanjeId == bolovanjeId);

        if (bolovanje == null)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Bolovanje ne postoji",
                Opis = "Zapis o bolovanju je u međuvremenu obrisan."
            });
            return (null, nalazi);
        }

        var radnik = Karton(bolovanje.BrojRadnika, bolovanje.Godina, bolovanje.Mesec);

        if (radnik == null)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                BrojRadnika = bolovanje.BrojRadnika,
                Provera = "Nepoznat radnik",
                Opis = $"Za broj radnika {bolovanje.BrojRadnika} ne postoji karton."
            });
            return (null, nalazi);
        }

        var pocetak = new DateTime(
            bolovanje.DatumPocetkaSprecenosti.Year, bolovanje.DatumPocetkaSprecenosti.Month, 1);

        var meseci = Enumerable.Range(1, 12)
            .Select(i => pocetak.AddMonths(-i))
            .OrderBy(d => d)
            .ToList();

        int prvaGodina = meseci[0].Year;

        var obracuni = _db.ObracuniPlata
            .AsNoTracking()
            .Include(o => o.Radnik)
            .Include(o => o.Stavke)
            .Include(o => o.Isplata)
            .Where(o => o.Godina >= prvaGodina && !o.Storniran && o.UgovorId == null)
            .ToList()
            .Where(o => o.Radnik != null && o.Radnik.BrojRadnika == bolovanje.BrojRadnika)
            .ToList();

        var redovi = new List<Oz7Red>();

        foreach (var mesec in meseci)
        {
            var uMesecu = obracuni.Where(o => o.Godina == mesec.Year && o.Mesec == mesec.Month).ToList();

            var red = new Oz7Red { Godina = mesec.Year, Mesec = mesec.Month };

            foreach (var o in uMesecu)
            {
                red.Casovi += Casovi(o);

                // Kolona 4 je bruto zarada i naknada zarade. Neoporeziva primanja — prevoz,
                // jubilarna nagrada, otpremnina — po članu 105. Zakona o radu u zaradu ne
                // ulaze, a `UkupnoBruto` ih i ne sadrži.
                red.Bruto += o.UkupnoBruto;

                // Kolona 3 je isti iznos bez poreza i doprinosa. To NIJE neto za isplatu:
                // obustave (rate kredita, samodoprinos) su radnikov trošak, a ne poreski, pa
                // ostvarenu zaradu ne umanjuju.
                red.Neto += o.UkupnoBruto - o.PorezNaDohodak - o.UkupniDoprinosi;
            }

            // Datum poslednje isplate tog meseca — tako to obrazac i traži („konačna isplata").
            // Mesec bez ijedne isplate ostaje prazan; DateTime.MinValue u koloni datuma bi se
            // odštampao kao 01.01.0001. i prošao nezapaženo.
            var datumi = uMesecu.Where(o => o.Isplata != null).Select(o => o.Isplata!.DatumIsplate).ToList();
            red.DatumIsplate = datumi.Count > 0 ? datumi.Max() : null;

            redovi.Add(red);
        }

        var obrazac = new Oz7Obrazac { Bolovanje = bolovanje, Radnik = radnik, Redovi = redovi };

        ProveriOz7(obrazac, nalazi);

        return (obrazac, nalazi);
    }

    /// <summary>
    /// Ukupan broj časova za koje je zarada ili naknada ostvarena. Uzima se iz stavki, jer
    /// one nose i sate koje <c>ObracunPlate</c> drži samo u legacy kolonama (bolovanje preko
    /// 30 dana, porodiljsko, plaćeno odsustvo po zakonu). Obračun bez stavki — nastao pre
    /// prevoda iz Faze 2.1 — pada na zbir kolona, dopunjen tim legacy satima.
    /// </summary>
    private static int Casovi(ObracunPlate o)
    {
        if (o.Stavke.Count > 0) return o.Stavke.Sum(s => s.Sati);

        return o.UkupnoSati
             + (int)Math.Round(o.BolovanjePreko60SatiLegacy)
             + (int)Math.Round(o.PorodiljskoOdsustvoSatiLegacy)
             + (int)Math.Round(o.PlacenoZakonskiSatiLegacy)
             + (int)Math.Round(o.Bolovanje100SatiLegacy)
             + (int)Math.Round(o.NedeljaSati);
    }

    // ── Kontrolne provere ────────────────────────────────────────────────

    private static NalazProvere Nalaz(TezinaNalaza tezina, Radnik radnik, string provera, string opis) => new()
    {
        Tezina = tezina,
        BrojRadnika = radnik.BrojRadnika,
        Radnik = radnik.ImeIPrezime,
        Provera = provera,
        Opis = opis
    };

    private static void ProveriRed(Oz10Red red, List<NalazProvere> nalazi)
    {
        var radnik = red.Radnik;
        var bolovanje = red.Bolovanje;

        if (bolovanje.DatumDo < bolovanje.DatumOd)
            nalazi.Add(Nalaz(TezinaNalaza.Greska, radnik, "Obrnut period",
                $"Datum „do“ ({bolovanje.DatumDo:dd.MM.yyyy}) je pre datuma „od“ ({bolovanje.DatumOd:dd.MM.yyyy})."));

        if (bolovanje.DatumOd < bolovanje.DatumPocetkaSprecenosti)
        {
            nalazi.Add(Nalaz(TezinaNalaza.Greska, radnik, "Period pre početka sprečenosti",
                $"Naknada se traži od {bolovanje.DatumOd:dd.MM.yyyy}, a sprečenost počinje {bolovanje.DatumPocetkaSprecenosti:dd.MM.yyyy}."));
        }
        else
        {
            // Prag nije svuda 31. dan: kod povrede na radu, profesionalne bolesti i davanja
            // tkiva Fond plaća od prvog dana, pa bi opšte upozorenje tamo bilo pogrešno.
            int? prviDan = Bolovanje.PrviDanNaTeretFonda(bolovanje.Osnov);

            if (prviDan is > 1 && bolovanje.DanSprecenostiNaPocetku < prviDan)
                nalazi.Add(Nalaz(TezinaNalaza.Upozorenje, radnik, $"Prvih {prviDan - 1} dana nosi poslodavac",
                    $"Period počinje {bolovanje.DanSprecenostiNaPocetku}. danom sprečenosti, a naknada za „{bolovanje.OsnovNaziv}“ " +
                    $"ide na teret Fonda od {prviDan}. dana (od {bolovanje.DatumPocetkaSprecenosti.AddDays(prviDan.Value - 1):dd.MM.yyyy}) — " +
                    "proveriti da li je period ispravno unet."));
        }

        if (string.IsNullOrWhiteSpace(red.Pol))
            nalazi.Add(Nalaz(TezinaNalaza.Greska, radnik, "Pol se ne može odrediti",
                "Obrazac traži pol osiguranika, a on se izvodi iz JMBG-a — koji nije unet ili nema 13 cifara."));
    }

    private static void ProveriOz7(Oz7Obrazac obrazac, List<NalazProvere> nalazi)
    {
        var radnik = obrazac.Radnik;

        if (string.IsNullOrWhiteSpace(radnik.Lbo))
            nalazi.Add(Nalaz(TezinaNalaza.Greska, radnik, "Nedostaje LBO",
                "Obrazac se podnosi sa ličnim brojem osiguranika sa zdravstvene kartice; unosi se u karton radnika."));

        if (obrazac.UkupnoCasova == 0)
            nalazi.Add(Nalaz(TezinaNalaza.Greska, radnik, "Nema osnova za obračun",
                "U dvanaest meseci pre sprečenosti nema nijednog obračuna, pa se prosek po času ne može utvrditi."));
        else if (obrazac.BrojMeseciBezObracuna > 0)
            nalazi.Add(Nalaz(TezinaNalaza.Upozorenje, radnik, "Meseci bez obračuna",
                $"{obrazac.BrojMeseciBezObracuna} od 12 meseci nema obračun. Po uputstvu uz obrazac se za mesece " +
                "u kojima radnik nije bio u radnom odnosu upisuje minimalna zarada za taj mesec — taj podatak " +
                "program nema, pa se ti redovi popunjavaju rukom."));
    }

    private void ProveriFirmu(List<NalazProvere> nalazi)
    {
        var firma = _db.Firme.AsNoTracking().FirstOrDefault();

        if (firma == null)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nema kartona firme",
                Opis = "Zaglavlje obrasca se popunjava iz kartona firme, a on nije unet."
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(firma.PosebanRacun))
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nema posebnog računa",
                Opis = "Fond refundaciju uplaćuje na poseban tekući račun poslodavca; unosi se u karton firme."
            });

        if (string.IsNullOrWhiteSpace(firma.SifraDelatnosti))
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Upozorenje,
                Provera = "Nema šifre delatnosti",
                Opis = "Zaglavlje obrasca OZ-10 traži šifru delatnosti poslodavca."
            });
    }
}
