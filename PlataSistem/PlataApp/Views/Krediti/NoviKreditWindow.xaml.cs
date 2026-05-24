using System;
using System.Linq;
using System.Windows;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Krediti;

public partial class NoviKreditWindow : Window
{
    private readonly Radnik _radnik;
    private readonly Kredit? _kredit;
    private readonly PlataDbContext _db;

    public NoviKreditWindow(Radnik radnik, Kredit? kredit = null)
    {
        InitializeComponent();
        
        _radnik = radnik;
        _kredit = kredit;
        _db = PlataDbContext.Create(AppConfig.DbPath);

        // Inicijalizuj ComboBox-eve za datum
        ComboMesec.ItemsSource = Enumerable.Range(1, 12).ToList();
        ComboMesec.SelectedItem = DateTime.Now.Month;

        int currentYear = DateTime.Now.Year;
        ComboGodina.ItemsSource = Enumerable.Range(currentYear - 5, 15).ToList();
        ComboGodina.SelectedItem = currentYear;

        TxtRadnikInfo.Text = $"Radnik: {radnik.ImeIPrezime} (Šifra: {radnik.BrojRadnika})";

        if (kredit != null)
        {
            // Edit mode
            TxtTitle.Text = "✏️ Izmena podataka o kreditu";
            TxtOpis.Text = kredit.Opis;
            TxtUkupanIznos.Text = kredit.UkupanIznos.ToString("0.00");
            TxtBrojRata.Text = kredit.BrojRata.ToString();
            TxtMesecnaRata.Text = kredit.MesecnaRata.ToString("0.00");
            
            ComboMesec.SelectedItem = kredit.DatumPocetka.Month;
            ComboGodina.SelectedItem = kredit.DatumPocetka.Year;

            CheckAktivno.Visibility = Visibility.Visible;
            CheckAktivno.IsChecked = kredit.Aktivan;
        }
    }

    private void TxtUkupanIznos_LostFocus(object sender, RoutedEventArgs e) => AutoCalculate();
    private void TxtBrojRata_LostFocus(object sender, RoutedEventArgs e) => AutoCalculate();
    private void TxtMesecnaRata_LostFocus(object sender, RoutedEventArgs e) => AutoCalculate();

    private void AutoCalculate()
    {
        bool hasUkupno = decimal.TryParse(TxtUkupanIznos.Text, out decimal ukupno) && ukupno > 0;
        bool hasRate = int.TryParse(TxtBrojRata.Text, out int rate) && rate > 0;
        bool hasMesecno = decimal.TryParse(TxtMesecnaRata.Text, out decimal mesecno) && mesecno > 0;

        // Ako imamo ukupno i broj rata, izračunaj mesečnu ratu
        if (hasUkupno && hasRate && !hasMesecno)
        {
            decimal rata = Math.Round(ukupno / rate, 2);
            TxtMesecnaRata.Text = rata.ToString("0.00");
        }
        // Ako imamo ukupno i mesečnu ratu, izračunaj broj rata
        else if (hasUkupno && hasMesecno && !hasRate)
        {
            int calculatedRate = (int)Math.Ceiling(ukupno / mesecno);
            TxtBrojRata.Text = calculatedRate.ToString();
        }
        // Ako imamo mesečnu ratu i broj rata, izračunaj ukupan iznos
        else if (hasMesecno && hasRate && !hasUkupno)
        {
            decimal calculatedUkupno = Math.Round(mesecno * rate, 2);
            TxtUkupanIznos.Text = calculatedUkupno.ToString("0.00");
        }
    }

    private void BtnOtkazi_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        // Validacije
        if (string.IsNullOrWhiteSpace(TxtOpis.Text))
        {
            MessageBox.Show("Molimo unesite naziv kredita / poverioca.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtUkupanIznos.Text, out decimal ukupno) || ukupno <= 0)
        {
            MessageBox.Show("Molimo unesite ispravan ukupan iznos duga.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TxtBrojRata.Text, out int rate) || rate <= 0)
        {
            MessageBox.Show("Molimo unesite ispravan ugovoreni broj rata.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtMesecnaRata.Text, out decimal mesecnaRata) || mesecnaRata <= 0)
        {
            MessageBox.Show("Molimo unesite ispravan iznos mesečne rate.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int mesec = (int)ComboMesec.SelectedItem;
        int godina = (int)ComboGodina.SelectedItem;
        DateTime datumPocetka = new DateTime(godina, mesec, 1);

        try
        {
            if (_kredit == null)
            {
                // ADD MODE: Kreiraj novi kredit
                var noviKredit = new Kredit
                {
                    RadnikId = _radnik.Id,
                    Opis = TxtOpis.Text.Trim(),
                    UkupanIznos = ukupno,
                    MesecnaRata = mesecnaRata,
                    OstatakDuga = ukupno, // Početni ostatak duga je jednak ukupnom iznosu
                    BrojRata = rate,
                    PlateneRate = 0,
                    DatumPocetka = datumPocetka,
                    DatumZavrsetka = datumPocetka.AddMonths(rate - 1),
                    Aktivan = true
                };

                _db.Krediti.Add(noviKredit);
            }
            else
            {
                // EDIT MODE: Izmeni postojeći kredit
                var dbKredit = await _db.Krediti.FindAsync(_kredit.Id);
                if (dbKredit != null)
                {
                    dbKredit.Opis = TxtOpis.Text.Trim();
                    dbKredit.UkupanIznos = ukupno;
                    dbKredit.MesecnaRata = mesecnaRata;
                    dbKredit.BrojRata = rate;
                    dbKredit.DatumPocetka = datumPocetka;
                    dbKredit.DatumZavrsetka = datumPocetka.AddMonths(rate - 1);
                    dbKredit.Aktivan = CheckAktivno.IsChecked ?? true;
                    
                    // Prilagodi ostatak duga na osnovu plaćenih rata
                    dbKredit.OstatakDuga = Math.Max(0, ukupno - (dbKredit.PlateneRate * mesecnaRata));
                }
            }

            await _db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju kredita: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
