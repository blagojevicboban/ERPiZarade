using System.Windows;
using System.Windows.Controls;

namespace PlataApp.Views.Radnici;

public partial class RadniciPage : Page
{
    public RadniciPage()
    {
        InitializeComponent();
    }

    private void BtnIzmeni_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RadniciViewModel vm)
        {
            vm.IsEditing = true;
            vm.StatusPoruka = "Izmena podataka radnika...";
        }
    }
}
