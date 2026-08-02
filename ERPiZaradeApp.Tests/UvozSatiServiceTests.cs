using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Uvoz sati mora da zadovolji dva pravila iz razvojne mape: fajl sa greškama se odbija sa
/// spiskom redova, a uspešan uvoz daje isti rezultat kao ručni unos. Delimično uvezeni sati
/// izgledaju kao uspeh, a daju pogrešan obračun radnicima iz neuvezenog dela.
/// </summary>
public class UvozSatiServiceTests : IDisposable
{
    private const int Godina = 2026;
    private const int Mesec = 3;

    private readonly string _dir;

    public UvozSatiServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "uvoz_sati_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static PlataDbContext NoviKontekst(params int[] brojeviRadnika)
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);
        foreach (int broj in brojeviRadnika)
        {
            db.Radnici.Add(new Radnik
            {
                Id = broj,
                BrojRadnika = broj,
                ImeIPrezime = $"Radnik {broj}",
                Godina = Godina,
                Mesec = Mesec,
                Aktivan = true
            });
        }
        db.SaveChanges();
        return db;
    }

    private string NapraviCsv(string sadrzaj)
    {
        string putanja = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(putanja, sadrzaj, Encoding.UTF8);
        return putanja;
    }

    private RezultatUvoza Procitaj(PlataDbContext db, string csv)
        => new UvozSatiService(db).Procitaj(NapraviCsv(csv), Godina, Mesec);

    [Fact]
    public void Procitaj_IspravanFajl_DajeRedoveBezGresaka()
    {
        using var db = NoviKontekst(1, 2);

        var rezultat = Procitaj(db, """
            Broj radnika;Redovni sati;Prekovremeni
            1;176;8
            2;160;0
            """);

        Assert.True(rezultat.JeIspravan);
        Assert.Equal(2, rezultat.Redovi.Count);
        Assert.Equal(176, rezultat.Redovi[0].RedovniSati);
        Assert.Equal(8, rezultat.Redovi[0].PrekovremeneSati);
    }

    /// <summary>Uvoz mora da da isti zapis kao ručni unos — to je kriterijum „gotovo".</summary>
    [Fact]
    public void Primeni_UpisujeSateIstoKaoRucniUnos()
    {
        using var db = NoviKontekst(1);
        var servis = new UvozSatiService(db);

        var rezultat = servis.Procitaj(NapraviCsv("""
            Broj radnika;Redovni sati;Noćni rad;Regres
            1;176;12;5000,50
            """), Godina, Mesec);

        int upisano = servis.Primeni(rezultat, Godina, Mesec);

        Assert.Equal(1, upisano);
        var sati = db.RadniSati.Single();
        Assert.Equal(176, sati.RedovniSati);
        Assert.Equal(12, sati.NocniSati);
        Assert.Equal(5000.50m, sati.RegresIznos);
    }

    [Fact]
    public void Primeni_ZamenjujePostojeceSateZaIstiPeriod()
    {
        using var db = NoviKontekst(1);
        db.RadniSati.Add(new RadniSat { RadnikId = 1, Godina = Godina, Mesec = Mesec, RedovniSati = 100 });
        db.SaveChanges();

        var servis = new UvozSatiService(db);
        var rezultat = servis.Procitaj(NapraviCsv("Broj radnika;Redovni sati\n1;176"), Godina, Mesec);
        servis.Primeni(rezultat, Godina, Mesec);

        Assert.Equal(176, db.RadniSati.Single().RedovniSati);
    }

    [Fact]
    public void Procitaj_NepostojeciRadnik_JeGreskaSaBrojemReda()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, """
            Broj radnika;Redovni sati
            1;176
            99;160
            """);

        Assert.False(rezultat.JeIspravan);
        var greska = Assert.Single(rezultat.Greske);
        Assert.Equal(3, greska.Red);
        Assert.Contains("99", greska.Opis);
    }

    [Fact]
    public void Procitaj_NeispravanBroj_JeGreskaSaNazivomKolone()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, """
            Broj radnika;Redovni sati
            1;sto sedamdeset
            """);

        var greska = Assert.Single(rezultat.Greske);
        Assert.Equal(2, greska.Red);
        Assert.Equal("Redovni sati", greska.Kolona);
    }

    [Fact]
    public void Procitaj_NegativnaVrednost_JeGreska()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Broj radnika;Redovni sati\n1;-8");

        Assert.False(rezultat.JeIspravan);
        Assert.Contains(rezultat.Greske, g => g.Opis.Contains("negativna"));
    }

    /// <summary>Decimalni sati su najčešće greška u kucanju, a tiho zaokruživanje je menja u tuđu platu.</summary>
    [Fact]
    public void Procitaj_DecimalniSati_JeGreska()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Broj radnika;Redovni sati\n1;176,5");

        Assert.Contains(rezultat.Greske, g => g.Kolona == "Redovni sati" && g.Opis.Contains("ceo broj"));
    }

    [Fact]
    public void Procitaj_DecimalniIznos_JeDozvoljen()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Broj radnika;Regres\n1;5000,50");

        Assert.True(rezultat.JeIspravan);
        Assert.Equal(5000.50m, rezultat.Redovi[0].RegresIznos);
    }

    /// <summary>Tuđa tabela često koristi tačku kao decimalni razdvajač.</summary>
    [Fact]
    public void Procitaj_TackaKaoDecimalniRazdvajac_SeTumaciIsto()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Broj radnika,Regres\n1,5000.50");

        Assert.True(rezultat.JeIspravan);
        Assert.Equal(5000.50m, rezultat.Redovi[0].RegresIznos);
    }

    [Fact]
    public void Procitaj_IstiRadnikDvaPuta_JeGreska()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, """
            Broj radnika;Redovni sati
            1;176
            1;160
            """);

        Assert.Contains(rezultat.Greske, g => g.Opis.Contains("više puta"));
    }

    [Fact]
    public void Procitaj_ZaglavljeBezKoloneBrojRadnika_JeGreska()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Ime;Redovni sati\nPera;176");

        Assert.False(rezultat.JeIspravan);
        Assert.Contains(rezultat.Greske, g => g.Opis.Contains(UvozSatiService.KolonaBrojRadnika));
    }

    /// <summary>Naslovi se prepoznaju bez obzira na dijakritiku i velika slova.</summary>
    [Fact]
    public void Procitaj_NaslovBezDijakritike_SePrepoznaje()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "BROJ RADNIKA;Nocni rad;Godisnji odmor\n1;12;40");

        Assert.True(rezultat.JeIspravan);
        Assert.Equal(12, rezultat.Redovi[0].NocniSati);
        Assert.Equal(40, rezultat.Redovi[0].GodisnjiOdmorSati);
    }

    [Fact]
    public void Procitaj_NepoznataKolona_SePrijavljujeAliNeBlokira()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Broj radnika;Redovni sati;Odeljenje\n1;176;Prodaja");

        Assert.True(rezultat.JeIspravan);
        Assert.Contains("Odeljenje", rezultat.NepoznateKolone);
    }

    [Fact]
    public void Procitaj_PrazneCelije_OstavljajuNulu()
    {
        using var db = NoviKontekst(1);

        var rezultat = Procitaj(db, "Broj radnika;Redovni sati;Prekovremeni\n1;176;");

        Assert.True(rezultat.JeIspravan);
        Assert.Equal(0, rezultat.Redovi[0].PrekovremeneSati);
    }

    [Fact]
    public void Primeni_FajlSaGreskama_Odbija()
    {
        using var db = NoviKontekst(1);
        var servis = new UvozSatiService(db);
        var rezultat = servis.Procitaj(NapraviCsv("Broj radnika;Redovni sati\n99;176"), Godina, Mesec);

        Assert.Throws<InvalidOperationException>(() => servis.Primeni(rezultat, Godina, Mesec));
        Assert.Empty(db.RadniSati);
    }

    [Fact]
    public void SacuvajSablon_DajeZaglavljeIRadnikePerioda()
    {
        using var db = NoviKontekst(1, 2);
        string putanja = Path.Combine(_dir, "sablon.xlsx");

        new UvozSatiService(db).SacuvajSablon(putanja, Godina, Mesec);

        Assert.True(File.Exists(putanja));

        // Šablon mora biti čitljiv istim uvozom koji ga je napravio.
        var rezultat = new UvozSatiService(db).Procitaj(putanja, Godina, Mesec);
        Assert.Empty(rezultat.Greske);
        Assert.Equal(2, rezultat.Redovi.Count);
    }
}
