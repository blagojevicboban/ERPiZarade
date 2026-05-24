using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataData.Models;

/// <summary>
/// Matična evidencija radnika — port RADNICI.DBF
/// </summary>
[Table("Radnici")]
public class Radnik
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    // Identifikacija
    [Required, MaxLength(60)]
    public string ImeIPrezime { get; set; } = "";

    [MaxLength(13)]
    public string Jmbg { get; set; } = "";

    public int BrojRadnika { get; set; }

    [MaxLength(20)]
    public string MaticniBroj { get; set; } = "";

    // Lični podaci
    public DateTime? DatumRodjenja { get; set; }

    [MaxLength(60)]
    public string MestoRodjenja { get; set; } = "";

    [MaxLength(80)]
    public string AdresaStanovanja { get; set; } = "";

    [MaxLength(40)]
    public string Mesto { get; set; } = "";

    // Podaci o zaposlenju
    public DateTime? DatumZaposlenja { get; set; }
    public DateTime? DatumPrestanka { get; set; }

    [MaxLength(10)]
    public string Kategorija { get; set; } = "";

    [MaxLength(60)]
    public string Radno_Mesto { get; set; } = "";

    public int BrojRadneJedinice { get; set; } = 1;

    // Koeficijenti i osnova
    [Column(TypeName = "decimal(10,4)")]
    public decimal Koeficijent { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal OsnovnaPlata { get; set; }

    // Doprinosi i porezi
    [Column(TypeName = "decimal(6,4)")]
    public decimal StopaPio { get; set; }

    [Column(TypeName = "decimal(6,4)")]
    public decimal StopaZdravstvo { get; set; }

    [Column(TypeName = "decimal(6,4)")]
    public decimal StopaNezaposlenost { get; set; }

    // Bankovski podaci
    [MaxLength(25)]
    public string BankovniRacun { get; set; } = "";

    [MaxLength(30)]
    public string NazivBanke { get; set; } = "";

    // Status
    public bool Aktivan { get; set; } = true;

    // Poresko oslobođenje
    [Column(TypeName = "decimal(12,2)")]
    public decimal LicnoOslobodjenje { get; set; }

    // Evidencija
    public DateTime DatumUnosa { get; set; } = DateTime.Now;
    public DateTime? DatumIzmene { get; set; }

    // Navigaciona svojstva
    public ICollection<ObracunPlate> Obracuni { get; set; } = [];
    public ICollection<Kredit> Krediti { get; set; } = [];
    public ICollection<RadniSat> RadniSati { get; set; } = [];
}
