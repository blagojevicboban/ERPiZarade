using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Testovi ne prolaze kroz <c>App.OnStartup</c>, gde se licenca QuestPDF-a inače postavlja,
/// pa bi svako generisanje PDF-a u testu palo na proveri licence. Postavlja se jednom po
/// učitavanju test asemblija.
/// </summary>
internal static class TestUskladjivanje
{
    [ModuleInitializer]
    internal static void Postavi()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
