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
SQLite baza podataka (`plata.db`) nalazi se u osnovnom direktorijumu ili se kreira/ažurira prilikom prvog pokretanja na osnovu migracija.

## Kreiranje instalacije i objava

U osnovnom direktorijumu nalaze se skripte za publikovanje i kreiranje instalacije:
- `publish.ps1`: PowerShell skripta za prevođenje i publikovanje (publish) aplikacije za izdavanje (release).
- `PlataSetup.iss`: Inno Setup skripta koja od publikovanih fajlova kreira instalacioni `.exe` paket.
- `Instalacija.ps1`: Pomoćna PowerShell skripta za instalacione procese.
