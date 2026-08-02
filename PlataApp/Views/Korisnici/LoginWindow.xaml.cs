using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PlataData;

namespace PlataApp.Views.Korisnici;

public partial class LoginWindow : Window
{
    private readonly PlataDbContext _db;

    public LoginWindow()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        LoadCompanyInfo();

#if DEBUG
        TxtUsername.Text = "admin";
        TxtPassword.Password = "admin";
#endif
        TxtUsername.Focus();

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        TxtVersion.Text = $"ERPi © 2026 Blagojević Boban - v{version?.ToString(3)}";
    }

    private void LoadCompanyInfo()
    {
        var firma = AppConfig.ActiveFirmaId.HasValue
            ? _db.Firme.Find(AppConfig.ActiveFirmaId.Value)
            : null;
        firma ??= _db.Firme.FirstOrDefault();

        TxtFirma.Text = !string.IsNullOrWhiteSpace(firma?.Naziv) ? firma.Naziv : "Nije dostupna kompanija";
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DoLogin();
        }
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        DoLogin();
    }

    private void DoLogin()
    {
        TxtError.Visibility = Visibility.Collapsed;
        var username = TxtUsername.Text.Trim();
        var password = TxtPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Unesite korisničko ime i lozinku.");
            return;
        }

        var korisnik = _db.Korisnici.FirstOrDefault(k => k.KorisnickoIme == username);

        if (korisnik == null || !PlataDbContext.VerifyPassword(password, korisnik.LozinkaHash))
        {
            ShowError("Pogrešno korisničko ime ili lozinka.");
            return;
        }

        if (!korisnik.JeAktivan)
        {
            ShowError("Vaš nalog je deaktiviran. Obratite se administratoru.");
            return;
        }

        AppSession.TrenutniKorisnik = korisnik;
        korisnik.PoslednjaPrijava = DateTime.Now;
        _db.SaveChanges();
        _db.Dispose();

        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();

        Close();
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }
}
