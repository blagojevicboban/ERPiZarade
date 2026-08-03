using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Radni sati po isplati (Faza 2.2).
///
/// <c>RadniSat</c> je bio jedinstven po (radnik, godina, mesec), pa je unos sati za drugu
/// isplatu meseca prepisivao onaj za prvu. Iznosi već napravljenih obračuna time nisu bili
/// ugroženi — svaki obračun nosi svoje sate u svojim kolonama — ali je ekran radnih sati
/// pokazivao poslednji unos, ma za koju isplatu bio rađen.
///
/// Kao i u <see cref="IsplataTests"/>, prvi i najvažniji test je kontrolni: mesec sa
/// <b>jednom</b> isplatom mora da se ponaša tačno kao pre ove izmene.
/// </summary>
public class RadniSatiPoIsplatiTests
{
    private const int Godina = 2026;
    private const int Mesec = 5;

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
            SifraOpstine = "013"
        });
        db.SaveChanges();
        return db;
    }

    private static void DodajRadnika(PlataDbContext db, int id)
    {
        db.Radnici.Add(new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Jmbg = "0101990710016",
            Godina = Godina,
            Mesec = Mesec,
            Aktivan = true
        });
        db.SaveChanges();
    }

    private static RadniSat DodajSate(PlataDbContext db, int radnikId, int? isplataId, int redovniSati)
    {
        if (!db.Radnici.Any(r => r.Id == radnikId)) DodajRadnika(db, radnikId);

        var sati = new RadniSat
        {
            RadnikId = radnikId,
            Godina = Godina,
            Mesec = Mesec,
            IsplataId = isplataId,
            RedovniSati = redovniSati
        };

        db.RadniSati.Add(sati);
        db.SaveChanges();
        return sati;
    }

    private static Isplata DodajAkontaciju(PlataDbContext db)
    {
        var rezultat = new IsplataService(db).Dodaj(
            Godina, Mesec, VrstaIsplate.Akontacija, "Akontacija", new DateTime(Godina, Mesec, 15));

        Assert.True(rezultat.Uspesno, rezultat.Poruka);
        return rezultat.Isplata!;
    }

    // ── Kontrolni test ────────────────────────────────────────────────

    /// <summary>
    /// Sve dok mesec ima jednu isplatu, sati bez upisane isplate — dakle svi zatečeni — moraju
    /// ostati obuhvaćeni. Ovaj test je taj koji drži da nadogradnja ne promeni nijedan ekran.
    /// </summary>
    [Fact]
    public void JednaIsplata_ObuhvataISateBezUpisaneIsplate()
    {
        using var db = NoviKontekst();
        DodajSate(db, 1, isplataId: null, redovniSati: 176);
        DodajSate(db, 2, isplataId: null, redovniSati: 160);

        var prva = new IsplataService(db).Obezbedi(Godina, Mesec);

        var obuhvaceni = IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, prva).ToList();

        Assert.Equal(2, obuhvaceni.Count);
        Assert.All(obuhvaceni, s => Assert.Null(s.IsplataId));
    }

    /// <summary>Bez zadate isplate obuhvat je ceo period — tako rade pozivi koji za isplate ne znaju.</summary>
    [Fact]
    public void BezZadateIsplate_ObuhvatJeCeoPeriod()
    {
        using var db = NoviKontekst();
        var servis = new IsplataService(db);
        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = DodajAkontaciju(db);

        DodajSate(db, 1, prva.IsplataId, 176);
        DodajSate(db, 1, druga.IsplataId, 80);

        Assert.Equal(2, IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, null).Count());
    }

    // ── Razdvajanje po isplati ────────────────────────────────────────

    [Fact]
    public void DrugaIsplata_ImaSvojeSate_APrviUnosOstajeNetaknut()
    {
        using var db = NoviKontekst();
        var servis = new IsplataService(db);
        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = DodajAkontaciju(db);

        DodajSate(db, 1, prva.IsplataId, 176);
        DodajSate(db, 1, druga.IsplataId, 80);

        var uPrvoj = Assert.Single(IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, prva).ToList());
        var uDrugoj = Assert.Single(IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, druga).ToList());

        Assert.Equal(176, uPrvoj.RedovniSati);
        Assert.Equal(80, uDrugoj.RedovniSati);
    }

    /// <summary>
    /// Zatečeni red bez upisane isplate pripada prvoj, pa ga druga isplata ne sme videti —
    /// inače bi joj akontacija stigla sa satima konačne zarade.
    /// </summary>
    [Fact]
    public void DrugaIsplata_NeVidiSateBezUpisaneIsplate()
    {
        using var db = NoviKontekst();
        var servis = new IsplataService(db);
        servis.Obezbedi(Godina, Mesec);
        var druga = DodajAkontaciju(db);

        DodajSate(db, 1, isplataId: null, redovniSati: 176);

        Assert.Empty(IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, druga).ToList());
    }

    // ── Uvoz sati ─────────────────────────────────────────────────────

    /// <summary>
    /// Uvoz zamenjuje sate <b>svoje</b> isplate. Do Faze 3.1 je zamenjivao sve sate meseca,
    /// pa bi uvoz za akontaciju obrisao ono što je uneto za konačnu zaradu.
    /// </summary>
    [Fact]
    public void Uvoz_UDruguIsplatu_NeDiraSatePrve()
    {
        using var db = NoviKontekst();
        var servis = new IsplataService(db);
        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = DodajAkontaciju(db);

        DodajSate(db, 1, prva.IsplataId, 176);

        var uvoz = new UvozSatiService(db);
        var procitano = new RezultatUvoza
        {
            Redovi = [new RadniSat { RadnikId = 1, Godina = Godina, Mesec = Mesec, RedovniSati = 80 }]
        };

        Assert.Equal(1, uvoz.Primeni(procitano, Godina, Mesec, druga));

        Assert.Equal(176, IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, prva).Single().RedovniSati);
        Assert.Equal(80, IsplataService.Obuhvat(db.RadniSati, Godina, Mesec, druga).Single().RedovniSati);
    }

    /// <summary>Bez zadate isplate uvoz radi kao pre: zamenjuje zatečeni red istog radnika.</summary>
    [Fact]
    public void Uvoz_BezZadateIsplate_ZamenjujeZateceneSateKaoPre()
    {
        using var db = NoviKontekst();
        DodajSate(db, 1, isplataId: null, redovniSati: 100);

        var procitano = new RezultatUvoza
        {
            Redovi = [new RadniSat { RadnikId = 1, Godina = Godina, Mesec = Mesec, RedovniSati = 176 }]
        };

        new UvozSatiService(db).Primeni(procitano, Godina, Mesec);

        var sati = Assert.Single(db.RadniSati.ToList());
        Assert.Equal(176, sati.RedovniSati);
        Assert.Null(sati.IsplataId);
    }

    // ── Veza sa isplatom ──────────────────────────────────────────────

    /// <summary>
    /// Sati su unos, ne dokaz: brišu se zajedno sa isplatom za koju su uneti. Obračun se ne
    /// briše nikad — on ostaje kao dokaz šta je bilo obračunato i prijavljeno.
    /// </summary>
    [Fact]
    public void ObrisiIsplatu_BriseISateTeIsplate()
    {
        using var db = NoviKontekst();
        var servis = new IsplataService(db);
        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = DodajAkontaciju(db);

        DodajSate(db, 1, prva.IsplataId, 176);
        DodajSate(db, 1, druga.IsplataId, 80);

        var rezultat = servis.Obrisi(druga.IsplataId);

        Assert.True(rezultat.Uspesno, rezultat.Poruka);
        Assert.Contains("radnih sati", rezultat.Poruka);

        var preostali = Assert.Single(db.RadniSati.ToList());
        Assert.Equal(176, preostali.RedovniSati);
        Assert.Equal(prva.IsplataId, preostali.IsplataId);
    }

    [Fact]
    public void PoveziZatecene_UpisujePrvuIsplatuISatima()
    {
        using var db = NoviKontekst();
        DodajSate(db, 1, isplataId: null, redovniSati: 176);
        DodajSate(db, 2, isplataId: null, redovniSati: 160);

        var servis = new IsplataService(db);
        var prva = servis.Obezbedi(Godina, Mesec);

        Assert.Equal(2, servis.PoveziZatecene(Godina, Mesec));

        Assert.All(db.RadniSati.ToList(), s =>
        {
            Assert.Equal(prva.IsplataId, s.IsplataId);
        });

        // Drugi poziv nema šta da poveže.
        Assert.Equal(0, servis.PoveziZatecene(Godina, Mesec));
    }
}
