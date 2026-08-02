using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Praznici;

public partial class PrazniciPage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<Praznik> _praznici = [];

    /// <summary>Dani obrisani u tabeli; brišu se iz baze tek kad se pritisne „Sačuvaj".</summary>
    private readonly List<Praznik> _zaBrisanje = [];

    public PrazniciPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        int tekuca = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        ComboGodina.ItemsSource = Enumerable.Range(tekuca - 2, 6).ToList();
        ComboGodina.SelectedItem = tekuca;
    }

    private int Godina => ComboGodina.SelectedItem as int? ?? DateTime.Now.Year;

    private void ComboGodina_SelectionChanged(object sender, SelectionChangedEventArgs e) => Ucitaj();

    private void Ucitaj()
    {
        var pocetak = new DateTime(Godina, 1, 1);
        var kraj = new DateTime(Godina, 12, 31);

        _zaBrisanje.Clear();
        _praznici = new ObservableCollection<Praznik>(
            _db.Praznici.Where(p => p.Datum >= pocetak && p.Datum <= kraj).OrderBy(p => p.Datum));

        GridPraznici.ItemsSource = _praznici;

        OsveziFond();
        StatusMessage.Text = _praznici.Count == 0
            ? $"Za {Godina}. nema unetih dana. Pritisnite „Popuni zakonske praznike\"."
            : $"{Godina}: {_praznici.Count} unetih dana.";
    }

    /// <summary>
    /// Fond se računa iz onoga što je u bazi, pa se prikazuje tek posle snimanja — inače bi
    /// tabela pokazivala sate koje obračun još ne koristi.
    /// </summary>
    private void OsveziFond()
    {
        var servis = new PraznikService(_db);

        GridFond.ItemsSource = Enumerable.Range(1, 12).Select(m => new FondRed
        {
            Mesec = NaziviMeseci.Za(m),
            BrojPraznika = servis.Praznici(Godina, m).Count(p => p.Neradni),
            RadniDani = servis.RadniDani(Godina, m),
            FondSati = servis.FondSati(Godina, m)
        }).ToList();
    }

    private void BtnPopuni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int dodato = new PraznikService(_db).ObezbediGodinu(Godina);
            Ucitaj();

            StatusMessage.Text = dodato > 0
                ? $"Dodato {dodato} zakonskih praznika za {Godina}."
                : $"Zakonski praznici za {Godina}. su već uneti.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Praznici nisu popunjeni: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        var novi = new Praznik
        {
            Datum = new DateTime(Godina, 1, 1),
            Naziv = "",
            Neradni = true,
            RucniUnos = true
        };

        _praznici.Add(novi);
        GridPraznici.SelectedItem = novi;
        GridPraznici.ScrollIntoView(novi);
        StatusMessage.Text = "Unesite datum i naziv, pa pritisnite „Sačuvaj\".";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridPraznici.SelectedItem is not Praznik izabrani)
        {
            StatusMessage.Text = "Izaberite dan koji želite da obrišete.";
            return;
        }

        _praznici.Remove(izabrani);
        if (izabrani.PraznikId != 0) _zaBrisanje.Add(izabrani);

        StatusMessage.Text = $"Dan {izabrani.Datum:dd.MM.yyyy} će biti obrisan po snimanju.";
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        // Bez ovoga vrednost iz ćelije u kojoj je kursor ostaje nesnimljena.
        GridPraznici.CommitEdit(DataGridEditingUnit.Row, true);

        var bezNaziva = _praznici.Where(p => string.IsNullOrWhiteSpace(p.Naziv)).ToList();
        if (bezNaziva.Count > 0)
        {
            MessageBox.Show("Svaki dan mora imati naziv.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Jedan dan sme da postoji samo jednom — dva zapisa istog datuma bi se dvaput
        // oduzela od fonda sati.
        var duplikat = _praznici.GroupBy(p => p.Datum.Date).FirstOrDefault(g => g.Count() > 1);
        if (duplikat != null)
        {
            MessageBox.Show($"Datum {duplikat.Key:dd.MM.yyyy} je unet više puta.", "Dvostruki unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vanGodine = _praznici.FirstOrDefault(p => p.Datum.Year != Godina);
        if (vanGodine != null)
        {
            MessageBox.Show($"Datum {vanGodine.Datum:dd.MM.yyyy} ne pripada {Godina}. godini.",
                "Pogrešna godina", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_zaBrisanje.Count > 0) _db.Praznici.RemoveRange(_zaBrisanje);

            foreach (var praznik in _praznici.Where(p => p.PraznikId == 0))
                _db.Praznici.Add(praznik);

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Kalendar je sačuvan. Novi obračuni koriste ažuriran fond sati.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kalendar nije sačuvan: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
