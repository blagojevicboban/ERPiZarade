using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Ugovori;

/// <summary>
/// Red u tabeli ugovora: sam ugovor plus ono što se o njemu vidi tek kad se nađe karton
/// primaoca i prebroje obračunate isplate po njemu.
/// </summary>
public class UgovorRed
{
    public required Ugovor Ugovor { get; init; }

    public int UgovorId => Ugovor.UgovorId;
    public string Broj => Ugovor.Broj;
    public string VrstaNaziv => Ugovor.VrstaUgovora?.Naziv ?? "";
    public int BrojRadnika => Ugovor.BrojRadnika;

    /// <summary>Ime iz kartona primaoca; upozorenje kad kartona nema.</summary>
    public string Primalac { get; init; } = "";

    public string Predmet => Ugovor.Predmet;
    public decimal UgovorenIznos => Ugovor.UgovorenIznos;

    /// <summary>Da li je ugovoreni iznos neto — od toga zavisi šta preračun radi.</summary>
    public string VrstaIznosa => Ugovor.IznosJeNeto ? "neto" : "bruto";

    public string TipPrimaocaNaziv => UgovorRed.NazivTipaPrimaoca(Ugovor.TipPrimaoca);

    /// <summary>Šifra vrste prihoda koju bi obračun po ovom ugovoru dobio.</summary>
    public string Svp { get; init; } = "";

    /// <summary>Koliko je puta po ugovoru već obračunata naknada (bez storniranih).</summary>
    public int BrojIsplata { get; init; }

    public decimal IsplaceniBruto { get; init; }

    public string AktivanStr => Ugovor.Aktivan ? "da" : "—";

    /// <summary>Da li ugovor ima tekst dokumenta i kada je poslednji put snimljen.</summary>
    public string DokumentStr => string.IsNullOrWhiteSpace(Ugovor.Tekst)
        ? "—"
        : Ugovor.DatumTeksta?.ToString("dd.MM.yyyy") ?? "ima";

    public static string NazivTipaPrimaoca(TipPrimaocaPrihoda tip) => tip switch
    {
        TipPrimaocaPrihoda.Zaposleni => "01 — zaposleno lice",
        TipPrimaocaPrihoda.OsnivacZaposlenUSvomDrustvu => "02 — osnivač zaposlen u svom društvu",
        TipPrimaocaPrihoda.SamostalnaDelatnost => "03 — samostalna delatnost",
        TipPrimaocaPrihoda.Poljoprivrednik => "04 — poljoprivrednik",
        TipPrimaocaPrihoda.NijeOsiguranPoDrugomOsnovu => "05 — nije osiguran po drugom osnovu",
        TipPrimaocaPrihoda.Nerezident => "06 — nerezident",
        TipPrimaocaPrihoda.InvalidnoLice => "07 — invalidno lice",
        TipPrimaocaPrihoda.VojniOsiguranik => "08 — vojni osiguranik",
        TipPrimaocaPrihoda.PenzionerPoOsnovuZaposlenosti => "09 — penzioner po osnovu zaposlenosti",
        TipPrimaocaPrihoda.PenzionerPoOsnovuSamostalneDelatnosti => "10 — penzioner po osnovu samostalne delatnosti",
        TipPrimaocaPrihoda.NemaDoprinosaVanRadnogOdnosa => "11 — van radnog odnosa, bez doprinosa",
        TipPrimaocaPrihoda.VojniPenzioner => "12 — vojni penzioner",
        TipPrimaocaPrihoda.PoljoprivredniPenzioner => "13 — poljoprivredni penzioner",
        _ => tip.ToString()
    };
}

/// <summary>Stavka padajuće liste tipova primaoca prihoda.</summary>
public sealed class TipPrimaocaStavka
{
    public required TipPrimaocaPrihoda Tip { get; init; }
    public string Naziv => UgovorRed.NazivTipaPrimaoca(Tip);
}

/// <summary>Stavka padajuće liste primalaca — lica označenih kao van radnog odnosa.</summary>
public sealed class PrimalacStavka
{
    public required int BrojRadnika { get; init; }
    public required string ImeIPrezime { get; init; }

    /// <summary>
    /// Da li je lice i u radnom odnosu. Zaposleni sme biti primalac po ugovoru — tada mu
    /// šifra vrste prihoda nosi tip primaoca <c>01</c> — pa se u listi vidi šta je ko, da se
    /// tip ne bi izabrao pogrešno.
    /// </summary>
    public bool URadnomOdnosu { get; init; }

    public string Naziv => URadnomOdnosu
        ? $"{BrojRadnika}. {ImeIPrezime}  (zaposlen)"
        : $"{BrojRadnika}. {ImeIPrezime}";
}

/// <summary>Red u tabeli obračunatih naknada izabrane isplate.</summary>
public sealed class NaknadaRed
{
    public required int ObracunId { get; init; }
    public string Primalac { get; init; } = "";
    public string Vrsta { get; init; } = "";
    public string Svp { get; init; } = "";
    public decimal Bruto { get; init; }
    public decimal Osnovica { get; init; }
    public decimal Porez { get; init; }
    public decimal Doprinosi { get; init; }
    public decimal Neto { get; init; }
    public bool Zakljucan { get; init; }
    public bool Storniran { get; init; }
    public string StatusStr => Storniran ? "STORNO" : Zakljucan ? "zaključan" : "—";
}
