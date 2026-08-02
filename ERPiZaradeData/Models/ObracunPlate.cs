using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

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

    public bool Zakljucan { get; set; }

    /// <summary>
    /// Razloženi sastav bruto iznosa po vrstama primanja (Faza 2.1). Zbir stavki jednak je
    /// ukupnom bruto iznosu obračuna — kolone iznad ostaju netaknute, pa stariji ekrani i
    /// štampe rade nepromenjeno.
    /// </summary>
    public ICollection<ObracunStavka> Stavke { get; set; } = [];

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

    private decimal _poreskaOsnovica;
    [Column(TypeName = "decimal(14,2)")]
    public decimal PoreskaOsnovica
    {
        get
        {
            if (_poreskaOsnovica == 0 && PorezNaDohodak > 0)
            {
                return Math.Max(0, Neto - LicniOdbitak);
            }
            return _poreskaOsnovica;
        }
        set => _poreskaOsnovica = value;
    }

    /// <summary>DBF polje 'umanjenje' = licni odbitak (SAMODOP.PRG: sum_umanj)</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal LicniOdbitak { get; set; }

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
    public int SmenskiSati { get; set; }
    public int RadPraznikomSati { get; set; }
    public int NocniRadPraznikomSati { get; set; }
    public int PlacenoOdsustvoSati { get; set; }

    // ── META ───────────────────────────────────────────────
    // Napomena: nekadašnje polje `Zakljucen` je uklonjeno — bilo je duplikat
    // `Zakljucan` (linija 21) koji je jedini izvor istine za zaključavanje.
    public DateTime DatumObracuna { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(14,4)")]
    public decimal Prosek { get; set; }

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    // ── DODATNO MIGRIRANE LEGACY KOLONE IZ OBRACUN.DBF / OBRACUNI.DBF ──
    [Column(TypeName = "decimal(14,2)")]
    public decimal Koeficijent { get; set; }

    public int MinuliRadGodine { get; set; }

    [MaxLength(20)]
    public string Kategorija { get; set; } = "";

    public int BrojRadneJedinice { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal UkupnoRadnihSatiLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal FondSatiMesecni { get; set; }

    [Column(TypeName = "decimal(14,5)")]
    public decimal CenaSataRedovan { get; set; }

    [Column(TypeName = "decimal(14,5)")]
    public decimal CenaSataMinuliRad { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DodaciLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DodatakNaM1 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DodatakNaM2 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DodatakNaM3 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoOsnovica { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TopliObrokIznos { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal BrutoPioOsnovica { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoNaknadeLegacy { get; set; }

    [MaxLength(20)]
    public string Operativni { get; set; } = "";

    [MaxLength(20)]
    public string Oznaka { get; set; } = "";

    [Column(TypeName = "decimal(14,2)")]
    public decimal NedeljaSati { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal BolovanjePreko60SatiLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PorodiljskoOdsustvoSatiLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PlacenoOdsustvoSatiLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PlacenoZakonskiSatiLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Bolovanje100SatiLegacy { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MinimalnaPlataOsnovica { get; set; }

    public int SifraSamodoprinosa1 { get; set; }
    public int SifraSamodoprinosa2 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PosebanPorez { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoPorez { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetoBezPoreza { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Varijabila { get; set; }


    // ── NOT MAPPED HELPERS FOR UI BINDINGS ────────────────
    [NotMapped]
    public int UkupnoSati => RedovniSati + BolovanjeSati + PrekovremeneSati + GodisnjioOdmorSati + DrzavniPraznikSati + NocniSati + SmenskiSati + RadPraznikomSati + NocniRadPraznikomSati + PlacenoOdsustvoSati;

    [NotMapped]
    public decimal UkupnoBruto => BrutoZarada + BrutoBolovanje;

    [NotMapped]
    public decimal UkupniDoprinosi => DoprinosPioRadnik + DoprinosZdravstvoRadnik + DoprinosNezaposlenostRadnik;

    [NotMapped]
    public decimal NetoPreDoprinosa => BrutoZarada + BrutoBolovanje - PorezNaDohodak - (DoprinosPioRadnik + DoprinosZdravstvoRadnik + DoprinosNezaposlenostRadnik);

    [NotMapped]
    public decimal Bruto1 => UkupnoBruto;

    [NotMapped]
    public decimal UkupniDoprinosiPoslodavca => DoprinosPioPoslodavac + DoprinosZdravstvoPoslodavac + DoprinosNezaposlenostPoslodavac;

    [NotMapped]
    public decimal UkupniOdbici => KreditObustava + Samodoprinosi + OstaliOdbici;

    /// <summary>Bruto 2 = Bruto 1 + doprinosi na teret poslodavca (ukupan teret poslodavca)</summary>
    [NotMapped]
    public decimal Bruto2 => Bruto1 + UkupniDoprinosiPoslodavca;

    [NotMapped]
    public decimal UkupnaMasaZaIsplatu => Bruto2;

    [NotMapped]
    public string StopaPioRadnikStr { get; set; } = "14.00%";

    [NotMapped]
    public string StopaZdravstvoRadnikStr { get; set; } = "5.15%";

    [NotMapped]
    public string StopaNezaposlenostRadnikStr { get; set; } = "0.75%";

    [NotMapped]
    public string StopaPioPoslodavacStr { get; set; } = "10.00%";

    [NotMapped]
    public string StopaZdravstvoPoslodavacStr { get; set; } = "5.15%";

    [NotMapped]
    public string StopaNezaposlenostPoslodavacStr { get; set; } = "0.00%";

    // Navigacija
    public Radnik Radnik { get; set; } = null!;
}
