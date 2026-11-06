# Flight Control page handoff

Use the neighboring `player-download` ZIP as the sole player download. The
`source-upload` ZIP is for a public source repository, not for installation.
The game and engine are separate downloads and are not in either archive.

## Page structure

1. Hero: Flight Control emblem, title **Flight Control for Fate of the Galaxy**,
   one-line promise, Download button, and an **Unofficial fan tool** label.
2. Requirements and setup: Windows x64, Fate of the Galaxy 1.1.0 or 1.1.1 with its FSO
   26.0.1 SDL3 engine, a native pilot created on first run, XInput gamepad.
   Show the five steps in `PAGE-COPY.md`.
3. Features: flight axes and pitch direction, D-pad command wheels and held
   cockpit look, Menu camera cycle, menu pointer, four macros, visual controller
   preview, shareable advanced wheel profiles.
4. Screenshot gallery in the order listed below. Use captions and alt text.
5. Help and trust: link to the bundled `Help.html`, `docs/INSTALL.md`,
   `docs/TESTING.md`, `LICENSE`, `ARTWORK-LICENSE.md`, `NOTICE.md`, and source.
6. Footer: By Manic Trash Panda, coded with ChatGPT; contact
   dev@manictrashpanda.dev. Include the independent fan-tool disclaimer.

## Files

- `player-download/FotG-Gamepad-Wheel-preview-*.zip`: player archive. Preserve
  its exact name and publish its SHA-256 from `SHA256SUMS.txt` in this handoff.
- `source-upload/FotG-Gamepad-Wheel-source-*.zip`: repository source snapshot.
- `screenshots/setup-final.png`: actual graphical installer, ready to repair.
- `screenshots/flight-response.jpg`: controller preview and calibration.
- `screenshots/macro-deck.jpg`: wheel, macros, and hold controls.
- `screenshots/advanced-layouts.jpg`: wheel reordering and profile sharing.
- `assets/FlightControl.png`: emblem; display only with this tool under
  `ARTWORK-LICENSE.md`. Do not offer as a standalone logo download.

## Publishing checks

- Download the player ZIP from the final URL and compare its SHA-256 with the
  value in `SHA256SUMS.txt`.
- Extract the ZIP before running `Setup.exe`; keep its folder structure intact.
- Check the page at 1280×800, 1920×1080, and portrait tablet width. Keep the
  download button and requirements visible without a wide-screen layout.
- Link the matching source commit or repository when it has been published.
  Do not show the source ZIP as the primary download.

This is a preview pending the full real-controller mission checklist. Do not
call it a stable release until that checklist is completed.
