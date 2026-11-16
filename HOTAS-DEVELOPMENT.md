# Flight Control HOTAS

Start with docs/HOTAS-BUTTON-HANDOFF.md for the in-game results so far and docs/HOTAS-WORK-LOG-2026-11-15.md for the
newest work (what was built, what worked, what failed, what still needs a human). The checkout baseline is Flight Control 2.

After committing shared fixes in the sibling Flight Control 2 checkout, sync here:

    git fetch flight-control
    git merge flight-control/feature/flight-control-2

Keep HOTAS-specific work on codex/flight-control-hotas.

## The HOTAS tab

Select the FotG 2 installation, then open the **HOTAS** tab. It reads joysticks through the engine's own SDL3 library,
so it sees exactly what the game sees.

1. **Scan controllers.** Every device is listed as a chip with a live dot. Move a control and its widget lights up in the
   preview, which tells you which physical device is which. A device with new inputs (another mode) is merged in.
   **Control not listed?** listens for a control and says whether the game can see it at all.
2. **Pick an action, then press the control.** The list holds game actions (axes, weapons, targeting, throttle, energy,
   views, squad and mission) and Flight Control actions (wheels, menus, triggers, sticks). Press a button, move an axis
   (either side of an axis works as a button), or push a hat. It listens across all devices at once, ignores jittery
   axes, and assigns once the signal is clear. Clicking a control in the preview does the same without the hardware.
3. **Arrange the preview.** Drag controls into place, choose a background picture of your own device (it stays local and is
   not shared), or reset. Positions are stored as fractions of the canvas.
4. **Apply to pilot.** Close the game first. Apply warns about bindings it will remove and refuses an input that has two
   jobs. **Restore backup** puts back the pilot, settings, control table and Flight Control map from the last apply.

The working profile saves itself as `fotg-hotas-profile.json` beside the pilot data. **Open profile** and **Save profile**
exchange profiles. Numbers are shown from 1, as in Windows' game controller test; profiles keep the engine's zero-based
index.

## How the two kinds of action reach the game

- **Game actions** are written to MyMod's default control override as native `CID_JOY` bindings. Supported: buttons 0-127,
  axes 0-7 (invert supported), an axis side as a button (`AXIS_BTN`), and the first hat's four directions (`HAT`).
  Only these use the game's four joystick slots.
- **Flight Control actions** drive a virtual gamepad. The bridge reads the HOTAS through SDL3 (`setup/HotasInput.cs`),
  maps it onto gamepad buttons, triggers and sticks using `fotg-hotas-virtual.json`, and layers that over XInput. The
  existing wheels, tap/hold gestures, hyperspace chord and menu navigation run unchanged. These devices need no slot, so
  pedals or a second stick can be used here freely.

## Diagnostics

The Console exe has read-only helpers: `--hotas-scan <game folder> <out file>` lists what SDL sees,
`--render-hotas <png> [listen | live "<game folder>"]` renders the tab headlessly, and `--hotas-uitest <out file>` drives
the assign flow against stand-in controllers. `WheelBridge.exe --hotas-probe <game folder> <map json> <out file>` reads a
map once and reports the virtual gamepad it would send. `--self-test` on both exes covers the profile, table generation,
capture and virtual-pad logic.

## Not covered yet

- FotG 1.1.x (classic) has no HOTAS support: the panel and the bridge's HOTAS layer need the SDL3 engine and MyMod's
  control table.
- Native hat and action-name details come from FSO's source and have not been confirmed in FotG 2.0.2. See the work log.

## Sharing, switching and where the approaches meet

- **Sharing.** Save profile writes a copy with no device serials and no picture paths; Open profile strips both again, refuses
  files over 2 MB, and keeps your own profile as `fotg-hotas-profile.hotas.bak-<time>` (last five). A shared profile matches
  your hardware by SDL GUID (same model = same GUID). For a different model, right-click a controller chip in the HOTAS tab and
  choose one of your connected controllers; bindings and layout stay. The Profiles tab (wheel layouts, macros, button actions)
  is separate: a shared HOTAS setup that relies on button quick-press actions needs that profile too.
- **Gamepad mode** (HOTAS tab) removes the HOTAS block and the Flight Control map, puts the pilot's preset and the game's
  joystick selection back as they were before HOTAS was first applied (recorded in `fotg-hotas-state.hotas`), and keeps the HOTAS
  profile. If HOTAS was applied before the record existed, the preset falls back to FotG Classic Native or default and the
  joystick selection is left for Flight response, APPLY WITH BACKUP. Applying HOTAS again brings it straight back.
- **Where the two meet.** HOTAS game bindings use the game's joystick slots, and the gamepad's Apply uses slot 0 too. The
  gamepad's Apply now warns when HOTAS bindings are applied, and the HOTAS tab says when the slots no longer match.
  Flight Control actions alone (no game bindings) never touch the pilot preset, the control table or the joystick slots, so a
  gamepad's own axes keep working next to a HOTAS used only for wheels and buttons. The bridge layers the HOTAS over the
  gamepad, so both can be used at once.
- **Test modes.** `--hotas-flowtest <game folder> <out file>` runs apply, re-apply, gamepad mode, features-only and restore
  against a throwaway copy of the game files; the real game folder is only read.
- `--hotas-sharetest <out file>` has one player save a profile and another load it (different serials, an existing profile of
  their own), checks the saved file for personal details, refuses a set of broken or hostile files without changing anything,
  and confirms the loaded profile survives a restart.
- Shared profiles use the `.fchotas` extension (JSON inside, with a `"Format":"FlightControlHotas"` marker). Save defaults to it,
  Open lists it first and still offers earlier `.json` exports; a file with a different marker is refused.

## Proposal: encoder wheel for the command wheels (not built)

A rotary encoder usually shows up as two buttons pulsed once per detent (clockwise, counter-clockwise). Selection in a wheel is
`W.selected` from the stick angle (`sector_from_stick` in the Lua, sectors 1-8, only some filled). Plan: two new Flight Control
controls, Wheel step clockwise / counter-clockwise, active only while a wheel is open like Wheel select; the Lua moves
`selected` to the next filled sector with wrap-around and Wheel select confirms. Needs: two spare bits in the virtual gamepad
(1024 and 2048 are free), a step method in the Lua guarded so an error can never break the wheel, and pulse counting in the
bridge. The bridge polls every 25 ms, so a detent pulse of a few milliseconds can be missed; counting SDL button-down events
between ticks and replaying them as one-tick pulses with a gap tick (about 20 steps per second) fixes that. Two ordinary
buttons can stand in for an encoder when testing everything except very fast spinning.

## Flight axes: Game and Precision

The game gives every joystick axis one global dead zone and one sensitivity, plus an invert flag, and has no per-axis
calibration. **Axes…** in the HOTAS tab switches the profile's `AxisMode` between:

- **Game**: the HOTAS block binds the flight axes natively as before. The dialog measures axes and can set the global dead zone.
- **Precision**: each flight axis has `HotasAxisSettings` (dead zone, saturation, curve, calibration min/centre/max, invert). Apply clears
  the five game axis actions (CID_NONE) and writes the axes into the map file under `Flight`. The bridge shapes them
  (`HotasAxisMath` in `setup/HotasInput.cs`) and appends `|pitch|yaw|roll|throttle|mask` (thousandths, mask 1/2/4 stick axes,
  8 absolute throttle, 16 relative throttle) to the input line it already writes. The wheel script (`precision_frame`) sets
  `FLIGHT_CONTROL_LUA_STEERING` (or `FLIGHT_CONTROL_LUA_FULL` when a throttle axis is mapped) and writes `ci.Pitch`, `Heading`,
  `Bank` with the game's own signs (pitch and bank negated, bank times 1.5). With a throttle axis it takes the engine's own result
  each frame, so keys and match speed still work, and replaces it only when the lever moves (absolute) or integrates the rate
  (relative). Stale data, a dialog, a lost window, the end of a mission or any Lua fault hands control back to the engine.
- The Lua is covered by `scripts/test-precision-axes.cjs` (and the encoder step), run with the fengari harness:
  `node scripts/test-precision-axes.cjs <path to fengari>`, like the other `scripts/test-*.cjs`. The bridge reads real axes through
  `--hotas-probe`, which now also prints the flight state.
- Calibration (`AxisCalibrator` in `console/src/HotasAxisTools.cs`) measures rest (centre, wobble) and sweep (travel). A lever only sweeps.
