using System;
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

namespace PlataApp.Views.Obracun;

public partial class ObracunPage : Page
{
    static ObracunPage()
    {
        // Podesi QuestPDF licencu za Community izdanje
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ObracunPage()
    {
        InitializeComponent();
    }

    private void BtnStampajListic_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ObracunViewModel vm || vm.SelectedObracun == null)
        {
            MessageBox.Show("Molimo vas da izaberete obračun za štampu.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var obracun = vm.SelectedObracun;
        var sfd = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = $"Platni_Listic_{obracun.Radnik.ImeIPrezime.Replace(" ", "_")}_{obracun.Mesec:D2}_{obracun.Godina}.pdf",
            Title = "Sačuvaj platni listić"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                GeneratePdfReport(obracun, sfd.FileName);
                
                var result = MessageBox.Show("Platni listić je uspešno generisan. Želite li da ga otvorite?", 
                    "Uspeh", MessageBoxButton.YesNo, MessageBoxImage.Information);
                
                if (result == MessageBoxResult.Yes)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška prilikom generisanja PDF-a: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void GeneratePdfReport(ObracunPlate o, string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Calibri"));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("ZAVOD ZA POLJOPRIVREDU").Bold().FontSize(14).FontColor(Colors.Indigo.Darken4);
                        col.Item().Text("PIROT").FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().Text("Obračunski listić (Platni listić) za: " + $"{o.Mesec:D2}/{o.Godina}").Bold().FontSize(12).FontColor(Colors.Indigo.Medium);
                    });
                    
                    row.ConstantItem(80).AlignRight().AlignMiddle().Column(col =>
                    {
                        col.Item().Text("v1.0.0").FontSize(8).FontColor(Colors.Grey.Lighten1);
                    });
                });

                // Content
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    // 1. Podaci o radniku
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Zaposleni:").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(o.Radnik.ImeIPrezime).Bold().FontSize(12);
                            c.Item().Text($"Radno mesto: {o.Radnik.Radno_Mesto ?? "Nije definisano"}").FontSize(9);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("JMBG:").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(o.Radnik.Jmbg ?? "-").FontSize(10);
                            c.Item().Text($"Tekući račun: {o.Radnik.BankovniRacun ?? "-"}").FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(15).Text("EVIDENCIJA ČASOVA").Bold().FontSize(10).FontColor(Colors.Indigo.Darken4);
                    col.Item().LineHorizontal(1).LineColor(Colors.Indigo.Darken4);

                    // Tabela sati
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Text("Opis").Bold();
                            header.Cell().AlignRight().Text("Časovi").Bold();
                            header.Cell().AlignRight().Text("Bruto iznos (RSD)").Bold();
                        });

                        // Rows
                        table.Cell().Text("Redovan rad (efektivni sati)");
                        table.Cell().AlignRight().Text($"{o.RedovniSati}");
                        table.Cell().AlignRight().Text($"{o.BrutoZarada:N2}");

                        if (o.BolovanjeSati > 0)
                        {
                            table.Cell().Text("Bolovanje");
                            table.Cell().AlignRight().Text($"{o.BolovanjeSati}");
                            table.Cell().AlignRight().Text($"{o.BrutoBolovanje:N2}");
                        }

                        if (o.PrekovremeneSati > 0)
                        {
                            table.Cell().Text("Prekovremeni rad");
                            table.Cell().AlignRight().Text($"{o.PrekovremeneSati}");
                            table.Cell().AlignRight().Text($"{o.BrutoNaknade:N2}");
                        }

                        if (o.GodisnjioOdmorSati > 0)
                        {
                            table.Cell().Text("Godišnji odmor");
                            table.Cell().AlignRight().Text($"{o.GodisnjioOdmorSati}");
                            table.Cell().AlignRight().Text("0,00");
                        }
                    });

                    col.Item().PaddingTop(15).Text("FINANSIJSKI OBRAČUN").Bold().FontSize(10).FontColor(Colors.Indigo.Darken4);
                    col.Item().LineHorizontal(1).LineColor(Colors.Indigo.Darken4);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(120);
                        });

                        decimal ukupniDoprinosi = o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik;

                        table.Cell().Text("1. BRUTO ZARADA (ukupno)");
                        table.Cell().AlignRight().Text($"{(o.BrutoZarada + o.BrutoBolovanje + o.BrutoNaknade):N2}").Bold();

                        table.Cell().Text("2. Porez na dohodak građana");
                        table.Cell().AlignRight().Text($"{o.PorezNaDohodak:N2}").FontColor(Colors.Red.Darken2);

                        table.Cell().Text("3. Doprinosi za socijalno osiguranje (teret zaposlenog)");
                        table.Cell().AlignRight().Text($"{ukupniDoprinosi:N2}").FontColor(Colors.Red.Darken2);

                        table.Cell().Text("4. Ostali odbici i krediti");
                        table.Cell().AlignRight().Text($"{o.KreditObustava:N2}");

                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);

                        table.Cell().Text("ZA ISPLATU (Neto iznos)").Bold().FontSize(12);
                        table.Cell().AlignRight().Text($"{o.NetoIsplata:N2}").Bold().FontSize(13).FontColor(Colors.Green.Darken3);
                    });

                    // Potpisi
                    col.Item().PaddingTop(50).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                            c.Item().PaddingTop(3).Text("Potpis ovlašćenog lica").AlignCenter().FontSize(8);
                        });
                        row.ConstantItem(100);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                            c.Item().PaddingTop(3).Text("Potpis zaposlenog").AlignCenter().FontSize(8);
                        });
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    private void BtnNoviObracun_Click(object sender, RoutedEventArgs e)
    {
        var window = new NoviObracunWindow
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() == true)
        {
            if (DataContext is ObracunViewModel vm)
            {
                _ = vm.LoadObracuneAsync();
            }
        }
    }

    private void BtnIzveziXml_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ObracunViewModel vm || vm.Obracuni == null || vm.Obracuni.Count == 0)
        {
            MessageBox.Show("Nema obračuna za izvoz u odabranom periodu.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var datumPlacanja = PromptForPaymentDate();
        if (!datumPlacanja.HasValue) return;

        var sfd = new SaveFileDialog
        {
            Filter = "XML dokument (*.xml)|*.xml",
            FileName = $"PPP-PD_{vm.SelectedMesec:D2}_{vm.SelectedGodina}.xml",
            Title = "Sačuvaj PPP-PD XML deklaraciju"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var xmlService = new Services.XmlExportService();
                
                // standardni podaci za Zavod za poljoprivredu Pirot
                string pib = "100224119";
                string maticniBroj = "07198305";
                string naziv = "ZAVOD ZA POLJOPRIVREDU PIROT";

                var xmlSadrzaj = xmlService.GeneratePppPdXml(
                    vm.Obracuni.ToList(),
                    datumPlacanja.Value,
                    pib,
                    maticniBroj,
                    naziv
                );

                File.WriteAllText(sfd.FileName, xmlSadrzaj, System.Text.Encoding.UTF8);

                MessageBox.Show("PPP-PD XML poreska deklaracija je uspešno generisana i sačuvana.", 
                    "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška prilikom generisanja XML-a: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private DateTime? PromptForPaymentDate()
    {
        var dialog = new Window
        {
            Title = "Datum plaćanja",
            Width = 330,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White,
            Style = null
        };

        var stack = new StackPanel { Margin = new Thickness(15) };
        var text = new TextBlock 
        { 
            Text = "Izaberite datum plaćanja za PPP-PD prijavu:", 
            Margin = new Thickness(0, 0, 0, 10),
            FontWeight = FontWeights.SemiBold
        };
        
        var datePicker = new DatePicker 
        { 
            SelectedDate = DateTime.Now,
            Margin = new Thickness(0, 0, 0, 15)
        };
        
        var buttons = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right 
        };
        
        var btnOk = new Button 
        { 
            Content = "Potvrdi", 
            Width = 75, 
            Height = 25, 
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnOk.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
        
        var btnCancel = new Button 
        { 
            Content = "Otkaži", 
            Width = 75, 
            Height = 25, 
            IsCancel = true 
        };
        btnCancel.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

        buttons.Children.Add(btnOk);
        buttons.Children.Add(btnCancel);
        
        stack.Children.Add(text);
        stack.Children.Add(datePicker);
        stack.Children.Add(buttons);
        
        dialog.Content = stack;
        
        if (dialog.ShowDialog() == true)
        {
            return datePicker.SelectedDate;
        }
        return null;
    }
}
