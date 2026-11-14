// Regression test: does the game's own SDL2.dll turn the bridge's injected keys into key events?
// The standard FSO 26.0.1 engine (SDL 2.26.5) drops a key sent with only a virtual-key code, so LT (Tab),
// Menu/pause (Esc), the wheel's number keys and Enter went dead on clean installs. The bridge now sends scan codes.
//
// Build (from the repository root, compiled with the real bridge code so WinInput is what gets tested):
//   csc /nologo /platform:x64 /target:exe /main:SdlKeys /reference:System.Windows.Forms.dll /reference:System.Drawing.dll
//       /out:SdlKeys.exe scripts\SdlKeyTest.cs bridge\WheelBridge.cs setup\GameInstall.cs setup\BuildInfo.cs setup\AssemblyInfo.cs
// Run:  SdlKeys.exe "<game>\kn_portable\Library\bin\FSO-26.0.1\windows\x64_avx"   (the folder that holds SDL2.dll)
// A small window takes focus for about three seconds. Every row of the last column must say "received".
// The "scan code" column omits the extended-key flag, so End and the arrows read "wrong" there; that is expected.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// Which injected keyboard events does the game's own SDL2.dll turn into key events?
// Sends the same two kinds of events as the bridge (virtual-key only, and scan code) to a real SDL2 window.
class SdlKeys {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_Init(uint flags);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr SDL_CreateWindow([MarshalAs(UnmanagedType.LPStr)] string title, int x, int y, int w, int h, uint flags);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_PollEvent(IntPtr ev);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern void SDL_DestroyWindow(IntPtr w);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern void SDL_Quit();
    [DllImport("user32.dll", CharSet = CharSet.Ansi)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION u; }

    static void Key(ushort vk, ushort scan, bool useScan, bool down) {
        var i = new INPUT { type = 1 };
        i.u.ki.vk = useScan ? (ushort)0 : vk;
        i.u.ki.scan = useScan ? scan : (ushort)0;
        i.u.ki.flags = (useScan ? 8u : 0u) | (down ? 0u : 2u);
        SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
    }

    // SDL_KeyboardEvent: type@0, state@12, scancode@16.
    static List<int> Drain(IntPtr ev, int ms) {
        var seen = new List<int>();
        var end = Environment.TickCount + ms;
        while (Environment.TickCount < end) {
            while (SDL_PollEvent(ev) != 0) {
                if (Marshal.ReadInt32(ev, 0) == 0x300) seen.Add(Marshal.ReadInt32(ev, 16));
            }
            Thread.Sleep(5);
        }
        return seen;
    }

    static int Main(string[] args) {
        SetDllDirectory(args[0]);
        if (SDL_Init(0x20) != 0) { Console.WriteLine("SDL_Init failed"); return 2; }   // SDL_INIT_VIDEO
        IntPtr win = SDL_CreateWindow("fotg-key-test", 200, 200, 300, 120, 4);          // SDL_WINDOW_SHOWN
        IntPtr ev = Marshal.AllocHGlobal(64);
        Drain(ev, 500);
        IntPtr hwnd = FindWindow(null, "fotg-key-test");
        keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);   // allow SetForegroundWindow
        SetForegroundWindow(hwnd);
        for (int t = 0; t < 20 && GetForegroundWindow() != hwnd; t++) { Thread.Sleep(100); SetForegroundWindow(hwnd); }
        Console.WriteLine("test window is foreground: " + (GetForegroundWindow() == hwnd));
        Drain(ev, 300);

        // name, virtual key, scan code, expected SDL scancode
        var keys = new[] {
            new object[] { "Tab (LT)",        (ushort)0x09, (ushort)0x0F, 43 },
            new object[] { "Esc (Menu/pause)",(ushort)0x1B, (ushort)0x01, 41 },
            new object[] { "1 (wheel number)",(ushort)0x31, (ushort)0x02, 30 },
            new object[] { "0 (wheel number)",(ushort)0x30, (ushort)0x0B, 39 },
            new object[] { "Z (computer)",    (ushort)0x5A, (ushort)0x2C, 29 },
            new object[] { "Enter",           (ushort)0x0D, (ushort)0x1C, 40 },
            new object[] { "F4 (mission log)",(ushort)0x73, (ushort)0x3E, 61 },
            new object[] { "Space",           (ushort)0x20, (ushort)0x39, 44 },
            new object[] { "End (extended)",  (ushort)0x23, (ushort)0x4F, 77 },
            new object[] { "Left (arrow)",    (ushort)0x25, (ushort)0x4B, 80 },
        };
        Console.WriteLine("{0,-18} {1,-26} {2,-26} {3,-26}", "key", "virtual-key only", "scan code", "bridge WinInput.Tap");
        foreach (object[] k in keys) {
            ushort vk = (ushort)k[1], sc = (ushort)k[2]; int want = (int)k[3];
            Key(vk, sc, false, true); Key(vk, sc, false, false);
            var a = Drain(ev, 250);
            Key(vk, sc, true, true); Key(vk, sc, true, false);
            var b = Drain(ev, 250);
            WinInput.Tap(vk);
            var c = Drain(ev, 250);
            Func<List<int>, string> show = l => l.Contains(want) ? "received (SDL " + want + ")" : (l.Count == 0 ? "NOTHING" : "wrong: " + string.Join(",", l.ConvertAll(x => x.ToString()).ToArray()));
            Console.WriteLine("{0,-18} {1,-26} {2,-26} {3,-26}", k[0], show(a), show(b), show(c));
        }
        SDL_DestroyWindow(win); SDL_Quit();
        return 0;
    }
}
