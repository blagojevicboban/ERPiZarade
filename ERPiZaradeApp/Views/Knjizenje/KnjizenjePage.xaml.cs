using System.Windows.Controls;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Knjizenje;

public partial class KnjizenjePage : Page
{
    /// <param name="rod">
    /// Rod isplata koje se knjiže. Naknada van radnog odnosa ide na svoj konto troška iz
    /// šifarnika vrsta ugovora, pa se i nalog za knjiženje pravi zasebno.
    /// </param>
    public KnjizenjePage(RodIsplate rod = RodIsplate.Zarada)
    {
        InitializeComponent();
        DataContext = new KnjizenjeViewModel(rod);
    }
}
