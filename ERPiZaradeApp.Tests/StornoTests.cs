using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Storniranje (Faza 2.7). Stornirani obračun ostaje u istoriji sa svim iznosima, ali se
/// izostavlja svuda gde se novac isplaćuje ili prijavljuje. Testovi drže dve stvari koje
/// se najlakše razmimoiđu:
/// 1. da izostavljanje bude <b>svuda</b>, a ne samo tamo gde se prvo primeti;
/// 2. da rata kredita bude vraćena tačno jednom — ni nula puta, ni dvaput.
/// </summary>
public class StornoTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;
    private static readonly DateTime DatumValute = new(2026, 4, 5);

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

    private static ObracunPlate DodajObracun(PlataDbContext db, int id, bool zakljucan = false)
    {
        db.Radnici.Add(new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Jmbg = "0101990710016",
            BankovniRacun = $"160-111111111{id}-11",
            Godina = Godina,
            Mesec = Mesec
        });

        var obracun = new ObracunPlate
        {
            Id = id,
            RadnikId = id,
            Godina = Godina,
            Mesec = Mesec,
            Zakljucan = zakljucan,
            BrutoZarada = 80000m,
            NetoIsplata = 50000m,
            PorezNaDohodak = 5000m,
            DoprinosPioRadnik = 2500m,
            DoprinosZdravstvoRadnik = 2500m,
            DoprinosPioPoslodavac = 2500m,
            DoprinosZdravstvoPoslodavac = 2500m
        };

        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    /// <summary>Kredit sa jednom već skinutom ratom, tako da period obračuna pada u opseg plaćenih.</summary>
    private static Kredit DodajKredit(PlataDbContext db, int radnikId)
    {
        var kredit = new Kredit
        {
            Id = radnikId,
            RadnikId = radnikId,
            Opis = "Test kredit",
            UkupanIznos = 120000m,
            MesecnaRata = 10000m,
            BrojRata = 12,
            PlateneRate = 1,
            OstatakDuga = 110000m,
            DatumPocetka = new DateTime(Godina, Mesec, 1),
            Aktivan = true
        };
        db.Krediti.Add(kredit);
        db.SaveChanges();
        return kredit;
    }

    private static StornoService Servis(PlataDbContext db) => new(db);

    [Fact]
    public void Storniranje_ObracunOstajeUBaziSaSvimIznosima()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var rezultat = Servis(db).Storniraj(Godina, Mesec, 1, "Pogrešan broj sati");

        Assert.True(rezultat.Uspesno);

        var o = db.ObracuniPlata.Single();
        Assert.True(o.Storniran);
        Assert.Equal("Pogrešan broj sati", o.RazlogStorniranja);
        Assert.NotNull(o.DatumStorniranja);

        // Iznosi se ne nuliraju — i dalje se zna šta je bilo obračunato.
        Assert.Equal(50000m, o.NetoIsplata);
        Assert.Equal(5000m, o.PorezNaDohodak);
    }

    [Fact]
    public void Storniranje_ZakljucanObracun_JeDozvoljenoBezOtkljucavanja()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1, zakljucan: true);

        var rezultat = Servis(db).Storniraj(Godina, Mesec, 1, "Radnik nije radio taj mesec");

        Assert.True(rezultat.Uspesno);

        var o = db.ObracuniPlata.Single();
        Assert.True(o.Storniran);
        Assert.True(o.Zakljucan); // period ostaje zaključan
    }

    [Fact]
    public void Storniranje_BezRazloga_SeOdbija()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var rezultat = Servis(db).Storniraj(Godina, Mesec, 1, "   ");

        Assert.False(rezultat.Uspesno);
        Assert.False(db.ObracuniPlata.Single().Storniran);
    }

    [Fact]
    public void Storniranje_UpisujeRevizioniTrag()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        Servis(db).Storniraj(Godina, Mesec, 1, "Duplirani obračun");

        var zapis = db.ObracunAuditi.Single();
        Assert.Equal(AkcijaObracuna.Storniran, zapis.Akcija);
        Assert.Equal(1, zapis.BrojRadnika);
        Assert.Contains("Duplirani obračun", zapis.Detalji);
    }

    [Fact]
    public void Storniranje_IzostavljaObracunIzNalogaZaPrenos()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);

        Servis(db).Storniraj(Godina, Mesec, 2, "Greška u obračunu");

        var prijava = new PppPdPrijava
        {
            Godina = Godina, Mesec = Mesec,
            Bop = "9712345678901234A",
            IznosZaUplatu = 15000m,
            RacunZaUplatu = EPoreziImportService.PodrazumevaniRacunObjedinjeneNaplate,
            ModelPozivaNaBroj = EPoreziImportService.PodrazumevaniModel,
            Status = StatusPrijave.Prihvacena
        };

        var paket = new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijava, DatumValute);

        // Samo nestornirani radnik dobija nalog za neto zaradu.
        Assert.Single(paket.Nalozi, n => n.Vrsta == VrstaNaloga.NetoZarada);
    }

    [Fact]
    public void Storniranje_IzostavljaObracunIzGodisnjePotvrde()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);

        Servis(db).Storniraj(Godina, Mesec, 2, "Greška u obračunu");

        var rezultat = new PppPoService(db).Pripremi(Godina);

        Assert.Single(rezultat.Obrasci);
        Assert.Equal(1, rezultat.Obrasci[0].Radnik.BrojRadnika);
    }

    [Fact]
    public void Storniranje_VracaRatuKreditaTacnoJednom()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajKredit(db, 1);

        var rezultat = Servis(db).Storniraj(Godina, Mesec, 1, "Isplata nije izvršena");

        Assert.Equal(1, rezultat.BrojKredita);

        var kredit = db.Krediti.Single();
        Assert.Equal(0, kredit.PlateneRate);
        Assert.Equal(120000m, kredit.OstatakDuga);
        Assert.True(kredit.Aktivan);

        // Ponovno storniranje istog obračuna ne sme da vrati ratu drugi put.
        var ponovo = Servis(db).Storniraj(Godina, Mesec, 1, "Isplata nije izvršena");
        Assert.False(ponovo.Uspesno);
        Assert.Equal(0, db.Krediti.Single().PlateneRate);
    }

    [Fact]
    public void PonistavanjeStorniranja_VracaObracunIRatuUPrvobitnoStanje()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajKredit(db, 1);

        Servis(db).Storniraj(Godina, Mesec, 1, "Greška operatera");
        var rezultat = Servis(db).PonistiStorniranje(Godina, Mesec, 1, "Greška je bila u prijavi, ne u obračunu");

        Assert.True(rezultat.Uspesno);

        var o = db.ObracuniPlata.Single();
        Assert.False(o.Storniran);
        Assert.Null(o.DatumStorniranja);
        Assert.Equal("", o.RazlogStorniranja);

        var kredit = db.Krediti.Single();
        Assert.Equal(1, kredit.PlateneRate);
        Assert.Equal(110000m, kredit.OstatakDuga);
    }

    [Fact]
    public void Storniranje_CelogPerioda_ObuhvataSveObracune()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);
        DodajObracun(db, 3);

        var rezultat = Servis(db).Storniraj(Godina, Mesec, null, "Ceo obračun se ponavlja");

        Assert.Equal(3, rezultat.BrojObracuna);
        Assert.All(db.ObracuniPlata.ToList(), o => Assert.True(o.Storniran));

        var zapis = db.ObracunAuditi.Single();
        Assert.Null(zapis.BrojRadnika);
        Assert.Contains("3 obračuna", zapis.Detalji);
    }

    /// <summary>
    /// Kontrolni test: bez ijednog storna sve ostaje brojčano isto kao pre Faze 2.7.
    /// Ovaj je uhvatio više grešaka nego onaj koji proverava novo pravilo.
    /// </summary>
    [Fact]
    public void BezStorniranja_NaloziIPotvrdeOstajuNepromenjeni()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);

        var prijava = new PppPdPrijava
        {
            Godina = Godina, Mesec = Mesec,
            Bop = "9712345678901234A",
            IznosZaUplatu = 30000m,
            RacunZaUplatu = EPoreziImportService.PodrazumevaniRacunObjedinjeneNaplate,
            ModelPozivaNaBroj = EPoreziImportService.PodrazumevaniModel,
            Status = StatusPrijave.Prihvacena
        };

        var paket = new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijava, DatumValute);

        Assert.Equal(2, paket.Nalozi.Count(n => n.Vrsta == VrstaNaloga.NetoZarada));
        Assert.Single(paket.Nalozi, n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);
        Assert.Equal(2, new PppPoService(db).Pripremi(Godina).Obrasci.Count);
    }
}
