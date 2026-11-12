# Flight Control 1.1.4 — Campaign utilities (candidate)

- Use LB + RB together, held for 300 ms, to request hyperspace with Return.
  Individual shoulder actions remain, with a 120 ms combination decision delay.
- D-pad Right now opens one combined wheel: four macros on the cardinal slots,
  with diagonal groups for responses 1–5, responses 6–0, dock/nav, and other
  mission commands. This replaces the Y toggle and X page switch.
- Existing saved macros and four-slot layout profiles retain their actions.
- In-mission system dialogs now request visible cursor/menu input and clear
  held flight inputs. Focus/disconnect handling prevents delayed key replay.

This is an unreleased test candidate. Campaign and VR validation remains
pending. SteamVR OpenGL session initialization is still a known issue on the
reported setup; the launcher flags were verified. Meta Link is the previously
working VR path. GCW text entry and Ctrl+Enter planning forms remain unsupported.

The user confirmed controller input and the combined wheel working in a local
FotG 1.1.3 test, and specifically confirmed the shoulder escape shortcut feels
better under fire. This does not establish a complete campaign or VR test.
