# 🧭 Plan nastavka razvoja — ERPiZarade

> Radni dokument za nastavak posla u novoj sesiji. Prati razvojnu mapu iz
> [`ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md`](ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md) i beleži
> šta je urađeno, šta je namerno odloženo i **na čemu se stalo zbog podataka koji nedostaju**.
>
> Stanje na dan **04.08.2026**, verzija **1.16.0**, 352 testa (uz 69 u ERPiFinansije 1.2.0).

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
| — | Radni sati po isplati (dovršetak 2.2) | ✅ | 1.13.0 |
| **3.1** | Automatsko knjiženje u ERPiFinansije | ✅ | 1.14.0 |
| **2.6** | Bolovanja preko 30 dana, RFZO obrasci (OZ-7, OZ-10) | ✅ | 1.15.0 |
| — | Knjiženje refundacije (225 / 454, 455, 456) | ✅ | 1.15.0 |
| — | Odvajanje isplata van radnog odnosa (`Isplata.Rod`), podela menija | ✅ | 1.16.0 |
| **3.2** | Preuzimanje putnih naloga iz ERPiFinansije | ⬜ | |
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
| **XML „Potvrda o ostvarenoj zaradi" za eBolovanje** | Portal prima podatke o zaradi iz 12 meseci i **učitavanjem XML fajla**, ali šema nije javno objavljena — ni u korisničkom uputstvu ni na sajtu RFZO. Pet polja koja portal traži (mesec i godina, ukupan broj plaćenih časova, neto, bruto, datum isplate) su tačno kolone obrasca OZ-7, pa `Oz7Obrazac` već nosi sve što treba; nedostaje samo zapisivač. | Jedan primer XML-a ili šema — najlakše iz samog portala, ako tamo postoji šablon za preuzimanje. Bez toga se format ne piše napamet, isto pravilo kao kod Halcom fajla. |
| **Polja 3.7, 3.8 i 3.8a kod naknada** | Pravilnik uz 3.7 kaže „obavezno se popunjava za konačan obračun **zarade**", a primeri Poreske uprave za autorske naknade te kolone ostavljaju **prazne**. `XmlExportService` ih za `JeVanRadnogOdnosa` šalje kao **0**. Da li XSD traži izostavljanje elementa ili prihvata nulu — ne piše se napamet. | Isti preuzet XML kao za BOP i JIPD. |
| **OVP oznake za deo vrsta ugovora** | Potvrđeno je 601/602/603 (ugovor o delu i naknade odborima), 301/302/303 (autorske naknade 50% i 43%) i 150/151 (PP poslovi). Za autorsku naknadu sa **34%** normiranih troškova OVP nije potvrđen i ostavljen je **prazan** — obračun prolazi, ali kontrolna provera javlja grešku. Ostaje i da se potvrdi koji tip primaoca ide uz PP poslove. | Provera u važećem Katalogu vrste prihoda i unos u šifarnik „Vrste ugovora". Bez nove verzije. |

---

## 3. Sledeći koraci, po preporučenom redosledu

### 3.1. Faza 3.2 — preuzimanje putnih naloga iz ERPiFinansije *(preporučeno prvo)*

Dnevnice i putni troškovi već postoje u ERPiFinansije (`PutniNalog`); ERPiZarade treba da
**preuzme oporezivi deo**, ne da ga računa iznova. Smer je obrnut od 3.1, ali je pravilo isto:
iznos se prepisuje sa mesta gde nastaje.

### 3.2. Namerno odloženo

| Šta | Zašto je odloženo |
| :--- | :--- |
| **Brisanje perioda** | I dalje briše sve isplate meseca odjednom, sada i naknade po ugovoru. Brisanje pojedinačne isplate ide preko ekrana isplata, gde je i zaštićeno. |
| **Zaključavanje po isplati** | Zaključavanje ostaje na periodu. Isplata je **obuhvat, ne stanje** — drugo mesto koje kaže „ovo je zaključano" bilo bi isti duplikat kao nekadašnji `Zakljucan`/`Zakljucen`. |
| **Obrazac M-UN i M-4** | **Ne treba ih ni raditi.** Ukinuti su od 01.01.2019. (čl. 30 Zakona o izmenama i dopunama ZPIO briše čl. 144); Fond PIO podatke preuzima elektronski iz PPP-PD, najkasnije do kraja februara za prethodnu godinu. Stari obrasci važe samo za period zaključno sa 31.12.2018. Ako se u nekoj sesiji „primeti da nedostaju" — ne dodavati ih. |
| **Podnošenje zahteva RFZO iz programa** | Od **01.04.2026.** se zahtev za obračun i refundaciju podnosi **isključivo elektronski**, kroz „eBolovanje – Poslodavac" na Portalu eUprava, uz kvalifikovani sertifikat i potpis zahteva. Portal period i uzrok preuzima iz **doznake** (koju program nema), RFZO sam obračunava naknadu, a uz zahtev idu izjave i izvod iz PPP-PD prijave. Program to ne može da zameni; OZ-7 i OZ-10 služe za pripremu i proveru brojeva pre unosa i kao arhivski trag. Jedino što bi imalo smisla je XML za sekciju „Potvrda o ostvarenoj zaradi" — vidi tabelu blokiranog iznad. |
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
    tok kroz svaki od tih izvoza. Ono što se **odvaja jeste isplata**, ne obračun — vidi
    tačku 38.

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

20. **Radni sat je unos, obračun je dokaz.** Zato se sati brišu zajedno sa isplatom za koju
    su uneti (uz poruku koliko ih je bilo), a obračun ne — isplata koja nosi obračune se ne
    briše uopšte. Ne izjednačavati ta dva; sat se ponovo unese, obračun ne može da se vrati.

21. **Brojevi faza su iz razvojne mape, ne iz ovog dokumenta.** Numeracija odeljaka ovde
    (3.1, 3.2…) je redosled posla i **ne poklapa se** sa fazama iz
    `ANALIZA_I_PREDLOZI_FUNKCIONALNOSTI.md`. „Faza 3.1" je tamo automatsko knjiženje u
    ERPiFinansije — urađeno u 1.14.0, i to je faza u kojoj su `VrstaPrimanja.Konto`,
    `VrstaUgovora.Konto` i `Radnik.SifraMestaTroska` prvi put upotrebljeni. Radni sati po
    isplati su **dovršetak Faze 2.2** i tako su označeni u kodu i migraciji.

22. **Obuhvat po isplati piše se jednom, u `IsplataService.Obuhvat`.** Od 1.13.0 je generički,
    nad `IPripadaIsplati` — nosi ga i `ObracunPlate`, i `RadniSat`, i `ObracunVerzija`. Ne
    pisati `Where(x => x.IsplataId == null || x.IsplataId == id)` u upitu; to je bilo
    prepisano na tri mesta i upravo se tako razilazi. Uz svaki nov tip koji dobije
    `IsplataId` ide i test nad **pravim SQLite fajlom**, jer se pristup polju preko
    interfejsa mora prevesti u SQL, a InMemory to ne proverava.

23. **Prenos sati iz ranijeg meseca uzima sate njegove prve isplate.** Prenosi se ono što je
    radnik u mesecu radio, a to stoji uz konačnu zaradu; bez toga bi mesec sa akontacijom dao
    dva reda za istog radnika i prenos bi pao.

24. **Nalog za knjiženje se ne čuva.** Izvodi se iz obračuna svaki put iznova; u bazi zarada
    ne postoji entitet „nalog". Zbog toga izmena konta odmah važi, a pogrešan izvoz se
    ispravlja ponovnim izvozom. Snimljen nalog bi bio treći zapis istih iznosa — pored
    obračuna i pored naloga u glavnoj knjizi — i prvi bi se razišao sa ostalima.

25. **Trošak se uzima iz stavki, ne iz bruta.** `UkupnoBruto` ne sadrži neoporeziva primanja
    (prevoz, jubilarna nagrada): ona se isplaćuju radniku, a po zakonu nisu ni u poreskoj
    osnovici ni u osnovici doprinosa. Zbir stavki ih nosi i jedino se sa njim nalog
    uravnoteži. Obračun bez stavki nema ni neoporezivih primanja, pa je za njega bruto isto to.

26. **Protivstava ne zavisi od vrste primanja.** Konto troška živi uz vrstu primanja i vrstu
    ugovora — tamo mu je mesto, jer trošak zavisi od toga *šta* je isplaćeno. Neto obaveza,
    porez, doprinosi i obustave zavise od **uloge iznosa u nalogu**, kojih ima konačno mnogo;
    zato stoje u zasebnom šifarniku sa sistemskim ključem. Ne dodavati polja `KontoObaveze`
    uz vrstu primanja — svaka vrsta bi nosila isti broj.

27. **Neuravnotežen nalog se ne izvozi, a neslaganje se javlja po radniku.** Razlika u glavnoj
    knjizi se više ne može vezati za obračun iz kog je došla. Kontrola sastava (bruto − porez −
    doprinosi − obustave = neto) hvata to dok je ispravka još jeftina. Ne ublažavati je na
    upozorenje.

28. **Zamena polja u šablonu nema uslova ni petlji.** Traži se `{Polje}` i menja vrednošću —
    ništa više. Šablon sa granama bio bi program koji niko ne testira, a piše ga knjigovođa.
    Polje koje se ne prepozna ili nije popunjeno **ostaje vidljivo** i prijavljuje se; ne
    brisati ga tiho, jer se praznina na mestu iznosa primeti tek posle potpisa.

29. **`Bolovanje` ne nosi nijedan iznos.** Naknada je već obračunata i stoji u stavkama;
    zapis nosi samo ono što se iz obračuna ne vidi — dane, osnov i to da li je prva isplata
    iz Fonda. Ne dodavati polje sa iznosom refundacije: bio bi treći zapis istog novca,
    pored obračuna i pored naloga za knjiženje, i prvi bi se razišao sa ostalima. Iz istog
    razloga ni `BrojDana` nije kolona nego se izvodi iz datuma.

30. **Šta je na teret Fonda kaže šifarnik.** `VrstaPrimanja.NaTeretFonda` je označena samo za
    `B60`; povreda na radu i nega člana porodice zavise od slučaja i filijale. Ne uvoditi
    spisak šifri u `RfzoService` — bilo bi to isto pravilo iz tačke 1.

31. **Pol osiguranika se izvodi iz JMBG-a.** Cifre 10–12 ga nose (`JmbgValidator.Pol`).
    Ne dodavati `Radnik.Pol` — to bi bila druga vrednost koja može protivrečiti JMBG-u,
    isti duplikat kao oznaka olakšice iz tačke 2.

32. **Bolovanje se vezuje za period, ne za isplatu.** Refundira se ono što je radniku u mesecu
    isplaćeno, bez obzira kroz koliko je isplata prošlo. Zato `Bolovanje` nije `IPripadaIsplati`
    i zato spisak OZ-10 sabira sve obračune meseca, a ne obuhvat jedne isplate.

33. **Porez i doprinosi se na naknadu dele srazmerno, i neto je ostatak.** Obračun ih ne vodi po
    stavkama, pa se udeo računa iz zbira stavki. Neto se izvodi kao `bruto − doprinosi − porez`
    da kontrola sa obrasca (14 = 15 + 17 + 18) izlazi i posle zaokruživanja. Ne računati neto
    zasebno i ne zaokruživati ga posebno.

34. **Refundirana naknada nije trošak.** Kontni okvir je izvodi iz grupe 52 u celosti: umesto
    troška nastaje potraživanje na **225**, a obaveze idu na **454/455/456** umesto na
    450–453. Ne vraćati je na 520/450 „zbog jednostavnosti" — time bi trošak firme bio veći
    za iznos koji Fond vraća. Napomena: **455 spaja porez i doprinose zaposlenog**, dok su
    kod redovne zarade to dva konta (451 i 452); to nije previd nego Kontni okvir.

35. **Obustava se skida prvo sa zarade, pa tek onda sa naknade.** Za pun mesec bolovanja
    zarade nema, pa obustava pada na 454. Bez tog redosleda bi 450 ispao negativan. Iznos na
    225 obustava **ne dira** — Fond refundira obračunato, ne isplaćeno.

36. **Prag za teret Fonda nije svuda 31. dan.** Povreda na radu, profesionalna bolest i
    davanje tkiva idu od prvog dana; nega člana porodice zavisi od uzrasta člana, koji zapis
    ne nosi. `Bolovanje.PrviDanNaTeretFonda` to drži na jednom mestu i koristi se **samo za
    upozorenje** — nijedan iznos od njega ne zavisi. Ne vraćati opštu proveru „30 dana za
    sve": netačno upozorenje nauči korisnika da nalaze preskače.

37. **Mesec bez obračuna u OZ-7 ostaje prazan.** Uputstvo uz obrazac traži minimalnu zaradu za
    taj mesec, a program je nema. Prazan red se prijavljuje i popunjava rukom; upisan iznos
    „za svaki slučaj" ušao bi u prosek po kome se naknada isplaćuje.

38. **Rod isplate je jedini razdvajač zarade i naknade.** Član 11 Pravilnika obračunski period
    (polje 1.2) za zaradu određuje kao mesec *za koji* se isplaćuje, a za prihod van radnog
    odnosa kao mesec **isplate** — a prijava ima jedno takvo polje, jedan datum plaćanja i
    jednu oznaku K/A. Ne dodavati `VrstaIsplate.Naknada`: rod bi se tada čitao sa dva mesta
    koja mogu protivrečiti jedno drugom, isti duplikat kao oznaka olakšice iz tačke 2.

39. **Redni broj 1 pripada zaradi.** `IsplataService.Obezbedi` ga rezerviše i pravi **samo**
    isplatu roda `Zarada`; isplata naknada se nikad ne pravi sama, jer joj je datum plaćanja
    ono što deli prijavu od prijave. To je ono što drži `Isplata.JePrva` tačnim — obračuni bez
    `IsplataId` su uvek zarade.

40. **`PppPdPrijava` ne dobija rod.** Redni broj je jedinstven u mesecu kroz oba roda i već je
    veza ka isplati (tačka 10). Mesec izgleda kao „1. Konačna zarada", „2. Naknade po ugovoru".

41. **Lica su jedan registar.** Zaposleni sme biti isplaćen po ugovoru — šifra vrste prihoda za
    to je `1 01 601 00 0`, gde `01` znači „zaposleni". `PppPoService` grupiše po `BrojRadnika`
    kroz sve obračune, pa bi zaseban registar primalaca istom licu izdao **dve** godišnje
    potvrde. Ne praviti tabelu „Primaoci" — „👤 Primaoci po ugovoru" je pogled, ne registar.

42. **`Radnik.VanRadnogOdnosa` znači samo „nije u radnom odnosu".** Ko je primalac kaže ugovor
    (`Ugovor.BrojRadnika` + `TipPrimaoca`). Ne vraćati oznaku kao uslov za izbor primaoca i ne
    postavljati je licu u radnom odnosu: prvo onemogućava honorar zaposlenom, drugo ga tiho
    izbacuje iz obračuna plate, radnih sati i listića. Iz istog razloga kontrolna provera o
    neoznačenom kartonu **ćuti** za tipove primaoca 01 i 02.

43. **Karton koji `ObezbediKarton` prepisuje mora biti veran.** Otkako i zaposleni sme biti
    primalac, taj karton može biti prvi zapis lica u mesecu — i onaj koji obračun zarade posle
    zatekne. Ne skraćivati kopiju „jer naknadi treba samo JMBG i račun": bez koeficijenta i
    osnovne plate bi tom licu zarada tog meseca ispala pogrešna.

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
  trinaest migracija, od kojih jedna briše kolonu.
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
  ni „Platni listići", a da su mu zatečene zarade ostale netaknute. Za lice **koje jeste u
  radnom odnosu** oznaka se ne postavlja i u kartonu mu se ništa ne menja — ono sme biti i
  primalac po ugovoru, i tada mu se zarada obračunava kao i pre.
- Tek onda ugovor: „📝 Ugovori i naknade" → **prvo napraviti isplatu naknada** dugmetom ➕
  (traži se samo datum kada honorar ide na račun), pa izabrati vrstu, primaoca, tip primaoca i
  iznos. Računica se vidi **pre** upisa — proveriti brojeve rukom na jednom primeru (bruto
  50.000 po ugovoru o delu daje neto 32.400 uz porez 8.000 i PIO 9.600), pa tek onda 🧮.
- **Od 1.16.0 se naknada prijavljuje zasebno.** Posle obračuna otvoriti „📋 PPP-PD — naknade"
  i potvrditi tri stvari: da je u prijavi **samo** red naknade sa svojom SVP šifrom i nulama u
  satima, da je **obračunski period mesec isplate** (a ne mesec zarade), i da je oznaka
  konačne isplate zaključana na „K". Zatim otvoriti „📋 PPP-PD — zarade" i potvrditi da je
  **red zarade ostao brojčano isti** i da naknade tamo nema.
- U „🏦 Nalozi za prenos" iz grupe naknada proveriti da je naknada dobila svrhu po predmetu
  ugovora i šifru plaćanja iz šifarnika vrsta ugovora.
- **Zaposleni sa ugovorom o delu (novo u 1.16.0).** Izabrati zaposlenog kao primaoca i tip
  primaoca **„01 — zaposleno lice"**. Posle obračuna proveriti da je taj radnik i dalje u
  „Obračun plate" sa istom zaradom, da kontrolna provera **ne** javlja da lice nije označeno,
  i da mu u PPP-PO za tu godinu stoji **jedna** potvrda sa dva reda — `1 01 101 00 0` za
  zaradu i `1 01 601 00 0` za honorar.
- **Za generator ugovora prvo popuniti zastupnika** u kartonu firme (Firme → Zastupnik i
  Funkcija zastupnika). Bez toga generisani dokument prijavljuje `{FirmaZastupnik}` kao
  nepopunjeno polje i ostavlja ga vidljivim u tekstu — što je namerno.
- Zatim 📄 na izabranom ugovoru → „Generiši iz šablona" → pročitati ceo tekst i uporediti sa
  onim što firma inače potpisuje. Formulacije se menjaju u „🖋️ Šabloni ugovora"; izmena
  šablona **ne dira** tekstove već zaključenih ugovora.
- Proveriti **iznos slovima** na jednom primeru pre nego što dokument ode na potpis — razlika
  brojke i slova tumači se u korist slova.
- **Radni sati po isplati (1.13.0) se prvo proveravaju „na prazno".** Otvoriti „⏱️ Radni sati"
  za zatečeni mesec i potvrditi da je padajuća lista isplata **onemogućena** sa „1. Konačna
  zarada", da je broj redova isti kao u 1.12.0 i da je „💾 Sačuvaj i preračunaj" dao **iste
  iznose**. Sve dok je isplata jedna, ovaj ekran mora raditi kao pre.
- Tek onda druga isplata: dodati akontaciju (💸 Isplate u mesecu → ➕), vratiti se na radne
  sate, izabrati je u listi i uneti **drugačiji** broj redovnih sati jednom radniku. Zatim
  prebaciti listu nazad na konačnu zaradu i potvrditi da su njeni sati **ostali isti** — to je
  upravo ono što se do 1.12.0 gubilo.
- Proveriti i da akontacija **nije skinula ratu kredita** (Krediti → ostatak duga) i da se u
  „➕ Dodaj radnika" nudi i radnik koji sate već ima u konačnoj zaradi.
- Uvoz sati (📥) probati **u akontaciju**, pa potvrditi da su sati konačne zarade netaknuti.
- **Knjiženje (1.14.0) počinje od šifarnika.** Otvoriti „📗 Konta za knjiženje" i uporediti
  brojeve sa kontnim planom firme u ERPiFinansije. Podrazumevani su iz Kontnog okvira; ko vodi
  analitiku (520-1 po jedinici) upisuje svoje. **Konto koji ne postoji u kontnom planu zaustavlja
  uvoz na drugoj strani** — bolje ga ispraviti odmah nego posle prvog izvoza.
- Proveriti i konta uz **vrste primanja** (💰) i uz **vrste ugovora** (📄). Naknade zarade —
  godišnji odmor, praznik, bolovanje — treba da imaju **520**, isto kao zarada; do 1.14.0 im je
  stajao 521, što migracija ispravlja sama ako broj nije menjan ručno.
- Ako se trošak deli po organizacionim delovima, uneti **šifru mesta troška** u karton radnika,
  istu onu koja stoji u ERPiFinansije („Mesta troška" → Šifra). Bez nje nalog i dalje radi, samo
  bez podele.
- Zatim „📒 Nalog za knjiženje" za zatečeni mesec: proveriti da piše **„✔ u ravnoteži"**, da je
  broj obračuna isti kao u „Obračun plate", i — ovo je glavna provera — da je iznos na kontu
  **450 jednak zbiru naloga za prenos** neto zarada, a 451/452/453 iznosima iz PPP-PD prijave.
- Ako kontrola javi **„Sastav obračuna se ne slaže"**, taj radnik ima obračun koji treba
  prekalkulisati; nalog se dotle ne izvozi. To nije kvar knjiženja nego nalaz o obračunu.
- „📊 CSV" otvoriti u tabeli i uporediti sa mesečnom rekapitulacijom **pre** nego što nalog ode
  u knjige. Tek onda „📒 JSON".
- U ERPiFinansije (od 1.2.0) otvoriti „Nalozi" → **„📒 Uvoz zarada"**, izabrati snimljen fajl i
  pročitati šta piše u potvrdi. Nalog ulazi kao **neproknjižen** — pregledati stavke, pa ga
  proknjižiti dugmetom „✅ Proknjiži".
- Posle knjiženja proveriti **karticu konta 450**: saldo mora biti jednak onome što se isplaćuje
  radnicima, i zatvara se izvodom kad isplata prođe.
- **Bolovanja (1.15.0) počinju od šifarnika i kartona.** U „💰 Vrste primanja" potvrditi da je
  „Bolovanje preko 30 dana" označeno kao **na teret Fonda**, i označiti i ostale ako filijala
  refundira i njih. U karton firme uneti **poseban račun** i **šifru delatnosti**, a u karton
  radnika **LBO** — bez posebnog računa i LBO-a kontrolne provere javljaju grešku.
- Zatim „🏥 Bolovanja i RFZO" za mesec u kome je isplaćeno bolovanje preko 30 dana: uneti radnika,
  **početak sprečenosti** (ne prvi dan refundacije) i period, pa proveriti da se u tabeli pojavio
  iznos naknade — on dolazi iz obračuna i ovde se ne unosi. Ako je nula, u obračunu nema sati
  bolovanja preko 30 dana.
- Glavna provera: **bruto naknada mora biti jednaka zbiru stavki „Bolovanje preko 30 dana"** iz
  tog meseca, a kolona „Za isplatu" bruto uvećan za doprinose na teret poslodavca — to je ono što
  Fond refundira. Za pun mesec bolovanja iznosi moraju biti **isti** kao u obračunu, bez podele.
- „📋" snima OZ-10, „🖨️" OZ-7 za izabrani red. Na OZ-7 proveriti da je period **12 meseci pre
  meseca u kome je sprečenost nastupila** i da su meseci bez obračuna prazni — njih po uputstvu
  popunjavate minimalnom zaradom rukom, i kontrola ih nabraja.
- Prosek po času sa OZ-7 uporediti sa ručnim računom na jednom radniku: ukupan bruto podeljen
  ukupnim brojem časova iz te iste tabele.
- **Knjiženje refundacije se proverava na istom mesecu.** Otvoriti „📒 Nalog za knjiženje" za mesec
  sa bolovanjem i potvrditi tri stvari: da naknada **nije** na kontu 520, da je iznos na kontu
  **225 jednak koloni „za isplatu" na OZ-10**, i da nalog i dalje piše „✔ u ravnoteži".
- Ako neki radnik ima i bolovanje i ratu kredita, proveriti da konto **450 nije negativan** — obustava
  se skida prvo sa zarade, a kod punog meseca bolovanja pada na 454.
- Konta 225, 454, 455 i 456 uporediti sa kontnim planom firme u ERPiFinansije, kao i ostala —
  konto koji tamo ne postoji zaustavlja uvoz.
- Posle knjiženja, **potraživanje na 225 se zatvara u ERPiFinansije** izvodom posebnog računa kad
  refundacija stigne od Fonda. Taj korak program ne radi sam.
- **Sam zahtev se od 01.04.2026. podnosi kroz „eBolovanje – Poslodavac"** na Portalu eUprava, ne u
  papiru. Obrasci odavde služe da se brojevi provere pre unosa; u portalu se period i uzrok
  preuzimaju iz doznake, a podaci o zaradi iz 12 meseci traže se samo kod **prve** isplate za to
  bolovanje i unose ručno ili XML fajlom.
