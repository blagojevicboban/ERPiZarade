using System;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Zaštita platnog listića lozinkom pre slanja e-mailom.
///
/// QuestPDF ne ume da šifruje PDF, pa se gotov dokument otvara PDFsharp-om i ponovo
/// upisuje sa lozinkom. Bez toga bi listić sa JMBG-om i zaradom putovao poštom kao
/// dokument koji čita svako ko dođe do poruke.
/// </summary>
public static class PdfZastitaService
{
    /// <summary>
    /// Podrazumevana lozinka za otvaranje listića — JMBG radnika. Radnik je zna napamet,
    /// ne mora da se dogovara i ne prenosi se istim kanalom kao dokument.
    /// </summary>
    public static string PodrazumevanaLozinka(Radnik radnik) => (radnik.Jmbg ?? "").Trim();

    /// <summary>
    /// Vraća PDF zaštićen lozinkom za otvaranje. Lozinka vlasnika se postavlja na istu
    /// vrednost — bez nje bi PDFsharp dozvolio uklanjanje zaštite bez ikakvog znanja.
    /// </summary>
    public static byte[] Zastiti(byte[] pdf, string lozinka)
    {
        if (string.IsNullOrWhiteSpace(lozinka))
            throw new ArgumentException("Lozinka za zaštitu PDF-a ne može biti prazna.", nameof(lozinka));

        using var ulaz = new MemoryStream(pdf);
        using var dokument = PdfReader.Open(ulaz, PdfDocumentOpenMode.Modify);

        var zastita = dokument.SecuritySettings;
        zastita.UserPassword = lozinka;
        zastita.OwnerPassword = lozinka;
        zastita.PermitModifyDocument = false;
        zastita.PermitExtractContent = false;

        using var izlaz = new MemoryStream();
        dokument.Save(izlaz, closeStream: false);
        return izlaz.ToArray();
    }
}
