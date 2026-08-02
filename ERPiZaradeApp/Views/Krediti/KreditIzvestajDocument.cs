using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Krediti;

public class KreditIzvestajDocument
{
    private readonly List<Kredit> _krediti;
    private readonly int _godina;
    private readonly int _mesec;

    public KreditIzvestajDocument(List<Kredit> krediti, int godina, int mesec)
    {
        // Sortiraj kredite po šifri radnika, pa po opisu
        _krediti = krediti
            .OrderBy(k => k.Radnik.BrojRadnika)
            .ThenBy(k => k.Opis)
            .ToList();
        _godina = godina;
        _mesec = mesec;
    }

    public void Build(PageDescriptor page)
    {
        page.Size(PageSizes.A4.Portrait());
        page.Margin(1.0f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Calibri"));

        string nazivFirme = "NAZIV FIRME";
        try
        {
            using var db = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
            var firma = db.Firme.FirstOrDefault();
            if (firma != null)
            {
                nazivFirme = (firma.Naziv + " " + firma.Grad).Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(nazivFirme)) nazivFirme = "NAZIV FIRME";
            }
        }
        catch {}

        // Header
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(nazivFirme).Bold().FontSize(12).FontColor(Colors.Indigo.Darken4);
                col.Item().Text($"ZBIRNI MESEČNI IZVEŠTAJ KREDITA I OBUSTAVA").Bold().FontSize(11).FontColor(Colors.Indigo.Medium);
                col.Item().Text($"Obračunski period: {_mesec:D2}/{_godina}").FontSize(9).FontColor(Colors.Grey.Darken2);
            });
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
            });
        });

        // Content
        page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
        {
            col.Item().Table(table =>
            {
                // Columns: Rbr (25), Šifra (30), Radnik (120), Poverilac/Opis (110), Ukupno (70), Rata (60), Ostatak duga (70), Rate (45)
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);  // Rbr
                    columns.ConstantColumn(35);  // Šifra radnika
                    columns.RelativeColumn(1.8f); // Ime i prezime radnika
                    columns.RelativeColumn(1.5f); // Poverilac / Opis
                    columns.ConstantColumn(75);  // Ukupan dug
                    columns.ConstantColumn(65);  // Mesečna rata
                    columns.ConstantColumn(75);  // Ostatak duga
                    columns.ConstantColumn(50);  // Platene / Broj rata
                });

                // Header
                table.Header(header =>
                {
                    void AddHeaderCell(string text, bool alignRight = false)
                    {
                        var cell = header.Cell().Background(Colors.Indigo.Darken4).PaddingVertical(5).PaddingHorizontal(4);
                        var tb = cell.Text(text).Bold().FontColor(Colors.White).FontSize(8.5f);
                        if (alignRight) tb.AlignRight();
                    }

                    AddHeaderCell("Rbr");
                    AddHeaderCell("Šifra");
                    AddHeaderCell("Ime i prezime zaposlenog");
                    AddHeaderCell("Naziv obustave / Poverilac");
                    AddHeaderCell("Ukupno duga", alignRight: true);
                    AddHeaderCell("Rata (mes.)", alignRight: true);
                    AddHeaderCell("Ostatak duga", alignRight: true);
                    AddHeaderCell("Progres", alignRight: true);
                });

                int rbr = 1;
                decimal ukupnoUgovoreno = 0;
                decimal ukupnoRata = 0;
                decimal ukupnoOstatak = 0;

                foreach (var k in _krediti)
                {
                    // U ovom mesecu se obustavlja rata (ako je preostali dug veći od 0)
                    decimal trenutnaRata = Math.Min(k.MesecnaRata, k.OstatakDuga);
                    
                    ukupnoUgovoreno += k.UkupanIznos;
                    ukupnoRata += trenutnaRata;
                    ukupnoOstatak += k.OstatakDuga;

                    // Row
                    table.Cell().Padding(3).Text($"{rbr++}").FontSize(8.5f);
                    table.Cell().Padding(3).Text($"{k.Radnik.BrojRadnika}").FontSize(8.5f);
                    table.Cell().Padding(3).Text(k.Radnik.ImeIPrezime).Bold().FontSize(8.5f);
                    table.Cell().Padding(3).Text(k.Opis).FontSize(8.5f);
                    table.Cell().Padding(3).AlignRight().Text($"{k.UkupanIznos:N2}").FontSize(8.5f);
                    table.Cell().Padding(3).AlignRight().Text($"{trenutnaRata:N2}").FontSize(8.5f);
                    table.Cell().Padding(3).AlignRight().Text($"{k.OstatakDuga:N2}").Bold().FontSize(8.5f).FontColor(Colors.Red.Darken2);
                    table.Cell().Padding(3).AlignRight().Text($"{k.PlateneRate} / {k.BrojRata}").FontSize(8.5f);
                }

                // Sum row
                string bgColor = Colors.Indigo.Lighten5;
                table.Cell().Background(bgColor).Padding(4).Text("");
                table.Cell().Background(bgColor).Padding(4).Text("");
                table.Cell().Background(bgColor).Padding(4).Text("UKUPNA SUMA OBUSTAVA").Bold().FontSize(9).FontColor(Colors.Indigo.Darken3);
                table.Cell().Background(bgColor).Padding(4).Text("");
                
                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{ukupnoUgovoreno:N2}").Bold().FontSize(9);
                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{ukupnoRata:N2}").Bold().FontSize(9).FontColor(Colors.Indigo.Darken4);
                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{ukupnoOstatak:N2}").Bold().FontSize(9).FontColor(Colors.Red.Darken3);
                table.Cell().Background(bgColor).Padding(4).Text("");
            });
        });

        // Footer
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Knjigovodstvena evidencija obustava • Stranica ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
