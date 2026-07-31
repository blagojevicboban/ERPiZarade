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
    private readonly List<Samodoprinosi> _odbici;
    private readonly List<DoprinosiPoslodavca> _doprPoslodavca;
    private readonly int _godina;
    private readonly int _mesec;
    private readonly string _rjFilter;

    // Stope (fiksne kao u DOPRINOS.DBF za tekuci mesec)
    private const decimal StopaPorez    = 10.000m;
    private const decimal StopaPioR     = 14.000m;
    private const decimal StopaZdrR     =  5.150m;
    private const decimal StopaNezR     =  0.750m;
    private const decimal StopaPioP     = 10.000m;
    private const decimal StopaZdrP     =  5.150m;

    public RekapitulacijaDocument(List<ObracunPlate> obracuni, int godina, int mesec, string rjFilter,
                                   List<Samodoprinosi>? odbici = null,
                                   List<DoprinosiPoslodavca>? doprPoslodavca = null)
    {
        _obracuni = obracuni;
        _odbici   = odbici ?? new List<Samodoprinosi>();
        _godina   = godina;
        _mesec    = mesec;
        _rjFilter = rjFilter;
        _doprPoslodavca = doprPoslodavca ?? new List<DoprinosiPoslodavca>();
    }

    public void Build(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.2f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Courier New"));

        // ── Dinamičke stope doprinosa učitane iz obračuna ──────────────────────────────
        decimal stopaPioR = 14.000m;
        decimal stopaZdrR = 5.150m;
        decimal stopaNezR = 0.750m;
        
        decimal stopaPioP = 10.000m;
        decimal stopaZdrP = 5.150m;
        decimal stopaNezP = 0.000m;

        var prviObracun = _obracuni.FirstOrDefault();
        if (prviObracun != null)
        {
            if (decimal.TryParse(prviObracun.StopaPioRadnikStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valPioR)) stopaPioR = valPioR;
            if (decimal.TryParse(prviObracun.StopaZdravstvoRadnikStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valZdrR)) stopaZdrR = valZdrR;
            if (decimal.TryParse(prviObracun.StopaNezaposlenostRadnikStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valNezR)) stopaNezR = valNezR;

            if (decimal.TryParse(prviObracun.StopaPioPoslodavacStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valPioP)) stopaPioP = valPioP;
            if (decimal.TryParse(prviObracun.StopaZdravstvoPoslodavacStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valZdrP)) stopaZdrP = valZdrP;
            if (decimal.TryParse(prviObracun.StopaNezaposlenostPoslodavacStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valNezP)) stopaNezP = valNezP;
        }

        // ── Izračunavanja (odgovaraju SAMODOP.PRG: procedure rekapitulacija()) ───────────

        // Bruto zarada (neto_zar u OBRACUN.DBF)
        decimal sumBrutoZarada   = _obracuni.Sum(o => o.NetoZar);
        decimal sumBrutoBol      = _obracuni.Sum(o => o.NetoBol + o.NetoB100);
        decimal sumBrutoPlac     = _obracuni.Sum(o => o.NetoPlac);   // plac. odsustvo sankcije
        decimal sumBrutoPlZ      = _obracuni.Sum(o => o.NetoPlZ);    // plac. odsustvo po zakonu
        decimal sumBrutoNerd     = _obracuni.Sum(o => o.NetoNerd);   // drzavni praznik
        decimal sumBrutoGOd      = _obracuni.Sum(o => o.NetoGOd);    // godisnji odmor
        decimal sumBrutoMin      = _obracuni.Sum(o => o.BrutoMinuliRad);
        decimal sumBrutoDrza     = _obracuni.Sum(o => o.NetoDrza);   // rad na drz. praznik
        decimal sumBrutoNocni    = _obracuni.Sum(o => o.NetoNocni);
        decimal sumBrutoPrek     = _obracuni.Sum(o => o.NetoPrek);
        decimal sumBrutoNede     = _obracuni.Sum(o => o.NetoNede);   // nedeljom
        decimal sumBrutoTO       = _obracuni.Sum(o => o.NetoTo);     // topli obrok
        decimal sumBrutoReg      = _obracuni.Sum(o => o.NetoReg);    // regres
        decimal sumBrutoTer      = _obracuni.Sum(o => o.NetoTer);    // terenski
        decimal sumBrutoStim     = _obracuni.Sum(o => o.BrutoStimulacija); // stimulacija %
        decimal sumVarijabila    = _obracuni.Sum(o => o.Varijabila);        // bruto dodatak
        decimal sumKorDod        = _obracuni.Sum(o => o.KorDod);
        decimal sumKorDod1       = _obracuni.Sum(o => o.KorDod1);

        // Zarada (sum_neto = neto u OBRACUN)
        decimal sumZarada        = _obracuni.Sum(o => o.Neto);
        decimal sumDodPorez      = 0m;  // pos_por — nema zasebno u modelu, tretiramo kao 0
        decimal sumUkupNeto      = sumZarada - sumDodPorez;

        // Porez i osnovice
        decimal sumPorez         = _obracuni.Sum(o => o.PorezNaDohodak);
        // Umanjenje = licni odbitak = DBF polje 'umanjenje' (SAMODOP.PRG: sum_umanj)
        decimal sumUmanjenje     = _obracuni.Sum(o => o.LicniOdbitak);
        // Bruto osnovica za doprinose = Zarada (Neto), vidi BB.TXT red 39
        decimal sumBrOs          = sumZarada;
        decimal sumBrPIOOs       = sumZarada; // ista osnova za PIO (BB.TXT red 41)

        // Odbici: samodoprinosi i krediti po tipu
        var odbiciGrupisani = _odbici
            .GroupBy(o => o.Opis)
            .Select(g => (Naziv: g.Key, Iznos: g.Sum(x => x.Iznos)))
            .Where(g => g.Iznos > 0)
            .ToList();

        decimal sumSamDop  = _obracuni.Sum(o => o.Samodoprinosi);   // ukupni samodoprinosi
        decimal sumKred    = _obracuni.Sum(o => o.KreditObustava);  // ukupni krediti
        decimal sumUkOdbici = sumSamDop + sumKred;

        // Neto za isplatu
        decimal sumNetoZaIsp = sumUkupNeto - sumUkOdbici;

        // Doprinosi radnika
        decimal sumPioR  = _obracuni.Sum(o => o.DoprinosPioRadnik);
        decimal sumZdrR  = _obracuni.Sum(o => o.DoprinosZdravstvoRadnik);
        decimal sumNezR  = _obracuni.Sum(o => o.DoprinosNezaposlenostRadnik);
        decimal sumDopR  = sumPioR + sumZdrR + sumNezR;

        decimal sumZaradaBezPorDop = sumZarada - sumPorez - sumDopR;
        decimal sumZaIsplatu       = sumZaradaBezPorDop - sumUkOdbici;

        // Doprinosi poslodavca
        decimal sumPioP  = 0m;
        decimal sumZdrP  = 0m;
        decimal sumNezP  = 0m;

        if (_doprPoslodavca.Any())
        {
            // Use detailed employer contributions from DoprinosiPoslodavca table
            sumPioP = _doprPoslodavca.Sum(d => d.Zar1);
            sumZdrP = _doprPoslodavca.Sum(d => d.Zar2);
            sumNezP = _doprPoslodavca.Sum(d => d.Zar3);
        }
        else
        {
            sumPioP  = _obracuni.Sum(o => o.DoprinosPioPoslodavac);
            sumZdrP  = _obracuni.Sum(o => o.DoprinosZdravstvoPoslodavac);
            sumNezP  = _obracuni.Sum(o => o.DoprinosNezaposlenostPoslodavac);
        }

        // Masa za isplatu
        decimal masaCeoObr  = sumZarada + sumPioP + sumZdrP + sumNezP;
        decimal masaIsplac  = 0m;  // akontacije = 0 (BB.TXT)
        decimal masaOstalo  = masaCeoObr - masaIsplac;

        // Firma info (iz prvog obracuna ili hardcoded)
        string imeKor  = "PSSS PIROT DOO PIROT";
        string imeMes  = new[] {"januar","februar","mart","april","maj","jun",
                                 "jul","avgust","septembar","oktobar","novembar","decembar"}[_mesec - 1];

        // ── HEADER ──────────────────────────────────────────────────────────────────────
        page.Header().Column(hdr =>
        {
            hdr.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Datum stampe : {DateTime.Now:dd.MM.yyyy}").FontSize(8).FontFamily("Courier New");
                });
                row.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().Text(imeKor).Bold().FontSize(9).FontFamily("Courier New");
                });
            });
            hdr.Item().AlignCenter().PaddingTop(3)
                .Text($"R E K A P I T U L A C I J A  za  {imeMes} {_godina}")
                .Bold().FontSize(9).FontFamily("Courier New");
            hdr.Item().PaddingTop(2)
                .Text(new string('_', 109))
                .FontSize(7.5f).FontFamily("Courier New");
            hdr.Item().PaddingTop(1).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(72).AlignRight().Text("OBRACUN").FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(72).AlignRight().Text("AKONTACIJE").FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(72).AlignRight().Text("RAZLIKA").FontSize(8.5f).FontFamily("Courier New");
            });
        });

        // ── CONTENT ─────────────────────────────────────────────────────────────────────
        page.Content().PaddingVertical(0.2f, Unit.Centimetre).Column(col =>
        {
            void Row(string opis, decimal obracun, decimal akont = 0m, bool bold = false,
                     string? bgColor = null)
            {
                decimal razlika = obracun - akont;
                col.Item().Row(row =>
                {
                    // Opis (max 52 znaka)
                    row.RelativeItem().Background(bgColor ?? Colors.White)
                        .Text(opis)
                        .Style(bold
                            ? TextStyle.Default.Bold().FontSize(8.5f).FontFamily("Courier New")
                            : TextStyle.Default.FontSize(8.5f).FontFamily("Courier New"));


                    // OBRACUN
                    row.ConstantItem(72).Background(bgColor ?? Colors.White)
                        .AlignRight()
                        .Text($"{obracun:N2}")
                        .Style(bold
                            ? TextStyle.Default.Bold().FontSize(8.5f).FontFamily("Courier New")
                            : TextStyle.Default.FontSize(8.5f).FontFamily("Courier New"));

                    // AKONTACIJE
                    row.ConstantItem(72).Background(bgColor ?? Colors.White)
                        .AlignRight()
                        .Text($"{akont:N2}")
                        .FontSize(8.5f).FontFamily("Courier New");

                    // RAZLIKA
                    row.ConstantItem(72).Background(bgColor ?? Colors.White)
                        .AlignRight()
                        .Text($"{razlika:N2}")
                        .Style(bold
                            ? TextStyle.Default.Bold().FontSize(8.5f).FontFamily("Courier New")
                            : TextStyle.Default.FontSize(8.5f).FontFamily("Courier New"));
                });
            }

            // ── ZARADE ──────────────────────────────────────────────────────────────────
            Row("Bruto zarada.........................................", sumBrutoZarada);
            Row("Bruto naknada - bolovanje do 30 dana.................", sumBrutoBol);
            Row("Bruto naknada - placeno odsustvo - sankcije..........", sumBrutoPlac);
            Row("Bruto naknada - placeno odsustvo - po zakonu.........", sumBrutoPlZ);
            Row("Bruto naknada - drzavni praznik......................", sumBrutoNerd);
            Row("Bruto naknada - godisnji odmor.......................", sumBrutoGOd);
            Row("Bruto dodatak - minuli rad...........................", sumBrutoMin);
            Row("Bruto dodatak - rad na drzavni praznik...............", sumBrutoDrza);
            Row("Bruto dodatak - nocni rad............................", sumBrutoNocni);
            Row("Bruto dodatak - prekovremeni rad.....................", sumBrutoPrek);
            Row("Bruto dodatak - rad nedeljom.........................", sumBrutoNede);
            if (sumBrutoTO   != 0) Row("Bruto dodatak - topli obrok..........................", sumBrutoTO);
            if (sumBrutoReg  != 0) Row("Bruto dodatak - regres za god. odmor.................", sumBrutoReg);
            if (sumBrutoTer  != 0) Row("Bruto dodatak - terenski dodatak.....................", sumBrutoTer);
            if (sumBrutoStim  != 0) Row("Bruto naknada - stimulacija ..........................", sumBrutoStim);
            if (sumVarijabila != 0) Row("Bruto dodatak (varijabila)...........................", sumVarijabila);
            if (sumKorDod    != 0) Row("Bruto korektivni dodatak ............................", sumKorDod);
            if (sumKorDod1   != 0) Row("Bruto korektivni dodatak 1 ..........................", sumKorDod1);

            Row("Bruto 1 (Zarada).....................................",
                sumZarada, bold: true, bgColor: Colors.Grey.Lighten4);
            Row("Dodatni porez........................................", sumDodPorez);
            Row("Ukupan neto..........................................",
                sumUkupNeto, bold: true, bgColor: Colors.Grey.Lighten4);

            // ── ODBICI (samodoprinosi i krediti po imenu) ────────────────────────────────
            if (odbiciGrupisani.Count > 0)
            {
                foreach (var (naziv, iznos) in odbiciGrupisani)
                    Row(TruncPad(naziv, 45), iznos);
            }
            else if (sumUkOdbici != 0)
            {
                if (sumSamDop != 0) Row("Samodoprinosi........................................", sumSamDop);
                if (sumKred   != 0) Row("Krediti..............................................", sumKred);
            }

            Row("Neto  za isplatu.....................................",
                sumNetoZaIsp, bold: true, bgColor: Colors.Indigo.Lighten5);

            col.Item().PaddingTop(4);

            // ── REFUNDACIJE / INVALIDI / BOL > 30 ───────────────────────────────────────
            Row("Bruto naknada - invalidi II kategorije...............", 0m);
            Row("Umanjenje poreza na invalidninu .....................", 0m);
            Row("Bruto naknada ukupno - invalidi II kategorije........", 0m);
            col.Item().PaddingTop(2);
            Row("Bruto naknada - porodiljsko bolovanje................", 0m);
            col.Item().PaddingTop(2);
            Row("Bruto naknada - bolovanje preko 30 dana..............", 0m);
            Row("Umanjenje poreza na bolovanje preko 30 dana..........", 0m);
            Row("Bruto naknada - bolovanje preko 30 dana ukupno.......", 0m);

            Row("Umanjenje poreske osnovice...........................", sumUmanjenje);
            col.Item().PaddingTop(2);

            // Porez sa stopom (stopa u opisu, kao u BB.TXT)
            Row($"Porez..........................{StopaPorez,9:F3} % .....", sumPorez);
            col.Item().PaddingTop(2);

            Row("Bruto osnovica za obracun doprinosa ........... .....", sumBrOs);
            col.Item().PaddingTop(2);
            Row("Bruto osnovica za obracun doprinosa PIO........ .....", sumBrPIOOs);
            col.Item().PaddingTop(2);

            // ── DOPRINOSI RADNIKA ────────────────────────────────────────────────────────
            Row($"Dop.-penzijsko - zarada ...........{stopaPioR,9:F3} % .....", sumPioR);
            Row("Dop.-penzijsko  UKUPNO ..............................",
                sumPioR, bold: true, bgColor: Colors.Grey.Lighten5);

            Row($"Dop.-zdravstveno - zarada .........{stopaZdrR,9:F3} % .....", sumZdrR);
            Row("Dop.-zdravstveno  UKUPNO ............................",
                sumZdrR, bold: true, bgColor: Colors.Grey.Lighten5);

            Row($"Dop.-nezaposlenost - zarada .......{stopaNezR,9:F3} % .....", sumNezR);
            Row("Dop.-nezaposlenost  UKUPNO ..........................",
                sumNezR, bold: true, bgColor: Colors.Grey.Lighten5);

            // UKUPNO SOCIJALNI DOPRINOSI – poseban layout
            col.Item().Background(Colors.Grey.Lighten3).Row(row =>
            {
                row.RelativeItem()
                    .Text("  UKUPNO SOCIJALNI DOPRINOSI")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");

                row.ConstantItem(72).AlignRight()
                    .Text($"{sumDopR:N2}")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(72).AlignRight()
                    .Text($"{0m:N2}")
                    .FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(72).AlignRight()
                    .Text($"{sumDopR:N2}")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");
            });
            col.Item().PaddingTop(2);

            Row("Zarada bez poreza i doprinosa .......................",
                sumZaradaBezPorDop, bold: true);
            col.Item().PaddingTop(2);
            if (sumZaIsplatu != 0)
                Row("Za isplatu ..........................................", sumZaIsplatu, bold: true);

            // ── SEPARATOR ───────────────────────────────────────────────────────────────
            col.Item().PaddingTop(6)
                .Text(new string('_', 109)).FontSize(7.5f).FontFamily("Courier New");
            col.Item().PaddingTop(2).AlignCenter()
                .Text("DOPRINOSI NA TERET POSLODAVCA")
                .Bold().FontSize(9).FontFamily("Courier New");
            col.Item()
                .Text(new string('_', 109)).FontSize(7.5f).FontFamily("Courier New");
            col.Item().PaddingTop(2);

            // ── DOPRINOSI POSLODAVCA ─────────────────────────────────────────────────────
            Row($"Dop.-penzijsko - zarada ...........{stopaPioP,9:F3} % .....", sumPioP);
            Row("Dop.-penzijsko  UKUPNO ..............................",
                sumPioP, bold: true, bgColor: Colors.Grey.Lighten5);

            Row($"Dop.-zdravstveno - zarada .........{stopaZdrP,9:F3} % .....", sumZdrP);
            Row("Dop.-zdravstveno  UKUPNO ............................",
                sumZdrP, bold: true, bgColor: Colors.Grey.Lighten5);

            Row($"Dop.-nezaposlenost - zarada .......{stopaNezP,9:F3} % .....", sumNezP);
            Row("Dop.-nezaposlenost  UKUPNO ..........................",
                sumNezP, bold: true, bgColor: Colors.Grey.Lighten5);

            // ── UKUPNI DOPRINOSI PO VRSTI (RADNIK + POSLODAVAC) ─────────────────────────
            decimal sumPioUkupno = sumPioR + sumPioP;
            decimal sumZdrUkupno = sumZdrR + sumZdrP;
            decimal sumNezUkupno = sumNezR + sumNezP;

            col.Item().PaddingTop(4);
            void UkupnoRow(string opis, decimal iznos)
            {
                col.Item().Background(Colors.Grey.Lighten3).Row(row =>
                {
                    row.RelativeItem().Text("  " + opis).Bold().FontSize(8.5f).FontFamily("Courier New");
                    row.ConstantItem(110).AlignRight().Text($"{iznos:N2}").Bold().FontSize(8.5f).FontFamily("Courier New");
                });
            }
            UkupnoRow("PIO UKUPNO (radnik + poslodavac)", sumPioUkupno);
            UkupnoRow("ZDRAVSTVO UKUPNO (radnik + poslodavac)", sumZdrUkupno);
            UkupnoRow("NEZAPOSLENOST UKUPNO (radnik + poslodavac)", sumNezUkupno);

            // ── BRUTO 2 ──────────────────────────────────────────────────────────────────
            col.Item().PaddingTop(6)
                .Text(new string('_', 109)).FontSize(7.5f).FontFamily("Courier New");
            col.Item().PaddingTop(2).AlignCenter()
                .Text("BRUTO 2 (UKUPNA MASA ZA ISPLATU)")
                .Bold().FontSize(9).FontFamily("Courier New");
            col.Item()
                .Text(new string('_', 109)).FontSize(7.5f).FontFamily("Courier New");
            col.Item().PaddingTop(2);

            col.Item().Row(row =>
            {
                row.RelativeItem()
                    .Text("UKUPAN BRUTO 2 OBRACUN (Bruto 1 + dopr. posl.)")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(110).AlignRight()
                    .Text($"{masaCeoObr:N2}")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");
            });
            col.Item().Row(row =>
            {
                row.RelativeItem()
                    .Text("ISPLACENI DEO")
                    .FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(110).AlignRight()
                    .Text($"{masaIsplac:N2}")
                    .FontSize(8.5f).FontFamily("Courier New");
            });
            col.Item().Row(row =>
            {
                row.RelativeItem()
                    .Text("OSTALO ZA ISPLATU")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");
                row.ConstantItem(110).AlignRight()
                    .Text($"{masaOstalo:N2}")
                    .Bold().FontSize(8.5f).FontFamily("Courier New");
            });

            col.Item().PaddingTop(20);

            // ── POTPISI ──────────────────────────────────────────────────────────────────
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).Text("Šef računovodstva")
                        .AlignCenter().FontSize(8).FontFamily("Courier New").FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(80);
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).Text("Direktor / Ovlašćeno lice")
                        .AlignCenter().FontSize(8).FontFamily("Courier New").FontColor(Colors.Grey.Darken2);
                });
            });
        });

        // ── FOOTER ──────────────────────────────────────────────────────────────────────
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Rekapitulacija zarada  •  Stranica ")
                .FontSize(8).FontFamily("Courier New").FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string TruncPad(string s, int len)
        => s.Length > len ? s[..len] : s.PadRight(len, '.');
}
