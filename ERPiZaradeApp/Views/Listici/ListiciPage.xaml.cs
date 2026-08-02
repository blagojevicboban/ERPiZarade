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
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Listici;

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
        decimal bod = 1860.34m; // Standardna/fallback vrednost boda
        try
        {
            using var dbDetails = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
            var poreziRecord = dbDetails.Porezi
                .FirstOrDefault(p => p.Godina == o.Godina && p.Mesec == o.Mesec);
            if (poreziRecord != null && poreziRecord.VrBoda > 0)
            {
                bod = poreziRecord.VrBoda;
            }
        }
        catch {}
        decimal minPlataPercent = 0m;
        if (o.Radnik.OsnovnaPlata > 0 && o.Radnik.OsnovnaPlata <= 100)
        {
            minPlataPercent = o.Radnik.OsnovnaPlata;
        }
        decimal netoBod = bod * (1 - minPlataPercent / 100);

        // Računanje godina staža za minuli rad
        int yearsOfTenure = o.MinuliRadGodine;
        decimal procMinul = 0.40m;
        try
        {
            using var dbDetails = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
            var poreziRecord = dbDetails.Porezi
                .FirstOrDefault(p => p.Godina == o.Godina && p.Mesec == o.Mesec);
            if (poreziRecord != null)
            {
                procMinul = poreziRecord.ProcMinul;
            }
        }
        catch {}
        decimal minuliRadPercent = yearsOfTenure * procMinul;

        decimal stimulacijaPercent = 0m;
        try
        {
            using var dbDetails = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
            var radniSatiRecord = dbDetails.RadniSati
                .FirstOrDefault(r => r.RadnikId == o.RadnikId && r.Godina == o.Godina && r.Mesec == o.Mesec);
            if (radniSatiRecord != null)
            {
                stimulacijaPercent = radniSatiRecord.Stimulacija;
            }
        }
        catch {}

        string nazivFirme = "NAZIV FIRME";
        string podaciFirme = "PIB: -, MB: -";
        try
        {
            using var db = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
            var firma = db.Firme.FirstOrDefault();
            if (firma != null)
            {
                nazivFirme = (firma.Naziv + " " + firma.Grad).Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(nazivFirme)) nazivFirme = "NAZIV FIRME";
                podaciFirme = $"(PIB: {firma.Pib ?? "-"}, MB: {firma.Mb ?? "-"})";
            }
        }
        catch {}

        // Header
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(nazivFirme).Bold().FontSize(12).FontColor(Colors.Indigo.Darken4);
                col.Item().Text(podaciFirme).FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text($"OBRAČUN ZARADE za {o.Mesec:D2}/{o.Godina}").Bold().FontSize(11).FontColor(Colors.Indigo.Medium);
            });
            
            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().Text($"Datum štampe: {DateTime.Now:dd.MM.yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Text(nazivFirme).Bold().FontSize(8).FontColor(Colors.Indigo.Darken4);
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

                table.Cell().Text("Bolovanje (sati)").FontSize(8);
                table.Cell().AlignRight().Text($"{o.BolovanjeSati:N2}").FontSize(8);

                table.Cell().Text("Prekovremeni rad (sati)").FontSize(8);
                table.Cell().AlignRight().Text($"{o.PrekovremeneSati:N2}").FontSize(8);

                table.Cell().Text("Godišnji odmor (sati)").FontSize(8);
                table.Cell().AlignRight().Text($"{o.GodisnjioOdmorSati:N2}").FontSize(8);

                table.Cell().Text("Državni praznik (sati)").FontSize(8);
                table.Cell().AlignRight().Text($"{o.DrzavniPraznikSati:N2}").FontSize(8);

                table.Cell().Text("Noćni rad (sati)").FontSize(8);
                table.Cell().AlignRight().Text($"{o.NocniSati:N2}").FontSize(8);

                // Ukupno časova
                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2).Text("Ukupno radnih časova").Bold().FontSize(8);
                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2).AlignRight().Text($"{o.UkupnoSati:N2}").Bold().FontSize(8);
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

                decimal totalBruto = o.Neto; // totalBruto iz obračuna (sve komponente)

                // Bruto delovi (iz DBF kolona)
                AddRow("Bruto zarada (redovan rad)", o.NetoZar);
                AddRow($"Bruto naknada - minuli rad ({minuliRadPercent:F2}%)", o.BrutoMinuliRad);
                AddRow($"Bruto naknada - stimulacija ({stimulacijaPercent:F2}%)", o.BrutoStimulacija);
                AddRow("Bruto naknada - bolovanje do 30 dana", o.NetoBol);

                if (o.NetoB100 > 0)
                {
                    AddRow("Bruto naknada - bolovanje 100%", o.NetoB100);
                }
                if (o.NetoPlac > 0)
                {
                    AddRow("Bruto naknada - plaćeno odsustvo", o.NetoPlac);
                }
                if (o.NetoPlZ > 0)
                {
                    AddRow("Bruto naknada - plaćeno odsustvo zakonski", o.NetoPlZ);
                }

                AddRow("Bruto naknada - neradni državni praznik", o.NetoNerd);
                AddRow("Bruto naknada - rad na državni praznik", o.NetoDrza);
                AddRow("Bruto naknada - godišnji odmor", o.NetoGOd);
                AddRow("Bruto naknada - noćni rad", o.NetoNocni);

                if (o.NetoVezba > 0)
                {
                    AddRow("Bruto naknada - vojna vežba", o.NetoVezba);
                }

                AddRow("Bruto naknada - prekovremeni rad", o.NetoPrek);
                AddRow("Bruto dodatak - topli obrok", o.NetoTo);
                AddRow("Bruto dodatak - regres", o.NetoReg);

                if (o.NetoTer > 0)
                {
                    AddRow("Bruto dodatak - terenski dodatak", o.NetoTer);
                }
                if (o.KorDod > 0)
                {
                    AddRow("Bruto dodatak - korektivni dodatak", o.KorDod);
                }
                if (o.KorDod1 > 0)
                {
                    AddRow("Bruto dodatak - korektivni dodatak 1", o.KorDod1);
                }
                if (o.Kumul > 0)
                {
                    AddRow("Kumulativ", o.Kumul);
                }
                if (o.NetoNede > 0)
                {
                    AddRow("Bruto naknada - rad nedeljom", o.NetoNede);
                }

                AddRow("Bruto dodatak", o.Varijabila);

                // Linija razdvajanja
                table.Cell().ColumnSpan(4).PaddingVertical(1).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                // Ukupno Bruto
                AddRow("UKUPNA BRUTO ZARADA", totalBruto, bold: true);

                // Porez na dohodak (sa poreskim oslobođenjem i osnovicom za porez iznad)
                decimal poreskoOslobodjenje = o.LicniOdbitak > 0 ? o.LicniOdbitak : (totalBruto - o.PoreskaOsnovica);
                AddRow("Poresko oslobođenje", poreskoOslobodjenje);
                AddRow("Osnovica za porez", o.PoreskaOsnovica);
                if (o.PorezNaDohodak > 0)
                {
                    AddRow("Porez na dohodak građana (stopa 10.00%)", o.PorezNaDohodak);
                }

                // Osnovica za doprinose (iznad doprinosa na teret radnika)
                if (o.BrutoOsnovica == o.BrutoPioOsnovica)
                {
                    AddRow("Osnovica za obračun doprinosa", o.BrutoOsnovica);
                }
                else
                {
                    AddRow("Osnovica za obračun doprinosa (PIO)", o.BrutoPioOsnovica);
                    AddRow("Osnovica za obračun doprinosa (zdr. i nez.)", o.BrutoOsnovica);
                }

                // Doprinosi zaposlenog
                // Doprinosi zaposlenog (prikazuju se uvek, čak i ako su 0)
                {
                    decimal pioRate = o.Radnik.StopaPio > 0 ? o.Radnik.StopaPio * 100 : 14.00m;
                    AddRow($"Doprinos za PIO (stopa {pioRate:F2}%)", o.DoprinosPioRadnik);
                }
                {
                    decimal zdrRate = o.Radnik.StopaZdravstvo > 0 ? o.Radnik.StopaZdravstvo * 100 : 5.15m;
                    AddRow($"Doprinos za zdravstvo (stopa {zdrRate:F2}%)", o.DoprinosZdravstvoRadnik);
                }
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
                    using var dbDetails = ERPiZaradeData.PlataDbContext.Create(ERPiZaradeApp.AppConfig.DbPath);
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

                // Bruto 1
                decimal bruto1 = o.Bruto1;
                AddRow("Bruto 1 (Neto + porez + doprinosi)", bruto1);

                // Doprinosi poslodavca
                decimal bossPioRate = 10.00m;
                if (decimal.TryParse(o.StopaPioPoslodavacStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valPioP)) 
                    bossPioRate = valPioP;
                else
                {
                    if (o.Godina >= 2023) bossPioRate = 10.00m;
                    else if (o.Godina == 2022) bossPioRate = 11.00m;
                    else if (o.Godina >= 2020 || (o.Godina == 2019 && o.Mesec == 12)) bossPioRate = 11.50m;
                    else bossPioRate = 12.00m;
                }

                decimal bossZdrRate = 5.15m;
                if (decimal.TryParse(o.StopaZdravstvoPoslodavacStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valZdrP))
                    bossZdrRate = valZdrP;

                decimal bossNezRate = 0.00m;
                if (decimal.TryParse(o.StopaNezaposlenostPoslodavacStr?.Replace("%", "").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valNezP))
                    bossNezRate = valNezP;
                else
                {
                    if (o.Godina >= 2020 || (o.Godina == 2019 && o.Mesec == 12)) bossNezRate = 0.00m;
                    else bossNezRate = 0.75m;
                }

                // Doprinosi poslodavca (prikazuju se uvek, čak i ako su 0)
                {
                    AddRow($"Doprinos za PIO na teret poslodavca (stopa {bossPioRate:F2}%)", o.DoprinosPioPoslodavac);
                }
                {
                    AddRow($"Doprinos za zdravstvo na teret poslodavca (stopa {bossZdrRate:F2}%)", o.DoprinosZdravstvoPoslodavac);
                }
                {
                    AddRow($"Doprinos za nezaposlenost na teret poslodavca (stopa {bossNezRate:F2}%)", o.DoprinosNezaposlenostPoslodavac);
                }

                // Bruto 2
                decimal bruto2 = o.Bruto2;
                AddRow("Bruto 2 (Bruto 1 + doprinosi poslodavca)", bruto2);
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
