# Flight Control for Fate of the Galaxy

**Version 1.0.0.** Gamepad command wheels, menu navigation, controller calibration and shareable layouts for the Windows release of Fate of the Galaxy. Internally the add-on is called **FotG Gamepad Wheel**.

- **Download the player build:** https://manictrashpanda.dev/games/FotGFlightControl/ (the ZIP with Setup.exe; this repository's automatic source ZIP does not contain the compiled programs)
- **Help:** https://manictrashpanda.dev/games/FotGFlightControl/help/
- **Report a problem:** [GitHub issues](https://github.com/wray2064/FotGFlightControl/issues) or dev@manictrashpanda.dev

> Flight Control runs on multiple machines, but it hasn't been tested in every campaign and mission, or in multiplayer. Questions and suggestions: dev@manictrashpanda.dev.
>
> Unofficial fan tool. Not affiliated with or endorsed by the Fate of the Galaxy team, the FreeSpace Source Code Project, Lucasfilm or Disney. The game and engine are separate downloads and are not included here.

By **Manic Trash Panda**, coded with ChatGPT. Questions: **dev@manictrashpanda.dev**. See [About.html](About.html) for project and platform attributions.

The original add-on code, companion programs and documentation are released under the [MIT License](LICENSE). The original emblem may be used only with this tool under the [artwork terms](ARTWORK-LICENSE.md). The game and engine are separate downloads.

Contributions are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md). For source builds and release packaging, see [docs/RELEASE.md](docs/RELEASE.md).

An independent, additive controller mod for **Fate of the Galaxy 1.1.0 or 1.1.1** on Windows with the bundled **FSO 26.0.1 SDL3** engine. The Lua addon draws four in-flight panels; a small XInput bridge supplies raw D-pad and right-stick input and adds menu navigation. The external Flight Control Console configures wheel layouts and macros, and backs up native pilot settings.

![Setup detects the installed game](docs/screenshots/setup-final.png)

The [Flight Response](docs/screenshots/flight-response.jpg), [Macro Deck](docs/screenshots/macro-deck.jpg), and [Advanced Layouts](docs/screenshots/advanced-layouts.jpg) screenshots show the controller preview and configuration pages.

## Install

1. Install Fate of the Galaxy 1.1.0 or 1.1.1 with its bundled FSO 26.0.1 SDL3 engine. Run the unmodified game once and create a pilot, then exit. Connect the gamepad before running Setup.
2. Extract the entire ZIP. Close the game and any older FotG controller companion.
3. Double-click **Setup.exe**. It finds common game locations; otherwise choose the folder containing `kn_portable` with **Browse game**. Click **Install / Repair**.
4. Use **Play with Wheel** in Setup, or **Play with Gamepad Wheel.exe** in the game folder. This is the gamepad launch entry; the original **Play Fate of the Galaxy.cmd** runs without the wheel. Keep the bridge's tray icon running while playing.
5. Use **Configure** in Setup, or **Configure Gamepad Wheel.exe** in the game folder, to adjust macros, flight axes, wheel text, sensitivity and deadzone. **Advanced** reorders top-level wheel choices and imports or exports shareable JSON profiles. Choose **Use layout**, then **Save Wheel**; restart the game to load wheel changes. Squad ordering affects only the first communications page, while subsequent game menus retain their native order.

See the [step-by-step installation guide](docs/INSTALL.md) and [test checklist](docs/TESTING.md). Advanced users may still run `./Install.ps1 -GameRoot 'C:\path\to\Fate of the Galaxy 1.1.0'` in PowerShell.

The installer checks the game/engine version, copies only its own `GamepadMods/FotG_Gamepad_Wheel` directory, and creates two launchers. The wheel launcher uses a separate `GamepadMods_settings.ini` profile; installation identifies the connected controller through the game's bundled SDL3 library and selects it in that profile, backing up any prior profile. The Flight Control Console edits this active profile. The installer also backs up each existing native v4 pilot before reserving A, B, X, Y, LB, RB, Start, L3, R3 and D-pad joystick bindings; those inputs are delivered to the wheel instead. RT produces the normal mouse-primary-fire input in flight. Existing keyboard bindings, game scripts, engine, campaigns and saves remain in place. Use `-SkipPilotReservation` to install files without changing pilots, then press **Reserve wheel buttons** in the console for each pilot before testing.

## Controls

| Input | In flight | In menus |
|---|---|---|
| D-pad down | Tap for squad commands; hold to look rear | Next item / Down |
| D-pad up | Tap for ship systems; hold to look up | Previous item / Up |
| D-pad left | Tap for targeting; hold to look left | Previous item / Left |
| D-pad right | Tap for macros; hold to look right | Next item / Right |
| Right stick | Select wheel segment | Move pointer |
| A | Confirm selection; target ship in reticle when wheel closed | Activate selection; click after stick movement |
| B | Target closest attacker; cancel an open wheel | Back/Escape |
| RT | Primary fire when wheel closed | Mouse click |
| LT | Secondary fire when wheel closed | No menu action |
| Left stick | Native flight controls | Move pointer |
| X / Y | X cycles the selected target class; hold Y for the B-wing or T-Blade's third gun | Enter / Backspace |
| LB / RB | LB cycles secondary banks; RB changes ship mode | Previous / next tab |
| L3 / R3 | Match target speed / cycle subsystems on the targeted ship | No menu action |
| Start / Menu | Tap to pause; hold 500 ms to cycle Cockpit → Chase → External | Enter |

The main menu uses discrete D-pad selection with a visible cursor highlight. Other menus receive arrow key taps, while A sends Enter after D-pad navigation. After moving either stick, A clicks at the pointer; RT always clicks. LB/RB send Shift+Tab/Tab. This keeps the game's pointer-only controls accessible without a touchpad or finger mouse.

In flight, a short D-pad release opens its wheel. Holding a direction starts the matching view after the configured delay (250 ms by default), and releasing centers the view. The Macro Deck slider sets the delay from 150 to 600 ms. **Disable look around** makes every D-pad press open a wheel immediately. Menus retain immediate navigation. Save Wheel and restart the game after changing this setting.

**Hold Menu cycles camera** is enabled by default and can be turned off on the Macro Deck page. Each hold advances Cockpit → Chase → External → Cockpit; a short press still pauses. **Custom holds** is off by default. Enable it to reveal **Edit...**, where A, B, X, LB, RB, L3 and R3 can each receive one of the supported hold actions with a 250–1000 ms delay. A configured button's short action fires on release; Y and the triggers keep direct fire. The squad command wheel includes **Cycle boarding party task** during Assault missions, matching the game's keyboard 4 action. It is also available as a custom hold or macro action.

LB cycles the selected secondary bank for LT. Y fires the third primary gun directly on B-wings and T-Blade bombers and restores the previously selected bank afterward; Y has no flight action on other ships. RB changes the ship's available control mode: Falcon gun control, TIE-family burst fire, S-foils on equipped ships, or targeting computer mode on other ships with secondary weapons. The game's mission restrictions still apply. A targets the ship under the reticle, B targets the closest ship attacking you, and X cycles the ship class chosen on the targeting wheel (enemies by default). Choosing a class does not immediately change the target. The targeting wheel also clears the ship target, switches between a subsystem and the hull, targets a subsystem in the reticle, and cycles live turrets on the current ship. Subsystem actions are disabled when the selected ship has no eligible parts; R3 cycles subsystems directly.

**Flight Axes** defaults to **Classic direct (tested)**. It restores the earlier handheld layout using the engine's gamepad axes: right stick yaw/pitch; left stick roll/relative throttle. Choose **Classic inverted pitch** to reverse only the right stick's vertical steering. The stock 1.1 default instead binds yaw/pitch to the left stick, roll to right stick Y, and no throttle axis. The mod builds two named presets from the installed 1.1 control table and does not edit the game's table. Choose **Game's saved setup** in the console to stop applying a classic preset. The older profile used sensitivity 5 and deadzone 10%; set those on the Flight Response tab and apply with backup for the closest v1 comparison. The classic presets reserve A, B, X, L3, R3, LB, RB, Y and Start for the mod's context-aware actions. Existing custom button bindings may need to be reapplied after enabling Classic.

The wheel confirms dangerous depart/warp orders twice. The command panel uses `mn.CommItems` and native `selectItem()` so mission restrictions and FotG order hooks remain in effect. The targeting subsystem macro checks visibility, team, targetability and remaining subsystem health and is limited to single player. Energy presets are likewise limited to single player. An unavailable action displays a message instead of guessing an order.

Wheel labels now have near-opaque dark backplates, bright text and a clear amber selection border so combat effects do not wash them out. **Wheel Text** in the console selects the game's existing 20 px or 30 px font and regular or bold weight. The default is 20 px bold. Large text may occupy more of the screen on low resolutions; switch back to Medium if needed.

## Restore and remove

The console's **Restore latest backup** restores the selected pilot and matching FotG settings INI from a timestamped copy under `kn_portable/HardLightProductions/FreeSpaceOpen/ControllerSetupBackups`. Restore any pilots whose reserved controls you want back before removing the addon. Delete `GamepadMods/FotG_Gamepad_Wheel` and the two wheel launchers to remove it. The original game's launcher remains available.

## Test status

Source is included. Build and offline checks passed. On a Legion Go 2, testing confirmed that the repaired Classic direct preset restores right-stick yaw/pitch and left-stick roll/throttle in the practice mission. The prior failure was traced to a stale SDL2 joystick GUID in the wheel profile and a raw SDL3 joystick axis mapping applied to FSO's canonical SDL3 gamepad axes. The installer now detects the connected controller through the bundled SDL3 library. Classic inverted pitch is generated and selectable, but has not yet had a physical flight test. Issuing an actual squad order, all four panels, macro effects, broader mission compatibility, and multiplayer behavior still need review. Some mission-specific screens may still require stick pointer control. Fate of the Galaxy 1.1.1 is also supported. It uses the same packages, launch flags and FSO 26.0.1 SDL3 engine as 1.1.0, the Classic preset builder accepts its control defaults unchanged, and install and in-game play on 1.1.1 have been confirmed. Version 1.0.0 runs on multiple machines. It has not been tested in every campaign and mission, or in multiplayer; reports from those are welcome at dev@manictrashpanda.dev.
