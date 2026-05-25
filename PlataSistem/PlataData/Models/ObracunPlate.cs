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

    // ── LEGACY DETALJNI BRUTO DELOVI (PORT IZ DBF KOJI ODGOVARA STAMPE.PRG) ──
    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoZar { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoNerd { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoGOd { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoTo { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoReg { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Neto { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoBol { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoB100 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoPlac { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoPlZ { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoDrza { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoNocni { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoVezba { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoPrek { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoTer { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal KorDod { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal KorDod1 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Kumul { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoNede { get; set; }


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
    public int DrzavniPraznikSati { get; set; }
    public int NocniSati { get; set; }

    // ── META ───────────────────────────────────────────────
    public bool Zakljucen { get; set; } = false;
    public DateTime DatumObracuna { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(14,4)")]
    public decimal Prosek { get; set; }

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    // ── NOT MAPPED HELPERS FOR UI BINDINGS ────────────────
    [NotMapped]
    public int UkupnoSati => RedovniSati + BolovanjeSati + PrekovremeneSati + GodisnjioOdmorSati + DrzavniPraznikSati + NocniSati;

    [NotMapped]
    public decimal UkupnoBruto => BrutoZarada + BrutoBolovanje;

    [NotMapped]
    public decimal UkupniDoprinosi => DoprinosPioRadnik + DoprinosZdravstvoRadnik + DoprinosNezaposlenostRadnik;

    [NotMapped]
    public decimal NetoPreDoprinosa => BrutoZarada + BrutoBolovanje - PorezNaDohodak - (DoprinosPioRadnik + DoprinosZdravstvoRadnik + DoprinosNezaposlenostRadnik);

    [NotMapped]
    public decimal Bruto2 => BrutoZarada + BrutoBolovanje + DoprinosPioPoslodavac + DoprinosZdravstvoPoslodavac + DoprinosNezaposlenostPoslodavac;

    // Navigacija
    public Radnik Radnik { get; set; } = null!;
}
