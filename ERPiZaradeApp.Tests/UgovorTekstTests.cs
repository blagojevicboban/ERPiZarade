using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Generator teksta ugovora (Faza 2.3). Testovi drže tri stvari:
///
/// 1. da se polja zamene tačnim podacima, a <b>nepopunjeno polje ostane vidljivo</b> u tekstu
///    i bude prijavljeno — tiho brisanje bi dalo ugovor sa rupom na mestu iznosa ili roka;
/// 2. da iznos slovima bude tačan, jer se razlika brojke i slova tumači u korist slova;
/// 3. da tekst zaključenog ugovora <b>preživi izmenu šablona</b> — potpisan ugovor mora
///    ostati onakav kakav je potpisan.
/// </summary>
public class UgovorTekstTests
{
    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);
        db.Firme.Add(new Firma
        {
            Naziv = "TEST DOO",
            Adresa = "Ulica 1",
            Grad = "Pirot",
            Pib = "100000001",
            Mb = "12345678",
            Zastupnik = "Petar Petrović",
            FunkcijaZastupnika = "direktor"
        });
        db.SaveChanges();
        return db;
    }

    private static Ugovor DodajUgovor(PlataDbContext db, string tekstSablona, out SablonUgovora sablon)
    {
        var vrsta = new VrstaUgovora
        {
            Sifra = "UOD",
            Naziv = "Ugovor o delu",
            Ovp = "601",
            NormiraniTroskoviProcenat = 20m,
            StopaPoreza = 20m,
            StopaPioPrimalac = 24m
        };
        db.VrsteUgovora.Add(vrsta);

        db.Radnici.Add(new Radnik
        {
            BrojRadnika = 1,
            ImeIPrezime = "Mika Mikić",
            Jmbg = "0101990710016",
            AdresaStanovanja = "Cara Dušana 5",
            Mesto = "Pirot",
            BankovniRacun = "160-1111111111-11",
            VanRadnogOdnosa = true,
            Godina = 2026,
            Mesec = 4
        });
        db.SaveChanges();

        sablon = new SablonUgovora
        {
            Sifra = "UOD",
            Naziv = "Ugovor o delu",
            VrstaUgovoraId = vrsta.VrstaUgovoraId,
            Tekst = tekstSablona,
            Aktivan = true
        };
        db.SabloniUgovora.Add(sablon);

        var ugovor = new Ugovor
        {
            VrstaUgovoraId = vrsta.VrstaUgovoraId,
            BrojRadnika = 1,
            TipPrimaoca = TipPrimaocaPrihoda.Zaposleni,
            Broj = "12/2026",
            Predmet = "Izrada elaborata",
            UgovorenIznos = 50000m,
            DatumZakljucenja = new DateTime(2026, 4, 1),
            DatumOd = new DateTime(2026, 4, 1),
            DatumDo = new DateTime(2026, 4, 30)
        };

        db.Ugovori.Add(ugovor);
        db.SaveChanges();
        return ugovor;
    }

    // ── Zamena polja ─────────────────────────────────────────────────

    [Fact]
    public void Generisi_ZamenjujePoljaPodacimaUgovoraPrimaocaIFirme()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db,
            "{FirmaNaziv} ({FirmaPib}), zastupa {FirmaZastupnik}, {FirmaFunkcijaZastupnika}\n" +
            "{PrimalacIme}, JMBG {PrimalacJmbg}, {PrimalacRacun}\n" +
            "Ugovor {UgovorBroj} od {DatumZakljucenja}: {Predmet}\n" +
            "Rok: {DatumOd} — {DatumDo}\n" +
            "Naknada: {Iznos} ({IznosSlovima}), {VrstaIznosa}\n" +
            "SVP {Svp}, normirani troškovi {NormiraniTroskovi}%",
            out var sablon);

        var rezultat = new UgovorTekstService(db).Generisi(ugovor.UgovorId, sablon.SablonUgovoraId);

        Assert.True(rezultat.Uspesno, rezultat.Poruka);
        Assert.Empty(rezultat.NepopunjenaPolja);

        Assert.Contains("TEST DOO (100000001), zastupa Petar Petrović, direktor", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("Mika Mikić, JMBG 0101990710016, 160-1111111111-11", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("Ugovor 12/2026 od 01.04.2026: Izrada elaborata", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("Rok: 01.04.2026 — 30.04.2026", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("pedesethiljada dinara", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("bruto", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("SVP 101601000, normirani troškovi 20%", rezultat.Tekst, StringComparison.Ordinal);

        // Nijedno polje ne sme ostati nezamenjeno.
        Assert.DoesNotContain("{Firma", rezultat.Tekst, StringComparison.Ordinal);
    }

    /// <summary>
    /// Prazno polje ostaje vidljivo i prijavljuje se. Da se briše, ugovor bi izgledao ispravno
    /// sa prazninom umesto zastupnika — a to se primeti tek pri potpisu.
    /// </summary>
    [Fact]
    public void Generisi_NepopunjenoPolje_OstajeUTekstuIPrijavljujeSe()
    {
        using var db = NoviKontekst();

        var firma = db.Firme.Single();
        firma.Zastupnik = "";
        db.SaveChanges();

        var ugovor = DodajUgovor(db, "Zastupa {FirmaZastupnik}, {FirmaFunkcijaZastupnika}", out var sablon);

        var rezultat = new UgovorTekstService(db).Generisi(ugovor.UgovorId, sablon.SablonUgovoraId);

        Assert.True(rezultat.Uspesno);
        Assert.Contains("{FirmaZastupnik}", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("{FirmaZastupnik}", rezultat.NepopunjenaPolja);
        Assert.DoesNotContain("{FirmaFunkcijaZastupnika}", rezultat.NepopunjenaPolja);
    }

    [Fact]
    public void Generisi_NepoznatoPolje_OstajeUTekstu()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, "Klauzula: {NepostojecePolje}", out var sablon);

        var rezultat = new UgovorTekstService(db).Generisi(ugovor.UgovorId, sablon.SablonUgovoraId);

        Assert.Contains("{NepostojecePolje}", rezultat.Tekst, StringComparison.Ordinal);
        Assert.Contains("{NepostojecePolje}", rezultat.NepopunjenaPolja);
    }

    /// <summary>
    /// Tekst se čuva uz ugovor. Kad se šablon kasnije promeni, potpisan ugovor ostaje isti —
    /// inače bi izmena formulacije naknadno menjala već zaključene ugovore.
    /// </summary>
    [Fact]
    public void SacuvanTekst_PrezivljavaIzmenuSablona()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, "Prva verzija: {Predmet}", out var sablon);
        var servis = new UgovorTekstService(db);

        var generisan = servis.Generisi(ugovor.UgovorId, sablon.SablonUgovoraId);
        servis.Sacuvaj(ugovor.UgovorId, generisan.Tekst);

        sablon.Tekst = "Druga verzija: {Predmet}";
        db.SaveChanges();

        var sacuvan = db.Ugovori.Single();
        Assert.Equal("Prva verzija: Izrada elaborata", sacuvan.Tekst);
        Assert.NotNull(sacuvan.DatumTeksta);
    }

    [Fact]
    public void Sacuvaj_CuvaRucnuIzmenuBezDiranjaIznosa()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, "{Predmet}", out _);

        new UgovorTekstService(db).Sacuvaj(ugovor.UgovorId, "Dopisana klauzula o poverljivosti.");

        var sacuvan = db.Ugovori.Single();
        Assert.Equal("Dopisana klauzula o poverljivosti.", sacuvan.Tekst);

        // Tekst je dokument, a ne izvor podataka: iznos ostaje onakav kakav je unet.
        Assert.Equal(50000m, sacuvan.UgovorenIznos);
    }

    [Fact]
    public void PodrazumevaniSablon_BiraSablonSvojeVrsteUgovora()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, "{Predmet}", out var sablon);

        db.SabloniUgovora.Add(new SablonUgovora
        {
            Sifra = "OPS",
            Naziv = "Opšti",
            VrstaUgovoraId = null,
            Tekst = "opšti",
            Aktivan = true,
            Redosled = 1
        });
        db.SaveChanges();

        var izabrani = new UgovorTekstService(db).PodrazumevaniSablon(ugovor);

        Assert.Equal(sablon.SablonUgovoraId, izabrani?.SablonUgovoraId);
    }

    // ── Iznos slovima ────────────────────────────────────────────────

    /// <summary>
    /// Broj se piše sastavljeno, kako se piše na virmanu i u ugovoru. Rod i padež imenice
    /// zavise od poslednje cifre — „dvadesetjedan dinar", ali „dvadesetdva dinara".
    /// </summary>
    [Theory]
    [InlineData(0, "nula dinara")]
    [InlineData(1, "jedan dinar")]
    [InlineData(2, "dva dinara")]
    [InlineData(15, "petnaest dinara")]
    [InlineData(21, "dvadesetjedan dinar")]
    [InlineData(11, "jedanaest dinara")]
    [InlineData(100, "sto dinara")]
    [InlineData(1000, "hiljadu dinara")]
    [InlineData(2000, "dvehiljade dinara")]
    [InlineData(5000, "pethiljada dinara")]
    [InlineData(21000, "dvadesetjednahiljada dinara")]
    [InlineData(50000, "pedesethiljada dinara")]
    [InlineData(32400, "tridesetdvehiljadečetiristo dinara")]
    [InlineData(1000000, "milion dinara")]
    [InlineData(2000000, "dvamiliona dinara")]
    public void IznosSlovima_PisePravilno(decimal iznos, string ocekivano)
    {
        Assert.Equal(ocekivano, UgovorTekstService.IznosSlovima(iznos));
    }

    [Fact]
    public void IznosSlovima_ParePisuUzDinare()
    {
        Assert.Equal("sto dinara i pedeset para", UgovorTekstService.IznosSlovima(100.50m));
        Assert.Equal("sto dinara i dve pare", UgovorTekstService.IznosSlovima(100.02m));
        Assert.Equal("sto dinara i jedna para", UgovorTekstService.IznosSlovima(100.01m));

        // Zaokrugljivanje para naviše sme da prelije u dinar.
        Assert.Equal("deset dinara", UgovorTekstService.IznosSlovima(9.999m));
    }

    // ── Šifarnik šablona ─────────────────────────────────────────────

    /// <summary>
    /// Podrazumevani šabloni moraju pokriti sve vrste iz Faze 2.3 i koristiti samo polja koja
    /// generator poznaje — nepoznato polje bi u ugovoru ostalo kao vitičasta zagrada.
    /// </summary>
    [Fact]
    public void PodrazumevaniSabloni_KoristeSamoPoznataPolja()
    {
        var poznata = UgovorTekstService.Polja.Select(p => p.Polje).ToHashSet(StringComparer.Ordinal);
        var sabloni = SabloniUgovoraSeed.Podrazumevani();

        Assert.Equal(4, sabloni.Count);

        foreach (var sablon in sabloni)
        {
            Assert.False(string.IsNullOrWhiteSpace(sablon.Tekst), $"Šablon {sablon.Sifra} je prazan.");

            var upotrebljena = System.Text.RegularExpressions.Regex
                .Matches(sablon.Tekst, @"\{[A-Za-zČĆŠĐŽčćšđž]+\}")
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal);

            foreach (string polje in upotrebljena)
                Assert.True(poznata.Contains(polje), $"Šablon {sablon.Sifra} koristi nepoznato polje {polje}.");
        }
    }

    /// <summary>
    /// Podrazumevani šabloni moraju nositi pozivanje na propis koji im određuje obavezne
    /// elemente — bez toga se pri izmeni ne zna šta se sme izbaciti.
    /// </summary>
    [Fact]
    public void PodrazumevaniSabloni_PozivajuSeNaPropis()
    {
        var sabloni = SabloniUgovoraSeed.Podrazumevani();

        Assert.Contains("199", Sablon(sabloni, SabloniUgovoraSeed.UgovorODelu).Tekst, StringComparison.Ordinal);
        Assert.Contains("197", Sablon(sabloni, SabloniUgovoraSeed.PrivremeniPoslovi).Tekst, StringComparison.Ordinal);
        Assert.Contains("120 radnih dana", Sablon(sabloni, SabloniUgovoraSeed.PrivremeniPoslovi).Tekst, StringComparison.Ordinal);
        Assert.Contains("autorskom i srodnim pravima", Sablon(sabloni, SabloniUgovoraSeed.Autorski).Tekst, StringComparison.Ordinal);

        // Sistemski šabloni se ne brišu, pa nadogradnja ne vraća obrisani red.
        Assert.All(sabloni, s => Assert.True(s.JeSistemski));
    }

    private static SablonUgovora Sablon(List<SablonUgovora> sabloni, string sifra)
        => sabloni.Single(s => s.Sifra == sifra);
}
