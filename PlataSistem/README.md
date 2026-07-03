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

Proces izbacivanja novih verzija je potpuno automatizovan kroz **GitHub Actions**.

### Kako izbaciti novu verziju?
Umesto kucanja komandi i pravljenja tagova, kreirali smo fajl **`version.txt`** u osnovnom folderu.

Sve što treba da uradiš jeste da:
1. Otvoriš `version.txt` i upišeš novu verziju (npr. izbrišeš `1.0.0` i upišeš `1.0.1`).
2. Komituješ taj fajl i gurneš na GitHub (`git push`).

Čim GitHub prepozna da je fajl `version.txt` izmenjen, on automatski na svojim serverima prevodi kod, pakuje instalaciju i izbacuje novo izdanje klijentima pod tom verzijom!

> **Važna podešavanja repozitorijuma:** Da bi ovo radilo, na tvom GitHub repozitorijumu moraš otići u **Settings -> Actions -> General** i podesiti *Workflow permissions* na **Read and write permissions**. Pored toga, fajlovi sa podacima (`plata.db` i `Baze/`) moraju biti komitovani u Git da bi bili dodati u instalaciju.

*Lokalno generisanje:* Ukoliko želiš ručno da spakuješ aplikaciju (bez GitHuba), možeš pokrenuti lokalnu skriptu `publish.ps1` iz Powershell-a.
