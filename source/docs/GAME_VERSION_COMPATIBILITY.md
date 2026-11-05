# Retail and beta compatibility

The beta Steam build 26326487 moves Deadfire from Unity 5.6.3p3 / Mono CLR 2 to Unity 2019.4.41f1 / Mono CLR 4. It splits Unity's managed API into modules. The conversation classes, six patched method signatures, and both camera-offset call sites remain present.

The original startup configuration retained `[Preloader.Entrypoint] Assembly = UnityEngine.dll`. That is valid for retail, but the beta's startup type is in `UnityEngine.CoreModule.dll`. BepInEx therefore did not reach its chainloader, so neither the interface nor the menu option appeared. Its usual disk log remained from the earlier retail launch.

The shared bootstrap now:

1. Runs under .NET 2.0-compatible APIs, so both Mono runtimes can load it.
2. Detects the modular Unity runtime before BepInEx starts.
3. Selects `UnityEngine.dll` for retail or `UnityEngine.CoreModule.dll` for beta in the existing BepInEx configuration.
4. Preserves unrelated settings and custom entry-point assemblies.
5. Invokes the bundled BepInEx preloader without changing any game assemblies on disk.

The native Doorstop proxy is distributed as `version.dll`. Do not install both proxy names (`version.dll` and the previous `winhttp.dll`) together. The previous bootstrap is backed up outside the repository/game install.

Validation on 2026-11-05:

- Beta loader log confirms Unity 2019.4.41f1, all six runtime hooks installed, and chainloader startup complete.
- User confirmed the menu toggle and vertical interface are visible on beta.
- Existing interface/plugin DLLs were retained; no beta-only interface rebuild was required.
- Eight bootstrap tests cover retail selection, beta selection, switching back, new/missing config entries, preservation of custom/unrelated settings, and repeated initialization.
- Retail previously worked with these same interface/plugin DLLs. A fresh in-game retail launch after switching branches has not yet been performed with the new automatic bootstrap.

Build the universal interface/plugin against retail Managed references. On a beta-only installation, `source/Build.ps1 -BootstrapOnly` refreshes the new bootstrap and manifest while retaining the compatible interface/plugin payload. To rebuild all shared code while beta is installed, provide a retained retail reference directory through `-ReferenceManagedDirectory`. Reference assemblies are development inputs and are not included in Git.

The engine bootstrap logs its choice in `PillarsOfEternityII_Data/DialogOverhaul/BepInEx/DialogOverhaul.bootstrap.log`. The standard BepInEx log remains alongside it as `LogOutput.log`. The beta's Unity log moved to `%USERPROFILE%/AppData/LocalLow/Obsidian Entertainment/Pillars of Eternity II/Player.log`.
