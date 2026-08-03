# 🧭 Plan nastavka razvoja — ERPiZarade

> Radni dokument za nastavak posla u novoj sesiji. Prati razvojnu mapu iz
> [`ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md`](ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md) i beleži
> šta je urađeno, šta je namerno odloženo i **na čemu se stalo zbog podataka koji nedostaju**.
>
> Stanje na dan **03.08.2026**, verzija **1.11.0**, 272 testa.

---

## 1. Gde smo

| Faza | Stavka | Status | Verzija |
| :--- | :--- | :--- | :--- |
| **0** | Model podataka, revizioni trag, pre-flight kontrole | ✅ | 1.2.0 |
| **1.1** | Nalozi za prenos (Halcom TXT, trezorski ePP JSON) | ✅ | 1.3.0 |
| **1.2** | Platni listići e-mailom, PDF sa lozinkom, evidencija slanja | ✅ | 1.3.0 |
| **1.3** | Godišnji obrazac PPP-PO | ✅ | 1.4.0 |
| **1.4** | Kalendar praznika i automatski fond sati | ✅ | 1.4.0 |
| **1.5** | Uvoz radnih sati iz Excel/CSV | ✅ | 1.3.0 |
| **2.1** | Šifarnik vrsta primanja + stavke obračuna | ✅ | 1.5.0 |
| — | Prevod zatečenih obračuna na model stavki | ✅ | 1.6.0 |
| **2.5** | Neoporeziva primanja kao parametar | ✅ | 1.7.0 |
| **2.4** | Poreske olakšice kao šifarnik | ✅ | 1.8.0 |
| **2.7** | Storniranje, rekalkulacija, izmenjena prijava | ✅ | 1.9.0 |
| **2.2** | Entitet `Isplata` (više isplata u mesecu) | ✅ | 1.10.0 |
| **2.3** | Ugovori o delu, autorski, PP poslovi, naknade odborima | ✅ | 1.11.0 |
| — | Generator ugovora: šabloni, editor teksta, PDF | ✅ | 1.11.0 |
| **2.6** | Bolovanja preko 30 dana, RFZO obrasci (OZ-7, OZ-10) | ⬜ | |
| **3** | Integracija sa ERPi ekosistemom | ⬜ | |
| **4** | Kadrovski modul | ⬜ | |
| **5** | Web ESS | ⬜ preispitati | |

---

## 2. Blokirano — čeka podatke od korisnika

Ovo su jedina mesta gde je posao stao zbog nečega što se **ne sme pogađati**. U svakom slučaju
struktura je spremna i nedostaje samo zapisivač/čitač.

| Šta | Zašto je stalo | Šta je potrebno |
| :--- | :--- | :--- |
| **Kodni raspored Halcom fajla** | Specifikacija ga ne navodi; postavljen `windows-1250`. Pogrešan izbor ne obara uvoz, ali izobliči „č", „ć", „đ" u imenima. | Jedan izvezen fajl iz banke, ili potvrda posle prvog uvoza. Menja se jedna konstanta u `HalcomPpzWriter`. |
| **ePorezi XML sa BOP-om** | Čitanje je tolerantno (traži polja po značenju naziva) i prijavljuje šta ne prepozna. | Jedan preuzet XML → zamenjuje se tačnim čitanjem u `EPoreziImportService`. |
| **Obrazac PPD (zahtev za povraćaj)** | Podnosi se elektronski kao i PPP-PD, dakle XML-om, ali šema nije javno dostupna. Podaci već postoje u obračunu (`OlaksicaPorez`, `OlaksicaDoprinosi`). | Primer PPD XML-a ili specifikacija. |
| **OL oznake olakšica** | Karton je nudio `01/02/03` za čl. 21v; po Pravilniku o Obrascu PPD važe **OL08/OL09/OL10**. Lista je izvađena iz koda u šifarnik, pa se ispravlja bez nove verzije. | Provera u važećem Katalogu vrste prihoda i ispravka u šifarniku „Poreske olakšice". |
| **JIPD podnetih prijava** | Izmenjena prijava se poziva na JIPD prijave koju menja, a JIPD dodeljuje PU pri prijemu. Polje postoji uz prijavu, ali se ne popunjava samo — čitanje iz `EPoreziImportService` čeka isti XML kao i BOP. | Isti preuzet XML iz reda iznad. |
| **OVP oznake za deo vrsta ugovora** | Potvrđeno je 601/602/603 (ugovor o delu i naknade odborima), 301/302/303 (autorske naknade 50% i 43%) i 150/151 (PP poslovi). Za autorsku naknadu sa **34%** normiranih troškova OVP nije potvrđen i ostavljen je **prazan** — obračun prolazi, ali kontrolna provera javlja grešku. Ostaje i da se potvrdi koji tip primaoca ide uz PP poslove. | Provera u važećem Katalogu vrste prihoda i unos u šifarnik „Vrste ugovora". Bez nove verzije. |

---

## 3. Sledeći koraci, po preporučenom redosledu

### 3.1. Radni sati po isplati *(preporučeno prvo)*

Jedina stavka koju je 2.3 ostavila iza sebe, i jedina koja i dalje traži izmenu šeme.
`RadniSat` je jedinstven po (radnik, godina, mesec), pa obračun druge isplate prepisuje taj
red. **Iznosi već napravljenih obračuna ostaju netaknuti** — svaki obračun nosi svoje sate u
svojim kolonama — ali ekran radnih sati pokazuje poslednji unos.

Razdvajanje je dodavanje `RadniSat.IsplataId` uz izmenu jedinstvenog indeksa, po istom pravilu
kao `ObracunPlate.IsplataId`: `null` znači prvu isplatu perioda, pa se zatečeni redovi ne diraju
(vidi odluku 9). Dodiruje `RadniSatiPage`, `UvozSatiService` i `NoviObracunWindow`.

### 3.2. Faza 3.1 — automatsko knjiženje u ERPiFinansije

`VrstaPrimanja.Konto`, `VrstaUgovora.Konto` i `Radnik.SifraMestaTroska` postoje od ranije i još
se nigde ne koriste — uvedeni su baš za ovo.

### 3.3. Faza 2.6 — bolovanja preko 30 dana i RFZO obrasci

OZ-7 i OZ-10. Vrsta primanja `B60` postoji u šifarniku od 2.1 i obračun je puni, pa je osnova
spremna; nedostaje obrazac za refundaciju.

### 3.4. Namerno odloženo

| Šta | Zašto je odloženo |
| :--- | :--- |
| **Brisanje perioda** | I dalje briše sve isplate meseca odjednom, sada i naknade po ugovoru. Brisanje pojedinačne isplate ide preko ekrana isplata, gde je i zaštićeno. |
| **Zaključavanje po isplati** | Zaključavanje ostaje na periodu. Isplata je **obuhvat, ne stanje** — drugo mesto koje kaže „ovo je zaključano" bilo bi isti duplikat kao nekadašnji `Zakljucan`/`Zakljucen`. |
| **Obrazac M-UN i M-4** | **Ne treba ih ni raditi.** Ukinuti su od 01.01.2019. (čl. 30 Zakona o izmenama i dopunama ZPIO briše čl. 144); Fond PIO podatke preuzima elektronski iz PPP-PD, najkasnije do kraja februara za prethodnu godinu. Stari obrasci važe samo za period zaključno sa 31.12.2018. Ako se u nekoj sesiji „primeti da nedostaju" — ne dodavati ih. |
| **Prijava na osiguranje (obrazac M)** | Podnosi se preko **portala CROSO**, jedinstvenom prijavom za PIO, RFZO i nezaposlenost — dakle van ovog programa, i pre isplate. Za privremene i povremene poslove najkasnije dan pre početka rada. Program to ne može da zameni; zabeleženo je u pomoći kao korak koji se ne sme preskočiti. |
| **Obračunski listić za naknadu** | Primalac po ugovoru ne dobija platni listić — on prikazuje sate, fond i obustave, kojih ovde nema. Zaseban „obračun naknade" bi bio nova štampa, ne izmena postojeće. Generator ugovora (1.11.0) pokriva sam ugovor, ne i obračunski listić uz isplatu. |
| **Bogat format teksta ugovora** | Tekst je običan tekst; podebljava se samo naslov i red koji počinje sa „Član". RTF ili HTML bi značio da se ono što korisnik vidi u editoru više ne poklapa pouzdano sa PDF-om, a i da se dokument ne može uporediti prostim poređenjem teksta. |

---

## 4. Odluke koje ne treba poništavati

Ovo su svesne odluke sa razlogom; nova sesija ih lako „popravi" u pogrešnom smeru.

1. **Ništa što propis menja ne ide u kod.** Vrste primanja, olakšice, MFP mapiranje, neoporezivi
   limiti i praznici su **šifarnici**. Program vodi zarade za više firmi i mora da podrži i ono
   što danas niko ne koristi.

2. **Oznaka olakšice se ne duplira.** Živi na pozicijama 7–8 SVP šifre u `Radnik.Radno_Mesto`;
   `PoreskaOlaksica.Sifra` se ključuje po njoj. Ne dodavati `Radnik.SifraOlaksice` — to bi bio
   isti duplikat kao nekadašnji `Zakljucan`/`Zakljucen`.

3. **Stavke obračuna su razlaganje, ne novi obračun.** Stare kolone ostaju netaknute, pa svi
   ekrani i štampe rade nepromenjeno. Test `Stavke_ZbirJednakUkupnomBrutoIznosu` drži kriterijum.

4. **Povraćaj i oslobođenje se ne smeju izjednačiti.** Povraćaj **ne dira nijedan iznos**
   obračuna; oslobođenje umanjuje i prijavljuje se kroz MFP.

5. **Porezi i doprinosi idu JEDNOM uplatom** na objedinjeni račun sa BOP-om kao pozivom na broj.
   Koordinate plaćanja u šifarnicima `Doprinos` i `Porezi` su ostatak režima pre 01.03.2014. i
   generator ih namerno zaobilazi.

6. **Fond sati se ne nasleđuje od prethodnog meseca** — računa se iz kalendara. Ranije
   nasleđivanje je menjalo platu svakom radniku.

7. **Storno ne nulira iznose.** Stornirani obračun ostaje u bazi sa svim iznosima; menja se
   samo to da ga isplate i prijave preskaču. Nuliranje bi izbrisalo dokaz šta je bilo
   obračunato i prijavljeno, a upravo se to pri kontroli traži.

8. **Rata kredita se vraća tačno jednom.** `KreditRateService` je jedini izvor te računice;
   prekalkulacija i brisanje preskaču obračune kojima je rata već vraćena storniranjem. Ne
   dodavati novo mesto koje samo skida ili vraća rate.

9. **`ObracunPlate.IsplataId == null` znači prvu isplatu perioda.** To je ono što drži da se
   ništa ne menja dok mesec ima jednu isplatu — svi zatečeni obračuni i svi koje naprave
   ekrani radnih sati, poreza i doprinosa ostaju obuhvaćeni bez ijedne izmene. Pravilo je
   napisano **jednom**, u `IsplataService.Obuhvat`; ne prepisivati ga u upite. Ne praviti
   kolonu obaveznom — to bi zahtevalo da svaki od tih ekrana zna za isplate.

10. **Isplata se za PPP-PD prijavu vezuje rednim brojem.** `PppPdPrijava.RedniBroj` postoji od
    Faze 1.1 baš za to. Ne dodavati `PppPdPrijava.IsplataId` — bio bi duplikat, a redni broj
    je već ključ jedinstvenog indeksa (Godina, Mesec, RedniBroj).

11. **Obustave nosi samo konačna zarada.** Rate kredita i samodoprinos se skidaju isključivo na
    isplati vrste `KonacnaZarada`; akontacija, bonus i 13. plata idu bez njih. To je jedini
    razlog zašto mesec sme imati **samo jednu** konačnu zaradu — bez tog ograničenja bi se
    ista rata skinula dvaput. Ne dozvoljavati drugu, i ne skidati obustave na ostalim vrstama.

12. **Brisanje isplate iz sredine se ne dozvoljava.** Redni brojevi vezuju isplate za podnete
    prijave; pomeranje bi ostavilo prijavu uz pogrešnu isplatu. Briše se samo poslednja, i
    samo dok nema ni obračuna ni prijave.

13. **Naknada van radnog odnosa je `ObracunPlate`, ne novi entitet.** Nosi ista polja kao
    zarada (bruto, porez, doprinosi, neto), pa PPP-PD prijava, nalozi i godišnja potvrda rade
    nad njom bez ijedne izmene. Razlikuje je samo `UgovorId`. Zaseban entitet bi značio drugi
    tok kroz svaki od tih izvoza — a prijava se ionako podnosi **jedna po isplati**, sa svim
    prihodima tog dana.

14. **Šifra vrste prihoda za ugovore se sastavlja, ne upisuje.** Struktura `V-PP-OVP-OL-B` je
    propisana i stabilna; menja se sadržaj. U šifarniku stoji samo `VrstaUgovora.Ovp` (tri
    cifre), tip primaoca se bira uz ugovor, a `SvpService.Sastavi` ih spaja. Ne dodavati polje
    sa celom devetocifrenom šifrom — svaka kombinacija posla i statusa osiguranja bi tražila
    svoj red u šifarniku.

15. **Bez potvrđenog OVP-a šifra ostaje prazna.** Izmišljena šifra prolazi generisanje i pada
    tek kod Poreske uprave, kada je novac već isplaćen. Prazna se hvata kontrolnom proverom,
    dok je ispravka još jeftina. Isto pravilo kao kod neoporezivog limita koji nije unet.

16. **Osnovica doprinosa se upisuje samo kad se ne može izvesti.** `ObracunPlate.OsnovicaDoprinosa`
    je `null` za svaku zaradu — tamo se i dalje izvodi iz zbira PIO doprinosa, pa se nijedna
    zatečena prijava ne menja. Popunjava se samo za naknade van radnog odnosa, gde je osnovica
    bruto umanjen za normirane troškove. Ne praviti kolonu obaveznom.

17. **Prekalkulacija zarada ne dira naknade po ugovoru.** One ne nastaju iz sati i koeficijenata
    koji se ponovo računaju, nego zasebnom radnjom nad ugovorom. Bez uslova `UgovorId == null`
    u prekalkulaciji bi ih obračun zarade tiho obrisao.

18. **Tekst ugovora se čuva uz ugovor, ne uz šablon.** `Ugovor.Tekst` je snimak dokumenta
    kakav je potpisan; šablon je samo polazna tačka koja se s vremenom menja. Ne izvoditi tekst
    iz šablona pri svakom prikazu — izmena formulacije bi tada naknadno menjala već zaključene
    ugovore.

19. **Iznosi se iz teksta ugovora ne čitaju.** Obračun ide isključivo iz polja `Ugovor` i
    `VrstaUgovora`. Tekst je dokument, a ne izvor podataka; da je obrnuto, ispravka slovne
    greške bi menjala isplatu.

20. **Zamena polja u šablonu nema uslova ni petlji.** Traži se `{Polje}` i menja vrednošću —
    ništa više. Šablon sa granama bio bi program koji niko ne testira, a piše ga knjigovođa.
    Polje koje se ne prepozna ili nije popunjeno **ostaje vidljivo** i prijavljuje se; ne
    brisati ga tiho, jer se praznina na mestu iznosa primeti tek posle potpisa.

---

## 5. Način rada koji se pokazao dobrim

- **Novac se ne menja bez testa.** Svaka izmena obračuna ide uz test koji fiksira pravilo, i uz
  test da se **bez nove funkcije rezultat ne menja**. Taj drugi je uhvatio više grešaka nego prvi.
- **Format se ne piše napamet.** Halcom, trezorski ePP i MFP su napisani tek posle nalažene
  specifikacije. Gde specifikacije nema, radi se tolerantno i **prijavljuje šta nije prepoznato**.
- **Release build mora biti bez upozorenja** — CI ih tretira kao greške
  (`Directory.Build.props`), pa upozorenje u Debug-u obori objavu.
- **InMemory provajder nije SQLite.** Testovi rade nad `UseInMemoryDatabase`, koji prihvata i
  ono što SQLite odbija — najopasnije je `SUM` nad `decimal` kolonom, koje na strani baze pada
  sa „cannot apply aggregate operator 'Sum' on expressions of type 'decimal'". Takva greška
  prođe kroz ceo paket testova i pojavi se tek kod korisnika. Zato: zbrajanje decimalnih
  kolona se radi **u memoriji**, posle `ToList()`, a upit koji zbraja stoji u servisu i ima
  test nad **pravim SQLite fajlom** (`PlataDbContextMigrationTests`).
- **Migracija se ne regeneriše bez provere zatečenih baza.** `ef migrations remove` + `add`
  daje nov vremenski žig uz isti naziv, pa baza koja je stigla da primeni staru verziju pada
  pri sledećem pokretanju. Od 1.10.0 to hvata `UskladiPreimenovaneMigracije`, ali razliku u
  sadržaju dve verzije i dalje mora neko da namiri — u
  `DopuniKoloneIzRegenerisanihMigracija`. Bolje je dopisati novu migraciju nego prepravljati
  postojeću.
- **PDF specifikacije koje WebFetch ne pročita** otvaraju se lokalno PyMuPDF-om; tako su dobijeni
  i Halcom format i struktura MFP-a.
- Redosled objave: izmene → `dotnet build -c Release` → `dotnet test` → CHANGELOG → `version.txt`
  → commit → push. **Push na `main` objavljuje verziju korisnicima** (Velopack), pa ga pokretati
  samo na izričitu reč.

---

## 6. Pre nego što korisnik testira

- **Rezervna kopija baze** (Podešavanja → Rezervna kopija). Od 1.2.0 naovamo primenjuje se
  dvanaest migracija, od kojih jedna briše kolonu.
- Prevod zatečenih obračuna na stavke (Vrste primanja → 🔀) — **prvo proba**, koja ništa ne
  upisuje, pa tek onda potvrda.
- Storniranje se proba na **jednom** obračunu (Obračun plate → 🚫), pa se proveri da tog
  radnika više nema u nalozima za prenos, listićima i PPP-PD prijavi, a da mu je rata kredita
  vraćena. Poništavanje storna (↩) vraća sve u pređašnje stanje.
- **Isplate (1.10.0) se prvo proveravaju „na prazno".** Otvoriti „💸 Isplate u mesecu" za
  zatečeni mesec i potvrditi da postoji **jedna** isplata („1. Konačna zarada") sa tačnim
  brojem obračuna i netom — sve dok je isplata jedna, prijave, nalozi i listići moraju dati
  isti rezultat kao u 1.9.0.
- Tek onda probati drugu isplatu: dodati akontaciju (➕), obračunati je u „Obračun plate"
  biranjem te isplate, pa proveriti da **prvoj isplati nije ništa promenjeno** i da akontacija
  nije skinula ratu kredita.
- **Ugovori (1.11.0) se prvo proveravaju na šifarniku.** Otvoriti „📄 Vrste ugovora" i
  potvrditi OVP oznake iz važećeg Kataloga vrste prihoda — naročito autorsku naknadu sa 34%,
  koja je namerno ostavljena prazna. Dok OVP nije unet, kontrolna provera javlja grešku.
- Zatim primalac: na ekranu ugovora **„＋ novi"** uz padajuću listu primalaca — otvara unos
  novog kartona (ime, JMBG, adresa, opština, tekući račun) ili označavanje postojećeg kartona.
  Isto se može uraditi i u „Radnici", ali tek pošto se karton otvori dugmetom **„Izmeni"** —
  polja su van režima izmene onemogućena, pa čekboks tamo ne reaguje.
- Proveriti da se označeno lice posle toga **ne pojavljuje** u „Obračun plate", „Radni sati"
  ni „Platni listići", a da su mu zatečene zarade ostale netaknute.
- Tek onda ugovor: „📝 Ugovori van radnog odnosa" → izabrati vrstu, primaoca, tip primaoca i
  iznos. Računica se vidi **pre** upisa — proveriti brojeve rukom na jednom primeru (bruto
  50.000 po ugovoru o delu daje neto 32.400 uz porez 8.000 i PIO 9.600), pa tek onda 🧮.
- Posle obračuna naknade proveriti da se u PPP-PD prijavi te isplate pojavio **novi red** sa
  svojom SVP šifrom i nulama u satima, a da je **red zarade ostao brojčano isti**, i da je u
  nalozima za prenos naknada dobila svrhu po predmetu ugovora.
- **Za generator ugovora prvo popuniti zastupnika** u kartonu firme (Firme → Zastupnik i
  Funkcija zastupnika). Bez toga generisani dokument prijavljuje `{FirmaZastupnik}` kao
  nepopunjeno polje i ostavlja ga vidljivim u tekstu — što je namerno.
- Zatim 📄 na izabranom ugovoru → „Generiši iz šablona" → pročitati ceo tekst i uporediti sa
  onim što firma inače potpisuje. Formulacije se menjaju u „🖋️ Šabloni ugovora"; izmena
  šablona **ne dira** tekstove već zaključenih ugovora.
- Proveriti **iznos slovima** na jednom primeru pre nego što dokument ode na potpis — razlika
  brojke i slova tumači se u korist slova.
