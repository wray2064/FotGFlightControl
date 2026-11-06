# Install the gamepad wheel

This preview works with the Windows portable **Fate of the Galaxy 1.1.0 or 1.1.1** release
and its **FSO 26.0.1 SDL3** engine. The game is a separate download. The add-on
does not include the game or engine.

1. Install Fate of the Galaxy. Launch it normally, create a pilot, and exit.
   Connect your gamepad before running Setup.
2. Download the gamepad wheel release ZIP and extract the whole folder.
3. Double-click **Setup.exe**. It finds common game locations; use **Browse game**
   if it does not find yours. Select the folder containing `kn_portable`.
4. Click **Install / Repair**. Setup backs up existing pilot controls and any
   earlier gamepad wheel files, detects your controller through the bundled
   SDL3 library and selects it in the wheel launch profile, then creates **Play with Gamepad Wheel.exe** and
   **Configure Gamepad Wheel.exe** in the game folder.
5. Click **Play with Wheel** in Setup, or use the new launcher in the game folder.

Close the game before installing, repairing or changing pilot settings. If Setup
reports **First run required**, create a pilot in the unmodified game, close it,
then click **Check again**. If the game is updated to a different version, wait
for a compatible add-on release.

**Configure** opens the Flight Control Console. Use its Flight Response page to
sample center drift and set sensitivity/deadzone. **Apply with backup** saves
those pilot settings. The Command Wheel / Macro Deck page configures flight axes,
four macros, pitch direction, and wheel text. **Classic direct** keeps the tested
right-stick pitch; **Classic inverted pitch** reverses its vertical direction.
The **D-pad hold to look** slider changes when a held direction starts its view;
**Disable look around** makes the D-pad open wheels without view controls.
**Hold Menu cycles camera** is on by default. **Custom holds** is off by default;
turn it on to reveal **Edit...** for A, B, X, LB, RB, L3 and R3 hold actions.
**Save Wheel** saves those settings. Restart the game
after saving wheel settings.

To undo reserved pilot bindings, close the game and use **Restore latest backup**
in the console for each affected pilot. This restores the full pilot and matching
settings INI as they were at backup time, including other settings saved then.
Afterward you may remove `GamepadMods/FotG_Gamepad_Wheel` and the two wheel
launchers. The original game launcher remains available throughout.

Advanced users can run `Install.ps1 -GameRoot 'C:\path\to\Fate of the Galaxy 1.1.0'`
from PowerShell. The GUI Setup calls the same script.
