using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ERPiZaradeApp.Services;

/// <summary>Šta je pročitano iz dokumenta koji ePorezi izda po prihvatanju PPP-PD prijave.</summary>
public sealed class PodaciZaUplatu
{
    public string Bop { get; set; } = "";
    public decimal Iznos { get; set; }
    public string RacunZaUplatu { get; set; } = "";
    public string ModelPozivaNaBroj { get; set; } = "";
    public string Svrha { get; set; } = "";

    /// <summary>Polja koja u dokumentu nisu prepoznata i moraju se uneti ručno.</summary>
    public List<string> NeprepoznataPolja { get; } = [];

    /// <summary>Polja popunjena podrazumevanom vrednošću, a ne pročitana iz dokumenta.</summary>
    public List<string> PopunjenaPodrazumevano { get; } = [];

    /// <summary>Bez BOP-a i iznosa nalog za objedinjenu naplatu ne može da se formira.</summary>
    public bool JeUpotrebljiv => !string.IsNullOrWhiteSpace(Bop) && Iznos > 0;
}

/// <summary>
/// Čita podatke za nalog za prenos iz XML-a koji ePorezi generiše pošto Poreska uprava
/// prihvati PPP-PD prijavu — BOP, ukupan iznos i uplatni račun.
///
/// Čitanje je namerno tolerantno: elementi se traže po značenju naziva, a ne po tačnoj
/// putanji, jer se šema dokumenta menjala kroz verzije portala. Sve što se ne prepozna
/// prijavljuje se kroz <see cref="PodaciZaUplatu.NeprepoznataPolja"/> i unosi se ručno —
/// nijedno polje se ne pogađa.
/// </summary>
public class EPoreziImportService
{
    /// <summary>Uplatni račun objedinjene naplate poreza i doprinosa po odbitku od 01.03.2014.</summary>
    public const string PodrazumevaniRacunObjedinjeneNaplate = "840-4848-37";

    /// <summary>Model poziva na broj odobrenja za objedinjenu naplatu.</summary>
    public const string PodrazumevaniModel = "97";

    public PodaciZaUplatu Ucitaj(string putanjaFajla)
        => Procitaj(XDocument.Load(putanjaFajla));

    public PodaciZaUplatu Procitaj(XDocument dokument)
    {
        var podaci = new PodaciZaUplatu();
        var elementi = dokument.Descendants().ToList();

        podaci.Bop = NadjiTekst(elementi, "bop", "brojodobrenja", "odobrenjezaplacanje", "pozivnabroj");
        if (string.IsNullOrWhiteSpace(podaci.Bop))
            podaci.NeprepoznataPolja.Add("BOP (broj odobrenja za plaćanje)");

        podaci.Iznos = NadjiIznos(elementi, "iznoszauplatu", "ukupanIznos", "ukupno", "zauplatu", "iznos");
        if (podaci.Iznos <= 0)
            podaci.NeprepoznataPolja.Add("Iznos za uplatu");

        podaci.RacunZaUplatu = NadjiTekst(elementi, "uplatniracun", "racunzauplatu", "racun");
        if (string.IsNullOrWhiteSpace(podaci.RacunZaUplatu))
        {
            podaci.RacunZaUplatu = PodrazumevaniRacunObjedinjeneNaplate;
            podaci.PopunjenaPodrazumevano.Add($"Uplatni račun ({PodrazumevaniRacunObjedinjeneNaplate})");
        }

        podaci.ModelPozivaNaBroj = NadjiTekst(elementi, "modelpozivanabroj", "model");
        if (string.IsNullOrWhiteSpace(podaci.ModelPozivaNaBroj))
        {
            podaci.ModelPozivaNaBroj = PodrazumevaniModel;
            podaci.PopunjenaPodrazumevano.Add($"Model poziva na broj ({PodrazumevaniModel})");
        }

        podaci.Svrha = NadjiTekst(elementi, "svrhauplate", "svrha", "opisplacanja");

        return podaci;
    }

    /// <summary>
    /// Traži prvi element čiji naziv sadrži jedan od ključeva, redom kojim su navedeni —
    /// raniji ključ je precizniji, pa ima prednost nad opštijim.
    /// </summary>
    private static string NadjiTekst(List<XElement> elementi, params string[] kljucevi)
    {
        foreach (var kljuc in kljucevi)
        {
            var pogodak = elementi.FirstOrDefault(e =>
                Uprosti(e.Name.LocalName).Contains(kljuc, StringComparison.OrdinalIgnoreCase) &&
                !e.HasElements &&
                !string.IsNullOrWhiteSpace(e.Value));

            if (pogodak != null) return pogodak.Value.Trim();
        }
        return "";
    }

    private static decimal NadjiIznos(List<XElement> elementi, params string[] kljucevi)
    {
        foreach (var kljuc in kljucevi)
        {
            var kandidati = elementi.Where(e =>
                Uprosti(e.Name.LocalName).Contains(kljuc, StringComparison.OrdinalIgnoreCase) &&
                !e.HasElements);

            foreach (var kandidat in kandidati)
            {
                if (ParsirajIznos(kandidat.Value, out decimal iznos) && iznos > 0)
                    return iznos;
            }
        }
        return 0m;
    }

    /// <summary>
    /// Prihvata i „1.234.567,89" (domaći zapis) i „1234567.89" (XML zapis). Razlika je
    /// bitna: pogrešno pročitana decimalna tačka menja iznos za tri reda veličine.
    /// </summary>
    internal static bool ParsirajIznos(string? tekst, out decimal iznos)
    {
        iznos = 0m;
        if (string.IsNullOrWhiteSpace(tekst)) return false;

        string ocisceno = tekst.Trim().Replace(" ", "").Replace(" ", "");

        if (decimal.TryParse(ocisceno, NumberStyles.Number, CultureInfo.InvariantCulture, out iznos)
            && !ocisceno.Contains(','))
        {
            return true;
        }

        var srpski = CultureInfo.GetCultureInfo("sr-Latn-RS");
        return decimal.TryParse(ocisceno, NumberStyles.Number, srpski, out iznos);
    }

    /// <summary>Uklanja razdvajače iz naziva elementa da se „Broj_Odobrenja" i „BrojOdobrenja" traže isto.</summary>
    private static string Uprosti(string naziv)
        => naziv.Replace("_", "").Replace("-", "").Replace(".", "");
}
