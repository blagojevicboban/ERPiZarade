using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ERPiZaradeData.Models;

namespace ERPiZaradeData;

public class PlataDbContext : DbContext
{
    private static readonly object _initLock = new();
    private static readonly System.Collections.Generic.HashSet<string> _initializedDbs = new(System.StringComparer.OrdinalIgnoreCase);

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
    public DbSet<Banka> Banke => Set<Banka>();
    public DbSet<Korisnik> Korisnici => Set<Korisnik>();
    public DbSet<PppPdPrijava> PppPdPrijave => Set<PppPdPrijava>();
    public DbSet<ObracunAudit> ObracunAuditi => Set<ObracunAudit>();
    public DbSet<ObracunVerzija> ObracunVerzije => Set<ObracunVerzija>();
    public DbSet<SlanjeListica> SlanjaListica => Set<SlanjeListica>();
    public DbSet<Praznik> Praznici => Set<Praznik>();
    public DbSet<VrstaPrimanja> VrstePrimanja => Set<VrstaPrimanja>();
    public DbSet<ObracunStavka> ObracunStavke => Set<ObracunStavka>();
    public DbSet<UnetoPrimanje> UnetaPrimanja => Set<UnetoPrimanje>();
    public DbSet<PoreskaOlaksica> PoreskeOlaksice => Set<PoreskaOlaksica>();
    public DbSet<OlaksicaMfp> OlaksicaMfpDeklaracije => Set<OlaksicaMfp>();


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

        // Unique: jedan radnik po periodu
        modelBuilder.Entity<Radnik>()
            .HasIndex(r => new { r.BrojRadnika, r.Godina, r.Mesec })
            .IsUnique();

        // Indeks: brza pretraga po periodu
        modelBuilder.Entity<Radnik>()
            .HasIndex(r => new { r.Godina, r.Mesec });

        // Indeks: brza pretraga po JMBG
        modelBuilder.Entity<Radnik>()
            .HasIndex(r => r.Jmbg);

        // Indeks: brza pretraga po BrojRadnika
        modelBuilder.Entity<Radnik>()
            .HasIndex(r => r.BrojRadnika);

        // Indeks: brzo pretraži obračune po radniku/godini/mesecu
        modelBuilder.Entity<ObracunPlate>()
            .HasIndex(o => new { o.RadnikId, o.Godina, o.Mesec });

        // Indeks: radni sati po radniku/godini/mesecu
        modelBuilder.Entity<RadniSat>()
            .HasIndex(rs => new { rs.RadnikId, rs.Godina, rs.Mesec })
            .IsUnique();

        // Radnik → DoprinosiPoslodavca (1:N)
        modelBuilder.Entity<DoprinosiPoslodavca>()
            .HasOne(dp => dp.Radnik)
            .WithMany()
            .HasForeignKey(dp => dp.RadnikId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DoprinosiPoslodavca>()
            .HasIndex(dp => new { dp.RadnikId, dp.Godina, dp.Mesec });

        // Jedna prijava po periodu i rednom broju isplate
        modelBuilder.Entity<PppPdPrijava>()
            .HasIndex(p => new { p.Godina, p.Mesec, p.RedniBroj })
            .IsUnique();

        // Revizioni trag se čita hronološki po periodu
        modelBuilder.Entity<ObracunAudit>()
            .HasIndex(a => new { a.Godina, a.Mesec, a.Vreme });

        // Arhiva verzija se čita po periodu, pa po radniku unutar njega
        modelBuilder.Entity<ObracunVerzija>()
            .HasIndex(v => new { v.Godina, v.Mesec, v.BrojRadnika, v.Verzija });

        // Evidencija slanja se čita po periodu i po radniku („da li je dobio listić")
        modelBuilder.Entity<SlanjeListica>()
            .HasIndex(s => new { s.Godina, s.Mesec, s.BrojRadnika });

        // Jedan zapis po danu — dva praznika istog dana bi se dvaput oduzela od fonda sati
        modelBuilder.Entity<Praznik>()
            .HasIndex(p => p.Datum)
            .IsUnique();

        // Šifra vrste primanja je ono po čemu je kod traži, pa mora biti jedinstvena
        modelBuilder.Entity<VrstaPrimanja>()
            .HasIndex(v => v.Sifra)
            .IsUnique();

        // Brisanjem obračuna nestaju i njegove stavke — one same za sebe nemaju smisla
        modelBuilder.Entity<ObracunStavka>()
            .HasOne(s => s.Obracun)
            .WithMany(o => o.Stavke)
            .HasForeignKey(s => s.ObracunPlateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vrsta primanja koja je upotrebljena u obračunu ne sme da se obriše
        modelBuilder.Entity<ObracunStavka>()
            .HasOne(s => s.VrstaPrimanja)
            .WithMany(v => v.Stavke)
            .HasForeignKey(s => s.VrstaPrimanjaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Jedna stavka po vrsti primanja u okviru obračuna
        modelBuilder.Entity<ObracunStavka>()
            .HasIndex(s => new { s.ObracunPlateId, s.VrstaPrimanjaId })
            .IsUnique();

        // Uneto primanje: jedan iznos po radniku, periodu i vrsti
        modelBuilder.Entity<UnetoPrimanje>()
            .HasIndex(p => new { p.RadnikId, p.Godina, p.Mesec, p.VrstaPrimanjaId })
            .IsUnique();

        modelBuilder.Entity<UnetoPrimanje>()
            .HasOne(p => p.Radnik)
            .WithMany()
            .HasForeignKey(p => p.RadnikId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UnetoPrimanje>()
            .HasOne(p => p.VrstaPrimanja)
            .WithMany()
            .HasForeignKey(p => p.VrstaPrimanjaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Olakšica se traži po OL oznaci iz SVP šifre, pa dve iste čine šifarnik dvosmislenim
        modelBuilder.Entity<PoreskaOlaksica>()
            .HasIndex(o => o.Sifra)
            .IsUnique();

        // MFP deklaracije nemaju smisla bez olakšice kojoj pripadaju
        modelBuilder.Entity<OlaksicaMfp>()
            .HasOne(m => m.Olaksica)
            .WithMany(o => o.MfpDeklaracije)
            .HasForeignKey(m => m.PoreskaOlaksicaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Jedno MFP polje se deklariše najviše jednom po olakšici
        modelBuilder.Entity<OlaksicaMfp>()
            .HasIndex(m => new { m.PoreskaOlaksicaId, m.Oznaka })
            .IsUnique();
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

        lock (_initLock)
        {
            var absolutePath = System.IO.Path.GetFullPath(dbPath);
            if (!_initializedDbs.Contains(absolutePath))
            {
                InitializeDatabase(ctx);
                _initializedDbs.Add(absolutePath);
            }
        }

        return ctx;
    }

    private static void InitializeDatabase(PlataDbContext ctx)
    {
        bool zatecenaBaza = PostojiZatecenaSema(ctx);

        if (zatecenaBaza)
        {
            // Baza je napravljena ranijom verzijom preko EnsureCreated() i nema istoriju
            // migracija. Prvo je starim zakrpama dovodimo na aktuelnu šemu, pa je tek onda
            // žigošemo kao da je početna migracija već primenjena — u suprotnom bi Migrate()
            // pokušao da kreira tabele koje već postoje i pao nad živim podacima.
            PrimeniLegacyZakrpe(ctx);
            OznaciPocetnuMigracijuKaoPrimenjenu(ctx);
        }

        // Nova baza: kreira kompletnu šemu i istoriju. Zatečena: primenjuje samo nove migracije.
        ctx.Database.Migrate();

        // Optimizacija performansi SQLite-a na nivou same baze
        try { ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;"); } catch { }

        UbaciPodrazumevanePodatke(ctx);
    }

    /// <summary>
    /// Da li baza već sadrži tabele — znak da je nastala pre uvođenja EF migracija.
    /// </summary>
    private static bool PostojiZatecenaSema(PlataDbContext ctx)
    {
        var creator = ctx.Database.GetService<IRelationalDatabaseCreator>();
        return creator.Exists() && creator.HasTables();
    }

    /// <summary>
    /// Upisuje početnu migraciju u __EFMigrationsHistory BEZ izvršavanja njenog sadržaja,
    /// čime se zatečena baza usvaja u sistem migracija a da se nijedan podatak ne dira.
    /// Od tog trenutka svaka naredna izmena šeme ide kroz uobičajenu EF migraciju.
    /// </summary>
    private static void OznaciPocetnuMigracijuKaoPrimenjenu(PlataDbContext ctx)
    {
        var pocetnaMigracija = ctx.Database.GetMigrations().FirstOrDefault();
        if (string.IsNullOrEmpty(pocetnaMigracija)) return;

        ctx.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                ProductVersion TEXT NOT NULL
            );");

        ctx.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1});",
            pocetnaMigracija,
            typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "8.0.16");
    }

    /// <summary>
    /// Dopune šeme za baze nastale ranijim verzijama programa. Pokreće se SAMO nad
    /// zatečenim bazama — nove baze dobijaju ispravnu šemu direktno iz migracije.
    /// Ovde se više ne dodaju nove kolone; za izmene šeme koristiti "dotnet ef migrations add".
    /// </summary>
    private static void PrimeniLegacyZakrpe(PlataDbContext ctx)
    {
        // ── Bezbedno dodavanje novih kolona (za starije baze) ──────────────

        // Radnici: nova periodična arhitektura — Godina i Mesec
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Radnici ADD COLUMN Godina INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Radnici ADD COLUMN Mesec INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Radnici ADD COLUMN BrojRadnika INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Radnici ADD COLUMN Koeficijent1 DECIMAL(10,4) NOT NULL DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Radnici ADD COLUMN MinuliRadGodine INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Radnici ADD COLUMN Operativni TEXT NOT NULL DEFAULT '';"); } catch { }

        // ObracuniPlata: Zakljucavanje obracuna
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN Zakljucan INTEGER NOT NULL DEFAULT 0;"); } catch { }

        // Migracija Faza0 briše duplirano polje `Zakljucen`. Zatečene baze ga mogu imati
        // ili ne — zavisno od verzije koja ih je napravila — a DROP COLUMN nad nepostojećom
        // kolonom bi oborio nadogradnju. Zato ga ovde bezuslovno obezbeđujemo; ako već
        // postoji, ALTER pukne i catch ga proguta.
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN Zakljucen INTEGER NOT NULL DEFAULT 0;"); } catch { }

        // Migracija starih baza: BrojRadnika = Id (stara arhitektura)
        try { ctx.Database.ExecuteSqlRaw("UPDATE Radnici SET BrojRadnika = Id WHERE BrojRadnika = 0 OR BrojRadnika IS NULL;"); } catch { }

        // Kreiranje unique indeksa za novu arhitekturu
        try { ctx.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Radnici_BrojRadnika_Godina_Mesec ON Radnici (BrojRadnika, Godina, Mesec);"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Radnici_Godina_Mesec ON Radnici (Godina, Mesec);"); } catch { }

        // ── DoprinosiPoslodavca ─────────────────────────────────────────────
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

        try { ctx.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_ObracuniPlata_Godina_Mesec ON ObracuniPlata (Godina, Mesec);"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN Prosek DECIMAL(14,4) DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN Stimulacija DECIMAL(14,2) DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN Prosek DECIMAL(14,4) DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN Varijabila DECIMAL(14,2) DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN Varijabila DECIMAL(14,2) DEFAULT 0;"); } catch { }

        string[] noviSatiKoloneRS = {
            "SmenskiSati", "RadPraznikomSati", "NocniRadPraznikomSati", "PlacenoOdsustvoSati",
            "RadNedeljomSati", "PlacenoZakonskiSati", "BolovanjePreko60Sati", "PorodiljskoOdsustvoSati",
            "Bolovanje100Sati", "TopliObrokDani"
        };
        foreach (var col in noviSatiKoloneRS)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN " + col + " INTEGER NOT NULL DEFAULT 0;"); } catch { }
        }

        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE RadniSati ADD COLUMN RegresIznos DECIMAL(14,2) DEFAULT 0;"); } catch { }

        string[] noviSatiKoloneOP = { "SmenskiSati", "RadPraznikomSati", "NocniRadPraznikomSati", "PlacenoOdsustvoSati" };
        foreach (var col in noviSatiKoloneOP)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN " + col + " INTEGER NOT NULL DEFAULT 0;"); } catch { }
        }

        string[] newCols = {
            "NetoZar", "NetoNerd", "NetoGOd", "NetoTo", "NetoReg",
            "Neto", "NetoBol", "NetoB100", "NetoPlac", "NetoPlZ",
            "NetoDrza", "NetoNocni", "NetoVezba", "NetoPrek", "NetoTer",
            "KorDod", "KorDod1", "Kumul", "NetoNede",
            "LicniOdbitak"
        };
        foreach (var col in newCols)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN " + col + " DECIMAL(14,2) DEFAULT 0;"); } catch { }
        }

        string[] decimalColsOP = {
            "Koeficijent", "UkupnoRadnihSatiLegacy", "FondSatiMesecni", "DodaciLegacy",
            "DodatakNaM1", "DodatakNaM2", "DodatakNaM3", "BrutoOsnovica", "TopliObrokIznos",
            "BrutoPioOsnovica", "NetoNaknadeLegacy", "NedeljaSati", "BolovanjePreko60SatiLegacy",
            "PorodiljskoOdsustvoSatiLegacy", "PlacenoOdsustvoSatiLegacy", "PlacenoZakonskiSatiLegacy",
            "Bolovanje100SatiLegacy", "MinimalnaPlataOsnovica", "PosebanPorez", "NetoPorez", "NetoBezPoreza"
        };
        foreach (var col in decimalColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN " + col + " DECIMAL(14,2) DEFAULT 0;"); } catch { }
        }

        string[] decimal5ColsOP = { "CenaSataRedovan", "CenaSataMinuliRad" };
        foreach (var col in decimal5ColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN " + col + " DECIMAL(14,5) DEFAULT 0;"); } catch { }
        }

        string[] intColsOP = { "MinuliRadGodine", "BrojRadneJedinice", "SifraSamodoprinosa1", "SifraSamodoprinosa2" };
        foreach (var col in intColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN " + col + " INTEGER DEFAULT 0;"); } catch { }
        }

        string[] stringColsOP = { "Kategorija", "Operativni", "Oznaka" };
        foreach (var col in stringColsOP)
        {
            try { ctx.Database.ExecuteSqlRaw("ALTER TABLE ObracuniPlata ADD COLUMN " + col + " TEXT DEFAULT '';"); } catch { }
        }

        // Automatsko kopiranje 13-cifrenog JMBG-a
        try { ctx.Database.ExecuteSqlRaw("UPDATE Radnici SET Jmbg = MaticniBroj WHERE (Jmbg IS NULL OR trim(Jmbg) = '') AND MaticniBroj IS NOT NULL AND length(trim(MaticniBroj)) = 13;"); } catch { }

        // Kreiranje nedostajućih RadniSati iz obračuna
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
        catch { }

        // Firme
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
        catch { }

        // PlatniRazredi
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PlatniRazredi (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    R1 DECIMAL(14,2) NOT NULL DEFAULT 0, R2 DECIMAL(14,2) NOT NULL DEFAULT 0, R3 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R4 DECIMAL(14,2) NOT NULL DEFAULT 0, R5 DECIMAL(14,2) NOT NULL DEFAULT 0, R6 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    R7 DECIMAL(14,2) NOT NULL DEFAULT 0, R8 DECIMAL(14,2) NOT NULL DEFAULT 0, R9 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P1 DECIMAL(14,2) NOT NULL DEFAULT 0, P2 DECIMAL(14,2) NOT NULL DEFAULT 0, P3 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P4 DECIMAL(14,2) NOT NULL DEFAULT 0, P5 DECIMAL(14,2) NOT NULL DEFAULT 0, P6 DECIMAL(14,2) NOT NULL DEFAULT 0,
                    P7 DECIMAL(14,2) NOT NULL DEFAULT 0, P8 DECIMAL(14,2) NOT NULL DEFAULT 0, P9 DECIMAL(14,2) NOT NULL DEFAULT 0
                );");
        }
        catch { }

        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Porezi ADD COLUMN TopliObrokCena DECIMAL(14,2) DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Doprinosi ADD COLUMN NajnizaOsnovica DECIMAL(14,2) NOT NULL DEFAULT 0;"); } catch { }
        try { ctx.Database.ExecuteSqlRaw("ALTER TABLE Doprinosi ADD COLUMN NajvisaOsnovica DECIMAL(14,2) NOT NULL DEFAULT 0;"); } catch { }

        // Banke
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Banke (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Godina INTEGER NOT NULL,
                    Mesec INTEGER NOT NULL,
                    Sifra TEXT NOT NULL DEFAULT '',
                    Naziv TEXT NOT NULL DEFAULT '',
                    ZiroRacun TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS IX_Banke_Godina_Mesec ON Banke(Godina, Mesec);
            ");
        }
        catch { }

        // Korisnici (prijava u sistem)
        try
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Korisnici (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ImePrezime TEXT NOT NULL DEFAULT '',
                    KorisnickoIme TEXT NOT NULL,
                    LozinkaHash TEXT NOT NULL,
                    Uloga INTEGER NOT NULL DEFAULT 1,
                    JeAktivan INTEGER NOT NULL DEFAULT 1,
                    PoslednjaPrijava TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Korisnici_KorisnickoIme ON Korisnici(KorisnickoIme);
            ");
        }
        catch { }
    }

    /// <summary>
    /// Podrazumevani sadržaj šifarnika koji mora postojati da bi program radio.
    /// Pokreće se i nad novom i nad zatečenom bazom; ništa ne prepisuje.
    /// </summary>
    private static void UbaciPodrazumevanePodatke(PlataDbContext ctx)
    {
        try
        {
            if (!ctx.PlatniRazredi.Any())
            {
                ctx.PlatniRazredi.Add(new PlatniRazred
                {
                    R1 = 51297.00m, R2 = 51297.00m, R3 = 51297.00m, R4 = 51297.00m, R5 = 51297.00m,
                    R6 = 51297.00m, R7 = 51297.00m, R8 = 51297.00m, R9 = 0m,
                    P1 = 51297.00m, P2 = 51297.00m, P3 = 51297.00m, P4 = 51297.00m, P5 = 51297.00m,
                    P6 = 51297.00m, P7 = 51297.00m, P8 = 51297.00m, P9 = 0m
                });
                ctx.SaveChanges();
            }
        }
        catch { }

        try
        {
            // Šifarnik se dopunjuje po šifri: nove vrste ulaze, a izmene koje je korisnik
            // napravio nad postojećim ostaju netaknute.
            var postojece = ctx.VrstePrimanja.Select(v => v.Sifra).ToHashSet();
            var nove = VrstePrimanjaSeed.Podrazumevane()
                .Where(v => !postojece.Contains(v.Sifra))
                .ToList();

            if (nove.Count > 0)
            {
                ctx.VrstePrimanja.AddRange(nove);
                ctx.SaveChanges();
            }
        }
        catch { }

        try
        {
            var postojeceOlaksice = ctx.PoreskeOlaksice.Select(o => o.Sifra).ToHashSet();
            var noveOlaksice = PoreskeOlaksiceSeed.Podrazumevane()
                .Where(o => !postojeceOlaksice.Contains(o.Sifra))
                .ToList();

            if (noveOlaksice.Count > 0)
            {
                ctx.PoreskeOlaksice.AddRange(noveOlaksice);
                ctx.SaveChanges();
            }
        }
        catch { }

        try
        {
            if (!ctx.Korisnici.Any())
            {
                ctx.Korisnici.Add(new Korisnik
                {
                    ImePrezime = "Administrator",
                    KorisnickoIme = "admin",
                    LozinkaHash = HashPassword("admin"),
                    Uloga = UlogaKorisnika.Administrator,
                    JeAktivan = true
                });
                ctx.SaveChanges();
            }
        }
        catch { }
    }

    private const int PasswordSaltSize = 16;
    private const int PasswordHashSize = 32;
    private const int PasswordIterations = 100_000;

    public static string HashPassword(string password)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(PasswordSaltSize);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password, salt, PasswordIterations, System.Security.Cryptography.HashAlgorithmName.SHA256, PasswordHashSize);
        return $"PBKDF2${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash) || !storedHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
            return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, expected.Length);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
