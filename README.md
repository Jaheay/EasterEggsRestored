# Easter Egg Restored

Small Kopernicus/ModuleManager patch set for restoring or lifting stock easter egg statics that are buried or misplaced by stock terrain behavior or planet-pack terrain overlays.

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

# Easter Egg Restored v2

Experimental KSP plugin for moving stock PQSCity easter-egg statics that are not reachable via normal ModuleManager text-node patches.

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
