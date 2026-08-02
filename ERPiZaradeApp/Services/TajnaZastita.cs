using System;
using System.Security.Cryptography;
using System.Text;

namespace ERPiZaradeApp;

/// <summary>
/// Šifrovanje osetljivih vrednosti (za sada SMTP lozinke) pre upisa u <c>settings.json</c>.
///
/// Koristi Windows DPAPI vezan za korisnički nalog: vrednost može da dešifruje samo isti
/// Windows nalog na istom računaru. Prenos <c>settings.json</c> na drugi računar zato ne
/// prenosi i lozinku — to je namerno, jer je fajl inače običan tekst u profilu korisnika.
/// </summary>
public static class TajnaZastita
{
    /// <summary>Dodatni ulaz u ključ — bez njega bi svaka DPAPI vrednost istog naloga bila zamenljiva.</summary>
    private static readonly byte[] Entropija = Encoding.UTF8.GetBytes("ERPiZarade.Smtp.v1");

    public static string? Zastiti(string? otvorenTekst)
    {
        if (string.IsNullOrEmpty(otvorenTekst)) return null;

        try
        {
            byte[] sifrovano = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(otvorenTekst), Entropija, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(sifrovano);
        }
        catch (CryptographicException)
        {
            // Bez zaštite radije nema lozinke nego lozinke u čitljivom obliku.
            return null;
        }
    }

    public static string Otkrij(string? zasticenTekst)
    {
        if (string.IsNullOrEmpty(zasticenTekst)) return "";

        try
        {
            byte[] otvoreno = ProtectedData.Unprotect(
                Convert.FromBase64String(zasticenTekst), Entropija, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(otvoreno);
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            // Vrednost je šifrovana na drugom nalogu ili računaru — tretira se kao neuneta.
            return "";
        }
    }
}
