using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Isplate;

/// <summary>
/// Isplate unutar obračunskog meseca (Faza 2.2).
///
/// Ekran postoji zbog jedne stvari koja se ranije nije mogla zapisati: da mesec ima više
/// isplata. Sve dok ih ima jednu, ovde se vidi tačno ono što je i pre bilo — jedan red.
/// </summary>
public partial class IsplatePage : Page
{
    private PlataDbContext _db;
    private IsplataService _servis;
    private ObservableCollection<IsplataRed> _redovi = [];

    public IsplatePage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _servis = new IsplataService(_db);

        ComboVrsta.ItemsSource = new List<VrstaIsplateStavka>
        {
            new() { Vrsta = VrstaIsplate.Akontacija },
            new() { Vrsta = VrstaIsplate.Bonus },
            new() { Vrsta = VrstaIsplate.TrinaestaPlata },
            new() { Vrsta = VrstaIsplate.KonacnaZarada },
            new() { Vrsta = VrstaIsplate.Ostalo }
        };
        ComboVrsta.SelectedIndex = 0;

        PopuniPeriod();
        Ucitaj();
    }

    private void PopuniPeriod()
    {
        var godine = _db.ObracuniPlata
            .Select(o => o.Godina)
            .Distinct()
            .OrderByDescending(g => g)
            .ToList();

        int tekuca = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        if (!godine.Contains(tekuca)) godine.Insert(0, tekuca);

        ComboGodina.ItemsSource = godine;
        ComboMesec.ItemsSource = Enumerable.Range(1, 12).ToList();

        ComboGodina.SelectedItem = tekuca;
        ComboMesec.SelectedItem = AppConfig.ActiveMesec ?? DateTime.Now.Month;
    }

    private int Godina => ComboGodina.SelectedItem is int g ? g : DateTime.Now.Year;
    private int Mesec => ComboMesec.SelectedItem is int m ? m : DateTime.Now.Month;

    private void ComboPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        Ucitaj();
    }

    private void Ucitaj()
    {
        try
        {
            _servis.Obezbedi(Godina, Mesec);

            var redovi = new List<IsplataRed>();

            foreach (var isplata in _servis.Isplate(Godina, Mesec))
            {
                var obracuni = IsplataService
                    .Obuhvat(_db.ObracuniPlata, Godina, Mesec, isplata)
                    .Where(o => !o.Storniran)
                    .ToList();

                var prijava = _servis.PrijavaZa(isplata);

                redovi.Add(new IsplataRed
                {
                    Isplata = isplata,
                    BrojObracuna = obracuni.Count,
                    Neto = obracuni.Sum(o => o.NetoIsplata),
                    Bop = string.IsNullOrWhiteSpace(prijava?.Bop) ? "—" : prijava!.Bop,
                    StatusPrijaveStr = prijava == null ? "nema prijave" : prijava.Status.ToString()
                });
            }

            _redovi = new ObservableCollection<IsplataRed>(redovi);
            GridIsplate.ItemsSource = _redovi;

            GridNalazi.ItemsSource = _servis.Proveri(Godina, Mesec)
                .OrderByDescending(n => n.Tezina)
                .ToList();

            DatumNove.SelectedDate = new DateTime(Godina, Mesec, DateTime.DaysInMonth(Godina, Mesec));

            int bezIsplate = _db.ObracuniPlata
                .Count(o => o.Godina == Godina && o.Mesec == Mesec && o.IsplataId == null);

            StatusMessage.Text = bezIsplate > 0
                ? $"{_redovi.Count} isplata za {Mesec:D2}/{Godina}. Obračuna bez upisane isplate: {bezIsplate} — pripadaju prvoj isplati; dugme 🔗 to i upisuje."
                : $"{_redovi.Count} isplata za {Mesec:D2}/{Godina}.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        if (ComboVrsta.SelectedItem is not VrstaIsplateStavka stavka)
        {
            StatusMessage.Text = "Izaberite vrstu isplate.";
            return;
        }

        var rezultat = _servis.Dodaj(
            Godina, Mesec, stavka.Vrsta, TxtOpis.Text,
            DatumNove.SelectedDate ?? new DateTime(Godina, Mesec, DateTime.DaysInMonth(Godina, Mesec)));

        StatusMessage.Text = rezultat.Poruka;

        if (!rezultat.Uspesno)
        {
            MessageBox.Show(rezultat.Poruka, "Isplata nije dodata",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TxtOpis.Text = "";
        Ucitaj();
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridIsplate.SelectedItem is not IsplataRed red)
        {
            StatusMessage.Text = "Izaberite isplatu koju želite da obrišete.";
            return;
        }

        var rezultat = _servis.Obrisi(red.Isplata.IsplataId);
        StatusMessage.Text = rezultat.Poruka;

        if (!rezultat.Uspesno)
        {
            MessageBox.Show(rezultat.Poruka, "Isplata nije obrisana",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Ucitaj();
    }

    private void BtnPovezi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int povezano = _servis.PoveziZatecene(Godina, Mesec);

            StatusMessage.Text = povezano == 0
                ? "Svi obračuni ovog meseca već nose svoju isplatu."
                : $"Vezano {povezano} obračuna za prvu isplatu meseca. Nijedan iznos nije promenjen.";

            Ucitaj();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Povezivanje nije izvršeno: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        GridIsplate.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            _servis = new IsplataService(_db);
            Ucitaj();

            StatusMessage.Text = "Isplate su sačuvane.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Isplate nisu sačuvane: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
