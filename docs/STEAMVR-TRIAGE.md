# SteamVR desktop-only triage — 2026-11-08

## Decision

Defer deep SteamVR rendering investigation to FSO/OpenXR/SteamVR specialists.
The observed failure occurs during OpenXR graphics-session initialization,
after Flight Control has launched the engine with VR enabled. No launcher
code change is justified by the available evidence. This does not establish
whether the underlying defect belongs to FSO, SteamVR, or the graphics driver.
Meta Quest Link stereo/head tracking works according to the user and the VR
preview notes; it was not rerun during this investigation.

## Observed evidence

- Active 64-bit OpenXR runtime registry value:
  `HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` =
  `C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json`.
- SteamVR server log records two FSO connections, PIDs 37972 and 32844,
  at timestamps 2026-11-08 20:17:46 and 20:19:40 as written by the log
  (timezone not independently verified).
- Both engine command lines include:
  `-mod GamepadMods\FotG_Gamepad_Wheel,kn_portable\Library\FotG_R\FotG_R-1.1.2 -aa -soft_particles -deferred_cockpit -vr -stretch_menu`.
- Engine executable:
  `C:\Games\Fate of the Galaxy 1.1.0\kn_portable\Library\bin\FSO_2601SDL3-1.0.0\windows-x64\fs2_open_26_0_1_sdl3_x64_SSE2.exe`.
- SteamVR identifies the app as `VRApplication_OpenXRInstance` and reports
  `Using existing HMD vrlink.VRLINKHMDQUEST3S`.
- The app-specific SteamVR OpenXR log reports SteamVR 2.18.4, a successful
  server connection, and the requested extension `XR_KHR_opengl_enable`.
- Both runs then report these exact messages:
  `Unable to create internal OpenGL context: 6`
  `xrCreateSession: Failed to initialize binding`
- Invalid space/session handle assertions occur on shutdown after that failure.
  They are later symptoms, not proof of the initial cause.
- Server log also reports a missing action manifest. The app-specific log
  supplies the more direct graphics-session failure; do not assume adding a
  launcher-side action manifest fixes it.
- Later Steam Link stream resets are logged after the initial session failure;
  they do not explain the earlier failure by themselves.
- The last saved Flight Control telemetry reports `vr=0`, `gauge=0`, `draws=0`.
  It is a quit-state snapshot, not independently correlated to a specific PID.

## Source logs

- `C:\Program Files (x86)\Steam\logs\xrclient_fs2_open_26_0_1_sdl3_x64_SSE2.txt`
- `C:\Program Files (x86)\Steam\logs\vrserver.txt`
- `C:\Games\Fate of the Galaxy 1.1.0\kn_portable\HardLightProductions\FreeSpaceOpen\data\players\fotg-wheel-state.txt`

Copies of the two SteamVR logs at investigation time are stored beside this
report in `steamvr-triage-20261108/`. They may contain local filesystem paths.

## Launcher inspection

Hangar Bay passes `--vr` to WheelBridge. WheelBridge passes `-vr -stretch_menu`
to FSO. Recorded SteamVR command lines confirm the installed launch path
actually did so; this conclusion is not based only on source inspection.

## Remaining specialist checks

1. Reproduce with the same FSO executable and base FotG mod, without Flight
   Control's Lua mod or bridge, preserving VR flags. Compare with the failed
   runs above. This isolation test has not been performed.
2. Compare engine, GPU/driver and OpenGL context setup between SteamVR and
   working Meta OpenXR runs. Capture an FSO debug log at session creation.
3. Test the engine's OpenGL/OpenXR binding against SteamVR on the same GPU;
   investigate the internal OpenGL context creation failure before changing
   wheel rendering or controller input code.

No runtime registry settings, drivers, releases, installed binaries, or launcher
behavior were changed. No new headset run was performed. The pause/quit cursor
report remains separate and has not been fixed by this investigation.
