# Easter Eggs Restored

This package contains source and config for the EasterEggsRestored runtime PQSCity mover. It restores selected stock PQSCity easter eggs that are buried, displaced, or removed by terrain changes.

It currently targets three stock static city objects:

- Vall / Icehenge: restored to the nearby flat site, with `reorientFinalAngle = 0`.
- Dres / Anniversary6: restored under `BetterDres` by cloning the stock PQSCity if BetterDres removed it.
- Eeloo / Anniversary3: restored under `OPM/OuterPlanetsMod`.

## Included Configs

- `Vall_Icehenge_Restore.cfg`
  - Restores Vall / Icehenge.

- `Dres_RedCar_BetterDres.cfg`
  - `:NEEDS[BetterDres]`
  - Restores Dres / Anniversary6 by cloning the stock PQSCity if BetterDres removed it.

- `Eeloo_Snowkerbal_OPM.cfg`
  - `:NEEDS[OPM|OuterPlanetsMod]`
  - Restores Eeloo / Anniversary3.

Config is loaded from `GameDatabase` after ModuleManager has filtered the `EASTER_EGG_RESTORED` nodes.

## Build

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  .\Source\EasterEggsRestored.csproj `
  /p:Configuration=Release `
  /p:KspRoot="C:\Tools\Steam\steamapps\common\Kerbal Space Program"
```

## License

[EasterEggsRestored](https://github.com/Jaheay/EasterEggsRestored) © 2026 by [Jaheay](https://github.com/Jaheay) is licensed under [Creative Commons Attribution-ShareAlike 4.0 International](https://creativecommons.org/licenses/by-sa/4.0/)

## AI Disclosure

AI was used in the development of this mod. The mod was reviewed by a human at every step.
