using System.IO;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Slanje listića iznosi JMBG i zaradu iz kontrolisanog okruženja, pa testovi drže tri
/// pravila: greška kod jednog radnika ne sme da obori slanje ostalima, nezaštićen listić
/// se ne šalje prećutno, i svako slanje — uključujući neuspeh — mora ostaviti trag.
/// </summary>
public class ListicEmailServiceTests
{
    private const int Godina = 2026;
    private const int Mesec = 3;
    private const string Jmbg = "0101990710016";

    /// <summary>Hvata poruke umesto da ih šalje; može i da glumi pad servera.</summary>
    private sealed class LazniPosiljalac : IPosiljalac
    {
        public List<MimeMessage> Poslate { get; } = [];
        public Func<MimeMessage, Exception?>? PadaZa { get; set; }

        public Task PosaljiAsync(MimeMessage poruka, SmtpPodesavanja podesavanja, CancellationToken token)
        {
            var greska = PadaZa?.Invoke(poruka);
            if (greska != null) throw greska;

            Poslate.Add(poruka);
            return Task.CompletedTask;
        }
    }

    private static readonly SmtpPodesavanja Podesavanja = new()
    {
        Server = "smtp.test.rs",
        Port = 587,
        AdresaPosiljaoca = "obracun@firma.rs",
        ImePosiljaoca = "Obračun zarada"
    };

    private static PlataDbContext NoviKontekst()
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlataDbContext(options);
    }

    private static ObracunPlate DodajObracun(
        PlataDbContext db,
        int id,
        string email = "radnik@firma.rs",
        string jmbg = Jmbg)
    {
        var radnik = new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Jmbg = jmbg,
            Email = email,
            Kategorija = "4",
            Godina = Godina,
            Mesec = Mesec
        };
        db.Radnici.Add(radnik);

        var obracun = new ObracunPlate
        {
            Id = id,
            RadnikId = id,
            Godina = Godina,
            Mesec = Mesec,
            BrutoZarada = 80000m,
            NetoIsplata = 55000m,
            RedovniSati = 176,
            Radnik = radnik
        };
        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    private static async Task<(IzvestajSlanja izvestaj, LazniPosiljalac posiljalac)> Posalji(
        PlataDbContext db,
        IEnumerable<ObracunPlate> obracuni,
        bool zastiti = false,
        Func<MimeMessage, Exception?>? padaZa = null)
    {
        var posiljalac = new LazniPosiljalac { PadaZa = padaZa };
        var servis = new ListicEmailService(db, posiljalac);
        var izvestaj = await servis.PosaljiAsync(obracuni, Podesavanja, zastiti);
        return (izvestaj, posiljalac);
    }

    [Fact]
    public async Task Posalji_DvaRadnika_SaljeObemaIBeleziSlanje()
    {
        using var db = NoviKontekst();
        var prvi = DodajObracun(db, 1, email: "prvi@firma.rs");
        var drugi = DodajObracun(db, 2, email: "drugi@firma.rs");

        var (izvestaj, posiljalac) = await Posalji(db, [prvi, drugi]);

        Assert.Equal(2, izvestaj.Poslato);
        Assert.Equal(2, posiljalac.Poslate.Count);
        Assert.Equal(2, db.SlanjaListica.Count(s => s.Ishod == IshodSlanja.Poslato));
    }

    [Fact]
    public async Task Posalji_PorukaNosiPdfPrilog()
    {
        using var db = NoviKontekst();
        var obracun = DodajObracun(db, 1);

        var (_, posiljalac) = await Posalji(db, [obracun]);

        var prilozi = posiljalac.Poslate.Single().Attachments.ToList();
        Assert.Single(prilozi);
        Assert.Contains("Platni_listic_03_2026.pdf", prilozi[0].ContentDisposition?.FileName);
    }

    /// <summary>Radnik bez adrese se ne prijavljuje kao greška, ali mora ostati vidljiv u evidenciji.</summary>
    [Fact]
    public async Task Posalji_RadnikBezEmaila_SePreskaceIBelezi()
    {
        using var db = NoviKontekst();
        var obracun = DodajObracun(db, 1, email: "");

        var (izvestaj, posiljalac) = await Posalji(db, [obracun]);

        Assert.Equal(1, izvestaj.Preskoceno);
        Assert.Empty(posiljalac.Poslate);

        var zapis = db.SlanjaListica.Single();
        Assert.Equal(IshodSlanja.Preskoceno, zapis.Ishod);
        Assert.Contains("nema e-mail", zapis.Napomena);
    }

    /// <summary>
    /// Ako je zaštita uključena a lozinka se nema odakle izvesti, listić se NE šalje
    /// nezaštićen — to bi obesmislilo zaštitu i iznelo podatke bez ikakve prepreke.
    /// </summary>
    [Fact]
    public async Task Posalji_ZastitaUkljucenaARadnikBezJmbg_NeSaljeNezasticenListic()
    {
        using var db = NoviKontekst();
        var obracun = DodajObracun(db, 1, jmbg: "");

        var (izvestaj, posiljalac) = await Posalji(db, [obracun], zastiti: true);

        Assert.Equal(1, izvestaj.Preskoceno);
        Assert.Empty(posiljalac.Poslate);
        Assert.Contains("JMBG", db.SlanjaListica.Single().Napomena);
    }

    [Fact]
    public async Task Posalji_GreskaKodJednog_NePrekidaSlanjeOstalima()
    {
        using var db = NoviKontekst();
        var prvi = DodajObracun(db, 1, email: "prvi@firma.rs");
        var drugi = DodajObracun(db, 2, email: "drugi@firma.rs");

        var (izvestaj, posiljalac) = await Posalji(db, [prvi, drugi],
            padaZa: p => p.To.ToString().Contains("prvi@firma.rs")
                ? new InvalidOperationException("Server odbio poruku")
                : null);

        Assert.Equal(1, izvestaj.Neuspesno);
        Assert.Equal(1, izvestaj.Poslato);
        Assert.Single(posiljalac.Poslate);

        var neuspeh = db.SlanjaListica.Single(s => s.Ishod == IshodSlanja.Neuspesno);
        Assert.Contains("Server odbio poruku", neuspeh.Napomena);
    }

    /// <summary>Lozinka se ne sme naći u poruci — inače putuje istim kanalom kao dokument.</summary>
    [Fact]
    public async Task Posalji_ZasticenListic_NeNavodiLozinkuUPoruci()
    {
        using var db = NoviKontekst();
        var obracun = DodajObracun(db, 1);

        var (_, posiljalac) = await Posalji(db, [obracun], zastiti: true);

        string telo = posiljalac.Poslate.Single().TextBody ?? "";
        Assert.DoesNotContain(Jmbg, telo);
        Assert.Contains("JMBG", telo);
    }

    [Fact]
    public async Task Posalji_ZasticenListic_SeBeleziKaoZasticen()
    {
        using var db = NoviKontekst();
        var obracun = DodajObracun(db, 1);

        await Posalji(db, [obracun], zastiti: true);

        Assert.True(db.SlanjaListica.Single().ZasticenLozinkom);
    }

    [Fact]
    public async Task Posalji_NepotpunaPodesavanja_Odbija()
    {
        using var db = NoviKontekst();
        var obracun = DodajObracun(db, 1);
        var servis = new ListicEmailService(db, new LazniPosiljalac());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servis.PosaljiAsync([obracun], new SmtpPodesavanja { Server = "" }, zastitiLozinkom: false));
    }
}

/// <summary>Zaštita PDF-a lozinkom pre slanja.</summary>
public class PdfZastitaServiceTests
{
    private static byte[] PrimerPdf()
    {
        var radnik = new Radnik { ImeIPrezime = "Test Radnik", Jmbg = "0101990710016", Kategorija = "4" };
        var obracun = new ObracunPlate
        {
            Godina = 2026,
            Mesec = 3,
            BrutoZarada = 80000m,
            NetoIsplata = 55000m,
            Radnik = radnik
        };
        return ERPiZaradeApp.Views.Listici.PlatniListicDocument.Generisi(obracun);
    }

    [Fact]
    public void Zastiti_VracaPdfKojiSeBezLozinkeNeMozeOtvoriti()
    {
        byte[] zasticen = PdfZastitaService.Zastiti(PrimerPdf(), "tajna123");

        Assert.ThrowsAny<Exception>(() =>
        {
            using var tok = new MemoryStream(zasticen);
            return PdfSharp.Pdf.IO.PdfReader.Open(tok, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);
        });
    }

    [Fact]
    public void Zastiti_SaIspravnomLozinkom_DokumentSeOtvara()
    {
        byte[] zasticen = PdfZastitaService.Zastiti(PrimerPdf(), "tajna123");

        using var tok = new MemoryStream(zasticen);
        using var dokument = PdfSharp.Pdf.IO.PdfReader.Open(
            tok, "tajna123", PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);

        Assert.True(dokument.PageCount > 0);
    }

    [Fact]
    public void Zastiti_PraznaLozinka_Odbija()
    {
        Assert.Throws<ArgumentException>(() => PdfZastitaService.Zastiti(PrimerPdf(), "  "));
    }

    [Fact]
    public void PodrazumevanaLozinka_JeJmbgRadnika()
    {
        var radnik = new Radnik { Jmbg = " 0101990710016 " };
        Assert.Equal("0101990710016", PdfZastitaService.PodrazumevanaLozinka(radnik));
    }
}
