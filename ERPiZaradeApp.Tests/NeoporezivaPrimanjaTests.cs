using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Kriterijum iz razvojne mape (2.5): „prekoračenje neoporezivog limita automatski prelazi
/// u oporezivi deo". Testovi drže i drugu stranu istog pravila — neoporezivi deo se
/// <b>isplaćuje</b> radniku, a da pritom ne ulazi ni u porez ni u doprinose.
/// </summary>
public class NeoporezivaPrimanjaTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;
    private const int Fond = 176;
    private const decimal VrednostBoda = 10000m;

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

        db.VrstePrimanja.AddRange(VrstePrimanjaSeed.Podrazumevane());
        db.Radnici.Add(new Radnik
        {
            Id = 1, BrojRadnika = 1, ImeIPrezime = "Test Radnik", Jmbg = "0101990710016",
            Koeficijent = 7.04m, Godina = Godina, Mesec = Mesec
        });

        db.SaveChanges();
        return db;
    }

    private static void PodesiVrstu(PlataDbContext db, string sifra, Action<VrstaPrimanja> podesi)
    {
        var vrsta = db.VrstePrimanja.Single(v => v.Sifra == sifra);
        podesi(vrsta);
        db.SaveChanges();
    }

    private static void Unesi(PlataDbContext db, string sifra, decimal iznos)
    {
        db.UnetaPrimanja.Add(new UnetoPrimanje
        {
            RadnikId = 1,
            Godina = Godina,
            Mesec = Mesec,
            VrstaPrimanjaId = db.VrstePrimanja.Single(v => v.Sifra == sifra).VrstaPrimanjaId,
            Iznos = iznos
        });
        db.SaveChanges();
    }

    private static ObracunPlate Obracunaj(PlataDbContext db)
    {
        var radnik = db.Radnici.Single(r => r.Id == 1);
        var sati = new RadniSat { RadnikId = 1, Godina = Godina, Mesec = Mesec, RedovniSati = Fond, Prosek = 400m };
        return new ObracunService(db).Calculate(radnik, sati, Godina, Mesec, VrednostBoda, Fond);
    }

    private static ObracunStavka Stavka(PlataDbContext db, ObracunPlate obracun, string sifra)
    {
        int id = db.VrstePrimanja.Single(v => v.Sifra == sifra).VrstaPrimanjaId;
        return obracun.Stavke.Single(s => s.VrstaPrimanjaId == id);
    }

    /// <summary>Ispod limita — ništa ne ulazi u osnovicu, ceo iznos se isplaćuje.</summary>
    [Fact]
    public void Primanje_IspodLimita_JeUCelostiNeoporezivo()
    {
        using var db = NoviKontekst();
        PodesiVrstu(db, "PRV", v => v.NeoporeziviLimit = 5000m);
        Unesi(db, "PRV", 4000m);

        var bez = ObracunBezPrimanja();
        var obracun = Obracunaj(db);
        var stavka = Stavka(db, obracun, "PRV");

        Assert.Equal(0m, stavka.OporeziviDeo);
        Assert.Equal(4000m, stavka.NeoporeziviDeo);

        // Porez i doprinosi ostaju isti kao da primanja nema, a neto raste za pun iznos.
        Assert.Equal(bez.PorezNaDohodak, obracun.PorezNaDohodak);
        Assert.Equal(bez.NetoIsplata + 4000m, obracun.NetoIsplata);
    }

    /// <summary>Preko limita — višak postaje oporeziv, ostatak ostaje neoporeziv.</summary>
    [Fact]
    public void Primanje_PrekoLimita_ViskovaPostajeOporeziv()
    {
        using var db = NoviKontekst();
        PodesiVrstu(db, "PRV", v => v.NeoporeziviLimit = 5000m);
        Unesi(db, "PRV", 8000m);

        var obracun = Obracunaj(db);
        var stavka = Stavka(db, obracun, "PRV");

        Assert.Equal(3000m, stavka.OporeziviDeo);
        Assert.Equal(5000m, stavka.NeoporeziviDeo);
    }

    [Fact]
    public void Primanje_PrekoLimita_PovecavaPorezIDoprinose()
    {
        using var db = NoviKontekst();
        PodesiVrstu(db, "PRV", v =>
        {
            v.NeoporeziviLimit = 5000m;
            v.UlaziUOsnovicuDoprinosa = true;
        });
        Unesi(db, "PRV", 8000m);

        var bez = ObracunBezPrimanja();
        var obracun = Obracunaj(db);

        Assert.True(obracun.PorezNaDohodak > bez.PorezNaDohodak);
        Assert.True(obracun.DoprinosPioRadnik > bez.DoprinosPioRadnik);
    }

    /// <summary>
    /// Primanje koje se oporezuje ali ne ulazi u osnovicu doprinosa mora da podigne porez,
    /// a doprinose ne.
    /// </summary>
    [Fact]
    public void Primanje_OporezivoAliVanOsnoviceDoprinosa_NePovecavaDoprinose()
    {
        using var db = NoviKontekst();
        PodesiVrstu(db, "JUB", v =>
        {
            v.NeoporeziviLimit = 1000m;
            v.UlaziUOsnovicuDoprinosa = false;
        });
        Unesi(db, "JUB", 6000m);

        var bez = ObracunBezPrimanja();
        var obracun = Obracunaj(db);

        Assert.True(obracun.PorezNaDohodak > bez.PorezNaDohodak);
        Assert.Equal(bez.DoprinosPioRadnik, obracun.DoprinosPioRadnik);
        Assert.Equal(bez.DoprinosZdravstvoRadnik, obracun.DoprinosZdravstvoRadnik);
    }

    /// <summary>Limit nula znači da gornje granice nema — ceo iznos ostaje neoporeziv.</summary>
    [Fact]
    public void Primanje_BezUnetogLimita_JeUCelostiNeoporezivo()
    {
        using var db = NoviKontekst();
        Unesi(db, "SOL", 20000m);

        var obracun = Obracunaj(db);
        var stavka = Stavka(db, obracun, "SOL");

        Assert.Equal(0m, stavka.OporeziviDeo);
        Assert.Equal(20000m, stavka.NeoporeziviDeo);
    }

    /// <summary>Takva vrsta se prijavljuje, da limit iz propisa ne bi ostao neunet.</summary>
    [Fact]
    public void PreFlight_NeoporezivoPrimanjeBezLimita_JeUpozorenje()
    {
        using var db = NoviKontekst();
        Unesi(db, "SOL", 20000m);

        db.ObracuniPlata.Add(new ObracunPlate
        {
            Id = 1, RadnikId = 1, Godina = Godina, Mesec = Mesec,
            BrutoZarada = 80000m, NetoIsplata = 55000m, RedovniSati = Fond, FondSatiMesecni = Fond
        });
        db.SaveChanges();

        var rezultat = new PreFlightService(db).Proveri(Godina, Mesec);

        Assert.Contains(rezultat.Nalazi,
            n => n.Provera == "Neoporezivo primanje bez limita" && n.Tezina == TezinaNalaza.Upozorenje);
    }

    /// <summary>Oporeziva vrsta se oporezuje u punom iznosu, bez obzira na limit.</summary>
    [Fact]
    public void Primanje_OporezivaVrsta_JeOporezivoUPunomIznosu()
    {
        using var db = NoviKontekst();
        PodesiVrstu(db, "PRV", v =>
        {
            v.Oporezivo = true;
            v.NeoporeziviLimit = 5000m;
        });
        Unesi(db, "PRV", 8000m);

        var obracun = Obracunaj(db);
        var stavka = Stavka(db, obracun, "PRV");

        Assert.Equal(8000m, stavka.OporeziviDeo);
        Assert.Equal(0m, stavka.NeoporeziviDeo);
    }

    /// <summary>
    /// „Već isplaćeno van obračuna" (Faza 3.2 — npr. prekoračenje dnevnice iz putnog naloga):
    /// iznos mora ući u bruto i osnovicu doprinosa kao svako oporezivo primanje, ali se NE sme
    /// isplatiti drugi put kroz platni spisak — radnik ga je već primio. Neto zato ne raste za
    /// pun iznos (kao kod običnog primanja), nego pada tačno za porez i doprinose koje to
    /// primanje dodaje — to je ono što drži da isti novac ne ode radniku dvaput.
    /// </summary>
    [Fact]
    public void Primanje_VecIsplacenoVanObracuna_UlaziUOsnovicuAliSeNeIsplacujeDrugiPut()
    {
        using var db = NoviKontekst();
        Unesi(db, VrstePrimanjaSeed.DnevnicaPrekoracenje, 8000m);

        var bez = ObracunBezPrimanja();
        var obracun = Obracunaj(db);
        var stavka = Stavka(db, obracun, VrstePrimanjaSeed.DnevnicaPrekoracenje);

        // Ceo iznos je oporeziv (limit je već primenjen na strani ERPiFinansije).
        Assert.Equal(8000m, stavka.OporeziviDeo);
        Assert.Equal(0m, stavka.NeoporeziviDeo);

        // Ulazi u poresku osnovicu i osnovicu doprinosa — PPP-PD i doprinosi rastu.
        Assert.True(obracun.PorezNaDohodak > bez.PorezNaDohodak);
        Assert.True(obracun.DoprinosPioRadnik > bez.DoprinosPioRadnik);

        // Ne isplaćuje se drugi put: neto ne raste za 8000, nego pada tačno za dodatni porez
        // i doprinose koje je to primanje dodalo — matematički identično kao da je 8000 bio
        // običan gross bonus isplaćen kroz platu (radnik zadržava 8000 minus poreska obaveza),
        // samo je 8000 već stiglo drugim kanalom (putni nalog), a poreska obaveza se poravnava
        // kroz ovaj obračun.
        decimal dodatniPorez = obracun.PorezNaDohodak - bez.PorezNaDohodak;
        decimal dodatniDoprinosiRadnik =
            (obracun.DoprinosPioRadnik - bez.DoprinosPioRadnik) +
            (obracun.DoprinosZdravstvoRadnik - bez.DoprinosZdravstvoRadnik) +
            (obracun.DoprinosNezaposlenostRadnik - bez.DoprinosNezaposlenostRadnik);

        Assert.Equal(bez.NetoIsplata - dodatniPorez - dodatniDoprinosiRadnik, obracun.NetoIsplata);
    }

    /// <summary>Bez unetih primanja obračun mora ostati identičan kao pre Faze 2.5.</summary>
    [Fact]
    public void Obracun_BezUnetihPrimanja_OstajeNepromenjen()
    {
        using var db = NoviKontekst();
        var obracun = Obracunaj(db);

        var bez = ObracunBezPrimanja();

        Assert.Equal(bez.UkupnoBruto, obracun.UkupnoBruto);
        Assert.Equal(bez.NetoIsplata, obracun.NetoIsplata);
        Assert.Equal(bez.PorezNaDohodak, obracun.PorezNaDohodak);
    }

    /// <summary>Polazno stanje: isti obračun bez ijednog unetog primanja.</summary>
    private static ObracunPlate ObracunBezPrimanja()
    {
        using var db = NoviKontekst();
        return Obracunaj(db);
    }
}
