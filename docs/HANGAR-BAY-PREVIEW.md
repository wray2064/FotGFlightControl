# Hangar Bay preview

Open `Hangar Bay.exe` in the Fate of the Galaxy game folder. This is a local
preview for review before the next public release; the published 1.1.2 download
is unchanged.

## Pages

- **Deploy** launches Flight Control in flat-screen or VR mode. The mission
  pack selector lists installed Knossos mods whose `mod.json` declares
  `parent: FotG_R`. The selected pack's data folders load after Flight Control
  and before the FotG base game. This preview does not yet resolve arbitrary
  extra dependencies of a pack; test a pack's content before publishing.
- **Console** hosts the existing targeting-computer configuration UI.
  Its XInput slot is a live monitor. Selecting a different monitor slot alone
  does not change the FSO joystick.
- **Maintenance** lists installer and pilot/settings backups, opens a selected
  backup folder, and repairs from the installed package. **Select controller**
  asks the user to connect only the desired device, then runs repair so the
  engine's SDL library records its joystick GUID. Close the game and bridge
  before repair.
- **Field Guide** opens the included help inside Hangar Bay.

## Removal

The Remove button removes only Flight Control's installed files and launchers.
It leaves pilots, user settings, and all backups in place. Restore native pilot
settings in the Console first if you want the original bindings back. It
never deletes the game or other mission packs.

## Morning checks

1. Open Hangar Bay from the game folder. Switch among all four pages and check
   the console at your preferred handheld resolution.
2. Launch the base game in flat-screen mode and VR mode. Verify the sticks,
   menus, and wheels.
3. With an installed FotG mission pack, select it and check that its missions
   appear while Flight Control still responds. Do not use Remove during the
   launch test.
4. Review a backup and the controller selection flow before a release.
