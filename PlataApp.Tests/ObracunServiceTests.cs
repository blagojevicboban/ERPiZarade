using Microsoft.EntityFrameworkCore;
using PlataApp.Services;
using PlataData;
using PlataData.Models;

namespace PlataApp.Tests;

/// <summary>
/// Obračun zarada je matematički najrizičniji deo sistema — greška ovde ide direktno
/// u isplatu radnika i u PPP-PD prijavu Poreskoj upravi. Testovi fiksiraju zakonska
/// pravila (Zakon o radu čl. 108, poresko oslobođenje, najniža osnovica doprinosa).
///
/// Svi scenariji koriste fond od 176 časova i vrednost boda 10.000 tako da cena sata
/// ispada zaokružena: Koeficijent 7,04 → 400 RSD/h; Koeficijent 3,52 → 200 RSD/h.
/// </summary>
public class ObracunServiceTests
{
    private const int Fond = 176;
    private const decimal VrednostBoda = 10000m;
    private const int Godina = 2026;
    private const int Mesec = 3;

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);

        // Eksplicitni parametri perioda — da testovi ne zavise od hardkodovanih podrazumevanih vrednosti.
        db.Porezi.Add(new Porezi
        {
            Godina = Godina,
            Mesec = Mesec,
            RedniBroj = 1,
            ProcMinul = 0.40m,   // 0,4% po godini staža
            ProcPreko = 26.00m,
            ProcNocni = 26.00m,
            ProcDrzav = 110.00m,
            ProcBolov = 65.00m,
            ProcNedel = 0.00m,
            AkPorez = 10.00m,    // stopa poreza 10%
            Prvast = 28423.00m   // poresko oslobođenje
        });

        db.SaveChanges();
        return db;
    }

    private static Radnik DodajRadnika(
        PlataDbContext db,
        decimal koeficijent = 7.04m,
        int minuliRadGodine = 0,
        string kategorija = "",
        string radnoMesto = "101101000")
    {
        var radnik = new Radnik
        {
            Id = 1,
            BrojRadnika = 1,
            ImeIPrezime = "Petar Petrović",
            Koeficijent = koeficijent,
            MinuliRadGodine = minuliRadGodine,
            Kategorija = kategorija,
            Radno_Mesto = radnoMesto,
            Godina = Godina,
            Mesec = Mesec
        };

        db.Radnici.Add(radnik);
        db.SaveChanges();
        return radnik;
    }

    private static RadniSat Sati(int redovni = Fond, int prekovremeni = 0) => new()
    {
        RadnikId = 1,
        Godina = Godina,
        Mesec = Mesec,
        RedovniSati = redovni,
        PrekovremeneSati = prekovremeni
    };

    // ── Osnovni obračun ──────────────────────────────────────────────────

    [Fact]
    public void Calculate_PunFond_RacunaBrutoIzKoeficijenta()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        // 176 h × 400 RSD/h = 70.400
        Assert.Equal(400m, o.CenaSataRedovan);
        Assert.Equal(70400m, o.NetoZar);
        Assert.Equal(70400m, o.Neto);
    }

    /// <summary>
    /// Zakon o radu čl. 108: minuli rad se obračunava isključivo na osnovnu zaradu.
    /// Prekovremeni, noćni i praznični sati NE ulaze u osnov.
    /// </summary>
    [Fact]
    public void Calculate_MinuliRad_RacunaSeSamoNaOsnovnuZaradu()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, minuliRadGodine: 10);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(redovni: 100, prekovremeni: 76), Godina, Mesec, VrednostBoda, Fond);

        // Osnov = 100 h × 400 = 40.000; minuli = 40.000 × 0,4% × 10 = 1.600.
        // Da su prekovremeni ušli u osnov, bilo bi 2.816.
        Assert.Equal(1600m, o.BrutoMinuliRad);
        Assert.Equal(10, o.MinuliRadGodine);
    }

    // ── Porez ────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_PoreskoOslobodjenje_SrazmernoOdradjenimSatima()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        var service = new ObracunService(db);

        // Pola meseca: 88 od 176 sati → oslobođenje se prepolovljava.
        var o = service.Calculate(radnik, Sati(redovni: 88), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(14211.50m, o.LicniOdbitak);
        Assert.Equal(35200m - 14211.50m, o.PoreskaOsnovica);
        Assert.Equal(Math.Round((35200m - 14211.50m) * 0.10m, 2), o.PorezNaDohodak);
    }

    /// <summary>
    /// Poreska osnovica je ograničena na nulu — kada je bruto ispod oslobođenja,
    /// razlika se ne sme preneti kao negativan porez.
    /// </summary>
    [Fact]
    public void Calculate_BrutoIspodOslobodjenja_PoreskaOsnovicaJeNula()
    {
        using var db = NoviKontekst();
        // 1,76 → 100 RSD/h; pun fond od 176 h → 17.600 bruto, ispod oslobođenja od 28.423.
        var radnik = DodajRadnika(db, koeficijent: 1.76m);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(17600m, o.Neto);
        Assert.Equal(28423m, o.LicniOdbitak);
        Assert.Equal(0m, o.PoreskaOsnovica);
        Assert.Equal(0m, o.PorezNaDohodak);
    }

    // ── Doprinosi ────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_PodrazumevaneStope_RacunaDoprinoseRadnika()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        // Bruto 70.400 je iznad najniže osnovice (51.297), pa se doprinosi računaju na pun bruto.
        Assert.Equal(70400m, o.BrutoOsnovica);
        Assert.Equal(Math.Round(70400m * 0.1400m, 2), o.DoprinosPioRadnik);
        Assert.Equal(Math.Round(70400m * 0.0515m, 2), o.DoprinosZdravstvoRadnik);
        Assert.Equal(Math.Round(70400m * 0.0075m, 2), o.DoprinosNezaposlenostRadnik);
    }

    [Fact]
    public void Calculate_StopeIzBaze_ImajuPrednostNadPodrazumevanim()
    {
        using var db = NoviKontekst();
        db.Doprinosi.AddRange(
            new Doprinos { Godina = Godina, Mesec = Mesec, RedniBroj = 1, Naziv = "PIO", ProcRadn = 15.00m, ProcPosl = 11.00m },
            new Doprinos { Godina = Godina, Mesec = Mesec, RedniBroj = 2, Naziv = "Zdravstvo", ProcRadn = 6.00m, ProcPosl = 6.00m },
            new Doprinos { Godina = Godina, Mesec = Mesec, RedniBroj = 3, Naziv = "Nezaposlenost", ProcRadn = 1.00m, ProcPosl = 0.50m });
        db.SaveChanges();

        var radnik = DodajRadnika(db);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(Math.Round(70400m * 0.15m, 2), o.DoprinosPioRadnik);
        Assert.Equal(Math.Round(70400m * 0.06m, 2), o.DoprinosZdravstvoRadnik);
        Assert.Equal(Math.Round(70400m * 0.01m, 2), o.DoprinosNezaposlenostRadnik);
        Assert.Equal(Math.Round(70400m * 0.11m, 2), o.DoprinosPioPoslodavac);
    }

    /// <summary>
    /// Zaposleni penzioner (šifra radnog mesta počinje sa "109") ne plaća doprinos
    /// za osiguranje od nezaposlenosti — ni radnik ni poslodavac.
    /// </summary>
    [Fact]
    public void Calculate_Penzioner_NemaDoprinosZaNezaposlenost()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, radnoMesto: "109101000");
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(0m, o.DoprinosNezaposlenostRadnik);
        Assert.Equal(0m, o.DoprinosNezaposlenostPoslodavac);
        // Ostali doprinosi se i dalje obračunavaju normalno.
        Assert.True(o.DoprinosPioRadnik > 0m);
    }

    // ── Najniža osnovica doprinosa ───────────────────────────────────────

    [Fact]
    public void Calculate_ZaradaIspodNajnizeOsnovice_PodizeOsnovicuDoprinosa()
    {
        using var db = NoviKontekst();
        // 200 RSD/h × 176 h = 35.200 bruto, ispod najniže osnovice od 51.297.
        var radnik = DodajRadnika(db, koeficijent: 3.52m);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(35200m, o.Neto);
        Assert.Equal(ObracunService.DefaultMinContributionBase, o.BrutoOsnovica);
        // Doprinos se računa na podignutu osnovicu, ne na stvarni bruto.
        Assert.Equal(Math.Round(ObracunService.DefaultMinContributionBase * 0.1400m, 2), o.DoprinosPioRadnik);
    }

    [Fact]
    public void Calculate_Kategorija9_NePodizeOsnovicuNaMinimalnu()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, koeficijent: 3.52m, kategorija: "9");
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        // Kategorija 9 je izuzeta od minimalne osnovice — doprinosi idu na stvarni bruto.
        Assert.Equal(35200m, o.BrutoOsnovica);
        Assert.Equal(Math.Round(35200m * 0.1400m, 2), o.DoprinosPioRadnik);
    }

    // ── Obustave i neto isplata ──────────────────────────────────────────

    [Fact]
    public void Calculate_KreditnaRata_OgranicenaNaOstatakDuga()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        db.Krediti.Add(new Kredit
        {
            RadnikId = radnik.Id,
            Opis = "Poslednja rata",
            MesecnaRata = 10000m,
            OstatakDuga = 3000m,
            DatumPocetka = new DateTime(Godina, 1, 1),
            Aktivan = true
        });
        db.SaveChanges();

        var service = new ObracunService(db);
        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        // Ne sme se obustaviti više nego što radnik duguje.
        Assert.Equal(3000m, o.KreditObustava);
    }

    [Fact]
    public void Calculate_NeaktivanKredit_SeNeObustavlja()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        db.Krediti.Add(new Kredit
        {
            RadnikId = radnik.Id,
            MesecnaRata = 5000m,
            OstatakDuga = 50000m,
            DatumPocetka = new DateTime(Godina, 1, 1),
            Aktivan = false
        });
        db.SaveChanges();

        var service = new ObracunService(db);
        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(0m, o.KreditObustava);
    }

    [Fact]
    public void Calculate_NetoIsplata_JednakaBrutoMinusOdbici()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        var service = new ObracunService(db);

        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        decimal ocekivano = Math.Round(
            70400m
            - (70400m * 0.1400m)
            - (70400m * 0.0515m)
            - (70400m * 0.0075m)
            - ((70400m - 28423m) * 0.10m), 2);

        Assert.Equal(ocekivano, o.NetoIsplata);
    }

    [Fact]
    public void Calculate_ObustaveVeceOdZarade_NetoIsplataNijeNegativna()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);
        db.Krediti.Add(new Kredit
        {
            RadnikId = radnik.Id,
            MesecnaRata = 500000m,
            OstatakDuga = 500000m,
            DatumPocetka = new DateTime(Godina, 1, 1),
            Aktivan = true
        });
        db.SaveChanges();

        var service = new ObracunService(db);
        var o = service.Calculate(radnik, Sati(), Godina, Mesec, VrednostBoda, Fond);

        Assert.Equal(0m, o.NetoIsplata);
    }
}
