# HOTAS work log, 15 Nov 2026 (overnight session)

Branch `codex/flight-control-hotas`, three local commits on top of 782888b (nothing pushed). Read this first when reviewing.

## What was asked

One codebase for FotG 2 gamepad and HOTAS (1.1.3 ideal, not required), as many HOTAS setups as possible including several
devices at once (Freedom 2.4 stick plus PXN pedals as the stress case). First the mechanics, then a proper UX flow inside the
Console, then this record. No other changes were made outside this checkout, and the installed game was not touched.

## What was built

**Mechanics**
- Profile v2 (`HardwareProfile`): `InputIndex` replaces `AxisIndex` (v1 files still load, v2 never writes the old name),
  binding kinds Button / Axis / Hat / AxisButton, direction, invert, a `Virtual` group for Flight Control actions, and a
  per-device preview `Layouts` list (positions as fractions, optional local background image path).
- `HotasModel`: what each group accepts, validation, conflicts (one physical input, one job) and overlap notes.
- Native apply (`HotasApply.cs`): about 80 game actions in six categories, axes with invert, relative throttle, an axis side
  as a button (`AXIS_BTN`), the first hat's four directions (`HAT`), a warning listing bindings that apply will remove, and
  a restore that puts back pilot, settings, control table and the Flight Control map (`RestoreHotas`).
- Flight Control actions from a HOTAS: `setup/HotasInput.cs` maps HOTAS inputs onto a virtual gamepad (buttons, hat
  directions, axis sides, triggers, sticks with invert), the bridge reads joysticks through the engine's SDL3 and layers the
  result over XInput. The wheels, tap/hold, hyperspace chord and menu navigation needed no change. These devices use no
  FotG joystick slot, so extra devices (pedals, a second stick) are not limited to four.
- Capture (`HotasCapture.cs`): watches every input on every device at once, ignores axes that jitter at rest, picks the
  strongest axis, treats an axis side as a button when allowed, and reports ambiguity when two axes move together.

**UX (the HOTAS tab in the Console)**
- Replaces the modal inspector. Scan, device chips with live dots, searchable action list, press-to-assign across all
  devices (or click the control in the preview), live preview (bars, circles, hat dials, 1-based numbers like Windows'
  game controller test), drag-to-arrange, user-supplied background image, "Control not listed?" probe, conflict and
  overlap messages, restore, and Apply. The working profile autosaves beside the pilot data.
- The tab hides the game/pilot panel to fit short screens unless something still needs choosing (button: Game / pilot).
  The default window is now taller (900 px design height); it is still clamped to the screen.

## What worked (evidence)

- Console `--self-test` and bridge `--self-test` pass, covering profile round trip, conflicts, native table generation
  (hat, axis side, invert, two devices, idempotent re-apply, dropped-binding warning), v1 compatibility, virtual pad
  composition (fake devices, unplug, merge with a real gamepad) and the capture logic.
- `--hotas-uitest` drives the real panel against stand-in controllers and passes: button, axis, native hat refusal of a
  diagonal then acceptance of a plain direction, a Flight Control trigger on a button, click-to-assign, conflict message,
  clear, layout reset.
- Real hardware, read only: `--hotas-scan` against `C:\Games\Fate of the Galaxy 2` found three devices: two "XInput
  Controller" entries (6 axes, 11 buttons, 1 hat each) and the Logitech Freedom 2.4 (4 axes, 10 buttons, 1 hat).
  `WheelBridge.exe --hotas-probe` opened the Freedom 2.4 by GUID through the new reader and read it (connected, axes at rest).
- Headless renders of the tab (demo and live scan) were inspected and the layout fixed from them.
- Console, Hangar Bay and bridge all compile from the lists in their own `Build.ps1` files.

## What failed or went wrong (and how it was handled)

- `Build.ps1` cannot run here: the PowerShell execution policy blocks scripts. The policy was not changed; the same `csc`
  command line was run directly. The Console, Hangar Bay and bridge exes in the working tree were rebuilt that way.
- No git identity is configured, so commits use a per-command identity ("Claude"). Rewrite the author if you care.
- My shell edits mangled backslashes in `console/Build.ps1` and `hangar/Build.ps1` (they listed `consolesrcHotasPanel.cs`).
  The second commit contains that mistake; the final commit fixes it. Everything was then rebuilt from the scripts' own lists.
- First headless render crashed (the exe needs `Assets` beside it); the ListView group headers did not draw, so sections
  are header rows instead; the Console is clamped to this screen (about 776 px high), which forced the setup-panel collapse.

## Not verified, needs you and the hardware (tomorrow's checklist)

1. **Native hats.** Written from FSO's source: button code 128+ (Down 0, Up 1, Left 2, Right 3, first hat only), flag
   `HAT`. FSO's own defaults and `hatBtnToEnum` disagree about up/down, so check each direction in game. If wrong, flip
   `NativeHatCode` in `HotasApply.cs`.
2. **Axis as button and invert.** `AXIS_BTN` and `INVERTED` flag names come from FSO's source. Not tried in FotG 2.0.2.
3. **The new action list.** Only the first ten action names are verified in game. The rest come from current FSO sources;
   FotG's engine may lack some. A wrong name should be skipped by the game, but confirm the table still loads.
4. **Flight Control actions from a HOTAS in a real flight.** Reading works (probe). The end-to-end path (map file written by
   Apply, bridge started afterwards, wheels opened from a hat) has not been run in the game. Start the game through the
   Flight Control launcher after applying so the bridge sees the map.
5. **Two devices at once.** Only the fake-device tests exercise stick plus pedals. The PXN pedals were not connected.
   Plug in both, scan, assign pedal axes to Yaw (game) and a trigger (Flight Control).
6. **Identical devices without serials** are matched by the order the system lists them. Untested on real twins.
7. **Apply and restore** were not run (they write to the game's files). Do it on a copy of the pilot first.
8. The two "XInput Controller" entries are SDL's view of the handheld gamepad; they show up as devices to bind.

## Known gaps

- FotG 1.1.3 / classic: no HOTAS support. It needs an SDL2 reader (the classic engine) and the classic control table path.
  The bridge only starts the HOTAS layer when the SDL3 engine is found.
- Restore covers the last HOTAS apply only; the older gamepad-side restore is unchanged.
- No Help.html or in-app help text for the HOTAS tab yet; `HOTAS-DEVELOPMENT.md` was updated.
- Hat directions as diagonals and extra hats work only for Flight Control actions, not game actions.
- Per-model device art and shared layouts are not done (users supply their own background).
- Apply replaces the whole HOTAS block. The warning lists what would be dropped, but there is no "import what is installed"
  yet (this is what cost the buttons in the earlier session).

## Decisions made without asking

- Virtual-gamepad approach for Flight Control actions (reuse all wheel logic instead of a second input path).
- Numbers shown from 1 everywhere in the UI; profiles keep zero-based indices.
- Assign happens automatically when the signal is clear (no confirm dialog); clear and reassign to undo.
- The HOTAS page hides the game/pilot panel when both are already set.
- Committed the previously untracked `docs/HOTAS-BUTTON-HANDOFF.md` because the development doc points to it.

## Later the same day: after in-game testing

- In-game: all axes and buttons, multi-device, wheels with Flight Control actions worked. The wheel/underlying-action clash was
  real: a game binding and a wheel role on one button cannot be gated by state (the engine has no way to pause its own
  bindings; a Lua override was tried and reverted). Fix: Flight Control actions can share inputs, and new Wheel select /
  Wheel cancel controls act only while a wheel is open; a button held through a selection is ignored in flight until released.
- Review of profile sharing, switching and conflicts (this section): background paths and serials are stripped on save and
  on load (a shared path could point at a network share), loading keeps the previous profile, controller remap added,
  Gamepad mode added with a recorded pre-HOTAS state, the gamepad's Apply warns when HOTAS is applied, a slot mismatch is
  reported, and Flight Control-only applies no longer move the pilot to the default preset.
- Found along the way: the working profile and map were first saved as `.json` in the players folder, so the Console listed
  them as pilots (fixed by renaming to `.hotas` / `.map`); Apply failed when creating a new file (fixed in the save helper).
- On this install the pilot preset is already `default` and the table has no FotG Classic Native preset, so Gamepad mode on
  this install falls back to `default` with the HOTAS block removed.

## 16 Nov 2026: axis configuration (dead zones, curves, calibration)

- The game has one global dead zone and sensitivity for all joystick axes and no calibration, so per-axis control needs Flight
  Control to process the axes. Built as an opt-in **Precision** mode (default stays Game): shared axis math, bridge shaping, game-script
  steering through the Lua control modes with a fail-safe release, apply support, and an **Axes…** dialog with live plots, sliders and a
  two-step calibration (rest, sweep) that recommends a dead zone.
- Checked: axis math and calibration unit tests, a Lua test suite in the fengari harness (it also found and confirmed the encoder
  step), table and map generation, the apply/switch-over test with precision, and the bridge reading the real Freedom 2.4's flight axes.
- Not checked in the game: steering, throttle behaviour and the feel of the curves. Things to look at first: pitch and bank directions
  and strength against Game mode, throttle (absolute and relative) including the keys, what happens in the external camera and free-look
  views, and alt-tabbing out and back.
