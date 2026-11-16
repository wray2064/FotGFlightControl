# Web copy: Flight Control HOTAS preview

Ready-to-paste text for https://manictrashpanda.dev/games/FotGFlightControl/ . Wording follows the existing page (plain, first person where the page already is, no promises beyond what is tested). Items in **[brackets]** need a decision or a value from the author before publishing. Nothing here has been published.

---

## 1. Page title and meta

- **Title:** Flight Control for Fate of the Galaxy: gamepad and HOTAS controls | Manic Trash Panda
- **Meta description:** Free, open-source controller add-on for Fate of the Galaxy. Command wheels, tap/hold actions and menu navigation on a gamepad, and now a HOTAS preview: map any joystick, throttle or pedals to the game and to the wheels.

## 2. Hero paragraph (replace the opening paragraph; keep the existing first-person story paragraph below it)

More commands, fewer compromises. Flight Control is a free controller add-on for Fate of the Galaxy. It brings D-pad command wheels, tap/hold actions, gamepad-friendly menu navigation and a deeply configurable layout to the Windows game, and now a **HOTAS preview**: assign any joystick, throttle or pedals to the game's controls and to Flight Control's wheels. It all runs from Hangar Bay, one app to launch, set up and look after it.

Short line for the top of the page: **New: HOTAS support (preview) for Fate of the Galaxy 2.**

## 3. New section: "HOTAS" (place after "Command wheels", before "Default layout")

**Eyebrow:** HOTAS · PREVIEW

**Headline:** Your stick, your throttle, your pedals.

**Intro:**
The HOTAS tab finds every joystick, throttle and pedal set the game can see and lets you assign them by using them. Pick an action, move the control, and it is assigned. It works across several devices at once, so a stick and a set of pedals can fly together, and the command wheels work from them too.

**Cards** (same numbered style as "What it adds"):

01 **Press to assign.** Choose an action, then move the axis, press the button or push the hat on any device. Flight Control listens to all of them at once and ignores jittery axes. You can also click the control in the live picture.

02 **Several devices.** Stick, throttle, pedals, button box. Game controls can use up to four devices, and wheel controls can use as many as you have. A gamepad can stay connected alongside.

03 **Game controls and wheels.** Flying controls, weapons, targeting, throttle, energy, views and squad commands go straight to the game. The command wheels, menus and gamepad-style buttons run through Flight Control, so they keep their tap and hold actions.

04 **One button, different jobs.** Put fire on a button and wheel select on the same button: it fires in flight and confirms inside a wheel. Wheel cancel and the secondary trigger work the same way. Good for a Freedom 2.4 with few buttons, or for choosing without moving your fingers.

05 **Encoder wheels.** Turn a rotary encoder to step round a command wheel one entry at a time, then press to select. Two ordinary buttons work too.

06 **A picture of your gear.** Live bars, buttons and hats you can drag into place, with an optional photo of your own device behind them.

07 **Back to the gamepad in one click.** Gamepad mode removes the HOTAS bindings and restores how the game was set up before. Your HOTAS profile is kept for next time.

08 **Careful with your files.** Everything is backed up before Apply, with a one-click restore. Shared profiles carry no serial numbers and no file paths from your PC.

**Screenshots to use** (all in `screenshots/`, taken from the real Console with demo controllers):

| File | Suggested placement | Alt text |
|---|---|---|
| 01-hotas-tab-overview.png | Hero image for the section | The HOTAS tab in the Flight Control Console, showing a joystick with its axes, hat and buttons labelled with the actions assigned to them |
| 02-listening-for-a-control.png | Beside card 01 | The Console asking the player to press or move a control to assign it to Pitch |
| 03-wheel-roles.png | Beside card 04 | The Wheel select, cancel and step actions assigned to buttons on a joystick |
| 04-second-controller.png | Beside card 02 | A pair of pedals selected as a second controller, with an axis assigned to yaw |
| 05-game-action-on-a-hat.png | Optional | A game action assigned to a hat direction |
| 06-arrange-preview.png | Beside card 06 | The preview in arrange mode, ready to drag controls into place |
| 07-flight-meaning-of-a-button.png | Optional | A gamepad-style button showing what it does in flight |
| 08-hotas-switched-off-in-simple.png | Optional, for the setup steps | The Console in Simple mode: the HOTAS tab is greyed out until Controls is set to Advanced |

## 4. "Tested so far" paragraph (replace the HOTAS part of the existing tested-so-far text)

HOTAS support is a preview. It has been flown with a Logitech Freedom 2.4 and a handheld gamepad on Fate of the Galaxy 2.0.2: joystick axes and buttons as game controls, several devices at once, and the command wheels driven from the joystick. Hat directions as game controls, axes used as buttons, inverted axes, rotary encoders and some of the extra game actions have not been tried on many devices yet. Fate of the Galaxy 1.1.x does not support HOTAS yet. If you have a HOTAS, I would love to hear how it goes: dev@manictrashpanda.dev.

## 5. How it works (for the install steps area)

1. Install Flight Control for Fate of the Galaxy 2 as usual and play once.
2. Open the Flight Control Console, set **Controls** to **Advanced**, and choose the **HOTAS** tab.
3. Move your controls to see them light up, then pick an action and use the control to assign it.
4. Close the game and press **Apply to pilot**.
5. Start the game from the Flight Control launcher.

Full guide: **[link to HOTAS-Manual page or PDF]**.

## 6. Download area

- **[Decide: ship as 1.3.0 "HOTAS preview" in the normal ZIP, or as a separate preview ZIP.]**
- Version line suggestion: VERSION **[1.3.0-preview]** · **[date]** · Flight Control · HOTAS preview
- File: **[FotG-Flight-Control-1.3.0-preview.zip]** · size **[ ]** · SHA-256 **[ ]** · source github.com/wray2064/FotGFlightControl · MIT for the code.
- A preview-only note: "The HOTAS preview changes the Console, the launcher and the wheel script. Setup backs up your pilot before it changes anything, and the HOTAS tab backs up again before each Apply."
- Keep the existing security paragraph. The new programs are not code-signed, same as before.

## 7. Replace the existing "Own a HOTAS?" boxes

**Current text to replace:** "Flight Control is gamepad-only today. If you fly with a stick and throttle, share an anonymous hardware profile…" and the "HOTAS Flight Control project · experimental" block.

**New text:**

> **HOTAS preview.** Flight Control now works with a joystick, throttle and pedals on Fate of the Galaxy 2. It is a preview: please try it and tell me what your hardware does. Your saved `.fchotas` profile contains no serial numbers and no file paths, so it is safe to attach to a report.
> [Read the HOTAS guide] [Send a report: dev@manictrashpanda.dev]

Keep the link to the HOTAS project page if it stays online, reworded as "Share a profile for your device".

## 8. FAQ block (new)

**Which games and versions?** Fate of the Galaxy 2.0.2 through Flight Control. Fate of the Galaxy 1.1.x is not supported for HOTAS yet.

**Which devices?** Any joystick, throttle, pedals or button box that the game's own engine can see. If the game cannot see a control, Flight Control cannot either, and the Console tells you.

**Can I keep my gamepad?** Yes. Flight Control actions can run from both at once, and Gamepad mode switches the game's own bindings back in one click.

**Does a button really fire and confirm?** Yes. Bind one button to the Right trigger and to Wheel select. In flight it fires, in a wheel it confirms and does not fire.

**Will it overwrite my settings?** Apply backs up the pilot, the game's settings and the control table first, and Restore backup puts them back. Applying only wheel actions never touches your gamepad's flight setup.

**Can I share my setup?** Yes. Save profile writes a `.fchotas` file. It has no serial numbers and no paths. Someone with a different device can pick one of theirs to take its place.

**Is it safe?** The code is open source and the downloads are checked against their SHA-256. The programs are not code-signed yet.

## 9. Changelog entry (draft, goes above 1.2.0)

**[1.3.0-preview]** · **[date]** · Release notes

PREVIEW

- **HOTAS tab.** Find your controllers, assign by pressing or moving a control, live preview you can arrange, several devices at once.
- **Game controls from a HOTAS.** Axes, buttons, axis sides as buttons, inverted axes and the first hat's four directions, with about 80 game actions.
- **Wheels from a HOTAS.** Wheel D-pad directions, select, cancel and the gamepad-style buttons from any device, with their tap and hold actions.
- **One button, different jobs.** Wheel select and cancel act only while a wheel is open; triggers pause while a wheel is open.
- **Encoder wheels.** Step clockwise and anticlockwise round an open wheel.
- **Gamepad mode.** Switch the game back to the gamepad and return to HOTAS without losing your profile.
- **Profiles.** `.fchotas` files without serial numbers or file paths; a device can be remapped to your own.
- **Safer Apply.** Backups before every change, a restore button, a warning before anything is removed, and a warning when applying gamepad settings would redirect HOTAS devices.
- Not yet: Fate of the Galaxy 1.1.x, hats beyond the first for game actions, diagonal hat directions for game actions.

## 10. Social or newsletter blurb

Flight Control now has a HOTAS preview for Fate of the Galaxy 2: assign any joystick, throttle or pedals by using them, run the command wheels from the stick, and put fire and wheel-select on the same button. It is free and open source, and I need people with real hardware to try it. [link]

## 11. Notes for the web builder

- Keep the existing look: yellow on black, monospaced labels, numbered cards, "UNOFFICIAL FAN TOOL / FREE · OPEN SOURCE" tags.
- The screenshots are 1140 by 776 PNGs from the real Console with demo controllers (Logitech Freedom 2.4 and PXN pedals). They contain no personal data. Provide 2x or crops if the layout needs them.
- Do not state that HOTAS is "fully supported" or "tested on many devices". Use "preview" wherever HOTAS is named in a headline.
- The version number, date, ZIP name, size and SHA-256 are placeholders until the release is packaged. Run the normal packaging, then fill the checksum.
- The manual is provided as Markdown and as a standalone HTML page (`manual/`). Either can be hosted as the "HOTAS guide".
