using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Arhiva prethodnih verzija obračuna (Faza 2.7). Prekalkulacija briše zatečeni rezultat;
/// testovi drže da se pre brisanja sačuva ono što je potrebno da se posle utvrdi šta se
/// promenilo i za koliko.
/// </summary>
public class VerzijeObracunaTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PlataDbContext(options);
    }

    private static ObracunPlate DodajObracun(PlataDbContext db, int id, decimal neto = 50000m, int verzija = 1)
    {
        var radnik = new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Jmbg = "0101990710016",
            Godina = Godina,
            Mesec = Mesec
        };
        db.Radnici.Add(radnik);

        var obracun = new ObracunPlate
        {
            Id = id,
            RadnikId = id,
            Radnik = radnik,
            Godina = Godina,
            Mesec = Mesec,
            Verzija = verzija,
            BrutoZarada = 80000m,
            BrutoBolovanje = 5000m,
            NetoIsplata = neto,
            PorezNaDohodak = 5000m,
            DoprinosPioRadnik = 2500m,
            DoprinosZdravstvoRadnik = 2500m,
            DoprinosPioPoslodavac = 2500m,
            NetoZar = 80000m
        };

        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    [Fact]
    public void Arhiviraj_UpisujeIznoseIPodatkeORadniku()
    {
        using var db = NoviKontekst();
        var o = DodajObracun(db, 1);

        int upisano = VerzijeObracunaService.Arhiviraj(db, [o], "Prekalkulacija perioda 03/2026");
        db.SaveChanges();

        Assert.Equal(1, upisano);

        var v = db.ObracunVerzije.Single();
        Assert.Equal(Godina, v.Godina);
        Assert.Equal(Mesec, v.Mesec);
        Assert.Equal(1, v.BrojRadnika);
        Assert.Equal("Radnik 1", v.ImeRadnika);
        Assert.Equal(1, v.Verzija);
        Assert.Equal(85000m, v.Bruto);          // BrutoZarada + BrutoBolovanje
        Assert.Equal(5000m, v.PorezNaDohodak);
        Assert.Equal(5000m, v.DoprinosiRadnik); // PIO + zdravstvo na teret radnika
        Assert.Equal(50000m, v.NetoIsplata);
        Assert.Contains("Prekalkulacija", v.Razlog);
    }

    [Fact]
    public void Arhiviraj_SnimakSadrziIKoloneKojeNijedanIzvestajNePrikazuje()
    {
        using var db = NoviKontekst();
        var o = DodajObracun(db, 1);

        VerzijeObracunaService.Arhiviraj(db, [o], "Prekalkulacija");
        db.SaveChanges();

        var snimak = db.ObracunVerzije.Single().Snimak;
        var vraceno = JsonSerializer.Deserialize<ObracunPlate>(snimak);

        Assert.NotNull(vraceno);
        Assert.Equal(80000m, vraceno!.NetoZar); // legacy kolona iz DBF-a
        Assert.Equal(50000m, vraceno.NetoIsplata);
    }

    /// <summary>
    /// Navigacija na radnika je ciklična i vodila bi u beskonačnu serijalizaciju; snimak
    /// mora da prođe i da je ne povuče, jer se radnik ionako čuva u svojoj tabeli.
    /// </summary>
    [Fact]
    public void Arhiviraj_NeSerijalizujeRadnikaAliGaNiNeGubi()
    {
        using var db = NoviKontekst();
        var o = DodajObracun(db, 1);

        VerzijeObracunaService.Arhiviraj(db, [o], "Prekalkulacija");
        db.SaveChanges();

        Assert.DoesNotContain("\"ImeIPrezime\"", db.ObracunVerzije.Single().Snimak);

        // Obračun u memoriji ostaje netaknut — snimanje ga ne sme osiromašiti.
        Assert.NotNull(o.Radnik);
        Assert.Equal("Radnik 1", o.Radnik.ImeIPrezime);
    }

    [Fact]
    public void SledecaVerzija_BezArhive_JeJedan()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        Assert.Equal(1, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 1));
    }

    [Fact]
    public void SledecaVerzija_RasteSaSvakimArhiviranjem()
    {
        using var db = NoviKontekst();
        var prva = DodajObracun(db, 1);

        VerzijeObracunaService.Arhiviraj(db, [prva], "Prva prekalkulacija");
        db.SaveChanges();
        Assert.Equal(2, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 1));

        prva.Verzija = 2;
        VerzijeObracunaService.Arhiviraj(db, [prva], "Druga prekalkulacija");
        db.SaveChanges();
        Assert.Equal(3, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 1));

        // Drugi radnik ima svoj brojač — verzija je svojstvo obračuna, ne perioda.
        Assert.Equal(1, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 2));
    }

    [Fact]
    public void Arhiviraj_BeleziDaLiJeVerzijaBilaZakljucanaIStornirana()
    {
        using var db = NoviKontekst();
        var o = DodajObracun(db, 1);
        o.Zakljucan = true;
        o.Storniran = true;
        db.SaveChanges();

        VerzijeObracunaService.Arhiviraj(db, [o], "Prekalkulacija posle storna");
        db.SaveChanges();

        var v = db.ObracunVerzije.Single();
        Assert.True(v.BioZakljucan);
        Assert.True(v.BioStorniran);
    }
}
