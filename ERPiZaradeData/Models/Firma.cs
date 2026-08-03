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

    /// <summary>
    /// Šifra opštine sedišta po šifarniku Poreske uprave — ide u PPP-PD zaglavlje
    /// (element SedistePrebivaliste). Do sada je stajala u podešavanjima aplikacije,
    /// što je za agencije značilo jednu vrednost za sve firme.
    /// </summary>
    [MaxLength(3)]
    public string SifraOpstine { get; set; } = "";

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

    /// <summary>
    /// Lice koje firmu zastupa pri potpisivanju. Ugovor van radnog odnosa se zaključuje
    /// „koga zastupa…", pa bez ovog polja generisani dokument ostaje sa prazninom koju
    /// korisnik popunjava rukom u svakom ugovoru.
    /// </summary>
    [MaxLength(60)]
    public string Zastupnik { get; set; } = "";

    /// <summary>Funkcija zastupnika („direktor", „zakonski zastupnik").</summary>
    [MaxLength(40)]
    public string FunkcijaZastupnika { get; set; } = "";

    [MaxLength(500)]
    public string Napomena { get; set; } = "";
}
