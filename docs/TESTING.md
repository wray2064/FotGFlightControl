# Test checklist

Test with the game closed before changing any settings. Keep the original game
launcher available for comparison.

1. Start Setup. Verify that it detects a complete 1.1.0, 1.1.1 or 1.1.2 install, a native pilot,
   and the SDL3 engine. Check the Browse and Check again paths.
2. Install or repair. Confirm the two game-folder launchers are created and the
   console opens from Setup. Check that a timestamped pilot backup exists.
3. Launch with the wheel. Check main menu D-pad navigation, A select, B back,
   stick pointer fallback, and LB/RB tab movement.
4. In flight, tap each D-pad direction and confirm that only its wheel opens,
   without a brief view shift. Hold each direction past the slider threshold;
   confirm that only the matching view activates and returns on release. Check
   both ends of the timing slider and Disable look around. Then select a harmless
   wheel option with the right stick and A; cancel with B. Check the targeting
   class, clear target, turret and subsystem choices against a valid target.
5. Test RT primary, LT secondary, LB bank cycle, RB ship mode, A reticle target,
   B closest attacker, X target cycle, L3 speed match, and R3 subsystem cycle.
   On a B-wing or T-Blade bomber, check Y third-gun behavior.
   Tap Menu to pause. Hold it three times and verify Cockpit, Chase, External,
   then Cockpit again; disable camera hold and verify Menu only pauses. Enable
   Custom holds, map one button, and verify a short press keeps its usual action
   while a long press runs the chosen action once.
   In an Assault mission, verify Cycle boarding party task appears under squad
   commands and changes the native boarding task.
6. Save each macro type in the console, restart the game, and verify its action.
   Test wheel text size and weight against bright combat effects.
7. Restore a pilot backup and confirm its native controls return.
   With two pads connected, choose the non-default XInput slot in Hangar Bay
   Service > Select controller, verify its preview responds, then Apply with
   backup. Restart and confirm that only the chosen pad controls flight, wheels,
   and menus. Check that Repair preserves the choice, and that unplugging the
   chosen pad does not silently hand control to the other one. Restore the
   latest backup and confirm the previous selection returns.
8. Buttons...: give A countermeasures and LB the afterburner (while held), save,
   restart, and confirm A fires one countermeasure per press and the afterburner
   runs only while LB is held. Confirm A still confirms and B still cancels in an
   open wheel. Reset to defaults and confirm the 1.0 actions return.
9. Custom axes: swap yaw and roll between the sticks, invert one axis, save,
   restart, and confirm each axis in flight. Run Install / Repair and confirm the
   custom axes survive. Check the main menu shows no startup warnings popup.
11. Clean install: on a FotG 1.1.2 folder with only the standard FSO 26.0.1
   (SDL2) engine, confirm Setup names it, Install detects the pad and its stick
   order, and flight, buttons and Custom axes behave as on the SDL3 engine.
10. Updates: with a test manifest (FLIGHTCONTROL_UPDATE_URL=file:///...), confirm
   Setup offers the update, refuses a wrong SHA-256, and shows the new Setup.exe
   in File Explorer without starting it.

Record game version, ship, mission, controller, Windows version, and the exact
steps for any failure. Avoid sharing pilot files or logs that contain personal
data in public issues.

Version 1.0.0 has passed build and offline checks. It has been tested on a
Lenovo Legion Go 2, ROG Ally X, Xbox controllers and generic gamepads, with Fate
of the Galaxy 1.1.0, 1.1.1 and 1.1.2, focusing extensively on practice missions to
refine how flying feels. The campaign and multiplayer have not yet been tested.
Send bugs, awkward interactions and ideas to dev@manictrashpanda.dev.
