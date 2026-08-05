using System;
using System.Linq;
using System.Windows;
using ERPiZaradeData;
using ERPiZaradeApp.Services;

namespace ERPiZaradeApp.Views.Primanja;

/// <summary>Jedan red prikaza u tabeli — pripremljen za DataGrid, ne izlaže servisne tipove.</summary>
public sealed class StavkaPrikaz
{
    public required string Jmbg { get; init; }
    public required string Prikaz { get; init; }
    public required string BrojNaloga { get; init; }
    public required decimal Iznos { get; init; }
    public required string Status { get; init; }
}

public partial class UvozPutnihNalogaWindow : Window
{
    private readonly PlataDbContext _db;
    private RezultatUvozaPutnihNaloga? _rezultat;

    /// <summary>Postavlja se na <c>true</c> ako je bar jedna stavka stvarno uvezena.</summary>
    public bool Uvezeno { get; private set; }

    public UvozPutnihNalogaWindow(PlataDbContext db)
    {
        InitializeComponent();
        _db = db;
    }

    private async void BtnOtvori_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Putni nalozi iz ERPiFinansije (*.json)|*.json|Svi fajlovi (*.*)|*.*",
            Title = "Izaberite izvoz iz ERPiFinansije"
        };

        if (ofd.ShowDialog() != true) return;

        TxtFajl.Text = ofd.FileName;
        BtnUvezi.IsEnabled = false;
        _rezultat = null;

        try
        {
            var servis = new PutniNaloziImportService(_db);
            _rezultat = await servis.ProcitajAsync(ofd.FileName);

            DgStavke.ItemsSource = _rezultat.Stavke.Select(s => new StavkaPrikaz
            {
                Jmbg = s.Jmbg,
                Prikaz = s.UparenRadnik != null
                    ? $"{s.UparenRadnik.BrojRadnika} {s.UparenRadnik.ImeIPrezime}"
                    : s.ZaposleniIme,
                BrojNaloga = s.BrojNaloga,
                Iznos = s.Iznos,
                Status = s.Greska ?? (s.VecUvezen ? "Već uvezen" : "Spremno")
            }).ToList();

            if (_rezultat.Nalazi.Count > 0)
            {
                ListaNalaza.ItemsSource = _rezultat.Nalazi
                    .Select(n => $"[{n.TezinaTekst}] {n.Provera}: {n.Opis}")
                    .ToList();
                PanelNalazi.Visibility = Visibility.Visible;
            }
            else
            {
                PanelNalazi.Visibility = Visibility.Collapsed;
            }

            string cilj = _rezultat.CiljnaIsplata != null
                ? $"isplatu „{_rezultat.CiljnaIsplata.Naziv}“"
                : "isplatu koja će biti napravljena";

            TxtStatus.Text = $"Period {_rezultat.Mesec:D2}/{_rezultat.Godina} ({_rezultat.FirmaNaziv}) → {cilj}. " +
                              $"{_rezultat.BrojZaUvoz} od {_rezultat.Stavke.Count} spremno za uvoz.";

            BtnUvezi.IsEnabled = _rezultat.SmeSeUvesti;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čitanju fajla: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnUvezi_Click(object sender, RoutedEventArgs e)
    {
        if (_rezultat == null || !_rezultat.SmeSeUvesti) return;

        try
        {
            var servis = new PutniNaloziImportService(_db);
            int broj = servis.Uvezi(_rezultat);

            Uvezeno = broj > 0;

            MessageBox.Show(
                $"Uvezeno {broj} stavki u obračun {_rezultat.Mesec:D2}/{_rezultat.Godina}.\n\n" +
                "Pokrenite ponovni obračun konačne zarade da iznosi uđu u platu.",
                "Uvoz završen", MessageBoxButton.OK, MessageBoxImage.Information);

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uvoz nije uspeo: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnZatvori_Click(object sender, RoutedEventArgs e) => Close();
}
