# Flight Control 2 branch handoff

FotG 2.x was announced November 13, 2026 as a separate game installation. It updates FSO and permits any controller input, including triggers and sticks, to bind to any in-game control. Players must reset devices in Options. The Windows launcher adds a Play tab and a `MyMod` template for custom files, with FotG as a dependency. Saves and control files may be copied manually from 1.x.

Flight Control 1.1.5 is the final 1.x build. Its installer intentionally accepts only FotG 1.1.0–1.1.3 and expects `kn_portable/Library/FotG_R/FotG_R-<version>`, `FotG_R_settings.ini`, and FSO 26.0.1 control defaults. Do not point it at 2.x using its version override: engine, device order, native bindings, save format, and mod dependency handling require new validation.

## Installed 2.x inspection (November 13, 2026)

- Fresh Windows install: `C:\Games\Fate of the Galaxy 2`; game manifest `kn_portable/Library/FotG_2/FotG_2-2.0.2/mod.json` reports ID `FotG_2`, version `2.0.2`, type `tc`.
- Its `FG_Core` package depends on engine ID `FSO_2601SDL3`, version `26.0.1-1`. The selected build is `kn_portable/Library/bin/FSO_2601SDL3-26.0.1-1/windows-x64/fs2_open_26_0_1_sdl3_x64_SSE2.exe`. The install also contains the separate `FSO 26.0.1` package, which is not the game manifest's selected dependency.
- The control defaults are plain files at `FotG_2-2.0.2/FG_Core/data/tables/Controlconfigdefaults.tbl` (23,975 bytes; SHA-256 `24A401C05A49D47F50D6AFC079250240AF252EF59610D7178895A27EF1328753`). This table should be parsed and tested directly before generating new presets.
- The separate `kn_portable/Library/FotG_2/MyMod-1.0.0` template has ID `MyMod`, parent `FotG_2`, type `mod`, `mod_flag` entries `MyMod` and `FotG_2`, and one `core` package depending on `FotG_2 >=2.0.2` and `FSO_2601SDL3 >=26.0.1-1`. Its writable content starts at `core/data/missions` and `core/data/tables`.
- Fresh settings are under `kn_portable/HardLightProductions/FreeSpaceOpen/data/FotG_2_settings.ini`. No pilot JSON was present at inspection; pilot format and controller behavior remain untested.
- The former active 1.1.3 game installation was deleted before this inspection. The tagged Flight Control 1.1.5 source and player ZIP remain available; no live 1.1.3 install is currently present for comparison.

## First tasks on the 2.x branch

1. Run FotG 2.0.2 once, create a pilot, and record its pilot format and actual controller device mapping. The installed layout, engine dependency, control table, and MyMod metadata are recorded above.
2. Check which Flight Control features are now native. Keep wheels, context actions, menu navigation, profile sharing, and VR HUD where they still add value. Rework preset generation rather than copying 1.x device indexes.
3. Adapt Setup and Hangar Bay detection and launch command construction to the 2.x MyMod pattern. Preserve additional user mods and mission packs.
4. Validate both sticks, triggers, D-pad tap/hold, face buttons, Menu camera cycle, wheels, campaign prompts, and VR separately. Test a clean install and migrated pilot controls.
5. Build and label a Flight Control 2 preview independently. Do not change the Flight Control 1 release artifacts or update manifest until compatibility is demonstrated.
