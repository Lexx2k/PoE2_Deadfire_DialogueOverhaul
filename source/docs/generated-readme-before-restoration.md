# Deadfire Dialogue Overhaul

A vertical, scrolling Dialogueue interface for Pillars of Eternity II: Deadfire, inspired by Disco Elysium and our Numenera overhaul.

The default build uses **BepInEx 5.4.23.2 and HarmonyX runtime patches**. It does not distribute or permanently patch `Assembly-CSharp.dll`. The conversation engine still controls choices, requirements, scripts and outcomes.

Features include a retained conversation transcript, native portraits and UI artwork, speaker colours, speech/narration formatting, glossary and reaction tooltips, response animations, camera framing, and a saved interface preference. Press **F10** during a conversation, or use **Options → Game → Vertical Dialogueue Interface**, to choose the vertical or original presentation.

## Install by copying files

Close Deadfire, then copy these into the game folder beside `PillarsOfEternityII.exe`:

1. The entire `PillarsOfEternityII_Data` folder. Merge it with the existing folder.
2. The three files inside `source/loader`: `winhttp.dll`, `doorstop_config.ini`, and `.doorstop_version`.

Start the game. No installer, archive extraction tool, build step or script execution is required.

**Upgrading our old DLL-patched prototype:** first restore the original `Assembly-CSharp.dll` from the verified `DialogueueOverhaulBackup` folder. This is a one-time migration; subsequent runtime mod updates are just file copying. The plugin refuses a still-patched legacy assembly to avoid double hooks.

**Already using BepInEx or another bootstrap loader:** do not overwrite its `winhttp.dll` or configuration blindly. With the same BepInEx 5.4.23.2 dependencies, keep its existing bootstrap and instead copy `PillarsOfEternityII_Data/DialogueOverhaul/BepInEx/plugins/DeadfireDialogueOverhaul` into its `BepInEx/plugins` folder. Still copy the interface DLL into the game's `Managed` folder. Other loader versions need compatibility testing.

## Layout

The repository root contains `readme.md`, `PillarsOfEternityII_Data`, and `source` (plus Git metadata).

- `PillarsOfEternityII_Data/Managed/Deadfire.DialogueOverhaul.dll`: interface implementation.
- `PillarsOfEternityII_Data/DialogueOverhaul/BepInEx`: bundled loader and runtime plugin.
- `PillarsOfEternityII_Data/DialogueOverhaul/ThirdParty`: dependency notices and Doorstop source.
- `source/src`: interface source.
- `source/runtime`: Harmony/BepInEx bridge source.
- `source/loader`: the bootstrap DLL/configuration installed beside the game executable.
- `source/tests`, `source/docs`: verification and technical notes.
- `source/legacy`: archived patcher, old installation tools and investigation history; not needed for copying the runtime version.

Doorstop requires the three bootstrap files beside the installed game executable. They stay under `source/loader` in Git to keep the repository root clean. The rest of the loader remains inside the game's data directory.

## Build and check

Requires the installed Windows x64 Mono game and a .NET 10 SDK. Pinned runtime dependencies are included; no NuGet restore is needed.

```powershell
.\source\Build.ps1
.\source\Test.ps1
```

Build output contains no replacement game assembly. Tests cover formatting, layout, camera calculations, runtime transpiler behaviour, payload hashes and the manual-copy bootstrap target. These checks do not substitute for an in-game startup and conversation test on Deadfire's older Mono runtime. See `source/docs/VALIDATION.md` for the current verification boundary.

## Uninstall and troubleshooting

With the game closed, remove `Deadfire.DialogueOverhaul.dll` from the game's `Managed` folder and the `DeadfireDialogueOverhaul` plugin folder. If this mod is the only user of the bundled loader, also remove its three bootstrap files and `PillarsOfEternityII_Data/DialogueOverhaul`. Keep any loader files shared with other mods.

The bundled loader writes its startup/plugin log to `PillarsOfEternityII_Data/DialogueOverhaul/BepInEx/LogOutput.log`. An existing standard loader uses `BepInEx/LogOutput.log`. Look for `Runtime hooks installed: 6 game methods`.

## Compatibility

Content mods are generally less likely to conflict than other Dialogueue/UI/camera mods. Runtime patches can coexist, but mods changing the same methods still require testing. Windows x64 is the current packaged target; Linux/Proton/macOS and controller presentation are not validated. The archived permanent-DLL installer is not the default installation method.

Local development build. Nothing has been committed, pushed or published.
