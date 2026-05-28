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
    public DbSet<DoprinosiPoslodavca> DoprinosiPoslodavca => Set<DoprinosiPoslodavca>();

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

        // Radnik → DoprinosiPoslodavca (1:N)
        modelBuilder.Entity<DoprinosiPoslodavca>()
            .HasOne(dp => dp.Radnik)
            .WithMany()
            .HasForeignKey(dp => dp.RadnikId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DoprinosiPoslodavca>()
            .HasIndex(dp => new { dp.RadnikId, dp.Godina, dp.Mesec });
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

        // Bezbedno kreiranje tabele DoprinosiPoslodavca ako ne postoji
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS DoprinosiPoslodavca (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RadnikId INTEGER NOT NULL,
                    Godina INTEGER NOT NULL,
                    Mesec INTEGER NOT NULL,
                    Zar1 DECIMAL(14,2) DEFAULT 0, Zar2 DECIMAL(14,2) DEFAULT 0, Zar3 DECIMAL(14,2) DEFAULT 0, Zar4 DECIMAL(14,2) DEFAULT 0, Zar5 DECIMAL(14,2) DEFAULT 0, Zar6 DECIMAL(14,2) DEFAULT 0, Zar7 DECIMAL(14,2) DEFAULT 0, Zar8 DECIMAL(14,2) DEFAULT 0, Zar9 DECIMAL(14,2) DEFAULT 0,
                    Bol1 DECIMAL(14,2) DEFAULT 0, Bol2 DECIMAL(14,2) DEFAULT 0, Bol3 DECIMAL(14,2) DEFAULT 0, Bol4 DECIMAL(14,2) DEFAULT 0, Bol5 DECIMAL(14,2) DEFAULT 0, Bol6 DECIMAL(14,2) DEFAULT 0, Bol7 DECIMAL(14,2) DEFAULT 0, Bol8 DECIMAL(14,2) DEFAULT 0, Bol9 DECIMAL(14,2) DEFAULT 0,
                    Nak1 DECIMAL(14,2) DEFAULT 0, Nak2 DECIMAL(14,2) DEFAULT 0, Nak3 DECIMAL(14,2) DEFAULT 0, Nak4 DECIMAL(14,2) DEFAULT 0, Nak5 DECIMAL(14,2) DEFAULT 0, Nak6 DECIMAL(14,2) DEFAULT 0, Nak7 DECIMAL(14,2) DEFAULT 0, Nak8 DECIMAL(14,2) DEFAULT 0, Nak9 DECIMAL(14,2) DEFAULT 0,
                    Nep1 DECIMAL(14,2) DEFAULT 0, Nep2 DECIMAL(14,2) DEFAULT 0, Nep3 DECIMAL(14,2) DEFAULT 0, Nep4 DECIMAL(14,2) DEFAULT 0, Nep5 DECIMAL(14,2) DEFAULT 0, Nep6 DECIMAL(14,2) DEFAULT 0, Nep7 DECIMAL(14,2) DEFAULT 0, Nep8 DECIMAL(14,2) DEFAULT 0, Nep9 DECIMAL(14,2) DEFAULT 0,
                    B60F1 DECIMAL(14,2) DEFAULT 0, B60F2 DECIMAL(14,2) DEFAULT 0, B60F3 DECIMAL(14,2) DEFAULT 0, B60F4 DECIMAL(14,2) DEFAULT 0, B60F5 DECIMAL(14,2) DEFAULT 0, B60F6 DECIMAL(14,2) DEFAULT 0, B60F7 DECIMAL(14,2) DEFAULT 0, B60F8 DECIMAL(14,2) DEFAULT 0, B60F9 DECIMAL(14,2) DEFAULT 0,
                    B601 DECIMAL(14,2) DEFAULT 0, B602 DECIMAL(14,2) DEFAULT 0, B603 DECIMAL(14,2) DEFAULT 0, B604 DECIMAL(14,2) DEFAULT 0, B605 DECIMAL(14,2) DEFAULT 0, B606 DECIMAL(14,2) DEFAULT 0, B607 DECIMAL(14,2) DEFAULT 0, B608 DECIMAL(14,2) DEFAULT 0, B609 DECIMAL(14,2) DEFAULT 0,
                    Inv1 DECIMAL(14,2) DEFAULT 0, Inv2 DECIMAL(14,2) DEFAULT 0, Inv3 DECIMAL(14,2) DEFAULT 0, Inv4 DECIMAL(14,2) DEFAULT 0, Inv5 DECIMAL(14,2) DEFAULT 0, Inv6 DECIMAL(14,2) DEFAULT 0, Inv7 DECIMAL(14,2) DEFAULT 0, Inv8 DECIMAL(14,2) DEFAULT 0, Inv9 DECIMAL(14,2) DEFAULT 0,
                    Por1 DECIMAL(14,2) DEFAULT 0, Por2 DECIMAL(14,2) DEFAULT 0, Por3 DECIMAL(14,2) DEFAULT 0, Por4 DECIMAL(14,2) DEFAULT 0, Por5 DECIMAL(14,2) DEFAULT 0, Por6 DECIMAL(14,2) DEFAULT 0, Por7 DECIMAL(14,2) DEFAULT 0, Por8 DECIMAL(14,2) DEFAULT 0, Por9 DECIMAL(14,2) DEFAULT 0,
                    FOREIGN KEY (RadnikId) REFERENCES Radnici(Id) ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_DoprinosiPoslodavca_RadnikId_Godina_Mesec ON DoprinosiPoslodavca (RadnikId, Godina, Mesec);
            ");
        }
        catch { }

        // Bezbedno kreiranje indeksa za brzu pretragu po godini i mesecu
        try
        {
            ctx.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_ObracuniPlata_Godina_Mesec ON ObracuniPlata (Godina, Mesec);");
        }
        catch { }

        // Bezbedno dodavanje kolona u SQLite bez migracija
        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN Prosek DECIMAL(14,4) DEFAULT 0;");
        }
        catch { /* Kolona vec postoji */ }

        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN Stimulacija DECIMAL(14,2) DEFAULT 0;");
        }
        catch { /* Kolona vec postoji */ }

        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN Prosek DECIMAL(14,4) DEFAULT 0;");
        }
        catch { /* Kolona vec postoji */ }

        // Nova polja za sate — RadniSati
        string[] noviSatiKoloneRS = { 
            "SmenskiSati", "RadPraznikomSati", "NocniRadPraznikomSati", "PlacenoOdsustvoSati",
            "RadNedeljomSati", "PlacenoZakonskiSati", "BolovanjePreko60Sati", "PorodiljskoOdsustvoSati", 
            "Bolovanje100Sati", "TopliObrokDani"
        };
        foreach (var col in noviSatiKoloneRS)
        {
            try { ctx.Database.ExecuteSqlRaw($"ALTER TABLE RadniSati ADD COLUMN {col} INTEGER NOT NULL DEFAULT 0;"); }
            catch { /* Kolona već postoji */ }
        }

        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN RegresIznos DECIMAL(14,2) DEFAULT 0;");
        }
        catch { /* Kolona vec postoji */ }

        // Nova polja za sate — ObracuniPlata
        string[] noviSatiKoloneOP = { "SmenskiSati", "RadPraznikomSati", "NocniRadPraznikomSati", "PlacenoOdsustvoSati" };
        foreach (var col in noviSatiKoloneOP)
        {
            try { ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} INTEGER NOT NULL DEFAULT 0;"); }
            catch { /* Kolona već postoji */ }
        }



        // Bezbedno dodavanje detaljnih bruto kolona
        string[] newCols = {
            "NetoZar", "NetoNerd", "NetoGOd", "NetoTo", "NetoReg",
            "Neto", "NetoBol", "NetoB100", "NetoPlac", "NetoPlZ",
            "NetoDrza", "NetoNocni", "NetoVezba", "NetoPrek", "NetoTer",
            "KorDod", "KorDod1", "Kumul", "NetoNede",
            "LicniOdbitak"   // DBF: umanjenje = licni odbitak (SAMODOP.PRG: sum_umanj)
        };
        foreach (var col in newCols)
        {
            try
            {
                ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} DECIMAL(14,2) DEFAULT 0;");
            }
            catch { /* Kolona već postoji */ }
        }

        // ── DODATNO MIGRIRANE LEGACY KOLONE IZ OBRACUN.DBF / OBRACUNI.DBF ──
        string[] decimalColsOP = {
            "Koeficijent", "UkupnoRadnihSatiLegacy", "FondSatiMesecni", "DodaciLegacy",
            "DodatakNaM1", "DodatakNaM2", "DodatakNaM3", "BrutoOsnovica", "TopliObrokIznos",
            "BrutoPioOsnovica", "NetoNaknadeLegacy", "NedeljaSati", "BolovanjePreko60SatiLegacy",
            "PorodiljskoOdsustvoSatiLegacy", "PlacenoOdsustvoSatiLegacy", "PlacenoZakonskiSatiLegacy",
            "Bolovanje100SatiLegacy", "MinimalnaPlataOsnovica", "PosebanPorez", "NetoPorez", "NetoBezPoreza"
        };
        foreach (var col in decimalColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} DECIMAL(14,2) DEFAULT 0;"); }
            catch { /* Kolona već postoji */ }
        }

        string[] decimal5ColsOP = { "CenaSataRedovan", "CenaSataMinuliRad" };
        foreach (var col in decimal5ColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} DECIMAL(14,5) DEFAULT 0;"); }
            catch { /* Kolona već postoji */ }
        }

        string[] intColsOP = { "MinuliRadGodine", "BrojRadneJedinice", "SifraSamodoprinosa1", "SifraSamodoprinosa2" };
        foreach (var col in intColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} INTEGER DEFAULT 0;"); }
            catch { /* Kolona već postoji */ }
        }

        string[] stringColsOP = { "Kategorija", "Operativni", "Oznaka" };
        foreach (var col in stringColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw($"ALTER TABLE ObracuniPlata ADD COLUMN {col} TEXT DEFAULT '';"); }
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

        // Bezbedno dodavanje TopliObrokCena u Porezi (cena toplog obroka po danu)
        try
        {
            ctx.Database.ExecuteSqlRaw("ALTER TABLE Porezi ADD COLUMN TopliObrokCena DECIMAL(14,2) DEFAULT 0;");
        }
        catch { /* Kolona već postoji */ }

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
