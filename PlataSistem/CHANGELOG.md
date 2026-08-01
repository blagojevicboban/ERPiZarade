# 📋 Istorija izmena (Changelog) — PlataSistem (ObracunZarada)

Sve značajne promene i novine u aplikaciji **PlataSistem** dokumentovane su u ovom fajlu.

Format je zasnovan na [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardu i prati Semantic Versioning.

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
