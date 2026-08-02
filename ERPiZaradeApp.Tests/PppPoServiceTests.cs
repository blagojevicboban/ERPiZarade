using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// PPP-PO je potvrda koju radnik dobija na ruke, pa mora da se slaže sa onim što je
/// prijavljeno Poreskoj upravi. Testovi drže kriterijum iz razvojne mape: obrazac sadrži
/// sve isplate po radniku i slaže se sa zbirom PPP-PD prijava.
/// </summary>
public class PppPoServiceTests
{
    private const int Godina = 2026;

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlataDbContext(options);
    }

    /// <summary>
    /// Radnik je periodičan zapis — jedan red po mesecu. Test pravi karton i obračun za
    /// svaki traženi mesec, kao što to radi i sama aplikacija.
    /// </summary>
    private static void DodajMesece(
        PlataDbContext db,
        int brojRadnika,
        IEnumerable<int> meseci,
        decimal bruto = 80000m,
        decimal porez = 5000m,
        decimal doprinosiRadnik = 12000m,
        string radnoMesto = "101101000",
        decimal brutoBolovanje = 0m)
    {
        foreach (int mesec in meseci)
        {
            // Id mora biti jedinstven i kad se metoda pozove više puta za istog radnika
            // (npr. da bi mu se dodao mesec sa drugom vrstom prihoda).
            int id = brojRadnika * 100 + mesec;

            db.Radnici.Add(new Radnik
            {
                Id = id,
                BrojRadnika = brojRadnika,
                ImeIPrezime = $"Radnik {brojRadnika}",
                Jmbg = "0101990710016",
                Radno_Mesto = radnoMesto,
                Godina = Godina,
                Mesec = mesec
            });

            db.ObracuniPlata.Add(new ObracunPlate
            {
                Id = id,
                RadnikId = id,
                Godina = Godina,
                Mesec = mesec,
                BrutoZarada = bruto,
                BrutoBolovanje = brutoBolovanje,
                PorezNaDohodak = porez,
                DoprinosPioRadnik = doprinosiRadnik,
                DoprinosPioPoslodavac = 8000m
            });
        }

        db.SaveChanges();
    }

    private static PppPoRezultat Pripremi(PlataDbContext db) => new PppPoService(db).Pripremi(Godina);

    [Fact]
    public void Pripremi_ZbiraSveMeseceJednogRadnika()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1, 2, 3]);

        var obrazac = Assert.Single(Pripremi(db).Obrasci);

        Assert.Equal(3, obrazac.BrojMeseci);
        Assert.Equal(240000m, obrazac.UkupnoBruto);
        Assert.Equal(15000m, obrazac.UkupnoPorez);
        Assert.Equal(36000m, obrazac.UkupnoDoprinosi);
    }

    /// <summary>Radnik ima jedan red po periodu, ali dobija jednu potvrdu za celu godinu.</summary>
    [Fact]
    public void Pripremi_JedanObrazacPoRadnikuBezObziraNaBrojPerioda()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1, 2, 3, 4, 5, 6]);
        DodajMesece(db, 2, [1, 2]);

        var rezultat = Pripremi(db);

        Assert.Equal(2, rezultat.Obrasci.Count);
        Assert.Equal(6, rezultat.Obrasci.Single(o => o.Radnik.BrojRadnika == 1).BrojMeseci);
        Assert.Equal(2, rezultat.Obrasci.Single(o => o.Radnik.BrojRadnika == 2).BrojMeseci);
    }

    /// <summary>Različite vrste prihoda idu u odvojene redove obrasca.</summary>
    [Fact]
    public void Pripremi_RazliciteVrstePrihoda_DajuOdvojeneRedove()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1]);
        // Bolovanje veće od zarade menja SVP na 109101000.
        DodajMesece(db, 1, [2], bruto: 10000m, brutoBolovanje: 50000m);

        var obrazac = Assert.Single(Pripremi(db).Obrasci);

        Assert.Equal(2, obrazac.Redovi.Count);
        Assert.Contains(obrazac.Redovi, r => r.Svp == SvpService.RedovnaZarada);
        Assert.Contains(obrazac.Redovi, r => r.Svp == SvpService.Bolovanje);
    }

    [Fact]
    public void Pripremi_ZbirRedova_JednakUkupnomIznosu()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1]);
        DodajMesece(db, 1, [2], bruto: 10000m, brutoBolovanje: 50000m);

        var obrazac = Assert.Single(Pripremi(db).Obrasci);

        Assert.Equal(obrazac.Redovi.Sum(r => r.BrutoPrihod), obrazac.UkupnoBruto);
        Assert.Equal(obrazac.Redovi.Sum(r => r.Porez), obrazac.UkupnoPorez);
    }

    [Fact]
    public void Pripremi_RadnikBezJmbg_JeGreska()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1]);
        foreach (var r in db.Radnici) r.Jmbg = "";
        db.SaveChanges();

        var rezultat = Pripremi(db);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Nedostaje JMBG");
    }

    [Fact]
    public void Pripremi_GodinaBezObracuna_JeGreska()
    {
        using var db = NoviKontekst();

        var rezultat = Pripremi(db);

        Assert.Empty(rezultat.Obrasci);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Nema obračuna");
    }

    // ── Slaganje sa PPP-PD prijavama ─────────────────────────────────

    /// <summary>Za jedan obračun: porez 5.000 + doprinosi radnika 12.000 + poslodavca 8.000 = 25.000.</summary>
    private const decimal UkupnoPoObracunu = 25000m;

    [Fact]
    public void Pripremi_ZbirSeSlazeSaPrijavama_NemaNalaza()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1]);
        db.PppPdPrijave.Add(new PppPdPrijava
        {
            Godina = Godina,
            Mesec = 1,
            Bop = "97A",
            IznosZaUplatu = UkupnoPoObracunu,
            Status = StatusPrijave.Prihvacena
        });
        db.SaveChanges();

        var rezultat = Pripremi(db);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Ne slaže se sa PPP-PD prijavama");
    }

    /// <summary>
    /// Razlika znači da je obračun izmenjen posle podnošenja prijave — potvrda bi radniku
    /// govorila jedno, a Poreska uprava imala drugo.
    /// </summary>
    [Fact]
    public void Pripremi_ZbirSeNeSlazeSaPrijavama_JeGreska()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1]);
        db.PppPdPrijave.Add(new PppPdPrijava
        {
            Godina = Godina,
            Mesec = 1,
            Bop = "97A",
            IznosZaUplatu = UkupnoPoObracunu + 100m,
            Status = StatusPrijave.Prihvacena
        });
        db.SaveChanges();

        var rezultat = Pripremi(db);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Ne slaže se sa PPP-PD prijavama");
    }

    /// <summary>Poređenje obuhvata samo mesece za koje prijava postoji.</summary>
    [Fact]
    public void Pripremi_MesecBezPrijave_NeUlaziUPoredjenje()
    {
        using var db = NoviKontekst();
        DodajMesece(db, 1, [1, 2]);
        db.PppPdPrijave.Add(new PppPdPrijava
        {
            Godina = Godina,
            Mesec = 1,
            Bop = "97A",
            IznosZaUplatu = UkupnoPoObracunu,
            Status = StatusPrijave.Prihvacena
        });
        db.SaveChanges();

        var rezultat = Pripremi(db);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Ne slaže se sa PPP-PD prijavama");
    }
}

/// <summary>
/// Određivanje SVP šifre je ranije stajalo u tri kopije koje su se već razišle. Testovi
/// fiksiraju jedno ponašanje za sve — prijavu, ekran i godišnju potvrdu.
/// </summary>
public class SvpServiceTests
{
    private static ObracunPlate Obracun(string radnoMesto, decimal zarada = 80000m, decimal bolovanje = 0m)
        => new()
        {
            BrutoZarada = zarada,
            BrutoBolovanje = bolovanje,
            Radnik = new Radnik { Radno_Mesto = radnoMesto }
        };

    [Fact]
    public void Odredi_DevetocifrenaSifra_SeKoristiKakoJeUneta()
    {
        Assert.Equal("101104000", SvpService.Odredi(Obracun("101104000")));
    }

    [Fact]
    public void Odredi_OpisPosla_DajeRedovnuZaradu()
    {
        Assert.Equal(SvpService.RedovnaZarada, SvpService.Odredi(Obracun("Rukovodilac službe")));
    }

    /// <summary>Bolovanje veće od zarade menja vrstu prihoda bez obzira na šifru u kartonu.</summary>
    [Fact]
    public void Odredi_BolovanjeVeceOdZarade_DajeSifruBolovanja()
    {
        Assert.Equal(SvpService.Bolovanje, SvpService.Odredi(Obracun("101101000", zarada: 10000m, bolovanje: 50000m)));
    }

    [Fact]
    public void Odredi_PraznoRadnoMesto_DajeRedovnuZaradu()
    {
        Assert.Equal(SvpService.RedovnaZarada, SvpService.Odredi(Obracun("")));
    }

    [Fact]
    public void JeSvpSifra_PrepoznajeSamoDevetCifara()
    {
        Assert.True(SvpService.JeSvpSifra("101101000"));
        Assert.False(SvpService.JeSvpSifra("10110100"));
        Assert.False(SvpService.JeSvpSifra("10110100A"));
        Assert.False(SvpService.JeSvpSifra(""));
    }
}
