using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using QuestPDF.Infrastructure;

namespace ERPiZaradeApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        AppLog.Init();
        AppLog.RegistrujGlobalneHandlere(this);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Velopack.VelopackApp.Build().Run();
        base.OnStartup(e);

        // Global QuestPDF license configuration to prevent exception during PDF generation
        QuestPDF.Settings.License = LicenseType.Community;

        // Baza koju je izabrao ERPiHub. Mora se pročitati PRE prvog pristupa
        // AppConfig.DbPath, jer se putanja tamo kešira za ceo životni vek procesa.
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--db-path" && i + 1 < e.Args.Length)
            {
                var customPath = e.Args[i + 1];
                if (File.Exists(customPath))
                {
                    UserSettings.Instance.ActiveDbPath = customPath;
                    UserSettings.Instance.Save();
                    Serilog.Log.Information("Baza zadata iz ERPiHub-a: {Putanja}", customPath);
                }
                else
                {
                    Serilog.Log.Warning("ERPiHub je zadao bazu koja ne postoji: {Putanja}", customPath);
                }
            }
        }

        var loginWindow = new Views.Korisnici.LoginWindow();
        loginWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Zatvori();
        base.OnExit(e);
    }
}

