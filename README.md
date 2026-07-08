# Easter Egg Restored

This package contains source and config for the EasterEggRestored runtime PQSCity mover.

It currently targets three stock static city objects:

- Vall / Icehenge: Vallhenge, moved to the flatter three-flag site from `quicksave-new.sfs`.
- Dres / Anniversary6 : red sports car, lifted only when `GameData/BetterDres` exists.
- Eeloo / Anniversary3:  Snowkerbal, lifted only when `GameData/OPM` or `GameData/OuterPlanetsMod` exists.

Current rule:

- Vall / Icehenge: moves Vallhenge upward to the surveyed Kertrey marker position.

## Build

Compile `Source/EasterEggRestored.csproj` against your KSP install:

```powershell
msbuild .\Source\EasterEggRestored.csproj /p:Configuration=Release /p:KspRoot="C:\Tools\Steam\steamapps\common\Kerbal Space Program"
```

The output DLL goes to:

```text
GameData/EasterEggRestored/Plugins/EasterEggRestored.dll
```

## Included patches

- `Vall_Icehenge_Restore.cfg`
  - Patches `City[Icehenge]` on Vall.
  - Moves/lifts Vallhenge to the surface

- `Dres_RedCar_BetterDres.cfg`
  - `:NEEDS[BetterDres]`
  - Patches `City[Anniversary6]` on Dres if that stock city node still exists.
  - Moves/Lifts the car to better dres height

- `Eeloo_Snowkerbal_OPM.cfg`
  - `:NEEDS[OPM]`
  - Patches `City[Anniversary3]` on Eeloo if that stock city node still exists.
