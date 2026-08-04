using System;
using System.Windows;
using System.Windows.Controls;

namespace ERPiZaradeApp.Views.Ugovori;

/// <summary>
/// Unos nove isplate naknada po ugovorima van radnog odnosa.
///
/// Traži samo <b>datum isplate</b> i opis, jer je datum ono što nosi obe stvari koje prijavu
/// određuju: on je datum plaćanja (polje 1.4 Obrasca PPP-PD), a mesec iz njega je obračunski
/// period (polje 1.2). Period se zato ne bira zasebno — dva polja za istu stvar bi se razišla,
/// a razlika bi se videla tek kad Poreska uprava prijavu odbije.
/// </summary>
public partial class NovaIsplataNaknadeWindow : Window
{
    /// <summary>Datum isplate; čita se tek pošto prozor vrati <c>true</c>.</summary>
    public DateTime DatumIsplate { get; private set; }

    public string Opis => TxtOpis.Text?.Trim() ?? "";

    public NovaIsplataNaknadeWindow(int godina, int mesec)
    {
        InitializeComponent();

        // Polazi se od izabranog meseca, ali od danas ako je to taj mesec — honorar se
        // najčešće unosi na dan isplate.
        var danas = DateTime.Today;
        DatumIsplatePicker.SelectedDate = danas.Year == godina && danas.Month == mesec
            ? danas
            : new DateTime(godina, mesec, 1);

        OsveziPoruku();
    }

    private void DatumIsplatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        => OsveziPoruku();

    /// <summary>
    /// Ispisuje šta izabrani datum znači za prijavu. Neradni dan se javlja odmah, jer ga
    /// Pravilnik izričito zabranjuje kao datum plaćanja („ne može biti neradni dan, odnosno
    /// dan kada ne radi platni promet") — a to se inače vidi tek pri podnošenju.
    /// </summary>
    private void OsveziPoruku()
    {
        if (PorukaPerioda == null) return;

        if (DatumIsplatePicker.SelectedDate is not DateTime datum)
        {
            PorukaPerioda.Text = "";
            return;
        }

        string period = $"Obračunski period prijave: {datum.Month:D2}/{datum.Year}.";

        PorukaPerioda.Text = datum.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? period + "  ⚠ Vikend — datum plaćanja ne sme biti dan kada ne radi platni promet."
            : period;
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        if (DatumIsplatePicker.SelectedDate is not DateTime datum)
        {
            MessageBox.Show(
                "Unesite datum isplate. On je datum plaćanja na PPP-PD prijavi i deli jednu " +
                "prijavu od druge, pa se ne može izostaviti.",
                "Datum nije unet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DatumIsplate = datum.Date;
        DialogResult = true;
    }
}
