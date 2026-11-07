# Lists joysticks through an SDL2-based FSO engine's SDL2.dll, the way that engine sees them.
# For each it returns the SDL2 GUID and, from the resting axis values, which raw axes are the
# two sticks: triggers rest at the minimum, so the remaining axes in order are left X, left Y,
# right X, right Y. AxisMap names them with FSO's axis names (X_AXIS = raw axis 0, and so on).
param([Parameter(Mandatory=$true)][string]$DllDir, [int]$SampleSeconds=0)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path $DllDir 'SDL2.dll'))) { throw "SDL2.dll was not found in $DllDir." }
if (-not ('Sdl2Probe' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Sdl2Probe {
  [StructLayout(LayoutKind.Sequential)] public struct Guid { public ulong a, b; }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string p);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_NumJoysticks();
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern Guid SDL_JoystickGetDeviceGUID(int index);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_JoystickGetGUIDString(Guid guid, System.Text.StringBuilder text, int length);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickNameForIndex(int index);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickOpen(int index);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern int SDL_JoystickNumAxes(IntPtr joystick);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_JoystickUpdate();
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_JoystickClose(IntPtr joystick);
  [DllImport("SDL2.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_Quit();
}
'@ }
$names = @('X_AXIS', 'Y_AXIS', 'Z_AXIS', 'RX_AXIS', 'RY_AXIS', 'RZ_AXIS')
[Sdl2Probe]::SetDllDirectory($DllDir) | Out-Null
if ([Sdl2Probe]::SDL_Init(0x200) -ne 0) { throw 'SDL2 joystick initialization failed' }
try {
  for ($i = 0; $i -lt [Sdl2Probe]::SDL_NumJoysticks(); $i++) {
    $guid = [Sdl2Probe]::SDL_JoystickGetDeviceGUID($i)
    $text = [Text.StringBuilder]::new(64)
    [Sdl2Probe]::SDL_JoystickGetGUIDString($guid, $text, $text.Capacity)
    $name = [Runtime.InteropServices.Marshal]::PtrToStringAnsi([Sdl2Probe]::SDL_JoystickNameForIndex($i))
    $joystick = [Sdl2Probe]::SDL_JoystickOpen($i)
    if ($joystick -eq [IntPtr]::Zero) { continue }
    try {
      $count = [Math]::Min(6, [Sdl2Probe]::SDL_JoystickNumAxes($joystick))
      # Let the first state report arrive, then read each axis a few times.
      $low = @{}
      for ($n = 0; $n -lt 8; $n++) {
        Start-Sleep -Milliseconds 40; [Sdl2Probe]::SDL_JoystickUpdate()
        for ($axis = 0; $axis -lt $count; $axis++) {
          $v = [Sdl2Probe]::SDL_JoystickGetAxis($joystick, $axis)
          if ($n -ge 3 -and $v -le -30000) { $low[$axis] = $true }
        }
      }
      if ($SampleSeconds -gt 0) {
        $until = [DateTime]::UtcNow.AddSeconds($SampleSeconds)
        while ([DateTime]::UtcNow -lt $until) {
          [Sdl2Probe]::SDL_JoystickUpdate()
          "{0:HH:mm:ss.fff}  {1}" -f [DateTime]::Now, ((0..($count - 1) | ForEach-Object { [Sdl2Probe]::SDL_JoystickGetAxis($joystick, $_) }) -join ' ')
          Start-Sleep -Milliseconds 100
        }
        continue
      }
      $sticks = @(0..($count - 1) | Where-Object { -not $low[$_] })
      # Two triggers and four stick axes is a gamepad; anything else keeps the order FSO 26.0.1's
      # SDL 2.26.5 reports for an Xbox controller (left X/Y, right X/Y, then the triggers),
      # measured on an Xbox 360 controller and matching the SDL3 build's numbering.
      $map = if ($count -ge 6 -and $low.Count -eq 2 -and $sticks.Count -eq 4) { @($sticks | ForEach-Object { $names[$_] }) } else { @('X_AXIS', 'Y_AXIS', 'Z_AXIS', 'RX_AXIS') }
      [pscustomobject]@{ Id = $i; Guid = $text.ToString(); Name = $name; AxisMap = ($map -join ','); Detected = ($low.Count -eq 2 -and $sticks.Count -eq 4) }
    } finally { [Sdl2Probe]::SDL_JoystickClose($joystick) }
  }
} finally { [Sdl2Probe]::SDL_Quit() }
