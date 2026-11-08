# VR preview preparation

This branch prepares OpenXR support for headset testing. It has not been
validated in a headset and is not part of the published 1.1.2 release.

## Launch and drawing

- `Play with Gamepad Wheel VR.exe` starts the same controller bridge and game
  with FreeSpace Open's `-vr -stretch_menu` flags. FSO's VR mode itself forces
  windowed rendering, mouse capture, a square mirror window and no VSync.
- The wheel uses a scripted HUD gauge in VR. This makes FSO draw it in stereo
  rather than repeating the flat 2D overlay at fixed pixels for each eye.
- Wheel input, timers, and held actions run from `On Simulation` in VR, because
  `On Frame` runs once per eye. Flat-screen flight keeps its existing hook.
- The wheel stays upright in the HUD for this preview. A tilted wheel would
  need a real cockpit surface or other 3D placement to gain physical depth;
  perspective painted into a flat HUD would make text harder to read.

## Headset test checklist

1. Set the headset's OpenXR runtime, connect it, and close the game.
2. Install this preview, then run `Play with Gamepad Wheel VR.exe` from the game
   folder. Verify that FSO actually enters VR, including head tracking.
3. In a practice mission, open all four wheels. Check that labels are single,
   sharp, centered, readable at a comfortable gaze angle, and clear in both
   eyes. Try cockpit, chase, and external cameras.
4. Try a short D-pad tap, a held look direction, a held face-button action,
   and wheel confirmation. Check for duplicate actions and normal timing.
5. Check menus, cockpit messages, motion comfort, and performance. Adjust the
   game's VR resolution as needed; this launcher does not force a render size.

If the wheel is off center, too close to the reticle, or tiring to read, adjust
the gauge's `Origin` and `Offset` in `zzz_fotg_wheel-hdg.tbm` after testing.
The wheel's font size and bold option remain in the Flight Control Console.

The preview launcher is separate from `Play with Gamepad Wheel.exe`. The
published 1.1.2 ZIP and website download are unchanged.
