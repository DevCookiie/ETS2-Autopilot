# ETS2 Autopilot - Installer
# Kør som administrator

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "  ETS2 Autopilot Installer" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# --- Find ETS2 ---
$steamPaths = @(
    "C:\Program Files (x86)\Steam",
    "C:\Program Files\Steam",
    "D:\Steam",
    "D:\SteamLibrary",
    "E:\Steam",
    "E:\SteamLibrary"
)

$ets2Path = $null
foreach ($steam in $steamPaths) {
    $candidate = "$steam\steamapps\common\Euro Truck Simulator 2"
    if (Test-Path $candidate) {
        $ets2Path = $candidate
        break
    }
}

# Check Steam libraryfolders.vdf for extra library locations
if (-not $ets2Path) {
    $vdf = "C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        $content = Get-Content $vdf -Raw
        $matches = [regex]::Matches($content, '"path"\s+"([^"]+)"')
        foreach ($m in $matches) {
            $lib = $m.Groups[1].Value.Replace("\\\\", "\")
            $candidate = "$lib\steamapps\common\Euro Truck Simulator 2"
            if (Test-Path $candidate) {
                $ets2Path = $candidate
                break
            }
        }
    }
}

if (-not $ets2Path) {
    Write-Host "FEJL: Kunne ikke finde ETS2. Angiv stien manuelt:" -ForegroundColor Red
    $ets2Path = Read-Host "ETS2 sti (f.eks. D:\Steam\steamapps\common\Euro Truck Simulator 2)"
    if (-not (Test-Path $ets2Path)) {
        Write-Host "Stien eksisterer ikke. Afbryder." -ForegroundColor Red
        exit 1
    }
}

Write-Host "ETS2 fundet: $ets2Path" -ForegroundColor Green

# --- Kopier plugin DLL ---
$pluginsDir = "$ets2Path\bin\win_x64\plugins"
if (-not (Test-Path $pluginsDir)) {
    New-Item -ItemType Directory -Force $pluginsDir | Out-Null
    Write-Host "Oprettede plugins mappe." -ForegroundColor Yellow
}

$dllSrc = Join-Path $PSScriptRoot "ETS2_Plugin\ets2autopilot.dll"
if (-not (Test-Path $dllSrc)) {
    Write-Host "FEJL: ets2autopilot.dll ikke fundet i ETS2_Plugin mappen." -ForegroundColor Red
    exit 1
}

Copy-Item $dllSrc "$pluginsDir\ets2autopilot.dll" -Force
Write-Host "Plugin installeret til: $pluginsDir" -ForegroundColor Green

# --- Kopier ETS2Autopilot.exe til Desktop ---
$exeSrc = Join-Path $PSScriptRoot "ETS2Autopilot.exe"
$desktop = [Environment]::GetFolderPath("Desktop")
Copy-Item $exeSrc "$desktop\ETS2Autopilot.exe" -Force
Write-Host "ETS2Autopilot.exe kopieret til dit skrivebord." -ForegroundColor Green

# --- Check vJoy ---
$vJoyInstalled = Test-Path "C:\Program Files\vJoy\x64\vJoyInterface.dll"
if (-not $vJoyInstalled) {
    Write-Host ""
    Write-Host "ADVARSEL: vJoy er ikke installeret!" -ForegroundColor Yellow
    Write-Host "Download det her: https://github.com/jshafer817/vJoy/releases" -ForegroundColor Yellow
} else {
    Write-Host "vJoy: installeret." -ForegroundColor Green
}

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "  Installation faerdig!" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Naeste trin:" -ForegroundColor White
Write-Host "  1. Start ETS2" -ForegroundColor White
Write-Host "  2. Gaa til Indstillinger -> Controls" -ForegroundColor White
Write-Host "  3. Tilfoej vJoy Device 1 som controller" -ForegroundColor White
Write-Host "     - X-akse   -> Styring" -ForegroundColor Gray
Write-Host "     - RZ-akse  -> Gas" -ForegroundColor Gray
Write-Host "     - Z-akse   -> Bremse" -ForegroundColor Gray
Write-Host "  4. Aaben ETS2Autopilot.exe fra skrivebordet" -ForegroundColor White
Write-Host "  5. Tryk F5 i spillet for at aktivere" -ForegroundColor White
Write-Host ""
Read-Host "Tryk Enter for at afslutte"
