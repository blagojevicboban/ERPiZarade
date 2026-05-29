; PlataSetup.iss
; Inno Setup skripta za kreiranje standardne Windows instalacije aplikacije PLATA
; Potrebno: Inno Setup 6.x - https://jrsoftware.org/isinfo.php
;
; UPOTREBA:
;   1. Instaliraj Inno Setup 6.x
;   2. Pokreni publish.ps1 da kreiraš ReleasePackage/
;   3. Otvori PlataSetup.iss u Inno Setup Compiler-u i klikni "Compile"
;      ili pokreni: iscc.exe PlataSetup.iss

#define AppName "PLATA"
#define AppFullName "Sistem za Obračun Zarada - PLATA"
#define AppVersion "1.0"
#define AppPublisher "Vaše Preduzeće"
#define AppURL "https://www.example.com"
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
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; Podrazumevana putanja instalacije (korisnik može da promeni)
DefaultDirName={#AppInstallDir}
DefaultGroupName={#AppName}

; Ime izlaznog fajla (npr. PlataSetup_v1.0.exe)
OutputDir=.
OutputBaseFilename=PlataSetup_v{#AppVersion}

; Kompresija
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Zahteva admin prava za instalaciju
PrivilegesRequired=admin

; Ikon instalacione skripte i uninstallera
SetupIconFile={#SourceDir}\plata.ico
UninstallDisplayIcon={app}\plata.ico

; Minimalni Windows zahtev (Windows 10)
MinVersion=10.0

; Jezik i kodna strana
ShowLanguageDialog=no

; Dozvoli korisniku da vidi folder
DisableDirPage=no
DisableProgramGroupPage=no

; Welkam i završna stranica
WizardStyle=modern
WizardSizePercent=120

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.WelcomeLabel2=Ovaj čarobnjak će instalirati [name/ver] na vaš računar.%n%nPreporučuje se da zatvorite sve druge aplikacije pre nastavka.
english.FinishedLabel=Instalacija [name] je uspešno završena.%n%nAplikacija se nalazi u folderu: %1%n%nMožete je pokrenuti putem prečice na radnoj površini.

[Tasks]
Name: "desktopicon"; Description: "Kreiraj prečicu na &radnoj površini"; GroupDescription: "Dodatne ikone:"; Flags: checkedonce
Name: "startmenuicon"; Description: "Kreiraj prečicu u &Start Meniju"; GroupDescription: "Dodatne ikone:"

[Files]
; Glavna aplikacija
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Ikonica aplikacije
Source: "{#SourceDir}\plata.ico"; DestDir: "{app}"; Flags: ignoreversion

; Glavna baza podataka - NE prepisivati ako već postoji!
Source: "{#SourceDir}\plata.db"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

; Instalaciona PowerShell skripta (opciono, za referencu)
Source: "{#SourceDir}\Instalacija.ps1"; DestDir: "{app}"; Flags: ignoreversion

; Baze firmi - kopirati samo one koje NE postoje na odredištu
Source: "{#SourceDir}\Baze\*.db"; DestDir: "{app}\Baze"; Flags: onlyifdoesntexist uninsneveruninstall; Check: BazeSourceExists

[Dirs]
; Kreirati Baze folder uvek
Name: "{app}\Baze"

[Icons]
; Prečica na desktopu (zajednicki za sve korisnike, admin install)
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "{#AppFullName}"; Tasks: desktopicon

; Prečica u Start Meniju
Name: "{commonprograms}\{#AppName}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "{#AppFullName}"; Tasks: startmenuicon
Name: "{commonprograms}\{#AppName}\Deinstaliraj {#AppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon

[Run]
; Pokrenuti aplikaciju po završetku instalacije (opciono)
Filename: "{app}\{#AppExeName}"; Description: "Pokreni {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Pri deinstalaciji - NE brisati baze podataka (sačuvati podatke firme!)

[UninstallDelete]
; Brisati samo privremene i log fajlove, NE baze podataka
Type: files; Name: "{app}\*.log"

[Code]
// Provera da li folder Baze postoji u source-u
function BazeSourceExists: Boolean;
var
  BazeDir: String;
begin
  BazeDir := ExpandConstant('{src}\{#SourceDir}\Baze');
  Result := DirExists(BazeDir);
end;

// Prikazati upozorenje ako korisnik pokušava da deinstalira
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if MsgBox(
      'Da li ste sigurni da želite da deinstalirate PLATA aplikaciju?' + #13#10 + #13#10 +
      'NAPOMENA: Vaše baze podataka firmi u folderu Baze/ i plata.db' + #13#10 +
      'NEĆE biti obrisane i ostaju sačuvane na disku.' + #13#10 + #13#10 +
      'Kliknite DA za nastavak deinstalacije, NE za odustajanje.',
      mbConfirmation, MB_YESNO) = IDNO
    then
      Abort;
  end;
end;

// Prikaz putanje instalacije na završnoj stranici
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Logovanje uspešne instalacije
    SaveStringToFile(
      ExpandConstant('{app}\install.log'),
      'PLATA instaliran: ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + #13#10 +
      'Putanja: ' + ExpandConstant('{app}') + #13#10,
      False
    );
  end;
end;
