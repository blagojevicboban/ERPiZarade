using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

// ── Oblik zapisa u fajlu ─────────────────────────────────────────────
// Nazivi svojstava su nazivi polja u fajlu. Menjaju se samo uz podizanje broja verzije
// formata — ERPiFinansije ih čita po imenu.

internal sealed class KnjizenjeFajl
{
    public string Format { get; set; } = NalogKnjizenjaWriter.OznakaFormata;
    public int Verzija { get; set; } = NalogKnjizenjaWriter.VerzijaFormata;
    public string Izvor { get; set; } = "";
    public KnjizenjeFirma? Firma { get; set; }
    public KnjizenjeNalog Nalog { get; set; } = new();
}

internal sealed class KnjizenjeFirma
{
    public string Naziv { get; set; } = "";
    public string? Pib { get; set; }
    public string? MaticniBroj { get; set; }
}

internal sealed class KnjizenjeNalog
{
    public string VrstaNaloga { get; set; } = "Zarade";
    public string Datum { get; set; } = "";
    public string Opis { get; set; } = "";
    public int Godina { get; set; }
    public int Mesec { get; set; }
    public int RedniBrojIsplate { get; set; }
    public decimal UkupnoDuguje { get; set; }
    public decimal UkupnoPotrazuje { get; set; }
    public List<KnjizenjeStavka> Stavke { get; set; } = [];
}

internal sealed class KnjizenjeStavka
{
    public int RedniBroj { get; set; }
    public string Konto { get; set; } = "";
    public string Opis { get; set; } = "";
    public decimal Duguje { get; set; }
    public decimal Potrazuje { get; set; }

    /// <summary>Šifra mesta troška; izostaje iz zapisa kad je nema.</summary>
    public string? MestoTroska { get; set; }
}

/// <summary>
/// Izvoz naloga za knjiženje u fajl koji ERPiFinansije uvozi u glavnu knjigu (Faza 3.1).
///
/// Format je <b>JSON</b> i namerno preslikava <c>Nalog</c> i <c>StavkaNaloga</c> iz
/// ERPiFinansije: uvoz je prepisivanje polja, bez ijednog računanja. Sve što bi se pri
/// prenosu računalo bilo bi drugo mesto koje ume da se raziđe sa obračunom.
///
/// Uz JSON stoji i <b>CSV</b>, koji nije zamena nego provera: knjigovođa ga otvori u
/// tabeli i uporedi sa rekapitulacijom pre nego što nalog uđe u knjige. Iznosi u njemu su
/// isti, formatirani po srpskom zapisu decimala.
/// </summary>
public static class NalogKnjizenjaWriter
{
    /// <summary>Oznaka po kojoj uvoz prepoznaje fajl.</summary>
    public const string OznakaFormata = "ERPi-nalog-za-knjizenje";

    /// <summary>Broj verzije formata; menja se kad se promeni značenje nekog polja.</summary>
    public const int VerzijaFormata = 1;

    private static readonly JsonSerializerOptions Opcije = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Naši znakovi moraju ostati čitljivi i kad se fajl otvori u običnom uređivaču.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    /// <summary>
    /// Zapisuje nalog kao JSON za uvoz u ERPiFinansije.
    /// </summary>
    /// <param name="nalazi">
    /// Šta bi uvoz odbio. Nalog koji nije u ravnoteži se prijavljuje ovde, a ne tek u
    /// glavnoj knjizi — tamo se više ne vidi iz kog obračuna je razlika došla.
    /// </param>
    public static string Generisi(NalogZaKnjizenje nalog, Firma? firma, out IReadOnlyList<NalazProvere> nalazi)
    {
        var lista = new List<NalazProvere>();
        nalazi = lista;

        if (nalog.Stavke.Count == 0)
        {
            lista.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nalog je prazan",
                Opis = "Nalog nema nijednu stavku, pa nema šta da se knjiži."
            });
        }

        if (!nalog.JeUravnotezen)
        {
            lista.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nalog nije u ravnoteži",
                Opis = $"Razlika duguje i potražuje je {nalog.Razlika:N2}. Glavna knjiga takav nalog odbija."
            });
        }

        foreach (var s in nalog.Stavke.Where(s => string.IsNullOrWhiteSpace(s.Konto)))
        {
            lista.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Stavka bez konta",
                Opis = $"Stavka {s.RedniBroj} („{s.Opis}“) nema broj konta."
            });
        }

        var fajl = new KnjizenjeFajl
        {
            Izvor = $"ERPiZarade {Verzija()}",
            Firma = firma == null ? null : new KnjizenjeFirma
            {
                Naziv = firma.Naziv,
                Pib = Prazno(firma.Pib),
                MaticniBroj = Prazno(firma.Mb)
            },
            Nalog = new KnjizenjeNalog
            {
                Datum = nalog.Datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Opis = nalog.Opis,
                Godina = nalog.Godina,
                Mesec = nalog.Mesec,
                RedniBrojIsplate = nalog.RedniBrojIsplate,
                UkupnoDuguje = nalog.UkupnoDuguje,
                UkupnoPotrazuje = nalog.UkupnoPotrazuje,
                Stavke = nalog.Stavke.Select(s => new KnjizenjeStavka
                {
                    RedniBroj = s.RedniBroj,
                    Konto = s.Konto,
                    Opis = s.Opis,
                    Duguje = s.Duguje,
                    Potrazuje = s.Potrazuje,
                    MestoTroska = Prazno(s.MestoTroska)
                }).ToList()
            }
        };

        return JsonSerializer.Serialize(fajl, Opcije);
    }

    /// <summary>
    /// Isti nalog u CSV obliku, za proveru u tabeli. Razdvajač je tačka-zarez, jer se
    /// decimale pišu zarezom — sa zarezom kao razdvajačem bi se kolone raspale.
    /// </summary>
    public static string GenerisiCsv(NalogZaKnjizenje nalog)
    {
        var sb = new StringBuilder();
        var kultura = CultureInfo.GetCultureInfo("sr-Latn-RS");

        sb.AppendLine("Redni broj;Konto;Opis;Duguje;Potražuje;Mesto troška");

        foreach (var s in nalog.Stavke)
        {
            sb.Append(s.RedniBroj.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(Polje(s.Konto)).Append(';')
              .Append(Polje(s.Opis)).Append(';')
              .Append(s.Duguje.ToString("N2", kultura)).Append(';')
              .Append(s.Potrazuje.ToString("N2", kultura)).Append(';')
              .AppendLine(Polje(s.MestoTroska));
        }

        sb.Append("UKUPNO;;;")
          .Append(nalog.UkupnoDuguje.ToString("N2", kultura)).Append(';')
          .Append(nalog.UkupnoPotrazuje.ToString("N2", kultura))
          .AppendLine(";");

        return sb.ToString();
    }

    /// <summary>Tačka-zarez i navodnik u tekstu bi razbili kolonu, pa se polje navodi.</summary>
    private static string Polje(string? vrednost)
    {
        string v = vrednost ?? "";
        if (v.IndexOfAny([';', '"', '\n', '\r']) < 0) return v;

        return '"' + v.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    private static string? Prazno(string? vrednost)
        => string.IsNullOrWhiteSpace(vrednost) ? null : vrednost.Trim();

    /// <summary>Verzija programa koji je nalog napravio — trag za slučaj da se format menja.</summary>
    private static string Verzija()
        => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
}
