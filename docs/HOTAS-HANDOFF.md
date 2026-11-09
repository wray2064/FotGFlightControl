# HOTAS support branch handoff

Campaign follow-up (2026-11-08): current utility work is now on
`feature/campaign-utilities`; read `CAMPAIGN-UTILITIES-HANDOFF.md` for the
1.1.4 candidate. This document remains the future HOTAS implementation plan.

## Branch and baseline

- Work on `feature/hotas-support`, forked from `vr-prep` at `a8d9892` (the
  Flight Control 1.1.3 Hangar Bay candidate). Keep the player candidate and
  website handoff on the parent branch unchanged while this work is explored.
- This is an investigation and implementation branch. Do not release a HOTAS
  ZIP or merge until actual stick-and-throttle testing succeeds.
- Keep XInput gamepad behavior, standard flight, VR, Hangar Bay, and existing
  JSON profiles working throughout. Gamepad selection must remain explicit.

## What the current code does

| Path | Current implementation | HOTAS consequence |
| --- | --- | --- |
| Native flight axes | `setup/Installer.cs` generates FSO control presets. `console/src/ControllerDevices.cs` enumerates SDL devices and matches them to an XInput slot. | A non-XInput stick may already appear in FSO, but the generated four-axis map and single selected joystick assume one gamepad. |
| Wheel, face controls, and menus | `bridge/WheelBridge.cs` polls `XInputGetState` for one slot. It writes buttons and stick values to the Lua bridge file, and sends keyboard/mouse input for menus, look, camera, and weapons. | A HOTAS needs a device-agnostic input source and bindings for hats, buttons, triggers, and axes. |
| Lua wheels | `mod/FotG_Gamepad_Wheel/data/tables/lua/zzz_fotg_wheel-sct.lua` parses the bridge's numeric button/stick protocol and handles wheels and actions. | Preserve the protocol initially if possible; normalize HOTAS input in the bridge rather than rewriting the wheel logic. |
| Configuration | `console/src/ControllerSetup.cs`, `ControllerDevices.cs`, `GamepadView.cs`, `ButtonsAndAxes.cs`, `MacroDeck.cs`, and `LayoutProfiles.cs` expose gamepad settings. | Add an explicit HOTAS profile and device preview. Do not present a gamepad diagram as a HOTAS calibration screen. |
| Launch and install | `hangar/HangarBay.cs`, `setup/Installer.cs`, and `Package.ps1` package and launch the same mod. | Hangar Bay should select a HOTAS profile without changing the installed gamepad profile. Backups must cover both. |

## First investigation

1. Record each test device's name, vendor/product ID, SDL GUID, detected axes,
   buttons, hats, and whether it appears as one or multiple devices. Test a
   standalone stick and a separate throttle if available. Do not assume that
   DirectInput axis order is consistent across hardware.
2. Check FSO 26.0.1's actual control binding support for separate joystick
   devices, absolute throttle, hats, and simultaneous keyboard/controller
   inputs. Inspect its control table and behavior in a practice mission before
   changing `Presets.FromTable`.
3. Decide whether to read HOTAS controls through the engine's SDL2/SDL3 DLL or
   another Windows input API. Prefer the engine's own SDL mapping for identity
   and axis numbering. Keep SDL2 and SDL3 enumeration paths explicit.
4. Test how device IDs and GUIDs behave across unplug/replug, USB port changes,
   reboot, and two devices with the same model name. Bind by stable identity
   when available and show a clear reconnection state when it is not.

## Implementation direction

- Introduce an input-device abstraction in the bridge with XInput and HOTAS
  backends. Normalize wheel direction, confirm/cancel, menu navigation, fire,
  look, and macro actions before publishing to Lua. Keep one active input
  profile; do not silently fall back to a different device after disconnect.
- Support a paired stick and throttle as separate physical devices. Allow the
  user to assign pitch, yaw, roll, and either absolute or relative throttle,
  with inversion, deadzone, and a live raw-value preview. Avoid assuming that a
  throttle axis centers like a stick or that a hat reports as a D-pad button.
- Start with an opt-in HOTAS preset. Preserve the tested XInput Classic direct
  and inverted presets byte-for-byte where feasible. If the game's native
  bindings cannot span two devices, document the limitation and use a bridge
  approach only after measuring its latency and safety under focus loss.
- Expose bindings by physical control name and device. Provide defaults only
  for observed hardware; allow remapping for other devices. Keep all wheel
  actions available even if the hardware has fewer buttons.
- Share HOTAS profiles as versioned JSON with device identity and mappings.
  Import must validate unknown actions, missing devices, axis ranges, and
  duplicate assignments without modifying active settings on failure.
- Retain the bridge's stale-input heartbeat and release held keys/mouse buttons
  on focus loss, disconnect, game exit, or backend error.

## Tests required before a merge proposal

1. On the existing Legion Go 2 and an external XInput pad, run the current
   gamepad checklist (`docs/TESTING.md`) on SDL2 and SDL3 game installs.
2. With a real HOTAS, verify every assigned flight axis in a practice mission,
   including center and end travel, inversion, deadzone, absolute throttle,
   and simultaneous stick-plus-throttle movement.
3. Verify menu control, all four wheels, selection feedback, fire groups,
   targeting, macros, cockpit look, camera cycle, and focus/disconnect cleanup.
4. Repeat in OpenXR VR and with an additional mission pack selected in Hangar
   Bay. Check that the chosen device remains chosen after restart and repair.
5. Exercise backups, restore, profile export/import, and removal. Test with a
   missing device and with two same-model devices. Record exact hardware and
   SDL versions in the test notes.
6. Rebuild the package, run Setup and Console self-tests, verify the player ZIP
   contents and checksum, and review the public help before proposing a merge.

## Merge gate

Merge back only after real hardware testing demonstrates that HOTAS and XInput
can coexist without changing the default gamepad experience. Include the
tested hardware list, known limits, migration behavior, and any new player
instructions in the merge proposal. Version and publish a new player ZIP only
after that review.
