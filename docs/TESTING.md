# Preview test checklist

Test with the game closed before changing any settings. Keep the original game
launcher available for comparison.

1. Start Setup. Verify that it detects a complete 1.1.0 install, a native pilot,
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

Record game version, ship, mission, controller, Windows version, and the exact
steps for any failure. Avoid sharing pilot files or logs that contain personal
data in public issues.

The current preview has passed build and offline checks, plus limited simulated
input and a practice-flight startup check. A complete real-controller play
session and broad mission coverage are still outstanding.
