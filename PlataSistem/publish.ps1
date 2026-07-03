# publish.ps1
# Automatska skripta za kreiranje instalacionog paketa i publikovanje aplikacije PLATA

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         PLATA - KREIRANJE INSTALACIONOG PAKETA           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$baseDir = Get-Location
$appProj = Join-Path $baseDir "PlataApp\PlataApp.csproj"
$publishOutputDir = Join-Path $baseDir "publish_output"
$releasePackageDir = Join-Path $baseDir "ReleasePackage" # Ovde će Velopack smestiti fajlove

# Prompt for version
$version = Read-Host "Unesi verziju za pakovanje (npr. 1.0.0)"
if ([string]::IsNullOrWhiteSpace($version)) {
    Write-Error "Verzija je obavezna!"
}

# 1. Čišćenje prethodnih paketa
Write-Host "[1/5] Čišćenje starih publish fajlova..." -ForegroundColor Yellow
if (Test-Path $publishOutputDir) {
    Remove-Item -Path $publishOutputDir -Recurse -Force
}

# 2. dotnet publish
Write-Host "[2/5] Pokretanje 'dotnet publish' (Self-Contained win-x64)..." -ForegroundColor Yellow
& dotnet publish $appProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishOutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publikovanje aplikacije nije uspelo!"
}

# Kopiranje baze i help fajlova u publish_output (Velopack pakuje ceo folder)
Write-Host "[3/5] Kopiranje dodatnih fajlova..." -ForegroundColor Yellow
$dbFile = Join-Path $baseDir "plata.db"
if (Test-Path $dbFile) {
    Copy-Item -Path $dbFile -Destination (Join-Path $publishOutputDir "plata.db") -Force
}

# Kopiranje help foldera
$helpSrc = Join-Path $baseDir "PlataApp\Resources\Help"
$helpDst = Join-Path $publishOutputDir "Resources\Help"
if (Test-Path $helpSrc) {
    New-Item -ItemType Directory -Path $helpDst -Force | Out-Null
    Copy-Item -Path "$helpSrc\*" -Destination $helpDst -Recurse -Force
}

# Kopiranje baze firmi
$bazeDir = Join-Path $baseDir "Baze"
if (Test-Path $bazeDir) {
    $bazeDestDir = Join-Path $publishOutputDir "Baze"
    New-Item -ItemType Directory -Path $bazeDestDir -Force | Out-Null
    Get-ChildItem -Path $bazeDir -File -Filter "firma_123_*.db" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $bazeDestDir -Force
    }
}

# 4. Instalacija/Ažuriranje vpk (Velopack CLI)
Write-Host "[4/5] Provera instalacije Velopack CLI alata (vpk)..." -ForegroundColor Yellow
try { & dotnet tool install -g vpk 2>$null } catch { }
try { & dotnet tool update -g vpk 2>$null } catch { }

# 5. Kreiranje paketa putem Velopack-a
Write-Host "[5/5] Pakovanje pomoću Velopack-a..." -ForegroundColor Yellow

$iconPath = Join-Path $baseDir "PlataApp\plata.ico"

$vpkArgs = @(
    "pack",
    "--packId", "PlataSistem",
    "--packVersion", $version,
    "--packDir", $publishOutputDir,
    "--mainExe", "PlataApp.exe",
    "--outputDir", $releasePackageDir,
    "--packTitle", "PlataSistem"
)

if (Test-Path $iconPath) {
    $vpkArgs += "--icon"
    $vpkArgs += $iconPath
}

& vpk $vpkArgs

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   USPEŠNO KREIRAN VELOPACK INSTALACIONI PAKET!           " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Fajlovi se nalaze u folderu: $releasePackageDir" -ForegroundColor White
Write-Host "Instalacioni fajl: PlataSistem-Setup-$version.exe" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green

Write-Host ""
$uploadToGithub = Read-Host "Da li želiš automatski upload na GitHub Releases? (y/n)"
if ($uploadToGithub -eq 'y' -or $uploadToGithub -eq 'Y') {
    Write-Host "NAPOMENA: Za upload ti je potreban GitHub token koji ima prava PISANJA (repo scope)!" -ForegroundColor Yellow
    $githubToken = Read-Host "Unesi GitHub token za upload"
    
    if ([string]::IsNullOrWhiteSpace($githubToken)) {
        Write-Host "Preskačem upload jer token nije unet." -ForegroundColor Yellow
    } else {
        Write-Host "Pokrećem upload na GitHub..." -ForegroundColor Yellow
        $uploadArgs = @(
            "upload", "github",
            "--repoUrl", "https://github.com/blagojevicboban/ObracunZarada",
            "--publish",
            "--releaseName", $version,
            "--token", $githubToken,
            "--outputDir", $releasePackageDir
        )
        & vpk $uploadArgs
        
        Write-Host "Upload uspešno završen!" -ForegroundColor Green
    }
}
