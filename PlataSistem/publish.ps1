# publish.ps1
# Automatska skripta za kreiranje instalacionog paketa i publikovanje aplikacije PLATA

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         PLATA - KREIRANJE INSTALACIONOG PAKETA           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$baseDir = Get-Location
$appProj = Join-Path $baseDir "PlataApp\PlataApp.csproj"
$dbFile = Join-Path $baseDir "plata.db"
$releasePackageDir = Join-Path $baseDir "ReleasePackage"
$publishOutputDir = Join-Path $baseDir "publish_output"
$plataInstallDir = Join-Path $baseDir "PlataInstall"

# 1. Čišćenje prethodnih paketa
Write-Host "[1/7] Čišćenje starih instalacionih fajlova..." -ForegroundColor Yellow
if (Test-Path $releasePackageDir) {
    Remove-Item -Path $releasePackageDir -Recurse -Force
}
if (Test-Path $publishOutputDir) {
    Remove-Item -Path $publishOutputDir -Recurse -Force
}
if (Test-Path $plataInstallDir) {
    Remove-Item -Path $plataInstallDir -Recurse -Force
}
New-Item -ItemType Directory -Path $plataInstallDir -Force | Out-Null

# 2. dotnet publish - samostalna single-file aplikacija za 64-bitni Windows
Write-Host "[2/7] Pokretanje 'dotnet publish' (Self-Contained win-x64)..." -ForegroundColor Yellow
& dotnet publish $appProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishOutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publikovanje aplikacije nije uspelo!"
}

# 3. Kreiranje strukture distributivnog foldera
Write-Host "[3/7] Priprema foldera za instalaciju..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $releasePackageDir -Force | Out-Null

# Kopiranje izvršnog fajla u izlazni paket
Copy-Item -Path (Join-Path $publishOutputDir "PlataApp.exe") -Destination $releasePackageDir -Force

# Kopiranje ikonice u izlazni paket
$icoFile = Join-Path $baseDir "PlataApp\plata.ico"
if (Test-Path $icoFile) {
    Write-Host "Kopiranje ikonice plata.ico..." -ForegroundColor Gray
    Copy-Item -Path $icoFile -Destination (Join-Path $releasePackageDir "plata.ico") -Force
} else {
    Write-Host "NAPOMENA: plata.ico nije pronađena." -ForegroundColor Yellow
}

# Kopiranje HTML uputstva u izlazni paket
$helpSrc = Join-Path $baseDir "PlataApp\Resources\Help\uputstvo.html"
$helpDst = Join-Path $releasePackageDir "Resources\Help"
if (Test-Path $helpSrc) {
    Write-Host "Kopiranje korisničkog uputstva (uputstvo.html)..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $helpDst -Force | Out-Null
    Copy-Item -Path $helpSrc -Destination (Join-Path $helpDst "uputstvo.html") -Force
} else {
    Write-Host "NAPOMENA: uputstvo.html nije pronađeno na $helpSrc" -ForegroundColor Yellow
}

# Kopiranje SQLite baze podataka u izlazni paket
if (Test-Path $dbFile) {
    Write-Host "Kopiranje baze podataka plata.db u instalacioni paket..." -ForegroundColor Gray
    Copy-Item -Path $dbFile -Destination (Join-Path $releasePackageDir "plata.db") -Force
} else {
    Write-Host "UPOZORENJE: Baza podataka plata.db nije pronađena u $dbFile!" -ForegroundColor Red
}

# Kopiranje foldera sa bazama firmi u izlazni paket - SIGURNI NAČIN (Samo šablonske baze, bez osetljivih podataka i backup-a)
$bazeDir = Join-Path $baseDir "Baze"
if (Test-Path $bazeDir) {
    Write-Host "Kopiranje šablonskih baza firmi u instalacioni paket..." -ForegroundColor Gray
    $bazeDestDir = Join-Path $releasePackageDir "Baze"
    New-Item -ItemType Directory -Path $bazeDestDir -Force | Out-Null
    
    # Kopiramo samo šablonsku/test bazu (firma_123_*.db), preskačemo osetljive produkcione baze i backup folder RezervneKopije
    Get-ChildItem -Path $bazeDir -File -Filter "firma_123_*.db" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $bazeDestDir -Force
        Write-Host "  -> Uključena šablonska baza: $($_.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "NAPOMENA: Folder Baze/ nije pronađen - baze firmi neće biti uključene u paket." -ForegroundColor Yellow
}

# 4. Kopiranje i priprema instalacione skripte
Write-Host "[4/7] Priprema instalacione skripte..." -ForegroundColor Yellow
$installerSource = Join-Path $baseDir "Instalacija.ps1"
if (Test-Path $installerSource) {
    Copy-Item -Path $installerSource -Destination (Join-Path $releasePackageDir "Instalacija.ps1") -Force
} else {
    Write-Error "Instalaciona skripta Instalacija.ps1 nije pronađena na putanji $installerSource!"
}

# 5. Kreiranje ZIP arhive za lakšu distribuciju
Write-Host "[5/7] Kreiranje distributivne ZIP arhive..." -ForegroundColor Yellow
$zipPath = Join-Path $plataInstallDir "PlataSistem_Instalacija.zip"
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}
Compress-Archive -Path (Join-Path $releasePackageDir "*") -DestinationPath $zipPath -Force

# 6. Kreiranje Windows instalera pomoću Inno Setup (ako je instaliran)
Write-Host "[6/7] Pokušaj kreiranja Windows instalera (Inno Setup)..." -ForegroundColor Yellow
$issScript = Join-Path $baseDir "PlataSetup.iss"

# Traži iscc.exe na standardnim lokacijama
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe",
    "C:\Program Files (x86)\Inno Setup 5\iscc.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe"
)
$isccExe = $null
foreach ($p in $isccPaths) {
    if (Test-Path $p) { $isccExe = $p; break }
}
# Pokušaj i putem PATH-a
if (-not $isccExe) {
    $isccCmd = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($isccCmd) { $isccExe = $isccCmd.Source }
}

if ($isccExe -and (Test-Path $issScript)) {
    Write-Host "Inno Setup pronađen: $isccExe" -ForegroundColor Gray
    Write-Host "Kompajliranje instalera..." -ForegroundColor Gray
    & $isccExe $issScript
    if ($LASTEXITCODE -eq 0) {
        $setupExe = Get-ChildItem -Path $plataInstallDir -Filter "PlataSetup_v*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($setupExe) {
            Write-Host "-> Windows installer kreiran: $($setupExe.Name)" -ForegroundColor Green
        }
    } else {
        Write-Host "UPOZORENJE: Inno Setup kompajliranje nije uspelo (exit code: $LASTEXITCODE)" -ForegroundColor Red
    }
} elseif (-not $isccExe) {
    Write-Host "[i] Inno Setup nije instaliran - Windows installer nije kreiran." -ForegroundColor Yellow
    Write-Host "    Preuzmi ga sa: https://jrsoftware.org/isinfo.php" -ForegroundColor Gray
    Write-Host "    Zatim ponovo pokreni publish.ps1 za automatsko kreiranje .exe instalera." -ForegroundColor Gray
} else {
    Write-Host "[i] PlataSetup.iss nije pronađen - preskaće se kreiranje Windows instalera." -ForegroundColor Yellow
}

# 7. Završetak i rezime
Write-Host "[7/7] Čišćenje privremenih fajlova..." -ForegroundColor Yellow
if (Test-Path $publishOutputDir) {
    Remove-Item -Path $publishOutputDir -Recurse -Force
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   USPEŠNO KREIRAN INSTALACIONI PAKET ZA APLIKACIJU!     " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Lokacija instalacionog paketa: $releasePackageDir" -ForegroundColor White
Write-Host "Lokacija ZIP arhive za slanje: $zipPath" -ForegroundColor White
$setupExeFinal = Get-ChildItem -Path $plataInstallDir -Filter "PlataSetup_v*.exe" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($setupExeFinal) {
    Write-Host "Windows installer (.exe):      $($setupExeFinal.FullName)" -ForegroundColor White
}
Write-Host "==========================================================" -ForegroundColor Green
