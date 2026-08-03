using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Obračuni van radnog odnosa (Faza 2.3). Testovi drže četiri stvari:
///
/// 1. da računica pogodi objavljeni primer iz prakse — bruto 50.000 po ugovoru o delu daje
///    neto 32.400 uz porez 8.000 i PIO 9.600;
/// 2. da preračun neta u bruto bude <b>inverzan</b> obračunu do pare, jer se naknada u praksi
///    ugovara „na ruke";
/// 3. da naknada uđe u PPP-PD prijavu sa svojom šifrom vrste prihoda, svojom osnovicom
///    doprinosa i bez sati — a da zarada u istoj prijavi ostane brojčano nepromenjena;
/// 4. da se stope čitaju iz šifarnika, pa izmena propisa menja rezultat bez izmene koda.
/// </summary>
public class UgovorObracunTests
{
    private const int Godina = 2026;
    private const int Mesec = 4;

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

    /// <summary>Ugovor o delu za lice osigurano po drugom osnovu — stanje propisa u 2026.</summary>
    private static VrstaUgovora VrstaUgovorODelu() => new()
    {
        Sifra = "UOD",
        Naziv = "Ugovor o delu",
        Ovp = "601",
        NormiraniTroskoviProcenat = 20m,
        StopaPoreza = 20m,
        StopaPioPrimalac = 24m,
        Aktivna = true
    };

    /// <summary>Autorska naknada sa normiranim troškovima od 50%.</summary>
    private static VrstaUgovora VrstaAutorski50() => new()
    {
        Sifra = "AUT50",
        Naziv = "Autorska naknada 50%",
        Ovp = "301",
        NormiraniTroskoviProcenat = 50m,
        StopaPoreza = 20m,
        StopaPioPrimalac = 24m,
        Aktivna = true
    };

    /// <summary>Privremeni i povremeni poslovi — oporezuju se kao zarada, bez normiranih troškova.</summary>
    private static VrstaUgovora VrstaPrivremeniPoslovi() => new()
    {
        Sifra = "PPP",
        Naziv = "Privremeni i povremeni poslovi",
        Ovp = "150",
        NormiraniTroskoviProcenat = 0m,
        StopaPoreza = 10m,
        StopaPioPrimalac = 14m,
        StopaZdravstvoPrimalac = 5.15m,
        StopaNezaposlenostPrimalac = 0.75m,
        StopaPioIsplatilac = 10m,
        StopaZdravstvoIsplatilac = 5.15m,
        Aktivna = true
    };

    private static Ugovor DodajUgovor(
        PlataDbContext db,
        VrstaUgovora vrsta,
        int brojRadnika = 1,
        TipPrimaocaPrihoda tip = TipPrimaocaPrihoda.Zaposleni,
        decimal iznos = 50000m)
    {
        if (vrsta.VrstaUgovoraId == 0)
        {
            db.VrsteUgovora.Add(vrsta);
            db.SaveChanges();
        }

        if (!db.Radnici.Any(r => r.BrojRadnika == brojRadnika))
        {
            db.Radnici.Add(new Radnik
            {
                BrojRadnika = brojRadnika,
                ImeIPrezime = $"Primalac {brojRadnika}",
                Jmbg = "0101990710016",
                BankovniRacun = $"160-222222222{brojRadnika}-11",
                SifraOpstine = "013",
                VanRadnogOdnosa = true,
                Godina = Godina,
                Mesec = Mesec
            });
            db.SaveChanges();
        }

        var ugovor = new Ugovor
        {
            VrstaUgovoraId = vrsta.VrstaUgovoraId,
            BrojRadnika = brojRadnika,
            TipPrimaoca = tip,
            Predmet = "Izrada elaborata",
            UgovorenIznos = iznos
        };

        db.Ugovori.Add(ugovor);
        db.SaveChanges();
        return ugovor;
    }

    private static Isplata DodajIsplatu(PlataDbContext db)
        => new IsplataService(db).Obezbedi(Godina, Mesec);

    // ── Računica ─────────────────────────────────────────────────────

    /// <summary>
    /// Objavljeni primer: bruto 50.000 → normirani troškovi 10.000 → osnovica 40.000 →
    /// porez 8.000 i PIO 9.600 → neto 32.400.
    /// </summary>
    [Fact]
    public void UgovorODelu_BrutoPedesetHiljada_DajeObjavljeniNeto()
    {
        var racunica = UgovorObracunService.Izracunaj(VrstaUgovorODelu(), 50000m);

        Assert.Equal(10000m, racunica.NormiraniTroskovi);
        Assert.Equal(40000m, racunica.Osnovica);
        Assert.Equal(8000m, racunica.Porez);
        Assert.Equal(9600m, racunica.PioPrimalac);
        Assert.Equal(0m, racunica.ZdravstvoPrimalac);
        Assert.Equal(32400m, racunica.Neto);

        // Ugovor o delu nema doprinose na teret isplatioca, pa je trošak jednak brutu.
        Assert.Equal(0m, racunica.DoprinosiIsplatioca);
        Assert.Equal(50000m, racunica.UkupanTrosak);
    }

    [Fact]
    public void AutorskaNaknada_NormiraniTroskoviPolovina_SmanjujuOsnovicu()
    {
        var racunica = UgovorObracunService.Izracunaj(VrstaAutorski50(), 100000m);

        Assert.Equal(50000m, racunica.NormiraniTroskovi);
        Assert.Equal(50000m, racunica.Osnovica);
        Assert.Equal(10000m, racunica.Porez);
        Assert.Equal(12000m, racunica.PioPrimalac);
        Assert.Equal(78000m, racunica.Neto);
    }

    /// <summary>
    /// Privremeni i povremeni poslovi se oporezuju kao zarada: bez normiranih troškova, i sa
    /// doprinosima podeljenim na primaoca i isplatioca — pa isplatilac ima trošak preko bruta.
    /// </summary>
    [Fact]
    public void PrivremeniPoslovi_DoprinosiSeDeleNaPrimaocaIIsplatioca()
    {
        var racunica = UgovorObracunService.Izracunaj(VrstaPrivremeniPoslovi(), 60000m);

        Assert.Equal(0m, racunica.NormiraniTroskovi);
        Assert.Equal(60000m, racunica.Osnovica);
        Assert.Equal(6000m, racunica.Porez);
        Assert.Equal(8400m, racunica.PioPrimalac);
        Assert.Equal(3090m, racunica.ZdravstvoPrimalac);
        Assert.Equal(450m, racunica.NezaposlenostPrimalac);
        Assert.Equal(42060m, racunica.Neto);

        Assert.Equal(6000m, racunica.PioIsplatilac);
        Assert.Equal(3090m, racunica.ZdravstvoIsplatilac);
        Assert.Equal(69090m, racunica.UkupanTrosak);
    }

    /// <summary>
    /// Preračun mora biti inverzan obračunu, i to na dinar — inače bi primalac dobio drugi
    /// iznos od ugovorenog, a razlika bi se videla tek na izvodu.
    /// </summary>
    [Theory]
    [InlineData(32400)]
    [InlineData(50000)]
    [InlineData(12345.67)]
    [InlineData(1)]
    public void BrutoIzNeta_VracaTacnoUgovoreniNeto(decimal neto)
    {
        var vrsta = VrstaUgovorODelu();

        decimal bruto = UgovorObracunService.BrutoIzNeta(vrsta, neto);

        Assert.Equal(neto, UgovorObracunService.Izracunaj(vrsta, bruto).Neto);
    }

    [Fact]
    public void BrutoIzNeta_ZaAutorskiIPrivremenePoslove_TakodjeVracaTacanNeto()
    {
        foreach (var vrsta in new[] { VrstaAutorski50(), VrstaPrivremeniPoslovi() })
        {
            decimal bruto = UgovorObracunService.BrutoIzNeta(vrsta, 75000m);
            Assert.Equal(75000m, UgovorObracunService.Izracunaj(vrsta, bruto).Neto);
        }
    }

    /// <summary>
    /// Stope žive u šifarniku, pa izmena propisa menja rezultat bez izmene koda. Test to
    /// fiksira: ista bruto naknada uz drugu stopu daje drugi neto.
    /// </summary>
    [Fact]
    public void IzmenaStopeUSifarniku_MenjaRezultatBezIzmeneKoda()
    {
        var vrsta = VrstaUgovorODelu();
        decimal netoPo24 = UgovorObracunService.Izracunaj(vrsta, 50000m).Neto;

        vrsta.StopaPioPrimalac = 25.5m;
        decimal netoPo255 = UgovorObracunService.Izracunaj(vrsta, 50000m).Neto;

        Assert.Equal(32400m, netoPo24);
        Assert.Equal(31800m, netoPo255);
    }

    // ── Šifra vrste prihoda ──────────────────────────────────────────

    /// <summary>
    /// Šifra se sastavlja po strukturi V-PP-OVP-OL-B: verzija kataloga, tip primaoca,
    /// oznaka vrste prihoda, olakšica i beneficirani staž.
    /// </summary>
    [Fact]
    public void Svp_SeSastavljaOdTipaPrimaocaIOvpOznake()
    {
        Assert.Equal("101601000", SvpService.Sastavi(TipPrimaocaPrihoda.Zaposleni, "601"));
        Assert.Equal("105602000", SvpService.Sastavi(TipPrimaocaPrihoda.NijeOsiguranPoDrugomOsnovu, "602"));
        Assert.Equal("101301000", SvpService.Sastavi(TipPrimaocaPrihoda.Zaposleni, "301"));

        // Struktura je ista kao kod zarade, što potvrđuje da je razlaganje ispravno.
        Assert.Equal(SvpService.RedovnaZarada, SvpService.Sastavi(TipPrimaocaPrihoda.Zaposleni, "101"));
    }

    /// <summary>
    /// Bez potvrđenog OVP-a se šifra ne izmišlja. Izmišljena bi prošla generisanje i pala
    /// tek kod Poreske uprave — a dotle je novac već isplaćen.
    /// </summary>
    [Fact]
    public void Svp_BezOvpOznake_OstajePrazan()
    {
        Assert.Equal("", SvpService.Sastavi(TipPrimaocaPrihoda.Zaposleni, ""));
        Assert.Equal("", SvpService.Sastavi(TipPrimaocaPrihoda.Zaposleni, "60"));
        Assert.Equal("", SvpService.Sastavi(TipPrimaocaPrihoda.Zaposleni, "6O1"));
    }

    [Fact]
    public void Odredi_ZaObracunPoUgovoru_UzimaSifruIzUgovoraANeIzRadnogMesta()
    {
        var vrsta = VrstaUgovorODelu();
        var obracun = new ObracunPlate
        {
            // Radno mesto nosi šifru zarade; ugovor mora da je nadjača.
            Radnik = new Radnik { Radno_Mesto = SvpService.RedovnaZarada },
            Ugovor = new Ugovor { TipPrimaoca = TipPrimaocaPrihoda.Zaposleni, VrstaUgovora = vrsta },
            UgovorId = 1
        };

        Assert.Equal("101601000", SvpService.Odredi(obracun));
    }

    // ── Upis obračuna ────────────────────────────────────────────────

    [Fact]
    public void Obracunaj_UpisujeObracunVezanZaIsplatuIUgovor()
    {
        using var db = NoviKontekst();
        var vrsta = VrstaUgovorODelu();
        var ugovor = DodajUgovor(db, vrsta);
        var isplata = DodajIsplatu(db);

        var rezultat = new UgovorObracunService(db)
            .Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, iznosJeNeto: false);

        Assert.True(rezultat.Uspesno, rezultat.Poruka);

        var obracun = db.ObracuniPlata.Single();
        Assert.Equal(ugovor.UgovorId, obracun.UgovorId);
        Assert.Equal(isplata.IsplataId, obracun.IsplataId);
        Assert.Equal(50000m, obracun.BrutoZarada);
        Assert.Equal(40000m, obracun.PoreskaOsnovica);
        Assert.Equal(40000m, obracun.OsnovicaDoprinosa);
        Assert.Equal(8000m, obracun.PorezNaDohodak);
        Assert.Equal(9600m, obracun.DoprinosPioRadnik);
        Assert.Equal(32400m, obracun.NetoIsplata);

        // Naknada se ne meri satima; nula u tim poljima je ono što ide i u prijavu.
        Assert.Equal(0, obracun.UkupnoSati);
        Assert.True(obracun.JeVanRadnogOdnosa);
    }

    [Fact]
    public void Obracunaj_KadJeIznosNeto_PrimalacDobijaTacnoUgovoreniIznos()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, VrstaUgovorODelu());
        var isplata = DodajIsplatu(db);

        var rezultat = new UgovorObracunService(db)
            .Obracunaj(ugovor.UgovorId, isplata.IsplataId, 32400m, iznosJeNeto: true);

        Assert.True(rezultat.Uspesno, rezultat.Poruka);
        Assert.Equal(32400m, db.ObracuniPlata.Single().NetoIsplata);
        Assert.Equal(50000m, db.ObracuniPlata.Single().BrutoZarada);
    }

    /// <summary>
    /// Dva obračuna po istom ugovoru u istoj isplati daju dva reda za isto lice u jednoj
    /// PPP-PD prijavi — Poreska uprava to odbija.
    /// </summary>
    [Fact]
    public void Obracunaj_DvaputUIstojIsplati_NijeDozvoljeno()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, VrstaUgovorODelu());
        var isplata = DodajIsplatu(db);
        var servis = new UgovorObracunService(db);

        Assert.True(servis.Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false).Uspesno);

        var drugi = servis.Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false);

        Assert.False(drugi.Uspesno);
        Assert.Single(db.ObracuniPlata);
    }

    /// <summary>
    /// Isti ugovor se sme isplatiti u ratama — svaka u svojoj isplati, sa svojom prijavom.
    /// </summary>
    [Fact]
    public void Obracunaj_UDveIsplateIstogMeseca_DajeDvaObracuna()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, VrstaUgovorODelu());
        var isplateServis = new IsplataService(db);
        var prva = isplateServis.Obezbedi(Godina, Mesec);
        var druga = isplateServis.Dodaj(Godina, Mesec, VrstaIsplate.Ostalo, "Druga rata", new DateTime(Godina, Mesec, 20));

        var servis = new UgovorObracunService(db);
        Assert.True(servis.Obracunaj(ugovor.UgovorId, prva.IsplataId, 25000m, false).Uspesno);
        Assert.True(servis.Obracunaj(ugovor.UgovorId, druga.Isplata!.IsplataId, 25000m, false).Uspesno);

        Assert.Equal(2, db.ObracuniPlata.Count());
        Assert.Equal(50000m, db.ObracuniPlata.Sum(o => o.BrutoZarada));
    }

    /// <summary>
    /// Karton primaoca je periodičan, a ugovor nije — kad se naknada isplaćuje u mesecu bez
    /// kartona, on se prepisuje iz poslednjeg ranijeg.
    /// </summary>
    [Fact]
    public void ObezbediKarton_ZaMesecBezKartona_PrepisujePoslednjiRaniji()
    {
        using var db = NoviKontekst();
        DodajUgovor(db, VrstaUgovorODelu());

        var karton = new UgovorObracunService(db).ObezbediKarton(1, Godina, Mesec + 2);

        Assert.NotNull(karton);
        Assert.Equal("Primalac 1", karton!.ImeIPrezime);
        Assert.Equal("0101990710016", karton.Jmbg);
        Assert.True(karton.VanRadnogOdnosa);
        Assert.Equal(Mesec + 2, karton.Mesec);
    }

    // ── PPP-PD prijava ───────────────────────────────────────────────

    private static XDocument Prijava(PlataDbContext db)
    {
        var obracuni = db.ObracuniPlata
            .Include(o => o.Radnik)
            .Include(o => o.Ugovor!).ThenInclude(u => u.VrstaUgovora)
            .ToList();

        string xml = new XmlExportService().GeneratePppPdXml(
            obracuni, new DateTime(Godina, Mesec, 30),
            "100000001", "12345678", "TEST DOO", "013", "011/000-000", "Ulica 1", "test@test.rs");

        return XDocument.Parse(xml);
    }

    private static XNamespace Tns => "http://pid.purs.gov.rs";

    [Fact]
    public void PppPd_NaknadaUlaziSaSvojomSifromOsnovicomIBezSati()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, VrstaUgovorODelu());
        var isplata = DodajIsplatu(db);

        new UgovorObracunService(db).Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false);

        var prihod = Prijava(db).Descendants(Tns + "PodaciOPrihodima").Single();

        Assert.Equal("101601000", prihod.Element(Tns + "SVP")!.Value);
        Assert.Equal("50000.00", prihod.Element(Tns + "Bruto")!.Value);
        Assert.Equal("40000.00", prihod.Element(Tns + "OsnovicaPorez")!.Value);
        Assert.Equal("8000.00", prihod.Element(Tns + "Porez")!.Value);

        // Osnovica doprinosa je upisana, a ne izvedena po stopi zarade — po njoj bi ispalo
        // 40.000 / 0,24 = 166.666,67.
        Assert.Equal("40000.00", prihod.Element(Tns + "OsnovicaDoprinosi")!.Value);
        Assert.Equal("9600.00", prihod.Element(Tns + "PIO")!.Value);

        Assert.Equal("0", prihod.Element(Tns + "BrojKalendarskihDana")!.Value);
        Assert.Equal("0", prihod.Element(Tns + "BrojEfektivnihSati")!.Value);
        Assert.Equal("0", prihod.Element(Tns + "MesecniFondSati")!.Value);
    }

    /// <summary>
    /// Kontrolni test: uvođenje naknada ne sme da promeni nijedan broj zarade u istoj
    /// prijavi. Taj test hvata više grešaka nego onaj koji proverava novo pravilo.
    /// </summary>
    [Fact]
    public void PppPd_ZaradaUIstojPrijavi_OstajeNepromenjena()
    {
        using var db = NoviKontekst();

        db.Radnici.Add(new Radnik
        {
            BrojRadnika = 9,
            ImeIPrezime = "Zaposleni Devet",
            Jmbg = "0101990710016",
            Radno_Mesto = SvpService.RedovnaZarada,
            SifraOpstine = "013",
            Godina = Godina,
            Mesec = Mesec
        });
        db.SaveChanges();

        var zaposleni = db.Radnici.Single(r => r.BrojRadnika == 9);
        db.ObracuniPlata.Add(new ObracunPlate
        {
            RadnikId = zaposleni.Id,
            Godina = Godina,
            Mesec = Mesec,
            BrutoZarada = 100000m,
            PoreskaOsnovica = 71577m,
            PorezNaDohodak = 7157.70m,
            DoprinosPioRadnik = 14000m,
            DoprinosPioPoslodavac = 10000m,
            DoprinosZdravstvoRadnik = 5150m,
            DoprinosZdravstvoPoslodavac = 5150m,
            RedovniSati = 176,
            NetoIsplata = 73692.30m
        });
        db.SaveChanges();

        var samoZarada = Prijava(db).Descendants(Tns + "PodaciOPrihodima").Single();
        string osnovicaDoprinosaPre = samoZarada.Element(Tns + "OsnovicaDoprinosi")!.Value;
        string sviPre = samoZarada.ToString();

        // Naknada po ugovoru ulazi u istu prijavu.
        var ugovor = DodajUgovor(db, VrstaUgovorODelu());
        var isplata = DodajIsplatu(db);
        new UgovorObracunService(db).Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false);

        var prihodi = Prijava(db).Descendants(Tns + "PodaciOPrihodima").ToList();
        Assert.Equal(2, prihodi.Count);

        var zaradaPosle = prihodi.Single(p => p.Element(Tns + "SVP")!.Value == SvpService.RedovnaZarada);

        // Osnovica doprinosa zarade se i dalje izvodi iz zbira PIO doprinosa: 24.000 / 0,24.
        Assert.Equal("100000.00", osnovicaDoprinosaPre);
        Assert.Equal(sviPre, zaradaPosle.ToString());
    }

    // ── Nalozi za prenos ─────────────────────────────────────────────

    /// <summary>
    /// Naknada ide na račun primaoca kao i zarada, ali sa svrhom po kojoj se na izvodu vidi
    /// šta je isplaćeno — a šifru plaćanja daje šifarnik, jer je propisuje NBS.
    /// </summary>
    [Fact]
    public void Nalozi_NaknadaNosiSvrhuPoUgovoruISifruPlacanjaIzSifarnika()
    {
        using var db = NoviKontekst();
        var vrsta = VrstaUgovorODelu();
        vrsta.SifraPlacanja = "241";

        var ugovor = DodajUgovor(db, vrsta);
        ugovor.Broj = "12/2026";
        db.SaveChanges();

        var isplata = DodajIsplatu(db);
        new UgovorObracunService(db).Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false);

        var paket = new NalogZaPrenosService(db)
            .Pripremi(Godina, Mesec, null, new DateTime(Godina, Mesec, 30), isplata);

        var nalog = paket.Nalozi.Single(n => n.Vrsta == VrstaNaloga.NetoZarada);

        Assert.Equal(32400m, nalog.Iznos);
        Assert.Equal("241", nalog.SifraPlacanja);
        Assert.Contains("Izrada elaborata", nalog.Svrha, StringComparison.Ordinal);
        Assert.Contains("12/2026", nalog.Svrha, StringComparison.Ordinal);
    }

    // ── Kontrolne provere ────────────────────────────────────────────

    [Fact]
    public void Provera_VrstaUgovoraBezOvpOznake_JeGreska()
    {
        using var db = NoviKontekst();
        var vrsta = VrstaUgovorODelu();
        vrsta.Ovp = "";

        var ugovor = DodajUgovor(db, vrsta);
        var isplata = DodajIsplatu(db);
        var servis = new UgovorObracunService(db);
        servis.Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false);

        var nalazi = servis.Proveri(Godina, Mesec);

        Assert.Contains(nalazi, n => n.Tezina == TezinaNalaza.Greska
                                     && n.Provera == "Vrsta ugovora bez oznake vrste prihoda");
    }

    /// <summary>
    /// Naknada nema sate ni fond, pa se provere zarade na nju ne primenjuju — inače bi
    /// svaka isplata po ugovoru prijavila „bruto ispod najniže osnovice" i zaustavila
    /// zaključavanje perioda.
    /// </summary>
    [Fact]
    public void PreFlight_NaknadaNePodlezeProveramaZarade()
    {
        using var db = NoviKontekst();
        db.Doprinosi.Add(new Doprinos { Godina = Godina, Mesec = Mesec, NajnizaOsnovica = 51297m });
        db.SaveChanges();

        var ugovor = DodajUgovor(db, VrstaUgovorODelu());
        var isplata = DodajIsplatu(db);
        new UgovorObracunService(db).Obracunaj(ugovor.UgovorId, isplata.IsplataId, 20000m, false);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Bruto ispod najniže osnovice");
        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Nedostaje e-mail");
        Assert.True(rezultat.SmeSeZakljucati);
    }

    [Fact]
    public void PreFlight_PrimalacBezTekucegRacuna_JeGreska()
    {
        using var db = NoviKontekst();
        var ugovor = DodajUgovor(db, VrstaUgovorODelu());

        var karton = db.Radnici.Single(r => r.BrojRadnika == 1);
        karton.BankovniRacun = "";
        db.SaveChanges();

        var isplata = DodajIsplatu(db);
        new UgovorObracunService(db).Obracunaj(ugovor.UgovorId, isplata.IsplataId, 50000m, false);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.Contains(rezultat.Nalazi, n => n.Tezina == TezinaNalaza.Greska
                                              && n.Provera == "Nedostaje tekući račun");
    }

    // ── Šifarnik ─────────────────────────────────────────────────────

    /// <summary>
    /// Podrazumevani šifarnik mora da pokrije sve tipove iz Faze 2.3, jer je to ono što
    /// korisnik zatiče pri prvom pokretanju.
    /// </summary>
    [Fact]
    public void Sifarnik_PodrazumevaneVrste_PokrivajuSveTipoveIzFaze()
    {
        var vrste = VrsteUgovoraSeed.Podrazumevane();

        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.UgovorODelu && v.Ovp == "601");
        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.NaknadaOdboru);
        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.Autorski50 && v.NormiraniTroskoviProcenat == 50m);
        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.Autorski43 && v.NormiraniTroskoviProcenat == 43m);
        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.Autorski34 && v.NormiraniTroskoviProcenat == 34m);
        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.PrivremeniPoslovi && v.StopaPoreza == 10m);
        Assert.Contains(vrste, v => v.Sifra == VrsteUgovoraSeed.PrivremeniZadruga);

        // Šifre su ono po čemu ih kod traži, pa dve iste čine šifarnik dvosmislenim.
        Assert.Equal(vrste.Count, vrste.Select(v => v.Sifra).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // Vrsta bez potvrđenog OVP-a mora reći zašto je prazan.
        foreach (var bezOvp in vrste.Where(v => string.IsNullOrWhiteSpace(v.Ovp)))
            Assert.False(string.IsNullOrWhiteSpace(bezOvp.Napomena));
    }
}
