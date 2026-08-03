using System.IO;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Obrasci za refundaciju naknade zarade iz sredstava RFZO — OZ-7 i OZ-10 (Faza 2.6).
///
/// Najvažniji su testovi koji tvrde da se obrazac slaže sa obračunom: iznos koji se traži od
/// Fonda mora biti tačno onaj koji je isplaćen i prijavljen kroz PPP-PD. Odmah za njima idu
/// dva koja drže formule sa samog obrasca — bruto = 15 + 17 + 18 i za isplatu = 15+16+17+18 —
/// jer filijala nalog koji se ne sabira vraća.
/// </summary>
public class RfzoServiceTests
{
    private const int Godina = 2026;
    private const int Mesec = 6;

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);

        db.Firme.Add(new Firma
        {
            Naziv = "TEST DOO",
            Pib = "100000001",
            PosebanRacun = "160-0000000123-45",
            SifraDelatnosti = "6201"
        });

        db.VrstePrimanja.AddRange(VrstePrimanjaSeed.Podrazumevane());
        db.SaveChanges();
        return db;
    }

    private static Radnik DodajRadnika(PlataDbContext db, int broj, string jmbg = "0101990710016", string lbo = "12345678901")
    {
        var radnik = new Radnik
        {
            BrojRadnika = broj,
            ImeIPrezime = $"Radnik {broj}",
            Jmbg = jmbg,
            Lbo = lbo,
            Godina = Godina,
            Mesec = Mesec,
            Aktivan = true
        };

        db.Radnici.Add(radnik);
        db.SaveChanges();
        return radnik;
    }

    /// <summary>
    /// Obračun sa stopama iz zakona; <paramref name="deoNaTeretFonda"/> je onaj deo bruta koji
    /// je isplaćen kao bolovanje preko 30 dana, ostatak je redovna zarada.
    /// </summary>
    private static ObracunPlate DodajObracun(
        PlataDbContext db, Radnik radnik, decimal bruto, decimal deoNaTeretFonda,
        int godina = Godina, int mesec = Mesec, int sati = 176)
    {
        decimal porez = Math.Round(bruto * 0.10m, 2);
        decimal pio = Math.Round(bruto * 0.14m, 2);
        decimal zdr = Math.Round(bruto * 0.0515m, 2);
        decimal nez = Math.Round(bruto * 0.0075m, 2);

        var obracun = new ObracunPlate
        {
            RadnikId = radnik.Id,
            Godina = godina,
            Mesec = mesec,
            BrutoZarada = bruto - deoNaTeretFonda,
            BrutoBolovanje = deoNaTeretFonda,
            PorezNaDohodak = porez,
            DoprinosPioRadnik = pio,
            DoprinosZdravstvoRadnik = zdr,
            DoprinosNezaposlenostRadnik = nez,
            DoprinosPioPoslodavac = Math.Round(bruto * 0.10m, 2),
            DoprinosZdravstvoPoslodavac = zdr,
            NetoIsplata = bruto - porez - pio - zdr - nez,
            RedovniSati = sati
        };

        var zarada = db.VrstePrimanja.First(v => v.Sifra == VrstePrimanjaSeed.OsnovnaZarada);
        var bolovanje = db.VrstePrimanja.First(v => v.Sifra == VrstePrimanjaSeed.BolovanjePreko30);

        if (bruto - deoNaTeretFonda > 0)
            obracun.Stavke.Add(new ObracunStavka
            {
                VrstaPrimanjaId = zarada.VrstaPrimanjaId,
                Iznos = bruto - deoNaTeretFonda,
                OporeziviDeo = bruto - deoNaTeretFonda,
                Sati = deoNaTeretFonda > 0 ? sati / 2 : sati
            });

        if (deoNaTeretFonda > 0)
            obracun.Stavke.Add(new ObracunStavka
            {
                VrstaPrimanjaId = bolovanje.VrstaPrimanjaId,
                Iznos = deoNaTeretFonda,
                OporeziviDeo = deoNaTeretFonda,
                Sati = bruto - deoNaTeretFonda > 0 ? sati / 2 : sati
            });

        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    private static Bolovanje DodajBolovanje(
        PlataDbContext db, int brojRadnika, DateTime od, DateTime doDatum,
        OsnovSprecenosti osnov = OsnovSprecenosti.Bolest, DateTime? pocetak = null, bool prva = true)
    {
        var zapis = new Bolovanje
        {
            BrojRadnika = brojRadnika,
            Godina = Godina,
            Mesec = Mesec,
            DatumPocetkaSprecenosti = pocetak ?? od.AddDays(-30),
            DatumOd = od,
            DatumDo = doDatum,
            Osnov = osnov,
            PrvaIsplata = prva
        };

        db.Bolovanja.Add(zapis);
        db.SaveChanges();
        return zapis;
    }

    // ── OZ-10 ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pun mesec bolovanja: ceo obračun je naknada na teret Fonda, pa se u obrazac prepisuju
    /// iznosi iz obračuna — bez ijedne podele i bez ijednog zaokruživanja.
    /// </summary>
    [Fact]
    public void Oz10_PunMesecBolovanja_PrepisujeIznoseIzObracuna()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var obracun = DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 30));

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        var red = Assert.Single(spisak.Redovi);
        Assert.Equal(80000m, red.BrutoNaknada);
        Assert.Equal(obracun.PorezNaDohodak, red.Porez);
        Assert.Equal(obracun.UkupniDoprinosi, red.DoprinosiIzNaknade);
        Assert.Equal(obracun.UkupniDoprinosiPoslodavca, red.DoprinosiNaNaknadu);
        Assert.Equal(30, red.BrojDana);
        Assert.Equal("да", red.PrvaIsplataStr);
    }

    /// <summary>
    /// Kontrola sa samog obrasca: bruto naknada je zbir kolona 15, 17 i 18, a kolona za
    /// isplatu zbir kolona 15, 16, 17 i 18. Filijala spisak koji se ne sabira vraća.
    /// </summary>
    [Fact]
    public void Oz10_Kolone_ZadovoljavajuFormuleObrasca()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 123456.78m, deoNaTeretFonda: 61728.39m);
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 16), new DateTime(Godina, Mesec, 30));

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);
        var red = Assert.Single(spisak.Redovi);

        Assert.Equal(red.BrutoNaknada, red.DoprinosiIzNaknade + red.Porez + red.NetoNaknada);
        Assert.Equal(red.ZaIsplatu, red.DoprinosiIzNaknade + red.DoprinosiNaNaknadu + red.Porez + red.NetoNaknada);
    }

    /// <summary>
    /// Mešovit mesec — pola zarade, pola bolovanja: od Fonda se traži samo deo koji na njegov
    /// teret i pada, a ne ceo obračun.
    /// </summary>
    [Fact]
    public void Oz10_MesovitMesec_TraziSamoDeoNaTeretFonda()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 100000m, deoNaTeretFonda: 40000m);
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 19), new DateTime(Godina, Mesec, 30));

        var red = Assert.Single(new RfzoService(db).Pripremi(Godina, Mesec).Redovi);

        Assert.Equal(40000m, red.BrutoNaknada);

        // Porez celog obračuna je 10.000; na teret Fonda pada 40% od toga.
        Assert.Equal(4000m, red.Porez);
        Assert.Equal(Math.Round(19900m * 0.40m, 2), red.DoprinosiIzNaknade);
    }

    /// <summary>
    /// Stornirani obračun nije isplaćen, pa se od Fonda ne traži njegova refundacija.
    /// </summary>
    [Fact]
    public void Oz10_StorniranObracun_NeUlaziUSpisak()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        var obracun = DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);
        obracun.Storniran = true;
        db.SaveChanges();

        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 30));

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.Equal(0m, spisak.UkupnoBruto);
        Assert.Contains(spisak.Nalazi, n => n.Provera == "Nema obračunate naknade");
        Assert.False(spisak.SmeSeIzvesti);
    }

    /// <summary>
    /// Vrsta primanja koja nije označena kao naknada na teret Fonda ne ulazi u obrazac — što
    /// je i smisao te oznake: šta Fond refundira ne odlučuje kod nego šifarnik.
    /// </summary>
    [Fact]
    public void Oz10_VrstaBezOznakeNaTeretFonda_NeUlaziUObrazac()
    {
        using var db = NoviKontekst();

        var vrsta = db.VrstePrimanja.First(v => v.Sifra == VrstePrimanjaSeed.BolovanjePreko30);
        vrsta.NaTeretFonda = false;
        db.SaveChanges();

        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 30));

        Assert.Equal(0m, new RfzoService(db).Pripremi(Godina, Mesec).UkupnoBruto);
    }

    /// <summary>
    /// Broj dana ide u kolonu svog osnova, a ostale kolone ostaju prazne — po tome filijala
    /// razvrstava zahtev.
    /// </summary>
    [Fact]
    public void Oz10_BrojDana_StojiUKoloniSvogOsnova()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 10),
            osnov: OsnovSprecenosti.OdrzavanjeTrudnoce);

        var red = Assert.Single(new RfzoService(db).Pripremi(Godina, Mesec).Redovi);

        Assert.Equal(10, red.DaniZa(OsnovSprecenosti.OdrzavanjeTrudnoce));
        Assert.Equal(0, red.DaniZa(OsnovSprecenosti.Bolest));
        Assert.Equal(0, red.DaniZa(OsnovSprecenosti.PovredaNaRadu));
    }

    /// <summary>
    /// Period unutar prvih 30 dana nosi poslodavac, ne Fond. Ne odbija se — datum početka
    /// sprečenosti ume biti pogrešno unet — ali se prijavljuje dok je ispravka jeftina.
    /// </summary>
    [Fact]
    public void Oz10_PeriodUnutarPrvih30Dana_PrijavljujeSe()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);

        var od = new DateTime(Godina, Mesec, 10);
        DodajBolovanje(db, 1, od, od.AddDays(5), pocetak: od.AddDays(-3));

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.Contains(spisak.Nalazi, n => n.Provera == "Prvih 30 dana nosi poslodavac");
    }

    /// <summary>
    /// Prag od 30 dana <b>ne važi za sve osnove</b>. Kod povrede na radu, profesionalne
    /// bolesti i davanja tkiva i organa Fond plaća od prvog dana, pa upozorenje tamo ne sme
    /// da se javi — bilo bi netačno i naučilo bi korisnika da nalaze preskače.
    /// </summary>
    [Theory]
    [InlineData(OsnovSprecenosti.PovredaNaRadu)]
    [InlineData(OsnovSprecenosti.ProfesionalnaBolest)]
    [InlineData(OsnovSprecenosti.DavalacTkivaIOrgana)]
    public void Oz10_OsnovKojiFondPlacaOdPrvogDana_NePrijavljujePrag(OsnovSprecenosti osnov)
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);

        var od = new DateTime(Godina, Mesec, 10);
        DodajBolovanje(db, 1, od, od.AddDays(5), osnov: osnov, pocetak: od);

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.DoesNotContain(spisak.Nalazi, n => n.Provera.StartsWith("Prvih", StringComparison.Ordinal));
    }

    /// <summary>
    /// Kod nege člana porodice prag zavisi od uzrasta člana — mlađi od tri godine od prvog
    /// dana, stariji od 31. Zapis uzrast ne nosi, pa se ne pretpostavlja ni jedno ni drugo.
    /// </summary>
    [Fact]
    public void Oz10_NegaClanaPorodice_NePretpostavljaPrag()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);

        var od = new DateTime(Godina, Mesec, 10);
        DodajBolovanje(db, 1, od, od.AddDays(5), osnov: OsnovSprecenosti.NegaClanaPorodice, pocetak: od);

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.DoesNotContain(spisak.Nalazi, n => n.Provera.StartsWith("Prvih", StringComparison.Ordinal));
    }

    /// <summary>
    /// Pol se izvodi iz JMBG-a, pa se uz radnika ne čuva. Bez upotrebljivog JMBG-a se kolona
    /// ne može popuniti, i to je greška — obrazac je traži.
    /// </summary>
    [Fact]
    public void Oz10_Pol_SeIzvodiIzJmbgAIzostanakSePrijavljuje()
    {
        using var db = NoviKontekst();

        // 0101990710016 → cifre 10–12 su „001", dakle muški.
        var muski = DodajRadnika(db, 1);
        // 0101990715002 → cifre 10–12 su „500", dakle ženski.
        var zenski = DodajRadnika(db, 2, jmbg: "0101990715002");
        var bezJmbg = DodajRadnika(db, 3, jmbg: "");

        foreach (var r in new[] { muski, zenski, bezJmbg })
        {
            DodajObracun(db, r, bruto: 80000m, deoNaTeretFonda: 80000m);
            DodajBolovanje(db, r.BrojRadnika, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 30));
        }

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.Equal("М", spisak.Redovi.Single(r => r.Radnik.BrojRadnika == 1).Pol);
        Assert.Equal("Ж", spisak.Redovi.Single(r => r.Radnik.BrojRadnika == 2).Pol);
        Assert.Equal("", spisak.Redovi.Single(r => r.Radnik.BrojRadnika == 3).Pol);
        Assert.Contains(spisak.Nalazi, n => n.Provera == "Pol se ne može odrediti" && n.BrojRadnika == 3);
    }

    /// <summary>
    /// Dva bolovanja istog radnika u istom mesecu dele jedan obračun. Podela ide po danima, a
    /// zbir redova mora ostati jednak onome što je obračunato — inače bi se od Fonda tražilo
    /// više ili manje nego što je isplaćeno.
    /// </summary>
    [Fact]
    public void Oz10_DvaBolovanjaIstogRadnika_DeleIznosBezGubitka()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 90000m, deoNaTeretFonda: 90000m);

        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 10));
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 11), new DateTime(Godina, Mesec, 30),
            osnov: OsnovSprecenosti.NegaClanaPorodice, prva: false);

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.Equal(2, spisak.Redovi.Count);
        Assert.Equal(90000m, spisak.UkupnoBruto);
        Assert.Contains(spisak.Nalazi, n => n.Provera == "Više bolovanja u istom mesecu");
    }

    /// <summary>
    /// Bez unetog posebnog računa Fond nema gde da uplati refundaciju, pa se obrazac ne izvozi.
    /// </summary>
    [Fact]
    public void Oz10_BezPosebnogRacuna_SeNeIzvozi()
    {
        using var db = NoviKontekst();

        var firma = db.Firme.First();
        firma.PosebanRacun = "";
        db.SaveChanges();

        var radnik = DodajRadnika(db, 1);
        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);
        DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 30));

        var spisak = new RfzoService(db).Pripremi(Godina, Mesec);

        Assert.Contains(spisak.Nalazi, n => n.Provera == "Nema posebnog računa");
        Assert.False(spisak.SmeSeIzvesti);
    }

    // ── OZ-7 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Osnov se uzima iz dvanaest meseci koji prethode mesecu u kome je sprečenost nastupila —
    /// ne iz meseca isplate, i ne uključujući sam mesec sprečenosti.
    /// </summary>
    [Fact]
    public void Oz7_Uzima12MeseciPreMesecaSprecenosti()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        // Sprečenost počinje u junu 2026; osnov su jun 2025. – maj 2026.
        var bolovanje = DodajBolovanje(db, 1, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30),
            pocetak: new DateTime(2026, 6, 1));

        var (obrazac, _) = new RfzoService(db).PripremiOz7(bolovanje.BolovanjeId);

        Assert.NotNull(obrazac);
        Assert.Equal(12, obrazac.Redovi.Count);
        Assert.Equal((2025, 6), (obrazac.Redovi[0].Godina, obrazac.Redovi[0].Mesec));
        Assert.Equal((2026, 5), (obrazac.Redovi[11].Godina, obrazac.Redovi[11].Mesec));
    }

    /// <summary>
    /// Prosek po času je ukupna zarada podeljena ukupnim brojem časova — po tome se naknada
    /// i obračunava, pa je to jedini broj sa ovog obrasca koji ide dalje.
    /// </summary>
    [Fact]
    public void Oz7_ProsekPoCasu_JeUkupnoPodeljenoCasovima()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        // Dvanaest istih meseci po 176 časova i 88.000 bruto → 500 po času.
        for (int i = 1; i <= 12; i++)
            DodajObracun(db, radnik, bruto: 88000m, deoNaTeretFonda: 0m, godina: 2025, mesec: i, sati: 176);

        var bolovanje = DodajBolovanje(db, 1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            pocetak: new DateTime(2026, 1, 1));

        var (obrazac, nalazi) = new RfzoService(db).PripremiOz7(bolovanje.BolovanjeId);

        Assert.NotNull(obrazac);
        Assert.Equal(12 * 176, obrazac.UkupnoCasova);
        Assert.Equal(12 * 88000m, obrazac.UkupnoBruto);
        Assert.Equal(500m, obrazac.ProsekBrutoPoCasu);

        // Neto po času: bruto umanjen za porez (10%) i doprinose radnika (19,9%).
        Assert.Equal(Math.Round(88000m * 0.701m / 176m, 4), obrazac.ProsekNetoPoCasu);

        Assert.DoesNotContain(nalazi, n => n.Tezina == TezinaNalaza.Greska);
    }

    /// <summary>
    /// Mesec bez obračuna se po uputstvu uz obrazac popunjava minimalnom zaradom, a nju
    /// program nema. Red ostaje prazan i prijavljuje se — izmišljen iznos bi ušao u prosek
    /// po kome se naknada isplaćuje.
    /// </summary>
    [Fact]
    public void Oz7_MesecBezObracuna_OstajePrazanIPrijavljujeSe()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        for (int i = 1; i <= 6; i++)
            DodajObracun(db, radnik, bruto: 88000m, deoNaTeretFonda: 0m, godina: 2025, mesec: i, sati: 176);

        var bolovanje = DodajBolovanje(db, 1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            pocetak: new DateTime(2026, 1, 1));

        var (obrazac, nalazi) = new RfzoService(db).PripremiOz7(bolovanje.BolovanjeId);

        Assert.NotNull(obrazac);
        Assert.Equal(6, obrazac.BrojMeseciBezObracuna);
        Assert.All(obrazac.Redovi.Where(r => r.Mesec > 6), r => Assert.Equal(0m, r.Bruto));
        Assert.Contains(nalazi, n => n.Provera == "Meseci bez obračuna");
    }

    /// <summary>
    /// Bez LBO-a obrazac nije upotrebljiv — po njemu Fond nalazi osiguranika, a JMBG ga ne
    /// zamenjuje.
    /// </summary>
    [Fact]
    public void Oz7_BezLbo_PrijavljujeGresku()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1, lbo: "");
        DodajObracun(db, radnik, bruto: 88000m, deoNaTeretFonda: 0m, godina: 2025, mesec: 12);

        var bolovanje = DodajBolovanje(db, 1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            pocetak: new DateTime(2026, 1, 1));

        var (_, nalazi) = new RfzoService(db).PripremiOz7(bolovanje.BolovanjeId);

        Assert.Contains(nalazi, n => n.Provera == "Nedostaje LBO" && n.Tezina == TezinaNalaza.Greska);
    }

    /// <summary>
    /// Kolona 5 je datum poslednje (konačne) isplate tog meseca. Mesec bez isplate ostaje
    /// prazan — <c>DateTime.MinValue</c> bi se odštampao kao „01.01.0001." i prošao nezapaženo.
    /// </summary>
    [Fact]
    public void Oz7_DatumIsplate_JePrazanZaMesecBezIsplate()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        var isplata = new Isplata
        {
            Godina = 2025,
            Mesec = 12,
            RedniBroj = 1,
            DatumIsplate = new DateTime(2026, 1, 10)
        };
        db.Isplate.Add(isplata);
        db.SaveChanges();

        var saIsplatom = DodajObracun(db, radnik, bruto: 88000m, deoNaTeretFonda: 0m, godina: 2025, mesec: 12);
        saIsplatom.IsplataId = isplata.IsplataId;

        DodajObracun(db, radnik, bruto: 88000m, deoNaTeretFonda: 0m, godina: 2025, mesec: 11);
        db.SaveChanges();

        var bolovanje = DodajBolovanje(db, 1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            pocetak: new DateTime(2026, 1, 1));

        var (obrazac, _) = new RfzoService(db).PripremiOz7(bolovanje.BolovanjeId);

        Assert.NotNull(obrazac);
        Assert.Equal(new DateTime(2026, 1, 10), obrazac.Redovi.Single(r => r.Mesec == 12).DatumIsplate);
        Assert.Null(obrazac.Redovi.Single(r => r.Mesec == 11).DatumIsplate);
        Assert.Null(obrazac.Redovi.Single(r => r.Mesec == 3).DatumIsplate);
    }

    // ── Štampa ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raspored PDF-a se ne proverava prevođenjem: kolona koja ne staje ili raspon ćelija koji
    /// se ne poklapa sa brojem kolona pada tek pri generisanju. OZ-10 ima dvadeset kolona i
    /// zbirni red sa rasponom, pa se oba dokumenta stvarno naprave.
    /// </summary>
    [Fact]
    public void Obrasci_SeGenerisuBezGreske()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        for (int i = 1; i <= 12; i++)
            DodajObracun(db, radnik, bruto: 88000m, deoNaTeretFonda: 0m, godina: 2025, mesec: i);

        DodajObracun(db, radnik, bruto: 80000m, deoNaTeretFonda: 80000m);
        DodajRadnika(db, 2, jmbg: "0101990715002");
        DodajObracun(db, db.Radnici.Single(r => r.BrojRadnika == 2), bruto: 60000m, deoNaTeretFonda: 30000m);

        var prvo = DodajBolovanje(db, 1, new DateTime(Godina, Mesec, 1), new DateTime(Godina, Mesec, 30));
        DodajBolovanje(db, 2, new DateTime(Godina, Mesec, 16), new DateTime(Godina, Mesec, 30),
            osnov: OsnovSprecenosti.PovredaNaRadu, prva: false);

        var servis = new RfzoService(db);
        var firma = db.Firme.First();
        string folder = Path.Combine(Path.GetTempPath(), "rfzo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            string oz10 = Path.Combine(folder, "OZ-10.pdf");
            Views.Bolovanja.Oz10Document.Sacuvaj(servis.Pripremi(Godina, Mesec), firma, oz10);
            Assert.True(new FileInfo(oz10).Length > 0);

            var (obrazac, _) = servis.PripremiOz7(prvo.BolovanjeId);
            Assert.NotNull(obrazac);

            string oz7 = Path.Combine(folder, "OZ-7.pdf");
            Views.Bolovanja.Oz7Document.Sacuvaj(obrazac, firma, oz7);
            Assert.True(new FileInfo(oz7).Length > 0);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Kolona 3 je zarada bez poreza i doprinosa, a NE neto za isplatu: obustave su radnikov
    /// trošak, pa ostvarenu zaradu ne umanjuju. Da se pomešaju, osnov bi bio manji od stvarnog
    /// i naknada bi svakom radniku sa kreditom ispala niža.
    /// </summary>
    [Fact]
    public void Oz7_Kolona3_NeUmanjujeSeZaObustave()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, 1);

        var obracun = DodajObracun(db, radnik, bruto: 100000m, deoNaTeretFonda: 0m, godina: 2025, mesec: 12);
        obracun.KreditObustava = 25000m;
        obracun.NetoIsplata -= 25000m;
        db.SaveChanges();

        var bolovanje = DodajBolovanje(db, 1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            pocetak: new DateTime(2026, 1, 1));

        var (obrazac, _) = new RfzoService(db).PripremiOz7(bolovanje.BolovanjeId);

        Assert.NotNull(obrazac);
        Assert.Equal(70100m, obrazac.UkupnoNeto);
        Assert.Equal(100000m, obrazac.UkupnoBruto);
    }
}
