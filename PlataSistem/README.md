🇬🇧 [English version below](#-payrollsystem--salary-calculation-system) &nbsp;|&nbsp; 🇷🇸 [Srpska verzija ispod](#-obracunzarada--sistem-za-obra%C4%8Dun-zarada-1)

---

# 💼 PayrollSystem — Salary Calculation System

> A Windows desktop application for payroll processing, employee management, and generation of legally required reports — built with C# / .NET 8 / WPF.

---

## ✨ Features

- 👥 **Employee management** — workers, categories, coefficients, and pay grades
- 🧮 **Payroll calculation** — automatic gross/net salary, tax, and contribution calculation per current rates
- ⏱️ **Work hours tracking** — regular, overtime, night shifts, sick leave, and annual leave
- 💳 **Loans** — tracking and repayment scheduling for employee loans
- 🏦 **Banks** — employee bank account management for salary transfers
- 🏢 **Companies** — multi-company support within a single database
- 📄 **Reports & printing** — pay slip (PDF), payroll list, recapitulation, XML for tax authority (PPP-PD)
- 🔄 **Auto-updates** — silent delta update system via Velopack

---

## 🛠️ Tech Stack

| Area | Technology |
|---|---|
| Language | C# 12 / .NET 8.0 |
| UI | WPF (Windows Presentation Foundation) |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Database | SQLite |
| ORM | Entity Framework Core 8 |
| Reports / PDF | QuestPDF |
| Packaging / Updates | Velopack |
| CI/CD | GitHub Actions |

---

## 📁 Project Structure

```
PayrollSystem/
├── PlataSistem/
│   ├── PlataApp/           # Main WPF project (Views, ViewModels, Services)
│   │   ├── Views/          # Pages: Employees, Payroll, WorkHours, Loans...
│   │   ├── Services/       # PayrollService, BackupService, XmlExportService...
│   │   └── Resources/      # Styles, Help documentation
│   ├── PlataData/          # Data Access Layer (EF Core entities, DbContext)
│   │   └── Models/         # Employee, PayrollRecord, Contribution, Company...
│   ├── PlataMigration/     # Legacy DBF data migration tool
│   ├── PlataInspect/       # Database inspection utility
│   ├── CheckDb/            # Database integrity checker
│   └── FixHistory/         # Historical data correction tool
├── .github/workflows/      # GitHub Actions (automated release)
├── PokreniAplikaciju.bat   # Quick launch script
└── PokreniMigraciju.bat    # Legacy migration launcher
```

---

## 🚀 Getting Started (Development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Visual Studio 2022+ (with the *".NET desktop development"* workload) **or** JetBrains Rider

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/blagojevicboban/PayrollSystem.git
cd PayrollSystem

# 2. Build the solution
dotnet build PlataSistem/PlataSistem.slnx

# 3. Run the application
dotnet run --project PlataSistem/PlataApp/PlataApp.csproj
```

> **Note:** The SQLite database is automatically created on first launch at `C:\PlataApp\`. No manual setup required.

---

## 📦 Installation (End Users)

Download the latest installer from the **[Releases](../../releases)** page and run `PlataSistemSetup.exe`. The application installs to the user profile **without administrator rights** and updates silently in the background.

---

## 🔄 Releasing a New Version

The release process is fully automated via GitHub Actions:

1. Edit `PlataSistem/version.txt` and set the new version (e.g. `1.2.0`)
2. Commit and push to the `main` branch:
   ```bash
   git add PlataSistem/version.txt
   git commit -m "bump: version 1.2.0"
   git push
   ```
3. GitHub Actions automatically: builds → packages with Velopack → creates a GitHub Release

> **Repository setup:** Go to **Settings → Actions → General** and set *Workflow permissions* to **Read and write permissions**.

---

## 🔒 Security Notes

- The employee database is **not part of this repository** (excluded via `.gitignore`)
- No hardcoded connection strings, passwords, or API keys exist in the codebase
- The GitHub Token is used exclusively via `secrets.GITHUB_TOKEN` (managed automatically by GitHub)

---

## 📜 License

This project currently has no explicit license. Contact the author for inquiries.

---

*Developed as an internal payroll tool. Compliant with Serbian labor law (Labor Law, Personal Income Tax Law).*

---
---

🇷🇸 [Srpska verzija ispod](#-obracunzarada--sistem-za-obra%C4%8Dun-zarada-1) &nbsp;|&nbsp; 🇬🇧 [English version above](#-payrollsystem--salary-calculation-system)

---

# 💼 ObracunZarada — Sistem za Obračun Zarada

> Desktop aplikacija za obračun zarada, upravljanje zaposlenima i generisanje zakonski propisanih izveštaja — razvijena u C# / .NET 8 / WPF.

---

## ✨ Funkcionalnosti

- 👥 **Upravljanje zaposlenima** — evidencija radnika, kategorija, koeficijenata i platnih razreda
- 🧮 **Obračun plata** — automatski izračun bruto/neto plate, poreza i doprinosa po važećim stopama
- ⏱️ **Radni sati** — evidencija redovnih, prekovremenih, noćnih, bolovanje i godišnji odmor
- 💳 **Krediti** — praćenje i otplata kredita zaposlenih
- 🏦 **Banke** — evidencija bankovnih računa za isplatu
- 🏢 **Firme** — podrška za više pravnih lica u istoj bazi
- 📄 **Štampa i izveštaji** — platni listić (PDF), platni spisak, rekapitulacija, XML za PPP-PD
- 🔄 **Automatska ažuriranja** — delta update sistem putem Velopack-a

---

## 🛠️ Tehnologije

| Oblast | Tehnologija |
|---|---|
| Jezik | C# 12 / .NET 8.0 |
| UI | WPF (Windows Presentation Foundation) |
| Arhitektura | MVVM (CommunityToolkit.Mvvm) |
| Baza podataka | SQLite |
| ORM | Entity Framework Core 8 |
| Izveštaji / PDF | QuestPDF |
| Pakovanje / Update | Velopack |
| CI/CD | GitHub Actions |

---

## 📁 Struktura projekta

```
PayrollSystem/
├── PlataSistem/
│   ├── PlataApp/           # Glavni WPF projekat (Views, ViewModels, Services)
│   │   ├── Views/          # Stranice: Radnici, Obračun, RadniSati, Krediti...
│   │   ├── Services/       # ObracunService, BackupService, XmlExportService...
│   │   └── Resources/      # Stilovi, Help dokumentacija
│   ├── PlataData/          # Data Access Layer (EF Core entiteti, DbContext)
│   │   └── Models/         # Radnik, ObracunPlate, Doprinos, Firma...
│   ├── PlataMigration/     # Alat za migraciju legacy podataka iz DBF fajlova
│   ├── PlataInspect/       # Pomoćni alat za inspekciju baze
│   ├── CheckDb/            # Provera integriteta baze podataka
│   └── FixHistory/         # Korekcija istorijskih podataka
├── .github/workflows/      # GitHub Actions (automatski release)
├── PokreniAplikaciju.bat   # Brzo pokretanje iz terminala
└── PokreniMigraciju.bat    # Pokretanje migracije legacy baze
```

---

## 🚀 Pokretanje projekta (za razvoj)

### Preduslovi

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Visual Studio 2022+ (sa *".NET desktop development"* workload-om) **ili** JetBrains Rider

### Koraci

```bash
# 1. Klonirati repozitorijum
git clone https://github.com/blagojevicboban/PayrollSystem.git
cd PayrollSystem

# 2. Prevesti projekat
dotnet build PlataSistem/PlataSistem.slnx

# 3. Pokrenuti aplikaciju
dotnet run --project PlataSistem/PlataApp/PlataApp.csproj
```

> **Napomena:** Baza podataka se automatski kreira pri prvom pokretanju u folderu `C:\PlataApp\`. Ne treba nikakvo manuelno podešavanje.

---

## 📦 Instalacija (za krajnje korisnike)

Preuzmi najnoviji instalacioni paket sa stranice **[Releases](../../releases)** i pokreni `PlataSistemSetup.exe`. Aplikacija se instalira u korisnički profil **bez administratorskih prava** i automatski se ažurira u pozadini.

---

## 🔄 Proces objavljivanja nove verzije

Verzionisanje je potpuno automatizovano putem GitHub Actions:

1. Otvori `PlataSistem/version.txt` i upiši novu verziju (npr. `1.2.0`)
2. Komituj i pushuj na `main` granu:
   ```bash
   git add PlataSistem/version.txt
   git commit -m "bump: version 1.2.0"
   git push
   ```
3. GitHub Actions automatski: prevodi kod → pakuje sa Velopack → kreira GitHub Release

> **Podešavanje repozitorijuma:** Na GitHub-u idi u **Settings → Actions → General** i postavi *Workflow permissions* na **Read and write permissions**.

---

## 🔒 Napomene o sigurnosti

- Baza podataka sa podacima o zaposlenima **nije deo repozitorijuma** (zaštićena `.gitignore`-om)
- Ne postoje hardkodovani connection stringovi, lozinke niti API ključevi u kodu
- GitHub Token se koristi isključivo putem `secrets.GITHUB_TOKEN` (automatski od strane GitHub-a)

---

## 📜 Licenca

Ovaj projekat je trenutno bez eksplicitne licence. Za pitanja kontaktirajte autora.

---

*Razvijeno kao interni alat za obračun zarada. Prilagođeno srpskom zakonodavstvu (Zakon o radu, Zakon o porezu na dohodak građana).*
