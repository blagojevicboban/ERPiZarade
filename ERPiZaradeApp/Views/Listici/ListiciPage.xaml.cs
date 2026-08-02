using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
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

    // ── AKCIJA: Slanje listića e-mailom ──
    private async void BtnPosaljiEmail_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ListiciViewModel vm) return;

        var selektovani = vm.Obracuni.Where(o => o.IsSelected).Select(o => o.Obracun).ToList();
        if (selektovani.Count == 0)
        {
            MessageBox.Show("Molimo vas da selektujete barem jednog zaposlenog.",
                "Nema selekcije", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var podesavanja = UcitajSmtpPodesavanja();
        if (!podesavanja.JePotpuno)
        {
            MessageBox.Show(
                "Nalog za slanje e-maila nije podešen.\n\nOtvorite Podešavanja → E-mail i unesite SMTP server, port i adresu pošiljaoca.",
                "E-mail nije podešen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool zastiti = UserSettings.Instance.ListiciZastitaLozinkom;
        int bezEmaila = selektovani.Count(o => string.IsNullOrWhiteSpace(o.Radnik?.Email));

        string upozorenje = bezEmaila > 0
            ? $"\n\nNapomena: {bezEmaila} od {selektovani.Count} zaposlenih nema e-mail adresu i biće preskočeno."
            : "";

        var potvrda = MessageBox.Show(
            $"Poslati platni listić na e-mail za {selektovani.Count} zaposlenih?\n\n" +
            $"Zaštita lozinkom: {(zastiti ? "uključena (lozinka je JMBG radnika)" : "isključena")}" +
            upozorenje,
            "Potvrda slanja", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (potvrda != MessageBoxResult.Yes) return;

        BtnPosaljiEmail.IsEnabled = false;
        vm.StatusText = $"Slanje listića za {selektovani.Count} zaposlenih...";

        try
        {
            using var db = ERPiZaradeData.PlataDbContext.Create(AppConfig.DbPath);
            using var posiljalac = new Services.SmtpPosiljalac();

            // Obračuni se učitavaju iz ovog konteksta da bi evidencija slanja i podaci
            // radnika bili iz istog izvora kao upis.
            var idjevi = selektovani.Select(o => o.Id).ToList();
            var zaSlanje = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => idjevi.Contains(o.Id))
                .ToList();

            var servis = new Services.ListicEmailService(db, posiljalac);
            var izvestaj = await servis.PosaljiAsync(zaSlanje, podesavanja, zastiti);

            vm.StatusText = $"Poslato {izvestaj.Poslato}, neuspešno {izvestaj.Neuspesno}, preskočeno {izvestaj.Preskoceno}.";
            MessageBox.Show(SastaviIzvestaj(izvestaj), "Slanje završeno",
                MessageBoxButton.OK,
                izvestaj.Neuspesno > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            vm.StatusText = "Greška pri slanju listića.";
            MessageBox.Show($"Slanje nije izvršeno:\n\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnPosaljiEmail.IsEnabled = true;
        }
    }

    private static Services.SmtpPodesavanja UcitajSmtpPodesavanja()
    {
        var s = UserSettings.Instance;
        return new Services.SmtpPodesavanja
        {
            Server = s.SmtpServer ?? "",
            Port = s.SmtpPort,
            KoristiSsl = s.SmtpKoristiSsl,
            Korisnik = s.SmtpKorisnik ?? "",
            Lozinka = s.SmtpLozinka,
            AdresaPosiljaoca = s.SmtpAdresaPosiljaoca ?? "",
            ImePosiljaoca = s.SmtpImePosiljaoca ?? ""
        };
    }

    /// <summary>Poimenično nabraja koga listić NIJE stigao — zbog toga se izveštaj i pravi.</summary>
    private static string SastaviIzvestaj(Services.IzvestajSlanja izvestaj)
    {
        string tekst = $"Poslato: {izvestaj.Poslato}\n" +
                       $"Neuspešno: {izvestaj.Neuspesno}\n" +
                       $"Preskočeno: {izvestaj.Preskoceno}\n";

        var problematicni = izvestaj.Stavke
            .Where(s => s.Ishod != ERPiZaradeData.Models.IshodSlanja.Poslato)
            .Take(15)
            .ToList();

        if (problematicni.Count > 0)
        {
            tekst += "\nNije poslato:\n" + string.Join("\n",
                problematicni.Select(s => $"• {s.BrojRadnika} {s.Radnik} — {s.Napomena}"));
        }

        return tekst;
    }

    // ── METODA: Generisanje jednog PDF-a ──
    private static void GenerateSinglePdf(ObracunPlate o, string filePath)
        => PlatniListicDocument.Sacuvaj(o, filePath);

    // ── METODA: Generisanje zbirnog PDF-a (jedan dokument sa više strana) ──
    private static void GenerateConsolidatedPdf(List<ObracunPlate> list, string filePath)
        => PlatniListicDocument.Sacuvaj(list, filePath);
}
