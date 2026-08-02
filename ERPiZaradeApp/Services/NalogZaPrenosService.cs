using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Vrsta naloga za prenos u paketu isplate zarada.</summary>
public enum VrstaNaloga
{
    /// <summary>Neto zarada na tekući račun radnika.</summary>
    NetoZarada = 0,

    /// <summary>Jedinstvena uplata poreza i doprinosa po odbitku na objedinjeni račun.</summary>
    ObjedinjenaNaplata = 1,

    /// <summary>Obustava (kredit, sudska zabrana, izdržavanje) na račun primaoca obustave.</summary>
    Obustava = 2
}

/// <summary>
/// Jedan nalog za prenos, nezavisan od formata bankarske aplikacije. Zapisivači za
/// Halcom (TXT), Asseco (XML) i ROL-XML rade nad ovim tipom — formati se razlikuju,
/// sadržaj naloga ne.
/// </summary>
public sealed class NalogZaPrenos
{
    public VrstaNaloga Vrsta { get; init; }

    public string PlatilacNaziv { get; init; } = "";
    public string PlatilacRacun { get; init; } = "";

    public string PrimalacNaziv { get; init; } = "";
    public string PrimalacRacun { get; init; } = "";

    /// <summary>Adresa primaoca — trezorski ePP je traži kao obavezno polje.</summary>
    public string PrimalacAdresa { get; init; } = "";

    public decimal Iznos { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Šifra plaćanja, nije URL.")]
    public string SifraPlacanja { get; init; } = "";

    public string ModelPozivaNaBroj { get; init; } = "";
    public string PozivNaBroj { get; init; } = "";

    public string Svrha { get; init; } = "";
    public DateTime DatumValute { get; init; }

    /// <summary>Broj radnika kad se nalog odnosi na jednog radnika — za uparivanje sa obračunom.</summary>
    public int? BrojRadnika { get; init; }
}

/// <summary>Rezultat pripreme naloga, sa kontrolama koje moraju proći pre slanja u banku.</summary>
public sealed class PaketNaloga
{
    public int Godina { get; init; }
    public int Mesec { get; init; }
    public IReadOnlyList<NalogZaPrenos> Nalozi { get; init; } = [];
    public IReadOnlyList<NalazProvere> Nalazi { get; init; } = [];

    public decimal ZbirZarada => Nalozi.Where(n => n.Vrsta == VrstaNaloga.NetoZarada).Sum(n => n.Iznos);
    public decimal ZbirObustava => Nalozi.Where(n => n.Vrsta == VrstaNaloga.Obustava).Sum(n => n.Iznos);
    public decimal ZbirPorezaIDoprinosa => Nalozi.Where(n => n.Vrsta == VrstaNaloga.ObjedinjenaNaplata).Sum(n => n.Iznos);
    public decimal Ukupno => Nalozi.Sum(n => n.Iznos);

    public int BrojGresaka => Nalazi.Count(n => n.Tezina == TezinaNalaza.Greska);
    public bool SmeSePoslatiUBanku => BrojGresaka == 0 && Nalozi.Count > 0;
}

/// <summary>
/// Priprema naloga za prenos za jedan obračunski period.
///
/// Od 01.03.2014. porezi i doprinosi po odbitku se ne plaćaju pojedinačno po vrsti, nego
/// <b>jednom uplatom</b> na objedinjeni račun, sa BOP-om iz prihvaćene PPP-PD prijave kao
/// pozivom na broj. Zato se koordinate plaćanja iz šifarnika <c>Doprinos</c> i <c>Porezi</c>
/// (ZiroRacun, PozivNaB, SifPlac) ovde <b>ne koriste</b> — one su ostatak ranijeg režima i
/// dale bi osam naloga umesto jednog.
/// </summary>
public class NalogZaPrenosService
{
    /// <summary>Šifra plaćanja za isplatu zarada.</summary>
    public const string SifraPlacanjaZarade = "240";

    /// <summary>Šifra plaćanja za poreze i doprinose.</summary>
    public const string SifraPlacanjaPorezi = "254";

    private readonly PlataDbContext _db;

    public NalogZaPrenosService(PlataDbContext db) => _db = db;

    /// <summary>
    /// Formira nalog za svaku neto zaradu i jedan zbirni nalog za objedinjenu naplatu.
    /// </summary>
    /// <param name="prijava">
    /// Prihvaćena PPP-PD prijava. Bez nje se nalog za poreze i doprinose ne formira —
    /// uplata bez BOP-a se ne može povezati sa prijavom i ostaje neraspoređena.
    /// </param>
    public PaketNaloga Pripremi(int godina, int mesec, PppPdPrijava? prijava, DateTime datumValute)
    {
        var nalazi = new List<NalazProvere>();
        var nalozi = new List<NalogZaPrenos>();

        var firma = _db.Firme.AsNoTracking().FirstOrDefault();
        if (firma == null || string.IsNullOrWhiteSpace(firma.BankovniRacun))
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nedostaje račun firme",
                Opis = "Tekući račun firme nije popunjen u kartonu firme, pa nalozi nemaju platioca."
            });
        }

        string platilacNaziv = firma?.Naziv ?? "";
        string platilacRacun = firma?.BankovniRacun ?? "";

        var obracuni = _db.ObracuniPlata
            .AsNoTracking()
            .Include(o => o.Radnik)
            .Where(o => o.Godina == godina && o.Mesec == mesec)
            .ToList();

        if (obracuni.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Prazan period",
                Opis = $"Za period {mesec:D2}/{godina} ne postoji nijedan obračun."
            });

            return new PaketNaloga { Godina = godina, Mesec = mesec, Nalozi = nalozi, Nalazi = nalazi };
        }

        string svrhaZarade = $"Isplata zarade za {mesec:D2}/{godina}";

        foreach (var o in obracuni.OrderBy(o => o.Radnik?.BrojRadnika ?? int.MaxValue))
        {
            if (o.NetoIsplata <= 0) continue;

            var radnik = o.Radnik;
            if (radnik == null) continue;

            if (string.IsNullOrWhiteSpace(radnik.BankovniRacun))
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    BrojRadnika = radnik.BrojRadnika,
                    Radnik = radnik.ImeIPrezime,
                    Provera = "Nedostaje tekući račun",
                    Opis = $"Neto {o.NetoIsplata:N2} se ne može isplatiti — radnik nema tekući račun."
                });
                continue;
            }

            nalozi.Add(new NalogZaPrenos
            {
                Vrsta = VrstaNaloga.NetoZarada,
                PlatilacNaziv = platilacNaziv,
                PlatilacRacun = platilacRacun,
                PrimalacNaziv = radnik.ImeIPrezime,
                PrimalacRacun = radnik.BankovniRacun,
                PrimalacAdresa = $"{radnik.AdresaStanovanja}; {radnik.Mesto}".Trim(' ', ';'),
                Iznos = o.NetoIsplata,
                SifraPlacanja = SifraPlacanjaZarade,
                Svrha = svrhaZarade,
                DatumValute = datumValute,
                BrojRadnika = radnik.BrojRadnika
            });
        }

        DodajNalogObjedinjeneNaplate(obracuni, prijava, platilacNaziv, platilacRacun, datumValute, godina, mesec, nalozi, nalazi);

        ProveriRavnotezuZarada(obracuni, nalozi, nalazi);

        return new PaketNaloga { Godina = godina, Mesec = mesec, Nalozi = nalozi, Nalazi = nalazi };
    }

    private static void DodajNalogObjedinjeneNaplate(
        List<ObracunPlate> obracuni,
        PppPdPrijava? prijava,
        string platilacNaziv,
        string platilacRacun,
        DateTime datumValute,
        int godina,
        int mesec,
        List<NalogZaPrenos> nalozi,
        List<NalazProvere> nalazi)
    {
        decimal nasZbir = obracuni.Sum(o =>
            o.PorezNaDohodak +
            o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik +
            o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac);

        if (prijava == null || string.IsNullOrWhiteSpace(prijava.Bop))
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nedostaje BOP",
                Opis = $"Porezi i doprinosi ({nasZbir:N2}) se ne mogu uplatiti bez BOP-a iz prihvaćene PPP-PD prijave. " +
                       "Učitajte dokument koji ePorezi izda po prihvatanju prijave."
            });
            return;
        }

        // Iznos koji je utvrdila Poreska uprava je merodavan; naš zbir služi samo za proveru.
        decimal iznos = prijava.IznosZaUplatu > 0 ? prijava.IznosZaUplatu : nasZbir;

        if (prijava.IznosZaUplatu > 0 && Math.Abs(prijava.IznosZaUplatu - nasZbir) >= 0.01m)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Iznos prijave se ne slaže sa obračunom",
                Opis = $"Poreska uprava traži {prijava.IznosZaUplatu:N2}, a zbir poreza i doprinosa iz obračuna je {nasZbir:N2} " +
                       $"(razlika {prijava.IznosZaUplatu - nasZbir:N2}). Prijava i obračun nisu isti."
            });
        }

        if (prijava.Status != StatusPrijave.Prihvacena)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Upozorenje,
                Provera = "Prijava nije prihvaćena",
                Opis = $"Status prijave je „{prijava.Status}“. BOP važi tek kad Poreska uprava prijavu prihvati."
            });
        }

        nalozi.Add(new NalogZaPrenos
        {
            Vrsta = VrstaNaloga.ObjedinjenaNaplata,
            PlatilacNaziv = platilacNaziv,
            PlatilacRacun = platilacRacun,
            PrimalacNaziv = "Objedinjena naplata poreza i doprinosa po odbitku",
            PrimalacRacun = string.IsNullOrWhiteSpace(prijava.RacunZaUplatu)
                ? EPoreziImportService.PodrazumevaniRacunObjedinjeneNaplate
                : prijava.RacunZaUplatu,
            Iznos = iznos,
            SifraPlacanja = SifraPlacanjaPorezi,
            ModelPozivaNaBroj = string.IsNullOrWhiteSpace(prijava.ModelPozivaNaBroj)
                ? EPoreziImportService.PodrazumevaniModel
                : prijava.ModelPozivaNaBroj,
            PozivNaBroj = prijava.Bop,
            Svrha = string.IsNullOrWhiteSpace(prijava.SvrhaUplate)
                ? $"Porezi i doprinosi po odbitku za {mesec:D2}/{godina}"
                : prijava.SvrhaUplate,
            DatumValute = datumValute
        });
    }

    /// <summary>
    /// Zbir naloga za zarade mora biti jednak zbiru neto isplata iz obračuna. Ako nije,
    /// negde je radnik ispao — a to se u banci vidi tek kad mu plata ne stigne.
    /// </summary>
    private static void ProveriRavnotezuZarada(
        List<ObracunPlate> obracuni,
        List<NalogZaPrenos> nalozi,
        List<NalazProvere> nalazi)
    {
        decimal ocekivano = obracuni.Where(o => o.NetoIsplata > 0).Sum(o => o.NetoIsplata);
        decimal uNalozima = nalozi.Where(n => n.Vrsta == VrstaNaloga.NetoZarada).Sum(n => n.Iznos);

        if (Math.Abs(ocekivano - uNalozima) >= 0.01m)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Zbir naloga se ne slaže sa obračunom",
                Opis = $"Zbir naloga za zarade je {uNalozima:N2}, a zbir neto isplata iz obračuna {ocekivano:N2} " +
                       $"(razlika {ocekivano - uNalozima:N2})."
            });
        }
    }
}
