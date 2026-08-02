using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Prelazak na model „vrsta primanja + stavke obračuna" je najveći pojedinačni zahvat u
/// planu, a kriterijum je da <b>postojeći obračuni daju identičan rezultat</b>. Testovi to
/// drže doslovno: stavke su razlaganje istog zbira, ne novi obračun.
/// </summary>
public class ObracunStavkeTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;
    private const int Fond = 176;
    private const decimal VrednostBoda = 10000m;

    private static PlataDbContext NoviKontekst(bool saSifarnikom = true)
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);

        db.Porezi.Add(new Porezi
        {
            Godina = Godina,
            Mesec = Mesec,
            RedniBroj = 1,
            ProcMinul = 0.40m,
            ProcPreko = 26.00m,
            ProcNocni = 26.00m,
            ProcDrzav = 110.00m,
            ProcBolov = 65.00m,
            ProcNedel = 0.00m,
            AkPorez = 10.00m,
            Prvast = 28423.00m
        });

        if (saSifarnikom)
            db.VrstePrimanja.AddRange(VrstePrimanjaSeed.Podrazumevane());

        db.SaveChanges();
        return db;
    }

    private static Radnik DodajRadnika(PlataDbContext db, int minuliRadGodine = 0)
    {
        var radnik = new Radnik
        {
            Id = 1,
            BrojRadnika = 1,
            ImeIPrezime = "Test Radnik",
            Jmbg = "0101990710016",
            Koeficijent = 7.04m,          // 400 RSD/h uz fond 176 i vrednost boda 10.000
            MinuliRadGodine = minuliRadGodine,
            Godina = Godina,
            Mesec = Mesec
        };
        db.Radnici.Add(radnik);
        db.SaveChanges();
        return radnik;
    }

    private static ObracunPlate Obracunaj(PlataDbContext db, Radnik radnik, RadniSat sati)
        => new ObracunService(db).Calculate(radnik, sati, Godina, Mesec, VrednostBoda, Fond);

    private static RadniSat Sati(Action<RadniSat>? podesi = null)
    {
        var sati = new RadniSat { RadnikId = 1, Godina = Godina, Mesec = Mesec, RedovniSati = Fond, Prosek = 400m };
        podesi?.Invoke(sati);
        return sati;
    }

    /// <summary>
    /// Kriterijum „gotovo" iz razvojne mape: zbir stavki mora biti jednak ukupnom bruto
    /// iznosu obračuna. Ako se raziđu, listić i prijava govore različite iznose.
    /// </summary>
    [Fact]
    public void Stavke_ZbirJednakUkupnomBrutoIznosu()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, minuliRadGodine: 10);

        var obracun = Obracunaj(db, radnik, Sati(s =>
        {
            s.RedovniSati = 160;
            s.PrekovremeneSati = 8;
            s.NocniSati = 8;
            s.BolovanjeSati = 8;
            s.GodisnjiOdmorSati = 8;
            s.Stimulacija = 10m;
            s.TopliObrokDani = 5000;
            s.RegresIznos = 3000m;
            s.Varijabila = 2500m;
        }));

        decimal zbirStavki = obracun.Stavke.Sum(s => s.Iznos);

        Assert.Equal(obracun.UkupnoBruto, zbirStavki, precision: 1);
    }

    [Fact]
    public void Stavke_JednostavanObracun_DajeOsnovnuZaraduJednakuBrutu()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);

        var obracun = Obracunaj(db, radnik, Sati());

        var stavka = Assert.Single(obracun.Stavke);
        Assert.Equal(obracun.UkupnoBruto, stavka.Iznos);
        Assert.Equal(Fond, stavka.Sati);
    }

    /// <summary>Svaka komponenta mora da dobije svoju stavku, a ne da se stopi u zbirnu.</summary>
    [Fact]
    public void Stavke_SvakaKomponenta_DobijaSvojuVrstuPrimanja()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db, minuliRadGodine: 5);

        var obracun = Obracunaj(db, radnik, Sati(s =>
        {
            s.RedovniSati = 100;
            s.PrekovremeneSati = 10;
            s.NocniSati = 10;
            s.RadPraznikomSati = 8;
            s.RadNedeljomSati = 8;
            s.GodisnjiOdmorSati = 16;
            s.BolovanjeSati = 8;
            s.RegresIznos = 1000m;
        }));

        var sifre = SifreStavki(db, obracun);

        Assert.Contains(VrstePrimanjaSeed.OsnovnaZarada, sifre);
        Assert.Contains(VrstePrimanjaSeed.MinuliRad, sifre);
        Assert.Contains(VrstePrimanjaSeed.Prekovremeni, sifre);
        Assert.Contains(VrstePrimanjaSeed.NocniRad, sifre);
        Assert.Contains(VrstePrimanjaSeed.RadPraznikom, sifre);
        Assert.Contains(VrstePrimanjaSeed.RadNedeljom, sifre);
        Assert.Contains(VrstePrimanjaSeed.GodisnjiOdmor, sifre);
        Assert.Contains(VrstePrimanjaSeed.Bolovanje, sifre);
        Assert.Contains(VrstePrimanjaSeed.Regres, sifre);
    }

    /// <summary>Primanje bez iznosa i bez sati ne stoji na listiću.</summary>
    [Fact]
    public void Stavke_KomponenteBezIznosa_SeNeUpisuju()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);

        var obracun = Obracunaj(db, radnik, Sati());
        var sifre = SifreStavki(db, obracun);

        Assert.DoesNotContain(VrstePrimanjaSeed.Prekovremeni, sifre);
        Assert.DoesNotContain(VrstePrimanjaSeed.Regres, sifre);
        Assert.DoesNotContain(VrstePrimanjaSeed.Bolovanje, sifre);
    }

    [Fact]
    public void Stavke_NoseSateOdgovarajuceKomponente()
    {
        using var db = NoviKontekst();
        var radnik = DodajRadnika(db);

        var obracun = Obracunaj(db, radnik, Sati(s =>
        {
            s.RedovniSati = 160;
            s.PrekovremeneSati = 12;
        }));

        Assert.Equal(160, StavkaPoSifri(db, obracun, VrstePrimanjaSeed.OsnovnaZarada).Sati);
        Assert.Equal(12, StavkaPoSifri(db, obracun, VrstePrimanjaSeed.Prekovremeni).Sati);
    }

    /// <summary>
    /// Bez popunjenog šifarnika obračun i dalje mora da radi — stavke su dodatak, ne uslov.
    /// To je i put nadogradnje: baza dobija stavke tek pri sledećem obračunu.
    /// </summary>
    [Fact]
    public void Stavke_BezSifarnika_ObracunOstajeIspravan()
    {
        using var db = NoviKontekst(saSifarnikom: false);
        var radnik = DodajRadnika(db);

        var obracun = Obracunaj(db, radnik, Sati());

        Assert.Empty(obracun.Stavke);
        Assert.True(obracun.UkupnoBruto > 0);
        Assert.True(obracun.NetoIsplata > 0);
    }

    /// <summary>Uvođenje stavki ne sme da promeni nijedan postojeći iznos obračuna.</summary>
    [Fact]
    public void Stavke_NeMenjajuIznoseObracuna()
    {
        var sati = Sati(s =>
        {
            s.RedovniSati = 160;
            s.PrekovremeneSati = 8;
            s.RegresIznos = 3000m;
        });

        using var saSifarnikom = NoviKontekst();
        var obracunSa = Obracunaj(saSifarnikom, DodajRadnika(saSifarnikom), sati);

        using var bezSifarnika = NoviKontekst(saSifarnikom: false);
        var obracunBez = Obracunaj(bezSifarnika, DodajRadnika(bezSifarnika), sati);

        Assert.Equal(obracunBez.UkupnoBruto, obracunSa.UkupnoBruto);
        Assert.Equal(obracunBez.NetoIsplata, obracunSa.NetoIsplata);
        Assert.Equal(obracunBez.PorezNaDohodak, obracunSa.PorezNaDohodak);
        Assert.Equal(obracunBez.BrutoMinuliRad, obracunSa.BrutoMinuliRad);
    }

    // ── Šifarnik ─────────────────────────────────────────────────────

    [Fact]
    public void Sifarnik_SistemskeVrsteImajuJedinstveneSifre()
    {
        var vrste = VrstePrimanjaSeed.Podrazumevane();

        Assert.Equal(vrste.Count, vrste.Select(v => v.Sifra).Distinct().Count());
    }

    /// <summary>
    /// Svaka bruto komponenta koju engine razlaže mora imati vrstu u šifarniku — inače bi
    /// se stavka tiho izgubila i zbir se ne bi slagao.
    /// </summary>
    [Fact]
    public void Sifarnik_SadrziSveSistemskeSifreKojeEngineKoristi()
    {
        var sifre = VrstePrimanjaSeed.Podrazumevane().Select(v => v.Sifra).ToHashSet();

        string[] koristiEngine =
        [
            VrstePrimanjaSeed.OsnovnaZarada, VrstePrimanjaSeed.MinuliRad, VrstePrimanjaSeed.Prekovremeni,
            VrstePrimanjaSeed.NocniRad, VrstePrimanjaSeed.RadPraznikom, VrstePrimanjaSeed.NeradniPraznik,
            VrstePrimanjaSeed.RadNedeljom, VrstePrimanjaSeed.GodisnjiOdmor, VrstePrimanjaSeed.Bolovanje,
            VrstePrimanjaSeed.Bolovanje100, VrstePrimanjaSeed.BolovanjePreko30, VrstePrimanjaSeed.Porodiljsko,
            VrstePrimanjaSeed.PlacenoOdsustvo, VrstePrimanjaSeed.PlacenoZakonski, VrstePrimanjaSeed.Stimulacija,
            VrstePrimanjaSeed.TopliObrok, VrstePrimanjaSeed.Regres, VrstePrimanjaSeed.BrutoDodatak
        ];

        foreach (string sifra in koristiEngine)
            Assert.Contains(sifra, sifre);
    }

    /// <summary>Bolovanje nosi svoju SVP šifru, jer se u prijavi vodi odvojeno od zarade.</summary>
    [Fact]
    public void Sifarnik_BolovanjeImaSvojuSvpSifru()
    {
        var vrste = VrstePrimanjaSeed.Podrazumevane();

        Assert.Equal(SvpService.Bolovanje, vrste.Single(v => v.Sifra == VrstePrimanjaSeed.Bolovanje).Svp);
        Assert.Equal(SvpService.RedovnaZarada, vrste.Single(v => v.Sifra == VrstePrimanjaSeed.OsnovnaZarada).Svp);
    }

    /// <summary>Novo primanje se dodaje kao red u šifarniku, bez izmene šeme baze.</summary>
    [Fact]
    public void Sifarnik_NovaVrstaSeDodajeBezIzmeneSeme()
    {
        using var db = NoviKontekst();

        db.VrstePrimanja.Add(new VrstaPrimanja
        {
            Sifra = "NAG",
            Naziv = "Nagrada za rezultat",
            Svp = SvpService.RedovnaZarada,
            Oporezivo = true,
            UlaziUOsnovicuDoprinosa = true,
            Konto = "520"
        });
        db.SaveChanges();

        Assert.Contains(db.VrstePrimanja, v => v.Sifra == "NAG");
    }

    // ── Pomoćne ──────────────────────────────────────────────────────

    private static HashSet<string> SifreStavki(PlataDbContext db, ObracunPlate obracun)
    {
        var poId = db.VrstePrimanja.ToDictionary(v => v.VrstaPrimanjaId, v => v.Sifra);
        return obracun.Stavke.Select(s => poId[s.VrstaPrimanjaId]).ToHashSet();
    }

    private static ObracunStavka StavkaPoSifri(PlataDbContext db, ObracunPlate obracun, string sifra)
    {
        int id = db.VrstePrimanja.Single(v => v.Sifra == sifra).VrstaPrimanjaId;
        return obracun.Stavke.Single(s => s.VrstaPrimanjaId == id);
    }
}
