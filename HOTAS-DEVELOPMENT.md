# Flight Control HOTAS

Start with docs/HOTAS-HANDOFF.md for hardware checks. The checkout baseline is Flight Control 2.

After committing shared fixes in the sibling Flight Control 2 checkout, sync here:

    git fetch flight-control
    git merge flight-control/feature/flight-control-2

Keep HOTAS-specific work on codex/flight-control-hotas.

## Hardware discovery

In the Flight Control Console, select the FotG 2 installation and open **HOTAS mode**.
Connect the Logitech Freedom 2.4 and PXN9 in the modes you plan to use. Choose
**New inventory** to capture all SDL3 joystick inputs currently exposed. Select
each device, move its controls to identify the live rows, enter optional labels,
and choose **Save profile**. This saves a versioned JSON hardware inventory.

Open the saved profile, unplug and reconnect the devices, then choose **Rescan
connections**. Check that each device still drives its own rows. Identical devices
without distinct SDL serials may require manual identification and are shown as
ambiguous in saved profiles. A mode that exposes a new set of inputs needs a new
inventory. **Open profile** expects Flight Control SDL3 hardware JSON. Existing
layout profiles and other shared formats require separate inspection.

## Flight actions

Select a device and an action, choose **Capture input**, then confirm the result.
Axis capture uses the largest movement. Button capture lists responding buttons.
Use **Save profile** to keep the assignments. **Apply to pilot** updates the
selected pilot, FotG joystick slots, and the MyMod control table. It saves a copy
of all three original files under ControllerSetupBackups. Launch Flight Control
MyMod to test the preset. The native HOTAS preset and device slots are shared
within the installation, and a later apply replaces them.
