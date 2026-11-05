# Runtime architecture

The interface assembly is unchanged in architecture: it reads native dialogue text and invokes native selection/Continue handlers. `runtime/RuntimePlugin.cs` supplies a BepInEx entry point and Harmony patches for the six existing game methods previously edited on disk.

| Game method | Runtime integration |
| --- | --- |
| UIConversationManager.Show | Postfix begins/observes the conversation |
| UIConversationManager.CheckRebuildContent | Prefix restores native visibility before pooling/rebuild; postfix captures content; transpiler adjusts only its two camera-offset requests |
| UIConversationManager.OnWillAdvanceToPlayerResponse | Postfix retains the chosen reply |
| UIConversationManager.OnWillContinue | Postfix retires current choices |
| UIConversationManager.Hide | Postfix observes the existing result without replacing it |
| UIOptionsManager.Show | Postfix attaches the native-style preference control |

Every target signature is resolved before patching starts. Initialization failure unpatches only this plugin's Harmony ID. The camera transpiler requires exactly two matching call sites and preserves existing instruction objects/labels. No global camera getter is patched. Other Harmony mods are not unpatched by our cleanup.

The plugin rejects an assembly referencing the old helper directly, preventing double hooks. Our old prototype needs a one-time restoration of its verified original backup before copying the runtime files. The runtime mod contains no assembly-writing installer or patcher; compatibility with unrelated game modifications depends on their method layouts.

The bootstrap points to `PillarsOfEternityII_Data/DialogOverhaul/BepInEx/core/BepInEx.Preloader.dll`. BepInEx derives its root from that entry-point path, so core/plugins/config/cache/logs stay inside the data directory. Users with a compatible existing standard root-level loader can copy only the runtime plugin and interface DLL into that setup without replacing its bootstrap.

Upstream references:

- https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2
- https://github.com/BepInEx/BepInEx/blob/v5.4.23.2/BepInEx.Preloader/Entrypoint.cs
- https://github.com/BepInEx/BepInEx/blob/v5.4.23.2/BepInEx/Paths.cs
- https://docs.bepinex.dev/articles/user_guide/installation/index.html

Original release ZIP SHA256: `F752CE4E838F4C305B9DA1404B6745F2CFF23B8BFD494F79F0C84D0A01F59B46`.
The manifest records individual redistributed payload hashes. Bootstrap configuration is the only modified upstream loader file (entry-point path relocated). Game artwork/fonts/assemblies are read from the installed game rather than included as source dependencies.

2026-11-05 update: Doorstop now uses source/loader/version.dll and invokes Deadfire.DialogOverhaul.Bootstrap.dll first. That shared .NET 2-compatible bootstrap selects the retail or beta Unity entry-point module before delegating to BepInEx.Preloader.dll. See GAME_VERSION_COMPATIBILITY.md.
