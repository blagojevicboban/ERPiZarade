# 📋 Istorija izmena (Changelog) — ERPiZarade (ObracunZarada)

Sve značajne promene i novine u aplikaciji **ERPiZarade** dokumentovane su u ovom fajlu.

Format je zasnovan na [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardu i prati Semantic Versioning.

---

## [1.17.0] - 2026-08-05

> **Prekoračenje neoporezive dnevnice iz ERPiFinansije sad ulazi u zaradu (Faza 3.2).** Deo
> dnevnice za službeni put u zemlji iznad zakonskog neoporezivog iznosa (2026: 3.471 RSD) po
> zakonu je deo zarade radnika, ne trošak firme — a radnik ga je već primio kroz putni nalog.

### 💸 Uvoz iz ERPiFinansije (`PutniNaloziImportService`, novi ekran u „Primanja")

- ERPiFinansije (od 1.6.0) računa prekoračenje po nalogu (isplaćena dnevnica minus zakonski
  limit × broj dnevnica) i izvozi ga u JSON za period isplate — knjigovođa ga uvozi kroz novo
  dugme „📥" na ekranu „Primanja".
- Uparivanje radnika ide **isključivo preko JMBG-a** (novo polje na putnom nalogu u
  ERPiFinansije) — bez pogađanja po imenu. Kontrolne provere zaustavljaju uvoz kad JMBG
  nedostaje, ne postoji ili pripada više radnika u istom mesecu, kad je fajl pogrešnog formata
  ili novije verzije, i kad je nalog već ranije uvezen.
- Prekoračenje ide **isključivo na konačnu zaradu** meseca, nikad na akontaciju — pravi se sama
  (isto pravilo kao svuda) ako mesec još nema nijednu isplatu; ako mesec ima isplate a nijedna
  nije konačna zarada, uvoz se zaustavlja umesto da pogodi pogrešnu.
- Dva putna naloga istog radnika u istom mesecu se **spajaju** u jedan red — isto pravilo koje
  ekran „Primanja" već traži za svaku vrstu primanja.

### 🧮 „Već isplaćeno van obračuna" — nova mehanika u `ObracunService`

Radnik dnevnicu dobija odmah, gotovinom ili na račun, kroz sam putni nalog — pre nego što
obračun zarade uopšte postoji. Da se prekoračenje prosto doda kao obično oporezivo primanje,
`NetoIsplata` formula bi taj novac isplatila **drugi put** kroz platni spisak.

- Nova `VrstaPrimanja.VecIsplacenoVanObracuna`: iznos i dalje ulazi u bruto, poresku osnovicu i
  osnovicu doprinosa (ispravno za PPP-PD), ali se **ne dodaje** u `NetoIsplata` — porez i
  doprinosi na taj deo efektivno padaju na ostatak plate istog meseca, isto kao da je
  prekoračenje bio običan gross bonus isplaćen kroz platu, samo je glavnica već stigla drugim
  kanalom.
- Nova sistemska vrsta primanja **„DNP" — Prekoračenje neoporezive dnevnice** (šifarnik „Vrste
  primanja"), sa `NeoporeziviLimit = 0` jer je limit već primenjen na strani ERPiFinansije.
- **Konto za DNP ostaje prazan** dok se ne potvrdi da li se prekoračenje uopšte knjiži na
  strani zarada — puni iznos dnevnice je već proknjižen u ERPiFinansije (5330/4650); dodatno
  knjiženje u „Nalog za knjiženje" zarada bi isti novac proknjižilo dvaput. `KnjizenjeService`
  već baca grešku za praznu vrstu koja se koristi u obračunu, pa je prazno bezbedno dok se ne
  odluči.
- **Tretman bruto/neto nije potvrđen kod knjigovođe** — kod pretpostavlja da je uvezeni iznos
  bruto (porez/doprinosi se računaju direktno na njega), ne neto koje bi trebalo bruto-uvećati.
  Videti `PLAN_NASTAVKA.md`, Faza 3.2, otvorena pitanja.

### 🔧 Popravka: `UnetoPrimanje` sad zna za isplatu

Do sada je `UnetoPrimanje` bio vezan samo za (radnik, period), bez `IsplataId` — mesec sa dve
isplate (akontacija + konačna zarada) bi isti uneti iznos obračunao **dvaput**. Popravljeno je
uzgred jer je preduslov za tačan uvoz dnevnice: `UnetoPrimanje` sad implementira
`IPripadaIsplati` i prolazi kroz `IsplataService.Obuhvat`, isto kao obračun i radni sat.
Migracija zatečene redove vezuje za prvu isplatu perioda (isti obrazac kao `Faza2_RadniSatiPoIsplati`).

### ✅ Nova kontrolna provera

„Prekoračenje dnevnice bez obračuna" (`PreFlightService`) — upozorava kad je primanje uvezeno,
a obračun te isplate još ne postoji ili nije osvežen; bez ove provere bi zaboravljen ponovni
obračun ostavio radnikovu zaradu kratku za tačno taj iznos, nezapaženo.

### Obim

Samo dnevnice **u zemlji** i samo **prekoračenje dnevnice** (ne i drugi troškovi putnog naloga
— gorivo, smeštaj, prevoz ostaju čist trošak firme). Inostranstvo i JSON/REST živa sinhronizacija
ostaju van obima. Detaljan plan i otvorena pitanja: `PLAN_NASTAVKA.md`, Faza 3.2.

---

## [1.16.0] - 2026-08-04

> **Isplate van radnog odnosa odvojene su od zarada.** Do sada su naknade po ugovoru ulazile u
> **istu** PPP-PD prijavu kao zarada — a po propisu ne smeju. Uz to je otklonjeno i to što se
> zaposlenom nije mogao obračunati ugovor o delu, iako je propisom predviđen.

### ⚖️ Zašto — član 11 Pravilnika o poreskoj prijavi za porez po odbitku

Polje **1.2 Obračunski period** se za dva roda prihoda popunjava različito:

- **zarada** — „mesec i godina *za koji* se vrši isplata zarade";
- **van radnog odnosa** — mesec **isplate**, jer meseca „za koji" nema.

Prijava ima **jedno** polje 1.2, **jedno** polje 1.4 (datum plaćanja) i jednu oznaku K/A, pa
julska zarada isplaćena u avgustu i honorar isplaćen istog dana ne mogu u istu prijavu: prva
nosi period 07, drugi 08. Oznaka „K" se uz to odnosi na konačnu isplatu **zarade**, pa honorar
u prijavi akontacije dobija „A", što za njega ne znači ništa.

### 💸 Rod isplate

- `Isplata` dobija **rod**: `Zarada` ili `VanRadnogOdnosa`. Podrazumevano je zarada, pa se
  **nijedna zatečena isplata ne menja** i mesec sa jednom isplatom radi kao u 1.15.0.
- Za rod van radnog odnosa `Godina`/`Mesec` znače **mesec isplate** — to je obračunski period
  njene prijave. Datum isplate ga i određuje: unese se datum, a period sledi iz njega.
- Mesec sme imati **koliko treba** isplata naknada — svaki datum je svoja prijava. Ograničenje
  „jedna konačna zarada mesečno" postoji zbog obustava i na naknade se ne odnosi.
- Oznaka konačne isplate je za naknade **uvek „K"** i na ekranu zaključana.
- **Redni broj 1 ostaje zaradi**, čime obračuni bez upisane isplate i dalje pripadaju njoj.
- Obračun naknade na isplatu zarade se **odbija pre upisa**, a zatečeno mešanje hvata nova
  kontrolna provera **„Pomešani rodovi u istoj isplati"**.

### 🧭 Meni podeljen na dva puta kroz posao

Grupa „ŠTAMPA" — koja odavno nije bila štampa nego ceo put od obračuna do knjiženja — podeljena
je na **ISPLATE ZARADA**, **ISPLATE VAN RADNOG ODNOSA** i **IZVEŠTAJI**.

- PPP-PD, nalozi za prenos, nalog za knjiženje i isplate u mesecu pojavljuju se **u obe grupe**,
  otvoreni sa rodom već izabranim — pogrešan rod se tako ne može ni izabrati.
- „Vrste ugovora" i „Šabloni ugovora" preselili su se iz ŠIFARNICI u novu grupu, a „Obračun
  plate" iz EVIDENCIJA u ISPLATE ZARADA.
- **PPP-PO ostaje van oba roda**, u IZVEŠTAJI: godišnja potvrda obuhvata sve prihode jednog
  lica, i zaradu i honorar.

### 👤 Zaposleni sme biti primalac po ugovoru

Šifra vrste prihoda za honorar zaposlenog je `1 01 601 00 0`, gde `01` znači „zaposleni". Dva
mesta su to do sada onemogućavala:

- ekran ugovora je nudio **samo** lica sa oznakom „van radnog odnosa", pa se zaposleni nije
  mogao ni izabrati;
- označavanje postojećeg kartona kao primaoca upisivalo je oznaku u **sve** periode tog lica —
  čime bi zaposleni tiho nestao iz obračuna plate, radnih sati i platnih listića.

Sada oznaka `VanRadnogOdnosa` znači **samo** „nije u radnom odnosu"; ko je primalac kaže sam
ugovor. Za lice u radnom odnosu se pri izboru **ništa ne menja u kartonu**, a kontrolna provera
o neoznačenom kartonu ćuti za tipove primaoca **01** i **02**, gde je lice legitimno i radnik.
Karton koji se prepisuje u mesec isplate je od sada **verna** kopija — ranije je izostavljao
koeficijent i osnovnu platu, što bi zaposlenom dalo pogrešnu zaradu u tom mesecu.

### 👥 Novi ekran „Primaoci po ugovoru"

Pogled nad **istim** registrom kao „Radnici", sa brojem ugovora, brojem isplata i isplaćenim
bruto iznosom. Registar se namerno **ne deli**: `PppPoService` grupiše po broju radnika kroz sve
obračune, pa bi zaseban registar primalaca istom licu izdao **dve** godišnje potvrde umesto
jedne. Lice koje je i zaposleno i primalac se ovde vidi sa oznakom „i u radnom odnosu".

### 🏷️ Tipovi primaoca prihoda dopunjeni do 13

Pravilnik uz polje 3.6 nabraja **13** oznaka vrste primaoca; šifarnik je imao prvih osam.
Dodate su **09** (penzioner po osnovu zaposlenosti), **10** (penzioner po osnovu samostalne
delatnosti), **11** (van radnog odnosa, bez doprinosa), **12** (vojni penzioner) i **13**
(poljoprivredni penzioner).

Oznaka **11** je bila stvarna prepreka: bez nje se OVP 315–321 — prihod autora od imovinskog
prava, samostalni umetnik koji doprinose plaća po rešenju PU, maloletno lice — nisu mogli
prijaviti uopšte.

### 🧪 Testovi
- 337 → **352**. Novi drže: da zatečene isplate posle migracije ostaju zarade (nad **pravim
  SQLite fajlom**), da obračunski period naknade bude mesec isplate, da zarada i naknada daju
  **dve različite prijave** dok prijava zarade ostaje brojčano ista, i da zaposleni sa ugovorom
  o delu dobije obračun, **ostane** u obračunu zarade i dobije **jednu** PPP-PO potvrdu sa dva
  reda.

---

## [1.15.0] - 2026-08-04

> **Faza 2.6** — bolovanja preko 30 dana i obrasci kojima se od RFZO traži refundacija
> isplaćene naknade zarade: **OZ-7** i **OZ-10**. Vrsta primanja `B60` postoji od Faze 2.1 i
> obračun je puni; nedostajao je obrazac.

### 🏥 Bolovanja i RFZO (novi ekran)
- Evidentira se **za koje dane i po kom osnovu** je naknada isplaćena, i da li je to prva isplata iz sredstava Fonda. **Nijedan iznos se ne unosi** — iznosi stoje u stavkama obračuna. Ponovljen iznos bi bio treći zapis istog novca, pored obračuna i pored naloga za knjiženje, i prvi bi se razišao sa ostalima.
- Uz svako bolovanje ide i **datum početka sprečenosti**, odvojen od perioda za koji se traži refundacija: prvih 30 dana nosi poslodavac, a od 31. dana Fond. Bez toga se ne zna koji je dan po redu, pa kontrola upozorava kada period počinje unutar prvih 30 dana.
- Osam osnova sprečenosti, po kolonama obrasca: bolest, povreda na radu, profesionalna bolest, nega člana porodice (65% i po čl. 78. st. 3), izolacija i praćenje, davalac tkiva i organa, održavanje trudnoće.

### 📋 Obrazac OZ-10 — spisak obračunatih i isplaćenih naknada zarada
- Sastavlja se za ceo mesec, jedan red po bolovanju. Kolone i njihov redosled prate obrazac sa sajta Fonda, uključujući i numeraciju od nule.
- **Formule sa obrasca drži kod, ne korisnik**: bruto naknada = 15 + 17 + 18, a kolona „za isplatu" = 15 + 16 + 17 + 18. Neto se računa kao ostatak bruta, pa kontrola izlazi i posle zaokruživanja. Oba pravila imaju test.
- Porez i doprinosi se dele **srazmerno udelu naknade u zbiru stavki** — obračun ih ne vodi po stavkama. Za pun mesec bolovanja udeo je ceo obračun, pa podele ni nema; za mešovit mesec je to jedina podela čiji se zbir slaže sa PPP-PD prijavom.
- Stornirani obračun nije isplaćen, pa se ni ne refundira.

### 🖨️ Obrazac OZ-7 — potvrda o ostvarenoj zaradi
- Osnov se uzima iz **12 meseci koji prethode mesecu u kome je sprečenost nastupila** — ne mesecu isplate. Iz njega se dobija prosek po času, po kome se naknada obračunava.
- Kolona 3 je zarada **bez poreza i doprinosa**, a ne neto za isplatu: obustave su radnikov trošak i ostvarenu zaradu ne umanjuju. Da se pomešaju, naknada bi svakom radniku sa kreditom ispala niža.
- Broj časova se uzima iz **stavki obračuna**; obračun bez stavki (zatečen pre Faze 2.1) pada na zbir kolona, dopunjen legacy satima za bolovanje preko 30 dana, porodiljsko i plaćeno odsustvo po zakonu.
- **Mesec bez obračuna ostaje prazan i prijavljuje se.** Po uputstvu uz obrazac se za mesece van radnog odnosa upisuje minimalna zarada za taj mesec — program taj podatak nema, a izmišljen iznos bi ušao u prosek po kome se naknada isplaćuje.

### 💰 Šifarnik: naknada na teret Fonda
- `VrstaPrimanja` dobija oznaku **„Na teret Fonda"**. Šta Fond refundira propisuje Zakon o zdravstvenom osiguranju, pa to stoji u šifarniku, a ne u kodu — isto pravilo po kome su tamo i SVP šifra i konto.
- Podrazumevano je označeno **samo „Bolovanje preko 30 dana"**; migracija to upisuje i u zatečene baze, jer dopuna šifarnika pri pokretanju dodaje samo vrste kojih nema. Ko refundira i naknadu za povredu na radu ili negu člana porodice, označi i te vrste.

### 👤 Novi podaci u kartonima
- Radnik: **LBO** — lični broj osiguranika sa zdravstvene kartice, koji traži OZ-7. JMBG ga ne zamenjuje.
- Firma: **poseban račun** na koji Fond uplaćuje refundaciju (nije isti kao poslovni račun), **šifra delatnosti** i **podračun poslovne jedinice**.
- **Pol osiguranika se ne čuva** — izvodi se iz JMBG-a (cifre 10–12). Zasebno polje bi bila druga vrednost koja mu može protivrečiti.

### 📒 Knjiženje refundacije
- **Refundirana naknada nije trošak poslodavca.** Kontni okvir joj ne daje samo druga konta obaveza nego je i **potpuno izvodi iz grupe 52**: umesto troška nastaje **potraživanje od Fonda na kontu 225**, a obaveze idu na **454** (neto), **455** (porez i doprinosi na teret zaposlenog — jedan konto, za razliku od 451 i 452) i **456** (doprinosi na teret poslodavca). Do sada je sve to išlo na 520/450, što je naduvavalo trošak za iznos koji Fond vraća.
- Iznos na kontu **225 je jednak koloni „za isplatu" obrasca OZ-10** — isti novac, isti izvor računice (`RfzoService.DeoNaTeretFonda`), pa se nalog i obrazac ne mogu razići. To drži test.
- **Obustava se skida prvo sa zarade**, a tek kad zarade nema — pun mesec bolovanja — pada na naknadu. Bez tog redosleda bi konto 450 za takav mesec ispao negativan, a takav nalog glavna knjiga odbija. Potraživanje od Fonda obustava ne dira: Fond refundira obračunato, ne isplaćeno.
- Četiri nova reda u šifarniku „Konta za knjiženje"; firma koja vodi analitiku menja brojeve kao i do sada.
- **Obračun bez naknade na teret Fonda daje nepromenjen nalog** — konto 450 je i dalje jednak zbiru naloga za prenos. Test to fiksira.

### ⚠️ Zahtev se od 01.04.2026. podnosi elektronski
- Sistem **„eBolovanje – Poslodavac"** je obavezan od 01.01.2026, a od **01.04.2026** se zahtev za obračun i refundaciju podnosi **isključivo elektronski**, preko Portala eUprava. Papirna predaja filijali više nije put, i program to ne može da zameni — zabeleženo je u pomoći, isto kao za CROSO.
- Obrasci OZ-7 i OZ-10 zato služe za **pripremu i proveru brojeva pre unosa u portal** i kao arhivski trag. Pet polja koja portal traži u sekciji „Potvrda o ostvarenoj zaradi" — mesec i godina, ukupan broj plaćenih časova, neto, bruto i datum isplate — su tačno kolone obrasca OZ-7.
- Portal prima i **XML fajl** sa podacima o zaradi umesto ručnog unosa, ali njegova šema nije javno objavljena. Dok se ne dobije primer, fajl se ne piše napamet — isto pravilo kao kod Halcom formata i ePorezi XML-a.

### 🩹 Ispravke
- **Prag od 30 dana ne važi za sve osnove.** Kod povrede na radu, profesionalne bolesti i davanja tkiva i organa Fond plaća od **prvog** dana, a kod nege člana porodice zavisi od uzrasta člana — što zapis ne nosi. Kontrolna provera je do sada upozoravala na svih osam osnova; sada samo tamo gde prag stvarno postoji. Netačno upozorenje je gore od nikakvog, jer nauči korisnika da nalaze preskače.

## [1.14.0] - 2026-08-04

> **Faza 3.1** — automatsko knjiženje u ERPiFinansije. Prva veza sa ostatkom ERPi ekosistema.
> `VrstaPrimanja.Konto`, `VrstaUgovora.Konto` i `Radnik.SifraMestaTroska` postoje od ranijih faza
> i do sada ih nije koristio niko — uvedeni su baš za ovo.

### 📒 Nalog za knjiženje (novi ekran)
- Temeljnica se **izvodi iz obračuna svaki put iznova**; ništa se ne upisuje. Zato se izmena konta odmah vidi, a pogrešan izvoz se ispravlja ponovnim izvozom umesto storniranjem.
- **Trošak** ide na konto upisan uz vrstu primanja, odnosno uz vrstu ugovora, i deli se po **šifri mesta troška** iz kartona radnika. Obaveze se po mestima troška ne dele — obaveza prema radniku je jedna bez obzira gde je radio.
- **Iznosi su isti oni koje obračun već nosi**: konto neto obaveza se poklapa sa zbirom naloga za prenos, a porez i doprinosi sa PPP-PD prijavom. Ništa se ne računa iznova — račun na drugom mestu ume da se raziđe sa prvim.
- **Neoporeziva primanja ulaze u trošak iako nisu u bruto iznosu.** Prevoz i jubilarna nagrada se isplaćuju radniku, a po zakonu nisu ni u poreskoj osnovici ni u osnovici doprinosa. Zato se trošak uzima iz **stavki obračuna**, a ne iz bruta; jedino se tako nalog uravnoteži.
- Svaka **isplata** se knjiži zasebnim nalogom, sa svojim datumom. Stornirani obračuni se ne knjiže.
- Izvoz u **JSON** za ERPiFinansije i u **CSV** za proveru u tabeli. CSV se sme snimiti i kad nalog nije spreman — upravo se u njemu i traži gde je razlika.

### 🧮 Kontrole pre knjiženja
- **Nalog koji nije u ravnoteži se ne izvozi.** Glavna knjiga takav dokument odbija, a razlika se u njoj više ne može vezati za obračun iz kog je došla.
- **Sastav obračuna se proverava po radniku**: bruto (odnosno zbir stavki) umanjen za porez, doprinose i obustave mora dati isplaćen neto. Neslaganje se javlja sa imenom radnika, dok je ispravka još jeftina.
- Vrsta primanja ili vrsta ugovora **bez konta** je greška, ne tiho svrstavanje na zbirni konto — trošak bi završio na pogrešnom mestu, a to se otkriva tek u bilansu.
- Obračun **bez stavki** (zatečen pre Faze 2.1) ide ceo na zbirni konto, uz upozorenje da se razlaganje pokrene.
- Radnik **bez mesta troška** se prijavljuje samo kad ga neki radnici imaju — firma koja ih ne vodi ne treba da vidi upozorenje na svakom nalogu.

### 📗 Šifarnik konta za knjiženje (novi šifarnik)
- Protivstava troška ne zavisi od toga šta je isplaćeno nego od **uloge iznosa u nalogu**: neto obaveza, porez, doprinosi, obustava. Zato svaka uloga ima svoj red sa sistemskim ključem po kome je kod traži; redovi se ne dodaju i ne brišu, menja se samo broj konta.
- Podrazumevani brojevi su iz **Pravilnika o Kontnom okviru** za privredna društva, zadruge i preduzetnike: 520, 521, 522–526, 529, 450–453, 465, 469, 489. Firma koja vodi analitiku upisuje svoje — nova verzija za to nije potrebna.
- **Ispravljen podrazumevani konto naknada zarade.** Godišnji odmor, praznik i bolovanje su imali upisan 521, koji po Kontnom okviru nosi samo doprinose na teret poslodavca; naknada zarade ide na **520**, zajedno sa zaradom. Polje do sada nije koristio niko, pa migracija to ispravlja i u zatečenim bazama — ali samo tamo gde vrednost nije menjana ručno.

### 🔗 Uvoz u ERPiFinansije
- ERPiFinansije od verzije **1.2.0** ima dugme „📒 Uvoz zarada" na ekranu naloga: pročita fajl, pokaže šta je u njemu i uveze nalog kao **neproknjižen**.
- Uvoz se zaustavlja kad fajl nije iz ERPiZarade, kad nalog nije u ravnoteži i kad neki konto ne postoji u kontnom planu. Mesta troška se uparuju po šifri; nepoznata šifra je samo upozorenje.

## [1.13.0] - 2026-08-03

> **Dovršetak Faze 2.2** — radni sati se vode po isplati, ne po mesecu. Poslednje što je
> uvođenje isplata ostavilo iza sebe i jedino što je i dalje tražilo izmenu šeme.

### ⏱️ Radni sati po isplati
- `RadniSat` je bio jedinstven po (radnik, godina, mesec), pa je **unos sati za drugu isplatu meseca prepisivao onaj za prvu**. Iznosi već napravljenih obračuna time nisu bili ugroženi — svaki obračun nosi svoje sate u svojim kolonama — ali je ekran radnih sati pokazivao poslednji unos, ma za koju isplatu bio rađen. Sada svaka isplata ima svoje sate.
- Ekran „⏱️ Radni sati" dobija **izbor isplate**, po istom pravilu kao obračun i ugovori: dok mesec ima jednu isplatu lista je onemogućena i sve radi kao pre. Izbor prati i „➕ Dodaj radnika" — radnik obuhvaćen konačnom zaradom se sada može dodati u akontaciju istog meseca.
- **Uvoz sati iz Excel/CSV** zamenjuje sate izabrane isplate. Do sada je zamenjivao sve sate meseca, pa bi uvoz za akontaciju obrisao ono što je uneto za konačnu zaradu.
- „Novi obračun" čita sate **izabrane isplate** i upisuje ih na nju. Prenos sati iz ranijeg meseca uzima sate njegove **prve** isplate — prenosi se ono što je radnik u mesecu radio, a to stoji uz konačnu zaradu.
- Preračun sa ovog ekrana poštuje pravilo o obustavama: na akontaciji i bonusu se rate kredita i samodoprinos **ne skidaju**, jer se skidaju na konačnoj isplati istog meseca.
- Uklanjanje radnika iz radnih sati vraća ratu kredita kroz `KreditRateService`, jedini izvor te računice — do sada je imalo svoju kopiju, koja nije znala da akontacija ratu nije ni skinula.

### 🔗 Veza sa isplatom
- **`IsplataId == null` i dalje znači prvu isplatu perioda.** Migracija zatečene redove veže za prvu isplatu njihovog meseca, i pravi je za period koji ima sate a nema obračun — sati se unesu, a obračun pokrene tek posle. Nijedan sat se pri tome ne menja.
- Dugme „🔗" na ekranu isplata sada povezuje i radne sate, ne samo obračune.
- **Brisanje isplate briše i sate unete za nju**, uz poruku koliko ih je bilo. Oni su unos iz kog obračun tek nastaje, a ne dokaz šta je isplaćeno; obračun se ne briše nikad — isplata koja nosi obračune se ne može obrisati.

### 🧹 Pravilo o obuhvatu na jednom mestu
- Pravilo „šta pripada ovoj isplati" bilo je prepisano na tri mesta. Sada ga nosi interfejs `IPripadaIsplati` (obračun, radni sat, arhivirana verzija), a `IsplataService.Obuhvat` je jedan generički upit. Uz njega ide i test nad **pravim SQLite fajlom** — InMemory provajder prihvata i ono što SQLite odbija.
- Ekran radnih sati je isti preračun imao prepisan na **četiri mesta** (snimanje, izmena ćelije, masovna izmena, izmena parametara perioda), pa se razlikovao samo po napomeni. Sada je jedna metoda; bez toga se izbor isplate ne bi mogao ugraditi a da se negde ne izgubi.

## [1.12.0] - 2026-08-03

> Dopuna uputstva uz Fazu 2.3. Nema izmena u obračunu, prijavama ni nalozima — objavljuje se
> zato što je 1.11.0 već kod korisnika, a bez ovog objašnjenja je prvi korak sa ugovorima
> izgledao kao kvar.

### 📖 Uputstvo
- **Zašto padajuća lista primalaca ume da bude prazna.** Primalac je karton radnika sa oznakom „Van radnog odnosa", a ta oznaka je nova — nijedan zatečeni karton je nema. Uputstvo sada kaže da se najbrže dodaje dugmetom **„＋ novi"** pored same liste, koje otvara unos novog kartona ili označavanje postojećeg (penzioner, bivši zaposleni).
- Zabeleženo je i zašto isti čekboks u meniju „Radnici" ne reaguje dok se karton ne otvori dugmetom **„Izmeni"**: polja kartona su van režima izmene onemogućena. To je bio pravi razlog zašto je lista ostajala prazna i posle pokušaja.
- Dopunjen redosled provere pre testiranja: unos primaoca, pa potvrda da ga ekrani zarade više ne nude za obračun plate, radne sate i platni listić, a da su mu zatečene zarade ostale netaknute.

### 🧭 Plan nastavka
- Upisano pravilo koje je proizašlo iz greške u 1.11.0: **InMemory provajder nije SQLite.** `SUM` nad `decimal` kolonom prolazi kroz ceo paket testova, a kod korisnika pada sa „cannot apply aggregate operator 'Sum' on expressions of type 'decimal'". Zbrajanje decimalnih kolona ide **u memoriji**, posle `ToList()`, a upit koji zbraja stoji u servisu i ima test nad **pravim SQLite fajlom**.

## [1.11.0] - 2026-08-03

> **Faza 2.3** — obračuni van radnog odnosa: ugovor o delu, autorske naknade, privremeni i
> povremeni poslovi, naknade članovima upravnog i nadzornog odbora. Preduslov je bila Faza 2.2:
> te naknade se ne vezuju za obračunski mesec nego za **isplatu**, jer se isplaćuju kad se
> isplate, a ne krajem meseca.

### 📝 Ugovori van radnog odnosa (novi ekran)
- Ugovor se zaključuje sa licem koje je u kartonu radnika označeno kao **„Van radnog odnosa"**. Odatle se uzimaju JMBG, opština prebivališta i tekući račun — zaseban registar primalaca bi bio drugo mesto za iste podatke.
- **Primalac se unosi sa istog ekrana** („＋ novi" uz padajuću listu): otvori se nov karton sa JMBG-om, opštinom i tekućim računom, ili se oznaka doda postojećem kartonu (penzioner, bivši zaposleni). Označavanje postojećeg lica postavlja oznaku na **sve njegove periode** — to da neko nije u radnom odnosu nije svojstvo meseca, pa bi oznaka na jednom mesecu ostavila da ga ekrani zarade i dalje nude u ostalima.
- Naknada se obračunava po ugovoru i **upisuje kao obračun vezan za izabranu isplatu**. Zbog toga PPP-PD prijava, nalozi za prenos i godišnja potvrda rade nad njom bez ijedne izmene: sve što je razlikuje od zarade je šifra vrste prihoda i to što se ne meri satima.
- Računica se **vidi pre upisa** — bruto, normirani troškovi, osnovica, porez, doprinosi po stopama, neto, trošak isplatioca. Isti razlog kao proba pri prevođenju obračuna na stavke: reč je o novcu koji ide fizičkom licu i prijavljuje se Poreskoj upravi.
- Naknada ugovorena **„na ruke"** se preračunava na bruto tačno u dinar: preračun je inverzan obračunu, uz doterivanje po pari koje pokriva zaokrugljivanje pojedinačnih stavki.
- Isti ugovor može biti isplaćen **u ratama** — po jedna u svakoj isplati, svaka sa svojom prijavom i svojim BOP-om. Dva obračuna po istom ugovoru u **istoj** isplati se odbijaju: dala bi dva reda za isto lice u jednoj prijavi.

### 📄 Šifarnik vrsta ugovora (novi šifarnik)
- Normirani troškovi, stopa poreza i stope doprinosa — podeljene **na teret primaoca i na teret isplatioca** — stoje kao redovi u šifarniku. Izmena propisa se unosi; nova verzija programa se ne čeka. Isto pravilo po kome su uvedene vrste primanja i poreske olakšice.
- Podrazumevani sadržaj prati stanje propisa u 2026: ugovor o delu i naknade odborima (normirani troškovi 20%, porez 20%, PIO 24%, zdravstveno 10,30% za neosigurano lice), autorske naknade sa 50%, 43% i 34% normiranih troškova, i privremeni i povremeni poslovi koji se oporezuju **kao zarada** (porez 10%, doprinosi podeljeni na primaoca i isplatioca).
- **Šifra vrste prihoda se sastavlja, ne prepisuje.** Struktura je propisana: `V-PP-OVP-OL-B` — verzija kataloga, tip primaoca prihoda, oznaka vrste prihoda, olakšica i beneficirani staž. U šifarniku stoji samo OVP (tri cifre), a tip primaoca se bira uz ugovor, jer isti posao nosi drugu šifru kad ga radi zaposleno lice a drugu kad ga radi lice bez osiguranja. Bez toga bi svaka kombinacija posla i statusa tražila svoj red.
- Vrsta bez potvrđenog OVP-a **ne dobija izmišljenu šifru** — ostaje prazna, a kontrolne provere je prijavljuju kao grešku. Izmišljena šifra prolazi generisanje i pada tek kod Poreske uprave, kada je novac već isplaćen.
- Šifra plaćanja za nalog za prenos se takođe unosi ovde; propisuje je NBS.

### 🖋️ Generator ugovora sa editorom (novi ekran + šifarnik)
- Uz svaki zaključen ugovor se **generiše tekst dokumenta** iz šablona, popunjen podacima ugovora, primaoca i firme, i **uređuje se u editoru** — dopisivanje klauzula, brisanje članova, sve što je potrebno pre potpisa.
- Tekst se čuva **uz ugovor, ne uz šablon**: šablon se s vremenom menja, a potpisan ugovor mora ostati onakav kakav je potpisan. Ponovno generisanje briše ručne izmene, pa traži izričitu potvrdu.
- **Iznosi se iz teksta ne čitaju.** Obračun ide iz polja ugovora; tekst je dokument, a ne izvor podataka. Da je obrnuto, ispravka slovne greške bi menjala isplatu.
- Izvoz u **PDF**, spreman za štampu i potpis.
- **Šabloni su šifarnik** („🖋️ Šabloni ugovora"): isporučena su četiri — ugovor o delu (čl. 199 Zakona o radu), ugovor o autorskom delu (Zakon o autorskom i srodnim pravima), ugovor o privremenim i povremenim poslovima (čl. 197, uz konstataciju o 120 radnih dana) i ugovor o naknadi članu organa upravljanja. Tekstovi su pisani prema **obaveznim elementima iz propisa**, a formulacije se menjaju iz programa — nacrt novog Zakona o autorskom i srodnim pravima je u javnoj raspravi od marta 2026, pa se odredbe o ustupanju prava mogu menjati bez nove verzije.
- Polja se pišu kao `{PrimalacIme}`, `{Iznos}`, `{DatumOd}`… Zamena je namerno **glupa** — nema uslova ni petlji, jer bi šablon time postao program koji niko ne testira, a piše ga knjigovođa.
- **Nepopunjeno polje ostaje vidljivo u tekstu** i prijavljuje se posle generisanja. Tiho brisanje bi dalo ugovor sa prazninom na mestu iznosa ili roka, a to se primeti tek kad je potpisan.
- **Iznos slovima** se ispisuje sam, sa ispravnim rodom i padežem („dvadesetjedan dinar", ali „dvadesetdva dinara"; „dvehiljade", ne „dvahiljada"). Razlika brojke i slova tumači se u korist slova, pa se to ne prepisuje rukom.
- Karton firme dobija **zastupnika i njegovu funkciju** — ugovor se zaključuje „koga zastupa…", a bez tog polja bi svaki generisani dokument imao istu prazninu.

### 🧾 Prijava, nalozi i ostali ekrani
- **Osnovica doprinosa se sada može upisati.** Do sada se izvodila iz zbira PIO doprinosa po stopi zarade (24%); kod naknade van radnog odnosa ona je bruto umanjen za normirane troškove, pa bi izvođenje dalo pogrešan broj u prijavi. Za zaradu se ništa ne menja — kolona ostaje prazna i izvođenje radi kao pre.
- Naknada u prijavi ide sa **nulom u broju kalendarskih dana, efektivnih sati i mesečnog fonda** — ne meri se satima.
- Nalog za prenos nosi **predmet ugovora** u svrsi plaćanja, da se na izvodu vidi šta je isplaćeno, i šifru plaćanja iz šifarnika.
- Ekrani zarade — obračun plate, radni sati, uvoz sati, platni listići — lica van radnog odnosa **ne nude**: nemaju koeficijent, sate ni fond, a listić prikazuje upravo to.
- **Prekalkulacija zarada ne dira obračunate naknade.** Nastale su zasebnom radnjom nad ugovorom, a ne iz sati i koeficijenata koji se ponovo računaju; bez tog uslova bi ih obračun zarade tiho obrisao.
- Pre-flight provere zarade se na naknadu ne primenjuju (najniža osnovica, sati veći od fonda, olakšice, e-mail za listić), a dobija svoje: vrsta ugovora bez OVP oznake je greška, primalac bez tekućeg računa ili sa neispravnim JMBG-om takođe.

### 🧪 Testovi
- 272 ukupno (50 novih). Zbir isplaćenog po ugovorima se proverava nad **pravim SQLite fajlom**, ne nad InMemory provajderom: SQLite ne ume `SUM` nad `decimal` kolonom, pa grupisanje na strani baze pada sa „cannot apply aggregate operator 'Sum'" — a InMemory to prihvata i greška prođe kroz sve ostale testove.
- Za generator ugovora: polja se zamenjuju tačnim podacima, a nepopunjeno i nepoznato polje **ostaju vidljivi** u tekstu i prijavljuju se; sačuvan tekst preživljava izmenu šablona; ručna izmena teksta ne dira iznos; podrazumevani šabloni koriste samo polja koja generator poznaje i pozivaju se na propis; iznos slovima je tačan za jedninu, 2–4, 11–14 i za pare.
- Za obračun: računica pogađa objavljeni primer iz prakse (bruto 50.000 → neto 32.400 uz porez 8.000 i PIO 9.600); preračun neta u bruto je inverzan obračunu do pare, za sve tri vrste; izmena stope u šifarniku menja rezultat bez izmene koda; šifra vrste prihoda se sastavlja po strukturi a bez OVP-a ostaje prazna; ugovor nadjačava radno mesto pri određivanju SVP-a; naknada ulazi u prijavu sa svojom osnovicom doprinosa i nulama u satima, a **zarada u istoj prijavi ostaje brojčano nepromenjena**; nalog nosi predmet ugovora i šifru plaćanja iz šifarnika; naknada ne podleže proverama zarade; nadogradnja zatečene baze donosi šifarnik bez diranja obračuna.

### 📮 Šta se uz isplatu i dalje radi van programa
- **PPP-PD je jedina prijava koja se za ovu isplatu podnosi.** Obrasci **M-UN/M-UN/K** (PIO, uz ugovorenu naknadu) i **M-4** ukinuti su od **01.01.2019.** — član 30. Zakona o izmenama i dopunama Zakona o PIO briše član 144, a Fond PIO od tada podatke o stažu i osnovicama preuzima elektronski od nadležnih organa, najkasnije do kraja februara za prethodnu godinu. Stari obrasci važe samo za period osiguranja zaključno sa 31.12.2018.
- **Prijava na obavezno socijalno osiguranje (obrazac M) ide preko portala CROSO**, ne kroz ovaj program — jedinstvenom prijavom se pokrivaju PIO, RFZO i nezaposlenost. Za privremene i povremene poslove podnosi se najkasnije **dan pre početka rada**.
- Provera staža i osnovica je na **e-Šalteru Fonda PIO** i portalu eUprava; od 2026. je pristup isključivo preko eID-a (kvalifikovani sertifikat ili ConsentID) — stari pristup preko JMBG-a i PIN-a više ne radi.

### ❗ Šta nedostaje
- **OVP oznake za autorsku naknadu sa 34% normiranih troškova nisu potvrđene** iz Kataloga vrste prihoda i ostavljene su prazne uz napomenu. Isto važi za tip primaoca uz privremene i povremene poslove. Popunjavaju se u šifarniku, bez nove verzije.
- **Radni sati su i dalje mesečni** — razdvajanje po isplati traži izmenu šeme i nije obuhvaćeno ovom fazom.

## [1.10.0] - 2026-08-03

> **Faza 2.2** — entitet `Isplata`. Iz plana nastavka: „strukturno najvažnije što je ostalo".
> Do sada je sve bilo vezano za par (godina, mesec), pa je mesec mogao imati **tačno jednu**
> isplatu — a akontacija pa konačna isplata, bonus i 13. plata su zasebne isplate istog meseca.

### 💸 Isplate u mesecu (novi ekran)
- Mesec sada može imati više isplata, svaku sa svojom **vrstom, opisom i datumom isplate**. Redni broj isplate je isti onaj koji `PppPdPrijava` nosi od Faze 1.1 — polje je tada uvedeno upravo za ovo, pa nove veze u šemi nije trebalo dodavati.
- **Dok mesec ima jednu isplatu, ništa se ne menja**: selektori isplate se ni ne prikazuju, a obračuni bez upisane isplate pripadaju prvoj. To pravilo stoji na **jednom mestu** (`IsplataService.Obuhvat`), da se ne bi razišlo po upitima.
- Ekran pokazuje po isplati: broj obračuna, neto, oznaku za konačnu isplatu, da li nosi obustave, BOP i status prijave. Uz to idu kontrolne provere koje se pokreću tek kad isplata ima više od jedne.

### 🧾 Svaka isplata je svoja PPP-PD prijava i svoj paket naloga
- PPP-PD prijava, nalozi za prenos i platni listići se prave **za jednu isplatu**, ne za ceo period.
- **BOP tuđe prijave se odbija.** BOP jedne isplate na nalogu druge šalje novac na pogrešnu deklaraciju — tamo višak, ovde manjak, i obe uplate neraspoređene. Paket se zaustavlja pre izvoza.
- Akontacija se prijavljuje sa oznakom **„A"** (nije konačna isplata prihoda), ostale isplate sa „K". Za mesec sa jednom isplatom važi zapamćena postavka, kao i do sada.
- Svrha na virmanu i ime izvezenog fajla nose oznaku isplate, da se dva paketa istog meseca ne bi pomešala u banci.

### 💳 Rata kredita se i dalje skida tačno jednom
- **Obustave (rate kredita i samodoprinos) nosi samo konačna zarada.** Akontacija, bonus i 13. plata se isplaćuju bez njih — inače bi radnik u istom mesecu platio istu ratu dva ili tri puta.
- Zato mesec sme imati samo **jednu** isplatu vrste „konačna zarada"; druga se odbija.
- Storniranje isplate koja nije nosila obustave **ne vraća** ratu: vraćanje neskinute rate bi radnikov dug smanjilo bez ijednog dinara koji je otišao poveriocu. Pravilo je u `KreditRateService`, jedinom izvoru te računice.

### 🚫 Prekalkulacija i storniranje diraju samo svoju isplatu
- Prekalkulacija je do sada brisala **sve** obračune perioda. Sada briše samo obračune izabrane isplate — akontacija koja je već isplaćena i prijavljena ostaje netaknuta.
- Isto važi za storniranje: radnik u mesecu sa više isplata ima više obračuna, i stornira se onaj koji je izabran.
- **Verzije obračuna se broje po isplati.** Prekalkulacija akontacije ne podiže redni broj verzije konačnoj isplati — to su zasebni tokovi. Arhiva nastala pre 1.10.0 pripada prvoj isplati, pa se potrošeni brojevi ne dodeljuju ponovo.
- **Kontrolna provera „dupli obračun" više ne javlja lažnu grešku.** Radnik u mesecu sa dve isplate ima dva obračuna, ali u dve različite prijave — po jedan red u svakoj. Provera sada grupiše po isplati; dva obračuna u **istoj** isplati su i dalje greška.
- Tabela obračuna dobija kolonu „Isplata", prazna dok je isplata jedna.

### 🔗 Zatečeni obračuni
- Migracija svakom zatečenom periodu pravi prvu isplatu („konačna zarada", datum poslednjeg dana meseca) i veže obračune za nju. **Nijedan iznos se ne dira.**
- Dugme 🔗 na ekranu isplata radi isto nad obračunima koji isplatu nemaju.

### 🐛 Nadogradnja je padala kad je migracija u razvoju regenerisana
- Migracija koja se u toku razvoja obriše pa ponovo napravi dobija **nov vremenski žig uz isti naziv**. Baza koja je stigla da primeni staru verziju nosi njen ID, pa je EF primenjivao po drugi put i program je pri pokretanju padao sa `SQLite Error 1: duplicate column name` — nad živim podacima, bez puta napred.
- Sada se istorija migracija usklađuje **pre** `Migrate()`: zapis sa starim žigom se prepisuje na aktuelni, uparivanjem po nazivu. Ono što nova verzija migracije donosi a stara nije imala stiže dopunom posle migracije, istim obrascem koji se od 1.2.0 koristi za zatečene baze.
- Provereno na svim zatečenim bazama ovog instalacije, uključujući onu sa 9.982 obračuna: podaci netaknuti, istorija svedena na jedan zapis.

### 🧪 Testovi
- 222 ukupno (26 novih): nadogradnja preko migracije sa starim žigom ne ponavlja istu izmenu; obuhvat po isplati u nalozima, storniranju i prekalkulaciji; prijava druge isplate na nalogu prve se odbija; storniranje akontacije ne vraća ratu kredita; obračun bez obustava se od onog sa obustavama razlikuje **tačno za ratu**; druga konačna zarada u mesecu se odbija; briše se samo poslednja i prazna isplata; verzije se broje po isplati a prva obuhvata i arhivu bez upisane isplate; isti radnik u dve isplate nije dupli obračun a u istoj jeste; migracija veže zatečene obračune ne menjajući iznose; i **bez ijedne dodatne isplate nalozi ostaju brojčano isti kao pre**.

### ❗ Šta nedostaje
- **Radni sati su i dalje mesečni.** Obračun druge isplate prepisuje red u `RadniSati` za taj mesec — iznosi već obračunatih obračuna ostaju netaknuti, jer svaki nosi svoje sate, ali ulazni podaci pokazuju poslednji unos. Sati po isplati traže izmenu šeme i ostavljeni su za Fazu 2.3, gde su i inače potrebni.
- Brisanje celog perioda i dalje briše sve isplate tog meseca odjednom.

## [1.9.0] - 2026-08-03

> **Faza 2.7** — storniranje, rekalkulacija i izmenjena prijava. Iz razvojne mape: „dešava se
> svakog meseca, a trenutno nema podržan put".

### 🚫 Storniranje obračuna (novo)
- Do sada je jedini put za grešku u **zaključanom** periodu bio otključavanje, čime se izmeni izlažu i svi ostali obračuni tog meseca. Sada se stornira **jedan obračun**, bez otključavanja perioda.
- Stornirani obračun se **ne briše i ne nulira** — iznosi ostaju vidljivi, jer je to i dalje ono što je jednom obračunato i po pravilu već prijavljeno. Menja se samo to da ga isplate i prijave više ne obuhvataju: **nalozi za prenos, platni listići, PPP-PD, PPP-PO, spiskovi i rekapitulacije** ga preskaču.
- **Razlog je obavezan.** Bez njega se posle mesecima ne zna zašto obračuna nema u prijavi, a upravo to je pitanje koje se pri kontroli postavlja. Razlog i korisnik idu u revizioni trag.
- Storniranje se može poništiti — takođe uz razlog i uz zapis.

### 💳 Rata kredita se vraća, i to tačno jednom
- Stornirani obračun nije isplaćen, pa se rata obustave vraća; da ostane skinuta, radnikov dug bi se smanjio bez ijednog dinara koji je otišao poveriocu.
- Ista računica je do sada stajala **prepisana na dva mesta** (prekalkulacija i brisanje perioda), pa bi storniranje bilo treće mesto na kom se mogla razići. Sada je u `KreditRateService`, jednom i sa testom. Prekalkulacija i brisanje preskaču obračune kojima je rata već vraćena storniranjem.

### 🕓 Prethodne verzije obračuna se čuvaju
- Prekalkulacija briše zatečeni rezultat i računa iznova. Do sada je time nepovratno nestajalo ono što je već isplaćeno i prijavljeno. Sada se **pre brisanja** pravi zapis: iznosi (bruto, porez, doprinosi, neto) kao kolone i **pun snimak obračuna** u JSON obliku — u tom trenutku se ne zna koje će polje kasnije biti sporno, a legacy kolone iz DBF-a ne prikazuje nijedan ekran, ali od njih zavisi ponovni obračun.
- Isto važi i za brisanje perioda, koje je do sada bilo bez traga o sadržaju.
- Obračun nosi **redni broj verzije**; zatečeni obračuni su verzija 1.

### 📜 Revizioni trag se konačno može pogledati
- Trag se upisuje od Faze 0, ali se **nigde nije prikazivao** — a zapis koji niko ne vidi ne odgovara ni na jedno pitanje koje se pri kontroli postavi. Novi prozor pokazuje ko je, kada i šta radio nad obračunima perioda, i koje su verzije zamenjene.
- Otvara se sa ekrana obračuna i iz pregleda svih obračuna.

### 🧾 Izmenjena PPP-PD prijava
- Prijava sada može da se izjasni da **menja ranije podnetu**: `VrstaIzmene` (1.5), `JIPD` prijave koja se menja (1.5a), `BrojResenja` (1.6) i `Osnov` (1.6a) — po objavljenom opisu XML strukture Poreske uprave.
- **JIPD nije isto što i BOP**: JIPD identifikuje prijavu, BOP uplatu po njoj. Oba se sada čuvaju uz prijavu; bez JIPD-a se prijava kasnije ne može izmeniti.
- Izmena bez JIPD-a, ili sa JIPD-om koji nije do 19 cifara, odbija se **pri generisanju** — dok je ispravka još jeftina, umesto da padne kod Poreske uprave.

### 🐛 Prazan `<DeklarisaniMFP>` je išao u svaku prijavu
- Specifikacija izričito zabranjuje prazne tagove: „opcije `<tag></tag>` ili `<tag/>` nisu dozvoljene", a kad se `DeklarisaniMFP` navede mora da nosi bar jedno MFP polje. Do sada se emitovao uvek, i za obračune bez olakšice. Sada se izostavlja.

### 🐛 Padajuća lista „Vrsta prijave" nosila je pogrešne oznake
- Verzija 1.8.0 je ispravila pogrešno tumačenje šifara **u komentaru**, ali je ekran i dalje nudio „2 - Po nalazu kontrole" i „3 - Po odluci suda". Po specifikaciji je **1** opšta · **2** po službenoj dužnosti · **3** samoprijavljivanje · **4** po nalazu kontrole · **5** po odluci suda. Izbor „po nalazu kontrole" slao je šifru za prijavu po službenoj dužnosti.
- Iz istog izvora ispravljen je i **tip isplatioca**: 2 je „pravno lice iz budžeta", a ne preduzetnik (preduzetnik je 4). Lista sada ima svih sedam vrednosti.

### 🐛 Sačuvani XML se razlikovao od kopiranog
- „Generiši XML" nije prosleđivao broj kalendarskih dana, pa je sačuvana datoteka mogla da nosi drugu vrednost od one koju „Kopiraj XML" stavi u privremenu memoriju.

### 🧪 Testovi
- 196 ukupno (27 novih): stornirani obračun izostaje iz naloga i godišnje potvrde a ostaje u bazi sa svim iznosima, storniranje zaključanog obračuna prolazi bez otključavanja, storniranje bez razloga se odbija, rata kredita se vraća tačno jednom i poništavanjem se vraća u prvobitno stanje, arhiva verzije sadrži i legacy kolone, redosled elemenata izmene odgovara XSD sekvenci, neispravan JIPD se odbija, i **bez ijednog storna svi zbirovi ostaju brojčano isti kao pre**.

### ❗ Šta nedostaje
- Storniranje ne generiše izmenjenu prijavu samo od sebe — prijava se i dalje priprema na ekranu PPP-PD, s tim što stornirani obračuni u nju više ne ulaze i ekran prikazuje koliko ih je izostavljeno.

## [1.8.0] - 2026-08-03

> **Faza 2.4** — poreske olakšice. Analiza ih navodi kao „u praksi najčešći razlog za ručnu
> intervenciju u obračunu".

### 🏷️ Šifarnik poreskih olakšica (novi ekran)
- **Nijedna konkretna olakšica nije ugrađena u kod.** Program vodi zarade za više firmi, pa mora da podrži i olakšice koje danas niko ne koristi, kao i one koje propis tek uvede — olakšica je zato **red u šifarniku**, isto kao vrsta primanja u Fazi 2.1.
- Uz svaku stoje mehanizam, procenti umanjenja poreza i doprinosa, rok važenja po propisu i **MFP deklaracija** za PPP-PD prijavu.
- Veza radnik → olakšica ide kroz **postojeću OL oznaku u SVP šifri** (pozicije 7–8 polja radnog mesta). Radnik ne dobija novo polje, pa nema ni prilike da se to dvoje raziđe.

### ⚖️ Dva mehanizma koja se ne smeju pomešati
- **Povraćaj** (čl. 21v): poslodavac plati pun iznos pa podnosi Obrazac PPD. Obračun i PPP-PD **ostaju nepromenjeni**; beleže se samo iznosi koji se traže natrag.
- **Oslobođenje**: umanjuje se ono što se plaća. Umanjenje ulazi u obračun i prijavljuje se kroz MFP.

Zamena bi značila da firma ili plati manje nego što sme, ili traži povraćaj koji joj ne sleduje.

### 🧾 MFP u PPP-PD prijavi
- `DeklarisaniMFP` se do sada emitovao **prazan**. Sada se popunjava po specifikaciji Poreske uprave: `MFP` se ponavlja po polju, sa oznakom `MFP.1`–`MFP.12` i vrednošću sa decimalnom tačkom.
- **Šta koje MFP polje znači zavisi od SVP šifre** i propisuje ga Katalog vrste prihoda, pa se mapiranje unosi u šifarnik, a ne ugrađuje u kod.

### 🐛 Lista olakšica u kartonu radnika bila je ugrađena u kod — i verovatno pogrešna
- Karton je nudio `01/02/03` kao „Novozaposleni 65/70/75% (čl. 21v)". Po objavljenom Pravilniku o Obrascu PPD, za čl. 21v važe oznake **OL08 / OL09 / OL10**. Ta oznaka ulazi u SVP šifru koja ide u PPP-PD, pa pogrešna oznaka znači pogrešnu prijavu.
- Lista sada dolazi **iz šifarnika**, pa se ispravlja bez izmene koda. Polazni sadržaj nosi napomenu da oznake i procente treba proveriti u važećem katalogu.

### ✅ Nove kontrolne provere
- Radnik nosi OL oznaku koje **nema u šifarniku** ili je isključena — umanjenje se neće primeniti.
- Olakšica tipa „oslobođenje" **bez ijedne MFP deklaracije** — umanjenje se neće prijaviti.

### 🧹 Ispravka pogrešnih šifara vrste prijave
- Komentar uz `PppPdPrijava.VrstaPrijave` tvrdio je „1=originalna, 3=izmenjena, 5=otkazana". Po specifikaciji PU je: **1** opšta · **2** po službenoj dužnosti · **3** samoprijavljivanje · **4** po nalazu kontrole · **5** po odluci suda. Bitno pred Fazu 2.7 (izmenjena prijava).

### 🧪 Testovi
- 169 ukupno (18 novih): oslobođenje umanjuje porez i doprinose za tačan procenat, povraćaj **ne dira nijedan iznos**, olakšica se ne primenjuje kad je istekla po šifarniku ili po radniku, procenat sa kartona ima prednost, a bez olakšice u šifarniku obračun ostaje brojčano identičan.

### ❗ Šta nedostaje
- **Obrazac PPD (zahtev za povraćaj) se ne generiše.** Podaci postoje u obračunu, ali se PPD podnosi elektronski, na isti način kao PPP-PD — dakle XML-om čiju šemu nemam. Kao i kod bankarskog formata, ne piše se napamet: potreban je primer fajla ili specifikacija.

## [1.7.0] - 2026-08-03

> **Faza 2.5** — neoporeziva primanja kao parametar. Kriterijum iz razvojne mape:
> „prekoračenje neoporezivog limita automatski prelazi u oporezivi deo".

### 🎁 Ostala primanja (novi ekran)
- Prevoz, jubilarne nagrade, solidarne pomoći i slično se unose po radniku i periodu — **kao red, ne kao nova kolona**. Ranije je svako takvo primanje moralo da dobije kolonu i u `RadniSati` i u `ObracunPlate`.
- Vrsta se bira iz šifarnika, gde stoje poreski tretman, neoporezivi limit i konto.
- Isti radnik ne može dva puta istu vrstu u istom periodu — inače bi se neoporezivi limit primenio na svaki red posebno.

### ⚖️ Podela na neoporezivi i oporezivi deo
- **Prekoračenje limita automatski ulazi u poresku osnovicu**, a ostatak se isplaćuje neoporezovan.
- Poštuje se i razlika između poreza i doprinosa: primanje koje se oporezuje ali **ne ulazi u osnovicu doprinosa** podiže porez, a doprinose ne.
- Neoporezivi deo se **isplaćuje radniku u punom iznosu** — nije bio ni u bruto iznosu ni u osnovicama, pa se dodaje na kraju, u neto.
- Stavka obračuna sada nosi i `OporeziviDeo`, pa se podela vidi po primanju, a ne samo u zbiru.

### 🐛 Limit nula je značio suprotno od onoga što polje kaže
- Prvo tumačenje je računalo oporezivi deo kao `Iznos − Limit`, pa je kod limita nula **ceo iznos ispadao oporeziv** — tačno obrnuto od značenja polja „neoporezivo". Sada limit nula znači da gornje granice nema, a takva vrsta u upotrebi se prijavljuje kroz kontrolne provere, da limit iz propisa ne bi ostao neunet.

### 🧪 Testovi
- 151 ukupno (8 novih): iznos ispod limita ne dira porez ni doprinose a diže neto za pun iznos; prekoračenje diže i porez i doprinose; primanje van osnovice doprinosa diže samo porez; i kontrolna provera za neunet limit.

## [1.6.0] - 2026-08-03

> Odgovor na otvoreno pitanje 6.3 iz analize: **legacy kolone se prevode sve**, umesto da se
> stari periodi zamrznu. Time nema dva puta kroz kod.

### 🔀 Prevođenje zatečenih obračuna na model stavki
- Dugme na ekranu vrsta primanja preslikava **postojeće obračune** u stavke, pa i stari periodi rade po novom modelu.
- **Proba se izvršava uvek pre upisa** — prikazuje koliko će biti prevedeno i koji obračuni neće, i tek onda traži potvrdu. Radi se nad podacima koji su već isplaćeni, pa se ne upisuje ništa što korisnik nije video.
- Obračun kod kog se zbir stavki **ne poklopi** sa bruto iznosom se ne prevodi, nego se prijavljuje poimenično sa razlikom. Delimično preveden obračun izgleda ispravno, a daje pogrešan listić.
- Ponovno pokretanje ne udvostručuje stavke.

### 🐛 Dve zamke u zatečenim podacima
- **Kolone `Neto*` sadrže bruto iznose.** `NetoZar` je bruto osnovne zarade, `NetoPrek` bruto prekovremenog i tako dalje — naziv je ostatak iz DBF-a i navodi na pogrešan zaključak. Prevod ih čita kao bruto, kako i jesu.
- **Bolovanje preko 30 dana i porodiljsko odsustvo nemaju sopstvenu kolonu sa iznosom** — imaju samo sate, a iznos je ulazio jedino u ukupan bruto. Rekonstruišu se istom formulom koju koristi obračun (sati × prosek); bez toga bi se zbir stavki razišao od bruta kod svakog radnika koji je bio na dužem bolovanju.

### 🧪 Testovi
- 143 ukupno (9 novih): ravnoteža posle prevoda, rekonstrukcija komponenti bez sopstvene kolone, odbijanje obračuna sa nepokrivenim delom bruta, tolerancija na zaokruživanje (do 0,50 RSD po obračunu) i idempotentnost.

## [1.5.0] - 2026-08-03

> **Faza 2.1** — šifarnik vrsta primanja i stavke obračuna. Analiza ovo naziva najvećim
> pojedinačnim zahvatom u planu i preduslovom za sve obračune van radnog odnosa.

### 💰 Šifarnik vrsta primanja (novi ekran)
- Do sada je svako novo primanje značilo **novu kolonu** u `ObracunPlate` i novu migraciju — tabela je zato narasla na preko šezdeset kolona. Sada se novo primanje dodaje kao **red u šifarniku**, bez ijedne izmene baze.
- Uz svaku vrstu stoji sve što je potrebno da se obračuna i proknjiži: **SVP šifra**, da li je oporeziva, ulazi li u osnovicu doprinosa, **neoporezivi limit** i **konto** za automatsko knjiženje (priprema za Fazu 3.1).
- Popunjeno je **18 sistemskih vrsta** koje odgovaraju jedna-na-jedan komponentama iz kojih se danas sastavlja bruto iznos, plus četiri neoporeziva primanja (prevoz, jubilarna nagrada, solidarna pomoć, poklon deci) kao primer da se nova vrsta dodaje bez izmene šeme.
- Sistemske vrste se ne mogu obrisati (engine ih traži po šifri), kao ni vrsta upotrebljena u postojećim obračunima — umesto toga se isključuju poljem „Aktivna".

### 🧾 Stavke obračuna (`ObracunStavka`)
- Obračun sada uz zbirne iznose nosi i **razloženi sastav bruta po vrstama primanja**, sa satima i iznosom po stavci.
- **Nijedan postojeći iznos nije promenjen.** Stavke su verno razlaganje istog zbira, a stare kolone ostaju netaknute — zato svi postojeći ekrani, štampe i izveštaji rade nepromenjeno.
- Ako šifarnik nije popunjen, obračun radi kao i pre; baza dobija stavke tek pri sledećem obračunu.

### 🧪 Testovi
- 134 ukupno (11 novih). Ključni test drži kriterijum iz razvojne mape doslovno: **zbir stavki mora biti jednak ukupnom bruto iznosu** obračuna. Dodatno se poredi obračun sa popunjenim i praznim šifarnikom — bruto, neto, porez i minuli rad moraju biti identični.

### ❗ Šta ostaje od Faze 2.1
- Engine i dalje **računa** po starim kolonama, a stavke izvodi iz rezultata. Sledeći korak je da stavke postanu izvor istine, čime se otvara pitanje iz analize (tačka 6.3): prevesti legacy kolone sve, ili zamrznuti stare periode i novi model primeniti od određenog datuma. Ovakvim redosledom ta odluka ostaje otvorena, a ništa se ne gubi.

## [1.4.0] - 2026-08-02

> **Faza 1 je zaokružena** — kalendar praznika sa automatskim fondom sati (1.4) i godišnji
> obrazac PPP-PO (1.3).

### 🧾 Godišnji obrazac PPP-PO (Faza 1.3)
- Novi ekran pravi **potvrdu o plaćenim porezima i doprinosima po odbitku**, koju je poslodavac dužan da uruči radniku do 31. januara za prethodnu godinu.
- Obrazac se sastavlja iz obračuna cele godine i grupiše **po vrsti prihoda (SVP)**, sa brojem meseci, bruto prihodom, poreskom osnovicom, porezom i doprinosima po redu, i zbirom.
- Štampa: potvrda za izabranog radnika, jedan zbirni PDF sa svima, ili poseban PDF po radniku u folder. Izdavanje se beleži u revizioni trag.
- **Kontrola slaganja sa PPP-PD prijavama**: ako se zbir poreza i doprinosa iz obračuna razlikuje od zbira iz podnetih prijava, to znači da je obračun izmenjen posle podnošenja — potvrda bi radniku govorila jedno, a Poreska uprava imala drugo. Neslaganje ne blokira štampu, ali traži izričitu potvrdu.

### 🧹 Jedna logika za SVP šifru umesto tri kopije
- Određivanje šifre vrste prihoda stajalo je u tri kopije — u izvozu PPP-PD, u prikazu na ekranu prijave i sada u godišnjem obrascu. Kopije su se već razišle, pa je isti obračun mogao dobiti jednu šifru u prijavi a drugu na ekranu. Sve tri sada koriste `SvpService`.
- SVP se i dalje izvodi iz teksta u `Radnik.Radno_Mesto` — to ostaje poznato ograničenje modela (tačka 4.1.2 analize), koje rešava šifarnik `VrstaPrimanja` iz Faze 2.1. Dok se ne uvede, bar postoji **jedno** mesto koje treba izmeniti.

### 📅 Kalendar praznika (novi ekran)
- Zakonski praznici se popunjavaju za izabranu godinu po Zakonu o državnim i drugim praznicima: Nova godina, Sretenje, Praznik rada, Dan primirja, Božić i uskršnji dani.
- **Datum pravoslavnog Uskrsa se računa** (julijanski račun po Meeus-u, uz razliku od 13 dana koja važi za 1900–2099). Van tog opsega metoda odbija da računa umesto da vrati pogrešan datum.
- Primenjeno je pravilo da se, ako **državni** praznik padne u nedelju, ne radi prvog narednog radnog dana — a da se **verski** praznik ne pomera. Pomeranje se računa tek nad potpunom listom praznika: usput bi „prvi naredni radni dan" ispao dan koji je i sam praznik (16. februar dok se obrađuje 15.).
- Kalendar je izmenjiv: firma dodaje sopstvene neradne dane (slava, kolektivni godišnji odmor), a ponovno popunjavanje zakonskih praznika ih ne dira.
- Desna tabela prikazuje **radne dane i fond sati po mesecima**, pa se dejstvo svake izmene odmah vidi.

### 🐛 Fond sati se nasleđivao od pogrešnog meseca
- Kada period nije imao sopstveni `FondCasova`, uzimao se fond **prethodnog meseca** — pa je februar sa 160 sati ulazio u mart koji ima 176. Cena radnog sata se računa iz fonda, tako da je to menjalo platu svakom radniku.
- Sada se u tom slučaju fond **računa iz kalendara** (radni dani × 8). Ručno unet fond za taj period i dalje ima prednost.

### 🧪 Testovi
- 123 ukupno (33 nova). Datum pravoslavnog Uskrsa se poredi sa poznatim datumima za šest godina (2022–2027), jer je to jedini deo kalendara koji se ne može proveriti pogledom. Provereno je i da praznik u vikendu ne umanjuje fond dvaput, da dan označen kao radni ne ulazi u umanjenje, i da PPP-PO prijavi neslaganje sa podnetim prijavama.

## [1.3.0] - 2026-08-02

> **Faza 1 razvojne mape** iz `ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md`: nalozi za prenos
> (1.1), platni listići e-mailom (1.2) i uvoz radnih sati (1.5).

### ✉️ Platni listići e-mailom (Faza 1.2)
- Novo dugme na ekranu platnih listića šalje svakom selektovanom zaposlenom njegov listić na adresu iz kartona radnika. Pre slanja se traži potvrda, uz prikaz koliko zaposlenih nema e-mail.
- **PDF se zaključava lozinkom**, a lozinka je JMBG radnika i **ne navodi se u poruci** — inače bi putovala istim kanalom kao dokument. Ako je zaštita uključena a radnik nema JMBG, listić se **ne šalje nezaštićen** nego se preskače.
- Greška kod jednog radnika ne prekida slanje ostalima; po završetku se poimenično navodi ko listić **nije** dobio.
- Novi tab **Podešavanja → E-mail**: SMTP server, port, TLS, nalog i adresa pošiljaoca, uz dugme „Proveri vezu" koje se poveže i prijavi bez slanja poruke. **Lozinka se čuva šifrovano** (Windows DPAPI, vezano za nalog na tom računaru) — `settings.json` je inače običan tekst u profilu korisnika.

### 🔒 Evidencija slanja (obaveza po ZZPL)
- Svako slanje se beleži: kome, kada, na koju adresu, sa kojim ishodom i da li je PDF bio zaštićen. Beleže se i **neuspesi i preskočeni**, jer je pitanje „ko listić nije dobio" jednako važno. Ime i adresa su denormalizovani da zapis ostane čitljiv i posle izmene kartona.
- Migracija `Faza1_EvidencijaSlanjaListica`.

### 🧾 Izgled listića izdvojen iz ekrana
- `ConfigurePage` iz code-behinda prešao je u `PlatniListicDocument`, pa štampa i e-mail koriste **isti** kod. Dva puta kroz različit kod značilo bi da radnik dobije drugačiji listić nego što knjigovođa vidi na ekranu.

### 🏦 Izvoz naloga u fajl za banku (Faza 1.1)
Formati se razlikuju po aplikaciji, pa su napisana dva zapisivača nad istim, neutralnim modelom naloga:

- **Hal E-Bank (TXT)** — format **fiksnih pozicija** koji prihvata većina poslovnih banaka: adresna stavka, sabirna sa zbirom i brojem naloga, pa po jedna individualna stavka po nalogu.
- **Trezorski ePP (JSON)** — za korisnike sa računom kod Uprave za trezor. Suprotno očekivanju, taj sistem prima **JSON, ne XML**, najviše 5000 naloga po fajlu.

Dva detalja iz specifikacije koja bi inače tiho oborila fajl:
- **Šifra plaćanja je u Halcom formatu dvocifreno polje**, iako je šifra trocifrena: `240` se upisuje razdvojeno — prva cifra je *oblik plaćanja* (pozicija 167), preostale dve su *šifra* (pozicija 168). Upis `240` u dvocifreno polje pomerio bi svako polje iza njega.
- **Iznos ide u parama, bez zareza, sa vodećim nulama**: `1.234,56` → `0000000123456`.

Ako zapisivač nađe grešku (predugačko ime, račun koji se ne svodi na 18 cifara, prazna adresa primaoca za trezor), **fajl se ne snima** — inače bi se otkrilo tek pri učitavanju u banci, gde poruka o grešci obično ne kaže koji je nalog sporan.

> ⚠️ **Kodni raspored Halcom fajla nije potvrđen.** Specifikacija ga ne navodi; postavljen je
> `windows-1250`, kako Hal E-Bank radi u regionu. Pogrešan izbor ne obara uvoz, ali izobliči
> „č", „ć" i „đ" u imenima — vidi se na prvom fajlu i menja se na jednom mestu u
> `HalcomPpzWriter`.

### 📦 Novi paketi
- `MailKit` (slanje e-maila — ugrađeni `SmtpClient` je zastareo), `PDFsharp` (zaštita PDF-a lozinkom, što QuestPDF ne podržava), `System.Text.Encoding.CodePages` (windows-1250) i `ClosedXML` (čitanje i pisanje .xlsx). Sve pod MIT licencom.

### 📥 Uvoz radnih sati iz Excel/CSV (Faza 1.5)
- Dva nova dugmeta na ekranu radnih sati: **preuzimanje šablona** (.xlsx sa zaglavljem i već upisanim radnicima perioda) i **uvoz** popunjenog fajla. Bez šablona korisnik pogađa nazive kolona, pa prvi uvoz po pravilu padne na zaglavlju.
- Nazivi kolona su isti kao natpisi na ekranu; prepoznaju se bez obzira na velika slova i dijakritiku (`Nocni rad` = `Noćni rad`). Nepoznate kolone se prijave i preskoče, ne blokiraju.
- **Fajl sa ijednom greškom se odbija u celini**, uz spisak grešaka sa brojem reda i nazivom kolone — delimično uvezeni sati izgledaju kao uspeh, a daju pogrešan obračun radnicima iz neuvezenog dela. Prijavljuju se: nepostojeći radnik, isti radnik dva puta, vrednost koja nije broj, negativna vrednost i decimalni broj u koloni sa satima.
- Uvoz **ne pokreće obračun** — sati se upisuju, a preračun ostaje na „Sačuvaj i preračunaj", da se unete vrednosti prvo provere.
- Uvoz je zabranjen nad zaključanim periodom i beleži se u revizioni trag.

> **Tumačenje brojeva se ne prepušta kulturi.** `decimal.Parse("5000,50")` u invariant kulturi
> daje **500050**, jer zarez tumači kao razdvajač hiljada. Razdvajači se zato razvrstavaju
> izričito: poslednji je decimalni, osim ako iza njega stoje tačno tri cifre — pa je `1.234`
> jednako 1234, a `5000.50` i `5000,50` jednako 5000,50.

### 🧪 Testovi
- 90 ukupno (59 novih). Halcom TXT se proverava **po tačnim pozicijama iz specifikacije**, a ne „da liči na format": dužina reda, iznos u parama, podela šifre plaćanja, sečenje predugačkog imena bez pomeranja polja iza njega. Uvoz sati se proverava i tako što se **generisani šablon pročita istim uvozom** koji ga je napravio.

## [1.2.0] - 2026-08-02

> **Faza 0 razvojne mape** iz `ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md` — dopuna modela
> podataka i kontrola bez kojih virmani, e-mail listići i PPP-PO iz Faze 1 nemaju odakle
> da povuku podatke. Sve izmene šeme su u jednoj migraciji (`Faza0_ModelPodatakaIKontrole`).

### ✅ Kontrolne provere pre zaključavanja (`PreFlightService`, `PreFlightPrompt`)
- Pred zaključavanje se sada izvršava **jedan zbirni izveštaj**: negativan neto, bruto ispod najniže osnovice doprinosa, nedostajući ili neispravan JMBG, radnik bez tekućeg računa, sati veći od mesečnog fonda, istekla poreska olakšica koja se i dalje primenjuje i **dva obračuna za isti JMBG** u istom periodu.
- **Greške zaustavljaju zaključavanje**; pregaziti ih može isključivo administrator, uz izričitu potvrdu. Nedostajući e-mail je upozorenje — ne tiče se ispravnosti obračuna.
- Prekovremeni sati se izuzimaju iz provere fonda, jer su po definiciji preko njega.
- Provera je uklopljena u **oba** puta zaključavanja (ekran obračuna i lista perioda).

### 🧾 Revizioni trag nad obračunima (`ObracunAudit`, `AuditService`)
- Kreiranje, prekalkulacija, zaključavanje, otključavanje i brisanje obračuna beleže **korisnika, vreme i period**. Isti obrazac kao `NalogAudit` u ERPiFinansije.
- Korisničko ime i ime radnika su namerno denormalizovani — zapis ostaje čitljiv i pošto se obračun obriše ili korisnik ukloni.

### 🆕 Dopune modela podataka
- **`Radnik.Email`** — preduslov za slanje platnih listića (Faza 1.2).
- **`Radnik.SifraMestaTroska`** — veza ka `MestoTroska` iz ERPiFinansije, po šifri (baze su zasebne), za raspored troška zarade pri knjiženju.
- **Dejstvo poreske olakšice** — `ProcenatPovracajaPoreza`, `ProcenatPovracajaDoprinosa`, `OlaksicaVaziDo`. Oznaka olakšice se **ne duplira**: ona je i dalje deo SVP šifre u `Radno_Mesto` i unosi se postojećom padajućom listom.
- **`PppPdPrijava`** — evidencija prijava sa **BOP-om** i statusom (pripremljena / podneta / prihvaćena / odbijena / stornirana). Bez BOP-a se ne mogu formirati nalozi za prenos poreza i doprinosa. `RedniBroj` unapred razdvaja više isplata u istom mesecu.
- **`Kredit`**: primalac obustave (naziv, račun, model i poziv na broj), tip obustave i **redosled naplate** — zakonsko izdržavanje ispred potrošačkih kredita.
- **`Firma.SifraOpstine`** — šifra opštine sedišta za PPP-PD zaglavlje.

### 🐛 PPP-PD prijava sa tuđim sedištem
- `XmlExportService` je imao **hardkodovane podrazumevane vrednosti** firme (`"079"`, `"010-123456"`, `info@firma.rs`). Izvoz sa ekrana obračuna se oslanjao na njih i tiho slao prijavu sa pogrešnom opštinom. Sada se svi podaci čitaju iz kartona firme, a **prazna šifra opštine odbija generisanje** umesto da propusti prijavu koju će Poreska uprava odbiti.
- Za agencije: šifra opštine iz kartona firme ima prednost nad vrednošću zapamćenom u podešavanjima aplikacije, koja je bila zajednička za sve firme.

### 🧹 Objedinjen flag zaključavanja
- Uklonjeno duplirano polje **`ObracunPlate.Zakljucen`** — upisivalo se, a nikad nije čitano. Jedini izvor istine je `Zakljucan`.

### 🧪 Testovi
- 15 novih testova za kontrolne provere. Test zatečene baze više ne koristi `EnsureCreated()` (koji uvek pravi šemu po **današnjem** modelu, pa bi ga svaka nova migracija rušila) — sada se šema podiže tačno do prve migracije, a istorija briše.

## [1.1.15] - 2026-08-02

### 🐛 Preuzimanje podataka i kada je nova verzija već pokrenuta (`AppConfig`)
- **Prazna podrazumevana baza više ne pobeđuje nad zatečenim podacima.** Ako je nova verzija već jednom pokrenuta, ona je napravila praznu `plata.db` i upisala je kao aktivnu — pa se posle preuzimanja podataka i dalje otvarala prazna. Sada se takva baza prepoznaje (nema nijedne firme) i aktivna se vraća na firmu koja je bila otvorena pre preimenovanja.
- **Zatečena istoimena baza sa podacima se ne gubi** — preuzima se pod sufiksom `_stara`, a ako je i ona prazna podrazumevana, preskače se da se ne bi pojavila kao lažna firma u spisku.

### 🎨 Ikonica aplikacije u boji modula
- `app.ico` je regenerisan iz originalnog 1024px izvora u **ljubičastoj boji** kartice modula, sa providnom pozadinom i svim veličinama do 256px (ranije samo do 64px, u istoj plavoj kao Finansije i Hub).

## [1.1.14] - 2026-08-02

### 🐛 Firme i baze nestale posle preimenovanja (`AppConfig`)
- **Podaci se preuzimaju iz starog foldera.** Preimenovanje u ERPi liniju promenilo je i ime foldera sa podacima (`%LOCALAPPDATA%\PlataApp` → `%LOCALAPPDATA%\ERPiZaradeApp`), pa je nova verzija startovala sa praznim spiskom firmi iako sve baze i dalje stoje na disku. Pri prvom pokretanju se sada **kopira ceo stari folder** — baze, rezervne kopije, podešavanja i logovi.
- **Aktivna baza se premapira** na kopiju u novom folderu, pa se aplikacija otvara na istoj firmi kao pre.
- Podaci se **kopiraju, ne premeštaju** — stara instalacija ostaje netaknuta dok se ne uverite da je sve preneto, a stari folder možete obrisati ručno. Preuzimanje se izvršava jednom i beleži se fajlom `preuzeto_iz_starog_foldera.txt`.

## [1.1.13] - 2026-08-02

### 🏷️ Preimenovanje projekta u ERPi liniju
- **Rešenje i svi projekti preimenovani**: `PlataSistem.slnx` → `ERPiZarade.slnx`, a projekti `PlataApp`/`PlataData`/`PlataApp.Tests`/`PlataMigration` → `ERPiZaradeApp`/`ERPiZaradeData`/`ERPiZaradeApp.Tests`/`ERPiZaradeMigration` (folderi, `.csproj` fajlovi, `namespace`-ovi i reference).
- **Repozitorijum i radni folder**: kod je premešten u `C:\ERPi\ERPiZarade`, a `origin` pokazuje na `https://github.com/blagojevicboban/ERPiZarade.git`.
- **Velopack `packId` je sada `ERPiZarade`** (ranije `PlataSistem`), izvršni fajl je `ERPiZaradeApp.exe`. `ERPiHub` prepoznaje i staru i novu instalaciju, pa se na računarima sa ranijom verzijom modul i dalje vidi kao instaliran.
- **Korisničko uputstvo** (`Resources/Help/uputstvo.html`) prebrendirano u „ERPi Zarade", uz ispravljenu putanju baze (`%LOCALAPPDATA%\ERPiZaradeApp\Baze\plata.db`).
- Ažurirani `.github/workflows/release.yml`, `.bat` skripte, skills dokumentacija i README.

## [1.1.12] - 2026-08-02

### 📁 Baze preseljene na lokaciju koja preživljava ažuriranje (`AppConfig`, `UserSettings`)

> ⚠️ **Važno pri nadogradnji:** pri prvom pokretanju ove verzije baze se automatski
> premeštaju u `%LOCALAPPDATA%\ERPiZaradeApp\Baze\`, a podešavanja u
> `%LOCALAPPDATA%\ERPiZaradeApp\settings.json`. **Starije verzije programa nakon toga neće
> pronaći baze** jer traže na zatečenim lokacijama — posle preseljenja koristiti isključivo
> ovu ili noviju verziju.

- **Baze više ne stoje u folderu izvornog koda ni u folderu instalacije.** Zatečeno stanje
  je bilo da aktivna baza živi u `C:\ERPi\ERPiZarade\Baze\` (gde je briše svako čišćenje
  repozitorijuma), dok su starije kopije stajale u `C:\ERPiZaradeApp\Baze\` (gde ih briše
  deinstalacija). Nijedna od te dve lokacije nije preživljavala ažuriranje programa.
- **Novi folder je `%LOCALAPPDATA%\ERPiZaradeApp\Baze\`** — isti obrazac koji već koriste
  ERPi Finansije i ERPi Sredstva. Velopack pri ažuriranju menja `%LOCALAPPDATA%\ERPiZarade\`,
  pa se podaci u `ERPiZaradeApp` folderu ne dodiruju.
- **Automatsko preseljenje pri prvom pokretanju**: baze se premeštaju zajedno sa pratećim
  `-wal` i `-shm` fajlovima (bez njih bi se izgubile transakcije koje SQLite još nije upisao),
  uz njih i folder `RezervneKopije`. Pri sudaru imena aktivna baza ima prednost, a zatečena
  se čuva pod sufiksom `_stara_<izvor>` radi poređenja. Postupak je idempotentan i svaki
  premeštaj se beleži u log.
- **Podešavanja premeštena iz Roaming-a**: sa `%APPDATA%\ERPiZarade\settings.json` na
  `%LOCALAPPDATA%\ERPiZaradeApp\settings.json`, uz jednokratno preuzimanje postojećih vrednosti
  (aktivna firma, izbor baze, zapamćeni PPP-PD podaci). Stara lokacija je bila zbunjujuća
  jer Velopack pod istim imenom „ERPiZarade" drži sasvim drugi folder.
- **Prihvatanje baze iz ERPiHub-a (`App.xaml.cs`)**: program sada čita `--db-path`, kao što
  Finansije i Sredstva već rade. Ranije je Hub prosleđivao izabranu firmu, a Zarade su je
  ignorisale i otvarale bazu iz podešavanja.

### 🗄️ Prelazak na EF Core migracije (`PlataDbContext`)
- **Šema baze se više ne održava kroz `EnsureCreated()` + ~60 `ALTER TABLE` naredbi u `try/catch` blokovima.** Uveden je standardni EF Core sistem migracija, isti kao u ERPiFinansije i ERPiSredstva. Ranije se svaki slom šeme gutao i bio nerazlučiv od poruke „kolona već postoji".
- **Zatečene baze korisnika se bezbedno usvajaju.** Baze napravljene ranijim verzijama nemaju `__EFMigrationsHistory` tabelu, pa bi ih `Migrate()` srušio pokušajem da kreira postojeće tabele. Nova logika ih prvo dovodi na aktuelnu šemu postojećim zakrpama, pa upisuje žig početne migracije **bez izvršavanja njenog sadržaja** — nijedan podatak se ne dira.
- **Stare zakrpe se više ne pokreću nad novim bazama.** Nova baza dobija ispravnu šemu direktno iz migracije, čime nestaje 60+ SQL naredbi pri svakom pokretanju.
- Ubuduće se izmene šeme rade isključivo preko `dotnet ef migrations add`.
- Poravnate verzije EF Core paketa (`ERPiZaradeData`, `ERPiZaradeMigration`, `ERPiZaradeApp` bili izmešani 8.0.0 / 8.0.16), čime je uklonjen MSB3277 sukob verzija sklopova.

### 🧪 Testovi
- Novi test projekat **`ERPiZaradeApp.Tests`** — prvi testovi u istoriji ovog modula.
- `ObracunServiceTests` (13 testova) — minuli rad isključivo na osnovnu zaradu (Zakon o radu čl. 108), srazmerno poresko oslobođenje, doprinosi i prednost stopa iz baze, izuzeće penzionera od doprinosa za nezaposlenost, najniža osnovica i izuzeta kategorija 9, ograničenje kreditne rate na ostatak duga, neto isplata nikad negativna.
- `PlataDbContextMigrationTests` (3 testa) — nova baza dobija migracije i istoriju; zatečena baza bez istorije zadržava podatke i dobija žig; ponovljeno otvaranje je idempotentno.
- Nadogradnja je dodatno proverena nad kopijom stvarne baze (6.945 radnika, 9.982 obračuna, 9.680 zapisa radnih sati) — svi redovi netaknuti.

### 🛠️ Interno
- **Struktura repozitorijuma spljoštena — jedan `version.txt`.** Izvorni kod je bio ugnježden u `ERPiZarade\ERPiZarade\`, pa su `version.txt`, `CHANGELOG.md` i `README.md` postojali na dva nivoa. Zbog toga su **verzija ugrađena u `.exe` i verzija Velopack paketa dolazile iz različitih fajlova**: `ERPiZaradeApp.csproj` je čitao ugnježdeni, a `release.yml` koreni `version.txt`. To je ranije već slomilo objavljivanje (commit `e35380d`). Sav izvor je premešten u koren repoa (`git mv`, istorija sačuvana), zastareli koreni `CHANGELOG.md` uklonjen, a srpski README preimenovan u `README.sr.md` (engleski ostaje kao GitHub landing page).
- Ažurirane sve reference na staru putanju: `release.yml` (uklonjen `working-directory`), `PokreniAplikaciju.bat`, `PokreniMigraciju.bat`, oba README-a i `.vscode` konfiguracija radnog prostora.
- **CI kapija kvaliteta (`.github/workflows/release.yml`)**: workflow razdvojen na `test` i `build`; release izlazi tek kada build i testovi prođu. Dodat `pull_request` triger.
- **`Directory.Build.props`**: upozorenja su greške u Release konfiguraciji.
- Očišćena preostala upozorenja prevodioca u `ERPiZaradeMigration` (nullable parametar, nekorišćena promenljiva, moguća null dereferenca).

---

## [1.1.11] - 2026-08-01

### 🎨 Dodatno zatamnjena ljubičasta paleta
- `PrimaryColor`/`PrimaryLightColor` dodatno zatamnjeni i zagasitiji (`#2D1B42` / `#43305F`) — manje zasićen, "priguešeniji" ton u odnosu na prethodnu izmenu.
- Ikonica 💼 na login ekranu sada bela (`Foreground="White"`) — ranije se renderovala crno i gubila se na tamnoj pozadini.

---

## [1.1.10] - 2026-08-01

### 🎨 Tamnija ljubičasta paleta
- Zatamnjena osnovna ljubičasta paleta za jedan ton (`PrimaryColor` `#5B21B6`→`#4C1D95`, `PrimaryLightColor` `#7C3AED`→`#5B21B6`) u `Resources/Styles.xaml`, primenjeno na sidebar, login ekran, dashboard grafikon i dijaloge (`NoviObracunWindow`, `DodajRadnikaRadniSatiWindow`).

---

## [1.1.4] - 2026-07-30

### 💰 Rebrendiranje u ERPi ZARADE
- **Zvanični naziv aplikacije**: Promenjen naziv u **`ERPi Zarade`** u svim prozorima, zaglavljima, prijavnom ekranu (`LoginWindow`) i navigaciji.
- **Podrška za sve vrste ličnih primanja**: Spremljen okvir za obračun zarada, ugovora o delu, PP poslova i autorskih ugovora.
- **🎨 Zvanična Ikonica**: Dodata nova visoko-rezoluciona ikona `app.ico` (aktovka + ERPi ZARADE) na plavoj zaobljenoj podlozi (`#2563EB`).

---

## [1.1.3] - 2026-07-29

### 🚀 Nove funkcionalnosti
- **Dashboard stranica**: Dodata nova početna stranica (`DashboardPage`) sa pregledom ključnih informacija o platnom sistemu.

### 🎨 UI / UX i Odzivnost
- **Usklađene boje UI komponenti**: Vizuelne boje navigacije, dugmića i header elemenata usklađene sa zvaničnom paletom aplikacije (`PrimaryColor #1A237E`, `AccentColor #00BCD4`).
- **Osveženi prikazi svih stranica**: Usklađeni layout i stilovi za `MainWindow`, `RadniciPage`, `ObracuniPage`, `ListiciPage`, `StampePage`, `PppPdPage`, `RadniSatiPage`, `KreditiPage`, `DoprinosiPage`, `PoreziPage`, `PlatniRazrediPage`, `BankePage`, `FirmePage`, `KorisniciPage` i `PodesavanjaPage`.

---

## [1.1.1] - 2026-07-29


### 🚀 ERPiHub Integracija & CLI Ruting
- **Podrška za `--db-path` CLI parametar**: Omogućeno pokretanje `ERPiZaradeApp.exe` iz ERPiHub centralnog kontrolnog panela sa automatskim prosleđivanjem putanje do SQLite baze podataka (sa automatskim čuvanjem u `UserSettings`).
- **`version.txt` binding u `.csproj`**: Verzija aplikacije sada se automatski čita iz `version.txt` i upisuje u `AssemblyVersion`, `FileVersion` i `Version` atribute pri svakom buildu.

---

## [1.1.0] - 2026-07-17

### 🚀 Inicijalno .NET 8 Izdanje
- Evidencija zaposlenih sa unosom ličnih i zakonskih podataka (JMBG, PIB, bankovni račun, koeficijent).
- Obračun zarada, bolovanja, toplog obroka i prevoza.
- Generisanje PPP-PD XML-a za Poresku upravu.
- Generisanje platnih listića u PDF formatu (QuestPDF).
- Generisanje virmana za banku.
- Velopack Auto-Update integracija.
