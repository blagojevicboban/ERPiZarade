using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PlataData;

namespace PlataApp.Views.Korisnici;

public partial class KorisniciPage : Page
{
    private readonly PlataDbContext _db;

    public KorisniciPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        UcitajKorisnike();
    }

    private void UcitajKorisnike()
    {
        var query = _db.Korisnici.AsQueryable();

        var pojam = TxtPretraga.Text.Trim().ToLower();
        if (!string.IsNullOrEmpty(pojam))
        {
            query = query.Where(k => k.ImePrezime.ToLower().Contains(pojam) ||
                                     k.KorisnickoIme.ToLower().Contains(pojam));
        }

        DgKorisnici.ItemsSource = query.OrderBy(k => k.ImePrezime).ToList();
    }

    private void TxtPretraga_TextChanged(object sender, TextChangedEventArgs e)
    {
        UcitajKorisnike();
    }

    private void BtnNoviKorisnik_Click(object sender, RoutedEventArgs e)
    {
        var window = new KorisnikWindow(_db, null);
        if (window.ShowDialog() == true)
        {
            UcitajKorisnike();
        }
    }

    private void BtnIzmeni_Click(object sender, RoutedEventArgs e)
    {
        var id = (int)((Button)sender).Tag;
        var korisnik = _db.Korisnici.Find(id);

        if (korisnik != null)
        {
            var window = new KorisnikWindow(_db, korisnik);
            if (window.ShowDialog() == true)
            {
                UcitajKorisnike();
            }
        }
    }

    private void BtnPonistiLozinku_Click(object sender, RoutedEventArgs e)
    {
        var id = (int)((Button)sender).Tag;
        var korisnik = _db.Korisnici.Find(id);

        if (korisnik != null)
        {
            if (MessageBox.Show($"Da li ste sigurni da želite da resetujete lozinku korisniku {korisnik.ImePrezime} na '123456'?",
                "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                korisnik.LozinkaHash = PlataDbContext.HashPassword("123456");
                _db.SaveChanges();
                MessageBox.Show("Lozinka je uspešno resetovana.", "Informacija", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void BtnDeaktiviraj_Click(object sender, RoutedEventArgs e)
    {
        var id = (int)((Button)sender).Tag;
        var korisnik = _db.Korisnici.Find(id);

        if (korisnik != null)
        {
            if (korisnik.Id == AppSession.TrenutniKorisnik?.Id)
            {
                MessageBox.Show("Ne možete deaktivirati sopstveni nalog.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            korisnik.JeAktivan = !korisnik.JeAktivan;
            _db.SaveChanges();
            UcitajKorisnike();
        }
    }
}
