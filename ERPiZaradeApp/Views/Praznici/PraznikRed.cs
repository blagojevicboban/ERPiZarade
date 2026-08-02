using System;

namespace ERPiZaradeApp.Views.Praznici;

/// <summary>Red u pregledu mesečnog fonda sati.</summary>
public sealed class FondRed
{
    public required string Mesec { get; init; }
    public int RadniDani { get; init; }
    public int FondSati { get; init; }
    public int BrojPraznika { get; init; }
}

/// <summary>Naziv meseca za prikaz — isti niz koristi i pregled obračuna.</summary>
public static class NaziviMeseci
{
    private static readonly string[] Imena =
    [
        "Januar", "Februar", "Mart", "April", "Maj", "Jun",
        "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar"
    ];

    public static string Za(int mesec)
        => mesec is >= 1 and <= 12 ? Imena[mesec - 1] : mesec.ToString();
}
