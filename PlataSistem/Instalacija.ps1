# Instalacija.ps1
# Automatizovani instalacioni program za WPF aplikaciju PLATA
# Pokreće se iz raspakovanog foldera

$ErrorActionPreference = "Stop"
Clear-Host

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       DOBRODOŠLI U INSTALACIONI PROGRAM - PLATA          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Provera administratorskih prava
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] Napomena: Skripta se ne izvršava sa administratorskim privilegijama." -ForegroundColor Yellow
    Write-Host "    Preporučuje se pokretanje kao administrator kako bi prečice u Start meniju bile ispravno kreirane." -ForegroundColor Yellow
    Write-Host ""
}

# 2. Defisanje podrazumevanih putanja
$defaultDest = "C:\PlataApp"
$currentDir = Get-Location

# 3. Upit korisnika za odredišni folder
Write-Host "Unesite putanju gde želite da instalirate program." -ForegroundColor White
Write-Host "Pritisnite [Enter] da prihvatite podrazumevanu lokaciju [$defaultDest]:" -ForegroundColor Gray
$userPath = Read-Host

$destDir = $defaultDest
if ($userPath.Trim() -ne "") {
    $destDir = $userPath.Trim()
}

Write-Host ""
Write-Host "Instalacija će biti izvršena u: $destDir" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------" -ForegroundColor Gray

# 4. Kreiranje ciljnog foldera
if (-not (Test-Path $destDir)) {
    Write-Host "Kreiranje direktorijuma $destDir..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

# 5. Kopiranje izvršnog fajla
Write-Host "Kopiranje datoteka aplikacije..." -ForegroundColor Yellow
$exeSource = Join-Path $currentDir "PlataApp.exe"
$exeDest = Join-Path $destDir "PlataApp.exe"

if (Test-Path $exeSource) {
    Copy-Item -Path $exeSource -Destination $exeDest -Force
} else {
    Write-Error "Greška: PlataApp.exe nije pronađen u trenutnom folderu!"
}

# 6. Kopiranje baze podataka - VRLO VAŽNO: Ne prebrisivati ako već postoji!
$dbSource = Join-Path $currentDir "plata.db"
$dbDest = Join-Path $destDir "plata.db"

if (Test-Path $dbSource) {
    if (Test-Path $dbDest) {
        Write-Host "[i] Baza podataka plata.db već postoji u odredištu. Preskače se kopiranje da bi se sačuvali postojeći podaci!" -ForegroundColor Green
    } else {
        Write-Host "Kopiranje nove baze podataka plata.db..." -ForegroundColor Yellow
        Copy-Item -Path $dbSource -Destination $dbDest -Force
    }
}

# 7. Kreiranje prečica (Desktop i Start Menu)
Write-Host "Kreiranje prečica na sistemu..." -ForegroundColor Yellow
try {
    $WshShell = New-Object -ComObject WScript.Shell
    
    # Prečica na radnoj površini (Desktop)
    $desktopPath = [System.Environment]::GetFolderPath("Desktop")
    $desktopShortcutPath = Join-Path $desktopPath "PLATA.lnk"
    $shortcut = $WshShell.CreateShortcut($desktopShortcutPath)
    $shortcut.TargetPath = $exeDest
    $shortcut.WorkingDirectory = $destDir
    $shortcut.Description = "Sistem za Obračun Zarada (PLATA)"
    $shortcut.Save()
    Write-Host "-> Prečica uspešno kreirana na Desktopu." -ForegroundColor Gray
    
    # Prečica u Start meniju
    $startMenuPath = [System.Environment]::GetFolderPath("Programs")
    $plataStartDir = Join-Path $startMenuPath "PLATA"
    
    if (-not (Test-Path $plataStartDir)) {
        New-Item -ItemType Directory -Path $plataStartDir -Force | Out-Null
    }
    
    $startShortcutPath = Join-Path $plataStartDir "PLATA.lnk"
    $startShortcut = $WshShell.CreateShortcut($startShortcutPath)
    $startShortcut.TargetPath = $exeDest
    $startShortcut.WorkingDirectory = $destDir
    $startShortcut.Description = "Sistem za Obračun Zarada (PLATA)"
    $startShortcut.Save()
    Write-Host "-> Prečica uspešno kreirana u Start Meniju." -ForegroundColor Gray
}
catch {
    Write-Host "UPOZORENJE: Greška pri kreiranju prečica (moguće zbog prava pristupa). Program je instaliran, ali prečice morate kreirati ručno." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

# 8. Kraj instalacije i instrukcije
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "         PLATA JE USPEŠNO INSTALIRANA NA RAČUNAR!         " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Aplikacija se nalazi u: $destDir" -ForegroundColor White
Write-Host "Sada možete pokrenuti program preko prečice 'PLATA' na radnoj površini." -ForegroundColor White
Write-Host "Hvala što koristite naš program!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Pritisnite [Enter] za izlaz..." -ForegroundColor Gray
$null = Read-Host
