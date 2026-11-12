# Flight Control Console

By **Manic Trash Panda**, coded with ChatGPT. Questions: **dev@manictrashpanda.dev**. See [About.html](About.html) for project and platform attributions.

The original console code and documentation are released under the [MIT License](LICENSE). The original emblem may be used only with this tool under the [artwork terms](ARTWORK-LICENSE.md).

Windows x64 setup app for Fate of the Galaxy 1.1.0, 1.1.1, 1.1.2 or 1.1.3 and the separate FotG Gamepad Wheel mod. Extract the entire folder and run `ControllerSetup.exe`. Requires .NET Framework 4.x. The game and wheel mod must be installed separately.

The targeting computer themed console has two pages:

- **Flight Response:** live XInput controller diagram, stick/trigger monitoring, center-drift sample, native sensitivity/deadzone/mouse-flight settings. Applying settings backs up the selected pilot JSON and FotG_R_settings.ini. The XInput monitor slot does not select the game's joystick.
- **Command Wheel / Macro Deck:** four D-pad-right slots. Each has a HUD label and one supported typed action: maximum shield energy, nearest enemy, enemy subsystem (with alias), match target speed, or balance energy. **Advanced** swaps the top-level choices in each wheel and imports/exports validated JSON profiles. Squad ordering applies only to the first communications page; later game menus keep their native order. Flight Axes selects the Classic native v1 layout (right stick yaw/pitch, left stick roll/throttle) or the game's saved setup. Wheel Text selects Medium or Large and regular or bold lettering; Medium Bold is the default. Saving writes `data/tables/lua/zz_fotg_wheel_config-sct.lua` inside `FotG_Gamepad_Wheel`; an earlier config is backed up under `ConfigBackups`. Restart the game to load changes.
- **Flight buttons:** RT fires the selected primary, LT fires the selected secondary, LB cycles secondary banks, and RB changes the available ship mode (Falcon gun control, TIE burst, S-foils, or targeting computer). Y fires a B-wing or T-Blade bomber's third gun directly and has no flight action on other ships. A targets under the reticle, B targets the closest ship attacking you, X cycles the target class chosen by the targeting wheel, and the D-pad opens the four wheel panels. The targeting wheel also clears the ship target, switches between subsystem and hull targeting, selects a subsystem under the reticle, and cycles live turrets. L3 matches target speed, and R3 cycles subsystems on the targeted ship.

The app auto-detects the wheel mod at `<game root>/GamepadMods/FotG_Gamepad_Wheel`. **Browse mod** supports another location. The selected folder must contain `data/tables/lua/zzz_fotg_wheel-sct.lua`.

**Reserve wheel buttons** is an explicit, per-pilot action. With the game closed, it backs up the whole pilot JSON plus the matching INI, then changes only joystick values 0–5, 7–9 and 128–131 to -1 in the native version-4 pilot controls array. It leaves other bindings and controller selection alone. The wheel companion supplies primary fire on RT after A is reserved. **Restore latest backup** can undo the reservation but restores whole backed-up pilot and INI files, including any other settings saved then.

For an installer, `ControllerSetup.exe --reserve-wheel --game "<game root>"` reserves those values for all existing native version-4 pilots. It preflights every pilot, backs up all changed pilots and the INI, rolls back on write failure, returns a nonzero exit code on failure, and does nothing when no matching bindings exist. Because it is a Windows GUI executable, launch this command with a waiting process API when checking its exit code.

Build with `Build.ps1` using Microsoft's installed .NET Framework compiler. `ControllerSetup.exe --self-test` checks INI section edits, button isolation, and macro label escaping without changing game settings; it writes `self-test.txt`. The source is in `src`; the icon and help are in `Assets` and `Help.html`.

The wheel mod handles in-game actions. This app alone does not inject controller input or add front-end menu navigation. Subsystem availability varies by ship and mission; the targeting wheel disables subsystem actions when the current ship has no eligible parts. The native deadzone persistence in the game's custom UI still needs in-game verification.

This is an unofficial, AI-assisted mod and tool. It contains no game assets or personal pilot data. The original console code and documentation are released under the MIT License in this folder; see ARTWORK-LICENSE.md for the emblem.
