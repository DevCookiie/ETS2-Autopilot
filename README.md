# ETS2 Autopilot

Automatisk kørsel til Euro Truck Simulator 2. Lastbilen styrer selv efter GPS-ruten — gas, bremse og styring.

## Funktioner

- **Lane keeping** — holder lastbilen i kørebanen automatisk
- **GPS-sving** — bremser ned og drejer ved kryds
- **Fartregulering** — følger hastighedsgrænsen fra GPS
- **F5 toggle** — tænd/sluk med ét tastetryk midt i spillet

## Download & Installation

### Krav
- Euro Truck Simulator 2 (Steam)
- [vJoy driver](https://github.com/jshafer817/vJoy/releases) — virtual controller
- [scs-sdk-plugin](https://github.com/RenCloud/scs-sdk-plugin/releases) — telemetri-plugin til ETS2

### Trin

1. **Download** nyeste release fra [Releases](../../releases)
2. **Pak zip ud**
3. **Højreklik `install.ps1` → Kør med PowerShell** (som administrator)
   - Finder ETS2 automatisk og installerer plugin-DLL'en
   - Kopier `ETS2Autopilot.exe` til dit skrivebord
4. **Konfigurér vJoy i ETS2:**
   - Indstillinger → Controls → tilføj vJoy Device 1
   - X-akse → Styring | RZ-akse → Gas | Z-akse → Bremse
5. **Start en GPS-rute** i ETS2
6. **Åbn `ETS2Autopilot.exe`**
7. **Tryk F5** for at aktivere autopiloten

## Brug

| Knap | Funktion |
|------|----------|
| F5   | Aktiver / deaktiver autopilot |
| —    | Autopiloten tager over styring, gas og bremse |

I appens vindue kan du justere:
- Max hastighed
- Afstand til at bremse ned ved sving
- Til/fra: hastighedsgrænse-følger
- Til/fra: lane keeping

## Sådan virker det

```
ETS2 (spillet)
  ↓  scs-sdk-plugin DLL skriver telemetri til shared memory
ETS2Autopilot.exe læser: position, hastighed, GPS, styring
  ↓  PID-controller beregner: styreinput, gas, bremse
vJoy virtual controller sender input tilbage til ETS2
```

## Steam Workshop

Workshop-siden viser mod i jeres mod-liste og linker til denne GitHub for download.
ETS2 Workshop understøtter ikke executables (.exe) direkte, så selve appen downloades herfra.

## Fejlfinding

**"Ikke forbundet til ETS2"**
→ Tjek at `ets2autopilot.dll` ligger i `ETS2/bin/win_x64/plugins/`
→ Tjek at `scs-sdk-plugin.dll` også er installeret der

**Lastbilen slingrer**
→ Sænk Max hastighed i appen
→ PID-værdier kan justeres i koden (SteerKp/Kd i AutopilotEngine.cs)

**vJoy virker ikke**
→ Kør `ETS2Autopilot.exe` som administrator
→ Reinstaller vJoy driver

## Support

Join our Discord for help, bug reports and updates:
**https://discord.gg/eKqHFe6VSV**

## Licens

MIT — brug og modificér frit.
