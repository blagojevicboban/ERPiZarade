using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.PppPo;

public partial class PppPoPage : Page
{
    private readonly PlataDbContext _db;
    private PppPoRezultat? _rezultat;

    public PppPoPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        int tekuca = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        ComboGodina.ItemsSource = Enumerable.Range(tekuca - 4, 5).Reverse().ToList();
        ComboGodina.SelectedItem = tekuca;
    }

    private int Godina => ComboGodina.SelectedItem as int? ?? DateTime.Now.Year;

    private void ComboGodina_SelectionChanged(object sender, SelectionChangedEventArgs e) => Ucitaj();

    private void Ucitaj()
    {
        _rezultat = new PppPoService(_db).Pripremi(Godina);

        GridObrasci.ItemsSource = _rezultat.Obrasci;

        ListaNalaza.ItemsSource = _rezultat.Nalazi;
        PanelNalazi.Visibility = _rezultat.Nalazi.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        StatusMessage.Text = _rezultat.Obrasci.Count == 0
            ? $"Za {Godina}. godinu nema obračuna."
            : $"{Godina}: {_rezultat.Obrasci.Count} potvrda · porez {_rezultat.UkupnoPorez:N2} · doprinosi {_rezultat.UkupnoDoprinosi:N2}";
    }

    private Firma? Firma() => _db.Firme.FirstOrDefault();

    /// <summary>
    /// Greške u kontrolama ne blokiraju štampu — potvrda se ponekad izdaje i dok se
    /// neslaganje ispravlja — ali se traži izričita potvrda, da neslaganje ne prođe nezapaženo.
    /// </summary>
    private bool PotvrdiUprkosNalazima()
    {
        if (_rezultat == null || _rezultat.BrojGresaka == 0) return true;

        return MessageBox.Show(
            $"Kontrole su našle {_rezultat.BrojGresaka} grešaka za {Godina}. godinu.\n\n" +
            "Potvrda koja se ne slaže sa podnetim PPP-PD prijavama govori radniku jedno, a Poreskoj upravi drugo.\n\n" +
            "Želite li ipak da nastavite?",
            "Kontrole nisu prošle", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void BtnJedan_Click(object sender, RoutedEventArgs e)
    {
        if (GridObrasci.SelectedItem is not PppPoObrazac obrazac)
        {
            MessageBox.Show("Izaberite radnika u tabeli.", "Nema selekcije",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!PotvrdiUprkosNalazima()) return;

        string ime = BezbednoIme(obrazac.Radnik.ImeIPrezime);
        var sfd = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = $"PPP-PO_{ime}_{Godina}.pdf",
            Title = "Sačuvaj potvrdu PPP-PO"
        };

        if (sfd.ShowDialog() != true) return;

        Izvrsi(() =>
        {
            PppPoDocument.Sacuvaj(obrazac, Firma(), sfd.FileName);
            StatusMessage.Text = $"Sačuvano: {sfd.FileName}";
        });
    }

    private void BtnSvi_Click(object sender, RoutedEventArgs e)
    {
        if (_rezultat == null || _rezultat.Obrasci.Count == 0) return;
        if (!PotvrdiUprkosNalazima()) return;

        var sfd = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = $"PPP-PO_svi_{Godina}.pdf",
            Title = "Sačuvaj zbirni dokument sa potvrdama"
        };

        if (sfd.ShowDialog() != true) return;

        Izvrsi(() =>
        {
            PppPoDocument.Sacuvaj(_rezultat.Obrasci, Firma(), sfd.FileName);
            ZabeleziIzdavanje(_rezultat.Obrasci.Count, "zbirni PDF");
            StatusMessage.Text = $"Sačuvano {_rezultat.Obrasci.Count} potvrda u {sfd.FileName}";
        });
    }

    private void BtnPojedinacno_Click(object sender, RoutedEventArgs e)
    {
        if (_rezultat == null || _rezultat.Obrasci.Count == 0) return;
        if (!PotvrdiUprkosNalazima()) return;

        var dialog = new OpenFolderDialog
        {
            Title = "Izaberite folder za potvrde",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != true) return;

        Izvrsi(() =>
        {
            var firma = Firma();
            foreach (var obrazac in _rezultat.Obrasci)
            {
                string ime = BezbednoIme(obrazac.Radnik.ImeIPrezime);
                string putanja = Path.Combine(dialog.FolderName, $"PPP-PO_{ime}_{Godina}.pdf");
                PppPoDocument.Sacuvaj(obrazac, firma, putanja);
            }

            ZabeleziIzdavanje(_rezultat.Obrasci.Count, "pojedinačni PDF-ovi");
            StatusMessage.Text = $"Sačuvano {_rezultat.Obrasci.Count} potvrda u {dialog.FolderName}";

            Process.Start(new ProcessStartInfo { FileName = dialog.FolderName, UseShellExecute = true });
        });
    }

    /// <summary>Izdavanje potvrda se beleži — obrazac sadrži lične podatke i zakonski je rok vezan za njega.</summary>
    private void ZabeleziIzdavanje(int broj, string oblik)
        => AuditService.Zabelezi(_db, Godina, 12, AkcijaObracuna.PppPdGenerisan,
            $"Izdato {broj} PPP-PO potvrda za {Godina}. godinu ({oblik})");

    private static void Izvrsi(Action akcija)
    {
        try
        {
            akcija();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dokument nije sačuvan:\n\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BezbednoIme(string ime)
    {
        foreach (char nedozvoljen in Path.GetInvalidFileNameChars())
            ime = ime.Replace(nedozvoljen, '_');
        return ime.Replace(' ', '_');
    }
}
