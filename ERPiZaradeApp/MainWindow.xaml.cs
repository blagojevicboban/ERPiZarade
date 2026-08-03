using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeApp.Views.Pomoc;
using ERPiZaradeApp.Views.Radnici;

namespace ERPiZaradeApp;

public partial class MainWindow : Window
{
    private Button? _activeNavBtn;
    private string _trenutnaSekcijaKljuc = "";

    public MainWindow()
    {
        InitializeComponent();

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.ToString(3)}  •  {DateTime.Now.Year}";

        // Primeni podešavanje maksimizovanog pokretanja
        if (UserSettings.Instance.PokretanjeMaximizovano)
            WindowState = WindowState.Maximized;

        UcitajImeFirme();
        InicijalizujAktivniPeriod();
        UpdateUserInfo();
        ApplyRolePermissions();
        // Otvori Radnu tablu kao početnu stranicu
        NavigateTo(BtnDashboard, "📊 Radna tabla", new Views.Dashboard.DashboardPage());

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
            // Javni repozitorijum — nije potreban token.
            string repoUrl = "https://github.com/blagojevicboban/ERPiZarade";
            // Ranije je ovde bio hardkodovan plaintext PAT za privatni repo ObracunZarada (kompromitovan,
            // mora se opozvati na GitHub-u). Ako je repo ikad ponovo privatan, token se čita iz env.
            // promenljive ili lokalnog fajla van repozitorijuma — nikad iz izvornog koda.
            string? token = GetUpdateToken();

            var source = new Velopack.Sources.GithubSource(repoUrl, token, false);
            var mgr = new Velopack.UpdateManager(source);
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                var dialog = new UpdateDialog(newVersion, mgr);
                dialog.Owner = this;
                dialog.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            // Logovanje greške (možemo izostaviti prikazivanje kako ne bismo plašili korisnika)
            Serilog.Log.Error(ex, "Greška pri ažuriranju");
        }
    }

    private static string? GetUpdateToken()
    {
        var envToken = Environment.GetEnvironmentVariable("ERPHUB_PLATA_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken)) return envToken;

        try
        {
            var tokenPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ERPiHub", "plata_update_token.txt");
            if (File.Exists(tokenPath))
            {
                var fileToken = File.ReadAllText(tokenPath).Trim();
                if (!string.IsNullOrWhiteSpace(fileToken)) return fileToken;
            }
        }
        catch { }

        return null;
    }

    public void InicijalizujAktivniPeriod()
    {
        try
        {
            using var db = ERPiZaradeData.PlataDbContext.Create(AppConfig.DbPath);
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
            using var db = ERPiZaradeData.PlataDbContext.Create(AppConfig.DbPath);
            ERPiZaradeData.Models.Firma? firma = null;
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

    private void UpdateUserInfo()
    {
        if (AppSession.TrenutniKorisnik != null)
        {
            TxtImeKorisnika.Text = AppSession.TrenutniKorisnik.ImePrezime;
            TxtUlogaKorisnika.Text = AppSession.TrenutniKorisnik.Uloga.ToString();
        }
    }

    private void ApplyRolePermissions()
    {
        // Samo Administrator sme da vidi Korisnike
        if (!AppSession.IsAdmin)
        {
            BtnKorisnici.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnKorisnici_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnKorisnici, "👥 Korisnički nalozi", new Views.Korisnici.KorisniciPage(), "Upravljanje pristupom i ulogama zaposlenih");

    private void BtnOdjava_Click(object sender, RoutedEventArgs e)
    {
        AppSession.TrenutniKorisnik = null;
        var loginWindow = new Views.Korisnici.LoginWindow();
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.M &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            BtnToggleSidebar_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F1)
        {
            OtvoriPomocKontekstualno();
            e.Handled = true;
        }
    }

    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (SidebarColumn.Width.Value > 100)
        {
            SidebarColumn.Width = new GridLength(64);
            TxtBrandTitle.Visibility = Visibility.Collapsed;
            TxtBrandSubtitle.Visibility = Visibility.Collapsed;
            HeaderRadnaTabla.Visibility = Visibility.Collapsed;
            HeaderObracuni.Visibility = Visibility.Collapsed;
            HeaderEvidencija.Visibility = Visibility.Collapsed;
            HeaderStampa.Visibility = Visibility.Collapsed;
            HeaderSifarnici.Visibility = Visibility.Collapsed;
            HeaderPodesavanja.Visibility = Visibility.Collapsed;
        }
        else
        {
            SidebarColumn.Width = new GridLength(220);
            TxtBrandTitle.Visibility = Visibility.Visible;
            TxtBrandSubtitle.Visibility = Visibility.Visible;
            HeaderRadnaTabla.Visibility = Visibility.Visible;
            HeaderObracuni.Visibility = Visibility.Visible;
            HeaderEvidencija.Visibility = Visibility.Visible;
            HeaderStampa.Visibility = Visibility.Visible;
            HeaderSifarnici.Visibility = Visibility.Visible;
            HeaderPodesavanja.Visibility = Visibility.Visible;
        }
    }

    private void NavigateTo(Button btn, string title, Page page, string subtitle = "", string helpAnchor = "")
    {
        if (_activeNavBtn != null)
            _activeNavBtn.Style = (Style)FindResource("NavButton");
        if (btn != null)
            btn.Style = (Style)FindResource("NavButtonActive");
        _activeNavBtn = btn;
        TxtHeaderTitle.Text = title;
        TxtHeaderSubtitle.Text = subtitle;
        _trenutnaSekcijaKljuc = helpAnchor;
        MainFrame.Navigate(page);
    }

    private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnDashboard, "📊 Radna tabla", new Views.Dashboard.DashboardPage(), helpAnchor: "Dashboard");

    private void BtnRadnici_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnRadnici, "👤 Radnici", new RadniciPage(), "Evidencija zaposlenih i njihovih podataka", helpAnchor: "Radnici");

    private void BtnRadniSati_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnRadniSati, "⏱️ Radni sati", new Views.RadniSati.RadniSatiPage(), helpAnchor: "RadniSati");

    private void BtnObracun_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnObracun, "📊 Obračun plate", new Views.Obracun.ObracunPage(), helpAnchor: "Obracun");

    private void BtnListici_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnListici, "🧾 Masovna štampa platnih listića", new Views.Listici.ListiciPage(), "Masovni izvoz u odvojene PDF datoteke ili generisanje jedinstvenog zbirnog dokumenta", helpAnchor: "Listici");

    private void BtnStampe_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnStampe, "📑 Knjigovodstveni izveštaji i rekapitulacije", new Views.Stampe.StampePage(), "Generisanje i štampa mesečnih platnih spiskova po radnim jedinicama i zbirnih rekapitulacija", helpAnchor: "Stampe");

    private void BtnPppPd_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPppPd, "📋 Poreska uprava — PPP-PD prijava", new Views.PppPd.PppPdPage(), "Pregled, pre-validacija poreskih osnovica i generisanje XML datoteke poreske deklaracije za Poresku Upravu Republike Srbije", helpAnchor: "PppPd");

    private void BtnPrimanja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPrimanja, "🎁 Ostala primanja", new Views.Primanja.PrimanjaPage(), "Prevoz, jubilarne nagrade, solidarne pomoći — prekoračenje neoporezivog limita automatski postaje oporezivo", helpAnchor: "Primanja");

    private void BtnPppPo_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPppPo, "🧾 PPP-PO — godišnja potvrda o plaćenim porezima i doprinosima", new Views.PppPo.PppPoPage(), "Potvrda koju je poslodavac dužan da uruči radniku do 31. januara za prethodnu godinu", helpAnchor: "PppPo");

    private void BtnNalozi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnNalozi, "🏦 Nalozi za prenos", new Views.Nalozi.NaloziPage(), "Priprema naloga za isplatu neto zarada i jedinstvene uplate poreza i doprinosa po BOP-u iz prihvaćene PPP-PD prijave", helpAnchor: "Nalozi");

    private void BtnIsplate_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnIsplate, "💸 Isplate u mesecu", new Views.Isplate.IsplatePage(), "Akontacija, konačna isplata, bonus i 13. plata kao zasebne isplate istog meseca — svaka sa svojom PPP-PD prijavom i svojim nalozima", helpAnchor: "Isplate");

    private void BtnUgovori_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnUgovori, "📝 Ugovori van radnog odnosa", new Views.Ugovori.UgovoriPage(), "Ugovor o delu, autorska naknada, privremeni i povremeni poslovi i naknade odborima — obračun naknade koja ulazi u istu PPP-PD prijavu kao zarada", helpAnchor: "Ugovori");

    private void BtnKnjizenje_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnKnjizenje, "📒 Nalog za knjiženje", new Views.Knjizenje.KnjizenjePage(), "Temeljnica za glavnu knjigu — trošak po vrstama primanja i mestima troška, obaveze po ulogama iznosa, za uvoz u ERPiFinansije", helpAnchor: "Knjizenje");

    private void BtnBolovanja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnBolovanja, "🏥 Bolovanja i refundacija RFZO", new Views.Bolovanja.BolovanjaPage(), "Evidencija privremene sprečenosti za rad preko 30 dana i obrasci OZ-7 i OZ-10 za refundaciju naknade zarade iz sredstava obaveznog zdravstvenog osiguranja", helpAnchor: "Bolovanja");

    private void BtnKontaKnjizenja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnKontaKnjizenja, "📗 Konta za knjiženje", new Views.KontaKnjizenja.KontaKnjizenjaPage(), "Konta obaveza i troškova na koja se knjiži obračun — početne vrednosti su iz Kontnog okvira", helpAnchor: "KontaKnjizenja");

    private void BtnVrsteUgovora_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnVrsteUgovora, "📄 Šifarnik vrsta ugovora", new Views.VrsteUgovora.VrsteUgovoraPage(), "Normirani troškovi, stopa poreza i doprinosi po vrsti ugovora van radnog odnosa", helpAnchor: "VrsteUgovora");

    private void BtnSabloniUgovora_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnSabloniUgovora, "🖋️ Šabloni ugovora", new Views.SabloniUgovora.SabloniUgovoraPage(), "Tekstovi ugovora van radnog odnosa sa poljima koja se popunjavaju podacima pri generisanju", helpAnchor: "SabloniUgovora");

    private void BtnKrediti_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnKrediti, "💳 Krediti i obustave", new Views.Krediti.KreditiPage(), "Evidencija bankovnih kredita i administrativnih obustava zaposlenih", helpAnchor: "Krediti");

    private void BtnBanke_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnBanke, "🏦 Šifrarnici banaka", new Views.Banke.BankePage(), "Pregled i izmena hronoloških šifarnika banaka i tekućih računa za obračun plata", helpAnchor: "Banke");

    private void BtnPraznici_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPraznici, "📅 Kalendar praznika i fond sati", new Views.Praznici.PrazniciPage(), "Zakonski i sopstveni neradni dani, iz kojih se računa mesečni fond radnih sati", helpAnchor: "Praznici");

    private void BtnVrstePrimanja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnVrstePrimanja, "💰 Šifarnik vrsta primanja", new Views.VrstePrimanja.VrstePrimanjaPage(), "Vrste primanja sa SVP šifrom, poreskim tretmanom i kontom — novo primanje se dodaje bez izmene baze", helpAnchor: "VrstePrimanja");

    private void BtnOlaksice_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnOlaksice, "🏷️ Šifarnik poreskih olakšica", new Views.Olaksice.OlaksicePage(), "Olakšice sa mehanizmom, procentima i MFP deklaracijom za PPP-PD prijavu", helpAnchor: "Olaksice");

    private void BtnFirme_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnFirme, "🏢 Upravljanje firmama", new Views.Firme.FirmePage(), "Pregled, izmena, unos novih i odabir aktivne firme za obračune i izveštaje", helpAnchor: "Firme");

    private void BtnPodesavanja_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPodesavanja, "⚙️ Podešavanja", new Views.Podesavanja.PodesavanjaPage(), "Upravljanje osnovnim podacima o firmi i kreiranje/vraćanje rezervne kopije baze podataka", helpAnchor: "Podesavanja");

    private void BtnPorezi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPorezi, "⚖️ Porezi i opšti parametri", new Views.Porezi.PoreziPage(), "Upravljanje poreskim stopama, mesečnim limitima, opštim parametrima i procentima uvećanja", helpAnchor: "Porezi");

    private void BtnDoprinosi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnDoprinosi, "📈 Doprinosi", new Views.Doprinosi.DoprinosiPage(), "Pregled, izmena i upravljanje stopama i žiro računima doprinosa na teret radnika i poslodavca", helpAnchor: "Doprinosi");

    private void BtnPlatniRazredi_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnPlatniRazredi, "📊 Platni razredi", new Views.PlatniRazredi.PlatniRazrediPage(), "Pregled i izmena najnižih bruto osnovica za stepene stručne spreme", helpAnchor: "PlatniRazredi");

    private void BtnObracuni_Click(object sender, RoutedEventArgs e)
        => NavigateTo(BtnObracuni, "🏢 Pregled svih obračuna", new Views.Obracuni.ObracuniPage(), helpAnchor: "Obracuni");

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        TxtHeaderTitle.Text = "❓ Pomoć";
        TxtHeaderSubtitle.Text = "";
        MainFrame.Navigate(new PomocPage());
    }

    private void OtvoriPomocKontekstualno()
    {
        TxtHeaderTitle.Text = "❓ Pomoć";
        TxtHeaderSubtitle.Text = "";
        MainFrame.Navigate(new PomocPage(_trenutnaSekcijaKljuc));
    }

    private void FirmaBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        NavigateTo(BtnFirme, "🏢 Upravljanje firmama", new Views.Firme.FirmePage(), "Pregled, izmena, unos novih i odabir aktivne firme za obračune i izveštaje");
    }

    private void ActivePeriodBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        NavigateTo(null!, "🏢 Pregled svih obračuna", new Views.Obracuni.ObracuniPage());
    }

    public void NavigateToObracun(int godina, int mesec)
    {
        NavigateTo(BtnObracun, "📊 Obračun plate", new Views.Obracun.ObracunPage(godina, mesec));
    }

    public void RestartujNakonPromeneBaze()
    {
        UcitajImeFirme();
        InicijalizujAktivniPeriod();
        NavigateTo(BtnDashboard, "📊 Radna tabla", new Views.Dashboard.DashboardPage());
    }

    public void OtvoriRadnike()
    {
        NavigateTo(BtnRadnici, "👤 Radnici", new Views.Radnici.RadniciPage());
    }

    public void OtvoriPorezi()
    {
        NavigateTo(BtnPorezi, "⚖️ Porezi i opšti parametri", new Views.Porezi.PoreziPage());
    }
}