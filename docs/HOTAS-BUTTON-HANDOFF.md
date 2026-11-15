# HOTAS button recognition handoff

Baseline: codex/flight-control-hotas, commit 782888b. Tested in FotG 2.0.2 through Flight Control MyMod with the Logitech Freedom 2.4. The player confirmed the recovered button assignments and the axis mappings worked in-game.

## Discover and capture

- Open **HOTAS mode** with the FotG 2 installation selected. The Console uses the installed FSO SDL3 joystick backend. It opens each joystick and enumerates all exposed buttons by their zero-based SDL index, along with axes and hats. The main Console's XInput slot indicator covers gamepads.
- Select the physical device and check that pressing a button changes its live row from **Released** to **Pressed**. Labels are optional. **Verify control** can confirm or correct a physical label.
- Select a button action and click **Capture input**. Start with the buttons released, press the intended physical button, then confirm its candidate. The capture compares each button with its starting state and retains every changed button until retry or confirmation. Axis capture uses a separate largest-movement rule.
- **Save profile** stores button assignments under FlightButtons next to FlightAxes. Each entry refers to the device's position in the profile and the SDL button index. The version 1 format currently calls the index field AxisIndex in both collections. Keep the full set of assignments in one profile when remapping.

## Apply and validate in FotG

- **Apply to pilot** rescans and checks that the assigned device and input are still present. Console matching uses SDL GUID and serial when available. Native apply requires a unique connected GUID and supports four joystick slots and button indices 0 through 127.
- Apply writes the joystick slots to FotG_2_settings.ini and writes native action IDs into MyMod's default control override. Each button uses its assigned CID_JOY slot, flags NONE, and the zero-based SDL index. The Console backs up the pilot, settings, and table before writing.
- The original standalone named preset did not survive launch. FotG saved the pilot back to default. The working implementation puts the HOTAS block inside MyMod's default override and selects default in the pilot.
- The second in-game test had working axes and zero working buttons because the last apply contained only axes. Apply replaces the installation's HOTAS block with the current profile. We recovered six button assignments from the earlier table backup, verified joystick 0 still had the same GUID, merged the buttons into the working default block, and relaunched MyMod.

| SDL button | Verified assignment |
| --- | --- |
| 0 | Primary fire |
| 1 | Secondary fire |
| 2 | Target next ship |
| 3 | Target ship in reticle |
| 4 | Target closest hostile |
| 5 | Target next subsystem |

For a new device, validate the path in order: live button row, capture candidate, saved FlightButtons entry, generated native binding, then the action in a MyMod practice mission. The shared R2 profiles describe HID-declared capabilities. Their button counts and usage IDs can guide testing, but live capture is needed to establish SDL indices and physical behavior. Hat directions are visible in the inspector. The current native action bank captures physical SDL buttons.

Implementation: console/src/HardwareInspector.cs handles discovery, capture, and profiles. console/src/HotasApply.cs generates the native bindings and applies them.
