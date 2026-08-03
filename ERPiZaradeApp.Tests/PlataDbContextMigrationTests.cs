using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Prelazak sa EnsureCreated() na EF migracije mora da bude bezbedan nad zatečenim
/// bazama korisnika — one su nastale pre migracija i nemaju __EFMigrationsHistory.
/// Ovi testovi rade nad stvarnim SQLite fajlovima, ne nad InMemory provajderom.
/// </summary>
public class PlataDbContextMigrationTests : IDisposable
{
    private readonly string _dir;

    public PlataDbContextMigrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plata_mig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string NovaPutanja() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".db");

    /// <summary>
    /// Podiže bazu na šemu prve migracije, pa briše istoriju migracija — čime nastaje
    /// tačno ono što je kod korisnika zatečeno: puna baza bez __EFMigrationsHistory.
    /// </summary>
    private static void NapraviZatecenuSemu(PlataDbContext ctx)
    {
        var prvaMigracija = ctx.Database.GetMigrations().First();
        ctx.GetService<IMigrator>().Migrate(prvaMigracija);
        ctx.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS __EFMigrationsHistory;");
    }

    /// <summary>
    /// Ubacuje obračun u zatečenu šemu. Spisak kolona se čita iz same baze: ObracuniPlata ima
    /// preko osamdeset NOT NULL kolona bez podrazumevane vrednosti, pa bi prepisan u test
    /// zastareo pri prvoj sledećoj migraciji.
    /// </summary>
    private static void UbaciObracunUZatecenuSemu(
        PlataDbContext ctx, int radnikId, int godina, int mesec, decimal neto)
    {
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();

        var kolone = new List<(string Ime, string Tip)>();

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(ObracuniPlata);";
            using var citac = pragma.ExecuteReader();
            while (citac.Read())
            {
                string ime = citac.GetString(1);
                if (ime == "Id") continue;
                kolone.Add((ime, citac.GetString(2).ToUpperInvariant()));
            }
        }

        static string Vrednost((string Ime, string Tip) k, int radnikId, int godina, int mesec, decimal neto)
            => k.Ime switch
            {
                "RadnikId" => radnikId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Godina" => godina.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Mesec" => mesec.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "NetoIsplata" => neto.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "DatumObracuna" => "'2026-02-28 00:00:00'",
                _ => k.Tip.Contains("TEXT", StringComparison.Ordinal) ? "''" : "0"
            };

        string sql =
            $"INSERT INTO ObracuniPlata ({string.Join(", ", kolone.Select(k => k.Ime))}) " +
            $"VALUES ({string.Join(", ", kolone.Select(k => Vrednost(k, radnikId, godina, mesec, neto)))});";

        ctx.Database.ExecuteSqlRaw(sql);
    }

    private static List<string> PrimenjeneMigracije(string putanja)
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseSqlite($"Data Source={putanja}")
            .Options;

        using var db = new PlataDbContext(options);
        return db.Database.GetAppliedMigrations().ToList();
    }

    [Fact]
    public void Create_NovaBaza_PrimenjujeMigracijeIUpisujeIstoriju()
    {
        string putanja = NovaPutanja();

        using (var db = PlataDbContext.Create(putanja))
        {
            Assert.True(db.Radnici.Count() == 0);
            // Podrazumevani sadržaj mora biti ubačen i u novoj bazi.
            Assert.True(db.PlatniRazredi.Any());
            Assert.True(db.Korisnici.Any(k => k.KorisnickoIme == "admin"));
        }

        var primenjene = PrimenjeneMigracije(putanja);
        Assert.NotEmpty(primenjene);
        Assert.Contains(primenjene, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ključni scenario: baza zatečena kod korisnika. Napravljena je preko EnsureCreated(),
    /// puna je podataka i nema istoriju migracija. Nakon nadogradnje podaci moraju ostati
    /// netaknuti, a baza mora dobiti žig početne migracije.
    /// </summary>
    [Fact]
    public void Create_ZatecenaBazaBezIstorije_CuvaPodatkeIZigosePocetnuMigraciju()
    {
        string putanja = NovaPutanja();

        // 1. Simulacija stare baze — šema iz vremena pre uvođenja migracija, bez istorije.
        //    Namerno se NE koristi EnsureCreated(): on uvek pravi šemu po današnjem modelu,
        //    pa bi fikstur bio noviji od `InitialCreate` i svaka nova migracija bi ga rušila.
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseSqlite($"Data Source={putanja}")
            .Options;

        using (var staraBaza = new PlataDbContext(options))
        {
            NapraviZatecenuSemu(staraBaza);

            // Upis ide sirovim SQL-om, jer bi EF pisao i kolone koje stara šema nema.
            staraBaza.Database.ExecuteSqlRaw(@"
                INSERT INTO Radnici
                    (Godina, Mesec, BrojRadnika, ImeIPrezime, Jmbg, MaticniBroj, MestoRodjenja,
                     AdresaStanovanja, Mesto, SifraOpstine, Kategorija, Radno_Mesto,
                     BrojRadneJedinice, MinuliRadGodine, Koeficijent, Koeficijent1, OsnovnaPlata,
                     StopaPio, StopaZdravstvo, StopaNezaposlenost, BankovniRacun, NazivBanke,
                     Aktivan, LicnoOslobodjenje, Operativni, DatumUnosa)
                VALUES
                    (2026, 1, 7, 'Mika Mikić', '0101990710014', '', '',
                     '', '', '', '', '',
                     1, 0, 2.5, 0, 0,
                     0, 0, 0, '', '',
                     1, 0, '', '2026-01-31 00:00:00');");
        }

        // Stanje pre nadogradnje: podaci postoje, istorija migracija ne.
        Assert.Empty(PrimenjeneMigracije(putanja));

        // 2. Nadogradnja — isti poziv koji aplikacija radi pri pokretanju.
        using (var db = PlataDbContext.Create(putanja))
        {
            var radnik = db.Radnici.SingleOrDefault(r => r.BrojRadnika == 7);
            Assert.NotNull(radnik);
            Assert.Equal("Mika Mikić", radnik.ImeIPrezime);
            Assert.Equal(2.5m, radnik.Koeficijent);

            // Naknadne migracije su se izvršile nad živim podacima: nova polja postoje,
            // a duplirano polje za zaključavanje je uklonjeno.
            radnik.Email = "mika@firma.rs";
            db.SaveChanges();
            Assert.True(db.PppPdPrijave.Count() == 0);
            Assert.True(db.ObracunAuditi.Count() == 0);
        }

        // 3. Baza je usvojena u sistem migracija bez ponovnog kreiranja tabela.
        var primenjene = PrimenjeneMigracije(putanja);
        Assert.Contains(primenjene, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
        Assert.Contains(primenjene, m => m.EndsWith("Faza0_ModelPodatakaIKontrole", StringComparison.Ordinal));
    }

    /// <summary>
    /// Faza 2.2: migracija mora zatečenim periodima dati njihovu prvu isplatu i povezati
    /// obračune sa njom. Bez toga bi ekran isplata za sve ranije mesece bio prazan, iako
    /// obračuni u njima postoje. Nijedan iznos se pri tome ne dira.
    /// </summary>
    [Fact]
    public void Create_ZatecenaBaza_PoveziObracuneSaPrvomIsplatom()
    {
        string putanja = NovaPutanja();

        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseSqlite($"Data Source={putanja}")
            .Options;

        using (var staraBaza = new PlataDbContext(options))
        {
            NapraviZatecenuSemu(staraBaza);

            // Id se zadaje eksplicitno: karton se ne može pročitati kroz EF dok je šema
            // zatečena, jer model već ima kolone koje ona nema.
            staraBaza.Database.ExecuteSqlRaw(@"
                INSERT INTO Radnici
                    (Id, Godina, Mesec, BrojRadnika, ImeIPrezime, Jmbg, MaticniBroj, MestoRodjenja,
                     AdresaStanovanja, Mesto, SifraOpstine, Kategorija, Radno_Mesto,
                     BrojRadneJedinice, MinuliRadGodine, Koeficijent, Koeficijent1, OsnovnaPlata,
                     StopaPio, StopaZdravstvo, StopaNezaposlenost, BankovniRacun, NazivBanke,
                     Aktivan, LicnoOslobodjenje, Operativni, DatumUnosa)
                VALUES
                    (11, 2026, 2, 3, 'Pera Perić', '0101990710014', '', '',
                     '', '', '', '', '',
                     1, 0, 2.5, 0, 0,
                     0, 0, 0, '', '',
                     1, 0, '', '2026-02-28 00:00:00');");

            UbaciObracunUZatecenuSemu(staraBaza, radnikId: 11, godina: 2026, mesec: 2, neto: 44444.44m);
        }

        using (var db = PlataDbContext.Create(putanja))
        {
            var isplata = db.Isplate.Single();
            Assert.Equal(2026, isplata.Godina);
            Assert.Equal(2, isplata.Mesec);
            Assert.Equal(1, isplata.RedniBroj);
            Assert.Equal(VrstaIsplate.KonacnaZarada, isplata.Vrsta);

            // Datum isplate je poslednji dan meseca — februar 2026. ima 28 dana.
            Assert.Equal(new DateTime(2026, 2, 28), isplata.DatumIsplate.Date);

            var obracun = db.ObracuniPlata.Single();
            Assert.Equal(isplata.IsplataId, obracun.IsplataId);

            // Migracija ne dira nijedan iznos.
            Assert.Equal(44444.44m, obracun.NetoIsplata);
        }
    }

    /// <summary>
    /// Faza 2.3: nadogradnja zatečene baze donosi šifarnik vrsta ugovora i nove kolone, a
    /// zatečene obračune ne dira. Migracija je čisto dodavanje — nova tabela i tri kolone —
    /// pa ništa što je već isplaćeno i prijavljeno ne sme da se promeni.
    /// </summary>
    [Fact]
    public void Create_ZatecenaBaza_DobijaSifarnikVrstaUgovoraBezDiranjaObracuna()
    {
        string putanja = NovaPutanja();

        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseSqlite($"Data Source={putanja}")
            .Options;

        using (var staraBaza = new PlataDbContext(options))
        {
            NapraviZatecenuSemu(staraBaza);

            staraBaza.Database.ExecuteSqlRaw(@"
                INSERT INTO Radnici
                    (Id, Godina, Mesec, BrojRadnika, ImeIPrezime, Jmbg, MaticniBroj, MestoRodjenja,
                     AdresaStanovanja, Mesto, SifraOpstine, Kategorija, Radno_Mesto,
                     BrojRadneJedinice, MinuliRadGodine, Koeficijent, Koeficijent1, OsnovnaPlata,
                     StopaPio, StopaZdravstvo, StopaNezaposlenost, BankovniRacun, NazivBanke,
                     Aktivan, LicnoOslobodjenje, Operativni, DatumUnosa)
                VALUES
                    (21, 2026, 3, 5, 'Mika Mikić', '0101990710014', '', '',
                     '', '', '', '', '',
                     1, 0, 2.5, 0, 0,
                     0, 0, 0, '', '',
                     1, 0, '', '2026-03-31 00:00:00');");

            UbaciObracunUZatecenuSemu(staraBaza, radnikId: 21, godina: 2026, mesec: 3, neto: 61234.56m);
        }

        using (var db = PlataDbContext.Create(putanja))
        {
            // Šifarnik stiže seed-om, isto kao vrste primanja i poreske olakšice.
            Assert.True(db.VrsteUgovora.Any(v => v.Sifra == VrsteUgovoraSeed.UgovorODelu));
            Assert.Equal(
                VrsteUgovoraSeed.Podrazumevane().Count,
                db.VrsteUgovora.Count());

            // Nema nijednog ugovora dok ga korisnik ne unese.
            Assert.Empty(db.Ugovori);

            // Šabloni ugovora stižu istim putem i vežu se za vrstu iste šifre, pa se pri
            // generisanju sami ponude.
            Assert.Equal(SabloniUgovoraSeed.Podrazumevani().Count, db.SabloniUgovora.Count());
            Assert.True(db.SabloniUgovora
                .Any(s => s.Sifra == SabloniUgovoraSeed.UgovorODelu && s.VrstaUgovoraId != null));

            var obracun = db.ObracuniPlata.Single();
            Assert.Equal(61234.56m, obracun.NetoIsplata);

            // Zatečeni obračun je zarada: bez ugovora i bez upisane osnovice doprinosa, pa se
            // ona i dalje izvodi kao pre — što drži prijave nepromenjenim.
            Assert.Null(obracun.UgovorId);
            Assert.Null(obracun.OsnovicaDoprinosa);
            Assert.False(db.Radnici.Any(r => r.VanRadnogOdnosa));
        }
    }

    /// <summary>
    /// Zbir isplaćenog po ugovorima mora da radi nad <b>pravim SQLite-om</b>.
    ///
    /// SQLite ne ume <c>SUM</c> nad <c>decimal</c> kolonom: grupisanje na strani baze pada sa
    /// „cannot apply aggregate operator 'Sum' on expressions of type 'decimal'". InMemory
    /// provajder to prihvata, pa greška prođe kroz sve ostale testove i pojavi se tek kod
    /// korisnika — zato ovaj test stoji ovde, među onima koji rade nad fajlom.
    /// </summary>
    [Fact]
    public void IsplacenoPoUgovorima_RadiNadSqliteBazom()
    {
        string putanja = NovaPutanja();

        using var db = PlataDbContext.Create(putanja);

        var vrsta = db.VrsteUgovora.First(v => v.Sifra == VrsteUgovoraSeed.UgovorODelu);

        db.Radnici.Add(new Radnik
        {
            BrojRadnika = 1,
            ImeIPrezime = "Primalac Jedan",
            Jmbg = "0101990710016",
            VanRadnogOdnosa = true,
            Godina = 2026,
            Mesec = 4
        });
        db.SaveChanges();

        var ugovor = new Ugovor
        {
            VrstaUgovoraId = vrsta.VrstaUgovoraId,
            BrojRadnika = 1,
            Predmet = "Izrada elaborata",
            UgovorenIznos = 50000m
        };
        db.Ugovori.Add(ugovor);
        db.SaveChanges();

        int radnikId = db.Radnici.Single().Id;

        db.ObracuniPlata.AddRange(
            new ObracunPlate { RadnikId = radnikId, Godina = 2026, Mesec = 4, UgovorId = ugovor.UgovorId, BrutoZarada = 30000m },
            new ObracunPlate { RadnikId = radnikId, Godina = 2026, Mesec = 4, UgovorId = ugovor.UgovorId, BrutoZarada = 20000m },
            // Stornirani se ne broji — nije isplaćen.
            new ObracunPlate { RadnikId = radnikId, Godina = 2026, Mesec = 4, UgovorId = ugovor.UgovorId, BrutoZarada = 99000m, Storniran = true });
        db.SaveChanges();

        var zbir = new ERPiZaradeApp.Services.UgovorObracunService(db).IsplacenoPoUgovorima();

        Assert.Equal(2, zbir[ugovor.UgovorId].BrojIsplata);
        Assert.Equal(50000m, zbir[ugovor.UgovorId].Bruto);
    }

    /// <summary>
    /// Migracija koja je u razvoju regenerisana — obrisana pa ponovo napravljena — dobija nov
    /// vremenski žig uz isti naziv. Baza koja je stigla da primeni staru verziju nosi njen ID,
    /// pa bi je EF primenio po drugi put i pao sa „duplicate column name" nad živim podacima.
    ///
    /// Test simulira upravo to: zapisu u istoriji se vrati stari žig, a kolone ostaju.
    /// </summary>
    [Fact]
    public void Create_MigracijaSaStarimZigom_NeRadiDvaputaIstuIzmenu()
    {
        string putanja = NovaPutanja();

        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseSqlite($"Data Source={putanja}")
            .Options;

        string aktuelni;

        // Priprema ide bez `Create`: on pamti koje je baze već inicijalizovao, pa bi drugi
        // poziv nad istom putanjom bio prazan hod. U programu se to ne dešava — svaki
        // pokretanje je nov proces sa praznim spiskom.
        using (var db = new PlataDbContext(options))
        {
            db.Database.Migrate();
            db.Radnici.Add(new Radnik { BrojRadnika = 4, ImeIPrezime = "Žika Žikić", Godina = 2026, Mesec = 3 });
            db.SaveChanges();

            aktuelni = db.Database.GetMigrations().Last();

            // Isti naziv, drugi žig — tačno ono što ostane posle „ef migrations remove/add".
            string stari = "19990101000000_" + aktuelni[(aktuelni.IndexOf('_') + 1)..];

            db.Database.ExecuteSqlRaw(
                "UPDATE __EFMigrationsHistory SET MigrationId = {0} WHERE MigrationId = {1};",
                stari, aktuelni);

            // Kolona koju je donela nova verzija migracije se uklanja, kao da je stara
            // verzija nije imala.
            db.Database.ExecuteSqlRaw("ALTER TABLE ObracunVerzije DROP COLUMN IsplataId;");
        }

        // Ovo je poziv koji je do sada padao.
        using (var db = PlataDbContext.Create(putanja))
        {
            Assert.Equal(1, db.Radnici.Count());

            // Kolona iz nove verzije migracije je stigla dopunom, iako je Migrate() preskočio.
            db.ObracunVerzije.Add(new ObracunVerzija
            {
                Godina = 2026, Mesec = 3, RadnikId = 1, IsplataId = null,
                BrojRadnika = 4, ImeRadnika = "Žika Žikić", Verzija = 1
            });
            db.SaveChanges();

            Assert.Single(db.ObracunVerzije);
        }

        // Istorija nosi aktuelni žig, i to tačno jednom.
        var primenjene = PrimenjeneMigracije(putanja);
        Assert.Contains(aktuelni, primenjene);
        Assert.Equal(primenjene.Count, primenjene.Distinct().Count());
    }

    [Fact]
    public void Create_PozvanDvaputa_JeIdempotentan()
    {
        string putanja = NovaPutanja();

        using (var db = PlataDbContext.Create(putanja))
        {
            db.Radnici.Add(new Radnik { BrojRadnika = 1, ImeIPrezime = "Prvi", Godina = 2026, Mesec = 1 });
            db.SaveChanges();
        }

        // Drugo otvaranje ne sme ništa da pokvari ni da duplira žig migracije.
        using (var db = PlataDbContext.Create(putanja))
        {
            Assert.Equal(1, db.Radnici.Count());
            Assert.Single(db.PlatniRazredi);
        }

        // Broj migracija raste kroz vreme, pa se proverava odsustvo duplikata, ne tačan broj.
        var primenjene = PrimenjeneMigracije(putanja);
        Assert.NotEmpty(primenjene);
        Assert.Equal(primenjene.Count, primenjene.Distinct().Count());
    }
}
