# ETS2 Autopilot - Test & Setup checker
$ErrorActionPreference = "SilentlyContinue"

$ok  = "[OK]  "
$err = "[FEJL]"
$war = "[OBS] "

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  ETS2 Autopilot - Setup Checker" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$allOk = $true

# --- 1. Find ETS2 ---
Write-Host "1. Finder ETS2..." -ForegroundColor White
$steamPaths = @(
    "C:\Program Files (x86)\Steam",
    "C:\Program Files\Steam",
    "D:\Steam", "D:\SteamLibrary",
    "E:\Steam", "E:\SteamLibrary"
)
$ets2Path = $null
foreach ($s in $steamPaths) {
    $c = "$s\steamapps\common\Euro Truck Simulator 2"
    if (Test-Path $c) { $ets2Path = $c; break }
}
# Check libraryfolders.vdf
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

if ($ets2Path) {
    Write-Host "  $ok ETS2 fundet: $ets2Path" -ForegroundColor Green
} else {
    Write-Host "  $err ETS2 ikke fundet!" -ForegroundColor Red
    $allOk = $false
}

# --- 2. Check scs-sdk-plugin ---
Write-Host ""
Write-Host "2. Tjekker scs-sdk-plugin..." -ForegroundColor White
$pluginsDir = "$ets2Path\bin\win_x64\plugins"
$sdkPlugin  = "$pluginsDir\scs-telemetry.dll"
$ourPlugin  = "$pluginsDir\ets2autopilot.dll"

if (Test-Path $sdkPlugin) {
    Write-Host "  $ok scs-telemetry.dll fundet" -ForegroundColor Green
} else {
    Write-Host "  $err scs-telemetry.dll MANGLER!" -ForegroundColor Red
    Write-Host "       Download fra: https://github.com/RenCloud/scs-sdk-plugin/releases" -ForegroundColor Yellow
    Write-Host "       Kopier til: $pluginsDir" -ForegroundColor Yellow
    $allOk = $false
}

# --- 3. Check vJoy ---
Write-Host ""
Write-Host "3. Tjekker vJoy..." -ForegroundColor White
$vJoyDll = "C:\Program Files\vJoy\x64\vJoyInterface.dll"
$vJoyConf = "C:\Program Files\vJoy\x64\vJoyConf.exe"

if (Test-Path $vJoyDll) {
    Write-Host "  $ok vJoy driver installeret" -ForegroundColor Green

    # Check if vJoy device 1 is configured
    $vJoyReg = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\vjoy\Parameters\Device1" -ErrorAction SilentlyContinue
    if ($vJoyReg) {
        Write-Host "  $ok vJoy Device 1 er konfigureret" -ForegroundColor Green
    } else {
        Write-Host "  $war vJoy Device 1 ikke fundet - aabner konfiguration..." -ForegroundColor Yellow
        if (Test-Path $vJoyConf) { Start-Process $vJoyConf }
    }
} else {
    Write-Host "  $err vJoy ikke installeret!" -ForegroundColor Red
    Write-Host "       Download fra: https://github.com/jshafer817/vJoy/releases" -ForegroundColor Yellow
    $allOk = $false
}

# --- 4. Check shared memory (ETS2 skal koere) ---
Write-Host ""
Write-Host "4. Tjekker ETS2 telemetri (ETS2 skal koere)..." -ForegroundColor White
$ets2Running = Get-Process "eurotrucks2" -ErrorAction SilentlyContinue
if ($ets2Running) {
    Write-Host "  $ok ETS2 korer (PID: $($ets2Running.Id))" -ForegroundColor Green

    # Try to open shared memory
    $code = @"
using System;
using System.IO.MemoryMappedFiles;
try {
    var mmf = MemoryMappedFile.OpenExisting("Local\\SCSTelemetry");
    Console.WriteLine("CONNECTED");
    mmf.Dispose();
} catch {
    Console.WriteLine("NOT_CONNECTED");
}
"@
    $tmpCs  = [System.IO.Path]::GetTempFileName() + ".cs"
    $tmpExe = [System.IO.Path]::GetTempFileName() + ".exe"
    $code | Out-File $tmpCs -Encoding utf8

    $result = & dotnet-script $tmpCs 2>$null
    if ($result -eq "CONNECTED") {
        Write-Host "  $ok Shared memory forbundet - telemetri virker!" -ForegroundColor Green
    } else {
        Write-Host "  $war ETS2 korer men scs-sdk-plugin sender ikke data endnu." -ForegroundColor Yellow
        Write-Host "       Saerg for scs-telemetry.dll er i plugins-mappen og genstart ETS2." -ForegroundColor Yellow
    }
} else {
    Write-Host "  $war ETS2 korer ikke - start spillet for fuld test" -ForegroundColor Yellow
}

# --- 5. Check vJoy DLL til appen ---
Write-Host ""
Write-Host "5. Tjekker vJoy DLL adgang..." -ForegroundColor White
$appDir = Join-Path $PSScriptRoot "dist"
$exePath = Join-Path $appDir "ETS2Autopilot.exe"
if (Test-Path $exePath) {
    Write-Host "  $ok ETS2Autopilot.exe fundet i dist\" -ForegroundColor Green
} else {
    Write-Host "  $war ETS2Autopilot.exe ikke bygget endnu" -ForegroundColor Yellow
    Write-Host "       Kør: cd AutopilotApp && dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\dist" -ForegroundColor Gray
}

# --- Resultat ---
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
if ($allOk) {
    Write-Host "  Alt ser godt ud - klar til test!" -ForegroundColor Green
} else {
    Write-Host "  Nogle ting mangler - se fejl ovenfor" -ForegroundColor Red
}
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "Tryk Enter for at afslutte"
