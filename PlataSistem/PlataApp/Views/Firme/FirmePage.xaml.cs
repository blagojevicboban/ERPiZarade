using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Firme;

public partial class FirmePage : Page
{
    private List<FirmaGridItem> _allFirme = new();
    private ObservableCollection<FirmaGridItem> _displayedFirme = new();
    private FirmaGridItem? _selectedItem;
    private bool _isEditing = false;
    private bool _isNew = false;

    public FirmePage()
    {
        InitializeComponent();
        Loaded += FirmePage_Loaded;
    }

    private void FirmePage_Loaded(object sender, RoutedEventArgs e)
    {
        UcitajPodatke();
    }

    private void UcitajPodatke()
    {
        try
        {
            var bazeDir = AppConfig.BazeDir;
            Directory.CreateDirectory(bazeDir);

            // Skeniraj sve .db baze u folderu Baze
            var dbFiles = Directory.GetFiles(bazeDir, "*.db");
            var firmeList = new List<FirmaGridItem>();

            foreach (var file in dbFiles)
            {
                try
                {
                    using var fileDb = PlataDbContext.Create(file);
                    var f = fileDb.Firme.FirstOrDefault();
                    if (f == null)
                    {
                        f = new Firma
                        {
                            Naziv = Path.GetFileNameWithoutExtension(file),
                            Pib = "000000000"
                        };
                        fileDb.Firme.Add(f);
                        fileDb.SaveChanges();
                    }

                    firmeList.Add(new FirmaGridItem
                    {
                        Id = f.Id,
                        Naziv = f.Naziv,
                        Pib = f.Pib,
                        Mb = f.Mb,
                        Grad = f.Grad,
                        Telefon = f.Telefon,
                        DbPath = file,
                        OriginalFirma = f
                    });
                }
                catch { }
            }

            // Ako nema nijedne baze podataka, pokreni inicijalizaciju podrazumevane baze podataka
            if (!firmeList.Any())
            {
                // Ovo će inicijalizovati DefaultDbPath i migrirati ga u Baze folder
                var defaultPath = AppConfig.DbPath; 
                try
                {
                    using var fileDb = PlataDbContext.Create(defaultPath);
                    var f = fileDb.Firme.FirstOrDefault();
                    if (f == null)
                    {
                        f = new Firma
                        {
                            Naziv = "Zavod za poljoprivredu",
                            Grad = "Pirot",
                            Pib = "123456789",
                            Mb = "98765432"
                        };
                        fileDb.Firme.Add(f);
                        fileDb.SaveChanges();
                    }

                    firmeList.Add(new FirmaGridItem
                    {
                        Id = f.Id,
                        Naziv = f.Naziv,
                        Pib = f.Pib,
                        Mb = f.Mb,
                        Grad = f.Grad,
                        Telefon = f.Telefon,
                        DbPath = defaultPath,
                        OriginalFirma = f
                    });
                }
                catch { }
            }

            _allFirme = firmeList.OrderBy(f => f.Naziv).ToList();
            OsveziTabelu();
            ResetForme();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Greška pri učitavanju firmi: {ex.Message}";
        }
    }

    private void OsveziTabelu()
    {
        var filter = SearchBox.Text.Trim().ToLower();
        List<FirmaGridItem> filtered;

        if (string.IsNullOrWhiteSpace(filter))
        {
            filtered = _allFirme;
            SearchPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            filtered = _allFirme.Where(f => 
                f.Naziv.ToLower().Contains(filter) || 
                f.Pib.ToLower().Contains(filter) ||
                f.Grad.ToLower().Contains(filter)
            ).ToList();
            SearchPlaceholder.Visibility = Visibility.Collapsed;
        }

        _displayedFirme = new ObservableCollection<FirmaGridItem>(filtered);
        FirmeGrid.ItemsSource = _displayedFirme;

        // Ponovo selektuj element
        if (_selectedItem != null)
        {
            var match = _displayedFirme.FirstOrDefault(f => f.DbPath == _selectedItem.DbPath);
            if (match != null)
            {
                FirmeGrid.SelectedItem = match;
            }
            else
            {
                FirmeGrid.SelectedItem = null;
            }
        }
    }

    private void ResetForme()
    {
        _isEditing = false;
        _isNew = false;

        FormScrollViewer.Visibility = Visibility.Collapsed;
        NoSelectionPlaceholder.Visibility = Visibility.Visible;
        ActionButtonsPanel.Visibility = Visibility.Collapsed;
        FormFieldsPanel.IsEnabled = false;

        TxtNaziv.Clear();
        TxtPib.Clear();
        TxtMb.Clear();
        TxtAdresa.Clear();
        TxtGrad.Clear();
        TxtBankovniRacun.Clear();
        TxtSifraPlacanja.Clear();
        TxtTelefon.Clear();
        TxtEmail.Clear();
        TxtNapomena.Clear();

        AzurirajDugmadBara();
    }

    private void PopuniFormu(Firma f)
    {
        FormScrollViewer.Visibility = Visibility.Visible;
        NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
        ActionButtonsPanel.Visibility = Visibility.Collapsed;
        FormFieldsPanel.IsEnabled = false;

        FormHeaderTitle.Text = "Detalji firme";

        TxtNaziv.Text = f.Naziv;
        TxtPib.Text = f.Pib;
        TxtMb.Text = f.Mb;
        TxtAdresa.Text = f.Adresa;
        TxtGrad.Text = f.Grad;
        TxtBankovniRacun.Text = f.BankovniRacun;
        TxtSifraPlacanja.Text = f.SifraPlacanja;
        TxtTelefon.Text = f.Telefon;
        TxtEmail.Text = f.Email;
        TxtNapomena.Text = f.Napomena;

        _isEditing = false;
        AzurirajDugmadBara();
    }

    private void AzurirajDugmadBara()
    {
        bool hasSelection = _selectedItem != null;
        BtnIzmeni.IsEnabled = hasSelection && !_isEditing;
        BtnObrisi.IsEnabled = hasSelection && !_isEditing;
        BtnAktivna.IsEnabled = hasSelection && !_isEditing && !_selectedItem!.IsCurrentlyActive;
    }

    private void FirmeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isEditing)
        {
            if (FirmeGrid.SelectedItem != _selectedItem)
            {
                FirmeGrid.SelectedItem = _selectedItem;
                return;
            }
        }

        _selectedItem = FirmeGrid.SelectedItem as FirmaGridItem;

        if (_selectedItem != null)
        {
            PopuniFormu(_selectedItem.OriginalFirma);
        }
        else
        {
            ResetForme();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OsveziTabelu();
    }

    private void BtnNovaFirma_Click(object sender, RoutedEventArgs e)
    {
        FirmeGrid.SelectedItem = null;
        _selectedItem = null;

        _isNew = true;
        _isEditing = true;

        FormScrollViewer.Visibility = Visibility.Visible;
        NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
        FormFieldsPanel.IsEnabled = true;
        ActionButtonsPanel.Visibility = Visibility.Visible;

        FormHeaderTitle.Text = "Unos nove firme";

        TxtNaziv.Clear();
        TxtPib.Clear();
        TxtMb.Clear();
        TxtAdresa.Clear();
        TxtGrad.Clear();
        TxtBankovniRacun.Clear();
        TxtSifraPlacanja.Clear();
        TxtTelefon.Clear();
        TxtEmail.Clear();
        TxtNapomena.Clear();

        AzurirajDugmadBara();
        TxtNaziv.Focus();
    }

    private void BtnIzmeni_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        _isNew = false;
        _isEditing = true;

        FormFieldsPanel.IsEnabled = true;
        ActionButtonsPanel.Visibility = Visibility.Visible;
        FormHeaderTitle.Text = "Izmena podataka firme";

        AzurirajDugmadBara();
        TxtNaziv.Focus();
    }

    private void BtnOtkazi_Click(object sender, RoutedEventArgs e)
    {
        if (_isNew)
        {
            ResetForme();
        }
        else if (_selectedItem != null)
        {
            PopuniFormu(_selectedItem.OriginalFirma);
        }
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNaziv.Text))
        {
            MessageBox.Show("Naziv firme je obavezno polje!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNaziv.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtPib.Text))
        {
            MessageBox.Show("PIB je obavezno polje!", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPib.Focus();
            return;
        }

        try
        {
            Firma f;
            string dbPathToSave = "";

            if (_isNew)
            {
                var pib = TxtPib.Text.Trim();
                var nazivClean = string.Concat(TxtNaziv.Text.Trim().Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                var fileName = $"firma_{pib}_{nazivClean}.db";
                var bazeDir = AppConfig.BazeDir;
                dbPathToSave = Path.Combine(bazeDir, fileName);

                // Ovo automatski kreira datoteku i sve tabele unutar nje
                using var newDb = PlataDbContext.Create(dbPathToSave);
                f = new Firma
                {
                    Naziv = TxtNaziv.Text.Trim(),
                    Pib = TxtPib.Text.Trim(),
                    Mb = TxtMb.Text.Trim(),
                    Adresa = TxtAdresa.Text.Trim(),
                    Grad = TxtGrad.Text.Trim(),
                    BankovniRacun = TxtBankovniRacun.Text.Trim(),
                    SifraPlacanja = TxtSifraPlacanja.Text.Trim(),
                    Telefon = TxtTelefon.Text.Trim(),
                    Email = TxtEmail.Text.Trim(),
                    Napomena = TxtNapomena.Text.Trim()
                };
                newDb.Firme.Add(f);
                newDb.SaveChanges();
            }
            else
            {
                if (_selectedItem == null) return;
                dbPathToSave = _selectedItem.DbPath;

                using var editDb = PlataDbContext.Create(dbPathToSave);
                f = editDb.Firme.FirstOrDefault() ?? new Firma();

                f.Naziv = TxtNaziv.Text.Trim();
                f.Pib = TxtPib.Text.Trim();
                f.Mb = TxtMb.Text.Trim();
                f.Adresa = TxtAdresa.Text.Trim();
                f.Grad = TxtGrad.Text.Trim();
                f.BankovniRacun = TxtBankovniRacun.Text.Trim();
                f.SifraPlacanja = TxtSifraPlacanja.Text.Trim();
                f.Telefon = TxtTelefon.Text.Trim();
                f.Email = TxtEmail.Text.Trim();
                f.Napomena = TxtNapomena.Text.Trim();

                editDb.Entry(f).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                editDb.SaveChanges();
            }

            // Ako je ovo bila jedina firma ili ako smo je upravo kreirali, a nemamo aktivnu, postavi je kao aktivnu
            if (string.IsNullOrEmpty(UserSettings.Instance.ActiveDbPath) || AppConfig.DbPath == dbPathToSave)
            {
                AppConfig.DbPath = dbPathToSave;
                var mainWin = Application.Current.MainWindow as MainWindow;
                mainWin?.UcitajImeFirme();
            }

            TxtStatus.Text = "Firma je uspešno sačuvana!";
            UcitajPodatke();

            // Izaberi sačuvani element
            var toSelect = _displayedFirme.FirstOrDefault(x => x.DbPath == dbPathToSave);
            if (toSelect != null)
            {
                FirmeGrid.SelectedItem = toSelect;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju firme: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        if (_allFirme.Count <= 1)
        {
            MessageBox.Show("Nije moguće obrisati jedinu firmu u bazi podataka!", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show(
            $"Da li ste sigurni da želite da obrišete firmu:\n\n{_selectedItem.Naziv} (PIB: {_selectedItem.Pib})?\n\nPAŽNJA: Ova akcija će trajno izbrisati bazu podataka (radnike, obračune i sate) za ovu firmu!",
            "Potvrda brisanja",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var isDeletedActive = _selectedItem.IsCurrentlyActive;
            var pathToDelete = _selectedItem.DbPath;

            // Zatvori konekcije da bismo skinuli lockove sa SQLite datoteke
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Ako je obrisana firma bila aktivna, prebaci na prvu sledeću pre brisanja
            if (isDeletedActive)
            {
                var prvaSledeca = _allFirme.FirstOrDefault(f => f.DbPath != pathToDelete);
                if (prvaSledeca != null)
                {
                    AppConfig.DbPath = prvaSledeca.DbPath;
                }
            }

            // Obriši fajl sa diska
            if (File.Exists(pathToDelete))
            {
                File.Delete(pathToDelete);
            }

            TxtStatus.Text = "Firma je uspešno obrisana!";
            UcitajPodatke();

            // Ako je obrisana aktivna firma, uradi kompletan reload
            if (isDeletedActive)
            {
                var mainWin = Application.Current.MainWindow as MainWindow;
                mainWin?.RestartujNakonPromeneBaze();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri brisanju firme: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAktivna_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        try
        {
            // 1. Postavi aktivnu putanju baze podataka
            AppConfig.DbPath = _selectedItem.DbPath;
            TxtStatus.Text = $"Firma '{_selectedItem.Naziv}' je postavljena kao aktivna!";

            // 2. Osveži tabelu i dugmad
            FirmeGrid.Items.Refresh();
            AzurirajDugmadBara();

            // 3. Proveri da li ima unetih radnika u izabranoj bazi podataka
            bool imaRadnika = false;
            try
            {
                using var db = PlataDbContext.Create(_selectedItem.DbPath);
                imaRadnika = db.Radnici.Any();
            }
            catch { }

            var mainWin = Application.Current.MainWindow as MainWindow;

            if (!imaRadnika)
            {
                // Restartuj osnovne podatke o firmi, ali nemoj otvarati obračune nego radnike
                mainWin?.UcitajImeFirme();
                mainWin?.InicijalizujAktivniPeriod();

                MessageBox.Show(
                    $"Firma '{_selectedItem.Naziv}' nema unetih radnika!\n\n" +
                    "Sistem će vas automatski preusmeriti u meni 'Radnici' kako biste uneli zaposlene za ovu firmu.",
                    "Firma nema radnika",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                mainWin?.OtvoriRadnike();
            }
            else
            {
                // Standardni kompletan restart (učitava ime, period i otvara obračune)
                mainWin?.RestartujNakonPromeneBaze();
                MessageBox.Show($"Firma '{_selectedItem.Naziv}' je uspešno postavljena kao aktivna za rad u sistemu!", "Aktivna firma promenjena", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri postavljanju aktivne firme: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public class FirmaGridItem
{
    public int Id { get; set; }
    public string Naziv { get; set; } = "";
    public string Pib { get; set; } = "";
    public string Mb { get; set; } = "";
    public string Grad { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string DbPath { get; set; } = "";
    public bool IsCurrentlyActive => DbPath == AppConfig.DbPath;
    public Firma OriginalFirma { get; set; } = null!;
}
