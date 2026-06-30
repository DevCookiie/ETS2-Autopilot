# ETS2 Autopilot - Fix manglende afhængigheder
$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  ETS2 Autopilot - Fix Dependencies" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# --- Find ETS2 ---
$ets2Path = $null
$steamPaths = @(
    "C:\Program Files (x86)\Steam",
    "C:\Program Files\Steam",
    "D:\Steam", "D:\SteamLibrary", "E:\Steam", "E:\SteamLibrary"
)
foreach ($s in $steamPaths) {
    $c = "$s\steamapps\common\Euro Truck Simulator 2"
    if (Test-Path $c) { $ets2Path = $c; break }
}
if (-not $ets2Path) {
    $vdf = "C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"') | ForEach-Object {
            $lib = $_.Groups[1].Value -replace "\\\\","\"
            $c = "$lib\steamapps\common\Euro Truck Simulator 2"
            if (Test-Path $c) { $ets2Path = $c }
        }
    }
}
if (-not $ets2Path) { Write-Host "FEJL: ETS2 ikke fundet!" -ForegroundColor Red; exit 1 }

$pluginsDir = "$ets2Path\bin\win_x64\plugins"
New-Item -ItemType Directory -Force $pluginsDir | Out-Null

# =========================================
# FIX 1: Download scs-telemetry.dll
# =========================================
Write-Host "FIX 1: Downloader scs-sdk-plugin..." -ForegroundColor Yellow

$dllDest = "$pluginsDir\scs-telemetry.dll"

if (Test-Path $dllDest) {
    Write-Host "  scs-telemetry.dll allerede installeret - springer over." -ForegroundColor Green
} else {
    try {
        # Hent seneste release info fra GitHub API
        Write-Host "  Henter release-info fra GitHub..." -ForegroundColor Gray
        $headers = @{ "User-Agent" = "ETS2Autopilot-Installer" }
        $release = Invoke-RestMethod "https://api.github.com/repos/RenCloud/scs-sdk-plugin/releases/latest" -Headers $headers

        # Find DLL asset
        $asset = $release.assets | Where-Object { $_.name -like "*.dll" -and $_.name -notlike "*pdb*" } | Select-Object -First 1

        if (-not $asset) {
            # Fallback: prøv zip
            $asset = $release.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1
        }

        if ($asset) {
            $tmpFile = "$env:TEMP\scs_download_$([System.IO.Path]::GetRandomFileName())"
            Write-Host "  Downloader: $($asset.name) ($([math]::Round($asset.size/1KB)) KB)..." -ForegroundColor Gray
            Invoke-WebRequest $asset.browser_download_url -OutFile $tmpFile -Headers $headers

            if ($asset.name -like "*.zip") {
                # Pak ud og find DLL
                $tmpDir = "$env:TEMP\scs_extract_$([System.Guid]::NewGuid())"
                Expand-Archive $tmpFile -DestinationPath $tmpDir -Force
                $dll = Get-ChildItem $tmpDir -Recurse -Filter "scs-telemetry.dll" | Select-Object -First 1
                if (-not $dll) {
                    $dll = Get-ChildItem $tmpDir -Recurse -Filter "*.dll" | Where-Object { $_.Name -notlike "*pdb*" } | Select-Object -First 1
                }
                if ($dll) {
                    Copy-Item $dll.FullName $dllDest -Force
                } else {
                    throw "DLL ikke fundet i zip-filen"
                }
                Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
            } else {
                Copy-Item $tmpFile $dllDest -Force
            }
            Remove-Item $tmpFile -Force -ErrorAction SilentlyContinue

            Write-Host "  [OK] scs-telemetry.dll installeret til:" -ForegroundColor Green
            Write-Host "       $pluginsDir" -ForegroundColor Green
        } else {
            throw "Ingen DLL asset fundet i release"
        }
    } catch {
        Write-Host "  [FEJL] Automatisk download fejlede: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Manuel installation:" -ForegroundColor Yellow
        Write-Host "  1. Gaa til: https://github.com/RenCloud/scs-sdk-plugin/releases" -ForegroundColor White
        Write-Host "  2. Download den nyeste .dll fil" -ForegroundColor White
        Write-Host "  3. Kopier den til:" -ForegroundColor White
        Write-Host "     $pluginsDir" -ForegroundColor Cyan
        Write-Host ""
    }
}

# =========================================
# FIX 2: Konfigurer vJoy Device 1
# =========================================
Write-Host ""
Write-Host "FIX 2: Konfigurerer vJoy Device 1..." -ForegroundColor Yellow

$vJoyDir  = "C:\Program Files\vJoy"
$vJoyConf = "$vJoyDir\x64\vJoyConf.exe"
$vJoyCli  = "$vJoyDir\x64\vjoyconfig.exe"  # CLI version hvis tilgængelig

# Tjek om Device 1 allerede eksisterer
$deviceExists = $false
try {
    $reg = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\vjoy\Parameters\Device1" -ErrorAction Stop
    $deviceExists = $true
} catch { }

if ($deviceExists) {
    Write-Host "  [OK] vJoy Device 1 allerede konfigureret." -ForegroundColor Green
} else {
    # Prøv CLI konfiguration
    if (Test-Path $vJoyCli) {
        Write-Host "  Konfigurerer via CLI..." -ForegroundColor Gray
        try {
            # Aktivér device 1 med de nødvendige akser (X=styring, RZ=gas, Z=bremse)
            & $vJoyCli 1 /AX 1 /AY 0 /AZ 1 /ARX 0 /ARY 0 /ARZ 1 /SL 0 /POV 0 /BTN 4 2>&1
            Write-Host "  [OK] vJoy Device 1 konfigureret via CLI." -ForegroundColor Green
        } catch {
            Write-Host "  CLI fejlede, aabner GUI..." -ForegroundColor Yellow
            Start-Process $vJoyConf
        }
    } elseif (Test-Path $vJoyConf) {
        Write-Host "  Aabner vJoy Configurator..." -ForegroundColor Gray
        Start-Process $vJoyConf
        Write-Host ""
        Write-Host "  I vJoy Configurator:" -ForegroundColor White
        Write-Host "  1. Saet 'Number of Devices' til 1" -ForegroundColor White
        Write-Host "  2. Sørg for at Device 1 har disse akser markeret:" -ForegroundColor White
        Write-Host "     [X] X    <- Styring" -ForegroundColor Cyan
        Write-Host "     [X] Z    <- Bremse" -ForegroundColor Cyan
        Write-Host "     [X] Rz   <- Gas" -ForegroundColor Cyan
        Write-Host "  3. Klik 'Apply' og derefter 'OK'" -ForegroundColor White
        Write-Host ""
        Read-Host "  Tryk Enter naar du har klikket Apply i vJoy Configurator"
    } else {
        Write-Host "  [FEJL] vJoy ikke installeret!" -ForegroundColor Red
        Write-Host "  Download fra: https://github.com/jshafer817/vJoy/releases" -ForegroundColor Yellow
    }
}

# =========================================
# Resultat
# =========================================
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan

$dll1Ok = Test-Path "$pluginsDir\scs-telemetry.dll"
$vjoyOk = Test-Path "C:\Program Files\vJoy\x64\vJoyInterface.dll"

if ($dll1Ok -and $vjoyOk) {
    Write-Host "  Alt fixet! Start ETS2 og koer testen:" -ForegroundColor Green
    Write-Host "  1. Start ETS2 (scs-telemetry.dll loades automatisk)" -ForegroundColor White
    Write-Host "  2. Aaben dist\ETS2Autopilot.exe" -ForegroundColor White
    Write-Host "  3. Gaa til Diagnostik-fanen og tjek at alt er groendt" -ForegroundColor White
    Write-Host "  4. Klik 'Send test-styring til vJoy' for at verificere" -ForegroundColor White
} else {
    if (-not $dll1Ok) { Write-Host "  [MANGLER] scs-telemetry.dll" -ForegroundColor Red }
    if (-not $vjoyOk) { Write-Host "  [MANGLER] vJoy" -ForegroundColor Red }
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "Tryk Enter for at afslutte"
