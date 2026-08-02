using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Jedan nalog u obliku koji očekuje trezorski ePP. Nazivi svojstava su nazivi polja u
/// fajlu i <b>ne smeju se menjati</b> — po njima se fajl proverava pri učitavanju.
/// Neobavezna polja su <c>null</c> kad se ne koriste, da bi ispala iz zapisa.
/// </summary>
internal sealed class TrezorNalog
{
    public string PaymentBasis { get; set; } = "";
    public int PaymentCode { get; set; }
    public decimal Amount { get; set; }

    public string DebtorBankAccount { get; set; } = "";
    public int? DebtorCodeModel { get; set; }
    public string? DebtorCode { get; set; }

    public string CreditorName { get; set; } = "";
    public string CreditorAddress { get; set; } = "";
    public string CreditorBankAccount { get; set; } = "";
    public int? CreditorCodeModel { get; set; }
    public string? CreditorCode { get; set; }

    public bool UrgentPayment { get; set; }
    public string? ExpectedPaymentDate { get; set; }
}

/// <summary>
/// Izvoz naloga za prenos u fajl za <b>trezorski ePP</b> (Elektronski platni promet Uprave
/// za trezor), koji koriste budžetski korisnici i firme sa računom kod trezora.
///
/// Format je <b>JSON</b>, a ne XML — niz naloga u jednom fajlu. Ograničenja dužina nisu
/// ukrasna: fajl koji ih premaši ePP odbija u celini, pa se proveravaju ovde, dok se još
/// vidi koji nalog je sporan.
/// </summary>
public static class TrezorEppWriter
{
    /// <summary>Najveći broj naloga u jednom fajlu koji ePP prihvata.</summary>
    public const int MaxNalogaPoFajlu = 5000;

    private const int MaxSvrha = 105;
    private const int MaxPozivNaBroj = 23;
    private const int MaxAdresa = 200;

    private static readonly JsonSerializerOptions Opcije = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Ćirilica i dijakritika moraju ostati čitljive, a ne pobegnuti u \uXXXX zapis.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    /// <summary>
    /// Pretvara naloge u JSON za ePP. Nalazi koji bi doveli do odbijanja fajla vraćaju se
    /// kroz <paramref name="nalazi"/> — izvoz se ne prekida, da se sve greške vide odjednom.
    /// </summary>
    public static string Generisi(IReadOnlyList<NalogZaPrenos> nalozi, out IReadOnlyList<NalazProvere> nalazi)
    {
        var pronadjeni = new List<NalazProvere>();

        if (nalozi.Count > MaxNalogaPoFajlu)
        {
            pronadjeni.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Previše naloga u fajlu",
                Opis = $"Fajl sadrži {nalozi.Count} naloga, a ePP prihvata najviše {MaxNalogaPoFajlu}. Podelite isplatu na više fajlova."
            });
        }

        var zapisi = nalozi.Select(n => Pretvori(n, pronadjeni)).ToList();

        nalazi = pronadjeni;
        return JsonSerializer.Serialize(zapisi, Opcije);
    }

    private static TrezorNalog Pretvori(NalogZaPrenos nalog, List<NalazProvere> nalazi)
    {
        void Prijavi(string provera, string opis) => nalazi.Add(new NalazProvere
        {
            Tezina = TezinaNalaza.Greska,
            BrojRadnika = nalog.BrojRadnika,
            Radnik = nalog.PrimalacNaziv,
            Provera = provera,
            Opis = opis
        });

        if (nalog.Svrha.Length > MaxSvrha)
            Prijavi("Svrha predugačka", $"Svrha plaćanja ima {nalog.Svrha.Length} znakova, a dozvoljeno je {MaxSvrha}.");

        if (nalog.PozivNaBroj.Length > MaxPozivNaBroj)
            Prijavi("Poziv na broj predugačak", $"Poziv na broj ima {nalog.PozivNaBroj.Length} znakova, a dozvoljeno je {MaxPozivNaBroj}.");

        // Adresa je obavezna u ePP-u; prazna vrednost obara ceo fajl, ne samo taj nalog.
        if (string.IsNullOrWhiteSpace(nalog.PrimalacAdresa))
            Prijavi("Nedostaje adresa primaoca", "Trezorski ePP traži adresu primaoca kao obavezno polje.");
        else if (nalog.PrimalacAdresa.Length > MaxAdresa)
            Prijavi("Adresa predugačka", $"Adresa primaoca ima {nalog.PrimalacAdresa.Length} znakova, a dozvoljeno je {MaxAdresa}.");

        bool imaPoziv = !string.IsNullOrWhiteSpace(nalog.PozivNaBroj);

        return new TrezorNalog
        {
            PaymentBasis = Skrati(nalog.Svrha, MaxSvrha),
            PaymentCode = ParsirajSifru(nalog.SifraPlacanja),
            Amount = nalog.Iznos,
            DebtorBankAccount = nalog.PlatilacRacun,
            CreditorName = nalog.PrimalacNaziv,
            CreditorAddress = Skrati(nalog.PrimalacAdresa, MaxAdresa),
            CreditorBankAccount = nalog.PrimalacRacun,
            CreditorCodeModel = imaPoziv ? ParsirajModel(nalog.ModelPozivaNaBroj) : null,
            CreditorCode = imaPoziv ? Skrati(nalog.PozivNaBroj, MaxPozivNaBroj) : null,
            UrgentPayment = false,
            ExpectedPaymentDate = nalog.DatumValute.ToString("yyyy-MM-dd'T'00:00:00", CultureInfo.InvariantCulture)
        };
    }

    private static int ParsirajSifru(string sifra)
        => int.TryParse(sifra, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static int? ParsirajModel(string model)
        => int.TryParse(model, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;

    /// <summary>
    /// Skraćuje na dozvoljenu dužinu. Predugačka vrednost je već prijavljena kao greška —
    /// ovo samo sprečava da se generiše fajl koji bi ePP odbio bez objašnjenja.
    /// </summary>
    private static string Skrati(string tekst, int maxDuzina)
        => tekst.Length <= maxDuzina ? tekst : tekst[..maxDuzina];
}
