using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Poreske olakšice. Dva mehanizma se ne smeju pomešati: <b>oslobođenje</b> umanjuje ono što
/// se plaća, <b>povraćaj</b> ne dira nijedan iznos nego se traži posebnim zahtevom. Zamena bi
/// značila da firma ili plati manje nego što sme, ili traži povraćaj koji joj ne sleduje.
/// </summary>
public class OlaksicaTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;
    private const int Fond = 176;
    private const decimal VrednostBoda = 10000m;

    /// <summary>SVP šifra sa OL oznakom na pozicijama 7–8.</summary>
    private static string Svp(string ol) => $"101101{ol}0";

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);
        db.Porezi.Add(new Porezi
        {
            Godina = Godina, Mesec = Mesec, RedniBroj = 1,
            ProcMinul = 0.40m, ProcPreko = 26.00m, ProcNocni = 26.00m,
            ProcDrzav = 110.00m, ProcBolov = 65.00m, ProcNedel = 0.00m,
            AkPorez = 10.00m, Prvast = 28423.00m
        });
        db.SaveChanges();
        return db;
    }

    private static PoreskaOlaksica DodajOlaksicu(
        PlataDbContext db,
        string sifra,
        MehanizamOlaksice mehanizam,
        decimal procenat = 70m,
        Action<PoreskaOlaksica>? podesi = null)
    {
        var olaksica = new PoreskaOlaksica
        {
            Sifra = sifra,
            Naziv = $"Test olakšica {sifra}",
            Mehanizam = mehanizam,
            ProcenatPoreza = procenat,
            ProcenatDoprinosa = procenat,
            Aktivna = true
        };
        podesi?.Invoke(olaksica);

        db.PoreskeOlaksice.Add(olaksica);
        db.SaveChanges();
        return olaksica;
    }

    private static Radnik DodajRadnika(PlataDbContext db, string ol, Action<Radnik>? podesi = null)
    {
        var radnik = new Radnik
        {
            Id = 1, BrojRadnika = 1, ImeIPrezime = "Test Radnik", Jmbg = "0101990710016",
            Koeficijent = 7.04m, Radno_Mesto = Svp(ol),
            Godina = Godina, Mesec = Mesec
        };
        podesi?.Invoke(radnik);

        db.Radnici.Add(radnik);
        db.SaveChanges();
        return radnik;
    }

    private static ObracunPlate Obracunaj(PlataDbContext db, Radnik radnik)
    {
        var sati = new RadniSat { RadnikId = radnik.Id, Godina = Godina, Mesec = Mesec, RedovniSati = Fond, Prosek = 400m };
        return new ObracunService(db).Calculate(radnik, sati, Godina, Mesec, VrednostBoda, Fond);
    }

    /// <summary>Polazno stanje: isti obračun bez ijedne olakšice u šifarniku.</summary>
    private static ObracunPlate BezOlaksice()
    {
        using var db = NoviKontekst();
        return Obracunaj(db, DodajRadnika(db, "00"));
    }

    // ── Prepoznavanje oznake ─────────────────────────────────────────

    [Theory]
    [InlineData("101101010", "01")]
    [InlineData("101101240", "24")]
    [InlineData("101101000", "")]     // 00 = bez olakšice
    [InlineData("Rukovodilac", "")]   // nije SVP šifra
    [InlineData("", "")]
    public void OznakaIzSvp_CitaPozicije7i8(string svp, string ocekivano)
    {
        Assert.Equal(ocekivano, OlaksicaService.OznakaIzSvp(svp));
    }

    // ── Mehanizmi ────────────────────────────────────────────────────

    /// <summary>Oslobođenje umanjuje porez i doprinose radnika za procenat olakšice.</summary>
    [Fact]
    public void Oslobodjenje_UmanjujePorezIDoprinose()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje, procenat: 70m);
        var obracun = Obracunaj(db, DodajRadnika(db, "24"));

        var bez = BezOlaksice();

        Assert.Equal("24", obracun.OlaksicaOznaka);
        Assert.True(obracun.OlaksicaUmanjujeUplatu);

        // 70% poreza i doprinosa se ne plaća.
        Assert.Equal(Math.Round(bez.PorezNaDohodak * 0.30m, 2), obracun.PorezNaDohodak, precision: 1);
        Assert.True(obracun.DoprinosPioRadnik < bez.DoprinosPioRadnik);
    }

    /// <summary>
    /// Povraćaj se plaća u punom iznosu — obračun mora biti brojčano identičan onom bez
    /// olakšice, a beleže se samo iznosi koji se traže natrag.
    /// </summary>
    [Fact]
    public void Povracaj_NeDiraNijedanIznosObracuna()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "09", MehanizamOlaksice.Povracaj, procenat: 70m);
        var obracun = Obracunaj(db, DodajRadnika(db, "09"));

        var bez = BezOlaksice();

        Assert.Equal(bez.PorezNaDohodak, obracun.PorezNaDohodak);
        Assert.Equal(bez.DoprinosPioRadnik, obracun.DoprinosPioRadnik);
        Assert.Equal(bez.NetoIsplata, obracun.NetoIsplata);

        Assert.False(obracun.OlaksicaUmanjujeUplatu);
        Assert.True(obracun.OlaksicaPorez > 0);
    }

    [Fact]
    public void Oslobodjenje_PovecavaNetoIsplatu()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje);
        var obracun = Obracunaj(db, DodajRadnika(db, "24"));

        Assert.True(obracun.NetoIsplata > BezOlaksice().NetoIsplata);
    }

    // ── Kada se ne primenjuje ────────────────────────────────────────

    /// <summary>Bez olakšice u šifarniku obračun mora biti identičan kao pre Faze 2.4.</summary>
    [Fact]
    public void BezOlaksiceUSifarniku_ObracunOstajeNepromenjen()
    {
        using var db = NoviKontekst();
        var obracun = Obracunaj(db, DodajRadnika(db, "24"));   // oznaka postoji, šifarnik prazan

        var bez = BezOlaksice();

        Assert.Equal("", obracun.OlaksicaOznaka);
        Assert.Equal(bez.PorezNaDohodak, obracun.PorezNaDohodak);
        Assert.Equal(bez.NetoIsplata, obracun.NetoIsplata);
    }

    [Fact]
    public void OznakaNula_NePraviUmanjenje()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "00", MehanizamOlaksice.Oslobodjenje);
        var obracun = Obracunaj(db, DodajRadnika(db, "00"));

        Assert.Equal("", obracun.OlaksicaOznaka);
        Assert.Equal(0m, obracun.OlaksicaPorez);
    }

    [Fact]
    public void IsklucenaOlaksica_SeNePrimenjuje()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje, podesi: o => o.Aktivna = false);
        var obracun = Obracunaj(db, DodajRadnika(db, "24"));

        Assert.Equal("", obracun.OlaksicaOznaka);
    }

    /// <summary>Rok iz šifarnika — olakšica je prestala da važi po propisu.</summary>
    [Fact]
    public void OlaksicaIsteklaPoSifarniku_SeNePrimenjuje()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje,
            podesi: o => o.VaziDo = new DateTime(Godina, Mesec - 1, 28));

        var obracun = Obracunaj(db, DodajRadnika(db, "24"));

        Assert.Equal("", obracun.OlaksicaOznaka);
    }

    /// <summary>Rok radnika — pravo tog lica je isteklo, iako olakšica i dalje postoji.</summary>
    [Fact]
    public void OlaksicaIsteklaPoRadniku_SeNePrimenjuje()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje);

        var radnik = DodajRadnika(db, "24", r => r.OlaksicaVaziDo = new DateTime(Godina, Mesec - 1, 28));
        var obracun = Obracunaj(db, radnik);

        Assert.Equal("", obracun.OlaksicaOznaka);
    }

    [Fact]
    public void OlaksicaKojaJosNijePocela_SeNePrimenjuje()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje,
            podesi: o => o.VaziOd = new DateTime(Godina + 1, 1, 1));

        var obracun = Obracunaj(db, DodajRadnika(db, "24"));

        Assert.Equal("", obracun.OlaksicaOznaka);
    }

    // ── Procenat po radniku ──────────────────────────────────────────

    /// <summary>Kod nekih olakšica se procenat određuje po licu, pa karton ima prednost.</summary>
    [Fact]
    public void ProcenatSaKartonaRadnika_ImaPrednostNadSifarnickim()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Povracaj, procenat: 65m);

        var radnik = DodajRadnika(db, "24", r =>
        {
            r.ProcenatPovracajaPoreza = 75m;
            r.ProcenatPovracajaDoprinosa = 75m;
        });

        var obracun = Obracunaj(db, radnik);

        Assert.Equal(Math.Round(obracun.PorezNaDohodak * 0.75m, 2), obracun.OlaksicaPorez, precision: 1);
    }

    // ── Kontrolne provere ────────────────────────────────────────────

    [Fact]
    public void PreFlight_OznakaKojeNemaUSifarniku_JeGreska()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, "24");
        DodajObracunZaProveru(db, radnik);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Olakšica nije u šifarniku");
    }

    [Fact]
    public void PreFlight_OslobodjenjeBezMfpDeklaracije_JeGreska()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "24", MehanizamOlaksice.Oslobodjenje);
        var radnik = DodajRadnika(db, "24");
        DodajObracunZaProveru(db, radnik);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Olakšica bez MFP deklaracije");
    }

    /// <summary>Povraćaj se ne prijavljuje kroz MFP, pa mu deklaracija ne treba.</summary>
    [Fact]
    public void PreFlight_PovracajBezMfpDeklaracije_NijeGreska()
    {
        using var db = NoviKontekst();
        DodajOlaksicu(db, "09", MehanizamOlaksice.Povracaj);
        var radnik = DodajRadnika(db, "09");
        DodajObracunZaProveru(db, radnik);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Olakšica bez MFP deklaracije");
    }

    private static void DodajObracunZaProveru(PlataDbContext db, Radnik radnik)
    {
        db.ObracuniPlata.Add(new ObracunPlate
        {
            Id = 1, RadnikId = radnik.Id, Godina = Godina, Mesec = Mesec,
            BrutoZarada = 80000m, NetoIsplata = 55000m,
            RedovniSati = Fond, FondSatiMesecni = Fond,
            Radnik = radnik
        });
        db.SaveChanges();
    }
}
