using System.Diagnostics;
using System.IO;
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

        // Primeni podešavanje maksimizovanog pokretanja
        if (UserSettings.Instance.PokretanjeMaximizovano)
            WindowState = WindowState.Maximized;

        UcitajImeFirme();
        InicijalizujAktivniPeriod();
        // Otvori Obračuni kao početnu stranicu
        NavigateTo(null!, new Views.Obracuni.ObracuniPage());

        // Automatski backup pri pokretanju (u pozadinskom threadu da ne usporava start)
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                Services.BackupService.Instance.NapraviAutomatskiBackup();
            }
            catch { }
        });

        // Provera ažuriranja
        _ = CheckForUpdatesAsync();
    }

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        try
        {
            // OVO JE LOKACIJA GDE SE NALAZE UPDATE FAJLOVI
            // Za potrebe testiranja koristićemo neki lokalni folder, a ti ovde možeš staviti URL (npr. GitHub Releases URL ili svoj web server)
            string updateUrl = @"C:\PlataUpdates"; 
            
            var mgr = new Velopack.UpdateManager(updateUrl);
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                // Prikaži obaveštenje korisniku
                var result = MessageBox.Show(
                    "Nova verzija je dostupna. Da li želite da je preuzmete i instalirate sada?",
                    "Ažuriranje aplikacije", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    await mgr.DownloadUpdatesAsync(newVersion);
                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
            }
        }
        catch (Exception ex)
        {
            // Logovanje greške (možemo izostaviti prikazivanje kako ne bismo plašili korisnika)
            System.Diagnostics.Debug.WriteLine($"Greška pri ažuriranju: {ex.Message}");
        }
    }

    public void InicijalizujAktivniPeriod()
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
            PlataData.Models.Firma? firma = null;
            if (AppConfig.ActiveFirmaId.HasValue)
            {
                firma = db.Firme.Find(AppConfig.ActiveFirmaId.Value);
            }
            if (firma == null)
            {
                firma = db.Firme.FirstOrDefault();
                if (firma != null)
                {
                    AppConfig.ActiveFirmaId = firma.Id;
                }
            }

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

    private void BtnRadniSati_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnRadniSati, new Views.RadniSati.RadniSatiPage());

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

    private void BtnBanke_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnBanke, new Views.Banke.BankePage());

    private void BtnFirme_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnFirme, new Views.Firme.FirmePage());

    private void BtnPodesavanja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPodesavanja, new Views.Podesavanja.PodesavanjaPage());

    private void BtnPorezi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPorezi, new Views.Porezi.PoreziPage());

    private void BtnDoprinosi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnDoprinosi, new Views.Doprinosi.DoprinosiPage());

    private void BtnPlatniRazredi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPlatniRazredi, new Views.PlatniRazredi.PlatniRazrediPage());

    private void BtnObracuni_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnObracuni, new Views.Obracuni.ObracuniPage());

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Putanja do HTML uputstva pored exe-a
            string helpPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "Help", "uputstvo.html");

            if (!File.Exists(helpPath))
            {
                MessageBox.Show(
                    $"Datoteka uputstva nije pronađena:\n{helpPath}",
                    "Uputstvo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = helpPath,
                UseShellExecute = true  // otvara u default browser-u
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri otvaranju uputstva: {ex.Message}",
                "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FirmaBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        NavigateTo(BtnFirme, new Views.Firme.FirmePage());
    }

    private void ActivePeriodBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        NavigateTo(null!, new Views.Obracuni.ObracuniPage());
    }

    public void NavigateToObracun(int godina, int mesec)
    {
        NavigateTo(BtnObracun, new Views.Obracun.ObracunPage(godina, mesec));
    }

    public void RestartujNakonPromeneBaze()
    {
        UcitajImeFirme();
        InicijalizujAktivniPeriod();
        NavigateTo(null!, new Views.Obracuni.ObracuniPage());
    }

    public void OtvoriRadnike()
    {
        NavigateTo(BtnRadnici, new Views.Radnici.RadniciPage());
    }

    public void OtvoriPorezi()
    {
        NavigateTo(BtnPorezi, new Views.Porezi.PoreziPage());
    }
}