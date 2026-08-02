using System;
using System.Linq;
using System.Windows;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Prikaz nalaza kontrolnih provera i odluka da li se zaključavanje nastavlja.
/// Odvojeno od <see cref="PreFlightService"/> da sam servis ostane bez veze sa UI-jem
/// i time proverljiv testovima.
/// </summary>
public static class PreFlightPrompt
{
    private const int MaxPrikazanihNalaza = 15;

    /// <summary>
    /// Upozorenja se samo prijavljuju; greške zaustavljaju radnju, a pregaziti ih može
    /// isključivo administrator — operater nema ovlašćenje da podnese prijavu za koju se
    /// zna da je neispravna.
    /// </summary>
    /// <returns><c>true</c> ako zaključavanje sme da se nastavi.</returns>
    public static bool DozvoliZakljucavanje(RezultatProvere provera)
    {
        if (provera.JeCist) return true;

        string izvestaj = SastaviIzvestaj(provera);

        if (provera.SmeSeZakljucati)
        {
            return MessageBox.Show(
                $"Kontrolne provere su našle {provera.BrojUpozorenja} upozorenja:\n\n{izvestaj}\n" +
                "Upozorenja ne sprečavaju zaključavanje. Želite li da nastavite?",
                "Kontrolne provere", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        if (!AppSession.IsAdmin)
        {
            MessageBox.Show(
                $"Kontrolne provere su našle {provera.BrojGresaka} grešaka:\n\n{izvestaj}\n" +
                "Period se ne može zaključati dok se greške ne otklone. Zaključavanje uprkos greškama može odobriti samo administrator.",
                "Zaključavanje zaustavljeno", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        return MessageBox.Show(
            $"Kontrolne provere su našle {provera.BrojGresaka} grešaka i {provera.BrojUpozorenja} upozorenja:\n\n{izvestaj}\n" +
            "Zaključavanje sa ovim greškama znači da će PPP-PD prijava ili nalozi za prenos verovatno biti odbijeni.\n\n" +
            "Da li kao administrator ipak odobravate zaključavanje?",
            "Potvrda administratora", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public static string SastaviIzvestaj(RezultatProvere provera)
    {
        var redovi = provera.Nalazi
            .OrderByDescending(n => n.Tezina)
            .ThenBy(n => n.BrojRadnika)
            .Take(MaxPrikazanihNalaza)
            .Select(n => n.BrojRadnika.HasValue
                ? $"• [{n.TezinaTekst}] {n.BrojRadnika} {n.Radnik} — {n.Provera}: {n.Opis}"
                : $"• [{n.TezinaTekst}] {n.Provera}: {n.Opis}");

        string tekst = string.Join(Environment.NewLine, redovi);

        if (provera.Nalazi.Count > MaxPrikazanihNalaza)
            tekst += $"{Environment.NewLine}… i još {provera.Nalazi.Count - MaxPrikazanihNalaza} nalaza.";

        return tekst + Environment.NewLine;
    }

    /// <summary>Kratak opis nalaza za revizioni trag.</summary>
    public static string OpisZaAudit(RezultatProvere provera)
        => provera.JeCist
            ? "kontrolne provere bez nalaza"
            : $"{provera.BrojGresaka} grešaka i {provera.BrojUpozorenja} upozorenja u kontrolnim proverama";
}
