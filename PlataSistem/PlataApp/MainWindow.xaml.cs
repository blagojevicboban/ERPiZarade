using System.Windows;
using System.Windows.Controls;
using PlataApp.Views.Radnici;

namespace PlataApp;

public partial class MainWindow : Window
{
    private Button? _activeNavBtn;

    public MainWindow()
    {
        InitializeComponent();
        UcitajImeFirme();
        // Otvori Radnici kao početnu stranicu
        NavigateTo(BtnRadnici, new RadniciPage());
    }

    public void UcitajImeFirme()
    {
        try
        {
            using var db = PlataData.PlataDbContext.Create(AppConfig.DbPath);
            var firma = db.Firme.FirstOrDefault();
            if (firma != null && !string.IsNullOrWhiteSpace(firma.Naziv))
            {
                ImeFirmeText.Text = firma.Naziv;
            }
            else
            {
                ImeFirmeText.Text = "Zavod za poljoprivredu";
            }
        }
        catch
        {
            ImeFirmeText.Text = "Zavod za poljoprivredu";
        }
    }

    private void NavigateTo(Button btn, Page page)
    {
        if (_activeNavBtn != null)
            _activeNavBtn.Style = (Style)FindResource("NavButton");
        btn.Style = (Style)FindResource("NavButtonActive");
        _activeNavBtn = btn;
        MainFrame.Navigate(page);
    }

    private void BtnRadnici_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnRadnici, new RadniciPage());

    private void BtnObracun_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnObracun, new Views.Obracun.ObracunPage());

    private void BtnListici_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnListici, new Views.Listici.ListiciPage());

    private void BtnStampe_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnStampe, new Views.Stampe.StampePage());

    private void BtnPppPd_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPppPd, new Views.PppPd.PppPdPage());

    private void BtnKrediti_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnKrediti, new Views.Krediti.KreditiPage());

    private void BtnPodesavanja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPodesavanja, new Views.Podesavanja.PodesavanjaPage());
}