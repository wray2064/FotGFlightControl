# Fate of the Galaxy 1.1.3 compatibility check

Checked on 2026-11-11 against the local Knossos-installed `FotG_R-1.1.3`.
The manifest declares FSO 26.0.1. The release uses a merged `data` folder and
contains `data/tables/Controlconfigdefaults.tbl`.

| Check | Result |
| --- | --- |
| Generate Classic direct and inverted control presets from the 1.1.3 table | Passed; both presets generated without a layout error. |
| Isolated install with an explicit 1.1.3 override on the previous build | Passed. This established that the content layout was usable before changing the supported list. |
| Normal isolated install after adding 1.1.3 to the installer, console, and bridge version lists | Passed without an override; generated the wheel mod, launchers, and axis record. |
| Engine selection on the local SDL3 package | Passed; selected FSO 26.0.1 SDL3 SSE2. |
| Engine selection with only the clean-install SDL2 package present | Passed; selected FSO 26.0.1 SDL2 AVX. |
| Launch the isolated SDL3 game with Flight Control and a copied pilot | Passed; pilot selection and the main menu rendered without a startup warning. The game was closed after the check. |
| In-flight axes, triggers, a D-pad wheel, and menus in a practice mission | Passed in the user's physical test after selecting the connected controller. Bridge diagnostics showed the Classic Native preset applied, XInput 0 active, stick movement, and a wheel tap. |
| Additional missions, other controllers, and VR in 1.1.3 | Not yet tested. |

The compatibility tests used isolated folders under `dist/` with junctions to
the installed game data and engine. They did not alter the real game's pilot
files, settings, or installed Flight Control files. The first physical test
appeared to have no input because its copied preferences
were pinned to disconnected XInput slot 1. Windows reported the connected pad
at XInput 0. Changing the isolated copy to slot 0 and running Install / Repair
rematched the FSO joystick; the subsequent practice test passed. This was a
test-profile selection issue, not a 1.1.3 control-table failure.

Core practice-mission compatibility is confirmed. Broader mission and VR checks
remain for a later release review. No public upload was made.
