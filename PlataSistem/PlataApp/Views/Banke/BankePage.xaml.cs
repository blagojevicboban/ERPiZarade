using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Banke;

public partial class BankePage : Page
{
    private PlataDbContext _db;
    private List<Banka> _sveBanke = [];
    private Banka? _selectedBanka;

    public BankePage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        UcitajPodatke();
    }

    private void UcitajPodatke()
    {
        try
        {
            PlataApp.Views.Radnici.BankCodeToNameConverter.ClearCache();
            
            int? aktivnaGodina = AppConfig.ActiveGodina;
            int? aktivniMesec = AppConfig.ActiveMesec;

            var query = _db.Banke.AsQueryable();
            if (aktivnaGodina.HasValue && aktivniMesec.HasValue)
            {
                query = query.Where(b => b.Godina == aktivnaGodina.Value && b.Mesec == aktivniMesec.Value);
            }

            _sveBanke = query
                .OrderBy(b => b.Sifra)
                .ToList();

            OsveziTabelu();

            if (aktivnaGodina.HasValue && aktivniMesec.HasValue)
            {
                StatusMessage.Text = $"Učitano {_sveBanke.Count} zapisa o bankama za aktivni obračun {aktivniMesec.Value:D2}/{aktivnaGodina.Value}.";
            }
            else
            {
                StatusMessage.Text = $"Učitano {_sveBanke.Count} zapisa o bankama.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju podataka: {ex.Message}";
        }
    }

    private void OsveziTabelu(string filter = "")
    {
        var prikazano = _sveBanke.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim().ToLower();
            prikazano = prikazano.Where(b =>
                b.Naziv.ToLower().Contains(filter) ||
                b.Sifra.ToLower().Contains(filter) ||
                b.ZiroRacun.ToLower().Contains(filter) ||
                b.Godina.ToString().Contains(filter) ||
                b.Mesec.ToString().Contains(filter)
            );
        }

        GridBanke.ItemsSource = prikazano.ToList();
    }

    private void TxtPretraga_TextChanged(object sender, TextChangedEventArgs e)
    {
        OsveziTabelu(TxtPretraga.Text);
    }

    private void GridBanke_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridBanke.SelectedItem is Banka selektovana)
        {
            _selectedBanka = selektovana;
            FormTitle.Text = "📝 Uredi podatke o banci";
            TxtSifra.Text = _selectedBanka.Sifra;
            TxtNaziv.Text = _selectedBanka.Naziv;
            TxtZiroRacun.Text = _selectedBanka.ZiroRacun;
            BtnObrisi.IsEnabled = true;
        }
        else
        {
            OcistiFormu();
        }
    }

    private void OcistiFormu()
    {
        _selectedBanka = null;
        FormTitle.Text = "➕ Dodaj novu banku";
        
        TxtSifra.Text = "";
        TxtNaziv.Text = "";
        TxtZiroRacun.Text = "";
        BtnObrisi.IsEnabled = false;
    }

    private void BtnNovaBanka_Click(object sender, RoutedEventArgs e)
    {
        GridBanke.SelectedItem = null;
        OcistiFormu();
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

        string sifra = TxtSifra.Text.Trim();
        if (string.IsNullOrWhiteSpace(sifra))
        {
            MessageBox.Show("Molimo unesite šifru banke.", "Validacija", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string naziv = TxtNaziv.Text.Trim();
        if (string.IsNullOrWhiteSpace(naziv))
        {
            MessageBox.Show("Molimo unesite naziv banke.", "Validacija", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string ziro = TxtZiroRacun.Text.Trim();

        try
        {
            if (_selectedBanka == null)
            {
                // Dodavanje novog zapisa
                // Provera duplikata
                var duplikat = _db.Banke.Any(b => b.Godina == godina && b.Mesec == mesec && b.Sifra == sifra);
                if (duplikat)
                {
                    MessageBox.Show($"Banka sa šifrom '{sifra}' već postoji za period {mesec:D2}/{godina}.", "Duplikat", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var novaBanka = new Banka
                {
                    Godina = godina,
                    Mesec = mesec,
                    Sifra = sifra,
                    Naziv = naziv,
                    ZiroRacun = ziro
                };

                _db.Banke.Add(novaBanka);
                _db.SaveChanges();

                MessageBox.Show("Nova banka je uspešno dodata u šifrarnik!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Izmena postojećeg zapisa
                _selectedBanka.Sifra = sifra;
                _selectedBanka.Naziv = naziv;
                _selectedBanka.ZiroRacun = ziro;

                _db.Entry(_selectedBanka).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                _db.SaveChanges();

                MessageBox.Show("Izmene na banci su uspešno sačuvane!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            UcitajPodatke();
            OcistiFormu();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju podataka: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnObrasi_Click(object sender, RoutedEventArgs e)
    {
        // Replaced by BtnObrisi_Click, just mapping to prevent compile error if old button had other name
        BtnObrisi_Click(sender, e);
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBanka == null) return;

        var rezultat = MessageBox.Show($"Da li ste sigurni da želite da obrišete banku '{_selectedBanka.Naziv}' za period {_selectedBanka.Mesec:D2}/{_selectedBanka.Godina}?", 
            "Potvrda brisanja", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (rezultat == MessageBoxResult.Yes)
        {
            try
            {
                _db.Banke.Remove(_selectedBanka);
                _db.SaveChanges();

                MessageBox.Show("Banka je uspešno obrisana iz šifrarnika.", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
                UcitajPodatke();
                OcistiFormu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri brisanju zapisa: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
