using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.RadniSati;

public partial class DodajRadnikaRadniSatiWindow : Window
{
    private readonly PlataDbContext _db;
    private readonly int _godina;
    private readonly int _mesec;
    private List<Radnik> _slobodniRadnici = new();

    public Radnik? SelectedRadnik { get; private set; }

    public DodajRadnikaRadniSatiWindow(int godina, int mesec)
    {
        InitializeComponent();
        Views.Pomoc.ContextHelpFix.UkloniDugmeZaPomoc(this);
        KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.F1) { new Views.Pomoc.EditHelpWindow("Dodavanje radnika", "Dodavanje radnika u radne sate", new[] { ("Enter / Dvaklik", "Dodaj selektovanog radnika"), ("Esc", "Zatvori prozor") }, "Izaberite slobodnog radnika sa liste.").ShowDialog(); e.Handled = true; } };
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _godina = godina;
        _mesec = mesec;

        string[] meseciStr = {
            "Januar", "Februar", "Mart", "April", "Maj", "Jun",
            "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar"
        };
        string periodNaziv = mesec >= 1 && mesec <= 12 ? $"{meseciStr[mesec - 1]} {godina}" : $"{mesec:D2}/{godina}";
        PeriodSubtitle.Text = $"Izbor radnika za period: {periodNaziv}";

        LoadRadnike();
    }

    private void LoadRadnike()
    {
        try
        {
            // Učitaj ID-eve radnika koji već imaju radne sate za ovaj period
            var vecDodatiRadnikIds = _db.RadniSati
                .Where(s => s.Godina == _godina && s.Mesec == _mesec)
                .Select(s => s.RadnikId)
                .ToList();

            // Učitaj sve radnike iz baze koji nisu već dodati
            var query = _db.Radnici.Where(r => !vecDodatiRadnikIds.Contains(r.Id));

            // Ako nije čekirano "Prikaži neaktivne", prikaži samo aktivne
            bool prikaziNeaktivne = CheckPrikaziNeaktivne.IsChecked ?? false;
            if (!prikaziNeaktivne)
            {
                query = query.Where(r => r.Aktivan);
            }

            _slobodniRadnici = query
                .OrderBy(r => r.BrojRadnika)
                .ToList();

            FilterList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom učitavanja radnika: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FilterList()
    {
        string filter = SearchBox.Text.Trim().ToLower();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        List<Radnik> filtrirani;
        if (string.IsNullOrEmpty(filter))
        {
            filtrirani = _slobodniRadnici;
        }
        else
        {
            filtrirani = _slobodniRadnici
                .Where(r => r.ImeIPrezime.ToLower().Contains(filter) || r.BrojRadnika.ToString().Contains(filter))
                .ToList();
        }

        GridSlobodniRadnici.ItemsSource = filtrirani;
        StatusMessage.Text = $"Pronađeno {filtrirani.Count} radnika na raspolaganju.";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterList();
    }

    private void CheckPrikaziNeaktivne_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            LoadRadnike();
        }
    }

    private void GridSlobodniRadnici_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        PotvrdiIzbor();
    }

    private void BtnOtkazi_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        PotvrdiIzbor();
    }

    private void PotvrdiIzbor()
    {
        if (GridSlobodniRadnici.SelectedItem is Radnik radnik)
        {
            SelectedRadnik = radnik;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Molimo izaberite radnika sa liste.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
