using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;
using Xunit;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Uvoz prekoračenja dnevnice iz ERPiFinansije (Faza 3.2). Fajl već nosi izračunat iznos —
/// testovi drže da se ovde ništa ne računa, samo upari radnik po JMBG-u i veže za konačnu
/// zaradu ciljnog meseca, i da se uvoz zaustavlja tamo gde bi tiha greška bila skupa
/// (nepoznat radnik, ponovljen uvoz, mesec bez konačne zarade).
/// </summary>
public class PutniNaloziImportServiceTests : IDisposable
{
    private readonly string _dir;

    public PutniNaloziImportServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "pn_uvoz_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);
        db.VrstePrimanja.AddRange(VrstePrimanjaSeed.Podrazumevane());
        db.Firme.Add(new Firma { Naziv = "TEST DOO", Pib = "100000001" });
        db.Radnici.Add(new Radnik
        {
            Id = 1, BrojRadnika = 1, ImeIPrezime = "Pera Perić",
            Jmbg = "0101990710016", Godina = 2026, Mesec = 6
        });
        db.SaveChanges();
        return db;
    }

    private string NapisiFajl(string sadrzaj)
    {
        string putanja = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(putanja, sadrzaj);
        return putanja;
    }

    private static string IspravanFajl(
        string jmbg = "0101990710016", decimal prekoracenje = 1529m, string brojNaloga = "PNZ-2026/001")
        => $$"""
        {
          "Format": "ERPi-putni-nalozi-za-zarade",
          "Verzija": 1,
          "Izvor": "ERPiFinansije 1.0.0",
          "Firma": { "Naziv": "TEST DOO", "Pib": "100000001" },
          "Godina": 2026,
          "Mesec": 6,
          "Stavke": [
            { "Jmbg": "{{jmbg}}", "ZaposleniIme": "Pera Perić", "BrojNaloga": "{{brojNaloga}}",
              "DatumPovratka": "2026-06-10", "UkupnoDnevnice": 5000.00, "NeoporeziviDeo": 3471.00,
              "PrekoracenjeDnevnice": {{prekoracenje}} }
          ]
        }
        """;

    [Fact]
    public async Task IspravanFajl_UparujeRadnikaISpremaZaUvoz()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);

        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));

        Assert.True(rezultat.SmeSeUvesti);
        Assert.Equal(1, rezultat.BrojZaUvoz);
        Assert.NotNull(rezultat.Stavke[0].UparenRadnik);
        Assert.Equal(1529m, rezultat.Stavke[0].Iznos);
    }

    [Fact]
    public async Task Uvoz_UpisujeUnetoPrimanjeNaKonacnuZaraduIPravIJeAkoNePostoji()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);
        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));

        Assert.Null(rezultat.CiljnaIsplata); // mesec još nema nijednu isplatu

        int broj = servis.Uvezi(rezultat);

        Assert.Equal(1, broj);
        var primanje = db.UnetaPrimanja.Single();
        Assert.Equal(1529m, primanje.Iznos);
        Assert.NotNull(primanje.IsplataId);

        var isplata = db.Isplate.Single(i => i.IsplataId == primanje.IsplataId);
        Assert.Equal(VrstaIsplate.KonacnaZarada, isplata.Vrsta);
        Assert.Equal(RodIsplate.Zarada, isplata.Rod);
    }

    [Fact]
    public async Task NepoznatFormat_JeGreska()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);
        string putanja = NapisiFajl("""{ "Format": "neki-drugi-format", "Verzija": 1 }""");

        var rezultat = await servis.ProcitajAsync(putanja);

        Assert.False(rezultat.SmeSeUvesti);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Nije izvoz iz ERPiFinansije");
    }

    [Fact]
    public async Task NovijaVerzija_JeGreska()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);
        string putanja = NapisiFajl("""{ "Format": "ERPi-putni-nalozi-za-zarade", "Verzija": 99 }""");

        var rezultat = await servis.ProcitajAsync(putanja);

        Assert.False(rezultat.SmeSeUvesti);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Novija verzija formata");
    }

    [Fact]
    public async Task JmbgNijeNadjen_StavkaIzostaje()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);

        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl(jmbg: "9999999999999")));

        Assert.Equal(0, rezultat.BrojZaUvoz);
        Assert.False(rezultat.Stavke[0].SmeSeUvesti);
        Assert.Contains("nije nađen", rezultat.Stavke[0].Greska);
    }

    [Fact]
    public async Task VecUvezenNalog_SeOdbija()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);

        var prvi = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));
        servis.Uvezi(prvi);

        var drugi = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));

        Assert.False(drugi.Stavke[0].SmeSeUvesti);
        Assert.True(drugi.Stavke[0].VecUvezen);
    }

    [Fact]
    public async Task DvaNalogaIstogRadnikaUIstomMesecu_SeSpajajuUJedanRed()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);

        string fajl = """
        {
          "Format": "ERPi-putni-nalozi-za-zarade",
          "Verzija": 1,
          "Godina": 2026,
          "Mesec": 6,
          "Stavke": [
            { "Jmbg": "0101990710016", "ZaposleniIme": "Pera Perić", "BrojNaloga": "PNZ-2026/001", "DatumPovratka": "2026-06-05", "PrekoracenjeDnevnice": 1000.00 },
            { "Jmbg": "0101990710016", "ZaposleniIme": "Pera Perić", "BrojNaloga": "PNZ-2026/002", "DatumPovratka": "2026-06-20", "PrekoracenjeDnevnice": 500.00 }
          ]
        }
        """;

        var rezultat = await servis.ProcitajAsync(NapisiFajl(fajl));
        int broj = servis.Uvezi(rezultat);

        Assert.Equal(2, broj);
        var primanje = db.UnetaPrimanja.Single();
        Assert.Equal(1500m, primanje.Iznos);
    }

    /// <summary>
    /// Prekoračenje ide na konačnu zaradu, nikad na akontaciju — ako mesec ima isplate a
    /// nijedna nije konačna zarada, uvoz staje umesto da pogodi pogrešnu.
    /// </summary>
    [Fact]
    public async Task MesecBezKonacneZarade_JeGreska()
    {
        using var db = NoviKontekst();

        var isplataServis = new IsplataService(db);
        isplataServis.Obezbedi(2026, 6);
        isplataServis.Dodaj(2026, 6, VrstaIsplate.Akontacija, "Akontacija", new DateTime(2026, 6, 15));

        // Prva isplata je po Obezbedi uvek konačna zarada; ovde je ručno prevodimo u drugu
        // vrstu da simuliramo mesec bez ijedne konačne — servis mora ostati odbrambeno tačan
        // i za taj slučaj, ne samo za onaj koji ekran danas dozvoljava.
        var prva = db.Isplate.Single(i => i.RedniBroj == 1);
        prva.Vrsta = VrstaIsplate.Bonus;
        db.SaveChanges();

        var servis = new PutniNaloziImportService(db);
        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));

        Assert.False(rezultat.SmeSeUvesti);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Mesec nema konačnu zaradu");
    }

    [Fact]
    public async Task PostojecaKonacnaZarada_SeKoristiKaoCiljnaIsplata()
    {
        using var db = NoviKontekst();
        var isplataServis = new IsplataService(db);
        var konacna = isplataServis.Obezbedi(2026, 6);

        var servis = new PutniNaloziImportService(db);
        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));

        Assert.NotNull(rezultat.CiljnaIsplata);
        Assert.Equal(konacna.IsplataId, rezultat.CiljnaIsplata!.IsplataId);

        servis.Uvezi(rezultat);

        var primanje = db.UnetaPrimanja.Single();
        Assert.Equal(konacna.IsplataId, primanje.IsplataId);
    }

    [Fact]
    public async Task PibSeNePoklapa_JeUpozorenjeNeGreska()
    {
        using var db = NoviKontekst();
        db.Firme.First().Pib = "999999999";
        db.SaveChanges();

        var servis = new PutniNaloziImportService(db);
        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));

        // Upozorenje ne blokira uvoz — samo greške to rade.
        Assert.True(rezultat.SmeSeUvesti);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "PIB firme se ne poklapa" && n.Tezina == TezinaNalaza.Upozorenje);
    }

    /// <summary>
    /// Uvoz ne dira postojeći obračun — dodaje samo <c>UnetoPrimanje</c>. Dok se konačna
    /// zarada ponovo ne obračuna, uvezeno prekoračenje ne utiče ni na bruto ni na neto, a
    /// PreFlight to mora da prijavi umesto da prođe nezapaženo.
    /// </summary>
    [Fact]
    public async Task PreFlight_UvezenoPrimanjeBezObracuna_JeUpozorenje()
    {
        using var db = NoviKontekst();
        var servis = new PutniNaloziImportService(db);
        var rezultat = await servis.ProcitajAsync(NapisiFajl(IspravanFajl()));
        servis.Uvezi(rezultat); // pravi konačnu zaradu, ali NE pravi ObracunPlate

        var nalaz = new PreFlightService(db).Proveri(2026, 6);

        Assert.Contains(nalaz.Nalazi, n => n.Provera == "Prekoračenje dnevnice bez obračuna");
    }
}
