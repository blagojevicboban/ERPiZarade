# 📋 Istorija izmena (Changelog) — PlataSistem (ObracunZarada)

Sve značajne promene i novine u aplikaciji **PlataSistem** dokumentovane su u ovom fajlu.

Format je zasnovan na [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardu i prati Semantic Versioning.

---

## [1.1.12] - 2026-08-02

### 📁 Baze preseljene na lokaciju koja preživljava ažuriranje (`AppConfig`, `UserSettings`)

> ⚠️ **Važno pri nadogradnji:** pri prvom pokretanju ove verzije baze se automatski
> premeštaju u `%LOCALAPPDATA%\PlataApp\Baze\`, a podešavanja u
> `%LOCALAPPDATA%\PlataApp\settings.json`. **Starije verzije programa nakon toga neće
> pronaći baze** jer traže na zatečenim lokacijama — posle preseljenja koristiti isključivo
> ovu ili noviju verziju.

- **Baze više ne stoje u folderu izvornog koda ni u folderu instalacije.** Zatečeno stanje
  je bilo da aktivna baza živi u `C:\ERP\PlataSistem\Baze\` (gde je briše svako čišćenje
  repozitorijuma), dok su starije kopije stajale u `C:\PlataApp\Baze\` (gde ih briše
  deinstalacija). Nijedna od te dve lokacije nije preživljavala ažuriranje programa.
- **Novi folder je `%LOCALAPPDATA%\PlataApp\Baze\`** — isti obrazac koji već koriste
  ERPi Finansije i ERPi Sredstva. Velopack pri ažuriranju menja `%LOCALAPPDATA%\PlataSistem\`,
  pa se podaci u `PlataApp` folderu ne dodiruju.
- **Automatsko preseljenje pri prvom pokretanju**: baze se premeštaju zajedno sa pratećim
  `-wal` i `-shm` fajlovima (bez njih bi se izgubile transakcije koje SQLite još nije upisao),
  uz njih i folder `RezervneKopije`. Pri sudaru imena aktivna baza ima prednost, a zatečena
  se čuva pod sufiksom `_stara_<izvor>` radi poređenja. Postupak je idempotentan i svaki
  premeštaj se beleži u log.
- **Podešavanja premeštena iz Roaming-a**: sa `%APPDATA%\PlataSistem\settings.json` na
  `%LOCALAPPDATA%\PlataApp\settings.json`, uz jednokratno preuzimanje postojećih vrednosti
  (aktivna firma, izbor baze, zapamćeni PPP-PD podaci). Stara lokacija je bila zbunjujuća
  jer Velopack pod istim imenom „PlataSistem" drži sasvim drugi folder.
- **Prihvatanje baze iz ErpHub-a (`App.xaml.cs`)**: program sada čita `--db-path`, kao što
  Finansije i Sredstva već rade. Ranije je Hub prosleđivao izabranu firmu, a Zarade su je
  ignorisale i otvarale bazu iz podešavanja.

### 🗄️ Prelazak na EF Core migracije (`PlataDbContext`)
- **Šema baze se više ne održava kroz `EnsureCreated()` + ~60 `ALTER TABLE` naredbi u `try/catch` blokovima.** Uveden je standardni EF Core sistem migracija, isti kao u AccountingSystem i SredstvaSystem. Ranije se svaki slom šeme gutao i bio nerazlučiv od poruke „kolona već postoji".
- **Zatečene baze korisnika se bezbedno usvajaju.** Baze napravljene ranijim verzijama nemaju `__EFMigrationsHistory` tabelu, pa bi ih `Migrate()` srušio pokušajem da kreira postojeće tabele. Nova logika ih prvo dovodi na aktuelnu šemu postojećim zakrpama, pa upisuje žig početne migracije **bez izvršavanja njenog sadržaja** — nijedan podatak se ne dira.
- **Stare zakrpe se više ne pokreću nad novim bazama.** Nova baza dobija ispravnu šemu direktno iz migracije, čime nestaje 60+ SQL naredbi pri svakom pokretanju.
- Ubuduće se izmene šeme rade isključivo preko `dotnet ef migrations add`.
- Poravnate verzije EF Core paketa (`PlataData`, `PlataMigration`, `PlataApp` bili izmešani 8.0.0 / 8.0.16), čime je uklonjen MSB3277 sukob verzija sklopova.

### 🧪 Testovi
- Novi test projekat **`PlataApp.Tests`** — prvi testovi u istoriji ovog modula.
- `ObracunServiceTests` (13 testova) — minuli rad isključivo na osnovnu zaradu (Zakon o radu čl. 108), srazmerno poresko oslobođenje, doprinosi i prednost stopa iz baze, izuzeće penzionera od doprinosa za nezaposlenost, najniža osnovica i izuzeta kategorija 9, ograničenje kreditne rate na ostatak duga, neto isplata nikad negativna.
- `PlataDbContextMigrationTests` (3 testa) — nova baza dobija migracije i istoriju; zatečena baza bez istorije zadržava podatke i dobija žig; ponovljeno otvaranje je idempotentno.
- Nadogradnja je dodatno proverena nad kopijom stvarne baze (6.945 radnika, 9.982 obračuna, 9.680 zapisa radnih sati) — svi redovi netaknuti.

### 🛠️ Interno
- **Struktura repozitorijuma spljoštena — jedan `version.txt`.** Izvorni kod je bio ugnježden u `PlataSistem\PlataSistem\`, pa su `version.txt`, `CHANGELOG.md` i `README.md` postojali na dva nivoa. Zbog toga su **verzija ugrađena u `.exe` i verzija Velopack paketa dolazile iz različitih fajlova**: `PlataApp.csproj` je čitao ugnježdeni, a `release.yml` koreni `version.txt`. To je ranije već slomilo objavljivanje (commit `e35380d`). Sav izvor je premešten u koren repoa (`git mv`, istorija sačuvana), zastareli koreni `CHANGELOG.md` uklonjen, a srpski README preimenovan u `README.sr.md` (engleski ostaje kao GitHub landing page).
- Ažurirane sve reference na staru putanju: `release.yml` (uklonjen `working-directory`), `PokreniAplikaciju.bat`, `PokreniMigraciju.bat`, oba README-a i `.vscode` konfiguracija radnog prostora.
- **CI kapija kvaliteta (`.github/workflows/release.yml`)**: workflow razdvojen na `test` i `build`; release izlazi tek kada build i testovi prođu. Dodat `pull_request` triger.
- **`Directory.Build.props`**: upozorenja su greške u Release konfiguraciji.
- Očišćena preostala upozorenja prevodioca u `PlataMigration` (nullable parametar, nekorišćena promenljiva, moguća null dereferenca).

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


### 🚀 ErpHub Integracija & CLI Ruting
- **Podrška za `--db-path` CLI parametar**: Omogućeno pokretanje `PlataApp.exe` iz ErpHub centralnog kontrolnog panela sa automatskim prosleđivanjem putanje do SQLite baze podataka (sa automatskim čuvanjem u `UserSettings`).
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
