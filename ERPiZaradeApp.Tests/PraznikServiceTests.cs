using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Fond sati ulazi u cenu radnog sata, pa greška u kalendaru menja platu svakom radniku.
/// Datum pravoslavnog Uskrsa je jedini deo koji se ne može proveriti pogledom na kalendar,
/// zato se poredi sa poznatim datumima iz više godina.
/// </summary>
public class PraznikServiceTests
{
    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlataDbContext(options);
    }

    // ── Uskrs ────────────────────────────────────────────────────────

    /// <summary>Poznati datumi pravoslavnog Uskrsa po gregorijanskom kalendaru.</summary>
    [Theory]
    [InlineData(2022, 4, 24)]
    [InlineData(2023, 4, 16)]
    [InlineData(2024, 5, 5)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 12)]
    [InlineData(2027, 5, 2)]
    public void PravoslavniUskrs_SlazeSeSaPoznatimDatumima(int godina, int mesec, int dan)
    {
        Assert.Equal(new DateTime(godina, mesec, dan), PraznikService.PravoslavniUskrs(godina));
    }

    /// <summary>Van 1900–2099 razlika između kalendara nije 13 dana, pa se metoda ne sme koristiti.</summary>
    [Fact]
    public void PravoslavniUskrs_VanPodrzanogOpsega_Odbija()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PraznikService.PravoslavniUskrs(2100));
    }

    [Fact]
    public void ZakonskiPraznici_SadrzeUskrsnjeDaneOkoUskrsa()
    {
        var praznici = PraznikService.ZakonskiPraznici(2026);
        var uskrs = new DateTime(2026, 4, 12);

        Assert.Contains(praznici, p => p.Datum == uskrs.AddDays(-2) && p.Naziv == "Veliki petak");
        Assert.Contains(praznici, p => p.Datum == uskrs);
        Assert.Contains(praznici, p => p.Datum == uskrs.AddDays(1) && p.Naziv == "Uskrsni ponedeljak");
    }

    [Fact]
    public void ZakonskiPraznici_SadrzeNovuGodinuBozicIDanPrimirja()
    {
        var praznici = PraznikService.ZakonskiPraznici(2026);

        Assert.Contains(praznici, p => p.Datum == new DateTime(2026, 1, 1));
        Assert.Contains(praznici, p => p.Datum == new DateTime(2026, 1, 7) && p.Naziv == "Božić");
        Assert.Contains(praznici, p => p.Datum == new DateTime(2026, 11, 11));
    }

    /// <summary>
    /// Zakon: ako državni praznik padne u nedelju, ne radi se prvog narednog radnog dana.
    /// 1. novembar 2026. nije praznik, ali 1. februar 2026. jeste nedelja — proverava se
    /// na Sretenju, koje 15.02.2026. pada baš u nedelju.
    /// </summary>
    [Fact]
    public void ZakonskiPraznici_DrzavniPraznikUNedelju_DajeNeradniPonedeljak()
    {
        Assert.Equal(DayOfWeek.Sunday, new DateTime(2026, 2, 15).DayOfWeek);

        var praznici = PraznikService.ZakonskiPraznici(2026);

        // 15. i 16. su praznik; pošto 15. pada u nedelju, neradni je i 17. februar.
        Assert.Contains(praznici, p => p.Datum == new DateTime(2026, 2, 17));
    }

    /// <summary>Verski praznik se ne pomera ako padne u nedelju.</summary>
    [Fact]
    public void ZakonskiPraznici_BozicUNedelju_NePomeraNaPonedeljak()
    {
        // 7. januar 2024. je nedelja.
        Assert.Equal(DayOfWeek.Sunday, new DateTime(2024, 1, 7).DayOfWeek);

        var praznici = PraznikService.ZakonskiPraznici(2024);

        Assert.DoesNotContain(praznici, p => p.Datum == new DateTime(2024, 1, 8));
    }

    [Fact]
    public void ZakonskiPraznici_NemaDvaZapisaZaIstiDan()
    {
        var praznici = PraznikService.ZakonskiPraznici(2026);

        Assert.Equal(praznici.Count, praznici.Select(p => p.Datum.Date).Distinct().Count());
    }

    // ── Kalendar u bazi ──────────────────────────────────────────────

    [Fact]
    public void ObezbediGodinu_PopunjavaPrazniceISamoJednom()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);

        int prviPut = servis.ObezbediGodinu(2026);
        int drugiPut = servis.ObezbediGodinu(2026);

        Assert.True(prviPut > 0);
        Assert.Equal(0, drugiPut);
    }

    /// <summary>Ponovno popunjavanje ne sme da obriše ono što je firma sama dodala.</summary>
    [Fact]
    public void ObezbediGodinu_NeDiraRucnoUneteDane()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);
        servis.ObezbediGodinu(2026);

        db.Praznici.Add(new Praznik
        {
            Datum = new DateTime(2026, 8, 28),
            Naziv = "Slava firme",
            RucniUnos = true
        });
        db.SaveChanges();

        servis.ObezbediGodinu(2026);

        Assert.Contains(db.Praznici, p => p.Naziv == "Slava firme");
    }

    // ── Fond sati ────────────────────────────────────────────────────

    /// <summary>
    /// Mart 2026. ima 22 radna dana (31 dan, 9 vikend-dana) i nijedan praznik,
    /// pa je fond 176 sati.
    /// </summary>
    [Fact]
    public void FondSati_MesecBezPraznika_JeBrojRadnihDanaPutaOsam()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);
        servis.ObezbediGodinu(2026);

        Assert.Equal(22, servis.RadniDani(2026, 3));
        Assert.Equal(176, servis.FondSati(2026, 3));
    }

    [Fact]
    public void FondSati_PraznikURadnomDanu_SmanjujeFond()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);
        servis.ObezbediGodinu(2026);

        int bezDodatnog = servis.RadniDani(2026, 3);

        db.Praznici.Add(new Praznik { Datum = new DateTime(2026, 3, 10), Naziv = "Test", RucniUnos = true });
        db.SaveChanges();

        Assert.Equal(DayOfWeek.Tuesday, new DateTime(2026, 3, 10).DayOfWeek);
        Assert.Equal(bezDodatnog - 1, servis.RadniDani(2026, 3));
    }

    /// <summary>Praznik koji padne u vikend se ne sme oduzeti dvaput.</summary>
    [Fact]
    public void FondSati_PraznikUVikendu_NeSmanjujeFond()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);
        servis.ObezbediGodinu(2026);

        int pre = servis.RadniDani(2026, 3);

        // 14. mart 2026. je subota.
        Assert.Equal(DayOfWeek.Saturday, new DateTime(2026, 3, 14).DayOfWeek);
        db.Praznici.Add(new Praznik { Datum = new DateTime(2026, 3, 14), Naziv = "Test", RucniUnos = true });
        db.SaveChanges();

        Assert.Equal(pre, servis.RadniDani(2026, 3));
    }

    /// <summary>Dan označen kao radni ne ulazi u umanjenje fonda.</summary>
    [Fact]
    public void FondSati_PraznikOznacenKaoRadni_NeSmanjujeFond()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);
        servis.ObezbediGodinu(2026);

        int pre = servis.RadniDani(2026, 3);

        db.Praznici.Add(new Praznik
        {
            Datum = new DateTime(2026, 3, 10),
            Naziv = "Obeležava se, ali se radi",
            Neradni = false,
            RucniUnos = true
        });
        db.SaveChanges();

        Assert.Equal(pre, servis.RadniDani(2026, 3));
    }

    /// <summary>Januar nosi Novu godinu i Božić, pa ima osetno manji fond od marta.</summary>
    [Fact]
    public void FondSati_Januar2026_UzimaUObzirNovuGodinuIBozic()
    {
        using var db = NoviKontekst();
        var servis = new PraznikService(db);
        servis.ObezbediGodinu(2026);

        // Januar 2026: 22 radna dana po kalendaru, minus 1. i 2. (čet/pet) i 7. (sreda).
        Assert.Equal(19, servis.RadniDani(2026, 1));
        Assert.Equal(152, servis.FondSati(2026, 1));
    }
}
