using System.Collections.Generic;
using ERPiZaradeData.Models;

namespace ERPiZaradeData;

/// <summary>
/// Podrazumevani šabloni ugovora van radnog odnosa (Faza 2.3).
///
/// Tekstovi su pisani prema <b>obaveznim elementima iz propisa</b>, a ne prepisani iz tuđih
/// obrazaca:
/// <list type="bullet">
///   <item>ugovor o delu — čl. 199 Zakona o radu: posao van delatnosti poslodavca, opis posla,
///         rok i mesto izvršenja, prijem posla, iznos naknade i rok isplate, pisana forma;</item>
///   <item>privremeni i povremeni poslovi — čl. 197 Zakona o radu: poslovi koji ne traju duže
///         od 120 radnih dana u kalendarskoj godini, krug lica sa kojima se sme zaključiti,
///         vreme obavljanja i naknada, pisana forma;</item>
///   <item>autorski ugovor — Zakon o autorskom i srodnim pravima: identifikacija autorskog dela,
///         prava koja se ustupaju odnosno prenose, visina i rokovi naknade, sadržinska,
///         prostorna i vremenska ograničenja, pisana forma.</item>
/// </list>
///
/// Formulacije nisu propisane, pa su ovde samo <b>polazna tačka</b>. Šablon se uređuje iz
/// programa; izmena zakona ili prakse ne traži novu verziju. To je i razlog zašto tekstovi
/// nisu ugrađeni u kod: nacrt novog Zakona o autorskom i srodnim pravima je u javnoj raspravi
/// od marta 2026, pa se formulacije oko ustupanja prava mogu menjati.
/// </summary>
public static class SabloniUgovoraSeed
{
    public const string UgovorODelu = "UOD";
    public const string Autorski = "AUT";
    public const string PrivremeniPoslovi = "PPP";
    public const string NaknadaOdboru = "ODB";

    public static List<SablonUgovora> Podrazumevani() =>
    [
        new()
        {
            Sifra = UgovorODelu,
            Naziv = "Ugovor o delu",
            Redosled = 10,
            JeSistemski = true,
            Napomena = "Član 199. Zakona o radu. Posao mora biti van delatnosti poslodavca.",
            Tekst = TekstUgovoraODelu
        },
        new()
        {
            Sifra = Autorski,
            Naziv = "Ugovor o autorskom delu",
            Redosled = 20,
            JeSistemski = true,
            Napomena = "Zakon o autorskom i srodnim pravima. Proveriti obim ustupanja prava pre potpisa.",
            Tekst = TekstAutorskogUgovora
        },
        new()
        {
            Sifra = PrivremeniPoslovi,
            Naziv = "Ugovor o privremenim i povremenim poslovima",
            Redosled = 30,
            JeSistemski = true,
            Napomena = "Član 197. Zakona o radu. Najviše 120 radnih dana u kalendarskoj godini.",
            Tekst = TekstPrivremenihPoslova
        },
        new()
        {
            Sifra = NaknadaOdboru,
            Naziv = "Ugovor o naknadi članu organa upravljanja",
            Redosled = 40,
            JeSistemski = true,
            Napomena = "Za članove upravnog i nadzornog odbora; naknadu po pravilu utvrđuje odluka nadležnog organa.",
            Tekst = TekstNaknadeOdboru
        }
    ];

    private const string Zaglavlje = """
        {FirmaNaziv}, {FirmaAdresa}, {FirmaGrad}
        PIB: {FirmaPib}   Matični broj: {FirmaMb}
        koga zastupa {FirmaZastupnik}, {FirmaFunkcijaZastupnika}
        (u daljem tekstu: Naručilac)

        i

        {PrimalacIme}, {PrimalacAdresa}, {PrimalacMesto}
        JMBG: {PrimalacJmbg}
        tekući račun: {PrimalacRacun}
        """;

    private const string Potpisi = """
        Ugovor je sačinjen u dva istovetna primerka, po jedan za svaku ugovornu stranu.

        U {FirmaGrad}, dana {DatumZakljucenja}


              ZA NARUČIOCA                                        {PotpisnikDrugeStrane}

        ______________________________                    ______________________________
        {FirmaZastupnik}                                  {PrimalacIme}
        """;

    private const string TekstUgovoraODelu = $"""
        UGOVOR O DELU
        broj {UgovorBrojPolje}

        Zaključen na osnovu člana 199. Zakona o radu, između:

        {Zaglavlje}
        (u daljem tekstu: Poslenik)

        Član 1.
        Poslenik se obavezuje da za Naručioca obavi sledeći posao:
        {PredmetPolje}

        Ugovorne strane saglasno konstatuju da posao iz stava 1. ovog člana nije u okviru
        delatnosti Naručioca i da se obavlja samostalno, bez zasnivanja radnog odnosa.

        Član 2.
        Poslenik će posao obaviti u periodu od {DatumOdPolje} do {DatumDoPolje}, samostalno
        i sopstvenim sredstvima, osim ako se ugovorne strane drukčije ne dogovore.

        Član 3.
        Naručilac je dužan da po prijemu obavljenog posla utvrdi njegovu količinu i kvalitet
        i da o uočenim nedostacima bez odlaganja obavesti Poslenika.

        Član 4.
        Za obavljeni posao Naručilac se obavezuje da Posleniku isplati naknadu u {VrstaIznosaPolje}
        iznosu od {IznosPolje} dinara ({IznosSlovimaPolje}).

        Naknada se isplaćuje na tekući račun Poslenika, po prijemu i prihvatanju obavljenog posla.

        Porez i doprinose po osnovu ove naknade obračunava i plaća Naručilac, u skladu sa
        propisima o porezu na dohodak građana i doprinosima za obavezno socijalno osiguranje.

        Član 5.
        Po ovom ugovoru Poslenik ne ostvaruje prava iz radnog odnosa.

        Član 6.
        Na sve što ovim ugovorom nije uređeno primenjuju se odredbe Zakona o radu i Zakona o
        obligacionim odnosima.

        Član 7.
        Sporove iz ovog ugovora ugovorne strane će rešavati sporazumno, a ako to ne bude moguće,
        nadležan je stvarno nadležni sud u {FirmaGradPolje}.

        {Potpisi}
        """;

    private const string TekstAutorskogUgovora = $"""
        UGOVOR O AUTORSKOM DELU
        broj {UgovorBrojPolje}

        Zaključen na osnovu Zakona o autorskom i srodnim pravima, između:

        {Zaglavlje}
        (u daljem tekstu: Autor)

        Član 1.
        Autor se obavezuje da za Naručioca stvori autorsko delo:
        {PredmetPolje}

        Član 2.
        Autor delo predaje Naručiocu u periodu od {DatumOdPolje} do {DatumDoPolje}.

        Član 3.
        Autor na Naručioca ustupa imovinska prava na delu iz člana 1. ovog ugovora, i to pravo
        umnožavanja, stavljanja u promet i javnog saopštavanja dela.

        Ustupanje je vremenski i prostorno neograničeno, osim ako je drukčije navedeno u
        napomeni uz ovaj ugovor.

        Moralna prava Autora su neprenosiva i ostaju Autoru. Naručilac je dužan da pri svakom
        korišćenju dela navede ime Autora.

        Član 4.
        Za stvoreno delo i ustupljena prava Naručilac se obavezuje da Autoru isplati naknadu u
        {VrstaIznosaPolje} iznosu od {IznosPolje} dinara ({IznosSlovimaPolje}).

        Naknada se isplaćuje na tekući račun Autora, po predaji i prihvatanju dela.

        Pri obračunu se priznaju normirani troškovi u visini od {NormiraniTroskoviPolje}% bruto
        naknade, a porez i doprinose obračunava i plaća Naručilac.

        Član 5.
        Autor jemči da je delo njegova originalna tvorevina i da ustupanjem prava iz člana 3.
        ne povređuje prava trećih lica.

        Član 6.
        Na sve što ovim ugovorom nije uređeno primenjuju se odredbe Zakona o autorskom i
        srodnim pravima i Zakona o obligacionim odnosima.

        {Potpisi}
        """;

    private const string TekstPrivremenihPoslova = $"""
        UGOVOR O OBAVLJANJU PRIVREMENIH I POVREMENIH POSLOVA
        broj {UgovorBrojPolje}

        Zaključen na osnovu člana 197. Zakona o radu, između:

        {Zaglavlje}
        (u daljem tekstu: Izvršilac)

        Član 1.
        Izvršilac se obavezuje da za Naručioca obavlja sledeće privremene i povremene poslove:
        {PredmetPolje}

        Ugovorne strane saglasno konstatuju da poslovi iz stava 1. ovog člana po svojoj prirodi
        ne traju duže od 120 radnih dana u kalendarskoj godini.

        Član 2.
        Poslovi se obavljaju u periodu od {DatumOdPolje} do {DatumDoPolje}, u prostorijama i
        vremenu koje odredi Naručilac, prema dinamici obima posla.

        Član 3.
        Naručilac se obavezuje da Izvršiocu isplati naknadu u {VrstaIznosaPolje} iznosu od
        {IznosPolje} dinara ({IznosSlovimaPolje}), na tekući račun Izvršioca.

        Naknada za privremene i povremene poslove smatra se zaradom u smislu propisa o porezu
        na dohodak građana; porez i doprinose za obavezno socijalno osiguranje obračunava i
        plaća Naručilac.

        Član 4.
        Naručilac je dužan da Izvršioca prijavi na obavezno socijalno osiguranje preko
        Centralnog registra obaveznog socijalnog osiguranja, najkasnije dan pre početka rada,
        i da mu obezbedi sredstva i opremu za bezbedan rad.

        Član 5.
        Na sve što ovim ugovorom nije uređeno primenjuju se odredbe Zakona o radu.

        {Potpisi}
        """;

    private const string TekstNaknadeOdboru = $"""
        UGOVOR O NAKNADI ZA RAD U ORGANU UPRAVLJANJA
        broj {UgovorBrojPolje}

        Zaključen između:

        {Zaglavlje}
        (u daljem tekstu: Član organa)

        Član 1.
        Član organa obavlja poslove:
        {PredmetPolje}

        Član 2.
        Poslovi se obavljaju u periodu od {DatumOdPolje} do {DatumDoPolje}.

        Član 3.
        Za rad iz člana 1. ovog ugovora Naručilac se obavezuje da Članu organa isplati naknadu
        u {VrstaIznosaPolje} iznosu od {IznosPolje} dinara ({IznosSlovimaPolje}), na njegov
        tekući račun.

        Naknada se oporezuje kao drugi prihod; porez i doprinose obračunava i plaća Naručilac.

        Član 4.
        Ovaj ugovor ne predstavlja zasnivanje radnog odnosa i po njemu se ne ostvaruju prava iz
        radnog odnosa.

        Član 5.
        Na sve što ovim ugovorom nije uređeno primenjuju se odredbe Zakona o obligacionim
        odnosima i akta nadležnog organa o naknadama članovima organa upravljanja.

        {Potpisi}
        """;

    // Polja se u konstantama pišu preko imenovanih pomoćnika, jer bi vitičaste zagrade u
    // interpolisanom stringu inače morale da se udvajaju — a tada ih je u tekstu teško čitati.
    private const string UgovorBrojPolje = "{UgovorBroj}";
    private const string PredmetPolje = "{Predmet}";
    private const string DatumOdPolje = "{DatumOd}";
    private const string DatumDoPolje = "{DatumDo}";
    private const string IznosPolje = "{Iznos}";
    private const string IznosSlovimaPolje = "{IznosSlovima}";
    private const string VrstaIznosaPolje = "{VrstaIznosa}";
    private const string NormiraniTroskoviPolje = "{NormiraniTroskovi}";
    private const string FirmaGradPolje = "{FirmaGrad}";
}
