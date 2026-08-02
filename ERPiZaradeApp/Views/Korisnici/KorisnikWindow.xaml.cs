using System;
using System.Windows;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Korisnici;

public partial class KorisnikWindow : Window
{
    private readonly PlataDbContext _db;
    private readonly Korisnik? _korisnik;

    public KorisnikWindow(PlataDbContext db, Korisnik? korisnik)
    {
        InitializeComponent();
        Views.Pomoc.ContextHelpFix.UkloniDugmeZaPomoc(this);
        KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.F1) { new Views.Pomoc.EditHelpWindow("Korisnički nalozi", "Upravljanje korisnicima i ulogama", new[] { ("Enter", "Sačuvaj korisnika"), ("Esc", "Zatvori prozor") }, "Izaberite ulogu (Administrator / Operater). Početna lozinka je 123456.").ShowDialog(); e.Handled = true; } };
        _db = db;
        _korisnik = korisnik;

        CmbUloga.ItemsSource = Enum.GetValues(typeof(UlogaKorisnika));

        if (_korisnik != null)
        {
            Title = "Izmena korisnika";
            TxtImePrezime.Text = _korisnik.ImePrezime;
            TxtKorisnickoIme.Text = _korisnik.KorisnickoIme;
            CmbUloga.SelectedItem = _korisnik.Uloga;
            PanelNovaLozinka.Visibility = Visibility.Visible;
        }
        else
        {
            Title = "Novi korisnik";
            CmbUloga.SelectedItem = UlogaKorisnika.Operater;
        }
    }

    private void BtnOtkazi_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        var ime = TxtImePrezime.Text.Trim();
        var korisnickoIme = TxtKorisnickoIme.Text.Trim();
        var uloga = (UlogaKorisnika)CmbUloga.SelectedItem;

        if (string.IsNullOrEmpty(ime) || string.IsNullOrEmpty(korisnickoIme))
        {
            MessageBox.Show("Ime i prezime i korisničko ime su obavezni.", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_korisnik == null)
        {
            var noviKorisnik = new Korisnik
            {
                ImePrezime = ime,
                KorisnickoIme = korisnickoIme,
                Uloga = uloga,
                LozinkaHash = PlataDbContext.HashPassword("123456"),
                JeAktivan = true
            };
            _db.Korisnici.Add(noviKorisnik);
            MessageBox.Show("Novi korisnik je kreiran. Njegova početna lozinka je: 123456\nKorisnik bi trebalo da je promeni.", "Informacija", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            _korisnik.ImePrezime = ime;
            _korisnik.KorisnickoIme = korisnickoIme;
            _korisnik.Uloga = uloga;

            if (!string.IsNullOrEmpty(TxtNovaLozinka.Password))
            {
                _korisnik.LozinkaHash = PlataDbContext.HashPassword(TxtNovaLozinka.Password);
            }
        }

        _db.SaveChanges();
        DialogResult = true;
        Close();
    }
}
