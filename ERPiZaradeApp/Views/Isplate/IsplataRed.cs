using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Isplate;

/// <summary>
/// Red u tabeli isplata: sama isplata plus ono što se o njoj vidi tek kad se prebroje
/// obračuni i potraži njena prijava.
/// </summary>
public class IsplataRed
{
    public required Isplata Isplata { get; init; }

    public int RedniBroj => Isplata.RedniBroj;
    public string VrstaNaziv => ERPiZaradeData.Models.Isplata.NazivVrste(Isplata.Vrsta);

    public string Opis
    {
        get => Isplata.Opis;
        set => Isplata.Opis = value ?? "";
    }

    public DateTime DatumIsplate
    {
        get => Isplata.DatumIsplate;
        set => Isplata.DatumIsplate = value;
    }

    /// <summary>Obračuni koje isplata obuhvata, bez storniranih.</summary>
    public int BrojObracuna { get; init; }

    public decimal Neto { get; init; }

    /// <summary>„K" ili „N" — oznaka za konačnu isplatu na PPP-PD prijavi.</summary>
    public string OznakaKonacne => Isplata.OznakaZaKonacnuIsplatu;

    /// <summary>Skidaju li se na ovoj isplati rate kredita i samodoprinos.</summary>
    public string ObustaveStr => Isplata.NosiObustave ? "da" : "—";

    /// <summary>BOP prijave te isplate; crtica dok prijave nema ili nije prihvaćena.</summary>
    public string Bop { get; init; } = "—";

    public string StatusPrijaveStr { get; init; } = "nema prijave";
}

/// <summary>Stavka padajuće liste vrsta isplate — vrednost uz naziv na srpskom.</summary>
public sealed class VrstaIsplateStavka
{
    public required VrstaIsplate Vrsta { get; init; }
    public string Naziv => ERPiZaradeData.Models.Isplata.NazivVrste(Vrsta);
}
