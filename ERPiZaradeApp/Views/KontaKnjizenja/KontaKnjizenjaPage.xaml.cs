using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.KontaKnjizenja;

/// <summary>
/// Šifarnik konta na koja se knjiži obračun (Faza 3.1).
///
/// Redovi se ne dodaju i ne brišu: svaki od njih je uloga koju kod traži po ključu, pa bi
/// obrisan red značio nalog bez protivstave. Menja se <b>samo broj konta</b> — to je jedino
/// što zavisi od kontnog plana firme.
/// </summary>
public partial class KontaKnjizenjaPage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<KontoKnjizenja> _konta = [];

    public KontaKnjizenjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        Ucitaj();
    }

    private void Ucitaj()
    {
        _konta = new ObservableCollection<KontoKnjizenja>(
            _db.KontaKnjizenja.OrderBy(k => k.Redosled).ThenBy(k => k.Kljuc));

        GridKonta.ItemsSource = _konta;

        int bezKonta = _konta.Count(k => string.IsNullOrWhiteSpace(k.Konto));

        StatusMessage.Text = bezKonta == 0
            ? $"{_konta.Count} konta."
            : $"{_konta.Count} konta; {bezKonta} bez upisanog broja — nalog za knjiženje se dotle ne izvozi.";
    }

    private void GridKonta_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        NapomenaKonta.Text = GridKonta.SelectedItem is KontoKnjizenja konto ? konto.Napomena : "";
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        GridKonta.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            foreach (var konto in _konta) konto.Konto = (konto.Konto ?? "").Trim();

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Šifarnik je sačuvan. Izmene važe od sledećeg naloga za knjiženje.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Šifarnik nije sačuvan: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Vraća brojeve konta na podrazumevane iz Kontnog okvira. Traži potvrdu — prilagođena
    /// analitika se ovim briše, a nju je neko unosio.
    /// </summary>
    private void BtnVratiPodrazumevano_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Brojevi konta se vraćaju na podrazumevane iz Kontnog okvira.\n\n" +
                "Ako ste unosili analitiku svog kontnog plana, biće prepisana. Nastaviti?",
                "Vraćanje podrazumevanih konta",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var podrazumevana = KontaKnjizenjaSeed.Podrazumevana()
            .ToDictionary(k => k.Kljuc, StringComparer.Ordinal);

        int izmenjeno = 0;

        foreach (var konto in _konta)
        {
            if (!podrazumevana.TryGetValue(konto.Kljuc, out var izvor)) continue;
            if (string.Equals(konto.Konto, izvor.Konto, StringComparison.Ordinal)) continue;

            konto.Konto = izvor.Konto;
            izmenjeno++;
        }

        GridKonta.Items.Refresh();

        StatusMessage.Text = izmenjeno == 0
            ? "Konta su već podrazumevana."
            : $"Vraćeno {izmenjeno} konta. Pritisnite „Sačuvaj\" da izmena ostane.";
    }
}
