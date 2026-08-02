using System.ComponentModel.DataAnnotations;

namespace ERPiZaradeData.Models;

/// <summary>
/// Neradni dan u kalendaru. Popunjava se zakonskim praznicima za godinu, ali ostaje
/// izmenjiv — firme imaju i sopstvene neradne dane (slava, kolektivni godišnji), a zakon
/// se menja.
/// </summary>
public class Praznik
{
    [Key]
    public int PraznikId { get; set; }

    public DateTime Datum { get; set; }

    [Required, MaxLength(80)]
    public string Naziv { get; set; } = "";

    /// <summary>
    /// Da li se tog dana ne radi. Neki praznici se obeležavaju a rade se, pa ne ulaze u
    /// obračun fonda sati.
    /// </summary>
    public bool Neradni { get; set; } = true;

    /// <summary>
    /// Da li je zapis uneo korisnik. Ponovno popunjavanje zakonskih praznika ne sme da
    /// obriše ono što je firma sama dodala.
    /// </summary>
    public bool RucniUnos { get; set; }
}
