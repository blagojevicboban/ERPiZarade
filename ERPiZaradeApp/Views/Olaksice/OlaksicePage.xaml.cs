using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Olaksice;

public partial class OlaksicePage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<PoreskaOlaksica> _olaksice = [];
    private ObservableCollection<OlaksicaMfp> _mfp = [];

    private readonly List<PoreskaOlaksica> _zaBrisanje = [];
    private readonly List<OlaksicaMfp> _mfpZaBrisanje = [];

    public OlaksicePage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        KolonaMehanizam.ItemsSource = Enum.GetValues<MehanizamOlaksice>();
        KolonaIzvor.ItemsSource = Enum.GetValues<IzvorMfp>();
        KolonaOznaka.ItemsSource = Enumerable.Range(1, 12).Select(i => $"MFP.{i}").ToList();

        Ucitaj();
    }

    private void Ucitaj()
    {
        _zaBrisanje.Clear();
        _mfpZaBrisanje.Clear();

        _olaksice = new ObservableCollection<PoreskaOlaksica>(
            _db.PoreskeOlaksice.Include(o => o.MfpDeklaracije).OrderBy(o => o.Sifra));

        GridOlaksice.ItemsSource = _olaksice;
        PrikaziMfp(null);

        StatusMessage.Text = $"{_olaksice.Count} olakšica u šifarniku.";
    }

    private void GridOlaksice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => PrikaziMfp(GridOlaksice.SelectedItem as PoreskaOlaksica);

    private void PrikaziMfp(PoreskaOlaksica? olaksica)
    {
        _mfp = olaksica == null
            ? []
            : new ObservableCollection<OlaksicaMfp>(olaksica.MfpDeklaracije.OrderBy(m => m.Oznaka));

        GridMfp.ItemsSource = _mfp;
        GridMfp.IsEnabled = olaksica != null;
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        var nova = new PoreskaOlaksica { Mehanizam = MehanizamOlaksice.Povracaj, Aktivna = true };
        _olaksice.Add(nova);
        GridOlaksice.SelectedItem = nova;
        GridOlaksice.ScrollIntoView(nova);
        StatusMessage.Text = "Unesite OL oznaku i naziv, pa pritisnite „Sačuvaj\".";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridOlaksice.SelectedItem is not PoreskaOlaksica izabrana)
        {
            StatusMessage.Text = "Izaberite olakšicu koju želite da obrišete.";
            return;
        }

        // Olakšica upotrebljena u obračunu se ne briše — bez nje se ne bi znalo po čemu je
        // umanjenje priznato.
        if (izabrana.PoreskaOlaksicaId != 0
            && _db.ObracuniPlata.Any(o => o.OlaksicaOznaka == izabrana.Sifra))
        {
            MessageBox.Show(
                $"„{izabrana.Naziv}\" je primenjena u postojećim obračunima i ne može se obrisati.\n\n" +
                "Isključite je poljem „Aktivna\" da se više ne primenjuje.",
                "Olakšica je u upotrebi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _olaksice.Remove(izabrana);
        if (izabrana.PoreskaOlaksicaId != 0) _zaBrisanje.Add(izabrana);

        PrikaziMfp(null);
        StatusMessage.Text = "Olakšica će biti obrisana po snimanju.";
    }

    private void BtnDodajMfp_Click(object sender, RoutedEventArgs e)
    {
        if (GridOlaksice.SelectedItem is not PoreskaOlaksica izabrana)
        {
            StatusMessage.Text = "Prvo izaberite olakšicu.";
            return;
        }

        var novo = new OlaksicaMfp
        {
            PoreskaOlaksicaId = izabrana.PoreskaOlaksicaId,
            Oznaka = "MFP.1",
            Izvor = IzvorMfp.UmanjenjePoreza
        };

        izabrana.MfpDeklaracije.Add(novo);
        _mfp.Add(novo);
        GridMfp.SelectedItem = novo;
    }

    private void BtnObrisiMfp_Click(object sender, RoutedEventArgs e)
    {
        if (GridOlaksice.SelectedItem is not PoreskaOlaksica izabrana) return;
        if (GridMfp.SelectedItem is not OlaksicaMfp izabranoPolje) return;

        izabrana.MfpDeklaracije.Remove(izabranoPolje);
        _mfp.Remove(izabranoPolje);
        if (izabranoPolje.OlaksicaMfpId != 0) _mfpZaBrisanje.Add(izabranoPolje);
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        GridOlaksice.CommitEdit(DataGridEditingUnit.Row, true);
        GridMfp.CommitEdit(DataGridEditingUnit.Row, true);

        if (_olaksice.Any(o => string.IsNullOrWhiteSpace(o.Sifra) || string.IsNullOrWhiteSpace(o.Naziv)))
        {
            MessageBox.Show("Svaka olakšica mora imati OL oznaku i naziv.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Oznaka mora biti dvocifrena — čita se sa tačnih pozicija SVP šifre.
        var pogresnaOznaka = _olaksice.FirstOrDefault(o =>
            o.Sifra.Trim().Length != 2 || !o.Sifra.Trim().All(char.IsDigit));

        if (pogresnaOznaka != null)
        {
            MessageBox.Show(
                $"OL oznaka „{pogresnaOznaka.Sifra}\" nije ispravna — mora imati tačno dve cifre, " +
                "jer se čita sa pozicija 7–8 SVP šifre.",
                "Neispravna oznaka", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var duplikat = _olaksice
            .GroupBy(o => o.Sifra.Trim())
            .FirstOrDefault(g => g.Count() > 1);

        if (duplikat != null)
        {
            MessageBox.Show($"OL oznaka „{duplikat.Key}\" je upotrebljena više puta.", "Dvostruka oznaka",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_zaBrisanje.Count > 0) _db.PoreskeOlaksice.RemoveRange(_zaBrisanje);
            if (_mfpZaBrisanje.Count > 0) _db.OlaksicaMfpDeklaracije.RemoveRange(_mfpZaBrisanje);

            foreach (var olaksica in _olaksice)
            {
                olaksica.Sifra = olaksica.Sifra.Trim();
                if (olaksica.PoreskaOlaksicaId == 0) _db.PoreskeOlaksice.Add(olaksica);
            }

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Šifarnik je sačuvan. Izmene važe od sledećeg obračuna.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Šifarnik nije sačuvan: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
