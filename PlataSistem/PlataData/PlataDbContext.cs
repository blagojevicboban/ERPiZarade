using Microsoft.EntityFrameworkCore;
using PlataData.Models;

namespace PlataData;

public class PlataDbContext : DbContext
{
    public PlataDbContext(DbContextOptions<PlataDbContext> options) : base(options) { }

    // Tabele
    public DbSet<Radnik> Radnici => Set<Radnik>();
    public DbSet<ObracunPlate> ObracuniPlata => Set<ObracunPlate>();
    public DbSet<Kredit> Krediti => Set<Kredit>();
    public DbSet<RadniSat> RadniSati => Set<RadniSat>();
    public DbSet<PoreznaStopa> PoreskeStope => Set<PoreznaStopa>();
    public DbSet<Kategorija> Kategorije => Set<Kategorija>();
    public DbSet<Samodoprinosi> Samodoprinosi => Set<Samodoprinosi>();
    public DbSet<Normativ> Normativi => Set<Normativ>();
    public DbSet<Porezi> Porezi => Set<Porezi>();
    public DbSet<Doprinos> Doprinosi => Set<Doprinos>();
    public DbSet<Firma> Firme => Set<Firma>();
    public DbSet<PlatniRazred> PlatniRazredi => Set<PlatniRazred>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Radnik → ObracunPlate (1:N)
        modelBuilder.Entity<ObracunPlate>()
            .HasOne(o => o.Radnik)
            .WithMany(r => r.Obracuni)
            .HasForeignKey(o => o.RadnikId)
            .OnDelete(DeleteBehavior.Restrict);

        // Radnik → Kredit (1:N)
        modelBuilder.Entity<Kredit>()
            .HasOne(k => k.Radnik)
            .WithMany(r => r.Krediti)
            .HasForeignKey(k => k.RadnikId)
            .OnDelete(DeleteBehavior.Restrict);

        // Radnik → RadniSati (1:N)
        modelBuilder.Entity<RadniSat>()
            .HasOne(rs => rs.Radnik)
            .WithMany(r => r.RadniSati)
            .HasForeignKey(rs => rs.RadnikId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indeks: brzo pretraži obračune po radniku/godini/mesecu (nije unique — može biti korekcija)
        modelBuilder.Entity<ObracunPlate>()
            .HasIndex(o => new { o.RadnikId, o.Godina, o.Mesec });

        // Indeks: radni sati po radniku/godini/mesecu
        modelBuilder.Entity<RadniSat>()
            .HasIndex(rs => new { rs.RadnikId, rs.Godina, rs.Mesec })
            .IsUnique();

        // Indeks: brza pretraga radnika po JMBG
        modelBuilder.Entity<Radnik>()
            .HasIndex(r => r.Jmbg);

        // Indeks: brza pretraga radnika po BrojRadnika
        modelBuilder.Entity<Radnik>()
            .HasIndex(r => r.BrojRadnika);
    }

    /// <summary>
    /// Kreira bazu i primenjuje sve migracije ako ne postoji.
    /// Poziva se pri pokretanju aplikacije.
    /// </summary>
    public static PlataDbContext Create(string dbPath)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlataDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        var ctx = new PlataDbContext(optionsBuilder.Options);
        ctx.Database.EnsureCreated();

        // Bezbedno dodavanje kolona u SQLite bez migracija
        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN Prosek DECIMAL(14,4) DEFAULT 0;");
        }
        catch { /* Kolona vec postoji */ }

        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN Prosek DECIMAL(14,4) DEFAULT 0;");
        }
        catch { /* Kolona vec postoji */ }

        // Bezbedno dodavanje detaljnih bruto kolona
        string[] newCols = {
            "NetoZar", "NetoNerd", "NetoGOd", "NetoTo", "NetoReg",
            "Neto", "NetoBol", "NetoB100", "NetoPlac", "NetoPlZ",
            "NetoDrza", "NetoNocni", "NetoVezba", "NetoPrek", "NetoTer",
            "KorDod", "KorDod1", "Kumul", "NetoNede"
        };
        foreach (var col in newCols)
        {
            try
            {
                ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} DECIMAL(14,2) DEFAULT 0;");
            }
            catch { /* Kolona već postoji */ }
        }


        // Automatsko kopiranje 13-cifrenog JMBG-a iz MaticniBroj u Jmbg ako je Jmbg prazan
        try
        {
            ctx.Database.ExecuteSqlRaw("UPDATE Radnici SET Jmbg = MaticniBroj WHERE (Jmbg IS NULL OR trim(Jmbg) = '') AND MaticniBroj IS NOT NULL AND length(trim(MaticniBroj)) = 13;");
        }
        catch { /* Tabela ili kolona ne postoji jos */ }

        // Automatsko kreiranje nedostajućih zapisa u RadniSati iz postojećih obračuna (istorijski DBF podaci)
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                INSERT INTO RadniSati (RadnikId, Godina, Mesec, RedovniSati, BolovanjeSati, PrekovremeneSati, GodisnjiOdmorSati, DrzavniPraznikSati, NocniSati, Prosek)
                SELECT RadnikId, Godina, Mesec, MAX(RedovniSati), MAX(BolovanjeSati), MAX(PrekovremeneSati), MAX(GodisnjioOdmorSati), 0, 0, MAX(Prosek)
                FROM ObracuniPlata o
                WHERE NOT EXISTS (
                    SELECT 1 FROM RadniSati rs 
                    WHERE rs.RadnikId = o.RadnikId AND rs.Godina = o.Godina AND rs.Mesec = o.Mesec
                )
                GROUP BY RadnikId, Godina, Mesec;");
        }
        catch { /* Tabela ne postoji još */ }

        // Bezbedno kreiranje tabele Firme ako ne postoji (EnsureCreated ne dodaje tabele u postojeću bazu)
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Firme (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Naziv TEXT NOT NULL DEFAULT '',
                    Adresa TEXT NOT NULL DEFAULT '',
                    Grad TEXT NOT NULL DEFAULT '',
                    Pib TEXT NOT NULL DEFAULT '',
                    Mb TEXT NOT NULL DEFAULT '',
                    BankovniRacun TEXT NOT NULL DEFAULT '',
                    SifraPlacanja TEXT NOT NULL DEFAULT '',
                    Telefon TEXT NOT NULL DEFAULT '',
                    Email TEXT NOT NULL DEFAULT '',
                    Napomena TEXT NOT NULL DEFAULT ''
                );");
        }
        catch { /* Tabela već postoji */ }

        // Bezbedno kreiranje tabele PlatniRazredi ako ne postoji
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PlatniRazredi (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    R1 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R2 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R3 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R4 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R5 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R6 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R7 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R8 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R9 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P1 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P2 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P3 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P4 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P5 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P6 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P7 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P8 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P9 DECIMAL(14,2) NOT NULL DEFAULT 0
                );");
        }
        catch { /* Tabela vec postoji */ }

        // Bezbedno inicijalno popunjavanje default platnih razreda ako je tabela prazna
        try
        {
            if (!ctx.PlatniRazredi.Any())
            {
                ctx.PlatniRazredi.Add(new PlatniRazred
                {
                    R1 = 51297.00m, R2 = 51297.00m, R3 = 51297.00m, R4 = 51297.00m, R5 = 51297.00m, R6 = 51297.00m, R7 = 51297.00m, R8 = 51297.00m, R9 = 0m,
                    P1 = 51297.00m, P2 = 51297.00m, P3 = 51297.00m, P4 = 51297.00m, P5 = 51297.00m, P6 = 51297.00m, P7 = 51297.00m, P8 = 51297.00m, P9 = 0m
                });
                ctx.SaveChanges();
            }
        }
        catch { }

        return ctx;
    }
}
