using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ERPiZaradeApp.Views.Pomoc;

public partial class PomocPage : Page
{
    private readonly List<PomocTema> _teme = new()
    {
        new PomocTema
        {
            Naslov = "👋 Dobrodošli u ERPi Zarade",
            Sadrzaj =
                "ERPiZaradeApp je desktop aplikacija za kompletnu obradu mesečnih zarada zaposlenih — od unosa radnih sati do generisanja platnih spiskova, rekapitulacija i izveštaja za banke.\n\n" +
                "SVI OBRAČUNI se vrše u skladu sa Zakonom o radu RS i poreskim propisima koji važe u momentu obračuna. Poreski parametri (stope doprinosa, vrednost boda, fond sati) se unose po periodu.\n\n" +
                "PRATITE TEME POMOĆI:\n" +
                "Sa leve strane izaberite željenu oblast, ili pogledajte temu '🚀 Brzi start' za standardni mesečni tok rada."
        },
        new PomocTema
        {
            Naslov = "🚀 Brzi start — tok rada",
            Sadrzaj =
                "Standardni mesečni tok rada obrade zarada:\n\n" +
                "1. Kreirajte novi obračunski period — u modulu 'Obračunski periodi', kliknite '➕ Novi obračun'. Sistem automatski kreira zapise radnih sati za sve aktivne radnike.\n\n" +
                "2. Proverite parametre perioda — na stranici 'Radni sati', u gornjoj traci proverite vrednost boda i fond časova za taj mesec.\n\n" +
                "3. Unesite radne sate zaposlenih — u tabeli unesite sate po kolonama (redovni, bolovanje, prekovremeni...). Koristite 'Brzo popunjavanje' za masovni unos.\n\n" +
                "4. Sačuvajte i preračunajte — dugme '💾 Sačuvaj i preračunaj' automatski izračunava bruto zaradu, minuli rad, doprinose, porez i neto isplatu.\n\n" +
                "5. Pregledajte obračune — u modulu 'Obračun plate' kliknite na radnika za detaljan platni listić.\n\n" +
                "6. Odštampajte izveštaje — u modulu 'Štampa' generišite platni spisak, rekapitulaciju i izveštaj za banke (PDF).\n\n" +
                "Obračun je uvek vezan za AKTIVNI PERIOD, istaknut u zaglavlju svakog ekrana. Menjanje radnih sati u zatvorenim periodima nije moguće."
        },
        new PomocTema
        {
            Naslov = "📁 Obračunski periodi",
            Sadrzaj =
                "Meni '📁 Obračunski periodi' je centralna tačka sistema — ovde se kreira novi mesečni obračun, bira aktivni period i pregledaju arhivirani obračuni.\n\n" +
                "1. KREIRANJE NOVOG OBRAČUNA:\n" +
                "• Kliknite '➕ Novi obračun', unesite godinu i mesec (sistem popunjava tekući mesec kao podrazumevani).\n" +
                "• Sistem kreira zapise radnih sati za sve aktivne radnike sa 0 sati. Vrednost boda i fond časova preuzimaju se iz poslednjeg definisanog perioda u Porezima.\n" +
                "• Nije moguće kreirati dva obračuna za isti mesec i godinu.\n\n" +
                "2. POSTAVLJANJE AKTIVNOG PERIODA:\n" +
                "• Kliknite na red u listi i dugme '📌 Postavi kao aktivan'. Samo aktivni period se može uređivati.",
            Kljuc = "Obracuni"
        },
        new PomocTema
        {
            Naslov = "⏱️ Radni sati",
            Sadrzaj =
                "Meni '⏱️ Radni sati' omogućava unos i izmenu svih vrsta radnih sati za svakog zaposlenog u AKTIVNOM periodu. Ako period nije postavljen, prvo ga aktivirajte u meniju 'Obračunski periodi'.\n\n" +
                "1. PARAMETRI PERIODA (gornja traka):\n" +
                "• Bod (RSD) — vrednost obračunskog boda za dati mesec, koristi se za izračun bruto zarade po koeficijentu.\n" +
                "• Fond sati — ukupan fond radnih časova u mesecu.\n\n" +
                "2. KOLONE (glavne): Redovni sati, Bolovanje, Prekovremeni, Godišnji odmor, Državni praznik, Noćni rad, Rad praznikom, Plaćeno odsustvo, Bolovanje >60 dana, Porodiljsko, Bolovanje 100%, Topli obrok (iznos), Regres (iznos), Stimulacija (%), Bruto dodatak, Prosek (12m).\n\n" +
                "3. BRZO POPUNJAVANJE:\n" +
                "• Izaberite kolonu u padajućem meniju '⚡ Brzo popunjavanje', unesite vrednost, kliknite '🚀 Primeni na sve' — popunjava kolonu za sve vidljive radnike.\n\n" +
                "4. DODAVANJE/UKLANJANJE RADNIKA IZ PERIODA:\n" +
                "• Dugmad '➕ Dodaj radnika' i '🗑️ Ukloni radnika' — korisno kad radnik počne sredinom meseca.\n\n" +
                "5. SNIMANJE: '💾 Sačuvaj i preračunaj' istovremeno snima izmene i pokreće kompletan obračun za sve zaposlene. Svako snimanje briše prethodne obračune za taj period i kreira nove — stari podaci se uvek mogu obnoviti ponovnim snimanjem.",
            Kljuc = "RadniSati"
        },
        new PomocTema
        {
            Naslov = "📊 Obračun plate",
            Sadrzaj =
                "Meni '📊 Obračun plate' prikazuje pregled svih obračunatih zarada za aktivni mesec — leva strana tabelarni pregled svih radnika, desni panel detaljan platni listić izabranog radnika.\n\n" +
                "1. LISTA (leva strana): Za isplatu (Neto), Bruto 1 (bruto zarada radnika), Bruto 2 (Bruto 1 + doprinosi poslodavca), Porez na dohodak.\n\n" +
                "2. DETALJAN PLATNI LISTIĆ (klik na red) sadrži: odrađene sate, finansijsku rekapitulaciju (bruto zarada, minuli rad, stimulacija, naknade), poreski obračun (poresko oslobođenje, osnovica, porez 10%), doprinose na teret radnika (PIO/zdravstvo/nezaposlenost), obustave (krediti, samodoprinosi) i rezultujuće iznose (ZA ISPLATU, Bruto 1, Bruto 2).\n\n" +
                "3. IZVOZ: '📄 Preuzmi PDF' — platni listić izabranog radnika; '🌐 Izvezi XML' — PPP-PD XML samo za tog radnika (kontrola pre masovnog izvoza).",
            Kljuc = "Obracun"
        },
        new PomocTema
        {
            Naslov = "👤 Radnici",
            Sadrzaj =
                "Meni '👤 Radnici' — kadrovska evidencija, matični podaci zaposlenih korišćeni u obračunu.\n\n" +
                "1. KLJUČNA POLJA: Broj radnika, Ime i prezime, JMBG (obavezan za PPP-PD), Koeficijent (množi se vrednošću boda za osnovnu satnicu), Osnovna plata (alternativa koeficijentu — fiksni bruto iznos, ima prednost ako je unet), Minuli rad (god.), Kategorija/razred, Radna jedinica, Tekući račun, Banka, Aktivan (neaktivni se ne uključuju u nove periode).\n\n" +
                "2. DODAVANJE: '➕ Novi radnik' — ime/prezime, JMBG, koeficijent ili osnovna plata, minuli rad i radna jedinica su neophodni za ispravan obračun.\n\n" +
                "Izmena koeficijenta ili staža radnika NE menja prethodne obračune — novi podaci važe tek od sledećeg preračunavanja.",
            Kljuc = "Radnici"
        },
        new PomocTema
        {
            Naslov = "🧾 Platni listići (masovna štampa)",
            Sadrzaj =
                "Meni '🧾 Platni listići' omogućava masovni izvoz platnih listića svih radnika za aktivni period — u odvojene PDF datoteke ili kao jedinstven zbirni dokument.\n\n" +
                "• Izaberite period (podrazumevano aktivni).\n" +
                "• Izaberite format izvoza (pojedinačni fajlovi po radniku ili jedan zbirni PDF).\n" +
                "• Generisani PDF se otvara u podrazumevanom pregledaču.",
            Kljuc = "Listici"
        },
        new PomocTema
        {
            Naslov = "📑 Izveštaji i rekapitulacije",
            Sadrzaj =
                "Meni '📑 Izveštaji & rekapit.' generiše tri tipa dokumenta u PDF formatu. Period i radna jedinica se biraju u filterima na vrhu.\n\n" +
                "1. MESEČNI PLATNI SPISAK (A4 Landscape): evidencija sati po vrstama, bruto zarada i naknade, poreske osnovice, doprinosi radnika, obustave, neto za isplatu, zbirni red. Dostupno i grupisano po radnim jedinicama.\n\n" +
                "2. MESEČNA REKAPITULACIJA (A4 Portrait): zbirni knjigovodstveni izveštaj — ukupni bruto troškovi, poreska osnovica i porez, kumulativni doprinosi radnika/poslodavca, samodoprinosi, krediti, finalni iznosi, potpisne linije.\n\n" +
                "3. IZVEŠTAJI ZA BANKE (A4 Portrait): platni spiskovi grupisani po bankama — reg. broj, tekući račun, ime, neto iznos, zbirna suma za prenos. Posebna stranica za svaku banku iz šifarnika.\n\n" +
                "Kliknite odgovarajuće dugme — PDF se generiše i automatski otvara za pregled i štampu.",
            Kljuc = "Stampe"
        },
        new PomocTema
        {
            Naslov = "📋 PPP-PD XML prijava",
            Sadrzaj =
                "Meni '📋 PPP-PD' automatski generiše XML datoteku za elektronsko podnošenje poreske prijave PPP-PD (obračunati porezi i doprinosi na zarade).\n\n" +
                "• Kliknite 'Generiši XML' za izabrani period — sistem kreira validiranu XML datoteku prema šemi Poreske uprave RS, spremnu za ePortal/ePorezi.\n" +
                "• Neophodni su ispravan JMBG svih zaposlenih i PIB firme u podešavanjima — proverite pre generisanja.\n" +
                "• Individualni XML iz platnog listića (dugme '🌐 Izvezi XML' u meniju Obračun plate) generiše XML samo za jednog radnika, za kontrolu pre masovnog izvoza.",
            Kljuc = "PppPd"
        },
        new PomocTema
        {
            Naslov = "⚖️ Porezi i opšti parametri",
            Sadrzaj =
                "Meni '⚖️ Porezi i parametri' definiše poreske parametre po periodu (godini i mesecu):\n\n" +
                "• Vrednost boda (RSD) — novčana vrednost obračunskog boda.\n" +
                "• Fond sati — mesečni fond radnih sati (160–184 h).\n" +
                "• % minulog rada (zakonski minimum 0,4% po godini staža), % prekovremenog rada (min. 26%), % noćnog rada (min. 26%), % državnog praznika (110%), % bolovanja (min. 65%).\n" +
                "• Stope doprinosa na teret radnika: PIO, zdravstvo, nezaposlenost.\n" +
                "• Stope doprinosa na teret poslodavca: PIO, zdravstvo, nezaposlenost.\n" +
                "• Poresko oslobođenje (lični odbitak) — mesečni zakonski iznos.\n\n" +
                "Ovi parametri direktno ulaze u formule obračuna (vidi temu '🔢 Formule obračuna zarade').",
            Kljuc = "Porezi"
        },
        new PomocTema
        {
            Naslov = "📈 Doprinosi",
            Sadrzaj =
                "Meni '📈 Doprinosi' prikazuje istorijat stopa doprinosa, uglavnom radi migracije podataka iz legacy sistema.\n\n" +
                "Trenutne stope doprinosa (PIO, zdravstvo, nezaposlenost — na teret radnika i poslodavca) se podešavaju u meniju '⚖️ Porezi i parametri', ne ovde.",
            Kljuc = "Doprinosi"
        },
        new PomocTema
        {
            Naslov = "📊 Platni razredi",
            Sadrzaj =
                "Meni '📊 Platni razredi' definiše razrede plata sa koeficijentima za lakšu kategorizaciju radnih mesta — koristi se prilikom unosa polja 'Kategorija (razred)' na kartici radnika.",
            Kljuc = "PlatniRazredi"
        },
        new PomocTema
        {
            Naslov = "💳 Krediti i obustave",
            Sadrzaj =
                "Meni '💳 Krediti' — evidencija bankovnih kredita i administrativnih obustava zaposlenih.\n\n" +
                "• Krediti evidentirani ovde ulaze u obračun kao odbitak pri izračunu neto iznosa za isplatu (vidi formulu u temi 'Formule obračuna zarade').\n" +
                "• Svaki kredit je vezan za konkretnog radnika i banku iz šifarnika Banke.",
            Kljuc = "Krediti"
        },
        new PomocTema
        {
            Naslov = "🏦 Šifarnik banaka",
            Sadrzaj =
                "Meni '🏦 Banke' — šifarnik komercijalnih banaka koji se koristi za grupisanje platnih naloga u izveštaju za banke i za tekuće račune radnika. Sadrži šifru i naziv banke.",
            Kljuc = "Banke"
        },
        new PomocTema
        {
            Naslov = "🏢 Upravljanje firmama",
            Sadrzaj =
                "Meni '🏢 Upravljanje firmama' — svaka firma ima svoju odvojenu bazu podataka.\n\n" +
                "• Podaci o firmi (naziv, adresa, PIB, matični broj) koriste se u zaglavljima izveštaja i u PPP-PD XML prijavi.\n" +
                "• Klik na karticu 'Firma' u bočnom meniju ili na dugme u ovom meniju menja aktivnu firmu.\n" +
                "• Sve ostale stranice aplikacije uvek prikazuju i menjaju podatke trenutno aktivne firme.",
            Kljuc = "Firme"
        },
        new PomocTema
        {
            Naslov = "👥 Korisnički nalozi",
            Sadrzaj =
                "Meni '👥 Korisnici' — upravljanje pristupom i ulogama zaposlenih koji koriste aplikaciju (dostupno samo Administratoru).\n\n" +
                "• Kreiranje naloga: ime, korisničko ime, uloga, lozinka.\n" +
                "• Pri izmeni postojećeg naloga, prazno polje lozinke zadržava postojeću.",
            Kljuc = "Korisnici"
        },
        new PomocTema
        {
            Naslov = "⚙️ Podešavanja",
            Sadrzaj =
                "Meni '⚙️ Podešavanja' — upravljanje osnovnim podacima o firmi i kreiranje/vraćanje rezervne kopije baze podataka.\n\n" +
                "• Baza podataka je SQLite datoteka koja se čuva u folderu aplikacije — preporučuje se redovna izrada rezervnih kopija.\n" +
                "• Opcija za automatsko pokretanje maksimizovano pri sledećem startu.",
            Kljuc = "Podesavanja"
        },
        new PomocTema
        {
            Naslov = "🔢 Formule obračuna zarade",
            Sadrzaj =
                "Sistem koristi standardnu metodologiju u skladu sa Zakonom o radu RS.\n\n" +
                "• Cena sata = Koeficijent × Vrednost boda / Fond sati (ili Osnovna plata / Fond sati ako je uneta).\n" +
                "• Bruto zarada = Redovni sati × Cena sata.\n" +
                "• Bruto prekovremeni/noćni/praznik = odgovarajući sati × Cena sata × (1 + %/100).\n" +
                "• Bruto stimulacija = Bruto zarada × (%_stimulacije / 100).\n" +
                "• Ukupan Bruto = zbir svih gornjih stavki + naknade (bolovanje, praznik, godišnji odmor, topli obrok, regres, bruto dodatak).\n" +
                "• Poreska osnovica = Ukupan Bruto − Lični odbitak; Porez = Osnovica × 10%.\n" +
                "• Doprinosi radnika = Ukupan Bruto × odgovarajuća stopa (PIO/zdravstvo/nezaposlenost).\n" +
                "• Neto = Ukupan Bruto − Porez − Doprinosi − Krediti − Samodoprinosi.\n" +
                "• Bruto 2 = Ukupan Bruto + doprinosi na teret poslodavca (PIO/zdravstvo/nezaposlenost)."
        },
        new PomocTema
        {
            Naslov = "📅 Minuli rad",
            Sadrzaj =
                "Minuli rad je uvećanje zarade za svaku punu godinu staža kod TRENUTNOG poslodavca, po članu 108. Zakona o radu RS. Zakonski minimum: 0,4% od osnovne zarade po godini staža.\n\n" +
                "OSNOV je isključivo osnovna zarada (redovni sati × cena sata):\n" +
                "Minuli_rad = Osnova × (%_minulog_rada / 100) × Broj_godina_staža\n\n" +
                "NE ULAZI u osnov: prekovremeni rad, noćni rad, rad na praznike, stimulacije/bonusi, naknade (topli obrok, regres, putni troškovi).\n\n" +
                "Broj punih godina staža unosi se ručno na kartici radnika (polje 'Minuli rad — god.') — sistem ga ne izračunava automatski iz datuma zapošljavanja; potrebno je ažurirati jednom godišnje."
        },
        new PomocTema
        {
            Naslov = "🏥 Bolovanje i posebne naknade",
            Sadrzaj =
                "• Bolovanje do 30 dana (na teret poslodavca): Bruto_bolovanje = Bolovanje_sati × (Prosek/Fond_sati) × (%_bolovanja/100), minimalno 65% prosečne zarade.\n" +
                "• Prosek zarade se automatski računa iz poslednjih 12 meseci (ukupno isplaćena bruto zarada / ukupno odrađeni redovni sati). Ako je u koloni 'Prosek (12m)' na Radnim satima uneta vrednost > 0, koristi se ona umesto automatskog izračuna.\n" +
                "• Bolovanje na teret RFZO (od 31. dana) — kolona 'Bolovanje >60 dana', evidentira se ali ne ulazi u trošak poslodavca.\n" +
                "• Bolovanje 100% (povrede na radu/profesionalne bolesti) — kolona 'Bolovanje 100%'.\n" +
                "• Porodiljsko odsustvo — posebna kolona, naknadu u celosti refundira RFZO.\n" +
                "• Godišnji odmor — naknada jednaka osnovnoj bruto zaradi.\n" +
                "• Neradni državni praznik — naknada po definisanoj stopi (obično 110%)."
        },
        new PomocTema
        {
            Naslov = "❓ Česta pitanja",
            Sadrzaj =
                "• Šta je razlika između Bruto 1 i Bruto 2? Bruto 1 = ukupna bruto zarada radnika (na platnom listiću). Bruto 2 = ukupni trošak poslodavca = Bruto 1 + doprinosi koje plaća poslodavac.\n\n" +
                "• Radnik je radio samo deo meseca? Unesite tačan broj sati u 'Redovni sati' — sistem automatski preračunava proporcionalan iznos. Fond sati ostaje fiksan za celu firmu.\n\n" +
                "• Kako dodati radnika usred meseca? Prvo ga dodajte u 'Radnici', zatim u 'Radni sati' kliknite '➕ Dodaj radnika' da ga uključite u aktivni period.\n\n" +
                "• Mogu li da menjam prethodne obračune? Da — postavite prethodni period kao aktivan, izmenite radne sate i kliknite '💾 Sačuvaj i preračunaj'. Proverite da li su štampane kopije izveštaja i dalje usaglašene.\n\n" +
                "• Gde se čuva baza? SQLite datoteka u folderu aplikacije — redovno pravite rezervne kopije (meni Podešavanja)."
        },
        new PomocTema
        {
            Naslov = "⌨️ Korisne prečice",
            Sadrzaj =
                "• F1 — Otvara Pomoć, direktno na temi koja odgovara trenutnoj stranici.\n" +
                "• Ctrl + M — Sklapa ili proširuje bočni navigacioni meni.\n" +
                "• Esc — Zatvara otvoreni modalni prozor (gde je podržano)."
        }
    };

    public PomocPage(string? initijalnaTema = null)
    {
        InitializeComponent();
        LstTeme.ItemsSource = _teme;

        var tema = initijalnaTema is not null ? _teme.FirstOrDefault(t => t.Kljuc == initijalnaTema) : null;
        LstTeme.SelectedItem = tema ?? (_teme.Count > 0 ? _teme[0] : null);
    }

    private void LstTeme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstTeme.SelectedItem is PomocTema tema)
        {
            TxtNaslovTeme.Text = tema.Naslov;
            TxtSadrzajTeme.Text = tema.Sadrzaj;
        }
    }

    private void TxtPretragaTema_TextChanged(object sender, TextChangedEventArgs e)
    {
        var upit = TxtPretragaTema.Text?.Trim() ?? string.Empty;
        var prethodnaSelekcija = LstTeme.SelectedItem as PomocTema;

        var filtrirano = upit.Length == 0
            ? _teme
            : _teme.Where(t =>
                t.Naslov.Contains(upit, StringComparison.OrdinalIgnoreCase) ||
                t.Sadrzaj.Contains(upit, StringComparison.OrdinalIgnoreCase)).ToList();

        LstTeme.ItemsSource = filtrirano;

        if (prethodnaSelekcija is not null && filtrirano.Contains(prethodnaSelekcija))
            LstTeme.SelectedItem = prethodnaSelekcija;
        else if (filtrirano.Count > 0)
            LstTeme.SelectedIndex = 0;
        else
        {
            TxtNaslovTeme.Text = "Nema rezultata";
            TxtSadrzajTeme.Text = "Nijedna tema pomoći ne odgovara pretrazi.";
        }
    }
}
