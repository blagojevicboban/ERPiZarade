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
ObracunZarada/
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
git clone https://github.com/blagojevicboban/ObracunZarada.git
cd ObracunZarada

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
