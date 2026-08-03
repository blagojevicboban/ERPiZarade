using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.SabloniUgovora;

/// <summary>
/// Šifarnik šablona ugovora van radnog odnosa (Faza 2.3).
///
/// Tekst ugovora je dokument, a ne podatak o novcu: propis mu određuje obavezne elemente,
/// formulacije bira firma. Zato se uređuje ovde — nacrt novog Zakona o autorskom i srodnim
/// pravima je u javnoj raspravi, pa se formulacije oko ustupanja prava mogu menjati bez
/// nove verzije programa.
/// </summary>
public partial class SabloniUgovoraPage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<SablonUgovora> _sabloni = [];
    private readonly List<SablonUgovora> _zaBrisanje = [];
    private SablonUgovora? _izabrani;

    public SabloniUgovoraPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        GridPolja.ItemsSource = UgovorTekstService.Polja
            .Select(p => new { p.Polje, p.Opis })
            .ToList();

        Ucitaj();
    }

    private void Ucitaj()
    {
        _zaBrisanje.Clear();
        _sabloni = new ObservableCollection<SablonUgovora>(
            _db.SabloniUgovora.OrderBy(s => s.Redosled).ThenBy(s => s.Sifra));

        GridSabloni.ItemsSource = _sabloni;
        GridSabloni.SelectedItem = _sabloni.FirstOrDefault();

        StatusMessage.Text = $"{_sabloni.Count} šablona.";
    }

    private void GridSabloni_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Tekst se u model upisuje pri promeni reda; bez toga bi prelazak na drugi šablon
        // izgubio ono što je upravo otkucano.
        UpisiTekstUIzabrani();

        _izabrani = GridSabloni.SelectedItem as SablonUgovora;
        TxtTekst.Text = _izabrani?.Tekst ?? "";

        if (_izabrani != null && !string.IsNullOrWhiteSpace(_izabrani.Napomena))
            StatusMessage.Text = _izabrani.Napomena;
    }

    private void TxtTekst_TextChanged(object sender, TextChangedEventArgs e) => UpisiTekstUIzabrani();

    private void UpisiTekstUIzabrani()
    {
        if (_izabrani != null && GridSabloni.SelectedItem == _izabrani)
            _izabrani.Tekst = TxtTekst.Text;
    }

    private void GridPolja_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridPolja.SelectedItem == null) return;

        string polje = GridPolja.SelectedItem.GetType().GetProperty("Polje")?
            .GetValue(GridPolja.SelectedItem)?.ToString() ?? "";

        if (polje.Length == 0) return;

        int mesto = TxtTekst.CaretIndex;
        TxtTekst.Text = TxtTekst.Text.Insert(mesto, polje);
        TxtTekst.CaretIndex = mesto + polje.Length;
        TxtTekst.Focus();
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        var novi = new SablonUgovora
        {
            Sifra = "",
            Naziv = "",
            Aktivan = true,
            Redosled = (_sabloni.Count == 0 ? 0 : _sabloni.Max(s => s.Redosled)) + 10
        };

        _sabloni.Add(novi);
        GridSabloni.SelectedItem = novi;
        GridSabloni.ScrollIntoView(novi);
        StatusMessage.Text = "Unesite šifru i naziv, pa otkucajte tekst.";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridSabloni.SelectedItem is not SablonUgovora izabrani)
        {
            StatusMessage.Text = "Izaberite šablon koji želite da obrišete.";
            return;
        }

        // Sistemski šablon se ne briše: seed bi ga pri sledećem pokretanju vratio, pa bi
        // brisanje izgledalo kao da nije upamćeno. Isključivanje je ono što traje.
        if (izabrani.JeSistemski)
        {
            MessageBox.Show(
                $"„{izabrani.Naziv}\" je šablon isporučen uz program i ne briše se — nadogradnja bi ga vratila.\n\n" +
                "Tekst mu slobodno menjajte; ako vam ne treba, isključite ga poljem „Aktivan\".",
                "Sistemski šablon", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _sabloni.Remove(izabrani);
        if (izabrani.SablonUgovoraId != 0) _zaBrisanje.Add(izabrani);

        _izabrani = null;
        TxtTekst.Text = "";
        StatusMessage.Text = $"„{izabrani.Naziv}\" će biti obrisan po snimanju.";
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        GridSabloni.CommitEdit(DataGridEditingUnit.Row, true);
        UpisiTekstUIzabrani();

        if (_sabloni.Any(s => string.IsNullOrWhiteSpace(s.Sifra) || string.IsNullOrWhiteSpace(s.Naziv)))
        {
            MessageBox.Show("Svaki šablon mora imati šifru i naziv.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var duplikat = _sabloni
            .GroupBy(s => s.Sifra.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplikat != null)
        {
            MessageBox.Show($"Šifra „{duplikat.Key}\" je upotrebljena više puta.", "Dvostruka šifra",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_zaBrisanje.Count > 0) _db.SabloniUgovora.RemoveRange(_zaBrisanje);

            foreach (var sablon in _sabloni)
            {
                sablon.Sifra = sablon.Sifra.Trim();
                if (sablon.SablonUgovoraId == 0) _db.SabloniUgovora.Add(sablon);
            }

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Šabloni su sačuvani. Već sačuvani tekstovi zaključenih ugovora ostaju nepromenjeni.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Šabloni nisu sačuvani: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
