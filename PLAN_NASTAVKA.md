# 🧭 Plan nastavka razvoja — ERPiZarade

> Radni dokument za nastavak posla u novoj sesiji. Prati razvojnu mapu iz
> [`ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md`](ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md) i beleži
> šta je urađeno, šta je namerno odloženo i **na čemu se stalo zbog podataka koji nedostaju**.
>
> Stanje na dan **03.08.2026**, verzija **1.8.0**, 169 testova.

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
| **2.2** | Entitet `Isplata` (više isplata u mesecu) | ⬜ | |
| **2.3** | Ugovori o delu, autorski, PP poslovi, naknade odborima | ⬜ | |
| **2.6** | Bolovanja preko 30 dana, RFZO obrasci (OZ-7, OZ-10) | ⬜ | |
| **2.7** | Storniranje, rekalkulacija, izmenjena prijava | ⬜ | |
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

---

## 3. Sledeći koraci, po preporučenom redosledu

### 3.1. Faza 2.7 — storniranje i izmenjena prijava *(preporučeno prvo)*

Dešava se svakog meseca, a trenutno nema podržan put. Manje je od 2.2, a otklanja stvarnu
mesečnu muku.

- storniranje zaključanog obračuna uz trag ko je stornirao (`ObracunAudit` već ima
  `AkcijaObracuna.Storniran`, samo se ne koristi);
- rekalkulacija sa čuvanjem prethodne verzije;
- izmenjena PPP-PD prijava — **pažnja**: šifre vrste prijave su `1` opšta, `2` po službenoj
  dužnosti, `3` samoprijavljivanje, `4` po nalazu kontrole, `5` po odluci suda. Ranije je u
  kodu stajalo pogrešno tumačenje.

### 3.2. Faza 2.2 — entitet `Isplata`

Strukturno najvažnije što je ostalo: akontacija + konačna isplata u istom mesecu, bonus,
13. plata. `PppPdPrijava` već ima `RedniBroj` upravo zato — pripremljen je da razdvoji više
isplata bez izmene šeme.

Dodiruje PPP-PD, naloge za prenos i storniranje odjednom, pa je veće od 2.7.

### 3.3. Faza 2.3 — obračuni van radnog odnosa

Ugovori o delu, autorski, PP poslovi, naknade odborima. **Preduslov je 2.2** — ti obračuni se
ne vezuju za obračunski mesec nego za isplatu. Šifarnik vrsta primanja iz 2.1 već nosi SVP,
poreski tretman i konto, pa je osnova spremna.

### 3.4. Faza 3.1 — automatsko knjiženje u ERPiFinansije

`VrstaPrimanja.Konto` i `Radnik.SifraMestaTroska` postoje od ranije i još se nigde ne koriste —
uvedeni su baš za ovo.

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

---

## 5. Način rada koji se pokazao dobrim

- **Novac se ne menja bez testa.** Svaka izmena obračuna ide uz test koji fiksira pravilo, i uz
  test da se **bez nove funkcije rezultat ne menja**. Taj drugi je uhvatio više grešaka nego prvi.
- **Format se ne piše napamet.** Halcom, trezorski ePP i MFP su napisani tek posle nalažene
  specifikacije. Gde specifikacije nema, radi se tolerantno i **prijavljuje šta nije prepoznato**.
- **Release build mora biti bez upozorenja** — CI ih tretira kao greške
  (`Directory.Build.props`), pa upozorenje u Debug-u obori objavu.
- **PDF specifikacije koje WebFetch ne pročita** otvaraju se lokalno PyMuPDF-om; tako su dobijeni
  i Halcom format i struktura MFP-a.
- Redosled objave: izmene → `dotnet build -c Release` → `dotnet test` → CHANGELOG → `version.txt`
  → commit → push. **Push na `main` objavljuje verziju korisnicima** (Velopack), pa ga pokretati
  samo na izričitu reč.

---

## 6. Pre nego što korisnik testira

- **Rezervna kopija baze** (Podešavanja → Rezervna kopija). Od 1.2.0 naovamo primenjuje se
  devet migracija, od kojih jedna briše kolonu.
- Prevod zatečenih obračuna na stavke (Vrste primanja → 🔀) — **prvo proba**, koja ništa ne
  upisuje, pa tek onda potvrda.
