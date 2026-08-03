using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Nalog za knjiženje (Faza 3.1).
///
/// Nalog je izveden iz obračuna i ništa u njemu ne sme da se računa iznova — zato su
/// najvažniji testovi oni koji tvrde da se zbirovi poklapaju sa onim što obračun već nosi:
/// neto sa nalogom za prenos, porez i doprinosi sa PPP-PD prijavom.
///
/// Drugi po važnosti je test ravnoteže: neuravnotežen nalog glavna knjiga odbija, a razlika
/// se u njoj više ne može vezati za radnika iz kog je došla.
/// </summary>
public class KnjizenjeServiceTests
{
    private const int Godina = 2026;
    private const int Mesec = 6;

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);

        db.Firme.Add(new Firma
        {
            Naziv = "TEST DOO",
            BankovniRacun = "160-0000000000-11",
            Pib = "100000001",
            Mb = "12345678",
            SifraOpstine = "013"
        });

        db.KontaKnjizenja.AddRange(KontaKnjizenjaSeed.Podrazumevana());
        db.SaveChanges();
        return db;
    }

    private static Radnik DodajRadnika(PlataDbContext db, int broj, string mestoTroska = "")
    {
        var radnik = new Radnik
        {
            BrojRadnika = broj,
            ImeIPrezime = $"Radnik {broj}",
            Jmbg = "0101990710016",
            Godina = Godina,
            Mesec = Mesec,
            SifraMestaTroska = mestoTroska,
            Aktivan = true
        };

        db.Radnici.Add(radnik);
        db.SaveChanges();
        return radnik;
    }

    /// <summary>
    /// Obračun sa brojevima koji se drže: neto je bruto umanjen za porez, doprinose i obustave.
    /// </summary>
    private static ObracunPlate DodajZaradu(
        PlataDbContext db, Radnik radnik, decimal bruto, int? isplataId = null,
        decimal obustava = 0m, decimal samodoprinos = 0m)
    {
        decimal porez = Math.Round(bruto * 0.10m, 2);
        decimal pio = Math.Round(bruto * 0.14m, 2);
        decimal zdr = Math.Round(bruto * 0.0515m, 2);
        decimal nez = Math.Round(bruto * 0.0075m, 2);

        var obracun = new ObracunPlate
        {
            RadnikId = radnik.Id,
            Godina = Godina,
            Mesec = Mesec,
            IsplataId = isplataId,
            BrutoZarada = bruto,
            PorezNaDohodak = porez,
            DoprinosPioRadnik = pio,
            DoprinosZdravstvoRadnik = zdr,
            DoprinosNezaposlenostRadnik = nez,
            DoprinosPioPoslodavac = Math.Round(bruto * 0.10m, 2),
            DoprinosZdravstvoPoslodavac = zdr,
            KreditObustava = obustava,
            Samodoprinosi = samodoprinos,
            NetoIsplata = bruto - porez - pio - zdr - nez - obustava - samodoprinos
        };

        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    /// <summary>Vrsta primanja sa svojim kontom, i stavka obračuna po njoj.</summary>
    private static void DodajStavku(PlataDbContext db, ObracunPlate obracun, string sifra, string konto, decimal iznos)
    {
        var vrsta = db.VrstePrimanja.FirstOrDefault(v => v.Sifra == sifra);

        if (vrsta == null)
        {
            vrsta = new VrstaPrimanja { Sifra = sifra, Naziv = sifra, Konto = konto };
            db.VrstePrimanja.Add(vrsta);
            db.SaveChanges();
        }

        db.ObracunStavke.Add(new ObracunStavka
        {
            ObracunPlateId = obracun.Id,
            VrstaPrimanjaId = vrsta.VrstaPrimanjaId,
            Iznos = iznos,
            OporeziviDeo = iznos
        });
        db.SaveChanges();
    }

    private static NalogZaKnjizenje Pripremi(PlataDbContext db, Isplata? isplata = null)
        => new KnjizenjeService(db).Pripremi(Godina, Mesec, isplata, new DateTime(Godina, Mesec, 28));

    private static decimal NaKontu(NalogZaKnjizenje nalog, string konto)
        => nalog.Stavke.Where(s => s.Konto == konto).Sum(s => s.Duguje + s.Potrazuje);

    // ── Ravnoteža ─────────────────────────────────────────────────────

    /// <summary>
    /// Prvi i najvažniji test: nalog mora biti u ravnoteži. Bez toga glavna knjiga odbija
    /// dokument, a razlika se u njoj više ne vezuje za radnika iz kog je došla.
    /// </summary>
    [Fact]
    public void Nalog_JeUravnotezen()
    {
        using var db = NoviKontekst();
        var r1 = DodajRadnika(db, 1);
        var r2 = DodajRadnika(db, 2);
        DodajZaradu(db, r1, 100_000m);
        DodajZaradu(db, r2, 80_000m, obustava: 5_000m);

        var nalog = Pripremi(db);

        Assert.True(nalog.JeUravnotezen, $"Razlika {nalog.Razlika:N2}");
        Assert.Equal(nalog.UkupnoDuguje, nalog.UkupnoPotrazuje);
        Assert.True(nalog.SmeSeIzvesti);
    }

    /// <summary>
    /// Neoporeziva primanja (prevoz, jubilarna nagrada) se isplaćuju radniku ali u bruto
    /// iznos ne ulaze. Trošak zato mora ići iz <b>stavki</b>, a ne iz bruta — inače nalog
    /// ne stoji baš za obračune koji imaju takvo primanje.
    /// </summary>
    [Fact]
    public void NeoporezivoPrimanje_UlaziUTrosak_INalogOstajeUravnotezen()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        // Bruto 100.000 uz prevoz 5.000 koji se isplaćuje pored zarade.
        var obracun = DodajZaradu(db, radnik, 100_000m);
        obracun.NetoIsplata += 5_000m;
        db.SaveChanges();

        DodajStavku(db, obracun, "ZAR", "520", 100_000m);
        DodajStavku(db, obracun, "PRV", "529", 5_000m);

        var nalog = Pripremi(db);

        Assert.True(nalog.JeUravnotezen, $"Razlika {nalog.Razlika:N2}");
        Assert.Equal(5_000m, NaKontu(nalog, "529"));
        Assert.Equal(100_000m, NaKontu(nalog, "520"));
    }

    /// <summary>
    /// Obračun čiji se sastav ne slaže mora da se prijavi <b>po radniku</b>, dok je ispravka
    /// još jeftina. Nalog se u tom slučaju ne izvozi.
    /// </summary>
    [Fact]
    public void NeslaganjeSastava_PrijavljujeSePoRadniku_INalogSeNeIzvozi()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 7);
        var obracun = DodajZaradu(db, radnik, 100_000m);

        obracun.NetoIsplata += 1_000m;   // neto koji ne odgovara sastavu
        db.SaveChanges();

        var nalog = Pripremi(db);

        var nalaz = Assert.Single(nalog.Nalazi, n => n.Provera == "Sastav obračuna se ne slaže");
        Assert.Equal(7, nalaz.BrojRadnika);
        Assert.Equal(TezinaNalaza.Greska, nalaz.Tezina);
        Assert.False(nalog.SmeSeIzvesti);
    }

    // ── Poklapanje sa onim što obračun već nosi ───────────────────────

    /// <summary>
    /// Iznos na kontu 450 mora biti isti onaj koji ide na naloge za prenos. Da se računa
    /// iznova, temeljnica bi umela da se raziđe sa bankom.
    /// </summary>
    [Fact]
    public void ObavezaZaNetoZaradu_JednakaZbiruNetoIsplata()
    {
        using var db = NoviKontekst();
        var r1 = DodajRadnika(db, 1);
        var r2 = DodajRadnika(db, 2);
        var o1 = DodajZaradu(db, r1, 100_000m);
        var o2 = DodajZaradu(db, r2, 60_000m, obustava: 3_000m);

        var nalog = Pripremi(db);

        Assert.Equal(o1.NetoIsplata + o2.NetoIsplata, NaKontu(nalog, "450"));
    }

    [Fact]
    public void PorezIDoprinosi_IduNaSvojaKonta()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var o = DodajZaradu(db, radnik, 100_000m);

        var nalog = Pripremi(db);

        Assert.Equal(o.PorezNaDohodak, NaKontu(nalog, "451"));
        Assert.Equal(o.UkupniDoprinosi, NaKontu(nalog, "452"));
        Assert.Equal(o.UkupniDoprinosiPoslodavca, NaKontu(nalog, "453"));

        // Doprinosi poslodavca su trošak firme, pa moraju stajati i na strani duguje.
        Assert.Equal(o.UkupniDoprinosiPoslodavca, NaKontu(nalog, "521"));
    }

    [Fact]
    public void Obustave_IduNaSvojKonto()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajZaradu(db, radnik, 100_000m, obustava: 7_500m, samodoprinos: 1_000m);

        var nalog = Pripremi(db);

        Assert.Equal(7_500m, NaKontu(nalog, "469"));
        Assert.Equal(1_000m, NaKontu(nalog, "489"));
        Assert.True(nalog.JeUravnotezen);
    }

    // ── Mesta troška ──────────────────────────────────────────────────

    /// <summary>
    /// Trošak se deli po mestu troška iz kartona radnika, a obaveza ne — obaveza prema
    /// radniku je jedna bez obzira gde je radio.
    /// </summary>
    [Fact]
    public void Trosak_SeDeliPoMestuTroska_ObavezaNe()
    {
        using var db = NoviKontekst();
        var r1 = DodajRadnika(db, 1, "MT-01");
        var r2 = DodajRadnika(db, 2, "MT-02");
        DodajZaradu(db, r1, 100_000m);
        DodajZaradu(db, r2, 50_000m);

        var nalog = Pripremi(db);

        var troskovi = nalog.Stavke.Where(s => s.Konto == "520").ToList();
        Assert.Equal(2, troskovi.Count);
        Assert.Equal(100_000m, troskovi.Single(s => s.MestoTroska == "MT-01").Duguje);
        Assert.Equal(50_000m, troskovi.Single(s => s.MestoTroska == "MT-02").Duguje);

        // Obaveza za neto ostaje jedna stavka, bez mesta troška.
        var obaveza = Assert.Single(nalog.Stavke, s => s.Konto == "450");
        Assert.Equal("", obaveza.MestoTroska);
    }

    /// <summary>
    /// Radnik bez mesta troška se prijavljuje samo kad ga <b>neki</b> imaju — firma koja ih
    /// uopšte ne vodi ne treba da vidi upozorenje na svakom nalogu.
    /// </summary>
    [Fact]
    public void MestoTroska_UpozorenjeSamoKadGaNekiImaju()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);
        DodajZaradu(db, DodajRadnika(db, 2), 50_000m);

        Assert.DoesNotContain(Pripremi(db).Nalazi, n => n.Provera == "Radnik bez mesta troška");

        using var db2 = NoviKontekst();
        DodajZaradu(db2, DodajRadnika(db2, 1, "MT-01"), 100_000m);
        DodajZaradu(db2, DodajRadnika(db2, 2), 50_000m);

        Assert.Contains(Pripremi(db2).Nalazi, n => n.Provera == "Radnik bez mesta troška");
    }

    // ── Vrste primanja ────────────────────────────────────────────────

    /// <summary>
    /// Kad je obračun razložen na stavke, trošak ide na konto svake vrste posebno — to je
    /// ono zbog čega konto uopšte stoji uz vrstu primanja.
    /// </summary>
    [Fact]
    public void Stavke_RazvrstavajuTrosakPoKontimaVrstaPrimanja()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var obracun = DodajZaradu(db, radnik, 100_000m);

        DodajStavku(db, obracun, "ZAR", "520", 90_000m);
        DodajStavku(db, obracun, "TOP", "520-1", 10_000m);

        var nalog = Pripremi(db);

        Assert.Equal(90_000m, NaKontu(nalog, "520"));
        Assert.Equal(10_000m, NaKontu(nalog, "520-1"));
        Assert.True(nalog.JeUravnotezen);
    }

    /// <summary>
    /// Vrsta primanja bez konta ne sme tiho da padne na zbirni konto — trošak bi završio na
    /// pogrešnom mestu, a to se otkriva tek u bilansu.
    /// </summary>
    [Fact]
    public void VrstaPrimanjaBezKonta_PrijavljujeSeKaoGreska()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var obracun = DodajZaradu(db, radnik, 100_000m);
        DodajStavku(db, obracun, "STI", konto: "", iznos: 100_000m);

        var nalog = Pripremi(db);

        Assert.Contains(nalog.Nalazi, n => n.Provera == "Vrsta primanja bez konta" && n.Tezina == TezinaNalaza.Greska);
        Assert.False(nalog.SmeSeIzvesti);

        // Iznos ipak ostaje u nalogu, na zbirnom kontu — nalog mora ostati u ravnoteži da bi
        // se videlo da nedostaje samo raspored, a ne novac.
        Assert.True(nalog.JeUravnotezen);
    }

    /// <summary>Obračun zatečen pre Faze 2.1 nema stavke; bruto ide ceo na zbirni konto.</summary>
    [Fact]
    public void ObracunBezStavki_IdeNaZbirniKonto_UzUpozorenje()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);

        var nalog = Pripremi(db);

        Assert.Equal(100_000m, NaKontu(nalog, "520"));
        Assert.Contains(nalog.Nalazi, n => n.Provera == "Obračun nije razložen na stavke"
                                           && n.Tezina == TezinaNalaza.Upozorenje);
        Assert.True(nalog.SmeSeIzvesti);
    }

    // ── Storno i isplate ──────────────────────────────────────────────

    /// <summary>Stornirani obračun se ne knjiži — nije ni isplaćen ni prijavljen.</summary>
    [Fact]
    public void StorniranObracun_NeUlaziUNalog()
    {
        using var db = NoviKontekst();
        var o = DodajZaradu(db, DodajRadnika(db, 1), 100_000m);
        DodajZaradu(db, DodajRadnika(db, 2), 50_000m);

        o.Storniran = true;
        db.SaveChanges();

        var nalog = Pripremi(db);

        Assert.Equal(1, nalog.BrojObracuna);
        Assert.Equal(50_000m, NaKontu(nalog, "520"));
    }

    /// <summary>
    /// Kontrolni test: dok mesec ima jednu isplatu, nalog obuhvata i obračune bez upisane
    /// isplate — dakle sve zatečene.
    /// </summary>
    [Fact]
    public void JednaIsplata_ObuhvataIObracuneBezUpisaneIsplate()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m, isplataId: null);

        var prva = new IsplataService(db).Obezbedi(Godina, Mesec);
        var nalog = Pripremi(db, prva);

        Assert.Equal(1, nalog.BrojObracuna);
        Assert.Equal(100_000m, NaKontu(nalog, "520"));
    }

    /// <summary>
    /// Akontacija i konačna zarada se knjiže zasebnim nalozima — svaka ima svoj datum i
    /// svoju prijavu, pa bi jedan nalog spojio dva dokumenta.
    /// </summary>
    [Fact]
    public void SvakaIsplata_ImaSvojNalog()
    {
        using var db = NoviKontekst();
        var servis = new IsplataService(db);
        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "Akontacija",
            new DateTime(Godina, Mesec, 15)).Isplata!;

        var radnik = DodajRadnika(db, 1);
        DodajZaradu(db, radnik, 100_000m, prva.IsplataId);
        DodajZaradu(db, radnik, 30_000m, druga.IsplataId);

        Assert.Equal(100_000m, NaKontu(Pripremi(db, prva), "520"));
        Assert.Equal(30_000m, NaKontu(Pripremi(db, druga), "520"));

        // Bez zadate isplate obuhvat je ceo period — tako rade pozivi koji za isplate ne znaju.
        Assert.Equal(130_000m, NaKontu(Pripremi(db, null), "520"));
    }

    [Fact]
    public void PrazanPeriod_PrijavljujeGresku()
    {
        using var db = NoviKontekst();

        var nalog = Pripremi(db);

        Assert.Empty(nalog.Stavke);
        Assert.Contains(nalog.Nalazi, n => n.Provera == "Prazan period");
        Assert.False(nalog.SmeSeIzvesti);
    }

    // ── Naknade van radnog odnosa ─────────────────────────────────────

    private static ObracunPlate DodajNaknadu(PlataDbContext db, Radnik primalac, decimal bruto, string konto)
    {
        var vrsta = new VrstaUgovora
        {
            Sifra = "UOD",
            Naziv = "Ugovor o delu",
            Ovp = "601",
            NormiraniTroskoviProcenat = 20m,
            StopaPoreza = 20m,
            StopaPioPrimalac = 24m,
            Konto = konto
        };

        db.VrsteUgovora.Add(vrsta);
        db.SaveChanges();

        var ugovor = new Ugovor
        {
            VrstaUgovoraId = vrsta.VrstaUgovoraId,
            BrojRadnika = primalac.BrojRadnika,
            Predmet = "Izrada elaborata",
            UgovorenIznos = bruto
        };

        db.Ugovori.Add(ugovor);
        db.SaveChanges();

        decimal osnovica = bruto * 0.80m;
        decimal porez = Math.Round(osnovica * 0.20m, 2);
        decimal pio = Math.Round(osnovica * 0.24m, 2);

        var obracun = new ObracunPlate
        {
            RadnikId = primalac.Id,
            Godina = Godina,
            Mesec = Mesec,
            UgovorId = ugovor.UgovorId,
            BrutoZarada = bruto,
            OsnovicaDoprinosa = osnovica,
            PorezNaDohodak = porez,
            DoprinosPioRadnik = pio,
            NetoIsplata = bruto - porez - pio
        };

        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    /// <summary>
    /// Naknada po ugovoru ide na svoja konta: trošak po vrsti ugovora, obaveza prema
    /// fizičkom licu, porez i doprinosi na konto ostalih obaveza.
    /// </summary>
    [Fact]
    public void NaknadaPoUgovoru_IdeNaSvojaKonta()
    {
        using var db = NoviKontekst();
        var primalac = DodajRadnika(db, 90);
        var o = DodajNaknadu(db, primalac, 50_000m, konto: "522");

        var nalog = Pripremi(db);

        Assert.Equal(50_000m, NaKontu(nalog, "522"));
        Assert.Equal(o.NetoIsplata, NaKontu(nalog, "465"));
        Assert.Equal(o.PorezNaDohodak + o.UkupniDoprinosi, NaKontu(nalog, "489"));
        Assert.True(nalog.JeUravnotezen, $"Razlika {nalog.Razlika:N2}");
    }

    /// <summary>
    /// Zarada i naknada u istoj isplati idu u <b>jedan</b> nalog, ali na razdvojena konta —
    /// prijava je ionako jedna po isplati, sa svim prihodima tog dana.
    /// </summary>
    [Fact]
    public void ZaradaINaknada_UIstomNalogu_NaRazdvojenimKontima()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);
        DodajNaknadu(db, DodajRadnika(db, 90), 50_000m, konto: "522");

        var nalog = Pripremi(db);

        Assert.Equal(100_000m, NaKontu(nalog, "520"));
        Assert.Equal(50_000m, NaKontu(nalog, "522"));
        Assert.True(nalog.JeUravnotezen, $"Razlika {nalog.Razlika:N2}");
        Assert.Equal(2, nalog.BrojObracuna);
    }

    [Fact]
    public void VrstaUgovoraBezKonta_PrijavljujeSeKaoGreska()
    {
        using var db = NoviKontekst();
        DodajNaknadu(db, DodajRadnika(db, 90), 50_000m, konto: "");

        var nalog = Pripremi(db);

        Assert.Contains(nalog.Nalazi, n => n.Provera == "Vrsta ugovora bez konta");
        Assert.False(nalog.SmeSeIzvesti);
        Assert.True(nalog.JeUravnotezen);
    }

    // ── Šifarnik konta ────────────────────────────────────────────────

    /// <summary>
    /// Broj konta je stvar kontnog plana firme: izmena u šifarniku mora da se vidi na
    /// sledećem nalogu, bez nove verzije programa.
    /// </summary>
    [Fact]
    public void IzmenaKontaUSifarniku_MenjaNalog()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);

        var konto = db.KontaKnjizenja.Single(k => k.Kljuc == KontaKnjizenjaSeed.ObavezaNetoZarada);
        konto.Konto = "450-1";
        db.SaveChanges();

        var nalog = Pripremi(db);

        Assert.Equal(0m, NaKontu(nalog, "450"));
        Assert.True(NaKontu(nalog, "450-1") > 0m);
    }

    [Fact]
    public void KontoBezBroja_ZaustavljaIzvoz()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);

        var konto = db.KontaKnjizenja.Single(k => k.Kljuc == KontaKnjizenjaSeed.ObavezaPorezZaposleni);
        konto.Konto = "";
        db.SaveChanges();

        var nalog = Pripremi(db);

        Assert.Contains(nalog.Nalazi, n => n.Provera == "Nedostaje konto");
        Assert.False(nalog.SmeSeIzvesti);
    }

    /// <summary>Ključevi u šifarniku moraju biti jedinstveni — nalog inače zavisi od redosleda.</summary>
    [Fact]
    public void Sifarnik_ImaJedinstveneKljuceve()
    {
        var podrazumevana = KontaKnjizenjaSeed.Podrazumevana();

        Assert.Equal(podrazumevana.Count, podrazumevana.Select(k => k.Kljuc).Distinct(StringComparer.Ordinal).Count());
        Assert.All(podrazumevana, k => Assert.False(string.IsNullOrWhiteSpace(k.Konto)));
        Assert.All(podrazumevana, k => Assert.False(string.IsNullOrWhiteSpace(k.Naziv)));
    }

    // ── Izvoz ─────────────────────────────────────────────────────────

    [Fact]
    public void Izvoz_SadrziOznakuFormataIStavke()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1, "MT-01"), 100_000m);

        var nalog = Pripremi(db);
        string json = NalogKnjizenjaWriter.Generisi(nalog, db.Firme.First(), out var nalazi);

        Assert.DoesNotContain(nalazi, n => n.Tezina == TezinaNalaza.Greska);
        Assert.Contains(NalogKnjizenjaWriter.OznakaFormata, json, StringComparison.Ordinal);
        Assert.Contains("\"MestoTroska\": \"MT-01\"", json, StringComparison.Ordinal);
        Assert.Contains("TEST DOO", json, StringComparison.Ordinal);

        // Naši znakovi ostaju čitljivi, ne beže u \uXXXX zapis.
        Assert.Contains("Obaveze za neto zarade", json, StringComparison.Ordinal);
    }

    /// <summary>Neuravnotežen nalog se ne sme snimiti — glavna knjiga bi ga odbila.</summary>
    [Fact]
    public void Izvoz_NeuravnotezenogNaloga_PrijavljujeGresku()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var o = DodajZaradu(db, radnik, 100_000m);
        o.NetoIsplata += 1_000m;
        db.SaveChanges();

        var nalog = Pripremi(db);
        NalogKnjizenjaWriter.Generisi(nalog, db.Firme.First(), out var nalazi);

        Assert.Contains(nalazi, n => n.Provera == "Nalog nije u ravnoteži");
    }

    [Fact]
    public void Csv_ImaZaglavljeIZbir()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);

        string csv = NalogKnjizenjaWriter.GenerisiCsv(Pripremi(db));

        Assert.StartsWith("Redni broj;Konto;Opis;Duguje;Potražuje;Mesto troška", csv, StringComparison.Ordinal);
        Assert.Contains("UKUPNO;", csv, StringComparison.Ordinal);
    }

    // ── Naknada koja se refundira od RFZO (Faza 2.6) ──────────────────

    /// <summary>
    /// Bolovanje preko 30 dana; obračun nosi i zaradu i naknadu, sve po zakonskim stopama.
    /// Naknada je označena kao naknada na teret Fonda, zarada nije.
    /// </summary>
    private static ObracunPlate DodajSaRefundacijom(
        PlataDbContext db, Radnik radnik, decimal zarada, decimal naknada, decimal obustava = 0m)
    {
        var obracun = DodajZaradu(db, radnik, zarada + naknada, obustava: obustava);

        DodajStavku(db, obracun, VrstePrimanjaSeed.OsnovnaZarada, "520", zarada);
        DodajStavku(db, obracun, VrstePrimanjaSeed.BolovanjePreko30, "520", naknada);

        var vrsta = db.VrstePrimanja.Single(v => v.Sifra == VrstePrimanjaSeed.BolovanjePreko30);
        vrsta.NaTeretFonda = true;
        db.SaveChanges();

        return obracun;
    }

    /// <summary>
    /// Refundirana naknada <b>nije trošak</b>: Kontni okvir je izvodi iz grupe 52 u celosti.
    /// Umesto troška nastaje potraživanje od Fonda na 225, a obaveze idu na 454, 455 i 456
    /// umesto na 450–453.
    /// </summary>
    [Fact]
    public void Refundacija_NeIdeNaTrosakNegoNaPotrazivanje()
    {
        using var db = NoviKontekst();
        DodajSaRefundacijom(db, DodajRadnika(db, 1), zarada: 60_000m, naknada: 40_000m);

        var nalog = Pripremi(db);

        Assert.True(nalog.JeUravnotezen, $"Razlika {nalog.Razlika:N2}");

        // Na trošak ide samo zarada; naknada je iz njega izašla.
        Assert.Equal(60_000m, NaKontu(nalog, "520"));

        // Potraživanje je bruto naknade uvećan za doprinose poslodavca na taj deo.
        decimal doprinosiPoslodavcaNaNaknadu = Math.Round(100_000m * 0.1515m * 0.40m, 2);
        Assert.Equal(40_000m + doprinosiPoslodavcaNaNaknadu, NaKontu(nalog, "225"));

        // Obaveze po refundaciji: 454 neto, 455 porez i doprinosi radnika, 456 doprinosi poslodavca.
        Assert.Equal(doprinosiPoslodavcaNaNaknadu, NaKontu(nalog, "456"));
        Assert.Equal(Math.Round(100_000m * 0.299m * 0.40m, 2), NaKontu(nalog, "455"));
        Assert.Equal(NaKontu(nalog, "225") - NaKontu(nalog, "455") - NaKontu(nalog, "456"), NaKontu(nalog, "454"));
    }

    /// <summary>
    /// Bez obustava potraživanje od Fonda mora biti tačno zbir tri obaveze koje su iz njega
    /// nastale — inače bi se ono što se traži razlikovalo od onoga što je obračunato.
    /// </summary>
    [Fact]
    public void Refundacija_Potrazivanje_JeZbirTriObaveze()
    {
        using var db = NoviKontekst();
        DodajSaRefundacijom(db, DodajRadnika(db, 1), zarada: 33_333.33m, naknada: 66_666.67m);

        var nalog = Pripremi(db);

        Assert.Equal(
            NaKontu(nalog, "454") + NaKontu(nalog, "455") + NaKontu(nalog, "456"),
            NaKontu(nalog, "225"));
    }

    /// <summary>
    /// Glavna kontrola cele faze: iznos na kontu <b>225</b> mora biti jednak koloni
    /// „за исплату" obrasca OZ-10 za isti mesec. To je isti novac — ono što Fond vraća — i
    /// oba broja dolaze iz istog izvora, pa se ne smeju razići.
    /// </summary>
    [Fact]
    public void Refundacija_Konto225_JeJednakSpiskuOz10()
    {
        using var db = NoviKontekst();

        db.Firme.Single().PosebanRacun = "160-0000000123-45";
        db.Firme.Single().SifraDelatnosti = "6201";

        var radnik = DodajRadnika(db, 1);
        DodajSaRefundacijom(db, radnik, zarada: 25_000m, naknada: 75_000m);

        db.Bolovanja.Add(new Bolovanje
        {
            BrojRadnika = 1,
            Godina = Godina,
            Mesec = Mesec,
            DatumPocetkaSprecenosti = new DateTime(Godina, Mesec - 1, 1),
            DatumOd = new DateTime(Godina, Mesec, 1),
            DatumDo = new DateTime(Godina, Mesec, 22),
            PrvaIsplata = true
        });
        db.SaveChanges();

        var nalog = Pripremi(db);
        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.Equal(spisak.UkupnoZaIsplatu, NaKontu(nalog, "225"));
        Assert.Equal(spisak.UkupnoBruto, NaKontu(nalog, "454") + NaKontu(nalog, "455"));
    }

    /// <summary>
    /// Pun mesec bolovanja kod radnika sa kreditom. Obustava se skida prvo sa zarade, a
    /// zarade ovde nema — pa pada na naknadu. Bez tog redosleda bi konto 450 ispao
    /// <b>negativan</b>, a takav nalog glavna knjiga odbija.
    /// </summary>
    [Fact]
    public void Refundacija_PunMesecBolovanjaSaKreditom_NeDajeNegativan450()
    {
        using var db = NoviKontekst();
        DodajSaRefundacijom(db, DodajRadnika(db, 1), zarada: 0m, naknada: 80_000m, obustava: 10_000m);

        var nalog = Pripremi(db);

        Assert.True(nalog.JeUravnotezen, $"Razlika {nalog.Razlika:N2}");
        Assert.All(nalog.Stavke, s => Assert.True(s.Duguje >= 0 && s.Potrazuje >= 0,
            $"Konto {s.Konto}: duguje {s.Duguje:N2}, potražuje {s.Potrazuje:N2}"));

        // Nema zarade, pa nema ni obaveze za neto zaradu; sve je na 454, umanjeno za obustavu.
        Assert.Equal(0m, NaKontu(nalog, "450"));
        Assert.Equal(10_000m, NaKontu(nalog, "469"));

        decimal netoNaknade = 80_000m - Math.Round(80_000m * 0.10m, 2) - Math.Round(80_000m * 0.199m, 2);
        Assert.Equal(netoNaknade - 10_000m, NaKontu(nalog, "454"));

        // Potraživanje od Fonda obustava ne dira — Fond refundira obračunato, ne isplaćeno.
        Assert.Equal(80_000m + Math.Round(80_000m * 0.1515m, 2), NaKontu(nalog, "225"));
    }

    /// <summary>
    /// Bez naknade na teret Fonda nalog mora ostati **nepromenjen**: nijedan konto grupe
    /// refundacije se ne pojavljuje, a 450 je i dalje jednak zbiru naloga za prenos.
    /// </summary>
    [Fact]
    public void BezRefundacije_NalogJeNepromenjen()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var obracun = DodajZaradu(db, radnik, 100_000m, obustava: 7_000m);
        DodajStavku(db, obracun, VrstePrimanjaSeed.OsnovnaZarada, "520", 100_000m);

        var nalog = Pripremi(db);

        Assert.Equal(0m, NaKontu(nalog, "225"));
        Assert.Equal(0m, NaKontu(nalog, "454"));
        Assert.Equal(0m, NaKontu(nalog, "455"));
        Assert.Equal(0m, NaKontu(nalog, "456"));
        Assert.Equal(obracun.NetoIsplata, NaKontu(nalog, "450"));
        Assert.Equal(100_000m, NaKontu(nalog, "520"));
    }

    [Fact]
    public void ImeFajla_NosiIsplatuTekKadIhImaVise()
    {
        using var db = NoviKontekst();
        DodajZaradu(db, DodajRadnika(db, 1), 100_000m);
        var nalog = Pripremi(db);

        Assert.Equal($"Knjizenje_{Godina}_{Mesec:D2}.json", KnjizenjeService.ImeFajla(nalog, "json"));
    }
}
