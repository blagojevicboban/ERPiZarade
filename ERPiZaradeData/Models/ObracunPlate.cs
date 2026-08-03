using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Mesečni obračun plate — port OBRACUN.DBF + OBRACUNI.DBF (istorija)
/// </summary>
[Table("ObracuniPlata")]
public class ObracunPlate : IPripadaIsplati
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Radnik))]
    public int RadnikId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    /// <summary>
    /// Isplata kojoj obračun pripada (Faza 2.2). <c>null</c> znači <b>prvu isplatu svog
    /// perioda</b> — tako svi zatečeni obračuni, nastali pre nego što je isplata uopšte
    /// postojala, ostaju obuhvaćeni bez ijedne izmene u njima. Pravilo se primenjuje na
    /// jednom mestu, u <c>IsplataService.Obuhvat</c>, da se ne bi razišlo po upitima.
    /// </summary>
    public int? IsplataId { get; set; }

    public Isplata? Isplata { get; set; }

    public bool Zakljucan { get; set; }

    /// <summary>
    /// Ugovor van radnog odnosa po kome je obračun nastao (Faza 2.3). <c>null</c> je zarada
    /// iz radnog odnosa — dakle sve što je postojalo do sada, pa se time nijedan zatečeni
    /// obračun ne menja.
    ///
    /// Obračun po ugovoru koristi ista polja kao zarada (bruto, porez, doprinosi, neto), pa
    /// prijave, nalozi i godišnja potvrda rade nad njim bez ijedne izmene. Razlikuje ga samo
    /// ono što se iz iznosa ne vidi: šifra vrste prihoda i to što se ne meri satima.
    /// </summary>
    public int? UgovorId { get; set; }

    public Ugovor? Ugovor { get; set; }

    /// <summary>
    /// Osnovica na koju su obračunati doprinosi. <c>null</c> znači „izvedi je kao i do sada" —
    /// iz zbira PIO doprinosa i ukupne stope — i takvi su svi obračuni zarade. Upisuje se samo
    /// tamo gde se izvesti ne može: kod prihoda van radnog odnosa osnovica je bruto umanjen za
    /// normirane troškove, pa bi izvođenje po stopi zarade dalo pogrešan broj u prijavi.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal? OsnovicaDoprinosa { get; set; }

    /// <summary>
    /// Razloženi sastav bruto iznosa po vrstama primanja (Faza 2.1). Zbir stavki jednak je
    /// ukupnom bruto iznosu obračuna — kolone iznad ostaju netaknute, pa stariji ekrani i
    /// štampe rade nepromenjeno.
    /// </summary>
    public ICollection<ObracunStavka> Stavke { get; set; } = [];

    // ── Storniranje (Faza 2.7) ─────────────────────────────
    /// <summary>
    /// Obračun je poništen, ali ostaje u istoriji. Iznosi se <b>ne brišu i ne nuliraju</b> —
    /// zna se šta je bilo obračunato — ali se stornirani obračun izostavlja svuda gde se
    /// novac isplaćuje ili prijavljuje: PPP-PD, nalozi za prenos, platni listići, PPP-PO.
    ///
    /// Storniranje je jedina radnja dozvoljena nad <see cref="Zakljucan"/> obračunom.
    /// Otključavanje perioda zbog jedne greške izlaže izmeni i sve ostale obračune.
    /// </summary>
    public bool Storniran { get; set; }

    public DateTime? DatumStorniranja { get; set; }

    /// <summary>Razlog se traži pri storniranju — bez njega se posle ne zna zašto obračuna nema.</summary>
    [MaxLength(200)]
    public string RazlogStorniranja { get; set; } = "";

    /// <summary>
    /// Redni broj verzije obračuna. Prekalkulacija briše zatečeni rezultat, pa se on pre
    /// brisanja arhivira u <see cref="ObracunVerzija"/>, a novi obračun dobija sledeći broj.
    /// Prva verzija je 1.
    /// </summary>
    public int Verzija { get; set; } = 1;

    // ── Poreska olakšica (Faza 2.4) ────────────────────────
    /// <summary>OL oznaka olakšice primenjene na ovaj obračun; prazno ako je nema.</summary>
    [MaxLength(2)]
    public string OlaksicaOznaka { get; set; } = "";

    /// <summary>
    /// Iznos poreza obuhvaćen olakšicom. Kod oslobođenja je to umanjenje koje je već
    /// odbijeno od <see cref="PorezNaDohodak"/>; kod povraćaja je iznos koji se traži
    /// natrag, a porez je plaćen u punom iznosu.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal OlaksicaPorez { get; set; }

    /// <summary>Isto za doprinose na teret radnika.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal OlaksicaDoprinosi { get; set; }

    /// <summary>
    /// Da li je olakšica umanjila ono što se plaća. Netačno znači povraćaj — iznosi su
    /// plaćeni u celosti i traže se posebnim zahtevom.
    /// </summary>
    public bool OlaksicaUmanjujeUplatu { get; set; }

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
    /// <summary>Kratka oznaka storna za tabele; crtica kad obračun važi.</summary>
    [NotMapped]
    public string StornoStr => Storniran ? "STORNO" : "—";

    /// <summary>
    /// Isplata za tabele. Prazno je za obračun koji pripada prvoj i jedinoj isplati meseca —
    /// tako kolona ostaje neupadljiva dok se ne pojavi druga isplata.
    /// </summary>
    [NotMapped]
    public string IsplataStr => Isplata == null || Isplata.JePrva ? "" : Isplata.Naziv;

    /// <summary>Obračun je naknada po ugovoru van radnog odnosa, a ne zarada.</summary>
    [NotMapped]
    public bool JeVanRadnogOdnosa => UgovorId.HasValue;

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
