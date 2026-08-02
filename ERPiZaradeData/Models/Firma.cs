using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

[Table("Firme")]
public class Firma
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public string Naziv { get; set; } = "";

    [MaxLength(200)]
    public string Adresa { get; set; } = "";

    [MaxLength(100)]
    public string Grad { get; set; } = "";

    [MaxLength(30)]
    public string Pib { get; set; } = ""; // PIB / PDV ID

    [MaxLength(30)]
    public string Mb { get; set; } = "";  // Matični broj / MB

    [MaxLength(100)]
    public string BankovniRacun { get; set; } = "";

    [MaxLength(50)]
    public string SifraPlacanja { get; set; } = "";

    [MaxLength(50)]
    public string Telefon { get; set; } = "";

    [MaxLength(100)]
    public string Email { get; set; } = "";

    [MaxLength(500)]
    public string Napomena { get; set; } = "";
}
