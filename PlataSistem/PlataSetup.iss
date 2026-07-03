; PlataSetup.iss
; Inno Setup skripta za kreiranje standardne Windows instalacije aplikacije PLATA
; Potrebno: Inno Setup 6.x - https://jrsoftware.org/isinfo.php
;
; UPOTREBA:
;   1. Pokreni publish.ps1 da kreiraš ReleasePackage/
;   2. Otvori PlataSetup.iss u Inno Setup Compiler-u i klikni "Compile"
;      ili pokreni: iscc.exe PlataSetup.iss

#define AppName "PLATA"
#define AppFullName "Sistem za Obračun Zarada - PLATA"
#define AppVersion "1.0"
#define AppPublisher "Zavod za Poljoprivredu"
#define AppURL ""
#define AppExeName "PlataApp.exe"
#define AppInstallDir "C:\PlataApp"
#define SourceDir "ReleasePackage"

[Setup]
; Jedinstveni GUID za aplikaciju (ne menjati nakon prvog puštanja!)
AppId={{A3F2C1B4-7D8E-4F9A-B2C3-D4E5F6A7B8C9}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppFullName} {#AppVersion}
AppPublisher={#AppPublisher}

; Podrazumevana putanja instalacije (korisnik može da promeni)
DefaultDirName={#AppInstallDir}
DefaultGroupName={#AppName}

; Ime izlaznog fajla (npr. PlataSetup_v1.0.exe)
OutputDir=PlataInstall
OutputBaseFilename=PlataSetup_v{#AppVersion}

; Kompresija
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Zahteva admin prava za instalaciju
PrivilegesRequired=admin

; Ikonice
SetupIconFile={#SourceDir}\plata.ico
UninstallDisplayIcon={app}\plata.ico

; Minimalni Windows zahtev (Windows 10)
MinVersion=10.0

; Wizard izgled
ShowLanguageDialog=no
WizardStyle=modern
WizardSizePercent=120
DisableDirPage=no
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.WelcomeLabel2=Ovaj čarobnjak će instalirati [name/ver] na vaš računar.%n%nPreporučuje se da zatvorite sve druge aplikacije pre nastavka.
english.FinishedLabel=Instalacija [name] je uspešno završena.%n%nAplikaciju možete pokrenuti putem prečice na radnoj površini ili Start Meniju.

[Tasks]
Name: "desktopicon"; Description: "Kreiraj prečicu na &radnoj površini"; GroupDescription: "Dodatne opcije:"; Flags: checkedonce

[Files]
; ── Glavna aplikacija ─────────────────────────────────────────────────────────
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Ikonica aplikacije
Source: "{#SourceDir}\plata.ico"; DestDir: "{app}"; Flags: ignoreversion

; ── Baza podataka ─────────────────────────────────────────────────────────────
; VAŽNO: Ne prepisivati ako već postoji (čuva podatke pri nadogradnji)!
Source: "{#SourceDir}\plata.db"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

; ── Korisničko uputstvo ───────────────────────────────────────────────────────
Source: "{#SourceDir}\Resources\Help\uputstvo.html"; DestDir: "{app}\Resources\Help"; Flags: ignoreversion

; ── Baze firmi ────────────────────────────────────────────────────────────────
; Kopirati samo šablonske baze koje ne postoje na odredištu (čuva produkcione baze)
Source: "{#SourceDir}\Baze\*.db"; DestDir: "{app}\Baze"; \
  Flags: onlyifdoesntexist uninsneveruninstall skipifsourcedoesntexist

[Dirs]
; Kreirati foldere uvek (i ako nema fajlova za kopiranje)
Name: "{app}\Baze"
Name: "{app}\Resources\Help"

[Icons]
; Prečica na Desktopu
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\plata.ico"; WorkingDir: "{app}"; \
  Comment: "{#AppFullName}"; Tasks: desktopicon

; Prečica u Start Meniju (direktno u Programs za Windows 11)
Name: "{commonprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\plata.ico"; WorkingDir: "{app}"; \
  Comment: "{#AppFullName}"

; Prečica za Korisničko uputstvo na Desktopu
Name: "{commondesktop}\PLATA - Uputstvo"; \
  Filename: "{app}\Resources\Help\uputstvo.html"; \
  Comment: "Otvori korisničko uputstvo za PLATA"; Tasks: desktopicon

[InstallDelete]
; Brisanje starog Start Menu foldera iz prethodnih verzija ako postoji
Type: filesandordirs; Name: "{commonprograms}\{#AppName}"

[Run]
; Pokrenuti aplikaciju po završetku instalacije (opciono, korisnik bira)
Filename: "{app}\{#AppExeName}"; Description: "Pokreni {#AppName} odmah"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Brisati samo log fajlove, NE baze podataka
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\install.log"

[Code]

var
  DeleteData: Boolean;

// ─── Deinstalacija ──────────────────────────────────────────────────────────
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if MsgBox(
      'Da li ste sigurni da želite da deinstalirate PLATA aplikaciju?',
      mbConfirmation, MB_YESNO) = IDNO
    then
      Abort;

    // Pitaj korisnika da li želi da ukloni i podatke
    DeleteData := MsgBox(
      'Da li želite da obrišete i sve baze podataka (plata.db i folder Baze) sa vašim podacima?' + #13#10 + #13#10 +
      'PAŽNJA: Ako izaberete DA, svi vaši podaci o obračunima i firmama biće trajno obrisani!',
      mbConfirmation, MB_YESNO) = IDYES;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    if DeleteData then
    begin
      DeleteFile(ExpandConstant('{app}\plata.db'));
      DelTree(ExpandConstant('{app}\Baze'), True, True, True);
      DelTree(ExpandConstant('{app}'), True, True, True);
    end;
  end;
end;

// ─── Logovanje uspešne instalacije ─────────────────────────────────────────
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    SaveStringToFile(
      ExpandConstant('{app}\install.log'),
      'PLATA instaliran: ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + #13#10 +
      'Putanja: ' + ExpandConstant('{app}') + #13#10 +
      'Verzija: {#AppVersion}' + #13#10,
      False
    );
  end;
end;
