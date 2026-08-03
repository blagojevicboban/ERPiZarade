using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>
/// Osnov privremene sprečenosti za rad — kolone 6–13 obrasca OZ-10.
///
/// Vrednosti su <b>kolone obrasca</b>, ne slobodna klasifikacija: RFZO za svaki osnov traži
/// broj dana u svojoj koloni, pa se novi osnov ne može dodati bez izmene obrasca. Zato ovo
/// jeste enum, a nije šifarnik — za razliku od vrsta primanja i olakšica, koje propis menja
/// bez izmene obrasca.
/// </summary>
public enum OsnovSprecenosti
{
    /// <summary>Kolona 6 — bolest.</summary>
    Bolest = 0,

    /// <summary>Kolona 7 — povreda na radu.</summary>
    PovredaNaRadu = 1,

    /// <summary>Kolona 8 — profesionalna bolest.</summary>
    ProfesionalnaBolest = 2,

    /// <summary>Kolona 9 — nega člana porodice, 65%.</summary>
    NegaClanaPorodice = 3,

    /// <summary>Kolona 10 — nega člana porodice po čl. 78. st. 3 Zakona.</summary>
    NegaClanaPorodiceClan78 = 4,

    /// <summary>Kolona 11 — izolacija i praćenje.</summary>
    IzolacijaIPracenje = 5,

    /// <summary>Kolona 12 — davalac tkiva i organa.</summary>
    DavalacTkivaIOrgana = 6,

    /// <summary>Kolona 13 — održavanje trudnoće.</summary>
    OdrzavanjeTrudnoce = 7
}

/// <summary>
/// Privremena sprečenost za rad na teret RFZO (Faza 2.6).
///
/// Ovde <b>nema nijednog iznosa</b>. Naknada je već obračunata i stoji u stavkama obračuna,
/// pod vrstom primanja koja je označena sa <see cref="VrstaPrimanja.NaTeretFonda"/>; ponovljen
/// iznos bi bio treći zapis istog novca — pored obračuna i pored naloga za knjiženje — i prvi
/// bi se razišao sa ostalima. Isto pravilo po kome se nalog za knjiženje ne čuva.
///
/// Zapis nosi samo ono što se iz obračuna ne vidi, a obrazac traži: <b>za koje dane</b> i
/// <b>po kom osnovu</b> je naknada isplaćena, i da li je to prva isplata iz sredstava Fonda.
///
/// Vezuje se za period (godina, mesec), a ne za isplatu: refundira se ono što je radniku u
/// mesecu isplaćeno, bez obzira na to kroz koliko je isplata prošlo. Zbog toga ovaj entitet
/// nije <see cref="IPripadaIsplati"/>.
/// </summary>
[Table("Bolovanja")]
public class Bolovanje
{
    [Key]
    public int BolovanjeId { get; set; }

    /// <summary>Identifikator radnika — isti broj koji karton nosi kroz sve periode.</summary>
    public int BrojRadnika { get; set; }

    /// <summary>Obračunski period u kome je naknada isplaćena; po njemu se sastavlja OZ-10.</summary>
    public int Godina { get; set; }

    public int Mesec { get; set; }

    /// <summary>
    /// Prvi dan sprečenosti — od njega teku 30 dana na teret poslodavca, a ne od
    /// <see cref="DatumOd"/>. Potreban je i za OZ-7, koji traži zaradu iz 12 meseci koji
    /// prethode <b>mesecu u kome je sprečenost nastupila</b>, a to nije nužno mesec isplate.
    /// </summary>
    public DateTime DatumPocetkaSprecenosti { get; set; } = DateTime.Today;

    /// <summary>Prvi dan perioda za koji se naknada refundira (kolona 4 obrasca OZ-10).</summary>
    public DateTime DatumOd { get; set; } = DateTime.Today;

    /// <summary>Poslednji dan tog perioda (kolona 5). Zahtev se podnosi za zatvoren period.</summary>
    public DateTime DatumDo { get; set; } = DateTime.Today;

    public OsnovSprecenosti Osnov { get; set; } = OsnovSprecenosti.Bolest;

    /// <summary>
    /// Prva isplata iz sredstava Fonda po ovoj sprečenosti — kolona 3 obrasca OZ-10, gde se
    /// upisuje „da", a u ostalim slučajevima crtica.
    /// </summary>
    public bool PrvaIsplata { get; set; }

    /// <summary>Broj doznake (izveštaja o sprečenosti); ne ulazi u obrazac, ali se traži uz njega.</summary>
    [MaxLength(30)]
    public string BrojDoznake { get; set; } = "";

    [MaxLength(200)]
    public string Napomena { get; set; } = "";

    public DateTime DatumUnosa { get; set; } = DateTime.Now;

    /// <summary>
    /// Broj dana za koji se naknada refundira — izvodi se iz datuma, jer bi upisan broj
    /// umeo da se sa njima raziđe, a obrazac oba prikazuje jedan pored drugog.
    /// </summary>
    [NotMapped]
    public int BrojDana => DatumDo >= DatumOd ? (DatumDo - DatumOd).Days + 1 : 0;

    /// <summary>Dan sprečenosti na koji pada <see cref="DatumOd"/>.</summary>
    [NotMapped]
    public int DanSprecenostiNaPocetku => (DatumOd - DatumPocetkaSprecenosti).Days + 1;

    /// <summary>
    /// Prvi dan sprečenosti od kog naknada pada na teret Fonda, po osnovu.
    /// <c>null</c> znači da zavisi od okolnosti koje program ne zna, pa se ne pretpostavlja.
    ///
    /// <b>Nije svuda 31.</b> Kod povrede na radu, profesionalne bolesti i davanja tkiva i
    /// organa Fond plaća od <b>prvog</b> dana; kod nege člana porodice zavisi od toga da li
    /// je član mlađi ili stariji od tri godine, što se iz ovog zapisa ne vidi.
    ///
    /// Vrednost služi <b>samo kontrolnoj proveri</b> — ni jedan dinar se po njoj ne računa.
    /// Zato i stoji uz enum, a ne u šifarniku: menja se zajedno sa kolonama obrasca.
    /// </summary>
    public static int? PrviDanNaTeretFonda(OsnovSprecenosti osnov) => osnov switch
    {
        OsnovSprecenosti.PovredaNaRadu => 1,
        OsnovSprecenosti.ProfesionalnaBolest => 1,
        OsnovSprecenosti.DavalacTkivaIOrgana => 1,

        // Mlađi od tri godine — od prvog dana; stariji — od 31. Zapis ne nosi uzrast.
        OsnovSprecenosti.NegaClanaPorodice => null,

        _ => 31
    };

    [NotMapped]
    public string PeriodStr => $"{DatumOd:dd.MM.yyyy}–{DatumDo:dd.MM.yyyy}";

    [NotMapped]
    public string OsnovNaziv => NazivOsnova(Osnov);

    public static string NazivOsnova(OsnovSprecenosti osnov) => osnov switch
    {
        OsnovSprecenosti.Bolest => "Bolest",
        OsnovSprecenosti.PovredaNaRadu => "Povreda na radu",
        OsnovSprecenosti.ProfesionalnaBolest => "Profesionalna bolest",
        OsnovSprecenosti.NegaClanaPorodice => "Nega člana porodice 65%",
        OsnovSprecenosti.NegaClanaPorodiceClan78 => "Nega člana porodice — čl. 78. st. 3",
        OsnovSprecenosti.IzolacijaIPracenje => "Izolacija i praćenje",
        OsnovSprecenosti.DavalacTkivaIOrgana => "Davalac tkiva i organa",
        _ => "Održavanje trudnoće"
    };
}
