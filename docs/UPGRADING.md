# Updating a Fate of the Galaxy 1.1.x install from Flight Control 1.1.4 or earlier

Install / Repair keeps your saved settings. Because this version changed some defaults and one wheel, Setup
writes your old behavior into your settings once, so nothing you rely on changes without you choosing it.

| What changed in this version | What you get when you update | What a fresh install gets |
| --- | --- | --- |
| New holds: R3 (mouse movement), RB (ship mode), L3 (link guns), LB (link torpedoes) | Off. Turn them on in Hangar Bay > Console > Buttons. | On |
| RB tap | Still cycles ship mode | Cycles the primary bank; hold RB for ship mode |
| Targeting wheel slot 6 | The Targeting order is set back to the default, because an old saved order would point at a different action (slot 6 changed from Subsystem in sight to Targeting computer). Subsystem in sight is still available as a button action. | New wheel |
| Systems, Macros and Squad orders, macros, long presses, axes | Kept | Defaults |

The first time you open the Console after updating, it shows a short notice that says what was kept and what was reset.
Your previous settings file is saved in the install snapshot named in the Setup log
(`GamepadMods/_WheelInstallBackups/<date>`), and in Hangar Bay under Service > Backups.

Wheel profiles (JSON) exported before this version import the same way: new holds off and the default Targeting
order. Profiles exported by this version can't be imported by Flight Control 1.1.4 or earlier.

Repair never repeats the migration: your settings are marked with `config_version` after the first update.

This applies to the Classic (Fate of the Galaxy 1.1.0 to 1.1.3) install only. A Fate of the Galaxy 2 install is new and always gets the current defaults.
