using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PlataData.Models;

namespace PlataApp.Views.Stampe;

public class RekapitulacijaDocument
{
    private readonly List<ObracunPlate> _obracuni;
    private readonly int _godina;
    private readonly int _mesec;
    private readonly string _rjFilter;

    public RekapitulacijaDocument(List<ObracunPlate> obracuni, int godina, int mesec, string rjFilter)
    {
        _obracuni = obracuni;
        _godina = godina;
        _mesec = mesec;
        _rjFilter = rjFilter;
    }

    public void Build(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.0f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Calibri"));

        // Header
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("ZAVOD ZA POLJOPRIVREDU PIROT").Bold().FontSize(12).FontColor(Colors.Indigo.Darken4);
                col.Item().Text("PIB: 100224119 • Matični broj: 07198305").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text($"MESEČNA REKAPITULACIJA ZARADA").Bold().FontSize(11).FontColor(Colors.Indigo.Medium);
                col.Item().Text($"Obračunski period: {_mesec:D2}/{_godina} • Filter RJ: {_rjFilter}").FontSize(8).FontColor(Colors.Grey.Darken2);
            });
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text("Zbirni izveštaj").Bold().FontSize(9).FontColor(Colors.Indigo.Darken4);
            });
        });

        // Content
        page.Content().PaddingVertical(0.4f, Unit.Centimetre).Column(col =>
        {
            // Sum data from SQLite obracuni
            int totalRadnici = _obracuni.Count;
            int totalHours = _obracuni.Sum(o => o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati);

            decimal grandBruto = _obracuni.Sum(o => o.BrutoZarada + o.BrutoBolovanje);
            decimal sumBrutoBolovanje = _obracuni.Sum(o => o.BrutoBolovanje);
            decimal sumBrutoMinuliRad = _obracuni.Sum(o => o.BrutoMinuliRad);
            decimal sumBrutoStimulacija = _obracuni.Sum(o => o.BrutoStimulacija);

            decimal sumBrutoNaknade = _obracuni.Sum(o =>
            {
                decimal naknade = o.BrutoNaknade;
                if (naknade == (o.BrutoZarada + o.BrutoBolovanje))
                {
                    return 0m;
                }
                return naknade;
            });

            decimal sumBrutoRedovna = grandBruto - sumBrutoBolovanje - sumBrutoMinuliRad - sumBrutoStimulacija - sumBrutoNaknade;

            decimal sumPoreskaOsn = _obracuni.Sum(o => o.PoreskaOsnovica);
            decimal sumPorez = _obracuni.Sum(o => o.PorezNaDohodak);

            decimal sumPioRadnik = _obracuni.Sum(o => o.DoprinosPioRadnik);
            decimal sumZdrRadnik = _obracuni.Sum(o => o.DoprinosZdravstvoRadnik);
            decimal sumNezRadnik = _obracuni.Sum(o => o.DoprinosNezaposlenostRadnik);
            decimal totalDoprinosiRadnik = sumPioRadnik + sumZdrRadnik + sumNezRadnik;

            decimal totalNeto1 = grandBruto - sumPorez - totalDoprinosiRadnik;

            decimal sumKrediti = _obracuni.Sum(o => o.KreditObustava);
            decimal sumSamodoprinosi = _obracuni.Sum(o => o.Samodoprinosi);
            decimal sumOstaliOdbici = _obracuni.Sum(o => o.OstaliOdbici);
            decimal totalObustave = sumKrediti + sumSamodoprinosi + sumOstaliOdbici;

            decimal grandNetoZaIsplatu = _obracuni.Sum(o => o.NetoIsplata);

            decimal sumPioPoslodavac = _obracuni.Sum(o => o.DoprinosPioPoslodavac);
            decimal sumZdrPoslodavac = _obracuni.Sum(o => o.DoprinosZdravstvoPoslodavac);
            decimal sumNezPoslodavac = _obracuni.Sum(o => o.DoprinosNezaposlenostPoslodavac);
            decimal totalDoprinosiPoslodavac = sumPioPoslodavac + sumZdrPoslodavac + sumNezPoslodavac;

            decimal grandBruto2 = grandBruto + totalDoprinosiPoslodavac;

            // Statistička traka (Key Metrics)
            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("BROJ OBRAČUNATIH RADNIKA").FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{totalRadnici} zaposlenih").FontSize(11).Bold();
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("UKUPNO ODRAĐENIH ČASOVA").FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{totalHours:N0} sati").FontSize(11).Bold();
                });
            });

            col.Item().PaddingTop(12).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(120);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Indigo.Darken4).Padding(4).Text("Opis finansijske stavke / rashoda").Bold().FontColor(Colors.White).FontSize(9);
                    header.Cell().Background(Colors.Indigo.Darken4).Padding(4).AlignRight().Text("Iznos u RSD").Bold().FontColor(Colors.White).FontSize(9);
                });

                void AddRow(string desc, decimal value, bool bold = false, string? bgColor = null)
                {
                    var cellDesc = table.Cell();
                    IContainer containerDesc = bgColor != null ? cellDesc.Background(bgColor) : cellDesc;
                    containerDesc.Padding(3.5f).Text(desc).Style(bold ? TextStyle.Default.Bold().FontSize(9) : TextStyle.Default.FontSize(9));

                    var cellVal = table.Cell();
                    IContainer containerVal = bgColor != null ? cellVal.Background(bgColor) : cellVal;
                    containerVal.Padding(3.5f).AlignRight().Text($"{value:N2}").Style(bold ? TextStyle.Default.Bold().FontSize(9) : TextStyle.Default.FontSize(9));
                }

                // 1. Bruto
                table.Cell().ColumnSpan(2).PaddingTop(6).Text("1. BRUTO RASHODI ZARADA ZAPOSLENIH").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                AddRow("Bruto zarada zaposlenih (redovan rad i godišnji)", sumBrutoRedovna);
                if (sumBrutoMinuliRad > 0) AddRow("Bruto naknada za minuli rad", sumBrutoMinuliRad);
                if (sumBrutoBolovanje > 0) AddRow("Bruto naknada za bolovanje do 30 dana", sumBrutoBolovanje);
                if (sumBrutoNaknade > 0) AddRow("Uvećanja zarade (prekovremeni, noćni, praznici)", sumBrutoNaknade);
                if (sumBrutoStimulacija > 0) AddRow("Varijabila / Stimulacija", sumBrutoStimulacija);
                AddRow("UKUPNA BRUTO ZARADA (1)", grandBruto, bold: true, bgColor: Colors.Grey.Lighten3);

                // 2. Osnovice i porezi
                table.Cell().ColumnSpan(2).PaddingTop(8).Text("2. POREZ NA DOHODAK").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                AddRow("Poreska osnovica (Bruto minus lično oslobođenje)", sumPoreskaOsn);
                AddRow("Porez na dohodak građana (stopa 10%)", sumPorez, bold: true);

                // 3. Doprinosi zaposlenog
                table.Cell().ColumnSpan(2).PaddingTop(8).Text("3. OBAVEZNI SOC. DOPRINOSI NA TERET ZAPOSLENOG").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                AddRow("Doprinos za PIO (stopa 14.00%)", sumPioRadnik);
                AddRow("Doprinos za zdravstvo (stopa 5.15%)", sumZdrRadnik);
                AddRow("Doprinos za nezaposlenost (stopa 0.75%)", sumNezRadnik);
                AddRow("UKUPNI DOPRINOSI NA TERET ZAPOSLENOG (2)", totalDoprinosiRadnik, bold: true, bgColor: Colors.Grey.Lighten4);

                // 4. Neto 1
                table.Cell().ColumnSpan(2).PaddingTop(8).Text("4. NETO ZARADA ZAPOSLENOG").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                AddRow("Neto 1 zarada (Bruto - Porez - Doprinosi zaposlenog)", totalNeto1, bold: true);

                // 5. Odbici
                table.Cell().ColumnSpan(2).PaddingTop(8).Text("5. OBUSTAVE I ODBICI OD NETO ZARADE").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                if (sumKrediti > 0) AddRow("Obustave po osnovu kredita i admin. zabrana", sumKrediti);
                if (sumSamodoprinosi > 0) AddRow("Opštinski samodoprinosi zaposlenih", sumSamodoprinosi);
                if (sumOstaliOdbici > 0) AddRow("Ostali odbici / obustave", sumOstaliOdbici);
                AddRow("UKUPNE OBUSTAVE ZAPOSLENIH (3)", totalObustave, bold: true, bgColor: Colors.Grey.Lighten4);

                // 6. Neto za isplatu
                table.Cell().ColumnSpan(2).PaddingTop(8).Text("6. FINALNA ISPLATA ZAPOSLENIMA").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                AddRow("NETO ZARADA ZA ISPLATU (Konačni neto = Neto 1 - Obustave)", grandNetoZaIsplatu, bold: true, bgColor: Colors.Indigo.Lighten5);

                // 7. Doprinosi poslodavca
                table.Cell().ColumnSpan(2).PaddingTop(8).Text("7. OBAVEZNI SOC. DOPRINOSI NA TERET POSLODAVCA").Bold().FontSize(9.5f).FontColor(Colors.Indigo.Darken3);
                AddRow("Doprinos za PIO (stopa 10.00%)", sumPioPoslodavac);
                AddRow("Doprinos za zdravstvo (stopa 5.15%)", sumZdrPoslodavac);
                if (sumNezPoslodavac > 0) AddRow("Doprinos za nezaposlenost (stopa 0.00%)", sumNezPoslodavac);
                AddRow("UKUPNI DOPRINOSI NA TERET POSLODAVCA (4)", totalDoprinosiPoslodavac, bold: true, bgColor: Colors.Grey.Lighten4);

                // 8. Bruto 2
                table.Cell().ColumnSpan(2).PaddingTop(10).Text("8. UKUPAN FINANSIJSKI RASHOD PREDUZEĆA (BRUTO 2)").Bold().FontSize(10).FontColor(Colors.Indigo.Darken3);
                AddRow("UKUPNI RASHOD ZAVODA (Bruto 2 = Bruto 1 + Doprinosi Poslodavca)", grandBruto2, bold: true, bgColor: Colors.Grey.Lighten2);
            });

            // Potpisi
            col.Item().PaddingTop(35).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).Text("Šef računovodstva").AlignCenter().FontSize(8).FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(120);
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).Text("Direktor / Ovlašćeno lice").AlignCenter().FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        });

        // Footer
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Zavod za poljoprivredu Pirot • Zbirna rekapitulacija zarada • Stranica ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
