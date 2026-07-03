# PlataSistem (Obračun Zarada)

Ovo je WPF desktop aplikacija namenjena za obračun zarada. Aplikacija omogućava upravljanje podacima o zaposlenima, obračun plata, generisanje izveštaja (PDF i XML) i upravljanje podacima u bazi.

## Tehnologije
- **Jezik:** C# 12 / .NET 8.0
- **Korisnički interfejs:** WPF (Windows Presentation Foundation)
- **Arhitektura:** MVVM (CommunityToolkit.Mvvm)
- **Baza podataka:** SQLite (`plata.db`)
- **ORM:** Entity Framework Core 8
- **Izveštaji i štampa:** QuestPDF

## Struktura projekta

Projekat je podeljen na nekoliko celina unutar `PlataSistem.slnx` rešenja:

* **PlataApp:** Glavni projekat aplikacije. Sadrži korisnički interfejs (Views), logiku prikaza (ViewModels) i servise za eksport (XML, PDF).
* **PlataData:** Sloj za pristup podacima (Data Access Layer). Sadrži EF Core entitete i DbContext za komunikaciju sa SQLite bazom podataka.
* **PlataMigration:** Projekat zadužen za upravljanje EF Core migracijama i ažuriranje šeme baze podataka.
* **PlataInspect:** Alati za inspekciju baze (verovatno eksterni ili pomoćni alat).
* **PlataInstall:** Skripte i fajlovi potrebni za kreiranje instalacionog paketa.

## Pokretanje projekta

Za rad na projektu potrebno je instalirati:
- Visual Studio 2022 (sa .NET desktop development radnim okruženjem) ili Rider.
- .NET 8 SDK.

Aplikacija se pokreće tako što postavite **PlataApp** kao startup projekat (StartUp project) u okviru vašeg razvojnog okruženja i pritisnete F5.

### Pokretanje iz terminala (Command Prompt / PowerShell)
Ako preferirate rad iz terminala, možete prevesti i pokrenuti aplikaciju sledećim komandama (iz osnovnog direktorijuma gde je `.slnx` fajl):

1. **Prevođenje (Build):**
   ```powershell
   dotnet build PlataSistem.slnx
   ```
2. **Pokretanje (Run):**
   ```powershell
   dotnet run --project PlataApp\PlataApp.csproj
   ```

SQLite baza podataka (`plata.db`) nalazi se u osnovnom direktorijumu ili se kreira/ažurira prilikom prvog pokretanja na osnovu migracija.

## Kreiranje instalacije, objava i automatska ažuriranja

Aplikacija koristi **Velopack** za kreiranje instalacionih paketa i sistem automatskih "delta" ažuriranja u pozadini (instalira se u korisnički profil bez potrebe za administratorskim pravima).

U osnovnom direktorijumu nalaze se skripte za publikovanje:
- `publish.ps1`: PowerShell skripta koja radi prevođenje (`dotnet publish`), instalira Velopack CLI (`vpk`) po potrebi, i pakuje aplikaciju.
  - Pokretanjem skripte biće ti zatraženo da uneseš broj verzije (npr. `1.0.1`).
  - Skripta generiše `Setup.exe` fajl i fajlove za ažuriranje u folderu `ReleasePackage`.
- *Ažuriranje:* Da bi aplikacija detektovala ažuriranja, potrebno je fajlove iz `ReleasePackage` prekopirati na lokaciju koja je konfigurisana u `MainWindow.xaml.cs` (trenutno lokalni testni folder `C:\PlataUpdates`, a kasnije web server).
- `Instalacija.ps1`: Pomoćna PowerShell skripta za razne instalacione procese.
