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

        string nazivFirme = "NAZIV FIRME";
        try
        {
            using var db = PlataData.PlataDbContext.Create(PlataApp.AppConfig.DbPath);
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
                col.Item().Text(nazivFirme).Bold().FontSize(10).FontColor(Colors.Indigo.Darken4);
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
                    columns.ConstantColumn(15);  // Rbr
                    columns.ConstantColumn(18);  // Šifra
                    columns.RelativeColumn(1.0f); // Ime i prezime
                    columns.ConstantColumn(16);  // Spr (Stručna sprema)
                    columns.ConstantColumn(22);  // Koef (Koeficijent / bodovi)
                    columns.ConstantColumn(12);  // RJ
                    columns.ConstantColumn(20);  // Rad
                    columns.ConstantColumn(20);  // Odm
                    columns.ConstantColumn(20);  // Bol
                    columns.ConstantColumn(22);  // Drz.N
                    columns.ConstantColumn(22);  // Drz.R
                    columns.ConstantColumn(18);  // Noc
                    columns.ConstantColumn(18);  // Ned
                    columns.ConstantColumn(18);  // Prek
                    columns.ConstantColumn(22);  // Ukup
                    columns.ConstantColumn(48);  // Bruto 1
                    columns.ConstantColumn(44);  // Osnovica za dopr.
                    columns.ConstantColumn(38);  // Porez 10%
                    columns.ConstantColumn(48);  // Doprinosi (PIO+ZDR+NEZ)
                    columns.ConstantColumn(48);  // Neto 1 (Bez por. i dop.)
                    columns.ConstantColumn(38);  // Obustave
                    columns.ConstantColumn(52);  // Za isplatu
                    columns.ConstantColumn(42);  // Dopr.P (na teret poslodavca)
                    columns.ConstantColumn(48);  // Bruto 2
                });

                // Header
                table.Header(header =>
                {
                    void AddHeaderCell(string text, bool alignRight = false)
                    {
                        var cell = header.Cell().Background(Colors.Indigo.Darken4).PaddingVertical(3).PaddingHorizontal(1);
                        var tb = cell.Text(text).Bold().FontColor(Colors.White).FontSize(7.5f);
                        if (alignRight) tb.AlignRight();
                    }

                    AddHeaderCell("Rbr");
                    AddHeaderCell("Šif");
                    AddHeaderCell("Ime i prezime zaposlenog");
                    AddHeaderCell("Spr");
                    AddHeaderCell("Koef");
                    AddHeaderCell("RJ");
                    AddHeaderCell("Rad", alignRight: true);
                    AddHeaderCell("Odm", alignRight: true);
                    AddHeaderCell("Bol", alignRight: true);
                    AddHeaderCell("Drz.N", alignRight: true);
                    AddHeaderCell("Drz.R", alignRight: true);
                    AddHeaderCell("Noc", alignRight: true);
                    AddHeaderCell("Ned", alignRight: true);
                    AddHeaderCell("Prek", alignRight: true);
                    AddHeaderCell("Ukup", alignRight: true);
                    AddHeaderCell("Bruto 1", alignRight: true);
                    AddHeaderCell("Osn.", alignRight: true);
                    AddHeaderCell("Por.", alignRight: true);
                    AddHeaderCell("Dopr.", alignRight: true);
                    AddHeaderCell("Neto1", alignRight: true);
                    AddHeaderCell("Obust.", alignRight: true);
                    AddHeaderCell("Za ispl.", alignRight: true);
                    AddHeaderCell("Dopr.P", alignRight: true);
                    AddHeaderCell("Bruto 2", alignRight: true);
                });

                int rbr = 1;

                if (_poJedinicama)
                {
                    // Group by radna jedinica
                    var groups = _obracuni.GroupBy(o => o.Radnik.BrojRadneJedinice).OrderBy(g => g.Key);
                    foreach (var grp in groups)
                    {
                        // RJ header row
                        table.Cell().ColumnSpan(24).Background(Colors.Grey.Lighten3).Padding(2).Row(r =>
                        {
                            r.RelativeItem().Text($"RADNA JEDINICA {grp.Key}").Bold().FontSize(8.5f).FontColor(Colors.Indigo.Darken3);
                        });

                        decimal rjRedovni = 0, rjOdmor = 0, rjBolovanje = 0, rjPraznikNerd = 0, rjPraznikRad = 0, rjNocni = 0, rjNedelja = 0, rjPrekovremeni = 0, rjUkupni = 0;
                        decimal rjBruto = 0, rjOsn = 0, rjPor = 0, rjDop = 0, rjNeto1 = 0, rjObu = 0, rjIsp = 0, rjDopP = 0, rjBruto2 = 0;

                        foreach (var o in grp)
                        {
                            decimal totalBruto = o.Bruto1;
                            decimal ukupniDop = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;
                            decimal neto1 = totalBruto - o.PorezNaDohodak - ukupniDop;
                            decimal obustave = o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici;
                            int ukupSati = o.UkupnoSati;
                            decimal doprPoslodavca = o.UkupniDoprinosiPoslodavca;
                            decimal bruto2 = o.Bruto2;

                            // Add to RJ sums
                            rjRedovni += o.RedovniSati;
                            rjOdmor += o.GodisnjioOdmorSati;
                            rjBolovanje += o.BolovanjeSati;
                            rjPraznikNerd += o.DrzavniPraznikSati;
                            rjPraznikRad += o.RadPraznikomSati;
                            rjNocni += o.NocniSati;
                            rjNedelja += (int)o.NedeljaSati;
                            rjPrekovremeni += o.PrekovremeneSati;
                            rjUkupni += ukupSati;

                            rjBruto += totalBruto;
                            rjOsn += o.PoreskaOsnovica;
                            rjPor += o.PorezNaDohodak;
                            rjDop += ukupniDop;
                            rjNeto1 += neto1;
                            rjObu += obustave;
                            rjIsp += o.NetoIsplata;
                            rjDopP += doprPoslodavca;
                            rjBruto2 += bruto2;

                            WriteRow(table, rbr++, o, totalBruto, ukupniDop, neto1, obustave,
                                o.RedovniSati, o.GodisnjioOdmorSati, o.BolovanjeSati, o.DrzavniPraznikSati, o.RadPraznikomSati, o.NocniSati, (int)o.NedeljaSati, o.PrekovremeneSati, ukupSati, doprPoslodavca, bruto2);
                        }

                        // RJ Sum row
                        WriteSumRow(table, $"Suma RJ {grp.Key}", rjRedovni, rjOdmor, rjBolovanje, rjPraznikNerd, rjPraznikRad, rjNocni, rjNedelja, rjPrekovremeni, rjUkupni,
                            rjBruto, rjOsn, rjPor, rjDop, rjNeto1, rjObu, rjIsp, rjDopP, rjBruto2, Colors.Grey.Lighten4);
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
                        int ukupSati = o.UkupnoSati;
                        decimal doprPoslodavca = o.UkupniDoprinosiPoslodavca;
                        decimal bruto2 = o.Bruto2;

                        WriteRow(table, rbr++, o, totalBruto, ukupniDop, neto1, obustave,
                            o.RedovniSati, o.GodisnjioOdmorSati, o.BolovanjeSati, o.DrzavniPraznikSati, o.RadPraznikomSati, o.NocniSati, (int)o.NedeljaSati, o.PrekovremeneSati, ukupSati, doprPoslodavca, bruto2);
                    }
                }

                // Grand total
                decimal gRedovni = _obracuni.Sum(o => o.RedovniSati);
                decimal gOdmor = _obracuni.Sum(o => o.GodisnjioOdmorSati);
                decimal gBolovanje = _obracuni.Sum(o => o.BolovanjeSati);
                decimal gPraznikNerd = _obracuni.Sum(o => o.DrzavniPraznikSati);
                decimal gPraznikRad = _obracuni.Sum(o => o.RadPraznikomSati);
                decimal gNocni = _obracuni.Sum(o => o.NocniSati);
                decimal gNedelja = _obracuni.Sum(o => (int)o.NedeljaSati);
                decimal gPrekovremeni = _obracuni.Sum(o => o.PrekovremeneSati);
                decimal gUkupni = _obracuni.Sum(o => o.UkupnoSati);

                decimal gBruto = _obracuni.Sum(o => o.Bruto1);
                decimal gOsn = _obracuni.Sum(o => o.PoreskaOsnovica);
                decimal gPor = _obracuni.Sum(o => o.PorezNaDohodak);
                decimal gDop = _obracuni.Sum(o => o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik);
                decimal gNeto1 = _obracuni.Sum(o => o.Bruto1 - o.PorezNaDohodak - (o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik));
                decimal gObu = _obracuni.Sum(o => o.KreditObustava + o.Samodoprinosi + o.OstaliOdbici);
                decimal gIsp = _obracuni.Sum(o => o.NetoIsplata);
                decimal gDopP = _obracuni.Sum(o => o.UkupniDoprinosiPoslodavca);
                decimal gBruto2 = _obracuni.Sum(o => o.Bruto2);

                WriteSumRow(table, "UKUPNA SUMA FIRME", gRedovni, gOdmor, gBolovanje, gPraznikNerd, gPraznikRad, gNocni, gNedelja, gPrekovremeni, gUkupni,
                    gBruto, gOsn, gPor, gDop, gNeto1, gObu, gIsp, gDopP, gBruto2, Colors.Indigo.Lighten5);
            });
        });

        // Footer
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Knjigovodstvena evidencija zarada • Stranica ").FontSize(7.0f).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(7.0f).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(7.0f).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(7.0f).FontColor(Colors.Grey.Darken1);
        });
    }

    private void WriteRow(TableDescriptor table, int rbr, ObracunPlate o, decimal totalBruto, decimal ukupniDop, decimal neto1, decimal obustave, int satR, int satO, int satB, int satDrzN, int satDrzR, int satNoc, int satNed, int satK, int satU, decimal dopP, decimal bruto2)
    {
        string spr = !string.IsNullOrWhiteSpace(o.Kategorija) ? o.Kategorija : (o.Radnik != null ? o.Radnik.Kategorija : "");
        decimal koef = o.Koeficijent > 0 ? o.Koeficijent : (o.Radnik != null ? o.Radnik.Koeficijent : 0m);
        int brRadnika = o.Radnik?.BrojRadnika ?? 0;
        string imeIprezime = o.Radnik?.ImeIPrezime ?? "[Nepoznat radnik]";
        int brRj = o.Radnik?.BrojRadneJedinice ?? 0;

        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{rbr}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{brRadnika}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text(imeIprezime).Bold().FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text(spr).FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{koef:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).Text($"{brRj}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satR}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satO}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satB}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satDrzN}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satDrzR}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satNoc}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satNed}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satK}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{satU}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{totalBruto:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{o.PoreskaOsnovica:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{o.PorezNaDohodak:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{ukupniDop:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{neto1:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{obustave:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{o.NetoIsplata:N2}").Bold().FontSize(7.5f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{dopP:N2}").FontSize(7.0f);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f).AlignRight().Text($"{bruto2:N2}").Bold().FontSize(7.5f);
    }

    private void WriteSumRow(TableDescriptor table, string title, decimal sR, decimal sO, decimal sB, decimal sDrzN, decimal sDrzR, decimal sNoc, decimal sNed, decimal sK, decimal sU, decimal sBruto, decimal sOsn, decimal sPor, decimal sDop, decimal sNeto1, decimal sObu, decimal sIsp, decimal sDopP, decimal sBruto2, string bgColor)
    {
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text(title).Bold().FontSize(7.5f).FontColor(Colors.Indigo.Darken3);
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).Text("");
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sR:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sO:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sB:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sDrzN:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sDrzR:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sNoc:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sNed:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sK:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sU:N0}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sBruto:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sOsn:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sPor:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sDop:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sNeto1:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sObu:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sIsp:N2}").Bold().FontSize(7.5f).FontColor(Colors.Indigo.Darken4);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sDopP:N2}").Bold().FontSize(7.0f);
        table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"{sBruto2:N2}").Bold().FontSize(7.5f).FontColor(Colors.Indigo.Darken4);
    }
}
