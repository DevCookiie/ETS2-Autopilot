# ETS2 Autopilot - Byg og installer guide

## Krav

### C++ Plugin
- Visual Studio 2022 (Community er gratis) med "Desktop development with C++" 
- CMake 3.20+

### C# App
- .NET 8 SDK (gratis fra microsoft.com/dotnet)
- vJoy driver: https://github.com/jshafer817/vJoy/releases

---

## Byg C++ Plugin

```bash
cd plugin
mkdir build && cd build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

Output: `build/Release/ets2autopilot.dll`

**Kopiér DLL'en til:**
```
C:\Program Files (x86)\Steam\steamapps\common\Euro Truck Simulator 2\bin\win_x64\plugins\
```
(Opret `plugins` mappen hvis den ikke eksisterer)

---

## Byg C# App

```bash
cd AutopilotApp
dotnet build -c Release
# Eller åbn AutopilotApp.csproj i Visual Studio
```

---

## ETS2 konfiguration

1. Start ETS2
2. Gå til **Indstillinger → Controls**
3. Tilføj vJoy Device 1 som controller
4. Mapp akserne:
   - X-akse → Styring
   - RZ-akse → Gas  
   - Z-akse → Bremse
5. Kør en tur og tjek at vJoy input virker manuelt

---

## Brug

1. Installer vJoy driver
2. Kopiér `ets2autopilot.dll` til ETS2 plugins mappe
3. Start ETS2 og kør en rute i GPS
4. Start `ETS2Autopilot.exe`
5. Vent til den viser "Forbundet til ETS2"
6. Tryk **F5** eller klik knappen for at aktivere

---

## Steam Workshop

Plugin-DLL'er kan ikke direkte uploades via Workshop (.scs content mods).
Distribuer som:
- GitHub Releases med installer
- Nexus Mods (https://www.nexusmods.com/eurotrucksimulator2)
- ETS2 community forums med manual install guide

---

## Finjustering af PID

Hvis lastbilen slingrer: sænk `SteerKp` (default 0.6)
Hvis reaktionen er for langsom: øg `SteerKp`
Hvis den oscillerer: øg `SteerKd`
