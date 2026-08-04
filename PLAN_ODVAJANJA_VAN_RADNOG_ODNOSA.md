# 🧾 Plan odvajanja isplata van radnog odnosa od zarade

> Radni dokument uz [`PLAN_NASTAVKA.md`](PLAN_NASTAVKA.md). Nastao iz nalaza da naknade po
> ugovorima van radnog odnosa danas dele PPP-PD prijavu sa zaradom, a **po propisu ne smeju**.
>
> Stanje na dan **04.08.2026**, polazna verzija **1.15.0**.

---

## 1. Šta kaže propis

Pravilnik o poreskoj prijavi za porez po odbitku, **član 11**:

**Tačka 1) — polje 1.1 Vrsta prijave:**

> „Opšta prijava se podnosi **pre svake isplate prihoda** za koje se plaća porez po odbitku…
> Ako se zarada isplaćuje u delovima, opšta prijava se podnosi onoliko puta u koliko delova se
> zarada isplaćuje."

**Tačka 2) — polje 1.2 Obračunski period:**

> „mesec i godina **za koji se vrši isplata zarade**, odnosno doprinosa za obavezno socijalno
> osiguranje, ako nije bilo isplate zarada, **kao i u slučaju isplate prihoda van radnog odnosa
> na koje se plaćaju i propisani doprinosi** za obavezno socijalno osiguranje. **Oznaka K se
> upisuje ako se vrši konačna isplata zarade** za obračunski period."

**Tačka 4) — polje 1.4 Datum plaćanja:**

> „datum kada je planirana isplata prihoda u slučaju podnošenja opšte prijave… Datum naveden
> pod rednim brojem 1.4 ne može biti neradni dan."

### Zaključak koji iz toga sledi

Prijava nosi **jedno** polje 1.2, **jedno** polje 1.4 i **jednu** oznaku K/A. Otuda:

1. **Obračunski period se razilazi.** Zarada za jul isplaćena 10.08. nosi period **07/2026** —
   mesec *za koji* se isplaćuje. Naknada po ugovoru nema „mesec za koji": ona nosi **mesec
   isplate, 08/2026**. Dva različita perioda ne staju u jedno polje, pa su to **dve prijave**.
2. **Oznaka K/A pripada zaradi.** „Oznaka K se upisuje ako se vrši konačna isplata *zarade*."
   Honorar koji sedne u prijavu akontacije dobija „A", što za njega ne znači ništa.
3. **Datum plaćanja je datum isplate tog prihoda.** Honorar se isplaćuje kad se isplaćuje, a
   ne uz platu; prijava se podnosi pre te isplate.

**Nijansa koja se ne sme prećutati:** jedna prijava *sme* nositi više vrsta prihoda — isti
pravilnik kod izmenjene prijave izričito kaže da se „mogu dodavati… nove vrste prihoda
postojećim primaocima prihoda". Ali to važi samo kad se **poklope i obračunski period i datum
plaćanja**. To je slučajnost, ne pravilo. Pravilo je nezavisnost prijava, i program mora da
podržava nju — poklapanje je tada samo poseban slučaj koji ništa ne traži.

---

## 2. Šta je danas u programu

Naknada je `ObracunPlate` sa popunjenim `UgovorId` (odluka #13 iz `PLAN_NASTAVKA.md`). **To je
i dalje tačno i ne menja se** — iznosi naknade imaju isti oblik kao iznosi zarade, pa nalozi,
knjiženje i godišnja potvrda rade nad njima bez izmene.

Problem je **jedan sloj iznad**: naknada se vezuje za `Isplata`, a `Isplata` je pojam zarade.

| Polje `Isplate` | Značenje za zaradu | Šta ispada za naknadu |
| :--- | :--- | :--- |
| `Godina` / `Mesec` | obračunski mesec zarade | naknada dobija **mesec zarade** umesto meseca isplate |
| `Vrsta` | `KonacnaZarada`, `Akontacija`, `Bonus`, `TrinaestaPlata` | nema vrednost koja znači „isplata naknada" |
| `OznakaZaKonacnuIsplatu` | „K" / „A" za celu prijavu | naknada nasleđuje oznaku od zarade |
| `NosiObustave` | rate kredita samo na konačnoj zaradi | za naknadu bez značenja |
| `RedniBroj` | veza ka `PppPdPrijava` | naknada deli prijavu sa zaradom |

### Gde se to vidi u kodu

| Mesto | Šta radi |
| :--- | :--- |
| `PppPdViewModel.LoadObracuneAsync` | `IsplataService.Obuhvat` povlači **i zarade i naknade** iste isplate u jedan XML |
| `UgovoriPage.UcitajIsplate` | nudi liste isplata zarade; naknada se mora „zakačiti" na neku od njih |
| `UgovorObracunService.Obracunaj` | `obracun.Godina/Mesec` uzima **iz isplate zarade** |
| `IsplataService.Dodaj` | odluka #11 (jedna konačna zarada mesečno) tera korisnika da za drugi honorar pravi lažnu „akontaciju" |
| `NalogZaPrenosService` | ispravno razdvaja šifru plaćanja i svrhu po `VrstaUgovora`, ali obuhvat i dalje dolazi iz isplate zarade |

### Šta se time praktično kvari

- Honorar isplaćen u avgustu, u mesecu u kom se isplaćuje julska zarada, ide u prijavu sa
  obračunskim periodom **07/2026** umesto **08/2026**.
- Honorar u mesecu **bez ijedne zarade** zahteva da se prvo napravi prazna isplata zarade.
- Dva honorara u istom mesecu traže drugu isplatu, a jedina raspoloživa vrsta je „akontacija"
  ili „ostalo" — obe netačne.
- Ako je zarada isplaćena u dva dela, honorar nasleđuje „A" od prvog dela.

---

## 3. Predlog reorganizacije

### Odabrana varijanta: `Isplata.Rod` — jedna kolona

`Isplata` dobija rod, po istom obrascu po kom je `ObracunPlate` dobio `UgovorId`:

```csharp
public enum RodIsplate
{
    /// <summary>Isplata zarade — obračunski period je mesec ZA KOJI se isplaćuje.</summary>
    Zarada = 0,

    /// <summary>Isplata naknada van radnog odnosa — obračunski period je mesec ISPLATE.</summary>
    VanRadnogOdnosa = 1
}
```

Pravila koja iz toga slede:

1. **`Rod = Zarada` je podrazumevano**, pa se nijedna zatečena isplata ne menja i program radi
   isto kao u 1.15.0 sve dok korisnik ne napravi prvu isplatu naknada. Isto pravilo kao kod
   `UgovorId == null` i `IsplataId == null`.
2. Za `Rod = VanRadnogOdnosa`, `Godina`/`Mesec` znače **mesec isplate** — to je obračunski
   period prijave, i tu se cela računica poklapa sama od sebe.
3. `OznakaZaKonacnuIsplatu` za taj rod je **uvek „K"** i na ekranu zaključana; „A" je oznaka
   konačne isplate *zarade* i za naknadu ne postoji.
4. `NosiObustave` je za taj rod **uvek netačno** — rate kredita i samodoprinos ostaju vezani
   isključivo za konačnu zaradu (odluka #11 ostaje netaknuta).
5. `RedniBroj` ostaje **jedinstven u mesecu preko oba roda**. Time se `PppPdPrijava` i njen
   indeks `(Godina, Mesec, RedniBroj)` **ne diraju**, a veza rednim brojem (odluka #10) ostaje
   ono što jeste. Mesec izgleda ovako: `1. Konačna zarada` · `2. Naknade po ugovoru (15.08.)`.
6. Provera „mesec sme imati samo jednu konačnu zaradu" se **ograničava na `Rod = Zarada`**.
   Isplata naknada ih sme imati koliko treba — svaka je svoj datum i svoja prijava.

### Osovina: svaka isplata nosi svoj obračun i svoju prijavu

Ovo je pravilo iz kog sve ostalo sledi, i vredi ga napisati odvojeno:

> **Jedna isplata → svoji obračuni → svoja PPP-PD prijava → svoj BOP → svoj paket naloga za
> prenos.** Bez izuzetka, i za zaradu i za naknadu.

Za zaradu to program već radi od Faze 2.2 — akontacija i konačna isplata su dve isplate, dve
prijave, dva BOP-a. **Naknade su do sada bile jedini prihod koji to nije imao**: nisu mogle da
budu isplata za sebe, pa su se lepile na tuđu prijavu.

Iz toga sledi i granularnost: **tri honorara isplaćena trima ljudima istog dana** su jedna
isplata i jedna prijava sa tri reda — jer im se poklapaju i period i datum plaćanja. **Tri
honorara isplaćena u tri dana** su tri isplate i tri prijave, jer polje 1.4 nosi jedan datum.
Program to ne pretpostavlja: korisnik pravi isplatu za svaki datum na koji zaista isplaćuje.

Zato isplata naknada **nikad ne nastaje sama** (za razliku od `IsplataService.Obezbedi` za
zaradu) — datum plaćanja se ne može pogoditi, a on je ono što prijavu deli od prijave.

### Zašto ne zaseban entitet `IsplataNaknade`

Zato što bi `ObracunPlate` morao da nosi **dva** strana ključa, `IPripadaIsplati` bi prestao da
bude jedan interfejs, a `IsplataService.Obuhvat` — jedino mesto koje zna šta znači „zapisi ove
isplate" (odluka #22) — bi se rascepio na dva. To je tačno ona vrsta duplikata protiv koje su
pisane odluke #2, #9 i #13. Rod je razlika u **značenju istog zapisa**, i tako se i zapisuje.

### Zašto ne `ObracunPlate` sam bez isplate

Naknada bez isplate ne bi imala ni datum plaćanja ni vezu ka prijavi, pa bi se ta dva podatka
morala dodati na obračun — a tamo već postoje, na isplati. Isplata je **obuhvat prijave**, i
naknadi treba obuhvat isto koliko i zaradi; treba joj samo **svoj**.

---

## 4. Šta se ne menja

Ovo su mesta koja bi se pri ovakvoj izmeni lako „popravila" u pogrešnom smeru:

1. **Naknada ostaje `ObracunPlate`** (odluka #13). Menja se čemu isplata pripada, ne šta je
   obračun.
2. **`PppPdPrijava` ne dobija `Rod` ni `IsplataId`** (odluka #10). Redni broj je jedinstven u
   mesecu preko oba roda, pa je veza već tačna.
3. **`IsplataService.Obuhvat` ostaje jedno mesto** (odluka #22). Rod se filtrira **pre** njega,
   pri izboru isplate na ekranu — ne unutar obuhvata, i ne prepisivanjem uslova po upitima.
4. **Obustave ostaju na konačnoj zaradi** (odluka #11).
5. **Prekalkulacija i dalje preskače `UgovorId != null`** (odluka #17).
6. **Šifra vrste prihoda se i dalje sastavlja** (odluka #14) i **prazan OVP i dalje pada na
   kontrolnoj proveri** (odluka #15).

---

## 5. Koraci, po redosledu

### 5.1. Model i migracija

- `RodIsplate` enum i `Isplata.Rod` sa podrazumevanom vrednošću `Zarada`.
- Migracija koja dopisuje kolonu sa `DEFAULT 0` — nova migracija, ne prepravka postojeće.
- Test nad **pravim SQLite fajlom** (`PlataDbContextMigrationTests`) da zatečene isplate posle
  migracije imaju `Rod = Zarada` i da se obuhvat nije promenio.

### 5.2. Servisni sloj

- `IsplataService.Dodaj` prima rod; provera jedne konačne zarade važi samo za `Zarada`.
- `IsplataService.Isplate(godina, mesec, rod?)` — filtriranje po rodu za ekrane.
- `IsplataService.Obezbedi` pravi **samo** prvu isplatu zarade, kao i do sada. Isplata naknada
  se nikad ne pravi sama — ona ima datum koji program ne može da pogodi.
- `UgovorObracunService.Obracunaj` odbija isplatu roda `Zarada` porukom koja objašnjava zašto.

### 5.3. Ekrani i meni

Grupa „ŠTAMPA" je podeljena; meni sada čita kao dva puta kroz posao:

```
ISPLATE ZARADA       💸 Isplate u mesecu · 📊 Obračun plate · 🧾 Platni listići
                     🏥 Bolovanja i RFZO · 📋 PPP-PD — zarade
                     🏦 Nalozi za prenos · 📒 Nalog za knjiženje

ISPLATE VAN          👤 Primaoci po ugovoru · 💸 Isplate naknada · 📝 Ugovori i naknade
RADNOG ODNOSA        📄 Vrste ugovora · 🖋️ Šabloni ugovora · 📋 PPP-PD — naknade
                     🏦 Nalozi za prenos · 📒 Nalog za knjiženje

IZVEŠTAJI            📑 Izveštaji & rekapit. · 🧾 PPP-PO (godišnja)
```

`IsplatePage`, `PppPdPage`, `NaloziPage` i `KnjizenjePage` primaju **rod kao parametar
konstruktora** i pojavljuju se u obe grupe — otvorene sa rodom već izabranim, pa se pogrešan
rod ne može ni izabrati. `Obezbedi` se poziva samo za rod `Zarada`.

**PPP-PO ostaje van oba roda**: godišnja potvrda obuhvata sve prihode jednog lica.

Na PPP-PD ekranu, kad je rod `VanRadnogOdnosa`: obračunski period je mesec te isplate, oznaka
K/A je zaključana na „K", a obračun pogrešnog roda zatečen na isplati se **izostavlja iz XML-a
i prijavljuje** — u prijavi bi nosio tuđi obračunski period. Bez ijedne isplate naknada ekran
staje i to kaže, umesto da tiho radi nad celim periodom.

### 5.4. Kontrolne provere

- Isplata roda `Zarada` koja sadrži obračun sa `UgovorId != null` → **greška** sa uputstvom da
  se naknada prebaci na isplatu naknada.
- Isplata roda `VanRadnogOdnosa` koja sadrži obračun sa `UgovorId == null` → **greška**.
- Isplata naknada bez ijednog obračuna → upozorenje (prijava bez reda se ne podnosi).

### 5.5. Evidencija lica — jedan registar, odvojen ekran

Registar lica se **ne deli**. Zaposleni sme biti isplaćen po ugovoru (šifra `1 01 601 00 0`,
gde `01` znači „zaposleni"), a `PppPoService` grupiše po `BrojRadnika` kroz sve obračune — pa
bi zaseban registar primalaca istom licu izdao **dve** godišnje potvrde umesto jedne.

Odvaja se pogled: **„👤 Primaoci po ugovoru"** nad istim `Radnici`, sa brojem ugovora, brojem
isplata i isplaćenim bruto iznosom, i oznakom „i u radnom odnosu".

`Radnik.VanRadnogOdnosa` od sada znači **samo** „nije u radnom odnosu"; ko je primalac kaže
ugovor. Otud tri ispravke:

1. `UgovoriPage.PopuniPrimaoce` nudi i zaposlene — dok je tu stajao filter po oznaci, šifra sa
   tipom primaoca 01 nije se mogla ni napraviti.
2. `PrimalacWindow` licu u radnom odnosu **ne postavlja** oznaku; ranije ju je upisivao u sve
   periode, čime bi zaposleni tiho nestao iz obračuna plate, radnih sati i listića.
3. `UgovorObracunService.Proveri` ćuti za tipove primaoca **01** i **02**.

Uz to je `ObezbediKarton` dopunjen da pravi **vernu** kopiju kartona: otkako i zaposleni sme
biti primalac, taj karton može biti prvi zapis lica u mesecu — a osakaćen bi mu dao nulti
koeficijent i pogrešnu zaradu.

### 5.6. Prevod zatečenih podataka — nije potreban

Korisnik je potvrdio da **nijedan obračun van radnog odnosa još ne postoji**, pa nema šta da se
prevodi. Kontrolna provera „Pomešani rodovi u istoj isplati" ostaje kao mreža za slučaj da se
takav zapis ipak nađe.

---

## 6. Dva zasebna nalaza, usput nađena

Nisu deo odvajanja, ali su nađeni pri čitanju Pravilnika i idu u isti krug posla.

### 6.1. `TipPrimaocaPrihoda` staje na 08, a propis ide do 13 — ✅ rešeno u 1.16.0

Pravilnik, član 11, uz polje 3.6, nabraja **13** oznaka vrste primaoca. `Ugovor.cs` je imao
prvih osam. Nedostajale su:

| Oznaka | Opis |
| :--- | :--- |
| 09 | lice penzioner po osnovu zaposlenosti |
| 10 | lice penzioner po osnovu samostalne delatnosti |
| **11** | **lice kome se isplaćuju prihodi van radnog odnosa na koje se ne obračunavaju i ne plaćaju doprinosi** |
| 12 | vojni penzioner |
| 13 | poljoprivredni penzioner |

**Oznaka 11 je bila ozbiljan nedostatak** — bez nje se OVP 315–321 (autorska naknada bez
doprinosa, samostalni umetnik po rešenju PU, maloletno lice) nisu mogli prijaviti uopšte. Za te
šifre je po uputstvu Poreske uprave u polja 3.12–3.16 upisano `0,00`, a `VrstaUgovora` već ume
da nosi sve stope na nuli — pa je oznaka bila jedino što je nedostajalo.

Svih pet je dodato, sa nazivima u padajućoj listi i testom da dvocifreni tip stane u pozicije
2–3 šifre bez pomeranja OVP-a (`1 11 315 00 0`).

### 6.2. Polja 3.7, 3.8 i 3.8a se za naknadu ne popunjavaju, a šalju se kao nule

Pravilnik uz polje **3.7 Broj dana**: *„Ovo polje se obavezno popunjava za konačan obračun
zarade, odnosno naknade zarade za obračunski period."* — dakle za zaradu. U primerima
popunjavanja Poreske uprave za autorske naknade (OVP 301–311) te kolone su **prazne**, a ne
nula.

`XmlExportService` za `JeVanRadnogOdnosa` postavlja `danaZaPrihod`, `efektivniSati` i
`fondSati` na **0** i emituje ih. Da li XSD očekuje izostavljanje elementa ili prihvata `0.00`
— **ne piše se napamet**, isto pravilo kao kod Halcom kodnog rasporeda. Ide u tabelu blokiranog
u `PLAN_NASTAVKA.md`, uz isti preuzeti primer XML-a koji već čeka zbog BOP-a i JIPD-a.

---

## 7. Otvorena pitanja za korisnika

| Pitanje | Zašto se ne pogađa |
| :--- | :--- |
| Obračunski period za prihode **bez doprinosa** (OVP 315–321, primalac 11) | Pravilnik polje 1.2 vezuje za prihode „**na koje se plaćaju** doprinosi". Za one bez njih ne kaže ništa, a XSD element verovatno traži. Rešava isti preuzeti XML. |
| Da li se u praksi honorar ikad podnosi zajedno sa zaradom | Ako korisnik to radi kad se datum i period poklope, isplata naknada tog dana može biti ista isplata — ali to mora biti **njegov izbor**, ne podrazumevano ponašanje. |
| Šifra plaćanja za naknade | `VrstaUgovora.SifraPlacanja` postoji i prazna pada na `240` (zarade). Propisuje je NBS; treba je uneti u šifarnik. |

---

## 8. Izvori

- [Pravilnik o poreskoj prijavi za porez po odbitku — Paragraf](https://www.paragraf.rs/propisi/pravilnik_o_poreskoj_prijavi_za_porez_po_odbitku.html)
- [Pravilnik, PDF (član 11 u celini)](https://www.paragraf.rs/propisi_download/pravilnik_o_poreskoj_prijavi_za_porez_po_odbitku.pdf)
- [Primeri popunjavanja Obrasca PPP-PD — Paragraf](https://www.paragraf.rs/dnevne-vesti/250214/pdf/primeri_popunjavanja_ppp-pd.pdf)
- [Obrazac PPP-PD — obrazac.rs](https://obrazac.rs/ppp-pd-obrazac/)
- [Ugovor o delu i autorski honorar: obračun i PPP-PD — Fedra](https://fedra.rs/blog/ugovor-o-delu-i-autorski-honorar-obracun-i-ppp-pd/)
