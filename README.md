# 💼 ERPiZarade — Salary Calculation System

> A Windows desktop application for payroll processing, employee management, and generation of legally required reports — built with C# / .NET 8 / WPF.

🇷🇸 [Srpska dokumentacija](README.sr.md)

---

## ✨ Features

- 👥 **Employee management** — workers, categories, coefficients, and pay grades
- 🧮 **Payroll calculation** — automatic gross/net salary, tax, and contribution calculation per current rates
- ⏱️ **Work hours tracking** — regular, overtime, night shifts, sick leave, and annual leave; entered per payout, manually or imported from Excel/CSV
- 💸 **Multiple payouts per month** — advance, final salary, bonus and 13th salary as separate payouts, each with its own tax return and payment orders
- 📝 **Contracts outside employment** — service, copyright, temporary work and board-member fees, with contract text generation and PDF export
- 🏛️ **Payment orders** — Halcom TXT and treasury ePP JSON; taxes and contributions in a single payment with its reference number
- 📒 **Journal entry** — general-ledger voucher split by income type and cost centre, exported for import into ERPiFinansije; only exported once balanced
- 💳 **Loans** — tracking and repayment scheduling for employee loans
- 🏦 **Banks** — employee bank account management for salary transfers
- 🏢 **Companies** — multi-company support within a single database
- 📄 **Reports & printing** — pay slip (password-protected PDF, e-mail delivery), payroll list, recapitulation, XML for tax authority (PPP-PD), annual PPP-PO
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
ERPiZarade/
├── ERPiZarade/
│   ├── ERPiZaradeApp/           # Main WPF project (Views, ViewModels, Services)
│   │   ├── Views/          # Pages: Employees, Payroll, WorkHours, Loans...
│   │   ├── Services/       # PayrollService, BackupService, XmlExportService...
│   │   └── Resources/      # Styles, Help documentation
│   ├── ERPiZaradeData/          # Data Access Layer (EF Core entities, DbContext)
│   │   └── Models/         # Employee, PayrollRecord, Contribution, Company...
│   ├── ERPiZaradeMigration/     # Legacy DBF data migration tool
│   ├── PlataInspect/       # Database inspection utility
│   ├── CheckDb/            # Database integrity checker
│   └── FixHistory/         # Historical data correction tool
└── .github/workflows/      # GitHub Actions (automated release)
```

---

## 🚀 Getting Started (Development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Visual Studio 2022+ (with the *".NET desktop development"* workload) **or** JetBrains Rider

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/blagojevicboban/ERPiZarade.git
cd ERPiZarade

# 2. Build the solution
dotnet build ERPiZarade.slnx

# 3. Run the application
dotnet run --project ERPiZaradeApp/ERPiZaradeApp.csproj
```

> **Note:** The SQLite database is automatically created on first launch at `C:\ERPiZaradeApp\`. No manual setup required.

---

## 📦 Installation (End Users)

Download the latest installer from the **[Releases](../../releases)** page and run `ERPiZaradeSetup.exe`.  
The application installs to the user profile **without administrator rights** and updates silently in the background.

---

## 🔄 Releasing a New Version

The release process is fully automated via GitHub Actions:

1. Edit `ERPiZarade/version.txt` and set the new version (e.g. `1.2.0`)
2. Commit and push to the `main` branch:
   ```bash
   git add ERPiZarade/version.txt
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
