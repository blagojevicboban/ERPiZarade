using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PlataApp.Views.Pomoc;

/// <summary>Generički kontekstualni help popup za edit-prozore, po uzoru na AccountingApp.Views.Pomoc.EditHelpWindow.</summary>
public partial class EditHelpWindow : Window
{
    public EditHelpWindow(string naslov, string podnaslov, IEnumerable<(string Precica, string Opis)> precice, string? dodatniTekst = null)
    {
        InitializeComponent();

        Title = naslov;
        TxtNaslov.Text = naslov;
        TxtPodnaslov.Text = podnaslov;

        PnlSadrzaj.Children.Add(new TextBlock
        {
            Text = "⌨️ Prečice na tastaturi",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["PrimaryBrush"],
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var (precica, opis) in precice)
        {
            var red = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            red.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            red.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var kolonaPrecica = new TextBlock { Text = precica, FontWeight = FontWeights.Bold, Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"] };
            var kolonaOpis = new TextBlock { Text = opis, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(kolonaOpis, 1);

            red.Children.Add(kolonaPrecica);
            red.Children.Add(kolonaOpis);
            PnlSadrzaj.Children.Add(red);
        }

        if (!string.IsNullOrWhiteSpace(dodatniTekst))
        {
            PnlSadrzaj.Children.Add(new Separator { Background = (Brush)Application.Current.Resources["BorderBrush"], Margin = new Thickness(0, 10, 0, 10) });
            PnlSadrzaj.Children.Add(new TextBlock
            {
                Text = dodatniTekst,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"]
            });
        }
    }

    private void BtnZatvori_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Enter)
        {
            Close();
        }
    }
}
