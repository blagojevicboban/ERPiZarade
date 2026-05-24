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

# 1. Čišćenje prethodnih paketa
Write-Host "[1/6] Čišćenje starih instalacionih fajlova..." -ForegroundColor Yellow
if (Test-Path $releasePackageDir) {
    Remove-Item -Path $releasePackageDir -Recurse -Force
}
if (Test-Path $publishOutputDir) {
    Remove-Item -Path $publishOutputDir -Recurse -Force
}

# 2. dotnet publish - samostalna single-file aplikacija za 64-bitni Windows
Write-Host "[2/6] Pokretanje 'dotnet publish' (Self-Contained win-x64)..." -ForegroundColor Yellow
& dotnet publish $appProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishOutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publikovanje aplikacije nije uspelo!"
}

# 3. Kreiranje strukture distributivnog foldera
Write-Host "[3/6] Priprema foldera za instalaciju..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $releasePackageDir -Force | Out-Null

# Kopiranje izvršnog fajla u izlazni paket
Copy-Item -Path (Join-Path $publishOutputDir "PlataApp.exe") -Destination $releasePackageDir -Force

# Kopiranje SQLite baze podataka u izlazni paket
if (Test-Path $dbFile) {
    Write-Host "Kopiranje baze podataka plata.db u instalacioni paket..." -ForegroundColor Gray
    Copy-Item -Path $dbFile -Destination (Join-Path $releasePackageDir "plata.db") -Force
} else {
    Write-Host "UPOZORENJE: Baza podataka plata.db nije pronađena u $dbFile!" -ForegroundColor Red
}

# 4. Kopiranje i priprema instalacione skripte
Write-Host "[4/6] Priprema instalacione skripte..." -ForegroundColor Yellow
$installerSource = Join-Path $baseDir "Instalacija.ps1"
if (Test-Path $installerSource) {
    Copy-Item -Path $installerSource -Destination (Join-Path $releasePackageDir "Instalacija.ps1") -Force
} else {
    Write-Error "Instalaciona skripta Instalacija.ps1 nije pronađena na putanji $installerSource!"
}

# 5. Kreiranje ZIP arhive za lakšu distribuciju
Write-Host "[5/6] Kreiranje distributivne ZIP arhive..." -ForegroundColor Yellow
$zipPath = Join-Path $baseDir "PlataSistem_Instalacija.zip"
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}
Compress-Archive -Path (Join-Path $releasePackageDir "*") -DestinationPath $zipPath -Force

# 6. Završetak i rezime
Write-Host "[6/6] Čišćenje privremenih fajlova..." -ForegroundColor Yellow
if (Test-Path $publishOutputDir) {
    Remove-Item -Path $publishOutputDir -Recurse -Force
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   USPEŠNO KREIRAN INSTALACIONI PAKET ZA APLIKACIJU!     " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Lokacija instalacionog paketa: $releasePackageDir" -ForegroundColor White
Write-Host "Lokacija ZIP arhive za slanje: $zipPath" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
