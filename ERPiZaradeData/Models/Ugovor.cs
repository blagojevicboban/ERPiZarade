using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Tip primaoca prihoda — pozicije 2–3 šifre vrste prihoda (oznaka <c>PP</c>).
///
/// Vrednosti su propisane Pravilnikom o poreskoj prijavi za porez po odbitku i ne biraju se
/// po vrsti posla nego po <b>statusu osiguranja primaoca</b>: isti ugovor o delu nosi drugu
/// šifru kad ga potpiše zaposleno lice, a drugu kad ga potpiše lice bez osiguranja.
/// </summary>
public enum TipPrimaocaPrihoda
{
    /// <summary>01 — lice koje je zaposleno (kod ovog ili drugog poslodavca).</summary>
    Zaposleni = 1,

    /// <summary>02 — osnivač odnosno član privrednog društva zaposlen u svom društvu.</summary>
    OsnivacZaposlenUSvomDrustvu = 2,

    /// <summary>03 — lice osigurano po osnovu samostalne delatnosti, uključujući samostalne umetnike.</summary>
    SamostalnaDelatnost = 3,

    /// <summary>04 — lice osigurano po osnovu poljoprivredne delatnosti.</summary>
    Poljoprivrednik = 4,

    /// <summary>05 — lice koje nije osigurano po drugom osnovu.</summary>
    NijeOsiguranPoDrugomOsnovu = 5,

    /// <summary>06 — nerezident.</summary>
    Nerezident = 6,

    /// <summary>07 — invalidno lice; koristi se samo uz prihod iz radnog odnosa.</summary>
    InvalidnoLice = 7,

    /// <summary>08 — vojni osiguranik.</summary>
    VojniOsiguranik = 8
}

/// <summary>
/// Šifarnik vrsta ugovora van radnog odnosa (Faza 2.3).
///
/// Ugovor o delu, autorska naknada, privremeni i povremeni poslovi i naknada članovima
/// organa upravljanja razlikuju se <b>samo brojevima</b>: koliko se priznaje normiranih
/// troškova, po kojoj stopi ide porez, koji se doprinosi plaćaju i na čiji teret. Sve to
/// propis menja, pa stoji ovde kao red u šifarniku, a ne u kodu — isto pravilo po kome su
/// uvedene <see cref="VrstaPrimanja"/> i <see cref="PoreskaOlaksica"/>.
///
/// <see cref="Ovp"/> je samo <b>srednji deo</b> šifre vrste prihoda. Ceo devetocifreni broj
/// zavisi i od statusa primaoca (<see cref="TipPrimaocaPrihoda"/>), pa se sastavlja pri
/// obračunu, a ne upisuje ovde — inače bi svaka kombinacija posla i statusa tražila svoj red.
/// </summary>
[Table("VrsteUgovora")]
public class VrstaUgovora
{
    [Key]
    public int VrstaUgovoraId { get; set; }

    /// <summary>Kratka šifra za prepoznavanje u izveštajima (npr. „UOD", „AUT50").</summary>
    [Required, MaxLength(10)]
    public string Sifra { get; set; } = "";

    [Required, MaxLength(80)]
    public string Naziv { get; set; } = "";

    /// <summary>
    /// Oznaka vrste prihoda iz Kataloga vrste prihoda — tri cifre, pozicije 4–6 SVP šifre
    /// (601 ugovor o delu, 301–323 autorske naknade, 150–152 privremeni i povremeni poslovi).
    /// Prazno znači da šifra nije potvrđena; obračun po takvoj vrsti prolazi, ali kontrolne
    /// provere na to upozoravaju jer prijava bez SVP šifre biva odbijena.
    /// </summary>
    [MaxLength(3)]
    public string Ovp { get; set; } = "";

    /// <summary>
    /// Normirani troškovi u procentima bruto naknade. Za ugovor o delu 20, za autorske
    /// naknade 50, 43 ili 34 zavisno od vrste dela; za privremene i povremene poslove 0,
    /// jer se ta naknada oporezuje kao zarada.
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal NormiraniTroskoviProcenat { get; set; }

    /// <summary>Stopa poreza na dohodak u procentima (20 za druge prihode, 10 za zaradu).</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaPoreza { get; set; }

    // ── Doprinosi na teret primaoca (skidaju se sa naknade) ──────────────
    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaPioPrimalac { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaZdravstvoPrimalac { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaNezaposlenostPrimalac { get; set; }

    // ── Doprinosi na teret isplatioca (dodatni trošak firme) ─────────────
    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaPioIsplatilac { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaZdravstvoIsplatilac { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal StopaNezaposlenostIsplatilac { get; set; }

    /// <summary>Konto za automatsko knjiženje u ERPiFinansije (Faza 3.1).</summary>
    [MaxLength(10)]
    public string Konto { get; set; } = "";

    /// <summary>
    /// Šifra plaćanja na nalogu za prenos. Naknada van radnog odnosa nije zarada, pa nosi
    /// drugu šifru od isplate zarada — a koju, propisuje NBS, ne program.
    /// </summary>
    [MaxLength(3)]
    public string SifraPlacanja { get; set; } = "";

    public int Redosled { get; set; }

    public bool Aktivna { get; set; } = true;

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    public ICollection<Ugovor> Ugovori { get; set; } = [];

    /// <summary>Zbir stopa koje umanjuju naknadu primaocu — porez ide posebno.</summary>
    [NotMapped]
    public decimal ZbirStopaPrimaoca => StopaPioPrimalac + StopaZdravstvoPrimalac + StopaNezaposlenostPrimalac;

    [NotMapped]
    public decimal ZbirStopaIsplatioca => StopaPioIsplatilac + StopaZdravstvoIsplatilac + StopaNezaposlenostIsplatilac;

    [NotMapped]
    public string NazivSaSifrom => $"{Sifra} — {Naziv}";
}

/// <summary>
/// Zaključen ugovor van radnog odnosa (Faza 2.3).
///
/// Primalac je zapis u <see cref="Radnik"/> označen sa <see cref="Radnik.VanRadnogOdnosa"/>:
/// tamo već stoji sve što isplata traži — JMBG, opština prebivališta, tekući račun, e-mail —
/// pa bi zaseban registar primalaca bio drugo mesto za iste podatke. Veza ide preko
/// <see cref="BrojRadnika"/>, jer je karton periodičan a ugovor nije.
///
/// Ugovor <b>nije</b> obračun: on je osnov, a isplaćuje se kroz <see cref="ObracunPlate"/>
/// vezan za <see cref="Isplata"/>. Zato jedan ugovor može imati više isplata (rate), i zato
/// se obračun po ugovoru ne vezuje za obračunski mesec nego za isplatu.
/// </summary>
[Table("Ugovori")]
public class Ugovor
{
    [Key]
    public int UgovorId { get; set; }

    [ForeignKey(nameof(VrstaUgovora))]
    public int VrstaUgovoraId { get; set; }

    /// <summary>Identifikator primaoca — isti broj koji karton radnika nosi kroz sve periode.</summary>
    public int BrojRadnika { get; set; }

    /// <summary>
    /// Status osiguranja primaoca u trenutku isplate. Bira se po licu, a ne po poslu, i
    /// određuje pozicije 2–3 šifre vrste prihoda.
    /// </summary>
    public TipPrimaocaPrihoda TipPrimaoca { get; set; } = TipPrimaocaPrihoda.NijeOsiguranPoDrugomOsnovu;

    [MaxLength(20)]
    public string Broj { get; set; } = "";

    /// <summary>Predmet ugovora — ide u svrhu plaćanja na nalogu i u obračunski listić.</summary>
    [MaxLength(200)]
    public string Predmet { get; set; } = "";

    public DateTime DatumZakljucenja { get; set; } = DateTime.Today;

    public DateTime? DatumOd { get; set; }
    public DateTime? DatumDo { get; set; }

    /// <summary>Ugovoreni iznos; podrazumevani predlog pri obračunu isplate po ugovoru.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal UgovorenIznos { get; set; }

    /// <summary>
    /// Da li je <see cref="UgovorenIznos"/> ugovoren kao neto „na ruke". U praksi se najčešće
    /// ugovara neto, pa se bruto dobija preračunom — a preračun mora biti tačan jer se od
    /// bruta računaju i porez i doprinosi.
    /// </summary>
    public bool IznosJeNeto { get; set; }

    public bool Aktivan { get; set; } = true;

    /// <summary>
    /// Tekst zaključenog ugovora, generisan iz šablona pa po potrebi izmenjen ručno.
    ///
    /// Čuva se <b>uz ugovor, ne uz šablon</b>: šablon se s vremenom menja, a potpisani ugovor
    /// mora ostati onakav kakav je potpisan. Iz istog razloga ponovno generisanje prepisuje
    /// tekst tek posle izričite potvrde.
    ///
    /// Iznosi se iz teksta <b>ne čitaju</b> — obračun ide iz polja ugovora. Tekst je dokument,
    /// a ne izvor podataka; da je obrnuto, ispravka slovne greške bi menjala isplatu.
    /// </summary>
    public string Tekst { get; set; } = "";

    /// <summary>Kada je tekst poslednji put generisan ili izmenjen; prazno dok ga nema.</summary>
    public DateTime? DatumTeksta { get; set; }

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    public DateTime DatumUnosa { get; set; } = DateTime.Now;

    public VrstaUgovora VrstaUgovora { get; set; } = null!;

    public ICollection<ObracunPlate> Obracuni { get; set; } = [];

    [NotMapped]
    public string PeriodStr => DatumOd.HasValue
        ? $"{DatumOd:dd.MM.yyyy}–{(DatumDo.HasValue ? DatumDo.Value.ToString("dd.MM.yyyy") : "…")}"
        : "";

    /// <summary>Pozicije 2–3 SVP šifre — tip primaoca kao dvocifreni broj.</summary>
    [NotMapped]
    public string OznakaPrimaoca => ((int)TipPrimaoca).ToString("D2");
}
