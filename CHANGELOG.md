# 📋 Istorija izmena (Changelog) — PlataSistem

Sve značajne promene i novine u aplikaciji **PlataSistem** dokumentovane su u ovom fajlu.

Format je zasnovan na [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardu i prati Semantic Versioning.

---

## [1.1.9] - 2026-08-01

### 🚀 Nove funkcionalnosti i Usklađivanje sa Sistemom
- **Nova verzija modula ERPi Zarade (`1.1.9`)** — verzija usklađena sa sistemom automatskog ažuriranja (Velopack & GitHub Releases).
- **Integracija sa Glavnom Knjigom (`AccountingSystem v1.0.52`)** — usklađeno proknjižavanje rashoda zarada, poreza i doprinosa po mestima troška i projektima.

---

## [1.1.8] - 2026-08-01

### 🎨 UI / UX i Vizuelna Identitetska Usklađenost
- **Redizajn Vizuelne Teme i Boja Modula Zarada (`#5B21B6` / `#7C3AED`)**:
  - Prilagođeni ljubičasto-purpurni tonovi i gradijenti za modul Zarada kako bi se jasan vizuelni identitet razlikovao od plavog Finansijskog knjigovodstva.
  - Ažurirani hederi, ikonice i gradijenti u `LoginWindow`, `MainWindow`, `Styles.xaml`, `DashboardViewModel`, `NoviObracunWindow`, `DodajRadnikaRadniSatiWindow` i `StampePage`.
- **Ažurirana verzija u `version.txt`**: `1.1.8`.

---

## [1.1.7] - 2026-08-01

### 🚀 Nove funkcionalnosti i Sinhronizacija
- **Sinhronizacija ERP verzija** — usklađena verzija aplikacije za obračun zarada i naknada sa Glavnom knjigom (`AccountingSystem v1.0.44`) i `ErpHub`.
- **Obračun poreza i doprinosa** — priprema podataka za obrazac PPP-PD i proknjižavanje rashoda zarada u Glavnu knjigu.
