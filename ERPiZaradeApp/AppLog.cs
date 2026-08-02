using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Serilog;

namespace ERPiZaradeApp;

/// <summary>
/// Centralno logovanje aplikacije.
///
/// Zapisi idu u %LOCALAPPDATA%\ERPiFinansijeApp\logs\log-GGGGMMDD.txt, novi fajl svakog dana,
/// zadržava se poslednjih 14 dana. Zamenjuje ranije ručno dopisivanje u crash.log
/// (koji je rastao bez ograničenja) i Debug.WriteLine pozive koji su u Release
/// verziji potpuno nevidljivi — zbog čega se do sada nijedan problem kod korisnika
/// nije mogao dijagnostikovati.
/// </summary>
public static class AppLog
{
    private const string AppName = "ERPiZaradeApp";

    public static string LogFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName, "logs");

    public static void Init()
    {
        Directory.CreateDirectory(LogFolder);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(LogFolder, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var verzija = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        Log.Information("=== Pokretanje {App} v{Verzija} ===", AppName, verzija);
    }

    /// <summary>
    /// Hvata sve što nije uhvaćeno u kodu: izuzetke na UI niti, fatalne izuzetke
    /// pozadinskih niti i neposmatrane Task izuzetke (koji inače tiho ruše proces).
    /// </summary>
    public static void RegistrujGlobalneHandlere(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            // Aplikacija nastavlja rad (Handled = true), pa se logger NE zatvara.
            Log.Error(e.Exception, "Neuhvaćena greška na korisničkom interfejsu");

            MessageBox.Show(
                $"Neočekivana greška:\n\n{e.Exception.Message}\n\nDetalji su zapisani u:\n{LogFolder}",
                "Greška aplikacije", MessageBoxButton.OK, MessageBoxImage.Error);

            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Fatalna greška — aplikacija se zatvara");
            Log.CloseAndFlush();
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Neposmatrana greška u pozadinskom zadatku");
            e.SetObserved();
        };
    }

    public static void Zatvori() => Log.CloseAndFlush();
}
