using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>Status PPP-PD prijave kod Poreske uprave.</summary>
public enum StatusPrijave
{
    /// <summary>XML je generisan, ali još nije predat.</summary>
    Pripremljena = 0,
    Podneta = 1,
    Prihvacena = 2,
    Odbijena = 3,
    Stornirana = 4
}

/// <summary>
/// Evidencija podnetih PPP-PD prijava. Postoji zbog dve stvari koje se nigde nisu čuvale:
/// <b>BOP</b> (broj odobrenja za plaćanje) — bez njega se ne mogu formirati nalozi za prenos
/// poreza i doprinosa (Faza 1.1) — i <b>status prijave</b>, da bi se videlo šta je prihvaćeno
/// a šta odbijeno.
///
/// Ključ je za sada (Godina, Mesec, RedniBroj). <see cref="RedniBroj"/> unapred razdvaja
/// više prijava u istom mesecu (akontacija + konačna isplata) i postaje veza ka entitetu
/// „Isplata" iz Faze 2.2 bez ponovne izmene šeme.
/// </summary>
[Table("PppPdPrijave")]
public class PppPdPrijava
{
    [Key]
    public int Id { get; set; }

    // ── Obračunski period ────────────────────────────────────────────
    public int Godina { get; set; }
    public int Mesec { get; set; }

    /// <summary>Redni broj prijave unutar meseca (1 = prva/jedina isplata).</summary>
    public int RedniBroj { get; set; } = 1;

    // ── Sadržaj prijave ──────────────────────────────────────────────
    /// <summary>Vrsta prijave po šifarniku PU: 1=originalna, 3=izmenjena, 5=otkazana.</summary>
    [MaxLength(2)]
    public string VrstaPrijave { get; set; } = "1";

    [MaxLength(50)]
    public string KlijentskaOznaka { get; set; } = "";

    public DateTime DatumPlacanja { get; set; }

    public int BrojZaposlenih { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal ZbirPoreza { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal ZbirDoprinosa { get; set; }

    // ── Odgovor Poreske uprave ───────────────────────────────────────
    /// <summary>
    /// Broj odobrenja za plaćanje — poziv na broj na nalozima za prenos poreza i doprinosa.
    /// Dobija se tek pošto PU prihvati prijavu, pa je prazan do tada.
    /// </summary>
    [MaxLength(30)]
    public string Bop { get; set; } = "";

    public StatusPrijave Status { get; set; } = StatusPrijave.Pripremljena;

    public DateTime? DatumPodnosenja { get; set; }

    /// <summary>Kada je status poslednji put promenjen (prihvatanje ili odbijanje).</summary>
    public DateTime? DatumStatusa { get; set; }

    /// <summary>Obrazloženje kad je prijava odbijena.</summary>
    [MaxLength(500)]
    public string Napomena { get; set; } = "";

    // ── Trag ─────────────────────────────────────────────────────────
    [MaxLength(260)]
    public string PutanjaFajla { get; set; } = "";

    public DateTime DatumKreiranja { get; set; } = DateTime.Now;
}
