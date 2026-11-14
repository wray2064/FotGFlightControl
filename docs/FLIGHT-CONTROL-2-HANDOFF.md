# Flight Control 2 branch handoff

FotG 2.x was announced November 13, 2026 as a separate game installation. It updates FSO and permits any controller input, including triggers and sticks, to bind to any in-game control. Players must reset devices in Options. The Windows launcher adds a Play tab and a `MyMod` template for custom files, with FotG as a dependency. Saves and control files may be copied manually from 1.x.

Flight Control 1.1.5 is the final 1.x build. Its installer intentionally accepts only FotG 1.1.0–1.1.3 and expects `kn_portable/Library/FotG_R/FotG_R-<version>`, `FotG_R_settings.ini`, and FSO 26.0.1 control defaults. Do not point it at 2.x using its version override: engine, device order, native bindings, save format, and mod dependency handling require new validation.

## First tasks on the 2.x branch

1. Download and install FotG 2.x alongside the retained 1.1.3 install. Record its actual folder names, manifest IDs, FSO version, control table, pilot format, and MyMod dependency layout.
2. Check which Flight Control features are now native. Keep wheels, context actions, menu navigation, profile sharing, and VR HUD where they still add value. Rework preset generation rather than copying 1.x device indexes.
3. Adapt Setup and Hangar Bay detection and launch command construction to the 2.x MyMod pattern. Preserve additional user mods and mission packs.
4. Validate both sticks, triggers, D-pad tap/hold, face buttons, Menu camera cycle, wheels, campaign prompts, and VR separately. Test a clean install and migrated pilot controls.
5. Build and label a Flight Control 2 preview independently. Do not change the Flight Control 1 release artifacts or update manifest until compatibility is demonstrated.
