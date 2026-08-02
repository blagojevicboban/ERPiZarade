using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Pre-flight provere su poslednja tačka na kojoj je ispravka jeftina — posle njih idu
/// PPP-PD prijava i nalozi za prenos, gde ispravka znači izmenjenu prijavu i storniranje.
/// Testovi fiksiraju šta se smatra greškom (blokira zaključavanje), a šta upozorenjem.
/// </summary>
public class PreFlightServiceTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;
    private const decimal NajnizaOsnovica = 45000m;

    /// <summary>JMBG rođenog 01.01.1990. sa ispravnom kontrolnom cifrom po modulu 11.</summary>
    private const string IspravanJmbg = "0101990710016";

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);
        db.Doprinosi.Add(new Doprinos
        {
            Godina = Godina,
            Mesec = Mesec,
            RedniBroj = 1,
            NajnizaOsnovica = NajnizaOsnovica,
            NajvisaOsnovica = 600000m
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>Radnik i obračun koji prolaze sve provere — polazna tačka svakog scenarija.</summary>
    private static ObracunPlate DodajIspravanObracun(
        PlataDbContext db,
        int id = 1,
        string jmbg = IspravanJmbg,
        Action<Radnik>? podesiRadnika = null,
        Action<ObracunPlate>? podesiObracun = null)
    {
        var radnik = new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Jmbg = jmbg,
            BankovniRacun = "160-1234567890-12",
            Email = $"radnik{id}@firma.rs",
            Godina = Godina,
            Mesec = Mesec,
            Aktivan = true
        };
        podesiRadnika?.Invoke(radnik);
        db.Radnici.Add(radnik);

        var obracun = new ObracunPlate
        {
            Id = id,
            RadnikId = radnik.Id,
            Godina = Godina,
            Mesec = Mesec,
            BrutoZarada = 80000m,
            NetoIsplata = 55000m,
            RedovniSati = 176,
            FondSatiMesecni = 176m
        };
        podesiObracun?.Invoke(obracun);
        db.ObracuniPlata.Add(obracun);

        db.SaveChanges();
        return obracun;
    }

    private static RezultatProvere Proveri(PlataDbContext db)
        => new PreFlightService(db).Proveri(Godina, Mesec);

    [Fact]
    public void Proveri_IspravanObracun_NemaNalaza()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db);

        var rezultat = Proveri(db);

        Assert.True(rezultat.JeCist);
        Assert.True(rezultat.SmeSeZakljucati);
        Assert.Equal(1, rezultat.BrojObracuna);
    }

    [Fact]
    public void Proveri_PrazanPeriod_JeGreska()
    {
        using var db = NoviKontekst();

        var rezultat = Proveri(db);

        Assert.False(rezultat.SmeSeZakljucati);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Prazan period");
    }

    [Fact]
    public void Proveri_NegativanNeto_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiObracun: o => o.NetoIsplata = -1200m);

        var rezultat = Proveri(db);

        Assert.False(rezultat.SmeSeZakljucati);
        Assert.Contains(rezultat.Nalazi,
            n => n.Provera == "Negativan neto" && n.Tezina == TezinaNalaza.Greska);
    }

    [Fact]
    public void Proveri_BrutoIspodNajnizeOsnovice_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiObracun: o => o.BrutoZarada = NajnizaOsnovica - 1m);

        var rezultat = Proveri(db);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Bruto ispod najniže osnovice");
    }

    [Fact]
    public void Proveri_BrutoTacnoNaNajnizojOsnovici_Prolazi()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiObracun: o => o.BrutoZarada = NajnizaOsnovica);

        var rezultat = Proveri(db);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Bruto ispod najniže osnovice");
    }

    [Fact]
    public void Proveri_NedostajeJmbg_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiRadnika: r => r.Jmbg = "");

        var rezultat = Proveri(db);

        Assert.False(rezultat.SmeSeZakljucati);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Nedostaje JMBG");
    }

    [Fact]
    public void Proveri_NeispravanJmbg_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiRadnika: r => r.Jmbg = "1234567890123");

        var rezultat = Proveri(db);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Neispravan JMBG");
    }

    [Fact]
    public void Proveri_NedostajeTekuciRacun_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiRadnika: r => r.BankovniRacun = "");

        var rezultat = Proveri(db);

        Assert.False(rezultat.SmeSeZakljucati);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Nedostaje tekući račun");
    }

    /// <summary>
    /// E-mail je uslov samo za slanje listića, ne i za ispravnost obračuna — zato ne sme
    /// da blokira zaključavanje.
    /// </summary>
    [Fact]
    public void Proveri_NedostajeEmail_JeSamoUpozorenje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiRadnika: r => r.Email = "");

        var rezultat = Proveri(db);

        Assert.True(rezultat.SmeSeZakljucati);
        Assert.Contains(rezultat.Nalazi,
            n => n.Provera == "Nedostaje e-mail" && n.Tezina == TezinaNalaza.Upozorenje);
    }

    [Fact]
    public void Proveri_SatiPrekoFonda_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiObracun: o => o.RedovniSati = 200);

        var rezultat = Proveri(db);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Sati veći od fonda");
    }

    /// <summary>Prekovremeni rad je po definiciji preko fonda i ne sme da se prijavi kao greška.</summary>
    [Fact]
    public void Proveri_PrekovremeniSatiPrekoFonda_NisuGreska()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiObracun: o => o.PrekovremeneSati = 24);

        var rezultat = Proveri(db);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Sati veći od fonda");
    }

    [Fact]
    public void Proveri_IsteklaOlaksicaKojaSeJosPrimenjuje_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiRadnika: r =>
        {
            r.ProcenatPovracajaPoreza = 70m;
            r.OlaksicaVaziDo = new DateTime(Godina, Mesec - 1, 28);
        });

        var rezultat = Proveri(db);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Istekla poreska olakšica");
    }

    [Fact]
    public void Proveri_VazecaOlaksica_Prolazi()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiRadnika: r =>
        {
            r.ProcenatPovracajaPoreza = 70m;
            r.OlaksicaVaziDo = new DateTime(Godina, 12, 31);
        });

        var rezultat = Proveri(db);

        Assert.True(rezultat.JeCist);
    }

    /// <summary>
    /// Dva obračuna za isti JMBG daju dva reda za isto lice u PPP-PD prijavi, što Poreska
    /// uprava odbija — a bez ove provere se otkriva tek pri podnošenju.
    /// </summary>
    [Fact]
    public void Proveri_DvaObracunaZaIstiJmbg_BlokiraZakljucavanje()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, id: 1);
        DodajIspravanObracun(db, id: 2, jmbg: IspravanJmbg);

        var rezultat = Proveri(db);

        Assert.False(rezultat.SmeSeZakljucati);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Dupli obračun");
    }

    [Fact]
    public void Proveri_ObracuniDrugogPerioda_SeNeUzimajuUObzir()
    {
        using var db = NoviKontekst();
        DodajIspravanObracun(db, podesiObracun: o => o.Mesec = Mesec + 1);

        var rezultat = Proveri(db);

        Assert.Equal(0, rezultat.BrojObracuna);
        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Prazan period");
    }
}
