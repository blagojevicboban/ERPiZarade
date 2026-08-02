using System.IO;
using Microsoft.EntityFrameworkCore;
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

        // 1. Simulacija stare baze — tačno onako kako ju je pravila ranija verzija.
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseSqlite($"Data Source={putanja}")
            .Options;

        using (var staraBaza = new PlataDbContext(options))
        {
            staraBaza.Database.EnsureCreated();
            staraBaza.Radnici.Add(new Radnik
            {
                BrojRadnika = 7,
                ImeIPrezime = "Mika Mikić",
                Jmbg = "0101990710014",
                Godina = 2026,
                Mesec = 1,
                Koeficijent = 2.5m
            });
            staraBaza.SaveChanges();
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
        }

        // 3. Baza je usvojena u sistem migracija bez ponovnog kreiranja tabela.
        var primenjene = PrimenjeneMigracije(putanja);
        Assert.Contains(primenjene, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
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

        Assert.Single(PrimenjeneMigracije(putanja));
    }
}
