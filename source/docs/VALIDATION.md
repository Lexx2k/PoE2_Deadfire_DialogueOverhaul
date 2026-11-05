# Runtime validation

Completed before first runtime launch:

- Interface and BepInEx/Harmony bridge compile against this game's Unity libraries and the pinned loader core.
- 66 existing formatting, inline metadata, dispatch, layout and camera tests pass.
- 9 runtime bridge/transpiler checks pass, including both camera calls, branch-label preservation, and rejection of unexpected call-site counts.
- Payload hashes and the bootstrap entry-point path are checked for the manual-copy layout. The runtime payload contains no replacement game assembly.
- The executable's PE machine is x64 (`0x8664`).

Pending until a real Deadfire launch with the runtime loader:

- BepInEx startup on Unity 5.6.7f1 and the game's older Mono runtime.
- Harmony patch installation and camera detours inside the game.
- Conversation opening/advancing/closing, F10 persistence, the menu toggle and tooltips.
- Compatibility with other installed mods.

An isolated .NET test is not proof of compatibility with the game's Mono runtime. The legacy build's prior visual checks do not establish runtime-loader startup compatibility.

2026-11-05: beta Unity 2019.4.41f1 startup and all six runtime hooks are confirmed in the live log; the user confirmed both the menu toggle and vertical interface. Eight additional bootstrap configuration tests pass (83 checks total). The new automatic entry-point selection handles retail and beta settings; a fresh retail branch-switch launch remains untested. See GAME_VERSION_COMPATIBILITY.md.
