using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Isplate u mesecu (Faza 2.2). Testovi drže tri stvari koje se najlakše razmimoiđu:
///
/// 1. da mesec sa <b>jednom</b> isplatom daje brojčano isti rezultat kao pre Faze 2.2 —
///    taj kontrolni test hvata više grešaka nego onaj koji proverava novo pravilo;
/// 2. da obuhvat po isplati bude <b>svuda</b>, a ne samo tamo gde se prvo primeti —
///    nalozi, storniranje, prekalkulacija;
/// 3. da rata kredita ostane skinuta <b>tačno jednom</b> u mesecu, i kad se u njemu
///    isplaćuje i akontacija i konačna zarada.
/// </summary>
public class IsplataTests
{
    private const int Godina = 2026;
    private const int Mesec = 4;
    private static readonly DateTime DatumValute = new(2026, 5, 5);

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

    private static void DodajRadnika(PlataDbContext db, int id)
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
        db.SaveChanges();
    }

    private static ObracunPlate DodajObracun(PlataDbContext db, int id, int? isplataId = null, decimal neto = 50000m)
    {
        if (!db.Radnici.Any(r => r.Id == id)) DodajRadnika(db, id);

        var obracun = new ObracunPlate
        {
            RadnikId = id,
            IsplataId = isplataId,
            Godina = Godina,
            Mesec = Mesec,
            BrutoZarada = 80000m,
            NetoIsplata = neto,
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

    private static PppPdPrijava DodajPrijavu(PlataDbContext db, int redniBroj, string bop, decimal iznos)
    {
        var prijava = new PppPdPrijava
        {
            Godina = Godina,
            Mesec = Mesec,
            RedniBroj = redniBroj,
            Bop = bop,
            IznosZaUplatu = iznos,
            RacunZaUplatu = EPoreziImportService.PodrazumevaniRacunObjedinjeneNaplate,
            ModelPozivaNaBroj = EPoreziImportService.PodrazumevaniModel,
            Status = StatusPrijave.Prihvacena
        };

        db.PppPdPrijave.Add(prijava);
        db.SaveChanges();
        return prijava;
    }

    private static IsplataService Servis(PlataDbContext db) => new(db);

    // ── Kontrolni test ────────────────────────────────────────────────

    /// <summary>
    /// Bez ijedne dodatne isplate sve mora ostati brojčano isto kao pre Faze 2.2:
    /// obračuni nemaju upisanu isplatu, a prva isplata ih svejedno obuhvata.
    /// </summary>
    [Fact]
    public void JednaIsplata_ObuhvataIObracuneBezUpisaneIsplate()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);

        var prva = Servis(db).Obezbedi(Godina, Mesec);

        var obuhvaceni = IsplataService.Obuhvat(db.ObracuniPlata, Godina, Mesec, prva).ToList();

        Assert.Equal(2, obuhvaceni.Count);
        Assert.All(obuhvaceni, o => Assert.Null(o.IsplataId));
    }

    [Fact]
    public void BezDodatnihIsplata_NaloziOstajuNepromenjeni()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);

        var prva = Servis(db).Obezbedi(Godina, Mesec);
        var prijava = DodajPrijavu(db, 1, "9712345678901234A", 30000m);

        var sisplatom = new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijava, DatumValute, prva);
        var bezIsplate = new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijava, DatumValute);

        Assert.Equal(bezIsplate.Nalozi.Count, sisplatom.Nalozi.Count);
        Assert.Equal(bezIsplate.Ukupno, sisplatom.Ukupno);
        Assert.True(sisplatom.SmeSePoslatiUBanku);
    }

    // ── Obuhvat ───────────────────────────────────────────────────────

    [Fact]
    public void DrugaIsplata_ObuhvataSamoSvojeObracune()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "Godišnji bonus", new DateTime(Godina, Mesec, 20)).Isplata!;

        DodajObracun(db, 1, prva.IsplataId, neto: 50000m);
        DodajObracun(db, 1, druga.IsplataId, neto: 12000m);
        DodajObracun(db, 2, prva.IsplataId, neto: 40000m);

        var uPrvoj = IsplataService.Obuhvat(db.ObracuniPlata, Godina, Mesec, prva).ToList();
        var uDrugoj = IsplataService.Obuhvat(db.ObracuniPlata, Godina, Mesec, druga).ToList();

        Assert.Equal(2, uPrvoj.Count);
        Assert.Single(uDrugoj);
        Assert.Equal(12000m, uDrugoj[0].NetoIsplata);
    }

    [Fact]
    public void Nalozi_ZaDruguIsplatu_NeSadrzeZaradeIzPrve()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "", new DateTime(Godina, Mesec, 15)).Isplata!;

        DodajObracun(db, 1, prva.IsplataId, neto: 50000m);
        DodajObracun(db, 1, druga.IsplataId, neto: 20000m);

        var prijavaDruge = DodajPrijavu(db, 2, "9700000000000002B", 15000m);

        var paket = new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijavaDruge, DatumValute, druga);

        var zarade = paket.Nalozi.Where(n => n.Vrsta == VrstaNaloga.NetoZarada).ToList();
        Assert.Single(zarade);
        Assert.Equal(20000m, zarade[0].Iznos);
    }

    /// <summary>
    /// BOP tuđe prijave na nalogu ove isplate šalje novac na pogrešnu deklaraciju: tamo
    /// višak, ovde manjak. Zato paket mora stati.
    /// </summary>
    [Fact]
    public void Nalozi_SaPrijavomDrugeIsplate_PrijavljujuGresku()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 15)).Isplata!;

        DodajObracun(db, 1, druga.IsplataId, neto: 20000m);

        var prijavaPrve = DodajPrijavu(db, 1, "9700000000000001A", 15000m);

        var paket = new NalogZaPrenosService(db).Pripremi(Godina, Mesec, prijavaPrve, DatumValute, druga);

        Assert.Contains(paket.Nalazi, n => n.Provera == "Prijava ne pripada ovoj isplati");
        Assert.DoesNotContain(paket.Nalozi, n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata);
        Assert.False(paket.SmeSePoslatiUBanku);
    }

    [Fact]
    public void Storniranje_JedneIsplate_NeDiraObracunDruge()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "", new DateTime(Godina, Mesec, 15)).Isplata!;

        var uPrvoj = DodajObracun(db, 1, prva.IsplataId);
        var uDrugoj = DodajObracun(db, 1, druga.IsplataId, neto: 20000m);

        var rezultat = new StornoService(db).Storniraj(Godina, Mesec, 1, "Greška u akontaciji", druga);

        Assert.True(rezultat.Uspesno);
        Assert.Equal(1, rezultat.BrojObracuna);

        Assert.True(db.ObracuniPlata.Single(o => o.Id == uDrugoj.Id).Storniran);
        Assert.False(db.ObracuniPlata.Single(o => o.Id == uPrvoj.Id).Storniran);
    }

    // ── Obustave ──────────────────────────────────────────────────────

    /// <summary>
    /// Akontacija ne nosi obustave, pa joj se rata i ne vraća pri storniranju. Da se vraća,
    /// radnikov dug bi se smanjio bez ijednog dinara koji je otišao poveriocu.
    /// </summary>
    [Fact]
    public void Storniranje_Akontacije_NeVracaRatuKredita()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        servis.Obezbedi(Godina, Mesec);
        var akontacija = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "", new DateTime(Godina, Mesec, 15)).Isplata!;

        DodajObracun(db, 1, akontacija.IsplataId, neto: 20000m);

        db.Krediti.Add(new Kredit
        {
            Id = 1,
            RadnikId = 1,
            Opis = "Test kredit",
            UkupanIznos = 120000m,
            MesecnaRata = 10000m,
            BrojRata = 12,
            PlateneRate = 1,
            OstatakDuga = 110000m,
            DatumPocetka = new DateTime(Godina, Mesec, 1),
            Aktivan = true
        });
        db.SaveChanges();

        var rezultat = new StornoService(db).Storniraj(Godina, Mesec, 1, "Pogrešan iznos akontacije", akontacija);

        Assert.True(rezultat.Uspesno);
        Assert.Equal(0, rezultat.BrojKredita);

        var kredit = db.Krediti.Single();
        Assert.Equal(1, kredit.PlateneRate);
        Assert.Equal(110000m, kredit.OstatakDuga);
    }

    [Fact]
    public void ObracunBezObustava_NeOdbijaRatuKreditaOdNeta()
    {
        using var db = NoviKontekst();
        DodajRadnika(db, 1);

        db.Krediti.Add(new Kredit
        {
            RadnikId = 1,
            Opis = "Test kredit",
            UkupanIznos = 120000m,
            MesecnaRata = 10000m,
            BrojRata = 12,
            OstatakDuga = 120000m,
            DatumPocetka = new DateTime(Godina, 1, 1),
            Aktivan = true
        });
        db.SaveChanges();

        var radnik = db.Radnici.Single();
        var sati = new RadniSat { RadnikId = 1, Godina = Godina, Mesec = Mesec, RedovniSati = 176 };
        radnik.OsnovnaPlata = 100000m;

        var servis = new ObracunService(db);

        var saObustavama = servis.Calculate(radnik, sati, Godina, Mesec, 1860.34m, 176);
        var bezObustava = servis.Calculate(radnik, sati, Godina, Mesec, 1860.34m, 176, saObustavama: false);

        Assert.Equal(10000m, saObustavama.KreditObustava);
        Assert.Equal(0m, bezObustava.KreditObustava);

        // Razlika u netu je tačno rata — ništa drugo se ne menja.
        Assert.Equal(10000m, bezObustava.NetoIsplata - saObustavama.NetoIsplata);
    }

    /// <summary>
    /// Dve konačne zarade u mesecu značile bi da se ista rata kredita skine dvaput, pa se
    /// druga ne dozvoljava. Ovo je jedina zaštita koja to pravilo drži.
    /// </summary>
    [Fact]
    public void Dodavanje_DrugeKonacneZarade_SeOdbija()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        servis.Obezbedi(Godina, Mesec);

        var rezultat = servis.Dodaj(Godina, Mesec, VrstaIsplate.KonacnaZarada, "", new DateTime(Godina, Mesec, 25));

        Assert.False(rezultat.Uspesno);
        Assert.Single(servis.Isplate(Godina, Mesec));
    }

    // ── Šifarnik isplata ──────────────────────────────────────────────

    [Fact]
    public void Obezbedi_PraviTacnoJednuPrvuIsplatu()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var ponovo = servis.Obezbedi(Godina, Mesec);

        Assert.Equal(prva.IsplataId, ponovo.IsplataId);
        Assert.Equal(1, prva.RedniBroj);
        Assert.Equal(VrstaIsplate.KonacnaZarada, prva.Vrsta);
        Assert.Single(db.Isplate);
    }

    [Fact]
    public void Dodaj_DodeljujeSledeciRedniBroj()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "Akontacija", new DateTime(Godina, Mesec, 15));
        var treca = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "Bonus", new DateTime(Godina, Mesec, 20));

        Assert.Equal(2, druga.Isplata!.RedniBroj);
        Assert.Equal(3, treca.Isplata!.RedniBroj);

        // Prva se napravila sama, jer bez nje zatečeni obračuni ne bi imali svoju isplatu.
        Assert.Equal(3, servis.Isplate(Godina, Mesec).Count);
    }

    [Fact]
    public void Brisanje_IsplateSaObracunima_SeOdbija()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20)).Isplata!;
        DodajObracun(db, 1, druga.IsplataId);

        var rezultat = servis.Obrisi(druga.IsplataId);

        Assert.False(rezultat.Uspesno);
        Assert.Equal(2, servis.Isplate(Godina, Mesec).Count);
    }

    /// <summary>
    /// Brisanje isplate iz sredine pomerilo bi redne brojeve onih iza nje, a redni broj je
    /// ono po čemu se podneta PPP-PD prijava vezuje za isplatu.
    /// </summary>
    [Fact]
    public void Brisanje_IsplateKojaNijePoslednja_SeOdbija()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "", new DateTime(Godina, Mesec, 15)).Isplata!;
        servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20));

        var rezultat = servis.Obrisi(druga.IsplataId);

        Assert.False(rezultat.Uspesno);
        Assert.Equal(3, servis.Isplate(Godina, Mesec).Count);
    }

    [Fact]
    public void Brisanje_PrazenPoslednjeIsplate_Uspeva()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20)).Isplata!;

        var rezultat = servis.Obrisi(druga.IsplataId);

        Assert.True(rezultat.Uspesno);
        Assert.Single(servis.Isplate(Godina, Mesec));
    }

    [Fact]
    public void PoveziZatecene_UpisujePrvuIsplatuBezPromeneIznosa()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);
        DodajObracun(db, 2);

        var servis = Servis(db);
        var prva = servis.Obezbedi(Godina, Mesec);

        int povezano = servis.PoveziZatecene(Godina, Mesec);

        Assert.Equal(2, povezano);
        Assert.All(db.ObracuniPlata.ToList(), o =>
        {
            Assert.Equal(prva.IsplataId, o.IsplataId);
            Assert.Equal(50000m, o.NetoIsplata);
        });

        // Drugi poziv nema šta da poveže.
        Assert.Equal(0, servis.PoveziZatecene(Godina, Mesec));
    }

    // ── PPP-PD ────────────────────────────────────────────────────────

    [Fact]
    public void Akontacija_SePrijavljujeKaoNekonacnaIsplata()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var akontacija = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "", new DateTime(Godina, Mesec, 15)).Isplata!;

        Assert.Equal("K", prva.OznakaZaKonacnuIsplatu);
        Assert.Equal("A", akontacija.OznakaZaKonacnuIsplatu);
    }

    [Fact]
    public void PrijavaZa_NalaziPrijavuPoRednomBrojuIsplate()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20)).Isplata!;

        DodajPrijavu(db, 1, "9700000000000001A", 15000m);
        DodajPrijavu(db, 2, "9700000000000002B", 4000m);

        Assert.Equal("9700000000000001A", servis.PrijavaZa(prva)!.Bop);
        Assert.Equal("9700000000000002B", servis.PrijavaZa(druga)!.Bop);
    }

    // ── Verzije ───────────────────────────────────────────────────────

    /// <summary>
    /// Prekalkulacija jedne isplate ne sme da podigne redni broj verzije drugoj — one su
    /// zaseban tok, i posle prekalkulacije akontacije konačna isplata je i dalje prva verzija.
    /// </summary>
    [Fact]
    public void Verzije_SeBrojePoIsplati()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Akontacija, "", new DateTime(Godina, Mesec, 15)).Isplata!;

        var uPrvoj = DodajObracun(db, 1, prva.IsplataId);

        VerzijeObracunaService.Arhiviraj(db, [uPrvoj], "Prekalkulacija prve isplate");
        db.SaveChanges();

        Assert.Equal(2, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 1, prva));
        Assert.Equal(1, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 1, druga));
    }

    /// <summary>
    /// Arhiva nastala pre Faze 2.2 nema upisanu isplatu. Prva isplata je mora obuhvatiti —
    /// inače bi prvi obračun posle nadogradnje ponovo dobio verziju 1, koja je već potrošena.
    /// </summary>
    [Fact]
    public void Verzije_PrvaIsplata_ObuhvataArhivuBezIsplate()
    {
        using var db = NoviKontekst();

        var zatecen = DodajObracun(db, 1);
        zatecen.Verzija = 1;

        VerzijeObracunaService.Arhiviraj(db, [zatecen], "Prekalkulacija pre Faze 2.2");
        db.SaveChanges();

        var prva = Servis(db).Obezbedi(Godina, Mesec);

        Assert.Equal(2, VerzijeObracunaService.SledecaVerzija(db, Godina, Mesec, 1, prva));
    }

    // ── Pre-flight ────────────────────────────────────────────────────

    /// <summary>
    /// Radnik u mesecu sa dve isplate ima dva obračuna, ali u dve različite prijave — po jedan
    /// red u svakoj. To nije dupli obračun i ne sme da zaustavi zaključavanje.
    /// </summary>
    [Fact]
    public void PreFlight_IstiRadnikUDveIsplate_NijeDupliObracun()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20)).Isplata!;

        DodajObracun(db, 1, prva.IsplataId);
        DodajObracun(db, 1, druga.IsplataId, neto: 10000m);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.DoesNotContain(rezultat.Nalazi, n => n.Provera == "Dupli obračun");
    }

    /// <summary>Dva obračuna istog radnika u istoj isplati su i dalje greška.</summary>
    [Fact]
    public void PreFlight_DvaObracunaUIstojIsplati_JesteDupliObracun()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);

        DodajObracun(db, 1, prva.IsplataId);
        DodajObracun(db, 1, prva.IsplataId, neto: 10000m);

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.Contains(rezultat.Nalazi, n => n.Provera == "Dupli obračun");
    }

    // ── Kontrolne provere ─────────────────────────────────────────────

    [Fact]
    public void Provere_JednaIsplata_NemajuStaDaPrijave()
    {
        using var db = NoviKontekst();
        DodajObracun(db, 1);

        var servis = Servis(db);
        servis.Obezbedi(Godina, Mesec);

        Assert.Empty(servis.Proveri(Godina, Mesec));
    }

    [Fact]
    public void Provere_IsplataBezPrijave_JeGreska()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20)).Isplata!;

        DodajObracun(db, 1, prva.IsplataId);
        DodajObracun(db, 1, druga.IsplataId, neto: 10000m);

        DodajPrijavu(db, 1, "9700000000000001A", 15000m);

        var nalazi = servis.Proveri(Godina, Mesec);

        Assert.Contains(nalazi, n => n.Provera == "Isplata bez PPP-PD prijave" && n.Tezina == TezinaNalaza.Greska);
    }

    [Fact]
    public void Provere_IstiBopNaViseIsplata_JeGreska()
    {
        using var db = NoviKontekst();
        var servis = Servis(db);

        var prva = servis.Obezbedi(Godina, Mesec);
        var druga = servis.Dodaj(Godina, Mesec, VrstaIsplate.Bonus, "", new DateTime(Godina, Mesec, 20)).Isplata!;

        DodajObracun(db, 1, prva.IsplataId);
        DodajObracun(db, 1, druga.IsplataId, neto: 10000m);

        DodajPrijavu(db, 1, "9700000000000001A", 15000m);
        DodajPrijavu(db, 2, "9700000000000001A", 3000m);

        var nalazi = servis.Proveri(Godina, Mesec);

        Assert.Contains(nalazi, n => n.Provera == "Isti BOP na više isplata" && n.Tezina == TezinaNalaza.Greska);
    }
}
