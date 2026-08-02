using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Evidencija radnika po obračunskim periodima — direktni port RADNICII.DBF + RADNICI.DBF
/// Jedan red = jedan radnik u jednom obračunskom periodu (Godina + Mesec).
/// Unikatna kombinacija: (BrojRadnika, Godina, Mesec).
/// </summary>
[Table("Radnici")]
public class Radnik
{
    [Key]
    // Auto-increment — Id više nije isti kao BrojRadnika
    public int Id { get; set; }

    // ── Obračunski period ────────────────────────────────────────────
    public int Godina { get; set; }
    public int Mesec { get; set; }

    // ── Identifikacija ───────────────────────────────────────────────
    /// <summary>RED_BROJ iz DBF — identifikator radnika (isti u svim periodima)</summary>
    public int BrojRadnika { get; set; }

    [Required, MaxLength(60)]
    public string ImeIPrezime { get; set; } = "";

    [MaxLength(13)]
    public string Jmbg { get; set; } = "";

    [MaxLength(20)]
    public string MaticniBroj { get; set; } = "";

    // ── Lični podaci ─────────────────────────────────────────────────
    public DateTime? DatumRodjenja { get; set; }

    [MaxLength(60)]
    public string MestoRodjenja { get; set; } = "";

    [MaxLength(80)]
    public string AdresaStanovanja { get; set; } = "";

    [MaxLength(40)]
    public string Mesto { get; set; } = "";

    [MaxLength(3)]
    public string SifraOpstine { get; set; } = "";

    /// <summary>Adresa za slanje platnog listića e-mailom (Faza 1.2).</summary>
    [MaxLength(120)]
    public string Email { get; set; } = "";

    // ── Podaci o zaposlenju ──────────────────────────────────────────
    public DateTime? DatumZaposlenja { get; set; }
    public DateTime? DatumPrestanka { get; set; }

    [MaxLength(10)]
    public string Kategorija { get; set; } = "";

    /// <summary>SVP šifra (npr. 101101000) — RADNO_M iz DBF</summary>
    [MaxLength(60)]
    public string Radno_Mesto { get; set; } = "";

    public int BrojRadneJedinice { get; set; } = 1;

    /// <summary>
    /// Šifra mesta troška iz ERPiFinansije (npr. „MT-01"). Veza je po šifri, a ne
    /// stranim ključem — ERPiZarade i ERPiFinansije rade nad zasebnim bazama.
    /// Koristi se za raspored troška zarade po mestima troška pri knjiženju (Faza 3.1).
    /// </summary>
    [MaxLength(20)]
    public string SifraMestaTroska { get; set; } = "";

    /// <summary>MIN_RAD — broj godina minulog rada</summary>
    public int MinuliRadGodine { get; set; }

    // ── Koeficijenti i osnova ────────────────────────────────────────
    [Column(TypeName = "decimal(10,4)")]
    public decimal Koeficijent { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal Koeficijent1 { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal OsnovnaPlata { get; set; }

    // ── Doprinosi i porezi ───────────────────────────────────────────
    [Column(TypeName = "decimal(6,4)")]
    public decimal StopaPio { get; set; }

    [Column(TypeName = "decimal(6,4)")]
    public decimal StopaZdravstvo { get; set; }

    [Column(TypeName = "decimal(6,4)")]
    public decimal StopaNezaposlenost { get; set; }

    // ── Bankarski podaci ─────────────────────────────────────────────
    [MaxLength(25)]
    public string BankovniRacun { get; set; } = "";

    [MaxLength(30)]
    public string NazivBanke { get; set; } = "";

    // ── Status ───────────────────────────────────────────────────────
    public bool Aktivan { get; set; } = true;

    // ── Poresko oslobođenje ──────────────────────────────────────────
    [Column(TypeName = "decimal(12,2)")]
    public decimal LicnoOslobodjenje { get; set; }

    // ── Poreske olakšice (čl. 21v/21j ZPDG i srodne) ─────────────────
    // Oznaka olakšice se NE čuva ovde — ona je već deo SVP šifre u `Radno_Mesto`
    // (pozicije 7–8) i unosi se padajućom listom u kartonu radnika. Ovde stoji samo
    // ono što iz oznake ne može da se izvede: koliko se vraća i do kada olakšica važi.

    /// <summary>Procenat povraćaja poreza po olakšici (npr. 70.00 za 70%).</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcenatPovracajaPoreza { get; set; }

    /// <summary>Procenat povraćaja doprinosa po olakšici.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcenatPovracajaDoprinosa { get; set; }

    /// <summary>Datum do kog olakšica važi; posle njega se ne primenjuje.</summary>
    public DateTime? OlaksicaVaziDo { get; set; }

    // ── Legacy / operativni podaci ───────────────────────────────────
    [MaxLength(10)]
    public string Operativni { get; set; } = "";

    // ── Evidencija ───────────────────────────────────────────────────
    public DateTime DatumUnosa { get; set; } = DateTime.Now;
    public DateTime? DatumIzmene { get; set; }

    // ── Navigaciona svojstva ─────────────────────────────────────────
    public ICollection<ObracunPlate> Obracuni { get; set; } = [];
    public ICollection<Kredit> Krediti { get; set; } = [];
    public ICollection<RadniSat> RadniSati { get; set; } = [];
}
