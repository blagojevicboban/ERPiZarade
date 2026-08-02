using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Stampe;

public class BankeIzvestajDocument
{
    private readonly List<ObracunPlate> _obracuni;
    private readonly int _godina;
    private readonly int _mesec;
    private readonly string _rjFilter;
    private readonly List<Banka> _bankeInfo;
    private readonly string _nazivFirme;

    public BankeIzvestajDocument(List<ObracunPlate> obracuni, int godina, int mesec, string rjFilter, List<Banka> bankeInfo, string nazivFirme)
    {
        _obracuni = obracuni;
        _godina = godina;
        _mesec = mesec;
        _rjFilter = rjFilter;
        _bankeInfo = bankeInfo;
        if (string.IsNullOrWhiteSpace(nazivFirme))
        {
            _nazivFirme = "NAZIV FIRME";
            try
            {
                using var db = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
                var firma = db.Firme.FirstOrDefault();
                if (firma != null)
                {
                    _nazivFirme = (firma.Naziv + " " + firma.Grad).Trim().ToUpper();
                    if (string.IsNullOrWhiteSpace(_nazivFirme)) _nazivFirme = "NAZIV FIRME";
                }
            }
            catch {}
        }
        else
        {
            _nazivFirme = nazivFirme;
        }
    }

    public void Build(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.2f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9f).FontFamily("Calibri"));

        // Naziv meseca na srpskom jeziku
        string imeMes = new[] {
            "januar", "februar", "mart", "april", "maj", "jun",
            "jul", "avgust", "septembar", "oktobar", "novembar", "decembar"
        }[_mesec - 1];

        // Grupišemo obračune po kodu banke iz radnika (o.Radnik.NazivBanke)
        // Ako je kod prazan ili null, tretiramo ga kao "1" (Gotovina)
        var grupisaniObracuni = _obracuni
            .GroupBy(o => string.IsNullOrWhiteSpace(o.Radnik?.NazivBanke) ? "1" : o.Radnik.NazivBanke.Trim())
            .OrderBy(g => g.Key)
            .ToList();

        page.Content().Column(col =>
        {
            bool isNotFirst = false;

            foreach (var grupa in grupisaniObracuni)
            {
                string bankCode = grupa.Key;
                
                // Pronalaženje naziva banke iz učitanih SQLite šifrarnika
                string bankName = "Gotovina";
                var bankInfo = _bankeInfo.FirstOrDefault(b => b.Sifra == bankCode);
                
                if (bankInfo != null && !string.IsNullOrWhiteSpace(bankInfo.Naziv))
                {
                    bankName = bankInfo.Naziv;
                }
                else
                {
                    // Fallback
                    if (bankCode == "1") bankName = "Gotovina";
                    else if (bankCode == "2") bankName = "BANKA INTESA";
                    else bankName = $"Banka šifra {bankCode}";
                }

                if (isNotFirst)
                {
                    col.Item().PageBreak();
                }
                isNotFirst = true;

                // ── ZAGLAVLJE GRUPE BANKE ──────────────────────────────────────────────
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text(_nazivFirme).Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken4);
                    });
                });

                col.Item().PaddingTop(10).AlignCenter().Column(c =>
                {
                    c.Item().Text($"I Z V E Š T A J  za mesec {imeMes} {_godina}.").Bold().FontSize(12);
                    c.Item().Text($"BANKA: {bankName.ToUpper()}").Bold().FontSize(11).FontColor(Colors.Indigo.Medium);
                });

                col.Item().PaddingTop(6).LineHorizontal(0.8f).LineColor(Colors.Grey.Medium);

                // ── TABELA ZA GRUPU BANKE ──────────────────────────────────────────────
                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);   // RBR
                        columns.ConstantColumn(70);   // Šifra
                        columns.ConstantColumn(160);  // Broj računa
                        columns.RelativeColumn(1.0f); // Ime i prezime
                        columns.ConstantColumn(110);  // Iznos za isplatu
                    });

                    // Tabela zaglavlje
                    table.Header(header =>
                    {
                        void AddHeaderCell(string text, bool alignRight = false)
                        {
                            var cell = header.Cell().Background(Colors.Indigo.Darken4).PaddingVertical(5).PaddingHorizontal(6);
                            var tb = cell.Text(text).Bold().FontColor(Colors.White).FontSize(9f);
                            if (alignRight) tb.AlignRight();
                        }

                        AddHeaderCell("Rbr");
                        AddHeaderCell("Šifra");
                        AddHeaderCell("Broj računa");
                        AddHeaderCell("Ime i prezime zaposlenog");
                        AddHeaderCell("Iznos", alignRight: true);
                    });

                    int rbr = 1;
                    decimal ukupnoZaBanku = 0m;

                    // Poredaj radnike po broju radnika
                    var sortiraniObracuni = grupa.OrderBy(o => o.Radnik?.BrojRadnika ?? 0).ToList();

                    foreach (var o in sortiraniObracuni)
                    {
                        int sifraRadnika = o.Radnik?.BrojRadnika ?? 0;
                        string ime = o.Radnik?.ImeIPrezime ?? "[Nepoznato ime]";
                        string racun = o.Radnik?.BankovniRacun ?? "";
                        decimal iznos = o.NetoIsplata;

                        ukupnoZaBanku += iznos;

                        // Rbr
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6)
                            .Text($"{rbr++}").FontSize(9f);

                        // Šifra
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6)
                            .Text($"{sifraRadnika}").FontSize(9f);

                        // Broj računa
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6)
                            .Text(racun).FontSize(9f);

                        // Ime i prezime
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6)
                            .Text(ime).Bold().FontSize(9f);

                        // Iznos za isplatu
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6)
                            .AlignRight()
                            .Text($"{iznos:N2}").FontSize(9.5f);
                    }

                    // Suma red na dnu tabele banke
                    table.Cell().Background(Colors.Indigo.Lighten5).PaddingVertical(6).PaddingHorizontal(6)
                        .Text("").FontSize(9.5f);
                    table.Cell().Background(Colors.Indigo.Lighten5).PaddingVertical(6).PaddingHorizontal(6)
                        .Text("").FontSize(9.5f);
                    table.Cell().Background(Colors.Indigo.Lighten5).PaddingVertical(6).PaddingHorizontal(6)
                        .Text("").FontSize(9.5f);
                    table.Cell().Background(Colors.Indigo.Lighten5).PaddingVertical(6).PaddingHorizontal(6)
                        .Text("S U M A").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken4);
                    table.Cell().Background(Colors.Indigo.Lighten5).PaddingVertical(6).PaddingHorizontal(6)
                        .AlignRight()
                        .Text($"{ukupnoZaBanku:N2}").Bold().FontSize(10f).FontColor(Colors.Indigo.Darken4);
                });
            }
        });

        // ── ZAJEDNIČKI FOOTER ───────────────────────────────────────────────────
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Izveštaj za prenos na bankovne račune  •  Stranica ")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
