using System.Configuration;
using System.Data;
using System.Windows;
using QuestPDF.Infrastructure;

namespace PlataApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global QuestPDF license configuration to prevent exception during PDF generation
        QuestPDF.Settings.License = LicenseType.Community;
    }
}

