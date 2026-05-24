using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PlataData.Models;

namespace PlataApp.Views.Stampe;

public class PlatniSpisakDocument
{
    private readonly List<ObracunPlate> _obracuni;
    private readonly int _godina;
    private readonly int _mesec;
    private readonly string _rjFilter;
    private readonly bool _poJedinicama;

    public PlatniSpisakDocument(List<ObracunPlate> obracuni, int godina, int mesec, string rjFilter, bool poJedinicama)
    {
        _obracuni = obracuni;
        _godina = godina;
        _mesec = mesec;
        _rjFilter = rjFilter;
        _poJedinicama = poJedinicama;
    }

    public void Build(PageDescriptor page)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(0.6f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(7.5f).FontFamily("Calibri"));

        // Header
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("ZAVOD ZA POLJOPRIVREDU PIROT").Bold().FontSize(11).FontColor(Colors.Indigo.Darken4);
                col.Item().Text($"PLATNI SPISAK ZARADA ZA { _mesec:D2}/{_godina}").Bold().FontSize(10).FontColor(Colors.Indigo.Medium);
                col.Item().Text($"Filter radne jedinice: {_rjFilter} • Grupisanje po RJ: {(_poJedinicama ? "DA" : "NE")}").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
            });
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        // Content
        page.Content().PaddingVertical(0.3f, Unit.Centimetre).Column(col =>
        {
            col.Item().Table(table =>
            {
                // Columns: Rbr (15), Šifra (15), Ime i prezime (110), RJ (20), Časovi (red/bol/prek/god/uk) (50), Bruto (50), Osnovica (50), Porez (45), Doprinosi (50), Neto1 (50), Obustave (45), Za isplatu (55)
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);  // Rbr
                    columns.ConstantColumn(22);  // Šifra
                    columns.RelativeColumn(2.0f); // Ime i prezime
                    columns.ConstantColumn(16);  // RJ
                    columns.ConstantColumn(30);  // Čas.Red
                    columns.ConstantColumn(30);  // Čas.Ukup
                    columns.ConstantColumn(52);  // Bruto
                    columns.ConstantColumn(52);  // Osnovica
                    columns.ConstantColumn(46);  // Porez
                    columns.ConstantColumn(52);  // Doprinosi
                    columns.ConstantColumn(52);  // Neto 1
                    columns.ConstantColumn(46);  // Obustave
                    columns.ConstantColumn(56);  // Za isplatu
                });

                // Header
                table.Header(header =>
                {
                    void AddHeaderCell(string text, bool alignRight = false)
                    {
                        var cell = header.Cell().Background(Colors.Indigo.Darken4).PaddingVertical(3).PaddingHorizontal(2);
                        var tb = cell.Text(text).Bold().FontColor(Colors.White).FontSize(7.5f);
                        if (alignRight) tb.AlignRight();
                    }

                    AddHeaderCell("Rbr");
                    AddHeaderCell("Šifra");
                    AddHeaderCell("Ime i prezime zaposlenog");
                    AddHeaderCell("RJ");
                    AddHeaderCell("Sati R", alignRight: true);
                    AddHeaderCell("Sati U", alignRight: true);
                    AddHeaderCell("Bruto zarada", alignRight: true);
                    AddHeaderCell("Pores. Osn.", alignRight: true);
                    AddHeaderCell("Porez 10%", alignRight: true);
                    AddHeaderCell("Doprinosi", alignRight: true);
                    AddHeaderCell("Neto 1", alignRight: true);
                    AddHeaderCell("Obustave", alignRight: true);
                    AddHeaderCell("Za isplatu", alignRight: true);
                });

                int rbr = 1;

                if (_poJedinicama)
                {
                    // Group by radna jedinica
                    var groups = _obracuni.GroupBy(o => o.Radnik.BrojRadneJedinice).OrderBy(g => g.Key);
                    foreach (var grp in groups)
                    {
                        // RJ header row
                        table.Cell().ColumnSpan(13).Background(Colors.Grey.Lighten3).Padding(2).Row(r =>
                        {
                            r.RelativeItem().Text($"RADNA JEDINICA {grp.Key}").Bold().FontSize(8).FontColor(Colors.Indigo.Darken3);
                        });

                        decimal rjRedovni = 0, rjUkupni = 0, rjBruto = 0, rjOsn = 0, rjPor = 0, rjDop = 0, rjNeto1 = 0, rjObu = 0, rjIsp = 0;

                        foreach (var o in grp)
                        {
                            decimal totalBruto = o.BrutoZarada + o.BrutoBolovanje;
                            decimal ukupniDop = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;
                            decimal neto1 = totalBruto - o.PorezNaDohodak - ukupniDop;
                            decimal obustave = o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici;
                            int ukupSati = o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati;

                            // Add to RJ sums
                            rjRedovni += o.RedovniSati;
                            rjUkupni += ukupSati;
                            rjBruto += totalBruto;
                            rjOsn += o.PoreskaOsnovica;
                            rjPor += o.PorezNaDohodak;
                            rjDop += ukupniDop;
                            rjNeto1 += neto1;
                            rjObu += obustave;
                            rjIsp += o.NetoIsplata;

                            WriteRow(table, rbr++, o, totalBruto, ukupSati, ukupniDop, neto1, obustave);
                        }

                        // RJ Sum row
                        WriteSumRow(table, $"Suma RJ {grp.Key}", rjRedovni, rjUkupni, rjBruto, rjOsn, rjPor, rjDop, rjNeto1, rjObu, rjIsp, Colors.Grey.Lighten4);
                    }
                }
                else
                {
                    // No grouping, print all
                    foreach (var o in _obracuni)
                    {
                        decimal totalBruto = o.BrutoZarada + o.BrutoBolovanje;
                        decimal ukupniDop = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;
                        decimal neto1 = totalBruto - o.PorezNaDohodak - ukupniDop;
                        decimal obustave = o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici;
                        int ukupSati = o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati;

                        WriteRow(table, rbr++, o, totalBruto, ukupSati, ukupniDop, neto1, obustave);
                    }
                }

                // Grand total
                decimal gRedovni = _obracuni.Sum(o => o.RedovniSati);
                decimal gUkupni = _obracuni.Sum(o => o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati);
                decimal gBruto = _obracuni.Sum(o => o.BrutoZarada + o.BrutoBolovanje);
                decimal gOsn = _obracuni.Sum(o => o.PoreskaOsnovica);
                decimal gPor = _obracuni.Sum(o => o.PorezNaDohodak);
                decimal gDop = _obracuni.Sum(o => o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik);
                decimal gNeto1 = _obracuni.Sum(o => (o.BrutoZarada + o.BrutoBolovanje) - o.PorezNaDohodak - (o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik));
                decimal gObu = _obracuni.Sum(o => o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici);
                decimal gIsp = _obracuni.Sum(o => o.NetoIsplata);

                WriteSumRow(table, "UKUPNA SUMA ZAVODA", gRedovni, gUkupni, gBruto, gOsn, gPor, gDop, gNeto1, gObu, gIsp, Colors.Indigo.Lighten5);
            });
        });

        // Footer
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Zavod za poljoprivredu Pirot • Knjigovodstvena evidencija zarada • Stranica ").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(7.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    private void WriteRow(TableDescriptor table, int rbr, ObracunPlate o, decimal totalBruto, int ukupSati, decimal ukupniDop, decimal neto1, decimal obustave)
    {
        table.Cell().Padding(1.5f).Text($"{rbr}").FontSize(7);
        table.Cell().Padding(1.5f).Text($"{o.Radnik.BrojRadnika}").FontSize(7);
        table.Cell().Padding(1.5f).Text(o.Radnik.ImeIPrezime).Bold().FontSize(7);
        table.Cell().Padding(1.5f).Text($"{o.Radnik.BrojRadneJedinice}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{o.RedovniSati}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{ukupSati}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{totalBruto:N2}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{o.PoreskaOsnovica:N2}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{o.PorezNaDohodak:N2}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{ukupniDop:N2}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{neto1:N2}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{obustave:N2}").FontSize(7);
        table.Cell().Padding(1.5f).AlignRight().Text($"{o.NetoIsplata:N2}").Bold().FontSize(7.5f);
    }

    private void WriteSumRow(TableDescriptor table, string title, decimal sRedovni, decimal sUkupni, decimal sBruto, decimal sOsn, decimal sPor, decimal sDop, decimal sNeto1, decimal sObu, decimal sIsp, string bgColor)
    {
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text(title).Bold().FontSize(7.5f).FontColor(Colors.Indigo.Darken3);
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sRedovni:N0}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sUkupni:N0}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sBruto:N2}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sOsn:N2}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sPor:N2}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sDop:N2}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sNeto1:N2}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sObu:N2}").Bold().FontSize(7.5f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sIsp:N2}").Bold().FontSize(8).FontColor(Colors.Indigo.Darken4);
    }
}
