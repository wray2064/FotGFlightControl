# Changelog

## 2.0.0-preview.1

- Added FotG 2.0.2 installation through MyMod alongside the Classic installation path.
- Hangar Bay launches standard flight, OpenXR, and selected mission packs. The FotG 2 installer places it beside Knossos.
- Either stick can navigate command wheels, shields, and power.
- The embedded console has Flight Response, Wheels, Buttons, and Profiles tabs. Advanced exposes experimental axis and gesture editing.
- Profiles include flight calibration, axis mapping, buttons, macros, wheel layouts, timing, and appearance.
- Hangar Bay has an integrated title bar and the Flight Control icon.

Fixes in this build:

- Hangar Bay Repair and Remove run inside Hangar Bay and no longer copy a program to the temp folder and start it hidden. They also work on FotG 2 (MyMod) installs: Remove deletes only Flight Control's own files and keeps pilots and backups, and shows Hangar Bay.exe in File Explorer to finish.
- Every key the bridge sends now uses a scan code. On the Classic (FotG 1.1.x) SDL2 engine, LT, Menu/pause, the wheel's number keys and Enter did nothing.
- Classic players updating from Flight Control 1.1.4 or earlier keep their settings: the new R3/RB/L3/LB holds start off, RB taps stay on ship mode and the Targeting order is reset once, with a notice. See docs/UPGRADING.md.
- Hangar Bay and its Console are named consistently; the Console help no longer describes the removed Y-swap Mission wheel.

FotG 2.0.2 Standard Flight and the locally adapted Endor mission were tested during development. OpenXR, multiplayer, and later FotG releases await verification.
