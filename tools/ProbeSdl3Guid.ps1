param([Parameter(Mandatory=$true)][string]$GameRoot, [int]$SampleSeconds=0, [string]$DllDir)
$ErrorActionPreference = 'Stop'
# The engine folder that holds SDL3.dll; defaults to the FSO 26.0.1-SDL3 package.
$dllDir = if ($DllDir) { $DllDir } else { Join-Path $GameRoot 'kn_portable\Library\bin\FSO_2601SDL3-1.0.0\windows-x64' }
if (-not (Test-Path -LiteralPath (Join-Path $dllDir 'SDL3.dll'))) { throw 'Bundled SDL3.dll was not found.' }
if (-not ('SdlProbe' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SdlProbe {
  [StructLayout(LayoutKind.Sequential)] public struct Guid { public ulong a, b; }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string p);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern bool SDL_Init(uint flags);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_GetJoysticks(out int count);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern Guid SDL_GetJoystickGUIDForID(uint id);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_GUIDToString(Guid guid, System.Text.StringBuilder text, int length);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_GetJoystickNameForID(uint id);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr SDL_OpenJoystick(uint id);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_CloseJoystick(IntPtr joystick);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_UpdateJoysticks();
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern short SDL_GetJoystickAxis(IntPtr joystick, int axis);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_free(IntPtr p);
  [DllImport("SDL3.dll", CallingConvention=CallingConvention.Cdecl)] public static extern void SDL_Quit();
}
'@ }
[SdlProbe]::SetDllDirectory($dllDir) | Out-Null
if (-not [SdlProbe]::SDL_Init(0x200)) { throw 'SDL joystick initialization failed' }
try {
  $count = 0
  $ids = [SdlProbe]::SDL_GetJoysticks([ref]$count)
  try {
    for ($i=0; $i -lt $count; $i++) {
      $id = [uint32][Runtime.InteropServices.Marshal]::ReadInt32($ids, 4*$i)
      $guid = [SdlProbe]::SDL_GetJoystickGUIDForID($id)
      $text = [Text.StringBuilder]::new(64)
      [SdlProbe]::SDL_GUIDToString($guid, $text, $text.Capacity)
      $name = [Runtime.InteropServices.Marshal]::PtrToStringAnsi([SdlProbe]::SDL_GetJoystickNameForID($id))
      if ($SampleSeconds -gt 0) {
        $joystick = [SdlProbe]::SDL_OpenJoystick($id)
        if ($joystick -eq [IntPtr]::Zero) { throw "Could not open SDL3 joystick $id" }
        try {
          $until = [DateTime]::UtcNow.AddSeconds($SampleSeconds)
          while ([DateTime]::UtcNow -lt $until) {
            [SdlProbe]::SDL_UpdateJoysticks()
            $values = @(for ($axis=0; $axis -lt 6; $axis++) { [SdlProbe]::SDL_GetJoystickAxis($joystick, $axis) })
            "{0:HH:mm:ss.fff}  {1}" -f [DateTime]::Now, ($values -join ' ')
            Start-Sleep -Milliseconds 100
          }
        } finally { [SdlProbe]::SDL_CloseJoystick($joystick) }
      } else {
        [pscustomobject]@{ Id=$i; Guid=$text.ToString(); Name=$name }
      }
    }
  } finally { [SdlProbe]::SDL_free($ids) }
} finally { [SdlProbe]::SDL_Quit() }
