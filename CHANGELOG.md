# Changelog

## 1.2.3 HOTAS (experimental)

An experimental branch of 1.2.0 with a HOTAS tab, for Fate of the Galaxy **2.0.2 only**. It does not support FotG 1.1.3 or earlier. It is published separately from the regular 1.2.0 download while players help define what the HOTAS support should do before it is merged into the main release.

- **HOTAS tab.** Scan your controllers, pick an action and press or move the control to assign it. A live preview you can arrange, with an optional picture of your own device. Several devices work at once.
- **Game controls and wheels from a HOTAS.** Flying controls, weapons, targeting, throttle, energy, views and squad commands go to the game; wheels, menus and gamepad-style buttons run through Flight Control with their tap and hold actions.
- **One button, different jobs.** Wheel select and cancel act only while a wheel is open, so one button can fire in flight and confirm in a wheel.
- **Encoder wheels.** Step clockwise and anticlockwise round an open wheel.
- **Axis settings.** Flight axes dialog with live plots, calibration, dead zone, saturation and curve per axis (Precision mode, experimental), or the game's own handling.
- **Gamepad mode and Restore backup.** Switch back to the gamepad in one click; everything is backed up before each Apply.
- **Shared profiles.** `.fchotas` files with no serial numbers or file paths; a device can be remapped to your own.
- **Full Knossos installs.** Setup, Hangar Bay, the Console and the bridge now work with a full (installed) Knossos as well as portable Knossos. Choose your Knossos library folder (the one that holds `FotG_2` and `bin`, by default `Documents\Freespace2-mods`); pilots and settings are read from `%APPDATA%\HardLightProductions\FreeSpaceOpen`. If you pick the Knossos app folder, Setup now says which folder to choose.
- **`Setup.exe --install` prints its output** in the command prompt it was started from, and shows a message when it fails and no prompt is attached.
- **MyMod is created for you** when FotG 2.0.2 is installed without one (a vanilla Knossos cannot make it), and Fate of the Galaxy 2 installed by a vanilla Knossos (data merged in one folder) is found. Setup also finds your library from a custom launcher's Knossos settings.
- **Diagnostic flight.** Hangar Bay has a new Diagnose page. It starts the game as usual and records what your devices report, what Flight Control sends and what the game does with it (including the throttle the ship really has). F8 marks the lever fully forward, F7 fully back, F9 a problem. When you quit, one ZIP report is built for the developer, without serial numbers or user names.
- Updates for this build come from the HOTAS page, so the regular 1.2.0 download is never offered as an update.

Everything in 1.2.0 below is included.

## 1.2.0

One release for Fate of the Galaxy 2.0.2 and the final classic FotG 1.1.3. Changes since 1.1.4:

- **New button bindings.** New hold actions for R3 (mouse movement), RB (ship mode), L3 (link guns) and LB (link torpedoes), plus quick and held actions for weapon banks, ship modes, gun linking, torpedo linking and mouse control. These cover what the Excoriator training mission and the campaign interface need.
- **Reorganized Hangar Bay Console.** Flight Response, Wheels, Buttons and Profiles tabs, with Advanced for custom axes and gestures. The Hangar Bay has its own title bar and the Flight Control icon.
- **Updated wheel order.** The Targeting wheel changed (slot 6 is now Targeting computer) and the Systems, Macros and Squad wheels were reordered. The Systems wheel opens Manual power (Y) and Shield allocation (X), and the Macros wheel carries the mission-command groups.
- **Hyperspace on both shoulders.** Hold LB and RB together for 300 ms to request hyperspace. Each shoulder keeps its own action.
- **Targeting computer toggle and Next subsystem** on the Targeting wheel.
- **Campaign bindings.** Mission responses 1-5 and 6-0, Dock / nav (Enter, next nav point, autopilot), mission log (F4), fleet info (F11) and fleet page (Space) are on the Macros wheel.
- **FotG 2.0.2 support.** Setup installs through MyMod alongside the classic installation.
- **Help.** Illustrated wheels, plus Manual power and Shield allocation.

Updating from 1.1.4 or earlier on FotG 1.1.x keeps your settings: the new holds start off, RB tap stays on ship mode, and the Targeting order is reset once with a notice. See docs/UPGRADING.md. A fresh install gets the new defaults.

VR ready: Flight Control's own VR mode works with the gamepad wheels. This is separate from the game's built-in VR support. Works with FotG 2.0.2 and FotG 1.1.3. Multiplayer and FotG releases after 2.0.2 are not verified.
