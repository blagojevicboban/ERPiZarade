# 📋 Istorija izmena (Changelog) — ERPiZarade (ObracunZarada)

Sve značajne promene i novine u aplikaciji **ERPiZarade** dokumentovane su u ovom fajlu.

Format je zasnovan na [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardu i prati Semantic Versioning.

---

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
