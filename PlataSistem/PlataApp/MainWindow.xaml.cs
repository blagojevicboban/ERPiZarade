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
        InicijalizujAktivniPeriod();
        // Otvori Obračuni kao početnu stranicu
        NavigateTo(null!, new Views.Obracuni.ObracuniPage());
    }

    private void InicijalizujAktivniPeriod()
    {
        try
        {
            using var db = PlataData.PlataDbContext.Create(AppConfig.DbPath);
            var latestObracun = db.ObracuniPlata
                .OrderByDescending(o => o.Godina)
                .ThenByDescending(o => o.Mesec)
                .FirstOrDefault();

            if (latestObracun != null)
            {
                AppConfig.ActiveGodina = latestObracun.Godina;
                AppConfig.ActiveMesec = latestObracun.Mesec;
            }
        }
        catch { }
        OsveziAktivniPeriodPrikaz();
    }

    public void OsveziAktivniPeriodPrikaz()
    {
        if (ActivePeriodText == null) return;

        if (AppConfig.ActiveGodina.HasValue && AppConfig.ActiveMesec.HasValue)
        {
            string[] meseciStr = {
                "Januar", "Februar", "Mart", "April", "Maj", "Jun",
                "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar"
            };
            int mesec = AppConfig.ActiveMesec.Value;
            int godina = AppConfig.ActiveGodina.Value;
            if (mesec >= 1 && mesec <= 12)
            {
                ActivePeriodText.Text = $"{meseciStr[mesec - 1]} {godina}";
                return;
            }
            ActivePeriodText.Text = $"{mesec:D2} / {godina}";
        }
        else
        {
            ActivePeriodText.Text = "Nije izabran";
        }
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
        if (btn != null)
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

    private void BtnPorezi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPorezi, new Views.Porezi.PoreziPage());

    private void BtnDoprinosi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnDoprinosi, new Views.Doprinosi.DoprinosiPage());

    private void BtnPlatniRazredi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPlatniRazredi, new Views.PlatniRazredi.PlatniRazrediPage());

    private void FirmaBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        NavigateTo(null!, new Views.Obracuni.ObracuniPage());
    }

    public void NavigateToObracun(int godina, int mesec)
    {
        NavigateTo(BtnObracun, new Views.Obracun.ObracunPage(godina, mesec));
    }
}