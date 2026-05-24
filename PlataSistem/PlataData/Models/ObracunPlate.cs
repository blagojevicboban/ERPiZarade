using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataData.Models;

/// <summary>
/// Mesečni obračun plate — port OBRACUN.DBF + OBRACUNI.DBF (istorija)
/// </summary>
[Table("ObracuniPlata")]
public class ObracunPlate
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Radnik))]
    public int RadnikId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    // ── BRUTO ──────────────────────────────────────────────
    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoZarada { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoBolovanje { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoNaknade { get; set; }      // prekovremeni, noćni, praznici

    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoStimulacija { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoMinuliRad { get; set; }

    // ── DOPRINOSI NA TERET RADNIKA ─────────────────────────
    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosPioRadnik { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosZdravstvoRadnik { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosNezaposlenostRadnik { get; set; }

    // ── DOPRINOSI NA TERET POSLODAVCA ─────────────────────
    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosPioPoslodavac { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosZdravstvoPoslodavac { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DoprinosNezaposlenostPoslodavac { get; set; }

    // ── POREZ ──────────────────────────────────────────────
    [Column(TypeName = "decimal(14,2)")]
    public decimal PorezNaDohodak { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PoreskaOsnovica { get; set; }

    // ── ODBICI ─────────────────────────────────────────────
    [Column(TypeName = "decimal(14,2)")]
    public decimal KreditObustava { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Samodoprinosi { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal OstaliOdbici { get; set; }

    // ── NETO ───────────────────────────────────────────────
    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoIsplata { get; set; }

    // ── SATI ───────────────────────────────────────────────
    public int RedovniSati { get; set; }
    public int BolovanjeSati { get; set; }
    public int PrekovremeneSati { get; set; }
    public int GodisnjioOdmorSati { get; set; }

    // ── META ───────────────────────────────────────────────
    public bool Zakljucen { get; set; } = false;
    public DateTime DatumObracuna { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(14,4)")]
    public decimal Prosek { get; set; }

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    // Navigacija
    public Radnik Radnik { get; set; } = null!;
}
