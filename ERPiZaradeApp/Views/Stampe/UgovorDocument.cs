using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERPiZaradeApp.Views.Stampe;

/// <summary>
/// Štampa teksta ugovora van radnog odnosa (Faza 2.3).
///
/// Tekst se čuva kao običan tekst, pa se i štampa kao takav: prazan red razdvaja pasuse,
/// a red koji je ceo velikim slovima ili počinje sa „Član" dobija podebljanje. To je jedino
/// „formatiranje" i namerno je toliko — bogat format (RTF, HTML) bi značio da se dokument
/// više ne može pouzdano uporediti sa onim što je korisnik video u editoru.
/// </summary>
public class UgovorDocument
{
    private readonly string _tekst;
    private readonly string _podnozje;

    public UgovorDocument(string tekst, string podnozje = "")
    {
        _tekst = tekst ?? "";
        _podnozje = podnozje ?? "";
    }

    public void Build(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(2.2f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily("Calibri").LineHeight(1.35f));

        page.Content().Column(col =>
        {
            foreach (string red in _tekst.Replace("\r\n", "\n").Split('\n'))
            {
                if (red.Trim().Length == 0)
                {
                    col.Item().Height(8);
                    continue;
                }

                col.Item().Text(text =>
                {
                    var span = text.Span(red);

                    if (JeNaslov(red)) span.Bold().FontSize(13).FontColor(Colors.Indigo.Darken4);
                    else if (JePodnaslov(red)) span.Bold();
                });
            }
        });

        page.Footer().Row(row =>
        {
            row.RelativeItem().Text(_podnozje).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(120).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    /// <summary>Naslov dokumenta — red bez malih slova, na početku teksta.</summary>
    private bool JeNaslov(string red)
    {
        string t = red.Trim();
        if (t.Length == 0 || t.Any(char.IsLower)) return false;

        // Samo prva takva linija je naslov; potpisni blok je takođe velikim slovima.
        int mesto = _tekst.IndexOf(red, StringComparison.Ordinal);
        return mesto >= 0 && mesto < 120;
    }

    private static bool JePodnaslov(string red)
        => red.TrimStart().StartsWith("Član", StringComparison.Ordinal);
}
