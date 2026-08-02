using System.ComponentModel.DataAnnotations;

namespace ERPiZaradeData.Models;

public enum IshodSlanja
{
    Poslato = 0,
    Neuspesno = 1,

    /// <summary>Radnik nema e-mail adresu — nije ni pokušano.</summary>
    Preskoceno = 2
}

/// <summary>
/// Evidencija slanja platnih listića e-mailom.
///
/// Nije pomoćni log nego obaveza: slanjem listića iznose se lični podaci (JMBG, zarada)
/// iz kontrolisanog okruženja, pa po Zakonu o zaštiti podataka o ličnosti mora da postoji
/// trag kome je, kada i na koju adresu podatak poslat. Adresa i ime radnika su namerno
/// denormalizovani — zapis mora da ostane čitljiv i pošto se karton radnika izmeni.
/// </summary>
public class SlanjeListica
{
    [Key]
    public int SlanjeListicaId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    public int BrojRadnika { get; set; }

    [MaxLength(60)]
    public string ImeRadnika { get; set; } = "";

    /// <summary>Adresa na koju je listić stvarno poslat, u trenutku slanja.</summary>
    [MaxLength(120)]
    public string Email { get; set; } = "";

    public IshodSlanja Ishod { get; set; }

    /// <summary>Da li je PDF bio zaštićen lozinkom.</summary>
    public bool ZasticenLozinkom { get; set; }

    /// <summary>Razlog neuspeha ili preskakanja.</summary>
    [MaxLength(300)]
    public string? Napomena { get; set; }

    /// <summary>Ko je pokrenuo slanje.</summary>
    public int? KorisnikId { get; set; }

    [MaxLength(100)]
    public string? KorisnickoIme { get; set; }

    public DateTime Vreme { get; set; } = DateTime.Now;
}
