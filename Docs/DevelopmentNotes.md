## Development Notes

Kittopia exports from the BetterDres/OPM install did not show the stock Dres/Eeloo City nodes. These patches can move/lift existing runtime City nodes by name, but they do not recreate the stock models from scratch if the planet pack fully removes the City node and model object.

If the Dres car or Eeloo Snowkerbal still does not appear, check `ModuleManager.ConfigCache` or a fresh Kittopia export to see whether `City[Anniversary6]` or `City[Anniversary3]` exists after patching.
