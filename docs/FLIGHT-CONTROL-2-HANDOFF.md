# Flight Control 2 branch handoff

Flight Control 2 workspace: `C:\Appdev\Flight control 2`, a separate checkout of `feature/flight-control-2`. Repository remote: `https://github.com/wray2064/FotGFlightControl`. The original 1.x checkout remains at `C:\Appdev\Flight control\updated\FotGFlightControl`; tag `v1.1.5` anchors its final source. Build outputs under `dist/` are generated and ignored by Git. The final 1.x ZIPs remain at `C:\Appdev\Flight control\updated\final-1.1.5`, outside both working trees. This 2.x branch is currently local and has not been pushed.

FotG 2.x was announced November 13, 2026 as a separate game installation. It updates FSO and permits any controller input, including triggers and sticks, to bind to any in-game control. Players must reset devices in Options. The Windows launcher adds a Play tab and a `MyMod` template for custom files, with FotG as a dependency. Saves and control files may be copied manually from 1.x.

Flight Control 1.1.5 is the final 1.x build. Its installer intentionally accepts only FotG 1.1.0–1.1.3 and expects `kn_portable/Library/FotG_R/FotG_R-<version>`, `FotG_R_settings.ini`, and FSO 26.0.1 control defaults. Do not point it at 2.x using its version override: engine, device order, native bindings, save format, and mod dependency handling require new validation.

## Installed 2.x inspection (November 13, 2026)

- Fresh Windows install: `C:\Games\Fate of the Galaxy 2`; game manifest `kn_portable/Library/FotG_2/FotG_2-2.0.2/mod.json` reports ID `FotG_2`, version `2.0.2`, type `tc`.
- Its `FG_Core` package depends on engine ID `FSO_2601SDL3`, version `26.0.1-1`. The selected build is `kn_portable/Library/bin/FSO_2601SDL3-26.0.1-1/windows-x64/fs2_open_26_0_1_sdl3_x64_SSE2.exe`. The install also contains the separate `FSO 26.0.1` package, which is not the game manifest's selected dependency.
- The control defaults are plain files at `FotG_2-2.0.2/FG_Core/data/tables/Controlconfigdefaults.tbl` (23,975 bytes; SHA-256 `24A401C05A49D47F50D6AFC079250240AF252EF59610D7178895A27EF1328753`). This table should be parsed and tested directly before generating new presets.
- The separate `kn_portable/Library/FotG_2/MyMod-1.0.0` template has ID `MyMod`, parent `FotG_2`, type `mod`, `mod_flag` entries `MyMod` and `FotG_2`, and one `core` package depending on `FotG_2 >=2.0.2` and `FSO_2601SDL3 >=26.0.1-1`. Its writable content starts at `core/data/missions` and `core/data/tables`.
- Fresh settings are under `kn_portable/HardLightProductions/FreeSpaceOpen/data/FotG_2_settings.ini`. No pilot JSON was present at inspection; pilot format and controller behavior remain untested.
- A fresh classic 1.1.3 installation is now at `C:\Games\Fate of the Galaxy Classic` with Flight Control 1.1.5 installed for the Issur pilot. The final Flight Control 1 player and source ZIPs are outside the repository at `C:\Appdev\Flight control\updated\final-1.1.5`; Git tag `v1.1.5` anchors the source. This install is the 1.x comparison baseline. Its fresh setup selected FSO 26.0.1 SDL2 AVX.
- The FotG team's public GitHub organization exposes its website and FSO engine fork, but no separate 2.x game-source repository or engine release tag was found during inspection. Use the installed 2.0.2 files as the immediate compatibility reference and compare engine behavior with the public FSO fork where needed.

## First tasks on the 2.x branch

1. Run FotG 2.0.2 once, create a pilot, and record its pilot format and actual controller device mapping. The installed layout, engine dependency, control table, and MyMod metadata are recorded above. Keep the classic 1.1.3 install separate for behavior comparisons.
2. Check which Flight Control features are now native. Keep wheels, context actions, menu navigation, profile sharing, and VR HUD where they still add value. Rework preset generation rather than copying 1.x device indexes.
3. Adapt Setup and Hangar Bay detection and launch command construction to the 2.x MyMod pattern. Preserve additional user mods and mission packs.
4. Validate both sticks, triggers, D-pad tap/hold, face buttons, Menu camera cycle, wheels, campaign prompts, and VR separately. Test a clean install and migrated pilot controls.
5. Build and label a Flight Control 2 preview independently. Do not change the Flight Control 1 release artifacts or update manifest until compatibility is demonstrated.

## 2.0.0-preview.1 release handoff

See INSTALL-FLIGHT-CONTROL-2.md, CHANGELOG.md and SITE-BUILDER-HANDOFF-2.0.0-PREVIEW.1.md. Package.ps1 builds the preview. Setup installs a root Hangar Bay executable plus its support folder and stages the overlay into MyMod. Game and engine paths are specific to verified FotG 2.0.2. The stable update feed stays on Classic.
