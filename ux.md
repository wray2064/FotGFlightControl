# UX handoff: Flight Control / Hangar Bay

## Assignment

Overhaul the **visual design and usability** of the Hangar Bay launcher and its embedded Flight Control Console. Work from the current local preview, then build and inspect the result. This is a presentation pass: preserve every working controller, game launch, mod loading, setup, backup, and configuration behavior.

The intended progression is a bold tactical-map launcher with white vector lines and clear pictograms, leading into an amber-on-black targeting-computer console. Make these feel like one product. Use original graphics and general retro cockpit cues; do not copy film frames, franchise insignia, ship silhouettes, UI artwork, or logos. Keep the project's existing Flight Control emblem unaltered; its separate terms are in `ARTWORK-LICENSE.md`.

## Baseline and location

- Repository: `C:\Appdev\Flight control\updated\FotGFlightControl`
- Branch at handoff: `vr-prep`
- Working controller/launcher baseline: commit `4100abe` (`Polish Hangar Bay and responsive controller console`). The working tree was clean when this handoff was written.
- Local installed preview: `C:\Games\Fate of the Galaxy 1.1.0\Hangar Bay.exe`. The installed game is for visual inspection; edit source in the repository.
- Build: `powershell -NoProfile -ExecutionPolicy Bypass -File console\Build.ps1`, then `powershell -NoProfile -ExecutionPolicy Bypass -File hangar\Build.ps1`. The Hangar Bay build lands at `setup\Hangar Bay.exe` and includes the console source.
- The current launcher is open on its Deploy page. A new build can be copied to the game root after closing Hangar Bay. Do not publish or push a release as part of this visual pass.

## User journey and behavior to preserve

1. **Deploy:** choose the base game or a discovered mission pack, then launch standard gamepad flight or OpenXR VR. The selected pack is layered with Flight Control; both launch modes keep the command wheels.
2. **Console / Flight Response:** choose a pilot and XInput monitor slot, inspect the live gamepad, tune sensitivity and deadzone, sample center drift, and apply native settings with a backup.
3. **Console / Command Wheel:** edit four macro slots, font, flight axes, D-pad tap/hold timing, camera hold, button actions, advanced wheel layout, and save the wheel. The macro cards are real controls, including disabled subsystem selectors when an action does not need one.
4. **Service:** inspect backups, open a selected backup, repair installation, select the active controller, and remove the tool. Keep the existing confirmations and guardrails.
5. **Guide:** show the installed `Help.html` within Hangar Bay. A missing guide should not crash the app.

The navigation, selected pilot, profile dropdown, live telemetry, save/apply buttons, status messages, keyboard focus, and error messages must remain understandable and operable. Avoid decorative controls that look actionable but have no action.

## Current visual entry points

| Source | Presentation work allowed |
| --- | --- |
| `hangar/HangarBay.cs` | `HangarBay` constructor layout/theme, `Nav`, `Button`, `Label`, `Page`, and the **layout portions** of `Home`, `Maintenance`, and `Help`; `TacticalPanel` and `VectorButton` drawing. |
| `console/src/Cockpit.cs` | `BuildCockpit`, `FitCockpit`, `ReflowFlightBody`, `ReflowMacroCards`, `ReflowNativeFooter`, shared visual helpers and styling. Preserve the wired actions. |
| `console/src/MacroDeck.cs` | Visual construction and responsive arrangement in `BuildMacroDeck`, `BuildMacroCard`, `ReflowMacroSettings`, and `ReflowMacroFooter`. Preserve action IDs, defaults, validation, save/load code, and callbacks. |
| `console/src/GamepadView.cs`, `console/src/TargetingVisual.cs`, `console/src/WindowChrome.cs` | Painting, typography, colors, visual states, and accessibility labels. Preserve controller polling, hit testing, and window behavior. |
| `Help.html` | CSS and presentation copy for the embedded guide. Preserve technical instructions and the documented controls. |

The same files contain functional code. **Do not alter** `HangarProgram`, `FindGame`, `RefreshPacks`, `Launch`, `RefreshBackups`, `OpenBackup`, `SelectController`, `Repair`, `Remove`, `CopyTree`, or any installer/update behavior. In the console, do not change native pilot/INI writes, controller detection, macro action definitions, Lua/profile serialization, wheel ordering logic, or the bridge. Leave `bridge/`, `mod/`, `setup/Installer.cs`, game files, pilot files, and the user's configuration untouched. If a functional edit appears necessary for a visual goal, document the dependency and stop at that boundary.

## Design goals

- Establish a clear hierarchy: one obvious primary launch action, an equally findable VR action, concise pack selection, and a persistent sense of the active section.
- Make the four sections feel related while giving the Console its own targeting-computer personality. Use bold vector strokes and sparse accent color; avoid glow or bloom on only one element.
- Give every clickable item distinct normal, hover, focus, selected, disabled, and dangerous-action treatment. The Remove action should look consequential without overpowering daily use.
- Keep text legible over line art and dark backgrounds. Avoid tiny status text, truncated action names, vertical letter stacking, and controls whose labels disappear at high display scaling.
- Make the gamepad preview useful for mapping: labeled controls, live stick/trigger travel, selected-control feedback, and readable explanatory text. Preserve its real input behavior.
- Treat Guide and Service as first-class screens. Backup rows should be scannable; help should wrap and have a clear reading width.
- Prefer touch-friendly targets (roughly 40 px or larger in the tested physical viewport) and enough spacing to avoid accidental taps on handhelds.

## Responsive contract

The Legion Go 2 is a poor size proxy for smaller handhelds. Inspect **window-size previews**, at the machine's current display scaling, for at least:

| Preview window | Expected behavior |
| --- | --- |
| 1920×1080-class desktop | Flight telemetry and tuning may sit side by side; macro cards are two columns with complete action names. |
| 1280×720 and 1280×800 | All screens remain usable; macro cards are two columns; compact settings and backup actions use readable rows. Vertical scrolling is acceptable. |
| 1024×700 | Macro cards become one column; each card shows HUD label, action, and subsystem. All save, apply, restore, and service actions can be reached by vertical scrolling. |

At every size: no horizontal scrolling, clipped controls, overlapping labels, or unreachable primary actions. Check the top **and bottom** of scrollable pages. Verify resizing across breakpoints in both directions; the console already has explicit reflow in `FitCockpit`. This is a window-size/DPI proxy, not a claim of physical GPD Win or ROG Ally hardware testing.

## Verification and deliverables

1. Build the console and Hangar Bay. Run `git diff --check` and confirm the working tree contains only intended visual/documentation files.
2. Inspect Deploy, both Console tabs, Service, and Guide at the viewport sizes above. For Console, inspect every macro card and the lower Save/Backup controls. Check hover, focus, disabled selectors, and scroll behavior. Do not press Save, Apply, Repair, Remove, or launch the game while doing visual inspection.
3. Keep or update screenshots only after viewing the actual final app. Existing `docs/screenshots/` images show prior UI states and are reference material, not approval of the new design.
4. Provide a short before/after description, screenshots of the final screens at desktop and compact sizes, build results, and any remaining visual limitations. Commit the source changes locally for review. Leave public publishing for the owner after hands-on testing.

## Ready-to-use task for the UX model

> Read `ux.md` and inspect the running Hangar Bay plus the listed source files. Redesign only the launcher and console presentation into a coherent tactical-map/targeting-computer experience. Preserve every existing callback, action, file operation, game launch argument, controller mapping, and config format. Build and visually verify Deploy, Console, Service, and Guide at desktop, 1280×720, and 1024×700 windows, including scrolled content. Deliver review screenshots and a local commit; do not push or publish.
