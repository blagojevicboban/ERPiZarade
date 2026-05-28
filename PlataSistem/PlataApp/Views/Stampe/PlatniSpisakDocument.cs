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
        page.Margin(0.4f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(7.8f).FontFamily("Calibri"));

        // Header
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("ZAVOD ZA POLJOPRIVREDU PIROT").Bold().FontSize(10).FontColor(Colors.Indigo.Darken4);
                col.Item().Text($"PLATNI SPISAK ZARADA ZA { _mesec:D2}/{_godina}").Bold().FontSize(9).FontColor(Colors.Indigo.Medium);
                col.Item().Text($"Filter radne jedinice: {_rjFilter} • Grupisanje po RJ: {(_poJedinicama ? "DA" : "NE")}").FontSize(7.0f).FontColor(Colors.Grey.Darken2);
            });
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            });
        });

        // Content
        page.Content().PaddingVertical(0.2f, Unit.Centimetre).Column(col =>
        {
            col.Item().Table(table =>
            {
                // Columns layout
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(18);  // Rbr
                    columns.ConstantColumn(20);  // Šifra
                    columns.RelativeColumn(1.0f); // Ime i prezime
                    columns.ConstantColumn(22);  // Spr (Stručna sprema)
                    columns.ConstantColumn(28);  // Koef (Koeficijent / bodovi)
                    columns.ConstantColumn(15);  // RJ
                    columns.ConstantColumn(24);  // Sat.R (Redovni)
                    columns.ConstantColumn(24);  // Sat.O (Godišnji odmor)
                    columns.ConstantColumn(24);  // Sat.B (Bolovanje)
                    columns.ConstantColumn(24);  // Sat.P (Državni praznik)
                    columns.ConstantColumn(24);  // Sat.K (Prekovremeni)
                    columns.ConstantColumn(26);  // Sat.U (Ukupni časovi)
                    columns.ConstantColumn(56);  // Bruto
                    columns.ConstantColumn(56);  // Osnovica za dopr.
                    columns.ConstantColumn(44);  // Porez 10%
                    columns.ConstantColumn(56);  // Doprinosi (PIO+ZDR+NEZ)
                    columns.ConstantColumn(56);  // Neto 1 (Bez por. i dop.)
                    columns.ConstantColumn(44);  // Obustave
                    columns.ConstantColumn(60);  // Za isplatu
                });

                // Header
                table.Header(header =>
                {
                    void AddHeaderCell(string text, bool alignRight = false)
                    {
                        var cell = header.Cell().Background(Colors.Indigo.Darken4).PaddingVertical(3).PaddingHorizontal(1);
                        var tb = cell.Text(text).Bold().FontColor(Colors.White).FontSize(7.8f);
                        if (alignRight) tb.AlignRight();
                    }

                    AddHeaderCell("Rbr");
                    AddHeaderCell("Šif");
                    AddHeaderCell("Ime i prezime zaposlenog");
                    AddHeaderCell("Spr");
                    AddHeaderCell("Koef");
                    AddHeaderCell("RJ");
                    AddHeaderCell("Sat.R", alignRight: true);
                    AddHeaderCell("Sat.O", alignRight: true);
                    AddHeaderCell("Sat.B", alignRight: true);
                    AddHeaderCell("Sat.P", alignRight: true);
                    AddHeaderCell("Sat.K", alignRight: true);
                    AddHeaderCell("Sat.U", alignRight: true);
                    AddHeaderCell("Bruto 1", alignRight: true);
                    AddHeaderCell("Osn.", alignRight: true);
                    AddHeaderCell("Por.", alignRight: true);
                    AddHeaderCell("Dopr.", alignRight: true);
                    AddHeaderCell("Neto1", alignRight: true);
                    AddHeaderCell("Obust.", alignRight: true);
                    AddHeaderCell("Za ispl.", alignRight: true);
                });

                int rbr = 1;

                if (_poJedinicama)
                {
                    // Group by radna jedinica
                    var groups = _obracuni.GroupBy(o => o.Radnik.BrojRadneJedinice).OrderBy(g => g.Key);
                    foreach (var grp in groups)
                    {
                        // RJ header row
                        table.Cell().ColumnSpan(19).Background(Colors.Grey.Lighten3).Padding(2).Row(r =>
                        {
                            r.RelativeItem().Text($"RADNA JEDINICA {grp.Key}").Bold().FontSize(8.5f).FontColor(Colors.Indigo.Darken3);
                        });

                        decimal rjRedovni = 0, rjOdmor = 0, rjBolovanje = 0, rjPraznik = 0, rjPrekovremeni = 0, rjUkupni = 0;
                        decimal rjBruto = 0, rjOsn = 0, rjPor = 0, rjDop = 0, rjNeto1 = 0, rjObu = 0, rjIsp = 0;

                        foreach (var o in grp)
                        {
                            decimal totalBruto = o.Bruto1;
                            decimal ukupniDop = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;
                            decimal neto1 = totalBruto - o.PorezNaDohodak - ukupniDop;
                            decimal obustave = o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici;
                            int ukupSati = o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati + o.DrzavniPraznikSati;

                            // Add to RJ sums
                            rjRedovni += o.RedovniSati;
                            rjOdmor += o.GodisnjioOdmorSati;
                            rjBolovanje += o.BolovanjeSati;
                            rjPraznik += o.DrzavniPraznikSati;
                            rjPrekovremeni += o.PrekovremeneSati;
                            rjUkupni += ukupSati;

                            rjBruto += totalBruto;
                            rjOsn += o.PoreskaOsnovica;
                            rjPor += o.PorezNaDohodak;
                            rjDop += ukupniDop;
                            rjNeto1 += neto1;
                            rjObu += obustave;
                            rjIsp += o.NetoIsplata;

                            WriteRow(table, rbr++, o, totalBruto, ukupniDop, neto1, obustave,
                                o.RedovniSati, o.GodisnjioOdmorSati, o.BolovanjeSati, o.DrzavniPraznikSati, o.PrekovremeneSati, ukupSati);
                        }

                        // RJ Sum row
                        WriteSumRow(table, $"Suma RJ {grp.Key}", rjRedovni, rjOdmor, rjBolovanje, rjPraznik, rjPrekovremeni, rjUkupni,
                            rjBruto, rjOsn, rjPor, rjDop, rjNeto1, rjObu, rjIsp, Colors.Grey.Lighten4);
                    }
                }
                else
                {
                    // No grouping, print all
                    foreach (var o in _obracuni)
                    {
                        decimal totalBruto = o.Bruto1;
                        decimal ukupniDop = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;
                        decimal neto1 = totalBruto - o.PorezNaDohodak - ukupniDop;
                        decimal obustave = o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici;
                        int ukupSati = o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati + o.DrzavniPraznikSati;

                        WriteRow(table, rbr++, o, totalBruto, ukupniDop, neto1, obustave,
                            o.RedovniSati, o.GodisnjioOdmorSati, o.BolovanjeSati, o.DrzavniPraznikSati, o.PrekovremeneSati, ukupSati);
                    }
                }

                // Grand total
                decimal gRedovni = _obracuni.Sum(o => o.RedovniSati);
                decimal gOdmor = _obracuni.Sum(o => o.GodisnjioOdmorSati);
                decimal gBolovanje = _obracuni.Sum(o => o.BolovanjeSati);
                decimal gPraznik = _obracuni.Sum(o => o.DrzavniPraznikSati);
                decimal gPrekovremeni = _obracuni.Sum(o => o.PrekovremeneSati);
                decimal gUkupni = _obracuni.Sum(o => o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati + o.DrzavniPraznikSati);

                decimal gBruto = _obracuni.Sum(o => o.Bruto1);
                decimal gOsn = _obracuni.Sum(o => o.PoreskaOsnovica);
                decimal gPor = _obracuni.Sum(o => o.PorezNaDohodak);
                decimal gDop = _obracuni.Sum(o => o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik);
                decimal gNeto1 = _obracuni.Sum(o => o.Bruto1 - o.PorezNaDohodak - (o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik));
                decimal gObu = _obracuni.Sum(o => o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici);
                decimal gIsp = _obracuni.Sum(o => o.NetoIsplata);

                WriteSumRow(table, "UKUPNA SUMA ZAVODA", gRedovni, gOdmor, gBolovanje, gPraznik, gPrekovremeni, gUkupni,
                    gBruto, gOsn, gPor, gDop, gNeto1, gObu, gIsp, Colors.Indigo.Lighten5);
            });
        });

        // Footer
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Zavod za poljoprivredu Pirot • Knjigovodstvena evidencija zarada • Stranica ").FontSize(7.0f).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(7.0f).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(7.0f).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(7.0f).FontColor(Colors.Grey.Darken1);
        });
    }

    private void WriteRow(TableDescriptor table, int rbr, ObracunPlate o, decimal totalBruto, decimal ukupniDop, decimal neto1, decimal obustave, int satR, int satO, int satB, int satP, int satK, int satU)
    {
        string spr = !string.IsNullOrWhiteSpace(o.Kategorija) ? o.Kategorija : (o.Radnik != null ? o.Radnik.Kategorija : "");
        decimal koef = o.Koeficijent > 0 ? o.Koeficijent : (o.Radnik != null ? o.Radnik.Koeficijent : 0m);
        int brRadnika = o.Radnik?.BrojRadnika ?? 0;
        string imeIprezime = o.Radnik?.ImeIPrezime ?? "[Nepoznat radnik]";
        int brRj = o.Radnik?.BrojRadneJedinice ?? 0;

        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{rbr}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{brRadnika}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text(imeIprezime).Bold().FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text(spr).FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{koef:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{brRj}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satR}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satO}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satB}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satP}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satK}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satU}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{totalBruto:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{o.PoreskaOsnovica:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{o.PorezNaDohodak:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{ukupniDop:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{neto1:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{obustave:N2}").FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{o.NetoIsplata:N2}").Bold().FontSize(8.0f);
    }

    private void WriteSumRow(TableDescriptor table, string title, decimal sR, decimal sO, decimal sB, decimal sP, decimal sK, decimal sU, decimal sBruto, decimal sOsn, decimal sPor, decimal sDop, decimal sNeto1, decimal sObu, decimal sIsp, string bgColor)
    {
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text(title).Bold().FontSize(8.0f).FontColor(Colors.Indigo.Darken3);
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sR:N0}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sO:N0}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sB:N0}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sP:N0}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sK:N0}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sU:N0}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sBruto:N2}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sOsn:N2}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sPor:N2}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sDop:N2}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sNeto1:N2}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sObu:N2}").Bold().FontSize(8.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sIsp:N2}").Bold().FontSize(8.5f).FontColor(Colors.Indigo.Darken4);
    }
}
