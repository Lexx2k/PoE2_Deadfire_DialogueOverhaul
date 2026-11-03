# Bundled runtime dependencies

The unmodified DLLs originate from the official BepInEx Windows x64 5.4.23.2 release:
https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2

Included components: BepInEx 5.4.23.2, HarmonyX (0Harmony assembly version 2.9.0.0), Harmony 2 interop, Mono.Cecil 0.10.4, MonoMod 22.1.29.1, and Unity Doorstop 4.3.0. Individual notices are in this directory. BepInEx/Harmony/HarmonyX/Mono.Cecil/MonoMod use their included MIT notices. Doorstop uses the included LGPL 2.1 text.

Doorstop's corresponding v4.3.0 source archive is included beside its license. Source and build instructions are also available at https://github.com/NeighTools/UnityDoorstop/tree/v4.3.0 . The bootstrap DLL is unmodified and can be replaced independently. Only the Doorstop configuration's entry-point path is changed for this package.

Original loader ZIP SHA256: F752CE4E838F4C305B9DA1404B6745F2CFF23B8BFD494F79F0C84D0A01F59B46.
