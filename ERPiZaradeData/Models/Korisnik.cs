using System.ComponentModel.DataAnnotations;

namespace ERPiZaradeData.Models;

public enum UlogaKorisnika
{
    Administrator = 0,
    Operater = 1,
    Gledalac = 2
}

public class Korisnik
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ImePrezime { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string KorisnickoIme { get; set; } = string.Empty;

    [Required]
    public string LozinkaHash { get; set; } = string.Empty;

    public UlogaKorisnika Uloga { get; set; } = UlogaKorisnika.Operater;

    public bool JeAktivan { get; set; } = true;

    public DateTime? PoslednjaPrijava { get; set; }
}
