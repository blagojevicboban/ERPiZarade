using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Podesavanja;

public partial class PodesavanjaPage : Page
{
    private PlataDbContext _db;
    private Firma? _currentFirma;

    public PodesavanjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        UcitajFirmaPodatke();

        // Inicijalizacija putanja za rezervnu kopiju
        try
        {
            TxtAktivnaBazaPath.Text = AppConfig.DbPath;
            TxtPredlozenoIme.Text = $"plata_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        }
        catch { }
    }

    private void UcitajFirmaPodatke()
    {
        try
        {
            _currentFirma = _db.Firme.FirstOrDefault();
            if (_currentFirma == null)
            {
                _currentFirma = new Firma();
            }
            PopuniFirmaFormu();
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju podataka o firmi: {ex.Message}";
        }
    }

    private void PopuniFirmaFormu()
    {
        if (_currentFirma == null) return;

        TxtFirmaNaziv.Text = _currentFirma.Naziv;
        TxtFirmaAdresa.Text = _currentFirma.Adresa;
        TxtFirmaGrad.Text = _currentFirma.Grad;
        TxtFirmaPib.Text = _currentFirma.Pib;
        TxtFirmaMb.Text = _currentFirma.Mb;
        TxtFirmaBankovniRacun.Text = _currentFirma.BankovniRacun;
        TxtFirmaSifraPlacanja.Text = _currentFirma.SifraPlacanja;
        TxtFirmaTelefon.Text = _currentFirma.Telefon;
        TxtFirmaEmail.Text = _currentFirma.Email;
        TxtFirmaNapomena.Text = _currentFirma.Napomena;
    }

    private void BtnSacuvajFirma_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFirma == null) return;

        try
        {
            _currentFirma.Naziv = TxtFirmaNaziv.Text.Trim();
            _currentFirma.Adresa = TxtFirmaAdresa.Text.Trim();
            _currentFirma.Grad = TxtFirmaGrad.Text.Trim();
            _currentFirma.Pib = TxtFirmaPib.Text.Trim();
            _currentFirma.Mb = TxtFirmaMb.Text.Trim();
            _currentFirma.BankovniRacun = TxtFirmaBankovniRacun.Text.Trim();
            _currentFirma.SifraPlacanja = TxtFirmaSifraPlacanja.Text.Trim();
            _currentFirma.Telefon = TxtFirmaTelefon.Text.Trim();
            _currentFirma.Email = TxtFirmaEmail.Text.Trim();
            _currentFirma.Napomena = TxtFirmaNapomena.Text.Trim();

            if (_currentFirma.Id == 0)
            {
                _db.Firme.Add(_currentFirma);
            }
            else
            {
                _db.Entry(_currentFirma).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            _db.SaveChanges();

            // Osveži ime firme u sidebar-u glavnog prozora
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.UcitajImeFirme();

            StatusMessage.Text = "Podaci o firmi su uspešno sačuvani!";
            MessageBox.Show("Podaci o firmi su uspešno sačuvani!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju podataka o firmi: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var dialog = new SaveFileDialog
            {
                Title = "Sačuvaj rezervnu kopiju baze podataka",
                Filter = "SQLite baza podataka (*.db)|*.db|Sve datoteke (*.*)|*.*",
                FileName = $"plata_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                DefaultExt = ".db"
            };

            if (dialog.ShowDialog() == true)
            {
                File.Copy(dbPath, dialog.FileName, true);
                StatusMessage.Text = $"Rezervna kopija je uspešno sačuvana na: {dialog.FileName}";
                MessageBox.Show($"Rezervna kopija baze podataka je uspešno kreirana!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Osveži predloženo ime za sledeći put
                TxtPredlozenoIme.Text = $"plata_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
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
            "UPOZORENJE: Ova operacija će u potpunosti zameniti sve trenutne podatke u sistemu! Preporučuje se da prvo napravite rezervnu kopiju trenutnog stanja baze.",
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
                var sourceFile = dialog.FileName;
                var destFile = AppConfig.DbPath;

                // 1. Zatvori aktivni DbContext
                _db.Dispose();

                // 2. Oslobodi sve SQLite konekcije iz poola kako bismo skinuli lockove sa datoteke
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                // 3. Kopiraj fajl
                File.Copy(sourceFile, destFile, true);

                // 4. Ponovo inicijalizuj DbContext
                _db = PlataDbContext.Create(destFile);

                // 5. Osveži sve podatke na ekranu
                UcitajFirmaPodatke();

                // 6. Osveži ime firme u glavnom prozoru
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.UcitajImeFirme();

                StatusMessage.Text = "Podaci su uspešno vraćeni iz rezervne kopije!";
                MessageBox.Show("Baza podataka je uspešno vraćena iz rezervne kopije!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
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
}
