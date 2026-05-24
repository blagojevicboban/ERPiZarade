using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using PlataData;
using PlataData.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PlataApp.Views.Krediti;

public partial class KreditiPage : Page
{
    public KreditiPage()
    {
        InitializeComponent();
    }

    private async void CheckBoxAktivan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is Kredit kredit)
        {
            // Obezbedi da se checkbox ponaša u skladu sa async DB operacijom
            cb.IsChecked = kredit.Aktivan;
            
            if (DataContext is KreditiViewModel vm)
            {
                await vm.ToggleKreditAktivnostAsync(kredit);
                // Osveži prikaz
                KreditiGrid.Items.Refresh();
            }
        }
    }

    private async void BtnObrisiKredit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Kredit kredit)
        {
            if (DataContext is KreditiViewModel vm)
            {
                await vm.DeleteKreditAsync(kredit);
            }
        }
    }

    private async void BtnNoviKredit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not KreditiViewModel vm || vm.SelectedRadnik == null) return;

        var dialog = new NoviKreditWindow(vm.SelectedRadnik);
        dialog.Owner = Window.GetWindow(this);
        
        if (dialog.ShowDialog() == true)
        {
            await vm.LoadKreditiForSelectedRadnikAsync();
        }
    }

    private async void BtnIzmeniKredit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Kredit kredit)
        {
            if (DataContext is not KreditiViewModel vm || vm.SelectedRadnik == null) return;

            var dialog = new NoviKreditWindow(vm.SelectedRadnik, kredit);
            dialog.Owner = Window.GetWindow(this);
            
            if (dialog.ShowDialog() == true)
            {
                await vm.LoadKreditiForSelectedRadnikAsync();
            }
        }
    }

    private void BtnZbirniIzvestaj_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not KreditiViewModel vm) return;

        // Podrazumevano koristi trenutni mesec i godinu
        int godina = DateTime.Now.Year;
        int mesec = DateTime.Now.Month;

        try
        {
            using var db = PlataDbContext.Create(AppConfig.DbPath);
            
            // Izvuci sve kredite sa radnicima
            var sviKrediti = db.Krediti
                .Include(k => k.Radnik)
                .Where(k => k.Aktivan)
                .ToList();

            if (sviKrediti.Count == 0)
            {
                MessageBox.Show("Nema aktivnih obustava kredita u sistemu za štampu izveštaja.", 
                    "Nema podataka", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PDF dokument (*.pdf)|*.pdf",
                FileName = $"Zbirni_Izvestaj_Kredita_{mesec:D2}_{godina}.pdf",
                Title = "Sačuvaj zbirni izveštaj kredita"
            };

            if (sfd.ShowDialog() == true)
            {
                vm.StatusText = "Generisanje izveštaja kredita...";
                
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        var doc = new KreditIzvestajDocument(sviKrediti, godina, mesec);
                        doc.Build(page);
                    });
                }).GeneratePdf(sfd.FileName);

                vm.StatusText = $"Izveštaj uspešno sačuvan: {Path.GetFileName(sfd.FileName)}";
                
                var res = MessageBox.Show("Zbirni izveštaj kredita je uspešno generisan. Želite li da ga otvorite?",
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
        }
        catch (Exception ex)
        {
            vm.StatusText = "Greška pri generisanju izveštaja kredita.";
            MessageBox.Show($"Greška tokom generisanja izveštaja: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
