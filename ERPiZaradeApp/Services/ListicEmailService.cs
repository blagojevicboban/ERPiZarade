using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ERPiZaradeApp.Views.Listici;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Ishod slanja za jednog radnika, onako kako se prikazuje i beleži.</summary>
public sealed class RezultatSlanja
{
    public int BrojRadnika { get; init; }
    public string Radnik { get; init; } = "";
    public string Email { get; init; } = "";
    public IshodSlanja Ishod { get; init; }
    public string? Napomena { get; init; }
}

/// <summary>Zbirni ishod jednog pokretanja slanja.</summary>
public sealed class IzvestajSlanja
{
    public IReadOnlyList<RezultatSlanja> Stavke { get; init; } = [];

    public int Poslato => Stavke.Count(s => s.Ishod == IshodSlanja.Poslato);
    public int Neuspesno => Stavke.Count(s => s.Ishod == IshodSlanja.Neuspesno);
    public int Preskoceno => Stavke.Count(s => s.Ishod == IshodSlanja.Preskoceno);
}

/// <summary>Podešavanja SMTP naloga, odvojena od <c>UserSettings</c> da servis ostane proverljiv.</summary>
public sealed class SmtpPodesavanja
{
    public string Server { get; init; } = "";
    public int Port { get; init; } = 587;
    public bool KoristiSsl { get; init; } = true;
    public string Korisnik { get; init; } = "";
    public string Lozinka { get; init; } = "";
    public string AdresaPosiljaoca { get; init; } = "";
    public string ImePosiljaoca { get; init; } = "";

    public bool JePotpuno =>
        !string.IsNullOrWhiteSpace(Server) &&
        Port > 0 &&
        !string.IsNullOrWhiteSpace(AdresaPosiljaoca);
}

/// <summary>Šalje poruku; izdvojeno da se slanje može proveriti bez pravog SMTP servera.</summary>
public interface IPosiljalac
{
    Task PosaljiAsync(MimeMessage poruka, SmtpPodesavanja podesavanja, CancellationToken token);
}

/// <summary>
/// Slanje preko pravog SMTP servera. Veza se otvara jednom za ceo paket — otvaranje po
/// poruci na desetinama radnika traje neuporedivo duže i deo servera to odbija.
/// </summary>
public sealed class SmtpPosiljalac : IPosiljalac, IDisposable
{
    private SmtpClient? _klijent;

    public async Task PosaljiAsync(MimeMessage poruka, SmtpPodesavanja podesavanja, CancellationToken token)
    {
        if (_klijent is not { IsConnected: true })
        {
            _klijent?.Dispose();
            _klijent = new SmtpClient();

            var bezbednost = podesavanja.KoristiSsl
                ? SecureSocketOptions.StartTlsWhenAvailable
                : SecureSocketOptions.None;

            await _klijent.ConnectAsync(podesavanja.Server, podesavanja.Port, bezbednost, token);

            if (!string.IsNullOrWhiteSpace(podesavanja.Korisnik))
                await _klijent.AuthenticateAsync(podesavanja.Korisnik, podesavanja.Lozinka, token);
        }

        await _klijent.SendAsync(poruka, token);
    }

    public void Dispose()
    {
        if (_klijent is { IsConnected: true })
        {
            try { _klijent.Disconnect(quit: true); } catch { /* veza se svakako zatvara */ }
        }
        _klijent?.Dispose();
        _klijent = null;
    }
}

/// <summary>
/// Slanje platnih listića e-mailom.
///
/// Svako slanje se beleži u <see cref="SlanjeListica"/> — to nije pomoćni log nego obaveza
/// po ZZPL, jer se listićem iznose lični podaci iz kontrolisanog okruženja. Beleži se i
/// neuspeh i preskakanje, da bi se videlo ko listić <b>nije</b> dobio.
/// </summary>
public class ListicEmailService
{
    private readonly PlataDbContext _db;
    private readonly IPosiljalac _posiljalac;

    public ListicEmailService(PlataDbContext db, IPosiljalac posiljalac)
    {
        _db = db;
        _posiljalac = posiljalac;
    }

    public async Task<IzvestajSlanja> PosaljiAsync(
        IEnumerable<ObracunPlate> obracuni,
        SmtpPodesavanja podesavanja,
        bool zastitiLozinkom,
        CancellationToken token = default)
    {
        if (!podesavanja.JePotpuno)
            throw new InvalidOperationException("SMTP podešavanja nisu potpuna — proverite server, port i adresu pošiljaoca.");

        var stavke = new List<RezultatSlanja>();
        var zapisi = new List<SlanjeListica>();
        var korisnik = AppSession.TrenutniKorisnik;

        foreach (var obracun in obracuni)
        {
            token.ThrowIfCancellationRequested();

            var radnik = obracun.Radnik;
            if (radnik == null) continue;

            var stavka = await PosaljiJednom(obracun, radnik, podesavanja, zastitiLozinkom, token);
            stavke.Add(stavka);

            zapisi.Add(new SlanjeListica
            {
                Godina = obracun.Godina,
                Mesec = obracun.Mesec,
                BrojRadnika = stavka.BrojRadnika,
                ImeRadnika = stavka.Radnik,
                Email = stavka.Email,
                Ishod = stavka.Ishod,
                ZasticenLozinkom = zastitiLozinkom && stavka.Ishod == IshodSlanja.Poslato,
                Napomena = stavka.Napomena,
                KorisnikId = korisnik?.Id,
                KorisnickoIme = korisnik?.KorisnickoIme,
                Vreme = DateTime.Now
            });
        }

        _db.SlanjaListica.AddRange(zapisi);
        await _db.SaveChangesAsync(token);

        return new IzvestajSlanja { Stavke = stavke };
    }

    private async Task<RezultatSlanja> PosaljiJednom(
        ObracunPlate obracun,
        Radnik radnik,
        SmtpPodesavanja podesavanja,
        bool zastitiLozinkom,
        CancellationToken token)
    {
        RezultatSlanja Ishod(IshodSlanja ishod, string? napomena = null) => new()
        {
            BrojRadnika = radnik.BrojRadnika,
            Radnik = radnik.ImeIPrezime,
            Email = radnik.Email ?? "",
            Ishod = ishod,
            Napomena = napomena
        };

        if (string.IsNullOrWhiteSpace(radnik.Email))
            return Ishod(IshodSlanja.Preskoceno, "Radnik nema e-mail adresu.");

        string lozinka = PdfZastitaService.PodrazumevanaLozinka(radnik);
        if (zastitiLozinkom && string.IsNullOrWhiteSpace(lozinka))
        {
            // Nezaštićen listić se ne šalje prećutno — to bi obesmislilo samu zaštitu.
            return Ishod(IshodSlanja.Preskoceno,
                "Zaštita lozinkom je uključena, a radnik nema JMBG iz kog se lozinka izvodi.");
        }

        try
        {
            byte[] pdf = PlatniListicDocument.Generisi(obracun);
            if (zastitiLozinkom) pdf = PdfZastitaService.Zastiti(pdf, lozinka);

            var poruka = SastaviPoruku(obracun, radnik, podesavanja, pdf, zastitiLozinkom);
            await _posiljalac.PosaljiAsync(poruka, podesavanja, token);

            return Ishod(IshodSlanja.Poslato);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Neuspeh kod jednog radnika ne sme da prekine slanje ostalima.
            return Ishod(IshodSlanja.Neuspesno, ex.Message);
        }
    }

    private static MimeMessage SastaviPoruku(
        ObracunPlate obracun,
        Radnik radnik,
        SmtpPodesavanja podesavanja,
        byte[] pdf,
        bool zasticen)
    {
        var poruka = new MimeMessage();
        poruka.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(podesavanja.ImePosiljaoca) ? podesavanja.AdresaPosiljaoca : podesavanja.ImePosiljaoca,
            podesavanja.AdresaPosiljaoca));
        poruka.To.Add(MailboxAddress.Parse(radnik.Email));
        poruka.Subject = $"Platni listić za {obracun.Mesec:D2}/{obracun.Godina}";

        string telo =
            $"Poštovani/a {radnik.ImeIPrezime},\n\n" +
            $"U prilogu je Vaš platni listić za {obracun.Mesec:D2}/{obracun.Godina}.\n\n";

        if (zasticen)
        {
            // Sama lozinka se NE navodi u poruci — inače zaštita ne znači ništa.
            telo += "Dokument je zaštićen lozinkom. Lozinka za otvaranje je Vaš JMBG.\n\n";
        }

        telo += "Ovu poruku je poslao program automatski; na nju nije potrebno odgovarati.\n";

        var telo_ = new BodyBuilder { TextBody = telo };
        telo_.Attachments.Add(
            $"Platni_listic_{obracun.Mesec:D2}_{obracun.Godina}.pdf",
            pdf,
            new ContentType("application", "pdf"));

        poruka.Body = telo_.ToMessageBody();
        return poruka;
    }
}
