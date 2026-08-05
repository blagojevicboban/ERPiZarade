using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Primanja;

/// <summary>Radnik u padajućoj listi — broj uz ime, jer se imena ponavljaju.</summary>
public sealed class RadnikIzbor
{
    public required int Id { get; init; }
    public required string Prikaz { get; init; }
}

public partial class PrimanjaPage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<UnetoPrimanje> _primanja = [];
    private readonly List<UnetoPrimanje> _zaBrisanje = [];

    private readonly int _godina;
    private readonly int _mesec;

    public PrimanjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        _godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        _mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
        TxtPeriod.Text = $"Obračunski period {_mesec:D2}/{_godina}";

        Ucitaj();
    }

    private void Ucitaj()
    {
        KolonaRadnik.ItemsSource = _db.Radnici
            .Where(r => r.Godina == _godina && r.Mesec == _mesec && r.Aktivan)
            .OrderBy(r => r.BrojRadnika)
            .Select(r => new RadnikIzbor { Id = r.Id, Prikaz = r.BrojRadnika + " " + r.ImeIPrezime })
            .ToList();

        // Nude se samo aktivne vrste — isključena vrsta je namerno povučena iz upotrebe.
        KolonaVrsta.ItemsSource = _db.VrstePrimanja
            .Where(v => v.Aktivna)
            .OrderBy(v => v.Redosled)
            .ToList();

        _zaBrisanje.Clear();
        _primanja = new ObservableCollection<UnetoPrimanje>(
            _db.UnetaPrimanja.Where(p => p.Godina == _godina && p.Mesec == _mesec));

        GridPrimanja.ItemsSource = _primanja;
        StatusMessage.Text = $"{_primanja.Count} unetih primanja za {_mesec:D2}/{_godina}.";
    }

    private bool PeriodZakljucan()
    {
        if (!_db.ObracuniPlata.Any(o => o.Godina == _godina && o.Mesec == _mesec && o.Zakljucan))
            return false;

        MessageBox.Show("Obračunski period je ZAKLJUČAN. Izmena primanja nije dozvoljena.",
            "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
        return true;
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        if (PeriodZakljucan()) return;

        var novo = new UnetoPrimanje { Godina = _godina, Mesec = _mesec };
        _primanja.Add(novo);
        GridPrimanja.SelectedItem = novo;
        GridPrimanja.ScrollIntoView(novo);
        StatusMessage.Text = "Izaberite radnika i vrstu, unesite iznos, pa pritisnite „Sačuvaj\".";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (PeriodZakljucan()) return;

        if (GridPrimanja.SelectedItem is not UnetoPrimanje izabrano)
        {
            StatusMessage.Text = "Izaberite primanje koje želite da obrišete.";
            return;
        }

        _primanja.Remove(izabrano);
        if (izabrano.UnetoPrimanjeId != 0) _zaBrisanje.Add(izabrano);

        StatusMessage.Text = "Primanje će biti obrisano po snimanju.";
    }

    private void BtnUvozPutnihNaloga_Click(object sender, RoutedEventArgs e)
    {
        if (PeriodZakljucan()) return;

        var win = new UvozPutnihNalogaWindow(_db) { Owner = Window.GetWindow(this) };
        win.ShowDialog();

        if (win.Uvezeno)
        {
            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();
        }
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        if (PeriodZakljucan()) return;

        GridPrimanja.CommitEdit(DataGridEditingUnit.Row, true);

        if (_primanja.Any(p => p.RadnikId == 0 || p.VrstaPrimanjaId == 0))
        {
            MessageBox.Show("Svako primanje mora imati izabranog radnika i vrstu.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Isti radnik, ista vrsta i ista isplata su jedan iznos — dva reda bi značila da se
        // primanje isplaćuje dvaput, a limit bi se primenio na svaki posebno. IsplataId ulazi
        // u poređenje od Faze 3.2, jer dva reda sa različitom isplatom (npr. jedan uz
        // akontaciju, jedan uz konačnu zaradu) jesu legitimno dva različita unosa.
        var duplikat = _primanja
            .GroupBy(p => new { p.RadnikId, p.VrstaPrimanjaId, p.IsplataId })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplikat != null)
        {
            MessageBox.Show(
                "Isti radnik ima dva puta istu vrstu primanja. Spojte ih u jedan iznos — " +
                "inače bi se neoporezivi limit primenio na svaki red posebno.",
                "Dvostruki unos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_zaBrisanje.Count > 0) _db.UnetaPrimanja.RemoveRange(_zaBrisanje);

            foreach (var primanje in _primanja.Where(p => p.UnetoPrimanjeId == 0))
                _db.UnetaPrimanja.Add(primanje);

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Primanja su sačuvana. Pokrenite ponovni obračun da uđu u platu.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Primanja nisu sačuvana: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
