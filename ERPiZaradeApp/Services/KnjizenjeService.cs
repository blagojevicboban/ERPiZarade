using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Jedna stavka naloga za knjiženje. Odgovara <c>StavkaNaloga</c> u ERPiFinansije, pa se
/// izvoz svodi na prepisivanje polja — bez računanja pri prenosu.
/// </summary>
public sealed class StavkaKnjizenja
{
    public int RedniBroj { get; set; }

    public string Konto { get; init; } = "";

    public string Opis { get; init; } = "";

    public decimal Duguje { get; init; }

    public decimal Potrazuje { get; init; }

    /// <summary>
    /// Šifra mesta troška iz ERPiFinansije. Prazno je za obaveze — one se ne dele po
    /// mestima troška, jer je obaveza prema radniku jedna bez obzira gde je radio.
    /// </summary>
    public string MestoTroska { get; init; } = "";

    /// <summary>Ključ iz šifarnika konta ili šifra vrste primanja — samo za trag u proveri.</summary>
    public string Izvor { get; init; } = "";
}

/// <summary>
/// Nalog za knjiženje jedne isplate — temeljnica koja ulazi u glavnu knjigu.
/// </summary>
public sealed class NalogZaKnjizenje
{
    public int Godina { get; init; }
    public int Mesec { get; init; }
    public DateTime Datum { get; init; }
    public string Opis { get; init; } = "";

    /// <summary>Redni broj isplate u mesecu; 1 kad je isplata jedna ili nije zadata.</summary>
    public int RedniBrojIsplate { get; init; } = 1;

    public IReadOnlyList<StavkaKnjizenja> Stavke { get; init; } = [];
    public IReadOnlyList<NalazProvere> Nalazi { get; init; } = [];

    /// <summary>Broj obračuna iz kojih je nalog nastao — kontrola da nije ispao neko.</summary>
    public int BrojObracuna { get; init; }

    public decimal UkupnoDuguje => Stavke.Sum(s => s.Duguje);
    public decimal UkupnoPotrazuje => Stavke.Sum(s => s.Potrazuje);
    public decimal Razlika => UkupnoDuguje - UkupnoPotrazuje;

    /// <summary>Nalog koji nije u ravnoteži glavna knjiga ne prima.</summary>
    public bool JeUravnotezen => Math.Abs(Razlika) < 0.01m;

    public int BrojGresaka => Nalazi.Count(n => n.Tezina == TezinaNalaza.Greska);

    public bool SmeSeIzvesti => Stavke.Count > 0 && JeUravnotezen && BrojGresaka == 0;
}

/// <summary>
/// Formiranje naloga za knjiženje obračuna zarada (Faza 3.1).
///
/// Nalog je <b>izveden</b> iz obračuna, ne novi podatak: trošak se uzima sa konta upisanog
/// uz vrstu primanja odnosno vrstu ugovora, a protivstava sa konta iz šifarnika
/// <see cref="KontoKnjizenja"/>. Nigde se ne računa iznos koji obračun već nosi — kad bi se
/// računao, temeljnica bi umela da se razlikuje od naloga za prenos i od PPP-PD prijave, a
/// upravo to knjigovođa mora da uskladi.
///
/// Zbog toga svaki obračun ide i kroz <b>kontrolu sastava</b>: bruto umanjen za porez,
/// doprinose i obustave mora dati neto koji je isplaćen. Ako ne da, nalog se ne izvozi —
/// razlika bi se u glavnoj knjizi pojavila kao neuravnotežen nalog, a tu se više ne vidi
/// koji je radnik uzrok.
/// </summary>
public class KnjizenjeService
{
    private readonly PlataDbContext _db;

    public KnjizenjeService(PlataDbContext db) => _db = db;

    /// <summary>Konta iz šifarnika, po ključu.</summary>
    private Dictionary<string, KontoKnjizenja> UcitajKonta()
    {
        try
        {
            return _db.KontaKnjizenja.AsNoTracking().ToDictionary(k => k.Kljuc, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            // Baza starije verzije nema tabelu — nalazi to prijavljuju, a nalog ostaje prazan.
            Serilog.Log.Warning(ex, "Šifarnik konta za knjiženje se ne može učitati");
            return [];
        }
    }

    /// <summary>
    /// Formira nalog za knjiženje za zadatu isplatu.
    /// </summary>
    /// <param name="isplata">
    /// Isplata za koju se nalog pravi. <c>null</c> znači ceo period — tako se ponaša svaki
    /// poziv koji za isplate ne zna. Kad mesec ima akontaciju i konačnu zaradu, nalog mora
    /// obuhvatiti tačno jednu: druga isplata je zaseban dokument sa svojim datumom.
    /// </param>
    /// <param name="datum">Datum naloga; podrazumevano datum isplate.</param>
    public NalogZaKnjizenje Pripremi(int godina, int mesec, Isplata? isplata, DateTime datum)
    {
        var nalazi = new List<NalazProvere>();
        var stavke = new List<StavkaKnjizenja>();
        var konta = UcitajKonta();

        // SUM nad decimal kolonom SQLite odbija, pa se sve sabira u memoriji posle ToList().
        var obracuni = IsplataService
            .Obuhvat(
                _db.ObracuniPlata.AsNoTracking()
                    .Include(o => o.Radnik)
                    .Include(o => o.Stavke).ThenInclude(s => s.VrstaPrimanja)
                    .Include(o => o.Ugovor!).ThenInclude(u => u.VrstaUgovora),
                godina, mesec, isplata)
            .Where(o => !o.Storniran)
            .ToList();

        string opis = OpisNaloga(godina, mesec, isplata);

        if (obracuni.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Prazan period",
                Opis = isplata == null
                    ? $"Za period {mesec:D2}/{godina} ne postoji nijedan obračun koji se knjiži."
                    : $"Isplata „{isplata.Naziv}“ za {mesec:D2}/{godina} ne obuhvata nijedan obračun."
            });

            return new NalogZaKnjizenje
            {
                Godina = godina, Mesec = mesec, Datum = datum, Opis = opis,
                RedniBrojIsplate = isplata?.RedniBroj ?? 1,
                Nalazi = nalazi
            };
        }

        if (konta.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Prazan šifarnik konta",
                Opis = "Šifarnik „Konta za knjiženje“ je prazan, pa se protivstava troška ne zna. " +
                       "Otvorite šifarnik — podrazumevana konta se upisuju pri pokretanju programa."
            });
        }

        var zarade = obracuni.Where(o => o.UgovorId == null).ToList();
        var naknade = obracuni.Where(o => o.UgovorId != null).ToList();

        ProveriSastav(obracuni, nalazi);

        DodajTroskoveZarada(zarade, konta, stavke, nalazi);
        DodajObavezeZarada(zarade, konta, stavke, nalazi);
        DodajTroskoveNaknada(naknade, konta, stavke, nalazi);
        DodajObavezeNaknada(naknade, konta, stavke, nalazi);

        // Duguje pa potražuje, unutar strane po kontu — tako nalog izgleda kao temeljnica
        // koju knjigovođa i inače dobija, i lakše se poredi sa rekapitulacijom.
        var poredane = stavke
            .OrderByDescending(s => s.Duguje > 0)
            .ThenBy(s => s.Konto, StringComparer.Ordinal)
            .ThenBy(s => s.MestoTroska, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < poredane.Count; i++) poredane[i].RedniBroj = i + 1;

        var nalog = new NalogZaKnjizenje
        {
            Godina = godina,
            Mesec = mesec,
            Datum = datum,
            Opis = opis,
            RedniBrojIsplate = isplata?.RedniBroj ?? 1,
            BrojObracuna = obracuni.Count,
            Stavke = poredane,
            Nalazi = nalazi
        };

        if (poredane.Count > 0 && !nalog.JeUravnotezen)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nalog nije u ravnoteži",
                Opis = $"Duguje {nalog.UkupnoDuguje:N2}, potražuje {nalog.UkupnoPotrazuje:N2}, " +
                       $"razlika {nalog.Razlika:N2}. Glavna knjiga takav nalog ne prima."
            });
        }

        return nalog;
    }

    private static string OpisNaloga(int godina, int mesec, Isplata? isplata)
    {
        if (isplata == null || (isplata.JePrva && isplata.Vrsta == VrstaIsplate.KonacnaZarada))
            return $"Obračun zarada {mesec:D2}/{godina}";

        return $"{isplata.NazivKratki} {mesec:D2}/{godina}";
    }

    /// <summary>
    /// Iznos koji obračun stavlja na teret firme, i time strana „duguje" ovog obračuna.
    ///
    /// To je <b>zbir stavki</b>, a ne <c>UkupnoBruto</c>: neoporeziva primanja (prevoz,
    /// jubilarna nagrada) se isplaćuju radniku i jesu trošak, ali u bruto iznos ne ulaze —
    /// po zakonu nisu ni u poreskoj osnovici ni u osnovici doprinosa. Zbir stavki ih nosi,
    /// pa se jedino sa njim nalog uravnoteži. Obračun bez stavki (zatečen pre Faze 2.1)
    /// nema neoporezivih primanja, pa je za njega bruto isto to.
    /// </summary>
    private static decimal OsnovicaTroska(ObracunPlate o)
        => o.Stavke.Count > 0 ? o.Stavke.Sum(s => s.Iznos) : o.UkupnoBruto;

    /// <summary>
    /// Kontrola da se sastav obračuna slaže: trošak umanjen za porez, doprinose i obustave
    /// mora dati isplaćen neto. Bez nje bi se razlika pojavila tek kao neuravnotežen nalog,
    /// gde se više ne vidi koji je radnik uzrok.
    /// </summary>
    private static void ProveriSastav(List<ObracunPlate> obracuni, List<NalazProvere> nalazi)
    {
        foreach (var o in obracuni)
        {
            decimal osnovica = OsnovicaTroska(o);
            decimal izvedeniNeto = osnovica - o.PorezNaDohodak - o.UkupniDoprinosi - o.UkupniOdbici;

            if (Math.Abs(izvedeniNeto - o.NetoIsplata) < 0.01m) continue;

            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                BrojRadnika = o.Radnik?.BrojRadnika,
                Radnik = o.Radnik?.ImeIPrezime ?? "",
                Provera = "Sastav obračuna se ne slaže",
                Opis = $"Primanja {osnovica:N2} umanjena za porez {o.PorezNaDohodak:N2}, doprinose " +
                       $"{o.UkupniDoprinosi:N2} i obustave {o.UkupniOdbici:N2} daju {izvedeniNeto:N2}, " +
                       $"a isplaćeno je {o.NetoIsplata:N2}. Obračun treba prekalkulisati pre knjiženja."
            });
        }
    }

    // ── Trošak zarada ────────────────────────────────────────────────

    /// <summary>
    /// Bruto zarada se knjiži na konto upisan uz <b>vrstu primanja</b>, a deli po mestu
    /// troška radnika. Obračun koji nije razložen na stavke (zatečen pre Faze 2.1) ide ceo
    /// na zbirni konto iz šifarnika, uz upozorenje — knjiženje po vrstama primanja je
    /// upravo ono zbog čega stavke postoje.
    /// </summary>
    private static void DodajTroskoveZarada(
        List<ObracunPlate> zarade,
        Dictionary<string, KontoKnjizenja> konta,
        List<StavkaKnjizenja> stavke,
        List<NalazProvere> nalazi)
    {
        if (zarade.Count == 0) return;

        var troskovi = new Dictionary<(string Konto, string Mt), decimal>();
        var opisi = new Dictionary<(string Konto, string Mt), string>();
        var bezKonta = new HashSet<string>(StringComparer.Ordinal);
        var nerazlozeni = new List<ObracunPlate>();

        string zbirniKonto = Konto(konta, KontaKnjizenjaSeed.TrosakZarade);

        foreach (var o in zarade)
        {
            string mt = o.Radnik?.SifraMestaTroska?.Trim() ?? "";

            if (o.Stavke.Count == 0)
            {
                if (o.UkupnoBruto == 0) continue;

                nerazlozeni.Add(o);
                Dodaj(troskovi, opisi, (zbirniKonto, mt), o.UkupnoBruto, "Troškovi zarada i naknada zarada");
                continue;
            }

            foreach (var s in o.Stavke)
            {
                if (s.Iznos == 0) continue;

                // Naknada koju refundira RFZO nije trošak poslodavca — Kontni okvir je izvodi
                // iz grupe 52 u celosti. Umesto troška nastaje potraživanje od Fonda; vidi
                // DodajRefundaciju.
                if (s.VrstaPrimanja?.NaTeretFonda == true) continue;

                string konto = s.VrstaPrimanja?.Konto?.Trim() ?? "";
                string naziv = s.VrstaPrimanja?.Naziv ?? "Zarada";

                if (konto.Length == 0)
                {
                    // Vrsta primanja bez konta ne sme tiho da padne na zbirni konto — trošak
                    // bi završio na pogrešnom mestu i to bi se otkrilo tek u bilansu.
                    if (s.VrstaPrimanja != null) bezKonta.Add($"{s.VrstaPrimanja.Sifra} — {naziv}");
                    konto = zbirniKonto;
                }

                Dodaj(troskovi, opisi, (konto, mt), s.Iznos, naziv);
            }
        }

        if (bezKonta.Count > 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Vrsta primanja bez konta",
                Opis = "Konto nije upisan uz: " + string.Join(", ", bezKonta.OrderBy(x => x, StringComparer.Ordinal)) +
                       ". Do unosa u šifarnik „Vrste primanja“ trošak bi otišao na zbirni konto."
            });
        }

        if (nerazlozeni.Count > 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Upozorenje,
                Provera = "Obračun nije razložen na stavke",
                Opis = $"{nerazlozeni.Count} obračuna nema stavke, pa im bruto ide ceo na konto " +
                       $"{zbirniKonto}. Razlaganje po vrstama primanja se pokreće u šifarniku " +
                       "„Vrste primanja“ (🔀)."
            });
        }

        UpozoriNaMestaTroska(zarade, nalazi);

        UpisiStavke(troskovi, opisi, stavke, duguje: true, izvor: "VrstaPrimanja.Konto");

        // Doprinosi na teret poslodavca su trošak firme, ne odbitak od zarade, pa idu na
        // svoj konto — i dele se po istom mestu troška kao i zarada na koju su obračunati.
        var doprinosi = new Dictionary<(string, string), decimal>();
        var opisiDoprinosa = new Dictionary<(string, string), string>();
        string kontoDoprinosa = Konto(konta, KontaKnjizenjaSeed.TrosakDoprinosaPoslodavca);

        foreach (var o in zarade)
        {
            // Doprinosi obračunati na refundiranu naknadu takođe nisu trošak — i njih Fond
            // vraća, pa ulaze u potraživanje umesto na 521.
            decimal naTeretFirme = o.UkupniDoprinosiPoslodavca - RfzoService.DeoNaTeretFonda(o).DoprinosiPoslodavca;
            if (naTeretFirme == 0) continue;

            string mt = o.Radnik?.SifraMestaTroska?.Trim() ?? "";
            Dodaj(doprinosi, opisiDoprinosa, (kontoDoprinosa, mt), naTeretFirme,
                "Doprinosi na zarade na teret poslodavca");
        }

        UpisiStavke(doprinosi, opisiDoprinosa, stavke, duguje: true, izvor: KontaKnjizenjaSeed.TrosakDoprinosaPoslodavca);

        ProveriKonta(konta, nalazi, KontaKnjizenjaSeed.TrosakZarade, KontaKnjizenjaSeed.TrosakDoprinosaPoslodavca);
    }

    /// <summary>
    /// Mesto troška se ne izmišlja: radnik bez šifre ulazi u nalog bez podele. Prijavljuje se
    /// tek kad ga <b>neki</b> radnici imaju — dok ga nema niko, firma ga očigledno ne vodi.
    /// </summary>
    private static void UpozoriNaMestaTroska(List<ObracunPlate> obracuni, List<NalazProvere> nalazi)
    {
        var sa = obracuni.Count(o => !string.IsNullOrWhiteSpace(o.Radnik?.SifraMestaTroska));
        if (sa == 0) return;

        var bez = obracuni.Where(o => string.IsNullOrWhiteSpace(o.Radnik?.SifraMestaTroska)).ToList();
        if (bez.Count == 0) return;

        nalazi.Add(new NalazProvere
        {
            Tezina = TezinaNalaza.Upozorenje,
            Provera = "Radnik bez mesta troška",
            Opis = $"{bez.Count} radnika nema šifru mesta troška, pa im trošak ostaje nerasporeden: " +
                   string.Join(", ", bez.Take(5).Select(o => o.Radnik?.ImeIPrezime ?? "?")) +
                   (bez.Count > 5 ? "…" : "") + ". Šifra se unosi u kartonu radnika."
        });
    }

    // ── Obaveze po zaradama ──────────────────────────────────────────

    private static void DodajObavezeZarada(
        List<ObracunPlate> zarade,
        Dictionary<string, KontoKnjizenja> konta,
        List<StavkaKnjizenja> stavke,
        List<NalazProvere> nalazi)
    {
        if (zarade.Count == 0) return;

        decimal netoZarada = 0m, porez = 0m, doprinosiZaposleni = 0m, doprinosiPoslodavca = 0m;
        decimal netoRefundacija = 0m, poreziZaposleniRefundacija = 0m, poreziPoslodavacRefundacija = 0m;
        decimal potrazivanje = 0m;

        foreach (var o in zarade)
        {
            var fond = RfzoService.DeoNaTeretFonda(o);

            porez += o.PorezNaDohodak - fond.Porez;
            doprinosiZaposleni += o.UkupniDoprinosi - fond.DoprinosiZaposleni;
            doprinosiPoslodavca += o.UkupniDoprinosiPoslodavca - fond.DoprinosiPoslodavca;

            poreziZaposleniRefundacija += fond.Porez + fond.DoprinosiZaposleni;
            poreziPoslodavacRefundacija += fond.DoprinosiPoslodavca;
            potrazivanje += fond.ZaRefundaciju;

            // Obustava umanjuje ono što se radniku isplaćuje, pa mora da se skine sa jedne od
            // dve obaveze prema njemu — 450 ili 454. Skida se **prvo sa zarade**, jer to
            // poslodavac plaća iz svojih sredstava; tek kad zarade nema — pun mesec bolovanja —
            // pada na naknadu. Bez tog redosleda bi konto 450 za takav mesec ispao negativan.
            decimal netoPreObustava = o.NetoIsplata + o.UkupniOdbici;
            decimal netoZaradeDeo = netoPreObustava - fond.Neto;
            decimal obustaveNaZaradu = Math.Min(o.UkupniOdbici, Math.Max(0m, netoZaradeDeo));

            netoZarada += netoZaradeDeo - obustaveNaZaradu;
            netoRefundacija += fond.Neto - (o.UkupniOdbici - obustaveNaZaradu);
        }

        // Bez naknade na teret Fonda ovo je tačno ono što je i do sada stajalo na 450:
        // `NetoIsplata`, dakle isti iznos koji ide na nalog za prenos.
        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaNetoZarada,
            netoZarada, "Obaveze za neto zarade");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaPorezZaposleni,
            porez, "Porez na zarade na teret zaposlenog");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaDoprinosiZaposleni,
            doprinosiZaposleni, "Doprinosi na zarade na teret zaposlenog");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaPoreziDoprinosiPoslodavac,
            doprinosiPoslodavca, "Doprinosi na zarade na teret poslodavca");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaObustave,
            zarade.Sum(o => o.KreditObustava + o.OstaliOdbici), "Obustave iz zarade");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaSamodoprinos,
            zarade.Sum(o => o.Samodoprinosi), "Samodoprinos");

        ProveriKonta(konta, nalazi,
            KontaKnjizenjaSeed.ObavezaNetoZarada,
            KontaKnjizenjaSeed.ObavezaPorezZaposleni,
            KontaKnjizenjaSeed.ObavezaDoprinosiZaposleni,
            KontaKnjizenjaSeed.ObavezaPoreziDoprinosiPoslodavac);

        DodajRefundaciju(konta, stavke, nalazi,
            potrazivanje, netoRefundacija, poreziZaposleniRefundacija, poreziPoslodavacRefundacija);
    }

    /// <summary>
    /// Naknada zarade na teret RFZO (Faza 2.6).
    ///
    /// Nije trošak i ne prolazi kroz grupu 52: umesto troška nastaje <b>potraživanje od
    /// Fonda</b> na kontu 225, a obaveze prema radniku i državi idu na 454, 455 i 456 umesto
    /// na 450–453. Potraživanje se zatvara izvodom posebnog računa kad refundacija stigne —
    /// taj korak je u ERPiFinansije, ne ovde.
    ///
    /// Iznos na 225 je isti onaj koji stoji u koloni „за исплату" obrasca OZ-10; oba dolaze
    /// iz <see cref="RfzoService.DeoNaTeretFonda"/>, pa se ne mogu razići.
    /// </summary>
    private static void DodajRefundaciju(
        Dictionary<string, KontoKnjizenja> konta,
        List<StavkaKnjizenja> stavke,
        List<NalazProvere> nalazi,
        decimal potrazivanje,
        decimal neto,
        decimal poreziZaposleni,
        decimal poreziPoslodavac)
    {
        if (potrazivanje == 0m && neto == 0m && poreziZaposleni == 0m && poreziPoslodavac == 0m) return;

        decimal zaokruzeno = Math.Round(potrazivanje, 2, MidpointRounding.AwayFromZero);

        if (zaokruzeno != 0)
        {
            stavke.Add(new StavkaKnjizenja
            {
                Konto = Konto(konta, KontaKnjizenjaSeed.PotrazivanjeRefundacije),
                Opis = "Potraživanja za naknade zarada koje se refundiraju",
                Duguje = zaokruzeno,
                Izvor = KontaKnjizenjaSeed.PotrazivanjeRefundacije
            });
        }

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaNetoRefundacija,
            neto, "Obaveze za neto naknade zarada koje se refundiraju");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaPoreziZaposleniRefundacija,
            poreziZaposleni, "Porez i doprinosi na refundirane naknade — na teret zaposlenog");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaPoreziPoslodavacRefundacija,
            poreziPoslodavac, "Doprinosi na refundirane naknade — na teret poslodavca");

        ProveriKonta(konta, nalazi,
            KontaKnjizenjaSeed.PotrazivanjeRefundacije,
            KontaKnjizenjaSeed.ObavezaNetoRefundacija,
            KontaKnjizenjaSeed.ObavezaPoreziZaposleniRefundacija,
            KontaKnjizenjaSeed.ObavezaPoreziPoslodavacRefundacija);
    }

    // ── Naknade van radnog odnosa ────────────────────────────────────

    private static void DodajTroskoveNaknada(
        List<ObracunPlate> naknade,
        Dictionary<string, KontoKnjizenja> konta,
        List<StavkaKnjizenja> stavke,
        List<NalazProvere> nalazi)
    {
        if (naknade.Count == 0) return;

        var troskovi = new Dictionary<(string, string), decimal>();
        var opisi = new Dictionary<(string, string), string>();
        var bezKonta = new HashSet<string>(StringComparer.Ordinal);

        string zbirniKonto = Konto(konta, KontaKnjizenjaSeed.TrosakNaknade);
        string kontoDoprinosa = Konto(konta, KontaKnjizenjaSeed.TrosakDoprinosaIsplatioca);

        foreach (var o in naknade)
        {
            var vrsta = o.Ugovor?.VrstaUgovora;
            string konto = vrsta?.Konto?.Trim() ?? "";
            string mt = o.Radnik?.SifraMestaTroska?.Trim() ?? "";

            if (konto.Length == 0)
            {
                if (vrsta != null) bezKonta.Add($"{vrsta.Sifra} — {vrsta.Naziv}");
                konto = zbirniKonto;
            }

            if (o.UkupnoBruto != 0)
                Dodaj(troskovi, opisi, (konto, mt), o.UkupnoBruto, vrsta?.Naziv ?? "Naknada po ugovoru");

            if (o.UkupniDoprinosiPoslodavca != 0)
                Dodaj(troskovi, opisi, (kontoDoprinosa, mt), o.UkupniDoprinosiPoslodavca,
                    "Doprinosi na naknade na teret isplatioca");
        }

        if (bezKonta.Count > 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Vrsta ugovora bez konta",
                Opis = "Konto nije upisan uz: " + string.Join(", ", bezKonta.OrderBy(x => x, StringComparer.Ordinal)) +
                       ". Do unosa u šifarnik „Vrste ugovora“ trošak bi otišao na zbirni konto."
            });
        }

        UpisiStavke(troskovi, opisi, stavke, duguje: true, izvor: "VrstaUgovora.Konto");

        ProveriKonta(konta, nalazi, KontaKnjizenjaSeed.TrosakNaknade, KontaKnjizenjaSeed.TrosakDoprinosaIsplatioca);
    }

    private static void DodajObavezeNaknada(
        List<ObracunPlate> naknade,
        Dictionary<string, KontoKnjizenja> konta,
        List<StavkaKnjizenja> stavke,
        List<NalazProvere> nalazi)
    {
        if (naknade.Count == 0) return;

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaNetoNaknada,
            naknade.Sum(o => o.NetoIsplata), "Obaveze prema fizičkim licima po ugovorima");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaPorezNaknada,
            naknade.Sum(o => o.PorezNaDohodak), "Porez na naknade po ugovorima");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaDoprinosiNaknada,
            naknade.Sum(o => o.UkupniDoprinosi), "Doprinosi na naknade na teret primaoca");

        DodajObavezu(stavke, konta, KontaKnjizenjaSeed.ObavezaDoprinosiIsplatioca,
            naknade.Sum(o => o.UkupniDoprinosiPoslodavca), "Doprinosi na naknade na teret isplatioca");

        // Obustave se na naknadama ne skidaju — one postoje samo uz konačnu zaradu. Ako se
        // ipak pojave, iznos bi ispao iz naloga, pa se to javlja.
        decimal odbici = naknade.Sum(o => o.UkupniOdbici);
        if (odbici != 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Obustava na naknadi po ugovoru",
                Opis = $"Naknade van radnog odnosa nose obustave u iznosu {odbici:N2}, a one se " +
                       "skidaju samo na konačnoj zaradi. Proverite obračun pre knjiženja."
            });
        }

        ProveriKonta(konta, nalazi,
            KontaKnjizenjaSeed.ObavezaNetoNaknada,
            KontaKnjizenjaSeed.ObavezaPorezNaknada,
            KontaKnjizenjaSeed.ObavezaDoprinosiNaknada,
            KontaKnjizenjaSeed.ObavezaDoprinosiIsplatioca);
    }

    // ── Pomoćne ──────────────────────────────────────────────────────

    private static string Konto(Dictionary<string, KontoKnjizenja> konta, string kljuc)
        => konta.TryGetValue(kljuc, out var k) ? k.Konto.Trim() : "";

    /// <summary>
    /// Prijavljuje ključeve kojima konto nije upisan, ali <b>samo one koji se u ovom nalogu
    /// stvarno koriste</b> — firma bez ugovora o delu ne treba da vidi grešku o kontu naknada.
    /// </summary>
    private static void ProveriKonta(
        Dictionary<string, KontoKnjizenja> konta, List<NalazProvere> nalazi, params string[] kljucevi)
    {
        foreach (string kljuc in kljucevi)
        {
            if (!konta.TryGetValue(kljuc, out var k))
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    Provera = "Nedostaje konto",
                    Opis = $"Šifarnik „Konta za knjiženje“ nema red „{kljuc}“."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(k.Konto))
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    Provera = "Nedostaje konto",
                    Opis = $"„{k.Naziv}“ nema upisan broj konta. Nalog bez konta glavna knjiga odbija."
                });
            }
        }
    }

    private static void Dodaj(
        Dictionary<(string, string), decimal> zbir,
        Dictionary<(string, string), string> opisi,
        (string Konto, string Mt) kljuc,
        decimal iznos,
        string opis)
    {
        zbir[kljuc] = zbir.TryGetValue(kljuc, out decimal p) ? p + iznos : iznos;

        // Na isti konto se najčešće slije više vrsta primanja. Opis tada nosi prvu i tri
        // tačke — nabrajanje svih bi u nalogu bilo duže od samog iznosa.
        if (!opisi.TryGetValue(kljuc, out string? zatecen)) opisi[kljuc] = opis;
        else if (!string.Equals(zatecen, opis, StringComparison.Ordinal) && !zatecen.EndsWith('…'))
            opisi[kljuc] = zatecen + "…";
    }

    private static void UpisiStavke(
        Dictionary<(string Konto, string Mt), decimal> zbir,
        Dictionary<(string, string), string> opisi,
        List<StavkaKnjizenja> stavke,
        bool duguje,
        string izvor)
    {
        foreach (var par in zbir)
        {
            decimal iznos = Math.Round(par.Value, 2, MidpointRounding.AwayFromZero);
            if (iznos == 0) continue;

            stavke.Add(new StavkaKnjizenja
            {
                Konto = par.Key.Konto,
                MestoTroska = par.Key.Mt,
                Opis = opisi.TryGetValue(par.Key, out string? o) ? o : "",
                Duguje = duguje ? iznos : 0m,
                Potrazuje = duguje ? 0m : iznos,
                Izvor = izvor
            });
        }
    }

    private static void DodajObavezu(
        List<StavkaKnjizenja> stavke,
        Dictionary<string, KontoKnjizenja> konta,
        string kljuc,
        decimal iznos,
        string opis)
    {
        decimal zaokruzen = Math.Round(iznos, 2, MidpointRounding.AwayFromZero);
        if (zaokruzen == 0) return;

        stavke.Add(new StavkaKnjizenja
        {
            Konto = Konto(konta, kljuc),
            Opis = opis,
            Potrazuje = zaokruzen,
            Izvor = kljuc
        });
    }

    /// <summary>Ime fajla za izvoz; isti obrazac kao kod naloga za prenos.</summary>
    public static string ImeFajla(NalogZaKnjizenje nalog, string ekstenzija)
    {
        string sufiks = nalog.RedniBrojIsplate > 1 ? $"_isplata{nalog.RedniBrojIsplate}" : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"Knjizenje_{nalog.Godina}_{nalog.Mesec:D2}{sufiks}.{ekstenzija}");
    }
}
