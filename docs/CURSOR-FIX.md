# In-mission popup cursor fix

Implemented on feature/hotas-support, 2026-11-08. No release or merge.

FSO system popups do not necessarily enter a separate game state. The wheel
previously continued publishing FLIGHT while the quit/restart dialog was open.
Use On Dialog Init/Frame/Close to publish MENU, close wheels, discard queued
flight actions, and show the cursor without grabbing it during dialog frames.
The engine's popup cursor stack restores cursor status after the dialog closes.
Dialog depth preserves menu mode until nested popups have all closed. Both
flat and VR wheel rendering/input are suspended during the popup, including
when VR simulation is paused. Existing telemetry sequence field position is
preserved; dialog depth is an additional named field.

The bridge releases held input on dialog entry/exit and waits for buttons and
triggers to be released before accepting input in the new context, preventing
held fire/confirm from clicking a choice or becoming an action on resume.

Validation completed:
- Bridge build and existing --self-test passed.
- scripts/test-dialog.cjs executes the actual Lua through Fengari with a mocked
  engine. Covers open/close telemetry, nested dialogs, cursor visibility, wheel
  and pending-action cleanup, suspended VR simulation, normal menu popups and
  campaign interface mission classification. Passed.
- git diff --check passed.
- Installed bridge and Lua copied to C:\Games\Fate of the Galaxy 1.1.0 and
  checked against source/build SHA-256. Prior files backed up outside the repo
  under C:\Appdev\Flight control\updated\cursor-fix-backups.

Run regression checks with:
  npm install --prefix <temporary-test-folder> fengari --no-audit --no-fund
  node scripts/test-dialog.cjs <temporary-test-folder>/node_modules/fengari

In-game validation still needed (not claimed by mocked tests):
1. Launch Standard Flight, enter a practice mission, open the mission-exit
   dialog. Verify visible cursor and controller pointer; select No to resume.
2. Repeat and select Yes, Quit; repeat and select Yes, Restart.
3. Open while holding fire/look or with a wheel open. Verify no accidental
   selection, stuck fire/look or wheel activation. Release before choosing.
4. Verify normal flight and menus after each transition. Repeat through the
   working Meta OpenXR path; SteamVR session initialization is separate.

Authoritative FSO source inspected for hooks and cursor stack semantics:
https://github.com/scp-fs2open/fs2open.github.com/blob/release_26_0_0/code/popup/popup.cpp
https://github.com/scp-fs2open/fs2open.github.com/blob/release_26_0_0/code/scripting/global_hooks.cpp
https://github.com/scp-fs2open/fs2open.github.com/blob/release_26_0_0/code/scripting/api/libs/controls.cpp
