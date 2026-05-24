using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PlataData.Models;

namespace PlataApp.Views.Listici;

public partial class ListiciPage : Page
{
    public ListiciPage()
    {
        InitializeComponent();
    }

    // ── AKCIJA: Pojedinačni izvoz u PDF iz tabele ──
    private void BtnPojedinacniPdf_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ObracunSelektivni item)
        {
            var o = item.Obracun;
            var sfd = new SaveFileDialog
            {
                Filter = "PDF dokument (*.pdf)|*.pdf",
                FileName = $"Platni_Listic_{o.Radnik.ImeIPrezime.Replace(" ", "_")}_{o.Mesec:D2}_{o.Godina}.pdf",
                Title = "Sačuvaj platni listić"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    GenerateSinglePdf(o, sfd.FileName);
                    var res = MessageBox.Show("Platni listić je uspešno generisan. Želite li da ga otvorite?",
                        "Uspeh", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Greška pri generisanju PDF-a: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // ── AKCIJA: Generiši zbirni PDF za sve selektovane ──
    private void BtnZbirniPdf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ListiciViewModel vm) return;

        var selektovani = vm.Obracuni.Where(o => o.IsSelected).Select(o => o.Obracun).ToList();
        if (selektovani.Count == 0)
        {
            MessageBox.Show("Molimo vas da selektujete barem jednog zaposlenog za zbirni PDF.",
                "Nema selekcije", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = $"Zbirni_Platni_Listici_{vm.SelectedMesec:D2}_{vm.SelectedGodina}.pdf",
            Title = "Sačuvaj zbirni platni listić"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                vm.StatusText = "Generisanje zbirnog PDF-a...";
                GenerateConsolidatedPdf(selektovani, sfd.FileName);
                vm.StatusText = $"Zbirni PDF uspešno generisan: {selektovani.Count} listića.";

                var res = MessageBox.Show($"Zbirni PDF je uspešno generisan sa {selektovani.Count} platna listića. Želite li da ga otvorite?",
                    "Uspeh", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri generisanju zbirnog PDF-a: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                vm.StatusText = "Greška pri generisanju zbirnog PDF-a.";
            }
        }
    }

    // ── AKCIJA: Batch pojedinačni izvoz u izabrani folder ──
    private void BtnBatchPdf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ListiciViewModel vm) return;

        var selektovani = vm.Obracuni.Where(o => o.IsSelected).Select(o => o.Obracun).ToList();
        if (selektovani.Count == 0)
        {
            MessageBox.Show("Molimo vas da selektujete barem jednog zaposlenog za batch izvoz.",
                "Nema selekcije", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Koristimo moderniji OpenFolderDialog dostupan u .NET 8.0
        var dialog = new OpenFolderDialog
        {
            Title = "Izaberite folder za izvoz platnih listića",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            string folderPath = dialog.FolderName;
            try
            {
                int uspesno = 0;
                vm.StatusText = $"Pokretanje batch izvoza u: {folderPath}...";

                foreach (var o in selektovani)
                {
                    string safeName = o.Radnik.ImeIPrezime.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
                    string fileName = $"Platni_Listic_{safeName}_{o.Mesec:D2}_{o.Godina}.pdf";
                    string fullPath = Path.Combine(folderPath, fileName);

                    GenerateSinglePdf(o, fullPath);
                    uspesno++;
                    vm.StatusText = $"Izvezeno: {uspesno} od {selektovani.Count} listića...";
                }

                vm.StatusText = $"Batch izvoz uspešno završen! {uspesno} listića je sačuvano u {folderPath}.";
                MessageBox.Show($"Uspešno je izvezeno {uspesno} pojedinačnih platnih listića u folder:\n\n{folderPath}",
                    "Batch izvoz završen", MessageBoxButton.OK, MessageBoxImage.Information);

                // Otvori folder u Exploreru
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška tokom batch izvoza: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                vm.StatusText = "Greška tokom batch izvoza.";
            }
        }
    }

    // ── METODA: Generisanje jednog PDF-a ──
    private void GenerateSinglePdf(ObracunPlate o, string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page => ConfigurePage(page, o));
        }).GeneratePdf(filePath);
    }

    // ── METODA: Generisanje zbirnog PDF-a (jedan dokument sa više strana) ──
    private void GenerateConsolidatedPdf(List<ObracunPlate> list, string filePath)
    {
        Document.Create(container =>
        {
            foreach (var o in list)
            {
                container.Page(page => ConfigurePage(page, o));
            }
        }).GeneratePdf(filePath);
    }

    // ── POMOĆNA METODA: Konfiguracija izgleda jedne stranice platnog listića ──
    private void ConfigurePage(PageDescriptor page, ObracunPlate o)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.2f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Calibri"));

        // Dekodiranje stepena / kategorije
        string stepenText = "-";
        if (int.TryParse(o.Radnik.Kategorija, out int razred) && razred >= 1 && razred <= 8)
        {
            string[] razred_c = {
                "I ili II stepen",
                "III stepen",
                "IV stepen",
                "V stepen",
                "VI stepen",
                "VII1 stepen",
                "VII2 stepen",
                "VIII stepen"
            };
            stepenText = razred_c[razred - 1];
        }
        else if (!string.IsNullOrWhiteSpace(o.Radnik.Kategorija))
        {
            stepenText = o.Radnik.Kategorija;
        }

        // Rekonstrukcija vrednosti boda i formule: bod - min_plata% = neto_bod
        decimal bod = 1860.34m; // Standardna vrednost boda
        decimal minPlataPercent = 0m;
        if (o.Radnik.OsnovnaPlata > 0 && o.Radnik.OsnovnaPlata <= 100)
        {
            minPlataPercent = o.Radnik.OsnovnaPlata;
        }
        decimal netoBod = bod * (1 - minPlataPercent / 100);

        // Računanje godina staža za minuli rad
        int yearsOfTenure = 0;
        if (o.Radnik.DatumZaposlenja.HasValue)
        {
            var obracunDate = new DateTime(o.Godina, o.Mesec, 1);
            yearsOfTenure = (int)((obracunDate - o.Radnik.DatumZaposlenja.Value).TotalDays / 365.0);
            if (yearsOfTenure < 0) yearsOfTenure = 0;
            if (yearsOfTenure > 99) yearsOfTenure = 99;
        }
        decimal minuliRadPercent = yearsOfTenure * 0.40m;

        // Header
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("ZAVOD ZA POLJOPRIVREDU").Bold().FontSize(12).FontColor(Colors.Indigo.Darken4);
                col.Item().Text("PIROT (PIB: 100224119, MB: 07198305)").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text($"OBRAČUN ZARADE za {o.Mesec:D2}/{o.Godina}").Bold().FontSize(11).FontColor(Colors.Indigo.Medium);
            });
            
            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text("ZAVOD ZA POLJOPRIVREDU PIROT").Bold().FontSize(8).FontColor(Colors.Indigo.Darken4);
            });
        });

        // Content
        page.Content().PaddingVertical(0.4f, Unit.Centimetre).Column(col =>
        {
            // 1. Podaci o radniku
            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Zaposleni:").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(o.Radnik.ImeIPrezime).Bold().FontSize(10);
                    c.Item().Text($"Radno mesto: {o.Radnik.Radno_Mesto ?? "Nije definisano"} (Red. br: {o.Radnik.BrojRadnika})").FontSize(8);
                    if (stepenText != "-")
                    {
                        c.Item().Text($"Stepen stručne spreme: {stepenText}").FontSize(8);
                    }
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Detalji obračuna:").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text($"JMBG: {o.Radnik.Jmbg ?? "-"}").FontSize(8);
                    c.Item().Text($"Bankovni račun: {o.Radnik.BankovniRacun ?? "-"}").FontSize(8);
                    c.Item().Text($"Koeficijent: {o.Radnik.Koeficijent:N2}").FontSize(8);
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Bodovna formula:").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text($"Bod: {bod:N2} - {minPlataPercent:N2}% = {netoBod:N2}").FontSize(8);
                    c.Item().Text($"Godine staža: {yearsOfTenure} ({minuliRadPercent:F2}%)").FontSize(8);
                });
            });

            // 2. Evidencija časova
            col.Item().PaddingTop(8).Text("EVIDENCIJA ČASOVA").Bold().FontSize(9).FontColor(Colors.Indigo.Darken4);
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Indigo.Darken4);

            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(100);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Opis vrste rada / odsustva").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("Ostvareni časovi").Bold().FontSize(8);
                });

                table.Cell().Text("Redovan rad (sati po vremenu)").FontSize(8);
                table.Cell().AlignRight().Text($"{o.RedovniSati:N2}").FontSize(8);

                if (o.BolovanjeSati > 0)
                {
                    table.Cell().Text("Radni sati - bolovanje do 30 dana").FontSize(8);
                    table.Cell().AlignRight().Text($"{o.BolovanjeSati:N2}").FontSize(8);
                }

                if (o.PrekovremeneSati > 0)
                {
                    table.Cell().Text("Radni sati - prekovremeni, noćni, praznici").FontSize(8);
                    table.Cell().AlignRight().Text($"{o.PrekovremeneSati:N2}").FontSize(8);
                }

                if (o.GodisnjioOdmorSati > 0)
                {
                    table.Cell().Text("Godišnji odmor").FontSize(8);
                    table.Cell().AlignRight().Text($"{o.GodisnjioOdmorSati:N2}").FontSize(8);
                }

                // Ukupno časova
                int ukupnoSati = o.RedovniSati + o.BolovanjeSati + o.PrekovremeneSati + o.GodisnjioOdmorSati;
                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2).Text("Ukupno radnih časova").Bold().FontSize(8);
                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2).AlignRight().Text($"{ukupnoSati:N2}").Bold().FontSize(8);
            });

            // 3. Finansijski obračun
            col.Item().PaddingTop(8).Text("FINANSIJSKI OBRAČUN").Bold().FontSize(9).FontColor(Colors.Indigo.Darken4);
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Indigo.Darken4);

            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(90);
                    columns.ConstantColumn(90);
                    columns.ConstantColumn(90);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Opis finansijske stavke / Osnovica").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("UKUPNO").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("PRIM. AKONT.").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("ZA ISPLATU").Bold().FontSize(8);
                });

                void AddRow(string desc, decimal val, bool bold = false)
                {
                    table.Cell().PaddingVertical(1).Text(desc).Style(bold ? TextStyle.Default.Bold().FontSize(8) : TextStyle.Default.FontSize(8));
                    table.Cell().PaddingVertical(1).AlignRight().Text($"{val:N2}").Style(bold ? TextStyle.Default.Bold().FontSize(8) : TextStyle.Default.FontSize(8));
                    table.Cell().PaddingVertical(1).AlignRight().Text("0,00").Style(bold ? TextStyle.Default.Bold().FontSize(8) : TextStyle.Default.FontSize(8));
                    table.Cell().PaddingVertical(1).AlignRight().Text($"{val:N2}").Style(bold ? TextStyle.Default.Bold().FontSize(8) : TextStyle.Default.FontSize(8));
                }

                decimal totalBruto = o.BrutoZarada + o.BrutoBolovanje;
                decimal naknade = o.BrutoNaknade;
                if (naknade == totalBruto)
                {
                    naknade = 0; // Ignore duplicate migrated total in BrutoNaknade column
                }
                decimal baseBruto = totalBruto - o.BrutoBolovanje - naknade - o.BrutoMinuliRad - o.BrutoStimulacija;

                // Bruto delovi
                if (baseBruto > 0)
                {
                    AddRow("Bruto zarada (redovan rad)", baseBruto);
                }
                if (o.BrutoMinuliRad > 0)
                {
                    AddRow($"Bruto naknada - minuli rad ({minuliRadPercent:F2}%)", o.BrutoMinuliRad);
                }
                if (o.BrutoBolovanje > 0)
                {
                    AddRow("Bruto naknada - bolovanje do 30 dana", o.BrutoBolovanje);
                }
                if (naknade > 0)
                {
                    AddRow("Bruto naknada - prekovremeni, noćni, praznici", naknade);
                }
                if (o.BrutoStimulacija > 0)
                {
                    AddRow("Varijabila / Stimulacija", o.BrutoStimulacija);
                }

                // Linija razdvajanja
                table.Cell().ColumnSpan(4).PaddingVertical(1).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                // Ukupno Bruto
                AddRow("UKUPNA BRUTO ZARADA", totalBruto, bold: true);

                // Osnovica i porez
                AddRow("Osnovica za obračun doprinosa", totalBruto);
                if (o.PorezNaDohodak > 0)
                {
                    AddRow("Porez na dohodak građana (stopa 10.00%)", o.PorezNaDohodak);
                }

                // Doprinosi zaposlenog
                if (o.DoprinosPioRadnik > 0)
                {
                    decimal pioRate = o.Radnik.StopaPio > 0 ? o.Radnik.StopaPio * 100 : 14.00m;
                    AddRow($"Doprinos za PIO (stopa {pioRate:F2}%)", o.DoprinosPioRadnik);
                }
                if (o.DoprinosZdravstvoRadnik > 0)
                {
                    decimal zdrRate = o.Radnik.StopaZdravstvo > 0 ? o.Radnik.StopaZdravstvo * 100 : 5.15m;
                    AddRow($"Doprinos za zdravstvo (stopa {zdrRate:F2}%)", o.DoprinosZdravstvoRadnik);
                }
                if (o.DoprinosNezaposlenostRadnik > 0)
                {
                    decimal nezRate = o.Radnik.StopaNezaposlenost > 0 ? o.Radnik.StopaNezaposlenost * 100 : 0.75m;
                    AddRow($"Doprinos za nezaposlenost (stopa {nezRate:F2}%)", o.DoprinosNezaposlenostRadnik);
                }

                // Neto 1
                decimal ukupniDoprinosi = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;
                decimal neto1 = totalBruto - o.PorezNaDohodak - ukupniDoprinosi;
                AddRow("Zarada zaposlenog umanjena za por. i dop.", neto1, bold: true);

                // Odbici / obustave
                if (o.KreditObustava > 0)
                {
                    AddRow("Obustave po osnovu kredita i admin. zabrana", o.KreditObustava);
                }
                // Dinamičko učitavanje pojedinačnih stavki samodoprinosa / obustava iz SQLite
                var detailedSam = new List<Samodoprinosi>();
                try
                {
                    using var dbDetails = PlataData.PlataDbContext.Create(PlataApp.AppConfig.DbPath);
                    detailedSam = dbDetails.Samodoprinosi
                        .Where(s => s.RadnikId == o.RadnikId && s.Godina == o.Godina && s.Mesec == o.Mesec)
                        .ToList();
                }
                catch {}

                if (detailedSam.Count > 0)
                {
                    foreach (var s in detailedSam)
                    {
                        AddRow(s.Opis, s.Iznos);
                    }
                }
                else if (o.Samodoprinosi > 0)
                {
                    AddRow("Opštinski samodoprinosi", o.Samodoprinosi);
                }
                if (o.OstaliOdbici > 0)
                {
                    AddRow("Ostali odbici / obustave", o.OstaliOdbici);
                }

                // Linija razdvajanja
                table.Cell().ColumnSpan(4).PaddingVertical(1).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                // Konačno za isplatu
                AddRow("ZA ISPLATU (Konačni neto)", o.NetoIsplata, bold: true);

                // Bruto 2
                decimal bruto2 = totalBruto + o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac;
                AddRow("Bruto 2 (Doprinosi na teret poslodavca)", bruto2);
            });

            // Potpisi
            col.Item().PaddingTop(25).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).Text("Potpis ovlašćenog lica").AlignCenter().FontSize(7).FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(150);
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).Text("Potpis zaposlenog").AlignCenter().FontSize(7).FontColor(Colors.Grey.Darken2);
                });
            });
        });

        // Footer
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Stranica ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            text.Span(" od ").FontSize(8).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
