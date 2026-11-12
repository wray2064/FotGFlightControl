# Flight Control page handoff

The Hangar Bay page assets in `updated/site-builder` describe the 1.1.3
candidate. Its player ZIP contains `Hangar Bay.exe`; the older 1.1.2 ZIP does
not. **Keep the new page and download in draft until the 1.1.3 candidate has
passed installation and gameplay testing.** The game and engine remain
separate downloads.

## Page structure

1. Hero: Flight Control emblem, title **Flight Control for Fate of the Galaxy**,
   one-line promise, Download button, and an **Unofficial fan tool** label.
2. Requirements and setup: Windows x64, Fate of the Galaxy 1.1.0, 1.1.1, 1.1.2 or 1.1.3 with its FSO
   26.0.1 engine (SDL2 build, or the SDL3 package), a native pilot created on first run, XInput gamepad.
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

- `player-download/FotG-Flight-Control-1.1.3.zip`: candidate player archive.
  Preserve its exact name and publish its SHA-256 after testing.
- `source-upload/FotG-Flight-Control-source-*.zip`: source from the same commit
  as the player archive. It is for the public repository, not installation.
- `screenshots/setup-final.png`: actual graphical installer, ready to repair.
- `screenshots/hangar-deploy.png`, `hangar-console.png`, `hangar-macros.png`,
  `hangar-service.png`, `hangar-guide.png`: current Hangar Bay flow in the
  site-builder folder. Prefer these over older standalone-console images for
  the primary gallery. Captions and alt text are in `docs/PAGE-COPY.md`.
- `screenshots/flight-response.jpg`: controller preview and calibration.
- `screenshots/macro-deck.jpg`: wheel, macros, and hold controls.
- `screenshots/advanced-layouts.jpg`: wheel reordering and profile sharing.
- `screenshots/vr/`: nine cropped images from a live OpenXR VR test,
  including three wheel details. See its `README.md` for filenames, recommended
  gallery order, captions, alt text, and the desktop-mirror disclosure. Use
  `vr-cockpit-engagement.png` as the lead VR image. Label the section **VR
  preview** until the matching build is published.
- `assets/FlightControl.png`: emblem; display only with this tool under
  `ARTWORK-LICENSE.md`. Do not offer as a standalone logo download.

## Publishing checks

- Confirm the player ZIP contains `Hangar Bay.exe`, install it in a clean game
  folder, and perform the real-controller checklist before publication.
- Download the ZIP from the final URL and compare its SHA-256 with the release
  manifest before switching the site's primary Download button.
- Extract the ZIP before running `Setup.exe`; keep its folder structure intact.
- Check the page at 1280×800, 1920×1080, and portrait tablet width. Keep the
  download button and requirements visible without a wide-screen layout.
- Link the matching source commit or repository when it has been published.
  Do not show the source ZIP as the primary download.

Practice-mission testing includes handheld and external XInput controllers.
The campaign and multiplayer have not been fully validated. Keep that note on
the page, with dev@manictrashpanda.dev for bugs and ideas.
