using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using PlataData;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PlataApp.Views.Stampe;

public partial class StampePage : Page
{
    public StampePage()
    {
        InitializeComponent();
    }

    private void BtnGenerisiSpisak_Click(object sender, RoutedEventArgs e)
    {
        GenerisiPlatniSpisak(poJedinicama: false);
    }

    private void BtnGenerisiSpisakRj_Click(object sender, RoutedEventArgs e)
    {
        GenerisiPlatniSpisak(poJedinicama: true);
    }

    private void GenerisiPlatniSpisak(bool poJedinicama)
    {
        if (DataContext is not StampeViewModel vm) return;

        int godina = vm.SelectedGodina;
        int mesec = vm.SelectedMesec;
        string rjFilter = vm.SelectedRadnaJedinica;

        int? targetRj = null;
        if (rjFilter != "Sve radne jedinice")
        {
            var parts = rjFilter.Split(' ');
            if (parts.Length > 0 && int.TryParse(parts.Last(), out int rjNum))
            {
                targetRj = rjNum;
            }
        }

        try
        {
            vm.StatusText = "Učitavanje podataka iz baze...";
            using var db = PlataDbContext.Create(AppConfig.DbPath);
            
            var obracuni = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Godina == godina && o.Mesec == mesec)
                .ToList();

            if (targetRj.HasValue)
            {
                obracuni = obracuni.Where(o => o.Radnik.BrojRadneJedinice == targetRj.Value).ToList();
            }

            if (obracuni.Count == 0)
            {
                vm.StatusText = "Nema obračuna za izabrani period/RJ.";
                MessageBox.Show("Nema obračunatih plata za izabrani period i radnu jedinicu.", 
                    "Nema podataka", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Poredaj po broju radnika
            obracuni = obracuni.OrderBy(o => o.Radnik.BrojRadnika).ToList();

            string suggestedName = poJedinicama 
                ? $"Platni_Spisak_po_RJ_{mesec:D2}_{godina}.pdf"
                : $"Platni_Spisak_{mesec:D2}_{godina}.pdf";

            if (targetRj.HasValue)
            {
                suggestedName = $"Platni_Spisak_RJ{targetRj.Value}_{mesec:D2}_{godina}.pdf";
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PDF dokument (*.pdf)|*.pdf",
                FileName = suggestedName,
                Title = "Sačuvaj platni spisak"
            };

            if (sfd.ShowDialog() == true)
            {
                vm.StatusText = "Generisanje platnog spiska...";
                
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        var doc = new PlatniSpisakDocument(obracuni, godina, mesec, rjFilter, poJedinicama);
                        doc.Build(page);
                    });
                }).GeneratePdf(sfd.FileName);

                vm.StatusText = $"Platni spisak uspešno sačuvan: {Path.GetFileName(sfd.FileName)}";
                
                var res = MessageBox.Show("Platni spisak je uspešno generisan. Želite li da ga otvorite?",
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
            else
            {
                vm.StatusText = "Otkazano čuvanje PDF-a.";
            }
        }
        catch (Exception ex)
        {
            vm.StatusText = "Greška pri generisanju platnog spiska.";
            MessageBox.Show($"Greška tokom generisanja platnog spiska: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnGenerisiRekapitulaciju_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not StampeViewModel vm) return;

        int godina = vm.SelectedGodina;
        int mesec = vm.SelectedMesec;
        string rjFilter = vm.SelectedRadnaJedinica;

        int? targetRj = null;
        if (rjFilter != "Sve radne jedinice")
        {
            var parts = rjFilter.Split(' ');
            if (parts.Length > 0 && int.TryParse(parts.Last(), out int rjNum))
            {
                targetRj = rjNum;
            }
        }

        try
        {
            vm.StatusText = "Učitavanje podataka za rekapitulaciju...";
            using var db = PlataDbContext.Create(AppConfig.DbPath);
            
            var obracuni = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Godina == godina && o.Mesec == mesec)
                .ToList();

            if (targetRj.HasValue)
            {
                obracuni = obracuni.Where(o => o.Radnik.BrojRadneJedinice == targetRj.Value).ToList();
            }

            if (obracuni.Count == 0)
            {
                vm.StatusText = "Nema obračuna za izabrani period/RJ.";
                MessageBox.Show("Nema obračunatih plata za izabrani period i radnu jedinicu za rekapitulaciju.", 
                    "Nema podataka", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string suggestedName = targetRj.HasValue
                ? $"Rekapitulacija_RJ{targetRj.Value}_{mesec:D2}_{godina}.pdf"
                : $"Rekapitulacija_{mesec:D2}_{godina}.pdf";

            var sfd = new SaveFileDialog
            {
                Filter = "PDF dokument (*.pdf)|*.pdf",
                FileName = suggestedName,
                Title = "Sačuvaj mesečnu rekapitulaciju"
            };

            if (sfd.ShowDialog() == true)
            {
                vm.StatusText = "Generisanje rekapitulacije...";
                
                // Učitaj odbice (samodoprinosi + krediti) po imenima za dinamički prikaz
                var odbici = db.Samodoprinosi
                    .Where(s => s.Godina == godina && s.Mesec == mesec)
                    .ToList();

                if (targetRj.HasValue)
                {
                    var rjRadniciIds = obracuni.Select(o => o.RadnikId).ToHashSet();
                    odbici = odbici.Where(s => rjRadniciIds.Contains(s.RadnikId)).ToList();
                }

                // Učitaj doprinose poslodavca za ovaj mesec/godinu
                var doprPoslodavca = db.DoprinosiPoslodavca
                    .Where(d => d.Godina == godina && d.Mesec == mesec)
                    .ToList();

                if (targetRj.HasValue)
                {
                    var rjRadniciIds = obracuni.Select(o => o.RadnikId).ToHashSet();
                    doprPoslodavca = doprPoslodavca.Where(d => rjRadniciIds.Contains(d.RadnikId)).ToList();
                }

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        var doc = new RekapitulacijaDocument(obracuni, godina, mesec, rjFilter, odbici, doprPoslodavca);
                        doc.Build(page);
                    });
                }).GeneratePdf(sfd.FileName);

                vm.StatusText = $"Rekapitulacija uspešno sačuvana: {Path.GetFileName(sfd.FileName)}";
                
                var res = MessageBox.Show("Rekapitulacija je uspešno generisana. Želite li da je otvorite?",
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
            else
            {
                vm.StatusText = "Otkazano čuvanje PDF-a.";
            }
        }
        catch (Exception ex)
        {
            vm.StatusText = "Greška pri generisanju rekapitulacije.";
            MessageBox.Show($"Greška tokom generisanja rekapitulacije: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnGenerisiBanke_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not StampeViewModel vm) return;

        int godina = vm.SelectedGodina;
        int mesec = vm.SelectedMesec;
        string rjFilter = vm.SelectedRadnaJedinica;

        int? targetRj = null;
        if (rjFilter != "Sve radne jedinice")
        {
            var parts = rjFilter.Split(' ');
            if (parts.Length > 0 && int.TryParse(parts.Last(), out int rjNum))
            {
                targetRj = rjNum;
            }
        }

        try
        {
            vm.StatusText = "Učitavanje podataka za izveštaj banaka...";
            using var db = PlataDbContext.Create(AppConfig.DbPath);

            var obracuni = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Godina == godina && o.Mesec == mesec)
                .ToList();

            if (targetRj.HasValue)
            {
                obracuni = obracuni.Where(o => o.Radnik.BrojRadneJedinice == targetRj.Value).ToList();
            }

            if (obracuni.Count == 0)
            {
                vm.StatusText = "Nema obračuna za izabrani period/RJ.";
                MessageBox.Show("Nema obračunatih plata za izabrani period i radnu jedinicu za izveštaj banaka.", 
                    "Nema podataka", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string suggestedName = targetRj.HasValue
                ? $"Izvestaj_za_banke_RJ{targetRj.Value}_{mesec:D2}_{godina}.pdf"
                : $"Izvestaj_za_banke_{mesec:D2}_{godina}.pdf";

            var sfd = new SaveFileDialog
            {
                Filter = "PDF dokument (*.pdf)|*.pdf",
                FileName = suggestedName,
                Title = "Sačuvaj izveštaj za banke"
            };

            if (sfd.ShowDialog() == true)
            {
                vm.StatusText = "Generisanje izveštaja za banke...";

                // Učitaj šifrarnik banaka iz SQLite-a za ovaj period
                var bankeInfo = db.Banke
                    .Where(b => b.Godina == godina && b.Mesec == mesec)
                    .ToList();

                // Učitaj naziv firme
                string nazivFirme = "";
                var firma = db.Firme.FirstOrDefault();
                if (firma != null && !string.IsNullOrWhiteSpace(firma.Naziv))
                {
                    nazivFirme = firma.Naziv;
                }

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        var doc = new BankeIzvestajDocument(obracuni, godina, mesec, rjFilter, bankeInfo, nazivFirme);
                        doc.Build(page);
                    });
                }).GeneratePdf(sfd.FileName);

                vm.StatusText = $"Izveštaj za banke uspešno sačuvan: {Path.GetFileName(sfd.FileName)}";

                var res = MessageBox.Show("Izveštaj za banke je uspešno generisan. Želite li da ga otvorite?",
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
            else
            {
                vm.StatusText = "Otkazano čuvanje PDF-a.";
            }
        }
        catch (Exception ex)
        {
            vm.StatusText = "Greška pri generisanju izveštaja za banke.";
            MessageBox.Show($"Greška tokom generisanja izveštaja za banke: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

