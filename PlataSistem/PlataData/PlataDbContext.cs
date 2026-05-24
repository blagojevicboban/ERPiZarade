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

        return ctx;
    }
}
