# Suggested web page copy

**Publishing status:** This copy describes the 1.1.4 Hangar Bay candidate.
Keep it in draft until installation and gameplay checks pass and the matching
player ZIP, source ZIP, and checksum are published.

## Hero

**Flight Control for Fate of the Galaxy**

Fly and navigate Fate of the Galaxy with a gamepad. Flight Control adds command
wheels, menu controls, controller calibration, and configurable layouts to the
Windows release.

**Download** · **View source** · **Read installation guide**

Unofficial fan-made controller add-on. Game download sold or distributed
separately by its creators.

## Install in five steps

1. Install Fate of the Galaxy 1.1.0, 1.1.1, 1.1.2 or 1.1.3 for Windows with its FSO 26.0.1
   engine. Run it once, create a pilot, and exit.
2. Connect an XInput gamepad. Download and fully extract the Flight Control ZIP.
3. Open `Setup.exe`. It usually finds the game; otherwise choose the folder
   containing `kn_portable`.
4. Choose **Install / Repair**. Setup backs up existing pilot controls and any
   earlier Flight Control files.
5. Open **Hangar Bay.exe** from the game folder. Choose the base game or a
   mission pack, then launch Standard Flight or OpenXR VR. Console, Service,
   and Guide are available from the same app.

Close the game before installing, repairing, or changing pilot settings.

## Feature cards

**Fly your way.** Tested classic direct axes put pitch and yaw on the right
stick and roll and relative throttle on the left. An inverted pitch option is
also available. Adjust sensitivity and deadzone in the console.

**Commands within reach.** Tap the D-pad for squad, systems, targeting, or
the combined macro/mission wheel. Four macros occupy its cardinal slots;
diagonal groups reach numbered mission prompts, docking/navigation and other
mission commands. Hold a direction to look around the cockpit. Hold Menu to
cycle Cockpit, Chase, and External views. Hold LB+RB to request hyperspace.

**Navigate without a mouse.** The D-pad handles common menu selections; either
stick can steer the pointer on custom screens.

**Shape your layout.** Reorder top-level wheel choices, configure four macro
slots, and export or import shareable JSON profiles. Optional per-button hold
actions are hidden until enabled.

## Requirements and scope

Windows x64; Fate of the Galaxy 1.1.0, 1.1.1, 1.1.2 or 1.1.3 with its FSO 26.0.1 engine; XInput-compatible
controller. The game and engine are not bundled. Multiplayer and every
mission-specific menu have not yet been validated; see the test checklist.

## Gallery captions and alt text

Use current Hangar Bay captures before older standalone-console images:

- **Deploy** (`docs/screenshots/hangar-deploy.png`) — “Choose a mission pack and
  launch standard flight or OpenXR VR.” Alt: “Hangar Bay Deploy screen with
  mission profile selector and two launch options.”
- **Console** (`docs/screenshots/hangar-console.png`) — “Preview the selected
  controller and tune flight response.” Alt: “Hangar Bay Console showing pilot,
  active gamepad slot, and live controller diagram.”
- **Command Wheel** (`docs/screenshots/hangar-macros.png`) — “Configure four
  macros and reach the advanced wheel controls.” Alt: “Hangar Bay Command Wheel
  tab showing four editable macro slots; mission commands occupy the in-game
  wheel's diagonals.”
- **Service** (`docs/screenshots/hangar-service.png`) — “Inspect backups,
  repair setup, or choose a controller.” Alt: “Hangar Bay Service screen with
  backup list and maintenance actions.”
- **Guide** (`docs/screenshots/hangar-guide.png`) — “Read setup and control help
  inside Hangar Bay.” Alt: “Hangar Bay Guide showing the built-in setup steps.”

1. **Setup** — “Graphical installer recognizes the game and offers Install,
   Play, and Configure.” Alt: “Black and yellow Flight Control setup window
   showing a detected Fate of the Galaxy installation.”
2. **Flight Response** — “See both sticks live, sample drift, and tune the
   native response.” Alt: “Flight Control calibration page with a controller
   diagram and analog stick readings.”
3. **Macro Deck** — “Set four commands, wheel readability, and tap or hold
   behavior.” Alt: “Macro Deck settings with wheel choices and timing controls.”
4. **Advanced Layouts** — “Reorder wheel slots and share a profile.” Alt:
   “Advanced layout editor showing reorderable wheel entries.”

## VR preview gallery

**Command wheels in the cockpit.** Flight Control's OpenXR preview draws its
ship systems, squad, and targeting wheels in VR. These images are unaltered
crops of the desktop VR mirror during a Quest 3S test, not headset lens photos.
Use the image order, individual captions, and alt text in
`docs/screenshots/vr/README.md`. Keep this section labeled **preview** until
the matching VR build is part of the published download.

## About and footer

By Manic Trash Panda. Coded with ChatGPT. Questions:
dev@manictrashpanda.dev.

Flight Control is an independent, unofficial fan tool. It is not affiliated
with or endorsed by the Fate of the Galaxy team, FreeSpace Source Code
Project, Lucasfilm, or Disney. Original code is MIT licensed. The Flight
Control emblem has separate tool-only reuse terms.
