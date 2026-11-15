// HOTAS input for Flight Control's own features. A stick, throttle or pedals are mapped onto a
// virtual gamepad, so the wheels, tap/hold gestures, hyperspace chord and menu navigation work
// unchanged from any joystick FotG's SDL3 engine can see. Shared by the Console and the bridge.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

struct HotasPadState {
    public ushort Buttons;
    public byte LeftTrigger, RightTrigger;
    public short LeftX, LeftY, RightX, RightY;
}

// Written by the Console when HOTAS bindings are applied, read by the bridge.
sealed class HotasMapFile {
    public int Version { get; set; }
    public List<HotasMapDevice> Devices { get; set; }
    public Dictionary<string, HotasMapBinding> Controls { get; set; }

    public static string FileName { get { return "fotg-hotas-virtual.map"; } }

    public static HotasMapFile Load(string path) {
        try {
            if (!File.Exists(path)) return null;
            var map = new JavaScriptSerializer().Deserialize<HotasMapFile>(File.ReadAllText(path));
            return Check(map) ? map : null;
        } catch (Exception) { return null; }
    }

    public static bool Check(HotasMapFile map) {
        if (map == null || map.Version != 1 || map.Devices == null || map.Controls == null) return false;
        foreach (var entry in map.Controls) {
            var b = entry.Value;
            if (!HotasPad.IsVirtualControl(entry.Key) || b == null || b.DeviceIndex < 0 || b.DeviceIndex >= map.Devices.Count ||
                b.Index < 0 || (b.Kind != "Button" && b.Kind != "Hat" && b.Kind != "Axis")) return false;
        }
        return map.Devices.All(d => d != null && !String.IsNullOrEmpty(d.Guid));
    }
}

sealed class HotasMapDevice {
    public string Guid { get; set; }
    public string Serial { get; set; }
}

// Kind is Button, Hat or Axis. Direction: SDL hat bits (1 up, 2 right, 4 down, 8 left) for a hat,
// or +1 / -1 for the side of an axis (0 = the whole axis). Invert flips an analog axis.
sealed class HotasMapBinding {
    public int DeviceIndex { get; set; }
    public string Kind { get; set; }
    public int Index { get; set; }
    public int Direction { get; set; }
    public bool Invert { get; set; }
}

interface IHotasReader {
    bool Connected(int device);
    int Axis(int device, int index);
    bool Button(int device, int index);
    int Hat(int device, int index);
}

static class HotasPad {
    public const int HatUp = 1, HatRight = 2, HatDown = 4, HatLeft = 8;

    // XInput button masks, so the result drops straight into the existing gamepad path.
    public static readonly Dictionary<string, int> ButtonBits = new Dictionary<string, int> {
        {"A",4096},{"B",8192},{"X",16384},{"Y",32768},{"LB",256},{"RB",512},{"L3",64},{"R3",128},
        {"Start",16},{"Back",32},{"Dpad up",1},{"Dpad down",2},{"Dpad left",4},{"Dpad right",8}
    };
    public static readonly string[] AnalogControls = {
        "Left trigger","Right trigger","Left stick X","Left stick Y","Right stick X","Right stick Y"
    };

    // Wheel select and cancel act only while a wheel is open, so a button can fire in flight and confirm in a wheel.
    public const string WheelSelect = "Wheel select", WheelCancel = "Wheel cancel";
    public static readonly string[] WheelOnlyControls = { WheelSelect, WheelCancel };

    public static IEnumerable<string> VirtualControls { get { return WheelOnlyControls.Concat(ButtonBits.Keys).Concat(AnalogControls); } }
    public static bool IsVirtualControl(string name) { return name != null && (ButtonBits.ContainsKey(name) || AnalogControls.Contains(name) || WheelOnlyControls.Contains(name)); }
    public static bool IsAnalog(string name) { return AnalogControls.Contains(name); }

    const int Half = 16384;

    static bool Digital(HotasMapBinding b, IHotasReader r) {
        switch (b.Kind) {
            case "Button": return r.Button(b.DeviceIndex, b.Index);
            case "Hat": return b.Direction != 0 && (r.Hat(b.DeviceIndex, b.Index) & b.Direction) == b.Direction;
            default: {
                int value = r.Axis(b.DeviceIndex, b.Index);
                int sign = b.Direction < 0 ? -1 : 1;
                return value * sign > Half;
            }
        }
    }

    static short Clamp(int v) { return (short)Math.Max(-32767, Math.Min(32767, v)); }

    // The analog value of a binding on a stick axis: an axis passes through, a button or hat gives full deflection.
    static short Stick(string name, HotasMapBinding b, IHotasReader r) {
        if (b.Kind != "Axis") return Digital(b, r) ? Clamp(b.Direction < 0 ? -32767 : 32767) : (short)0;
        int value = r.Axis(b.DeviceIndex, b.Index);
        // SDL reports forward as negative; XInput reports up as positive, which the bridge expects.
        bool flip = b.Invert ^ name.EndsWith("Y");
        return Clamp(flip ? -value : value);
    }

    static byte Trigger(HotasMapBinding b, IHotasReader r) {
        if (b.Kind != "Axis") return Digital(b, r) ? (byte)255 : (byte)0;
        int value = r.Axis(b.DeviceIndex, b.Index);
        if (b.Invert) value = -value;
        if (b.Direction == 0) return (byte)Math.Max(0, Math.Min(255, (value + 32768) * 255 / 65535));
        return (byte)Math.Max(0, Math.Min(255, value * (b.Direction < 0 ? -1 : 1) * 255 / 32767));
    }

    // Combines every mapped control into one virtual pad. connected is true when at least one mapped device responds.
    // wheelOpen switches on the wheel-only controls. guard (optional, kept between calls) remembers controls pressed
    // while a wheel was open, so the button that made a selection does not also act in flight once the wheel closes.
    public static HotasPadState Compose(HotasMapFile map, IHotasReader reader, bool wheelOpen, HashSet<string> guard, out bool connected) {
        var state = new HotasPadState();
        connected = false;
        if (map == null) return state;
        var live = new Dictionary<int, bool>();
        foreach (var entry in map.Controls) {
            var b = entry.Value;
            bool up;
            if (!live.TryGetValue(b.DeviceIndex, out up)) live[b.DeviceIndex] = up = reader.Connected(b.DeviceIndex);
            if (!up) continue;
            connected = true;
            string name = entry.Key;
            if (name == WheelSelect || name == WheelCancel) {
                if (wheelOpen && Digital(b, reader)) state.Buttons |= (ushort)(name == WheelSelect ? 4096 : 8192);
                continue;
            }
            int bit;
            bool isButton = ButtonBits.TryGetValue(name, out bit);
            bool digitalSource = b.Kind != "Axis" || (!IsAnalog(name));
            if (guard != null && digitalSource && (isButton || name == "Left trigger" || name == "Right trigger")) {
                bool active = Digital(b, reader);
                if (wheelOpen) { if (active) guard.Add(name); }
                else if (guard.Contains(name)) { if (active) continue; guard.Remove(name); }
            }
            if (isButton) { if (Digital(b, reader)) state.Buttons |= (ushort)bit; continue; }
            switch (name) {
                case "Left trigger": state.LeftTrigger = Trigger(b, reader); break;
                case "Right trigger": state.RightTrigger = Trigger(b, reader); break;
                case "Left stick X": state.LeftX = Stick(name, b, reader); break;
                case "Left stick Y": state.LeftY = Stick(name, b, reader); break;
                case "Right stick X": state.RightX = Stick(name, b, reader); break;
                case "Right stick Y": state.RightY = Stick(name, b, reader); break;
            }
        }
        return state;
    }

    public static HotasPadState Compose(HotasMapFile map, IHotasReader reader, out bool connected) {
        return Compose(map, reader, false, null, out connected);
    }

    // Layers the HOTAS pad over a real gamepad: buttons combine, and the stronger analog value wins.
    public static HotasPadState Merge(HotasPadState pad, HotasPadState hotas) {
        pad.Buttons |= hotas.Buttons;
        pad.LeftTrigger = Math.Max(pad.LeftTrigger, hotas.LeftTrigger);
        pad.RightTrigger = Math.Max(pad.RightTrigger, hotas.RightTrigger);
        pad.LeftX = Stronger(pad.LeftX, hotas.LeftX);
        pad.LeftY = Stronger(pad.LeftY, hotas.LeftY);
        pad.RightX = Stronger(pad.RightX, hotas.RightX);
        pad.RightY = Stronger(pad.RightY, hotas.RightY);
        return pad;
    }
    static short Stronger(short a, short b) { return Math.Abs((int)b) > Math.Abs((int)a) ? b : a; }

    sealed class FakeReader : IHotasReader {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public readonly HashSet<int> Offline = new HashSet<int>();
        public bool Connected(int device) { return !Offline.Contains(device); }
        int Get(string key) { int v; return Values.TryGetValue(key, out v) ? v : 0; }
        public int Axis(int d, int i) { return Get("a" + d + "." + i); }
        public bool Button(int d, int i) { return Get("b" + d + "." + i) != 0; }
        public int Hat(int d, int i) { return Get("h" + d + "." + i); }
    }

    public static bool SelfTest() {
        var map = new HotasMapFile {
            Version = 1,
            Devices = new List<HotasMapDevice> { new HotasMapDevice { Guid = new string('A', 32) }, new HotasMapDevice { Guid = new string('B', 32) } },
            Controls = new Dictionary<string, HotasMapBinding> {
                {"A", new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=3}},
                {"Dpad up", new HotasMapBinding{DeviceIndex=0,Kind="Hat",Index=0,Direction=HatUp}},
                {"Dpad right", new HotasMapBinding{DeviceIndex=0,Kind="Hat",Index=0,Direction=HatRight}},
                {"LB", new HotasMapBinding{DeviceIndex=0,Kind="Axis",Index=2,Direction=1}},
                {"Right trigger", new HotasMapBinding{DeviceIndex=1,Kind="Axis",Index=0,Direction=0}},
                {"Left trigger", new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=9}},
                {"Left stick Y", new HotasMapBinding{DeviceIndex=0,Kind="Axis",Index=1}},
                {"Right stick X", new HotasMapBinding{DeviceIndex=1,Kind="Axis",Index=1,Invert=true}}
            }
        };
        if (!HotasMapFile.Check(map)) return false;
        var json = new JavaScriptSerializer();
        if (!HotasMapFile.Check(json.Deserialize<HotasMapFile>(json.Serialize(map)))) return false;
        map.Controls["Bogus"] = map.Controls["A"];
        if (HotasMapFile.Check(map)) return false;
        map.Controls.Remove("Bogus");

        var r = new FakeReader();
        bool connected;
        var idle = Compose(map, r, out connected);
        // A centred hat, an unpressed button and a stick at rest give nothing.
        if (!connected || idle.Buttons != 0 || idle.LeftY != 0) return false;
        r.Values["b0.3"] = 1;
        r.Values["h0.0"] = HatUp | HatRight;
        r.Values["a0.2"] = 20000;
        r.Values["a0.1"] = -32768;
        r.Values["a1.0"] = 32767;
        r.Values["a1.1"] = 10000;
        r.Values["b0.9"] = 1;
        var s = Compose(map, r, out connected);
        if (s.Buttons != (4096 | 1 | 8 | 256)) return false;
        // Forward on the SDL Y axis becomes up on the virtual stick.
        if (s.LeftY != 32767) return false;
        if (s.RightTrigger != 255 || s.LeftTrigger != 255) return false;
        // Invert flips an X axis; Y inverts by default, so an inverted Y would not.
        if (s.RightX != -10000) return false;
        r.Values["a0.2"] = 10000;
        if ((Compose(map, r, out connected).Buttons & 256) != 0) return false;
        // An unplugged device drops only its own controls.
        r.Offline.Add(0);
        var partial = Compose(map, r, out connected);
        if (!connected || partial.Buttons != 0 || partial.RightTrigger != 255) return false;
        r.Offline.Add(1);
        Compose(map, r, out connected);
        if (connected) return false;
        // One physical button can fire in flight and confirm in a wheel, without leaking either way.
        var wmap = new HotasMapFile {
            Version = 1, Devices = new List<HotasMapDevice> { new HotasMapDevice { Guid = new string('C', 32) } },
            Controls = new Dictionary<string, HotasMapBinding> {
                {"Right trigger", new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=0}},
                {WheelSelect, new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=0}},
                {WheelCancel, new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=1}}}
        };
        if (!HotasMapFile.Check(wmap)) return false;
        var wr = new FakeReader(); var guard = new HashSet<string>();
        wr.Values["b0.0"] = 1;
        var inFlight = Compose(wmap, wr, false, guard, out connected);
        if (inFlight.RightTrigger != 255 || (inFlight.Buttons & 4096) != 0) return false;   // fires, does not confirm
        var inWheel = Compose(wmap, wr, true, guard, out connected);
        if ((inWheel.Buttons & 4096) == 0) return false;                                      // confirms in the wheel
        wr.Values["b0.1"] = 1;
        if ((Compose(wmap, wr, true, guard, out connected).Buttons & 8192) == 0) return false; // second button cancels
        var afterClose = Compose(wmap, wr, false, guard, out connected);
        if (afterClose.RightTrigger != 0 || afterClose.Buttons != 0) return false;           // still held: no stray shot
        wr.Values["b0.0"] = 0; wr.Values["b0.1"] = 0;
        Compose(wmap, wr, false, guard, out connected);
        wr.Values["b0.0"] = 1;
        if (Compose(wmap, wr, false, guard, out connected).RightTrigger != 255) return false; // fires again after release

        // Layering over a real gamepad keeps both.
        var merged = Merge(new HotasPadState { Buttons = 4096, LeftX = 500, RightX = -20000 },
            new HotasPadState { Buttons = 8192, LeftX = -900, RightX = 100 });
        return merged.Buttons == (4096 | 8192) && merged.LeftX == -900 && merged.RightX == -20000;
    }
}

// Reads joysticks through the SDL3 library that ships with FotG 2's engine.
sealed class HotasJoysticks : IHotasReader, IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Guid16 { public ulong A, B; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Init")] [return: MarshalAs(UnmanagedType.I1)] static extern bool Init(uint flags);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")] static extern void Quit();
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")] static extern void Free(IntPtr p);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoysticks")] static extern IntPtr List(out int count);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickGUIDForID")] static extern Guid16 GuidFor(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GUIDToString")] static extern void GuidText(Guid16 guid, StringBuilder text, int length);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_OpenJoystick")] static extern IntPtr Open(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_CloseJoystick")] static extern void Close(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickSerial")] static extern IntPtr Serial(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickConnected")] [return: MarshalAs(UnmanagedType.I1)] static extern bool IsConnected(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickAxis")] static extern short GetAxis(IntPtr joystick, int index);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickButton")] [return: MarshalAs(UnmanagedType.I1)] static extern bool GetButton(IntPtr joystick, int index);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickHat")] static extern byte GetHat(IntPtr joystick, int index);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_PumpEvents")] static extern void Pump();

    IntPtr[] handles = new IntPtr[0];
    HotasMapFile map;
    bool started;

    public HotasJoysticks(string engineFolder) {
        if (!SetDllDirectory(engineFolder)) throw new InvalidOperationException("Could not select the FotG 2 engine folder.");
        try {
            if (!Init(0x200)) throw new InvalidOperationException("SDL3 could not initialize joystick input.");
            started = true;
        } finally { SetDllDirectory(null); }
    }

    // Opens whichever mapped devices are currently plugged in. Safe to call repeatedly for hot-plug.
    public void Attach(HotasMapFile newMap) {
        map = newMap;
        if (handles.Length != newMap.Devices.Count) {
            CloseAll();
            handles = new IntPtr[newMap.Devices.Count];
        }
        int count;
        IntPtr ids = List(out count);
        if (ids == IntPtr.Zero) return;
        try {
            var taken = new HashSet<uint>();
            // Devices that are still open keep their identity; the others look for an unclaimed match.
            for (int i = 0; i < handles.Length; i++)
                if (handles[i] != IntPtr.Zero && !IsConnected(handles[i])) { Close(handles[i]); handles[i] = IntPtr.Zero; }
            var live = new List<uint>();
            for (int i = 0; i < count; i++) live.Add((uint)Marshal.ReadInt32(ids, i * 4));
            for (int i = 0; i < handles.Length; i++) {
                if (handles[i] != IntPtr.Zero) continue;
                foreach (uint id in live) {
                    if (taken.Contains(id)) continue;
                    var text = new StringBuilder(64);
                    GuidText(GuidFor(id), text, text.Capacity);
                    if (!String.Equals(text.ToString(), map.Devices[i].Guid, StringComparison.OrdinalIgnoreCase)) continue;
                    IntPtr h = Open(id);
                    if (h == IntPtr.Zero) continue;
                    string serial = Marshal.PtrToStringAnsi(Serial(h));
                    if (!String.IsNullOrEmpty(map.Devices[i].Serial) && serial != map.Devices[i].Serial) { Close(h); continue; }
                    handles[i] = h;
                    taken.Add(id);
                    break;
                }
            }
        } finally { Free(ids); }
    }

    public void Poll() { if (started) Pump(); }
    public bool Connected(int d) { return d >= 0 && d < handles.Length && handles[d] != IntPtr.Zero && IsConnected(handles[d]); }
    public int Axis(int d, int i) { return Connected(d) ? GetAxis(handles[d], i) : 0; }
    public bool Button(int d, int i) { return Connected(d) && GetButton(handles[d], i); }
    public int Hat(int d, int i) { return Connected(d) ? GetHat(handles[d], i) & 15 : 0; }

    void CloseAll() {
        foreach (IntPtr h in handles) if (h != IntPtr.Zero) Close(h);
        handles = new IntPtr[0];
    }

    public void Dispose() {
        CloseAll();
        if (started) { Quit(); started = false; }
    }
}
