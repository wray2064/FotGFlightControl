# Default hyperspace shoulder combination

LB + RB within 120 ms, held together for 300 ms, sends Return once through
Windows input. This follows FotG's existing KEY_ENTER / END_MISSION path and
its hyperspace script checks instead of calling jump functions directly.
Release both to rearm. Aborting a recognized combination consumes both
individual actions. A late second shoulder remains ordinary input.

Single shoulder presses are delayed by the 120 ms combination decision window.
A quick tap released within the window is forwarded as a tap. Custom long-press
and held mappings still run through the existing Lua handling, after that
initial delay. No JSON profile schema or saved mappings change.

The combination runs only during focused, connected FLIGHT with no wheel or
pending wheel. Opening a wheel or system dialog, leaving flight, focus loss,
or disconnect resets the recognizer and requires releasing both shoulders.
Axes, face buttons, and both triggers are preserved. No new confirmation screen
is added. Return must remain the game's Engage Hyperdrive key for this shortcut.

Validation: bridge Build.ps1 and --self-test passed, including near-simultaneous
presses in either order, exact time boundaries, one-shot/rearm, short taps,
individual holds, late second presses, canceled combinations, and resets.
Existing Lua dialog regression checks also passed. This tests input recognition,
not actual campaign jump success.

Installed locally in C:\Games\Fate of the Galaxy 1.1.0. No release or merge.
Manual campaign checks remaining: complete a jump under fire while steering and
firing; confirm no secondary-cycle or ship-mode change; cancel a short chord;
verify individual shoulder mappings and custom held/long actions; repeat after
pause, focus loss and disconnect, and in working Meta OpenXR VR.
