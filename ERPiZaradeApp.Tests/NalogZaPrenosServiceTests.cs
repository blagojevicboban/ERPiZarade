using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Nalozi za prenos su tačka na kojoj greška postaje novac na pogrešnom mestu. Testovi
/// fiksiraju dva pravila koja se najlakše prekrše:
/// 1. porezi i doprinosi idu <b>jednom</b> uplatom na objedinjeni račun (od 01.03.2014.),
///    a ne pojedinačno po vrsti doprinosa;
/// 2. zbir naloga mora da se slaže i sa obračunom i sa iznosom koji je utvrdila Poreska uprava.
/// </summary>
public class NalogZaPrenosServiceTests
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
            Pib = "100000001"
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>Obračun sa okruglim iznosima: neto 50.000, porez 5.000, doprinosi 4×2.500 = 10.000.</summary>
    private static void DodajObracun(
        PlataDbContext db,
        int id,
        decimal neto = 50000m,
        string racun = "160-1111111111-11")
    {
        db.Radnici.Add(new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Jmbg = "0101990710016",
            BankovniRacun = racun,
            Godina = Godina,
            Mesec = Mesec
        });

        db.ObracuniPlata.Add(new ObracunPlate
        {
            Id = id,
            RadnikId = id,
            Godina = Godina,
            Mesec = Mesec,
            NetoIsplata = neto,
            PorezNaDohodak = 5000m,
            DoprinosPioRadnik = 2500m,
            DoprinosZdravstvoRadnik = 2500m,
            DoprinosPioPoslodavac = 2500m,
            DoprinosZdravstvoPoslodavac = 2500m
        });

        db.SaveChanges();
    }

    /// <summary>Zbir poreza i doprinosa za jedan obračun iz <see cref="DodajObracun"/>.</summary>
    private const decimal PorezIDoprinosiPoObracunu = 15000m;

    private static PppPdPrijava PrihvacenaPrijava(decimal iznos, string bop = "9712345678901234A")
        => new()
        {
            Godina = Godina,
            Mesec = Mesec,
            Bop = bop,
            IznosZaUplatu = iznos,
            RacunZaUplatu = EPoreziImportService.PodrazumevaniRacunObjedinjeneNaplate,
            ModelPozivaNaBroj = EPoreziImportService.PodrazumevaniModel,
            Status = StatusPrijave.Prihvacena
        };

    private static PaketNaloga Pripremi(PlataDbContext db, PppPdPrijava? prijava)
        => new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijava, DatumValute);

    [Fact]
    public void Pripremi_DvaRadnika_DajeDvaNalogaZaZaradeIJedanZaPoreze()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1, racun: "160-1111111111-11");
        DodajObracun(db, 2, racun: "160-2222222222-22");

        var paket = Pripremi(db, PrihvacenaPrijava(2 * PorezIDoprinosiPoObracunu));

        Assert.Equal(2, paket.Nalozi.Count(n => n.Vrsta == VrstaNaloga.NetoZarada));

        // Ključno pravilo: JEDAN nalog za sve poreze i doprinose, ne po vrsti doprinosa.
        Assert.Single(paket.Nalozi, n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);
        Assert.True(paket.SmeSePoslatiUBanku);
    }

    [Fact]
    public void Pripremi_NalogZaPoreze_NosiObjedinjeniRacunModel97IBop()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var paket = Pripremi(db, PrihvacenaPrijava(PorezIDoprinosiPoObracunu, bop: "9711112222333344A"));
        var nalog = paket.Nalozi.Single(n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);

        Assert.Equal("840-4848-37", nalog.PrimalacRacun);
        Assert.Equal("97", nalog.ModelPozivaNaBroj);
        Assert.Equal("9711112222333344A", nalog.PozivNaBroj);
        Assert.Equal(PorezIDoprinosiPoObracunu, nalog.Iznos);
    }

    [Fact]
    public void Pripremi_ZbirNalogaZaZarade_JednakZbiruNetoIzObracuna()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1, neto: 50000m, racun: "160-1111111111-11");
        DodajObracun(db, 2, neto: 37250.44m, racun: "160-2222222222-22");

        var paket = Pripremi(db, PrihvacenaPrijava(2 * PorezIDoprinosiPoObracunu));

        Assert.Equal(87250.44m, paket.ZbirZarada);
        Assert.DoesNotContain(paket.Nalazi, n => n.Provera == "Zbir naloga se ne slaže sa obračunom");
    }

    /// <summary>
    /// Bez BOP-a uplata poreza ne može da se poveže sa prijavom i ostaje neraspoređena,
    /// pa se nalog uopšte ne formira.
    /// </summary>
    [Fact]
    public void Pripremi_BezPrijave_NeFormiraNalogZaPorezeIPrijavljujeGresku()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var paket = Pripremi(db, prijava: null);

        Assert.DoesNotContain(paket.Nalozi, n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);
        Assert.False(paket.SmeSePoslatiUBanku);
        Assert.Contains(paket.Nalazi, n => n.Provera == "Nedostaje BOP");
    }

    [Fact]
    public void Pripremi_IznosPoreskeUprave_ImaPrednostNadNasimZbirom()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var paket = Pripremi(db, PrihvacenaPrijava(iznos: 15100m));
        var nalog = paket.Nalozi.Single(n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);

        Assert.Equal(15100m, nalog.Iznos);
        Assert.Contains(paket.Nalazi, n => n.Provera == "Iznos prijave se ne slaže sa obračunom");
        Assert.False(paket.SmeSePoslatiUBanku);
    }

    [Fact]
    public void Pripremi_RadnikBezRacuna_NeUlaziUNalogeIPrijavljujeSe()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1, racun: "");

        var paket = Pripremi(db, PrihvacenaPrijava(PorezIDoprinosiPoObracunu));

        Assert.DoesNotContain(paket.Nalozi, n => n.Vrsta == VrstaNaloga.NetoZarada);
        Assert.Contains(paket.Nalazi, n => n.Provera == "Nedostaje tekući račun");
        Assert.False(paket.SmeSePoslatiUBanku);
    }

    [Fact]
    public void Pripremi_NeprihvacenaPrijava_JeUpozorenjeAliNalogSeFormira()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var prijava = PrihvacenaPrijava(PorezIDoprinosiPoObracunu);
        prijava.Status = StatusPrijave.Podneta;

        var paket = Pripremi(db, prijava);

        Assert.Single(paket.Nalozi, n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);
        Assert.Contains(paket.Nalazi,
            n => n.Provera == "Prijava nije prihvaćena" && n.Tezina == TezinaNalaza.Upozorenje);
        Assert.True(paket.SmeSePoslatiUBanku);
    }

    [Fact]
    public void Pripremi_NulaNeto_NeDajeNalog()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1, neto: 0m);

        var paket = Pripremi(db, PrihvacenaPrijava(PorezIDoprinosiPoObracunu));

        Assert.DoesNotContain(paket.Nalozi, n => n.Vrsta == VrstaNaloga.NetoZarada);
    }

    [Fact]
    public void Pripremi_FirmaBezRacuna_JeGreska()
    {
        using var db = NoviKontekst();
        var firma = db.Firme.First();
        firma.BankovniRacun = "";
        db.SaveChanges();

        DodajObracun(db, 1);

        var paket = Pripremi(db, PrihvacenaPrijava(PorezIDoprinosiPoObracunu));

        Assert.Contains(paket.Nalazi, n => n.Provera == "Nedostaje račun firme");
        Assert.False(paket.SmeSePoslatiUBanku);
    }

    [Fact]
    public void Pripremi_PrazanPeriod_NeDajeNijedanNalog()
    {
        using var db = NoviKontekst();

        var paket = Pripremi(db, PrihvacenaPrijava(0m));

        Assert.Empty(paket.Nalozi);
        Assert.Contains(paket.Nalazi, n => n.Provera == "Prazan period");
    }
}

/// <summary>
/// Čitanje dokumenta koji ePorezi izda po prihvatanju prijave. Nazivi elemenata se kroz
/// verzije portala menjaju, pa čitanje mora da bude tolerantno — ali nikad da pogađa.
/// </summary>
public class EPoreziImportServiceTests
{
    private static PodaciZaUplatu Procitaj(string xml)
        => new EPoreziImportService().Procitaj(System.Xml.Linq.XDocument.Parse(xml));

    [Fact]
    public void Procitaj_StandardniNaziviElemenata_NalaziBopIIznos()
    {
        var podaci = Procitaj("""
            <Odobrenje>
              <BOP>9712345678901234A</BOP>
              <IznosZaUplatu>153420.55</IznosZaUplatu>
              <UplatniRacun>840-4848-37</UplatniRacun>
              <ModelPozivaNaBroj>97</ModelPozivaNaBroj>
            </Odobrenje>
            """);

        Assert.True(podaci.JeUpotrebljiv);
        Assert.Equal("9712345678901234A", podaci.Bop);
        Assert.Equal(153420.55m, podaci.Iznos);
        Assert.Equal("840-4848-37", podaci.RacunZaUplatu);
        Assert.Empty(podaci.NeprepoznataPolja);
    }

    [Fact]
    public void Procitaj_NaziviSaRazdvajacima_SeIstoPrepoznaju()
    {
        var podaci = Procitaj("""
            <o>
              <Broj_Odobrenja>9700001111222233B</Broj_Odobrenja>
              <Ukupan_Iznos>1000.00</Ukupan_Iznos>
            </o>
            """);

        Assert.Equal("9700001111222233B", podaci.Bop);
        Assert.Equal(1000.00m, podaci.Iznos);
    }

    /// <summary>Domaći zapis iznosa: tačka je razdvajač hiljada, zarez decimalni.</summary>
    [Fact]
    public void Procitaj_DomaciZapisIznosa_SeIspravnoTumaci()
    {
        var podaci = Procitaj("""
            <o><BOP>97A</BOP><IznosZaUplatu>1.234.567,89</IznosZaUplatu></o>
            """);

        Assert.Equal(1234567.89m, podaci.Iznos);
    }

    [Fact]
    public void Procitaj_XmlZapisIznosa_SeIspravnoTumaci()
    {
        var podaci = Procitaj("""
            <o><BOP>97A</BOP><IznosZaUplatu>1234567.89</IznosZaUplatu></o>
            """);

        Assert.Equal(1234567.89m, podaci.Iznos);
    }

    /// <summary>Nepoznat oblik dokumenta se prijavljuje, a ne popunjava nagađanjem.</summary>
    [Fact]
    public void Procitaj_NepoznataSema_PrijavljujeNeprepoznataPolja()
    {
        var podaci = Procitaj("<nesto><drugo>xyz</drugo></nesto>");

        Assert.False(podaci.JeUpotrebljiv);
        Assert.Contains(podaci.NeprepoznataPolja, p => p.Contains("BOP"));
        Assert.Contains(podaci.NeprepoznataPolja, p => p.Contains("Iznos"));
    }

    [Fact]
    public void Procitaj_BezUplatnogRacuna_KoristiObjedinjeniRacunIPrijavljujeToKaoPodrazumevano()
    {
        var podaci = Procitaj("""
            <o><BOP>97A</BOP><IznosZaUplatu>100.00</IznosZaUplatu></o>
            """);

        Assert.Equal("840-4848-37", podaci.RacunZaUplatu);
        Assert.Equal("97", podaci.ModelPozivaNaBroj);
        Assert.Contains(podaci.PopunjenaPodrazumevano, p => p.Contains("840-4848-37"));
    }
}
