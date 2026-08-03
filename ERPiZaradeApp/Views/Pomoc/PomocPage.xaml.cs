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
                "• Isplata — sati se unose za JEDNU isplatu meseca. Dok mesec ima jednu isplatu lista je onemogućena i sve radi kao i do sada; nova isplata se dodaje u meniju '💸 Isplate u mesecu'. Sati uneti za akontaciju više ne prepisuju one unete za konačnu zaradu.\n" +
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
            Naslov = "💸 Isplate u mesecu",
            Sadrzaj =
                "Meni '💸 Isplate u mesecu' postoji zbog meseca u kom se zarada isplaćuje u više navrata: akontacija pa konačna isplata, bonus, 13. plata.\n\n" +
                "• Dok mesec ima JEDNU isplatu, sve radi kao i do sada — selektori isplate se ni ne prikazuju.\n" +
                "• Svaka isplata je zaseban obračun, zasebna PPP-PD prijava sa svojim BOP-om i zaseban paket naloga za prenos. BOP jedne isplate na nalogu druge šalje novac na pogrešnu deklaraciju, pa program to zaustavlja.\n" +
                "• Prekalkulacija i storniranje diraju samo obračune izabrane isplate — akontacija koja je već isplaćena ostaje netaknuta.\n" +
                "• RADNI SATI se od 1.13.0 takođe vode po isplati: svaka isplata ima svoj unos sati, a ekran '⏱️ Radni sati' i uvoz iz Excel/CSV rade nad izabranom isplatom.\n" +
                "• Brisanje isplate briše i radne sate unete za nju — oni su unos, ne dokaz. Obračun se ne briše nikad: isplata koja nosi obračune se ne može obrisati.\n" +
                "• OBUSTAVE (rate kredita i samodoprinos) skidaju se SAMO na konačnoj zaradi. Akontacija, bonus i 13. plata idu bez njih, jer bi radnik inače istu ratu platio više puta u mesecu. Zato mesec sme imati samo jednu isplatu vrste 'Konačna zarada'.\n" +
                "• Akontacija se u PPP-PD prijavi označava sa 'A' (nije konačna isplata prihoda), ostale isplate sa 'K'.\n" +
                "• Dugme '🔗' upisuje isplatu obračunima i radnim satima koji je nemaju — nijedan iznos ni sat se pri tome ne menja.",
            Kljuc = "Isplate"
        },
        new PomocTema
        {
            Naslov = "📝 Ugovori van radnog odnosa",
            Sadrzaj =
                "Meni '📝 Ugovori van radnog odnosa' vodi ugovor o delu, autorske naknade, privremene i povremene poslove i naknade članovima upravnog i nadzornog odbora.\n\n" +
                "• Primalac je karton radnika sa oznakom 'Van radnog odnosa'. Najbrže se unosi dugmetom '＋ novi' pored padajuće liste primalaca — otvara unos novog kartona ili označavanje postojećeg (penzioner, bivši zaposleni). Isto se može uraditi i u meniju 'Radnici', ali tek pošto se karton otvori dugmetom 'Izmeni': van režima izmene su polja onemogućena i čekboks ne reaguje.\n" +
                "• Iz kartona se uzimaju JMBG, opština prebivališta i tekući račun; ekrani zarade označeno lice posle toga ne nude za obračun plate, radne sate ni platni listić. Već obračunate zarade ostaju netaknute.\n" +
                "• Računica ima četiri koraka: osnovica = bruto − normirani troškovi, porez = osnovica × stopa, doprinosi = osnovica × stope, neto = bruto − porez − doprinosi na teret primaoca. Primer za ugovor o delu: bruto 50.000 → normirani troškovi 20% = 10.000 → osnovica 40.000 → porez 20% = 8.000 i PIO 24% = 9.600 → neto 32.400.\n" +
                "• Ako je naknada ugovorena 'na ruke', čekirajte 'neto' — bruto se dobija preračunom, tačno u dinar.\n" +
                "• Naknada se vezuje za ISPLATU, ne za mesec: ulazi u istu PPP-PD prijavu i isti paket naloga kao zarada te isplate, samo sa svojom šifrom vrste prihoda i bez sati. Naknada isplaćena drugog datuma ide u svoju isplatu, jer svaka isplata ima svoj datum plaćanja i svoj BOP.\n" +
                "• Isti ugovor može biti isplaćen u ratama — po jedna u svakoj isplati. Dva obračuna po istom ugovoru u ISTOJ isplati nisu dozvoljena, jer bi dala dva reda za isto lice u jednoj prijavi.\n" +
                "• Prekalkulacija zarada ne dira obračunate naknade — one nastaju zasebnom radnjom nad ugovorom.\n" +
                "• Platni listić se za naknadu ne pravi: on prikazuje sate, fond i obustave, kojih ovde nema.\n\n" +
                "ŠTA IDE VAN PROGRAMA:\n" +
                "• PRIJAVA NA OSIGURANJE (obrazac M) podnosi se preko portala CROSO — jedinstvenom prijavom za PIO, RFZO i nezaposlenost. Za privremene i povremene poslove najkasnije DAN PRE početka rada. Program to ne može da zameni.\n" +
                "• Obrasci M-UN, M-UN/K i M-4 se VIŠE NE PODNOSE — ukinuti su od 01.01.2019. Fond PIO podatke o stažu i osnovicama preuzima elektronski iz PPP-PD prijave, najkasnije do kraja februara za prethodnu godinu. Stari obrasci važe samo za period zaključno sa 31.12.2018.\n" +
                "• Staž i uplaćene doprinose primalac proverava na e-Šalteru Fonda PIO i na portalu eUprava; od 2026. pristup ide isključivo preko eID-a (kvalifikovani elektronski sertifikat ili ConsentID).",
            Kljuc = "Ugovori"
        },
        new PomocTema
        {
            Naslov = "📄 Šifarnik vrsta ugovora",
            Sadrzaj =
                "Meni '📄 Vrste ugovora' drži sve što o naknadi van radnog odnosa propisuje zakon: normirane troškove, stopu poreza i stope doprinosa, podeljene na teret primaoca i na teret isplatioca.\n\n" +
                "• Izmena propisa se unosi ovde — ne čeka se nova verzija programa.\n" +
                "• OVP je oznaka vrste prihoda iz Kataloga vrste prihoda, tri cifre (601 ugovor o delu, 301–323 autorske naknade, 150–152 privremeni i povremeni poslovi).\n" +
                "• Cela devetocifrena šifra vrste prihoda se SASTAVLJA pri obračunu, po strukturi V-PP-OVP-OL-B: verzija kataloga (1), tip primaoca prihoda (2 cifre, bira se uz ugovor), OVP (3), oznaka olakšice (2) i beneficirani staž (1). Za prihode van radnog odnosa poslednja tri mesta su nule.\n" +
                "• Vrsta bez unetog OVP-a prolazi obračun, ali je kontrolne provere prijavljuju kao grešku — prijava bez šifre vrste prihoda biva odbijena.\n" +
                "• Šifra plaćanja za nalog se takođe unosi ovde; propisuje je NBS, pa program ne pretpostavlja koja je.\n" +
                "• Vrsta upotrebljena u zaključenom ugovoru se ne briše — isključite je poljem 'Aktivna'.",
            Kljuc = "VrsteUgovora"
        },
        new PomocTema
        {
            Naslov = "🖋️ Šabloni ugovora i generator dokumenta",
            Sadrzaj =
                "Uz svaki zaključen ugovor može se generisati tekst dokumenta i urediti pre potpisa. Dugme '📄' na ekranu ugovora otvara editor.\n\n" +
                "• Isporučena su četiri šablona: ugovor o delu (član 199. Zakona o radu), ugovor o autorskom delu, ugovor o privremenim i povremenim poslovima (član 197, uz konstataciju da poslovi ne traju duže od 120 radnih dana u kalendarskoj godini) i ugovor o naknadi članu organa upravljanja.\n" +
                "• Tekstovi su pisani prema OBAVEZNIM ELEMENTIMA iz propisa; formulacije birate vi i menjate ih u meniju '🖋️ Šabloni ugovora'. Zato i postoje kao šifarnik — izmena zakona ili prakse ne traži novu verziju programa.\n" +
                "• Polja se pišu u vitičastim zagradama: {PrimalacIme}, {Iznos}, {IznosSlovima}, {DatumOd}… Spisak sa značenjem stoji desno od editora; dvoklik ubacuje polje na mesto kursora.\n" +
                "• Polje koje nije popunjeno OSTAJE VIDLJIVO u tekstu i prijavljuje se posle generisanja. Tako se rupa na mestu iznosa ili roka primeti pre potpisa, a ne posle.\n" +
                "• Iznos slovima se ispisuje sam, sa ispravnim rodom i padežem. Razlika brojke i slova tumači se u korist slova, pa se ne prepisuje rukom.\n" +
                "• Popunite 'Zastupnik' i 'Funkcija zastupnika' u kartonu firme — ugovor se zaključuje 'koga zastupa…', pa bi bez toga svaki dokument imao istu prazninu.\n" +
                "• TEKST SE ČUVA UZ UGOVOR, ne uz šablon: kasnija izmena šablona ne dira već zaključene ugovore. Ponovno generisanje briše ručne izmene i zato pita pre toga.\n" +
                "• Iznosi se iz teksta NE ČITAJU — obračun ide iz polja ugovora. Ispravka slovne greške u dokumentu ne može da promeni isplatu.\n" +
                "• '📄 PDF' snima dokument spreman za štampu i potpis.",
            Kljuc = "SabloniUgovora"
        },
        new PomocTema
        {
            Naslov = "📒 Nalog za knjiženje",
            Sadrzaj =
                "Meni '📒 Nalog za knjiženje' pravi temeljnicu za glavnu knjigu — dvostrani nalog koji se izvozi u ERPiFinansije.\n\n" +
                "• Nalog se IZVODI iz obračuna svaki put iznova; ništa se ovde ne upisuje. Zato se izmena konta odmah vidi, a pogrešan izvoz se ispravlja ponovnim izvozom, bez storniranja.\n" +
                "• TROŠAK ide na konto upisan uz VRSTU PRIMANJA (meni '💰 Vrste primanja') odnosno uz VRSTU UGOVORA, i deli se po ŠIFRI MESTA TROŠKA iz kartona radnika. Obaveze se ne dele po mestima troška — obaveza prema radniku je jedna bez obzira gde je radio.\n" +
                "• PROTIVSTAVA (neto obaveza, porez, doprinosi, obustave) dolazi iz šifarnika '📗 Konta za knjiženje'. Ona ne zavisi od toga šta je isplaćeno nego od uloge iznosa u nalogu, pa zato stoji zasebno.\n" +
                "• Iznosi su ISTI oni koje obračun već nosi: konto neto obaveza se poklapa sa zbirom naloga za prenos, a porez i doprinosi sa PPP-PD prijavom. Ništa se ne računa iznova.\n" +
                "• Svaka ISPLATA se knjiži zasebnim nalogom, sa svojim datumom. Stornirani obračuni se ne knjiže.\n" +
                "• NALOG KOJI NIJE U RAVNOTEŽI SE NE IZVOZI. Ako se sastav nekog obračuna ne slaže (bruto umanjen za porez, doprinose i obustave ne daje isplaćen neto), kontrola to javlja PO RADNIKU — u glavnoj knjizi bi se videla samo razlika, bez traga odakle je došla.\n" +
                "• Neoporeziva primanja (prevoz, jubilarna nagrada) ulaze u trošak iako nisu u bruto iznosu — zato se trošak uzima iz stavki obračuna, a ne iz bruta.\n" +
                "• '📒 JSON' snima fajl za uvoz u ERPiFinansije; '📊 CSV' iste stavke za proveru u tabeli, i sme se snimiti i kad nalog nije spreman — upravo se u njemu i traži gde je razlika.",
            Kljuc = "Knjizenje"
        },
        new PomocTema
        {
            Naslov = "🏥 Bolovanja i refundacija RFZO (OZ-7, OZ-10)",
            Sadrzaj =
                "Meni '🏥 Bolovanja i RFZO' vodi evidenciju privremene sprečenosti za rad i pravi obrasce kojima se od Republičkog fonda za zdravstveno osiguranje traži refundacija isplaćene naknade zarade.\n\n" +
                "• Naknadu za PRVIH 30 DANA sprečenosti nosi poslodavac; od 31. dana je refundira Fond. Zato se uz svako bolovanje unosi i POČETAK SPREČENOSTI, a ne samo period za koji se traži refundacija — bez njega se ne zna koji je to dan po redu, pa kontrola upozorava kad period počinje unutar prvih 30 dana.\n" +
                "• OVDE SE NE UNOSI NIJEDAN IZNOS. Naknada je već obračunata i stoji u stavkama obračuna. Ekran unosi samo ono što se iz obračuna ne vidi: za koje dane, po kom osnovu i da li je to prva isplata iz sredstava Fonda.\n" +
                "• Koje su naknade na teret Fonda kaže kolona 'Na teret Fonda' u meniju '💰 Vrste primanja'. Podrazumevano je označeno samo 'Bolovanje preko 30 dana'; ko refundira i naknadu za povredu na radu ili negu člana porodice, označi i te vrste.\n" +
                "• Porez i doprinosi se dele srazmerno udelu naknade u ukupnom bruto iznosu obračuna — obračun ih ne vodi po stavkama. Za pun mesec bolovanja udeo je ceo obračun, pa podele ni nema.\n" +
                "• OBRAZAC OZ-7 ('🖨️') je potvrda o ostvarenoj zaradi iz 12 meseci KOJI PRETHODE MESECU U KOME JE SPREČENOST NASTUPILA; iz nje se utvrđuje prosek po času, koji je osnov za naknadu. Traži LBO iz kartona radnika. Za mesece bez obračuna se po uputstvu upisuje minimalna zarada za taj mesec — taj podatak program nema, pa se ti redovi popunjavaju rukom, a kontrola ih nabraja.\n" +
                "• OBRAZAC OZ-10 ('📋') je spisak obračunatih i isplaćenih naknada zarada za ceo mesec; predaje se filijali u dva primerka. Kolona 'za isplatu' je ono što Fond refundira — bruto naknada uvećana za doprinose na teret poslodavca.\n" +
                "• Zaglavlje oba obrasca se popunjava iz kartona firme: POSEBAN RAČUN na koji Fond uplaćuje refundaciju i ŠIFRA DELATNOSTI. Poseban račun nije isti kao poslovni račun firme.\n" +
                "• Stornirani obračun nije isplaćen, pa se ni ne refundira — u obrazac ne ulazi.\n" +
                "• PRAG OD 30 DANA NE VAŽI ZA SVE OSNOVE. Kod povrede na radu, profesionalne bolesti i davanja tkiva i organa Fond plaća od PRVOG dana; kod nege člana porodice zavisi od toga da li je član mlađi ili stariji od tri godine, pa program tu ništa ne pretpostavlja. Upozorenje o prvih 30 dana se javlja samo tamo gde prag stvarno postoji.\n" +
                "• KNJIŽENJE: refundirana naknada NIJE trošak poslodavca. Ne ide na 520/521 nego se knjiži kao POTRAŽIVANJE od Fonda na kontu 225, uz obaveze na 454 (neto), 455 (porez i doprinosi zaposlenog) i 456 (doprinosi poslodavca). Nalog za knjiženje to radi sam; iznos na 225 je jednak koloni 'za isplatu' obrasca OZ-10. Potraživanje se zatvara u ERPiFinansije, izvodom posebnog računa kad refundacija stigne.\n" +
                "• ZAHTEV SE OD 01.04.2026. PODNOSI ELEKTRONSKI, kroz sistem 'eBolovanje – Poslodavac' na Portalu eUprava — papirna predaja filijali više nije put. Rok za tip 'Naknada zarade' je 15 dana od isplate zarade ostalim zaposlenima; za tip 'Refundacija' (kad je poslodavac već isplatio) rok je 3 godine. Program to ne može da zameni; obrasci ovde služe za pripremu i proveru brojeva PRE unosa u portal, i kao arhivski trag.\n" +
                "• U portalu se period i uzrok bolovanja preuzimaju iz doznake, a poslodavac unosi 'Prva isplata za bolovanje' i broj dana — isto što se ovde evidentira. Podaci o zaradi iz 12 meseci (sekcija 'Potvrda o ostvarenoj zaradi') unose se ručno ili učitavanjem XML fajla, i traže se SAMO kod prve isplate za to bolovanje. Pet polja koja portal traži — mesec i godina, ukupan broj plaćenih časova, neto, bruto i datum isplate — su tačno kolone obrasca OZ-7 iz ovog programa.",
            Kljuc = "Bolovanja"
        },
        new PomocTema
        {
            Naslov = "📗 Konta za knjiženje",
            Sadrzaj =
                "Meni '📗 Konta za knjiženje' drži konta na koja idu obaveze i troškovi po obračunu. Menja se SAMO broj konta — svaki red je uloga koju program traži po imenu, pa se redovi ne dodaju i ne brišu.\n\n" +
                "Podrazumevani brojevi su iz Pravilnika o Kontnom okviru za privredna društva, zadruge i preduzetnike:\n" +
                "• 520 — troškovi zarada i naknada zarada (bruto); tu ide i godišnji odmor, praznik i bolovanje na teret poslodavca\n" +
                "• 521 — troškovi doprinosa na teret poslodavca\n" +
                "• 522–526 — troškovi naknada po ugovorima van radnog odnosa (522 ugovor o delu, 523 autorski, 524 privremeni i povremeni poslovi, 525 ostali ugovori, 526 organi upravljanja i nadzora)\n" +
                "• 529 — ostali lični rashodi (neoporeziva primanja)\n" +
                "• 450 — obaveze za neto zarade; 451 porez na teret zaposlenog; 452 doprinosi na teret zaposlenog; 453 porezi i doprinosi na teret poslodavca\n" +
                "• 465 — obaveze prema fizičkim licima za naknade po ugovorima\n" +
                "• 469 — ostale obaveze (obustave iz zarade); 489 — ostale obaveze za poreze i doprinose\n\n" +
                "• Firma koja vodi analitiku (npr. 520-1 po poslovnoj jedinici) upisuje svoje brojeve — nova verzija programa za to nije potrebna.\n" +
                "• Dugme '↩' vraća podrazumevane brojeve; traži potvrdu, jer briše unetu analitiku.\n" +
                "• Konto bez broja zaustavlja izvoz naloga — glavna knjiga takav dokument odbija.",
            Kljuc = "KontaKnjizenja"
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
                "• Zašto se u 'Radni sati' pojavila lista 'Isplata'? Zato što mesec ima više od jedne isplate. Sati se unose za jednu isplatu: prebacivanjem liste vidite i uređujete sate te isplate, a sati ostalih ostaju netaknuti. Dok je isplata jedna, lista je onemogućena i sve radi kao ranije.\n\n" +
                "• Uneo sam sate, a u obračunu ih nema? Proverite da je u 'Novi obračun' izabrana ISTA isplata za koju su sati uneti — svaka isplata ima svoje sate.\n\n" +
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
