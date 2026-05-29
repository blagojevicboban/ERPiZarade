using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Podesavanja;

public partial class PodesavanjaPage : Page
{
    private PlataDbContext _db;

    public PodesavanjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        // Inicijalizacija putanja za rezervnu kopiju
        try
        {
            TxtAktivnaBazaPath.Text = AppConfig.DbPath;
            var dbName = !string.IsNullOrEmpty(AppConfig.DbPath) ? Path.GetFileNameWithoutExtension(AppConfig.DbPath) : "plata";
            TxtPredlozenoIme.Text = $"{dbName}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        }
        catch { }

        // Učitaj korisčke postavke programa
        try
        {
            ChkPokretanjeMaximizovano.IsChecked = UserSettings.Instance.PokretanjeMaximizovano;
        }
        catch { }

        // Učitaj istoriju rezervnih kopija
        OsveziIstorijuKopija();
    }

    private void OsveziIstorijuKopija()
    {
        try
        {
            var kopije = Services.BackupService.Instance.UcitajIstorijuKopija();
            LstIstorijaKopija.ItemsSource = kopije;
        }
        catch { }
    }

    private void ChkPostavke_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            UserSettings.Instance.PokretanjeMaximizovano = ChkPokretanjeMaximizovano.IsChecked == true;
            UserSettings.Instance.Save();
            StatusMessage.Text = "Postavke programa su sačuvane.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri čuvanju postavki: {ex.Message}";
        }
    }

    private void BtnKreirajBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dbPath = AppConfig.DbPath;
            if (!File.Exists(dbPath))
            {
                MessageBox.Show("Aktivna baza podataka ne postoji na navedenoj putanji!", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dbName = !string.IsNullOrEmpty(dbPath) ? Path.GetFileNameWithoutExtension(dbPath) : "plata";
            var dialog = new SaveFileDialog
            {
                Title = "Sačuvaj rezervnu kopiju baze podataka",
                Filter = "SQLite baza podataka (*.db)|*.db|Sve datoteke (*.*)|*.*",
                FileName = $"{dbName}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                DefaultExt = ".db"
            };

            if (dialog.ShowDialog() == true)
            {
                Services.BackupService.Instance.NapraviRucniBackup(dialog.FileName);
                StatusMessage.Text = $"Rezervna kopija je uspešno sačuvana na: {dialog.FileName}";
                MessageBox.Show($"Rezervna kopija baze podataka je uspešno kreirana!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Osveži predloženo ime za sledeći put i listu istorije
                var dbNameNew = !string.IsNullOrEmpty(AppConfig.DbPath) ? Path.GetFileNameWithoutExtension(AppConfig.DbPath) : "plata";
                TxtPredlozenoIme.Text = $"{dbNameNew}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                OsveziIstorijuKopija();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri kreiranju rezervne kopije: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnVratiBackup_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Da li ste sigurni da želite da vratite bazu podataka iz rezervne kopije?\n\n" +
            "UPOZORENJE: Ova operacija će u potpunosti zameniti sve trenutne podatke u sistemu za aktivnu firmu! Pre prepisivanja, biće automatski napravljena sigurnosna kopija trenutnog stanja baze.",
            "Potvrda vraćanja baze",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Izaberite rezervnu kopiju baze podataka za vraćanje",
                Filter = "SQLite baza podataka (*.db)|*.db|Sve datoteke (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                IzvrsiVracanjeBaze(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri otvaranju dijaloga: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void IzvrsiVracanjeBaze(string putanjaKopije)
    {
        try
        {
            // 1. Zatvori aktivni DbContext
            _db.Dispose();

            // 2. Vrati backup baze (automatski radi ClearAllPools i kreira safety backup pre prepisivanja)
            if (Services.BackupService.Instance.VratiBackup(putanjaKopije, out var greska))
            {
                // 3. Ponovo inicijalizuj DbContext
                _db = PlataDbContext.Create(AppConfig.DbPath);

                StatusMessage.Text = "Podaci su uspešno vraćeni iz rezervne kopije!";
                MessageBox.Show("Baza podataka je uspešno vraćena iz rezervne kopije!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);

                // 4. Osveži istoriju kopija
                OsveziIstorijuKopija();

                // 5. Restartuj i osveži MainWindow podatke i prikaze
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.RestartujNakonPromeneBaze();
            }
            else
            {
                // Ponovo pokreni DbContext u slučaju neuspeha
                _db = PlataDbContext.Create(AppConfig.DbPath);
                MessageBox.Show($"Greška pri vraćanju podataka: {greska}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            try
            {
                _db = PlataDbContext.Create(AppConfig.DbPath);
            }
            catch { }

            MessageBox.Show($"Greška pri vraćanju podataka: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LstIstorijaKopija_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnVratiIzIstorije.IsEnabled = LstIstorijaKopija.SelectedItem != null;
    }

    private void BtnVratiIzIstorije_Click(object sender, RoutedEventArgs e)
    {
        if (LstIstorijaKopija.SelectedItem is not Services.BackupItem selektovanaKopija)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Da li ste sigurni da želite da vratite bazu podataka iz izabrane kopije?\n\n" +
            $"Kopija: {selektovanaKopija.NazivFajla} ({selektovanaKopija.DatumPrikaz})\n\n" +
            $"UPOZORENJE: Trenutni podaci aktivne firme biće zamenjeni! Pre nego što to uradimo, automatski ćemo sačuvati trenutno stanje.",
            "Potvrda brzog vraćanja",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            IzvrsiVracanjeBaze(selektovanaKopija.Putanja);
        }
    }

    private void BtnOtvoriFolderKopija_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Services.BackupService.Instance.BackupDir;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri otvaranju foldera: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ── Vrednosni Konverteri za Boje Tipa Rezervne Kopije ───────────────────

public class TipBackupBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string tip)
        {
            if (tip.Contains("Pre vraćanja"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")); // Crvenkasto
            if (tip.Contains("Automatski"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")); // Plavkasto
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")); // Sivo
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class TipBackupForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string tip)
        {
            if (tip.Contains("Pre vraćanja"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
            if (tip.Contains("Automatski"))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF"));
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

