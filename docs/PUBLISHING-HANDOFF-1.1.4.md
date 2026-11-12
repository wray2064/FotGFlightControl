# Flight Control 1.1.4 publishing handoff

## Status

Release candidate, not published. Source branch `feature/mission-diagonals`
contains the FotG 1.1.3 compatibility work, in-mission popup/menu exit fix,
LB+RB hyperspace gesture, and combined macro/mission wheel. The user tested
controller input and the new wheel in the local FotG 1.1.3 installation and
confirmed that the shoulder escape works under fire. Do not describe this as
a complete campaign, multiplayer, or new-build VR validation.

## Player-facing change

D-pad Right opens one wheel. Four editable macros are on the cardinal slots.
Up-left opens responses 1–5; up-right opens 6–0; down-left opens Dock / nav;
down-right opens other mission commands (F4 log, F11 fleet info, Space fleet
page). A enters a group and confirms a command. B returns to macros or closes
the wheel. Numbered responses send actual keys. Dock / confirm sends Enter;
the mission determines whether Enter docks, confirms, or engages hyperspace.
Hold LB+RB for a hyperspace request without opening the wheel. FotG still
checks jump eligibility. The old Y toggle and X mission-page switch are gone.

The popup/menu exit fix makes the cursor visible in in-mission dialogs and
releases held flight input on entry and exit. A controller selected in Hangar
Bay is pinned; if disconnected, choose the currently connected controller in
Service before launching. The local test required switching from XInput 1
to XInput 0 after disconnecting a wheel controller. This was a saved-device
selection issue, not a regression in the wheel.

## Build and site files

- `player-download/FotG-Flight-Control-1.1.4.zip`: player ZIP, including Setup,
  Hangar Bay, the bridge, Lua addon, help, source and checksums.
- `source-upload/FotG-Flight-Control-source-<commit>.zip`: matching source.
- `release-metadata/update.json`: draft manifest. Publish only after the
  player ZIP is at the final URL and its public SHA-256 matches.
- `SHA256SUMS.txt`: hashes for the handoff artifacts.
- `docs/SITE-BUILDER-HANDOFF.md`, `docs/PAGE-COPY.md`, `docs/INSTALL.md`,
  `docs/MISSION-WHEEL.md`, `docs/RELEASE-NOTES-1.1.4.md`: current copy.
- `docs/Help.html` and `docs/Console-Help.html`: bundled help for Hangar Bay
  and the console. `screenshots/hangar-*.png` and `screenshots/vr/` remain
  usable; the earlier macro screenshot shows the same four configurable
  cards but predates the diagonal mission groups. Do not caption it as a
  screenshot of the new in-game wheel.

## Release checks before publication

1. Confirm install/repair from the 1.1.4 ZIP on a clean FotG 1.1.3 setup.
2. Confirm a numbered campaign prompt, a contextual docking prompt, log/fleet
   choices, and a popup's No/Quit path. Test a macro after entering and
   leaving a mission group.
3. Confirm the combined wheel's legibility in Meta OpenXR VR with this build.
4. Publish the player ZIP, verify the downloaded file's SHA-256, then publish
   the matching source and page. Publish `update.json` last.

The game, engine and Flight Control are separate downloads. The emblem is
licensed for use with this tool only; see `ARTWORK-LICENSE.md`. Contact:
dev@manictrashpanda.dev.
