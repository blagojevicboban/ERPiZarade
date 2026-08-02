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
