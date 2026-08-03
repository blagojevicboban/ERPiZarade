using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Jedna kolona koju uvoz prepoznaje, sa upisom u <see cref="RadniSat"/>.</summary>
public sealed class KolonaSati
{
    public required string Naziv { get; init; }
    public required Action<RadniSat, decimal> Upisi { get; init; }

    /// <summary>Celobrojne kolone ne smeju da prime decimalnu vrednost neopaženo.</summary>
    public bool JeCeoBroj { get; init; } = true;
}

/// <summary>Greška vezana za konkretan red — bez broja reda korisnik ne zna šta da ispravi.</summary>
public sealed class GreskaUvoza
{
    public int Red { get; init; }
    public string Kolona { get; init; } = "";
    public string Opis { get; init; } = "";

    public override string ToString()
        => string.IsNullOrEmpty(Kolona)
            ? $"Red {Red}: {Opis}"
            : $"Red {Red}, kolona „{Kolona}“: {Opis}";
}

/// <summary>Pročitan sadržaj fajla pre nego što se bilo šta upiše u bazu.</summary>
public sealed class RezultatUvoza
{
    public IReadOnlyList<RadniSat> Redovi { get; init; } = [];
    public IReadOnlyList<GreskaUvoza> Greske { get; init; } = [];

    /// <summary>Imena kolona iz fajla koje uvoz ne poznaje — prijavljuju se, ali ne blokiraju.</summary>
    public IReadOnlyList<string> NepoznateKolone { get; init; } = [];

    /// <summary>Fajl sa greškama se odbija u celini — delimičan uvoz sati je gori od nijednog.</summary>
    public bool JeIspravan => Greske.Count == 0 && Redovi.Count > 0;
}

/// <summary>
/// Uvoz radnih sati iz Excel (.xlsx) ili CSV fajla.
///
/// Nazivi kolona su isti kao natpisi na ekranu radnih sati — korisnik prepisuje ono što
/// već vidi, umesto da uči zaseban šifarnik. Prepoznavanje ne razlikuje velika i mala slova
/// ni dijakritiku, jer se fajl obično pravi ručno.
///
/// Fajl sa ijednom greškom se odbija u celini: uvezena polovina sati izgleda kao uspeh, a
/// daje pogrešan obračun radnicima iz druge polovine.
/// </summary>
public class UvozSatiService
{
    private readonly PlataDbContext _db;

    public UvozSatiService(PlataDbContext db) => _db = db;

    /// <summary>Kolona po kojoj se red vezuje za radnika.</summary>
    public const string KolonaBrojRadnika = "Broj radnika";

    /// <summary>
    /// Kolone su iste kao u masovnoj izmeni na ekranu radnih sati, da uvoz i ručni unos
    /// pokrivaju isti skup podataka.
    /// </summary>
    public static IReadOnlyList<KolonaSati> Kolone { get; } =
    [
        new() { Naziv = "Redovni sati",        Upisi = (r, v) => r.RedovniSati = (int)v },
        new() { Naziv = "Bolovanje",           Upisi = (r, v) => r.BolovanjeSati = (int)v },
        new() { Naziv = "Prekovremeni",        Upisi = (r, v) => r.PrekovremeneSati = (int)v },
        new() { Naziv = "Godišnji odmor",      Upisi = (r, v) => r.GodisnjiOdmorSati = (int)v },
        new() { Naziv = "Državni praznik",     Upisi = (r, v) => r.DrzavniPraznikSati = (int)v },
        new() { Naziv = "Noćni rad",           Upisi = (r, v) => r.NocniSati = (int)v },
        new() { Naziv = "Smenski rad",         Upisi = (r, v) => r.SmenskiSati = (int)v },
        new() { Naziv = "Rad praznikom",       Upisi = (r, v) => r.RadPraznikomSati = (int)v },
        new() { Naziv = "Noćni rad praznikom", Upisi = (r, v) => r.NocniRadPraznikomSati = (int)v },
        new() { Naziv = "Plaćeno odsustvo",    Upisi = (r, v) => r.PlacenoOdsustvoSati = (int)v },
        new() { Naziv = "Rad nedeljom",        Upisi = (r, v) => r.RadNedeljomSati = (int)v },
        new() { Naziv = "Plaćeno zakonski",    Upisi = (r, v) => r.PlacenoZakonskiSati = (int)v },
        new() { Naziv = "Bolovanje >60 dana",  Upisi = (r, v) => r.BolovanjePreko60Sati = (int)v },
        new() { Naziv = "Porodiljsko",         Upisi = (r, v) => r.PorodiljskoOdsustvoSati = (int)v },
        new() { Naziv = "Bolovanje 100%",      Upisi = (r, v) => r.Bolovanje100Sati = (int)v },
        new() { Naziv = "Topli obrok",         Upisi = (r, v) => r.TopliObrokDani = (int)v },
        new() { Naziv = "Regres",              Upisi = (r, v) => r.RegresIznos = v,   JeCeoBroj = false },
        new() { Naziv = "Stimulacija",         Upisi = (r, v) => r.Stimulacija = v,   JeCeoBroj = false },
        new() { Naziv = "Bruto dodatak",       Upisi = (r, v) => r.Varijabila = v,    JeCeoBroj = false },
        new() { Naziv = "Prosek",              Upisi = (r, v) => r.Prosek = v,        JeCeoBroj = false }
    ];

    // ── Čitanje ──────────────────────────────────────────────────────

    public RezultatUvoza Procitaj(string putanja, int godina, int mesec)
    {
        var tabela = Path.GetExtension(putanja).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ProcitajExcel(putanja)
            : ProcitajCsv(putanja);

        return Obradi(tabela, godina, mesec);
    }

    private static List<string[]> ProcitajExcel(string putanja)
    {
        using var radnaSveska = new XLWorkbook(putanja);
        var list = radnaSveska.Worksheets.First();

        var redovi = new List<string[]>();
        foreach (var red in list.RowsUsed())
        {
            int poslednja = red.LastCellUsed()?.Address.ColumnNumber ?? 0;
            redovi.Add(Enumerable.Range(1, poslednja)
                .Select(i => red.Cell(i).GetFormattedString().Trim())
                .ToArray());
        }
        return redovi;
    }

    private static List<string[]> ProcitajCsv(string putanja)
    {
        var linije = File.ReadAllLines(putanja, System.Text.Encoding.UTF8)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (linije.Count == 0) return [];

        // Tačka-zarez je podrazumevani razdvajač u domaćem Excel-u, jer je zarez decimalni.
        char razdvajac = linije[0].Count(z => z == ';') >= linije[0].Count(z => z == ',') ? ';' : ',';

        return linije.Select(l => l.Split(razdvajac).Select(p => p.Trim().Trim('"')).ToArray()).ToList();
    }

    private RezultatUvoza Obradi(List<string[]> tabela, int godina, int mesec)
    {
        var greske = new List<GreskaUvoza>();

        if (tabela.Count < 2)
        {
            greske.Add(new GreskaUvoza { Red = 0, Opis = "Fajl je prazan ili sadrži samo zaglavlje." });
            return new RezultatUvoza { Greske = greske };
        }

        var zaglavlje = tabela[0];
        var mapa = new Dictionary<int, KolonaSati>();
        var nepoznate = new List<string>();
        int kolonaBroja = -1;

        for (int i = 0; i < zaglavlje.Length; i++)
        {
            string naslov = zaglavlje[i];
            if (string.IsNullOrWhiteSpace(naslov)) continue;

            if (Isti(naslov, KolonaBrojRadnika))
            {
                kolonaBroja = i;
                continue;
            }

            var kolona = Kolone.FirstOrDefault(k => Isti(naslov, k.Naziv));
            if (kolona != null) mapa[i] = kolona;
            else nepoznate.Add(naslov);
        }

        if (kolonaBroja < 0)
        {
            greske.Add(new GreskaUvoza
            {
                Red = 1,
                Opis = $"Zaglavlje nema kolonu „{KolonaBrojRadnika}“, pa se redovi ne mogu vezati za radnike."
            });
            return new RezultatUvoza { Greske = greske, NepoznateKolone = nepoznate };
        }

        if (mapa.Count == 0)
        {
            greske.Add(new GreskaUvoza { Red = 1, Opis = "Zaglavlje ne sadrži nijednu prepoznatu kolonu sa satima." });
            return new RezultatUvoza { Greske = greske, NepoznateKolone = nepoznate };
        }

        var radniciPerioda = _db.Radnici
            .Where(r => r.Godina == godina && r.Mesec == mesec)
            .ToDictionary(r => r.BrojRadnika, r => r);

        var redovi = new List<RadniSat>();
        var vidjeni = new HashSet<int>();

        for (int i = 1; i < tabela.Count; i++)
        {
            int brojReda = i + 1;   // u fajlu se broji od 1, a prvi red je zaglavlje
            var celije = tabela[i];

            if (celije.All(string.IsNullOrWhiteSpace)) continue;

            string tekstBroja = kolonaBroja < celije.Length ? celije[kolonaBroja] : "";
            if (!int.TryParse(tekstBroja, NumberStyles.Integer, CultureInfo.InvariantCulture, out int brojRadnika))
            {
                greske.Add(new GreskaUvoza
                {
                    Red = brojReda,
                    Kolona = KolonaBrojRadnika,
                    Opis = $"„{tekstBroja}“ nije broj radnika."
                });
                continue;
            }

            if (!radniciPerioda.TryGetValue(brojRadnika, out var radnik))
            {
                greske.Add(new GreskaUvoza
                {
                    Red = brojReda,
                    Kolona = KolonaBrojRadnika,
                    Opis = $"Radnik {brojRadnika} ne postoji u periodu {mesec:D2}/{godina}."
                });
                continue;
            }

            if (!vidjeni.Add(brojRadnika))
            {
                greske.Add(new GreskaUvoza
                {
                    Red = brojReda,
                    Kolona = KolonaBrojRadnika,
                    Opis = $"Radnik {brojRadnika} se u fajlu pojavljuje više puta."
                });
                continue;
            }

            var sati = new RadniSat { RadnikId = radnik.Id, Godina = godina, Mesec = mesec };
            bool ispravan = true;

            foreach (var (indeks, kolona) in mapa)
            {
                string vrednost = indeks < celije.Length ? celije[indeks] : "";
                if (string.IsNullOrWhiteSpace(vrednost)) continue;

                if (!ParsirajBroj(vrednost, out decimal broj))
                {
                    greske.Add(new GreskaUvoza { Red = brojReda, Kolona = kolona.Naziv, Opis = $"„{vrednost}“ nije broj." });
                    ispravan = false;
                    continue;
                }

                if (broj < 0)
                {
                    greske.Add(new GreskaUvoza { Red = brojReda, Kolona = kolona.Naziv, Opis = "Vrednost ne može biti negativna." });
                    ispravan = false;
                    continue;
                }

                if (kolona.JeCeoBroj && broj != Math.Truncate(broj))
                {
                    greske.Add(new GreskaUvoza
                    {
                        Red = brojReda,
                        Kolona = kolona.Naziv,
                        Opis = $"Sati se unose kao ceo broj, a uneto je {broj}."
                    });
                    ispravan = false;
                    continue;
                }

                kolona.Upisi(sati, broj);
            }

            if (ispravan) redovi.Add(sati);
        }

        return new RezultatUvoza { Redovi = redovi, Greske = greske, NepoznateKolone = nepoznate };
    }

    /// <summary>
    /// Prihvata i „7,5" i „7.5" — fajl često dolazi iz tuđe tabele sa drugim razdvajačem.
    ///
    /// Kulturi se ovde ne sme prepustiti odluka: <c>decimal.Parse("5000,50")</c> u invariant
    /// kulturi daje <b>500050</b>, jer zarez tumači kao razdvajač hiljada. Zato se razdvajači
    /// razvrstavaju izričito: poslednji razdvajač je decimalni, osim ako iza njega stoje tačno
    /// tri cifre — tada je razdvajač hiljada („1.234" je 1234, a „5000.50" je 5000,50).
    /// </summary>
    internal static bool ParsirajBroj(string tekst, out decimal broj)
    {
        broj = 0m;

        string s = tekst.Trim().Replace(" ", "").Replace(" ", "");
        if (s.Length == 0) return false;

        int poslednji = Math.Max(s.LastIndexOf('.'), s.LastIndexOf(','));

        string zaParsiranje;
        if (poslednji < 0)
        {
            zaParsiranje = s;
        }
        else
        {
            int cifaraPosle = s.Length - poslednji - 1;
            bool jeDecimalni = cifaraPosle is > 0 and not 3;

            string ceo = s[..poslednji].Replace(".", "").Replace(",", "");
            string ostatak = s[(poslednji + 1)..];

            zaParsiranje = jeDecimalni ? $"{ceo}.{ostatak}" : ceo + ostatak;
        }

        return decimal.TryParse(zaParsiranje, NumberStyles.Number, CultureInfo.InvariantCulture, out broj);
    }

    /// <summary>Poređenje naziva kolone bez obzira na velika slova, dijakritiku i razmake.</summary>
    private static bool Isti(string a, string b)
        => Uprosti(a).Equals(Uprosti(b), StringComparison.OrdinalIgnoreCase);

    private static string Uprosti(string tekst)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char z in tekst.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(z) || z == '_' || z == '.') continue;
            sb.Append(z switch
            {
                'č' or 'ć' => 'c',
                'ž' => 'z',
                'š' => 's',
                'đ' => 'd',
                _ => z
            });
        }
        return sb.ToString();
    }

    // ── Upis ─────────────────────────────────────────────────────────

    /// <summary>
    /// Upisuje pročitane sate, zamenjujući postojeći zapis za istog radnika i period —
    /// isti ishod kao da su uneti ručno.
    /// </summary>
    public int Primeni(RezultatUvoza rezultat, int godina, int mesec)
    {
        if (!rezultat.JeIspravan)
            throw new InvalidOperationException("Uvoz se ne može primeniti dok fajl sadrži greške.");

        var postojeci = _db.RadniSati
            .Where(s => s.Godina == godina && s.Mesec == mesec)
            .ToDictionary(s => s.RadnikId, s => s);

        foreach (var novi in rezultat.Redovi)
        {
            if (postojeci.TryGetValue(novi.RadnikId, out var stari))
                _db.RadniSati.Remove(stari);

            _db.RadniSati.Add(novi);
        }

        _db.SaveChanges();
        return rezultat.Redovi.Count;
    }

    // ── Šablon ───────────────────────────────────────────────────────

    /// <summary>
    /// Pravi .xlsx sa zaglavljem i već upisanim radnicima perioda. Bez toga korisnik
    /// pogađa nazive kolona, pa prvi uvoz po pravilu padne na zaglavlju.
    /// </summary>
    public void SacuvajSablon(string putanja, int godina, int mesec)
    {
        using var radnaSveska = new XLWorkbook();
        var list = radnaSveska.AddWorksheet($"Sati {mesec:D2}-{godina}");

        list.Cell(1, 1).Value = KolonaBrojRadnika;
        list.Cell(1, 2).Value = "Ime i prezime";
        for (int i = 0; i < Kolone.Count; i++)
            list.Cell(1, i + 3).Value = Kolone[i].Naziv;

        list.Row(1).Style.Font.Bold = true;

        var radnici = _db.Radnici
            .Where(r => r.Godina == godina && r.Mesec == mesec && r.Aktivan && !r.VanRadnogOdnosa)
            .OrderBy(r => r.BrojRadnika)
            .ToList();

        for (int i = 0; i < radnici.Count; i++)
        {
            list.Cell(i + 2, 1).Value = radnici[i].BrojRadnika;
            list.Cell(i + 2, 2).Value = radnici[i].ImeIPrezime;
        }

        // Kolona sa imenom je samo pomoć pri unosu; uvoz je ne čita i ne mora da se popunjava.
        list.Columns().AdjustToContents();
        radnaSveska.SaveAs(putanja);
    }
}
