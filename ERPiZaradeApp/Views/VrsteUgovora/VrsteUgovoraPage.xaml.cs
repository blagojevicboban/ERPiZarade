using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.VrsteUgovora;

/// <summary>
/// Šifarnik vrsta ugovora van radnog odnosa (Faza 2.3).
///
/// Isti obrazac kao ekrani vrsta primanja i poreskih olakšica: sve što propis menja unosi se
/// ovde, pa izmena stopa ne traži novu verziju programa.
/// </summary>
public partial class VrsteUgovoraPage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<VrstaUgovora> _vrste = [];
    private readonly List<VrstaUgovora> _zaBrisanje = [];

    public VrsteUgovoraPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        Ucitaj();
    }

    private void Ucitaj()
    {
        _zaBrisanje.Clear();
        _vrste = new ObservableCollection<VrstaUgovora>(
            _db.VrsteUgovora.OrderBy(v => v.Redosled).ThenBy(v => v.Sifra));

        GridVrste.ItemsSource = _vrste;

        int bezOvp = _vrste.Count(v => v.Aktivna && string.IsNullOrWhiteSpace(v.Ovp));

        StatusMessage.Text = bezOvp == 0
            ? $"{_vrste.Count} vrsta ugovora."
            : $"{_vrste.Count} vrsta ugovora; {bezOvp} aktivnih nema OVP oznaku — obračun po njima " +
              "prolazi, ali prijava bez šifre vrste prihoda biva odbijena.";
    }

    private void GridVrste_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        NapomenaVrste.Text = GridVrste.SelectedItem is VrstaUgovora vrsta ? vrsta.Napomena : "";
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        var nova = new VrstaUgovora
        {
            Sifra = "",
            Naziv = "",
            Aktivna = true,
            Redosled = (_vrste.Count == 0 ? 0 : _vrste.Max(v => v.Redosled)) + 10
        };

        _vrste.Add(nova);
        GridVrste.SelectedItem = nova;
        GridVrste.ScrollIntoView(nova);
        StatusMessage.Text = "Unesite šifru, naziv i stope, pa pritisnite „Sačuvaj\".";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridVrste.SelectedItem is not VrstaUgovora izabrana)
        {
            StatusMessage.Text = "Izaberite vrstu koju želite da obrišete.";
            return;
        }

        // Sa vrstom bi nestali normirani troškovi i stope po kojima je naknada obračunata
        // i prijavljena — a to je ono što se pri kontroli traži.
        if (izabrana.VrstaUgovoraId != 0 && _db.Ugovori.Any(u => u.VrstaUgovoraId == izabrana.VrstaUgovoraId))
        {
            MessageBox.Show(
                $"„{izabrana.Naziv}\" je upotrebljena u zaključenim ugovorima i ne može se obrisati.\n\n" +
                "Isključite je poljem „Aktivna\" da se više ne nudi.",
                "Vrsta je u upotrebi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _vrste.Remove(izabrana);
        if (izabrana.VrstaUgovoraId != 0) _zaBrisanje.Add(izabrana);

        StatusMessage.Text = $"„{izabrana.Naziv}\" će biti obrisana po snimanju.";
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        GridVrste.CommitEdit(DataGridEditingUnit.Row, true);

        if (_vrste.Any(v => string.IsNullOrWhiteSpace(v.Sifra) || string.IsNullOrWhiteSpace(v.Naziv)))
        {
            MessageBox.Show("Svaka vrsta mora imati šifru i naziv.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var duplikat = _vrste
            .GroupBy(v => v.Sifra.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplikat != null)
        {
            MessageBox.Show($"Šifra „{duplikat.Key}\" je upotrebljena više puta.", "Dvostruka šifra",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // OVP koji nije tri cifre ne daje ispravnu šifru vrste prihoda; prazno je dozvoljeno
        // i znači „još nije potvrđen iz Kataloga".
        var neispravanOvp = _vrste.FirstOrDefault(v =>
            !string.IsNullOrWhiteSpace(v.Ovp)
            && (v.Ovp.Trim().Length != 3 || !v.Ovp.Trim().All(char.IsDigit)));

        if (neispravanOvp != null)
        {
            MessageBox.Show(
                $"OVP kod „{neispravanOvp.Naziv}\" nije ispravan — mora imati tačno tri cifre ili biti prazan.",
                "Neispravan OVP", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Normirani troškovi od 100% i više ostavljaju osnovicu nula ili negativnu — porez bi
        // ispao nula, a preračun neta u bruto nemoguć.
        var neispravniTroskovi = _vrste.FirstOrDefault(v =>
            v.NormiraniTroskoviProcenat < 0m || v.NormiraniTroskoviProcenat >= 100m);

        if (neispravniTroskovi != null)
        {
            MessageBox.Show(
                $"Normirani troškovi kod „{neispravniTroskovi.Naziv}\" moraju biti između 0 i 100 procenata.",
                "Neispravni normirani troškovi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_zaBrisanje.Count > 0) _db.VrsteUgovora.RemoveRange(_zaBrisanje);

            foreach (var vrsta in _vrste)
            {
                vrsta.Sifra = vrsta.Sifra.Trim();
                vrsta.Ovp = (vrsta.Ovp ?? "").Trim();

                if (vrsta.VrstaUgovoraId == 0) _db.VrsteUgovora.Add(vrsta);
            }

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Šifarnik je sačuvan. Izmene važe od sledećeg obračuna naknade.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Šifarnik nije sačuvan: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
