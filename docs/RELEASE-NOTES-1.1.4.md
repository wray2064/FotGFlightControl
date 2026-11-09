# Flight Control 1.1.4 — Campaign utilities (candidate)

- Use LB + RB together, held for 300 ms, to request hyperspace with Return.
  Individual shoulder actions remain, with a 120 ms combination decision delay.
- Switch the D-pad Right wheel between Macros and Mission with Y. X changes
  Mission pages. Send numbered replies/docking requests, open the mission log
  and access fleet information without reaching for the keyboard.
- The right-wheel choice is remembered; saved macros and layouts stay intact.
- In-mission system dialogs now request visible cursor/menu input and clear
  held flight inputs. Focus/disconnect handling prevents delayed key replay.

This is an unreleased test candidate. Campaign and VR validation remains
pending. SteamVR OpenGL session initialization is still a known issue on the
reported setup; the launcher flags were verified. Meta Link is the previously
working VR path. GCW text entry and Ctrl+Enter planning forms remain unsupported.
