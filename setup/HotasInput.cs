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
    // Flight axes processed by Flight Control (precision mode): Pitch, Yaw, Roll, Absolute throttle, Relative throttle.
    public Dictionary<string, HotasFlightAxis> Flight { get; set; }

    public static readonly string[] FlightNames = { "Pitch", "Yaw", "Roll", "Absolute throttle", "Relative throttle" };

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
        if (map.Flight != null)
            foreach (var entry in map.Flight) {
                var f = entry.Value;
                if (Array.IndexOf(FlightNames, entry.Key) < 0 || f == null || f.DeviceIndex < 0 || f.DeviceIndex >= map.Devices.Count ||
                    f.Index < 0 || f.Index > 255 || f.Shape == null || !f.Shape.Valid()) return false;
            }
        foreach (var entry in map.Controls) if (entry.Value != null && entry.Value.Shape != null && !entry.Value.Shape.Valid()) return false;
        foreach (var entry in map.Controls) {
            var b = entry.Value;
            if (!HotasPad.IsVirtualControl(entry.Key) || b == null || b.DeviceIndex < 0 || b.DeviceIndex >= map.Devices.Count ||
                b.Index < 0 || (b.Kind != "Button" && b.Kind != "Hat" && b.Kind != "Axis")) return false;
        }
        return map.Devices.All(d => d != null && !String.IsNullOrEmpty(d.Guid));
    }
}

// How an axis is shaped before it is used: calibration (where it rests and how far it travels), dead zone, saturation
// and curve. Calibration values of 0 mean "not calibrated": the full range with the centre at zero is used.
sealed class HotasAxisSettings {
    public double Deadzone = 0.05;     // 0 to 0.9 of the travel, around the centre (or at both ends of a throttle)
    public double Saturation = 1.0;    // 0.1 to 1: the point of travel that already counts as full deflection
    public double Curve = 0.0;         // -1 to 1: softer (positive) or sharper (negative) around the centre
    public int CalMin, CalCenter, CalMax;
    public int Noise;                  // peak-to-peak wobble measured at rest during calibration (raw units); informational
    public bool Invert;

    public bool Valid() {
        return !Double.IsNaN(Deadzone + Saturation + Curve) && Deadzone >= 0 && Deadzone <= 0.9 && Saturation >= 0.1 && Saturation <= 1.0 &&
            Curve >= -1 && Curve <= 1 && CalMin >= -32768 && CalMax <= 32767 && CalMin <= CalMax && CalCenter >= -32768 && CalCenter <= 32767 &&
            (CalMin == 0 && CalCenter == 0 && CalMax == 0 || (CalMax - CalMin >= 1024 && CalCenter >= CalMin && CalCenter <= CalMax));
    }
    public bool Calibrated { get { return CalMax > CalMin; } }
    public HotasAxisSettings Copy() {
        return new HotasAxisSettings { Deadzone = Deadzone, Saturation = Saturation, Curve = Curve, CalMin = CalMin, CalCenter = CalCenter, CalMax = CalMax, Noise = Noise, Invert = Invert };
    }
}

sealed class HotasFlightAxis {
    public int DeviceIndex { get; set; }
    public int Index { get; set; }
    public HotasAxisSettings Shape { get; set; }
}

static class HotasAxisMath {
    static double Clamp(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }

    static void Range(HotasAxisSettings s, out int min, out int center, out int max) {
        if (s.Calibrated && s.CalCenter >= s.CalMin && s.CalCenter <= s.CalMax) { min = s.CalMin; center = s.CalCenter; max = s.CalMax; }
        else { min = -32768; center = 0; max = 32767; }
    }

    // A stick axis: -1 to 1, 0 inside the dead zone, the same sign as the raw reading (before Invert).
    public static double Bipolar(int raw, HotasAxisSettings s) {
        int min, center, max;
        Range(s, out min, out center, out max);
        double x = raw >= center ? (max == center ? 0 : (raw - center) / (double)(max - center)) : (min == center ? 0 : (raw - center) / (double)(center - min));
        x = Clamp(x, -1, 1);
        if (s.Invert) x = -x;
        double a = Math.Abs(x), dz = Clamp(s.Deadzone, 0, 0.9);
        if (a <= dz) return 0;
        a = (a - dz) / (1 - dz);
        a = Math.Min(1, a / Clamp(s.Saturation, 0.1, 1));
        a = Math.Pow(a, Math.Pow(2, Clamp(s.Curve, -1, 1)));
        return Math.Sign(x) * a;
    }

    // A lever or trigger: 0 at the low raw end to 1 at the high raw end (Invert swaps them). The dead zone sits at both ends.
    public static double Unipolar(int raw, HotasAxisSettings s) {
        int min, center, max;
        Range(s, out min, out center, out max);
        double u = max == min ? 0 : Clamp((raw - min) / (double)(max - min), 0, 1);
        if (s.Invert) u = 1 - u;
        double dz = Clamp(s.Deadzone, 0, 0.45);
        if (u <= dz) return 0;
        if (u >= 1 - dz) return 1;
        u = (u - dz) / (1 - 2 * dz);
        return Math.Pow(u, Math.Pow(2, Clamp(s.Curve, -1, 1)));
    }

    // The game's own absolute throttle: full throttle with the lever pushed to the low raw end (forward on most levers).
    public static double Throttle(int raw, HotasAxisSettings s) {
        var flipped = s.Copy();
        flipped.Invert = !s.Invert;
        return Unipolar(raw, flipped);
    }
}

// What Flight Control's own axis processing hands to the game script each tick. Values are thousandths:
// pitch, yaw and roll -1000..1000, absolute throttle 0..1000, relative throttle -1000..1000.
struct HotasFlightState {
    public int Pitch, Yaw, Roll, Throttle, Mask;     // Mask: 1 pitch, 2 yaw, 4 roll, 8 absolute throttle, 16 relative throttle (valid right now)
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
    // Optional shaping for a stick or trigger axis; absent means the raw reading is used as before.
    public HotasAxisSettings Shape { get; set; }
}

interface IHotasReader {
    bool Connected(int device);
    int Axis(int device, int index);
    bool Button(int device, int index);
    int Hat(int device, int index);
    // Button presses since the last call, counted from events so a pulse shorter than a poll interval is not lost.
    int Presses(int device, int index);
}

// What the virtual gamepad remembers between polls.
sealed class HotasPadMemory {
    // Controls pressed while a wheel was open; ignored in flight until released.
    public readonly HashSet<string> Guard = new HashSet<string>();
    // Encoder steps waiting to be sent, one per tick with a quiet tick between so each is a separate press.
    public readonly Dictionary<string, int> PendingSteps = new Dictionary<string, int>();
    public readonly HashSet<string> StepGap = new HashSet<string>();
    public readonly Dictionary<string, bool> WasDown = new Dictionary<string, bool>();
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
    // An encoder turned one way or the other moves the highlight around the wheel (button bits 1024 and 2048).
    public const string WheelStepCw = "Wheel step clockwise", WheelStepCcw = "Wheel step counter-clockwise";
    public const int StepCwBit = 1024, StepCcwBit = 2048, MaxPendingSteps = 16;
    public static readonly string[] WheelOnlyControls = { WheelSelect, WheelCancel, WheelStepCw, WheelStepCcw };

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
        if (b.Shape != null) {
            var shape = b.Shape.Copy();
            shape.Invert = b.Invert;
            double shaped = HotasAxisMath.Bipolar(value, shape);
            // SDL reports forward as negative; XInput reports up as positive, which the bridge expects.
            return Clamp((int)Math.Round((name.EndsWith("Y") ? -shaped : shaped) * 32767));
        }
        // SDL reports forward as negative; XInput reports up as positive, which the bridge expects.
        bool flip = b.Invert ^ name.EndsWith("Y");
        return Clamp(flip ? -value : value);
    }

    static byte Trigger(HotasMapBinding b, IHotasReader r) {
        if (b.Kind != "Axis") return Digital(b, r) ? (byte)255 : (byte)0;
        int value = r.Axis(b.DeviceIndex, b.Index);
        if (b.Shape != null && b.Direction == 0) {
            var shape = b.Shape.Copy();
            shape.Invert = b.Invert;
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(HotasAxisMath.Unipolar(value, shape) * 255)));
        }
        if (b.Invert) value = -value;
        if (b.Direction == 0) return (byte)Math.Max(0, Math.Min(255, (value + 32768) * 255 / 65535));
        return (byte)Math.Max(0, Math.Min(255, value * (b.Direction < 0 ? -1 : 1) * 255 / 32767));
    }

    // Combines every mapped control into one virtual pad. connected is true when at least one mapped device responds.
    // wheelOpen switches on the wheel-only controls. guard (optional, kept between calls) remembers controls pressed
    // while a wheel was open, so the button that made a selection does not also act in flight once the wheel closes.
    public static HotasPadState Compose(HotasMapFile map, IHotasReader reader, bool wheelOpen, HotasPadMemory memory, out bool connected) {
        HashSet<string> guard = memory == null ? null : memory.Guard;
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
            if (name == WheelStepCw || name == WheelStepCcw) {
                if (memory != null) Step(memory, name, b, reader, wheelOpen, ref state);
                continue;
            }
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

    // Counts this tick's detents (button-down events, or a state edge when no event arrived) and sends at most one step
    // per tick, with a quiet tick between steps. Steps queued while no wheel is open are dropped.
    static void Step(HotasPadMemory memory, string name, HotasMapBinding b, IHotasReader reader, bool wheelOpen, ref HotasPadState state) {
        int count = b.Kind == "Button" ? reader.Presses(b.DeviceIndex, b.Index) : 0;
        bool now = Digital(b, reader), was;
        memory.WasDown.TryGetValue(name, out was);
        memory.WasDown[name] = now;
        if (count == 0 && now && !was) count = 1;
        int pending;
        memory.PendingSteps.TryGetValue(name, out pending);
        if (!wheelOpen) { memory.PendingSteps[name] = 0; memory.StepGap.Remove(name); return; }
        pending = Math.Min(MaxPendingSteps, pending + count);
        if (memory.StepGap.Remove(name)) { memory.PendingSteps[name] = pending; return; }
        if (pending > 0) {
            pending--;
            state.Buttons |= (ushort)(name == WheelStepCw ? StepCwBit : StepCcwBit);
            memory.StepGap.Add(name);
        }
        memory.PendingSteps[name] = pending;
    }

    // Flight Control's own processing of the flight axes. An axis whose device is not connected is simply left out of Mask.
    public static HotasFlightState ComposeFlight(HotasMapFile map, IHotasReader reader) {
        var state = new HotasFlightState();
        if (map == null || map.Flight == null) return state;
        foreach (var entry in map.Flight) {
            var f = entry.Value;
            if (!reader.Connected(f.DeviceIndex)) continue;
            int raw = reader.Axis(f.DeviceIndex, f.Index);
            switch (entry.Key) {
                case "Pitch": state.Pitch = (int)Math.Round(HotasAxisMath.Bipolar(raw, f.Shape) * 1000); state.Mask |= 1; break;
                case "Yaw": state.Yaw = (int)Math.Round(HotasAxisMath.Bipolar(raw, f.Shape) * 1000); state.Mask |= 2; break;
                case "Roll": state.Roll = (int)Math.Round(HotasAxisMath.Bipolar(raw, f.Shape) * 1000); state.Mask |= 4; break;
                case "Absolute throttle": state.Throttle = (int)Math.Round(HotasAxisMath.Throttle(raw, f.Shape) * 1000); state.Mask |= 8; state.Mask &= ~16; break;
                case "Relative throttle":
                    if ((state.Mask & 8) != 0) break;   // one throttle at a time: absolute wins
                    state.Throttle = (int)Math.Round(HotasAxisMath.Bipolar(raw, f.Shape) * 1000); state.Mask |= 16; break;
            }
        }
        return state;
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
        public int Presses(int d, int i) { int n = Get("p" + d + "." + i); Values["p" + d + "." + i] = 0; return n; }
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
        // Axis shaping: calibration, dead zone, saturation, curve, invert.
        var plain = new HotasAxisSettings { Deadzone = 0, Saturation = 1, Curve = 0 };
        if (HotasAxisMath.Bipolar(0, plain) != 0 || Math.Abs(HotasAxisMath.Bipolar(32767, plain) - 1) > 1e-9 || Math.Abs(HotasAxisMath.Bipolar(-32768, plain) + 1) > 1e-9) return false;
        var dz = new HotasAxisSettings { Deadzone = 0.2, Saturation = 1, Curve = 0 };
        if (HotasAxisMath.Bipolar(5000, dz) != 0 || HotasAxisMath.Bipolar(-6000, dz) != 0) return false;                 // inside the dead zone
        if (Math.Abs(HotasAxisMath.Bipolar(32767, dz) - 1) > 1e-9) return false;                                         // full travel is still full
        double justOut = HotasAxisMath.Bipolar(6600, dz);
        if (justOut <= 0 || justOut > 0.01) return false;                                                                // starts from zero, no jump
        var sat = new HotasAxisSettings { Deadzone = 0, Saturation = 0.5, Curve = 0 };
        if (HotasAxisMath.Bipolar(16384, sat) < 0.999 || HotasAxisMath.Bipolar(-20000, sat) > -0.999) return false;       // half travel already counts as full
        var soft = new HotasAxisSettings { Deadzone = 0, Saturation = 1, Curve = 1 };
        var sharp = new HotasAxisSettings { Deadzone = 0, Saturation = 1, Curve = -1 };
        if (!(HotasAxisMath.Bipolar(16384, soft) < 0.3 && HotasAxisMath.Bipolar(16384, sharp) > 0.65)) return false;     // positive curve is gentler at the centre
        if (Math.Abs(HotasAxisMath.Bipolar(16384, soft) + HotasAxisMath.Bipolar(-16384, soft)) > 1e-3) return false;      // symmetric
        var inv = new HotasAxisSettings { Deadzone = 0, Saturation = 1, Curve = 0, Invert = true };
        if (HotasAxisMath.Bipolar(10000, inv) >= 0) return false;
        // A worn stick that rests at 3000 and only reaches -20000 .. 28000 is mapped back to a full, centred range.
        var worn = new HotasAxisSettings { Deadzone = 0, Saturation = 1, Curve = 0, CalMin = -20000, CalCenter = 3000, CalMax = 28000 };
        if (!worn.Valid() || HotasAxisMath.Bipolar(3000, worn) != 0 || Math.Abs(HotasAxisMath.Bipolar(28000, worn) - 1) > 1e-9 || Math.Abs(HotasAxisMath.Bipolar(-20000, worn) + 1) > 1e-9) return false;
        if (HotasAxisMath.Bipolar(31000, worn) != 1 || HotasAxisMath.Bipolar(-25000, worn) != -1) return false;           // beyond the calibrated range stays clamped
        // Levers: forward (low raw) is full throttle, like the game's own axis; Invert swaps; dead zones sit at both ends.
        var lever = new HotasAxisSettings { Deadzone = 0.05, Saturation = 1, Curve = 0 };
        if (HotasAxisMath.Throttle(-32768, lever) != 1 || HotasAxisMath.Throttle(32767, lever) != 0) return false;
        double mid = HotasAxisMath.Throttle(0, lever);
        if (mid < 0.49 || mid > 0.51) return false;
        var leverInverted = lever.Copy(); leverInverted.Invert = true;
        if (HotasAxisMath.Throttle(-32768, leverInverted) != 0 || HotasAxisMath.Throttle(32767, leverInverted) != 1) return false;
        var shortLever = new HotasAxisSettings { Deadzone = 0, Saturation = 1, Curve = 0, CalMin = -12000, CalCenter = 0, CalMax = 20000 };
        if (HotasAxisMath.Throttle(-12000, shortLever) != 1 || HotasAxisMath.Throttle(20000, shortLever) != 0) return false;
        // Invalid settings are refused.
        if (new HotasAxisSettings { Deadzone = 0.95 }.Valid() || new HotasAxisSettings { Saturation = 0.05 }.Valid() || new HotasAxisSettings { CalMin = 100, CalCenter = 100, CalMax = 200 }.Valid()) return false;
        if (!new HotasAxisSettings().Valid()) return false;
        // Flight axes through the map: pitch, yaw, roll and a throttle, with a missing device left out of the mask.
        var fmap = new HotasMapFile {
            Version = 1, Devices = new List<HotasMapDevice> { new HotasMapDevice { Guid = new string('E', 32) }, new HotasMapDevice { Guid = new string('F', 32) } },
            Controls = new Dictionary<string, HotasMapBinding>(),
            Flight = new Dictionary<string, HotasFlightAxis> {
                {"Pitch", new HotasFlightAxis{DeviceIndex=0,Index=1,Shape=new HotasAxisSettings{Deadzone=0}}},
                {"Yaw", new HotasFlightAxis{DeviceIndex=1,Index=0,Shape=new HotasAxisSettings{Deadzone=0,Invert=true}}},
                {"Roll", new HotasFlightAxis{DeviceIndex=0,Index=0,Shape=new HotasAxisSettings{Deadzone=0.1}}},
                {"Absolute throttle", new HotasFlightAxis{DeviceIndex=0,Index=3,Shape=new HotasAxisSettings{Deadzone=0}}},
                {"Relative throttle", new HotasFlightAxis{DeviceIndex=0,Index=2,Shape=new HotasAxisSettings{Deadzone=0}}}}
        };
        if (!HotasMapFile.Check(fmap)) return false;
        if (!HotasMapFile.Check(json.Deserialize<HotasMapFile>(json.Serialize(fmap)))) return false;
        var fr = new FakeReader();
        fr.Values["a0.1"] = -16384; fr.Values["a1.0"] = 8192; fr.Values["a0.0"] = 1000; fr.Values["a0.3"] = -32768; fr.Values["a0.2"] = 32767;
        var flight = ComposeFlight(fmap, fr);
        if (flight.Mask != (1 | 2 | 4 | 8) || flight.Pitch > -499 || flight.Pitch < -501 || flight.Yaw > -249 || flight.Yaw < -251 || flight.Roll != 0 || flight.Throttle != 1000) return false;
        fr.Offline.Add(1);
        var partialFlight = ComposeFlight(fmap, fr);
        if ((partialFlight.Mask & 2) != 0 || partialFlight.Yaw != 0 || (partialFlight.Mask & 1) == 0) return false;
        var onlyRelative = new HotasMapFile { Version = 1, Devices = fmap.Devices, Controls = fmap.Controls, Flight = new Dictionary<string, HotasFlightAxis> { { "Relative throttle", fmap.Flight["Relative throttle"] } } };
        var rel = ComposeFlight(onlyRelative, fr);
        if (rel.Mask != 16 || rel.Throttle != 1000) return false;
        fmap.Flight["Pitch"].Shape.Deadzone = 5;
        if (HotasMapFile.Check(fmap)) return false;
        fmap.Flight["Pitch"].Shape.Deadzone = 0;
        // A stick used for wheel aiming can be shaped too; without Shape it behaves as before.
        var smap = new HotasMapFile {
            Version = 1, Devices = new List<HotasMapDevice> { new HotasMapDevice { Guid = new string('A', 32) } },
            Controls = new Dictionary<string, HotasMapBinding> {
                {"Right stick X", new HotasMapBinding{DeviceIndex=0,Kind="Axis",Index=0,Shape=new HotasAxisSettings{Deadzone=0.3}}},
                {"Right stick Y", new HotasMapBinding{DeviceIndex=0,Kind="Axis",Index=1}}}
        };
        var sr = new FakeReader(); sr.Values["a0.0"] = 6000; sr.Values["a0.1"] = 6000;
        bool sc;
        var shaped = Compose(smap, sr, out sc);
        if (shaped.RightX != 0 || shaped.RightY != -6000) return false;

        // One physical button can fire in flight and confirm in a wheel, without leaking either way.
        var wmap = new HotasMapFile {
            Version = 1, Devices = new List<HotasMapDevice> { new HotasMapDevice { Guid = new string('C', 32) } },
            Controls = new Dictionary<string, HotasMapBinding> {
                {"Right trigger", new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=0}},
                {WheelSelect, new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=0}},
                {WheelCancel, new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=1}}}
        };
        if (!HotasMapFile.Check(wmap)) return false;
        var wr = new FakeReader(); var guard = new HotasPadMemory();
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

        // An encoder: pulses become separate steps, only while a wheel is open, none lost to a fast spin.
        var emap = new HotasMapFile {
            Version = 1, Devices = new List<HotasMapDevice> { new HotasMapDevice { Guid = new string('D', 32) } },
            Controls = new Dictionary<string, HotasMapBinding> {
                {WheelStepCw, new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=10}},
                {WheelStepCcw, new HotasMapBinding{DeviceIndex=0,Kind="Button",Index=11}}}
        };
        if (!HotasMapFile.Check(emap)) return false;
        var er = new FakeReader(); var emem = new HotasPadMemory();
        er.Values["p0.10"] = 3;                                               // three clockwise detents inside one poll, button never seen down
        var e1 = Compose(emap, er, false, emem, out connected);
        if ((e1.Buttons & (StepCwBit | StepCcwBit)) != 0) return false;       // no wheel open: nothing is sent
        er.Values["p0.10"] = 3;
        var e2 = Compose(emap, er, true, emem, out connected);
        if ((e2.Buttons & StepCwBit) == 0 || (e2.Buttons & StepCcwBit) != 0) return false;
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCwBit) != 0) return false;   // quiet tick between steps
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCwBit) == 0) return false;   // second detent
        Compose(emap, er, true, emem, out connected);
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCwBit) == 0) return false;   // third detent
        Compose(emap, er, true, emem, out connected);
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCwBit) != 0) return false;   // and no more
        er.Values["p0.11"] = 1;
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCcwBit) == 0) return false;  // other direction
        Compose(emap, er, true, emem, out connected);
        // Without events, a plain press still steps once.
        er.Values["b0.10"] = 1;
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCwBit) == 0) return false;
        Compose(emap, er, true, emem, out connected);
        if ((Compose(emap, er, true, emem, out connected).Buttons & StepCwBit) != 0) return false;   // held: no repeat
        er.Values["p0.10"] = 100;
        Compose(emap, er, false, emem, out connected);                        // wheel closed: a flood is dropped
        er.Values["b0.10"] = 0;
        Compose(emap, er, true, emem, out connected);
        if (emem.PendingSteps[WheelStepCw] != 0) return false;

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
    uint[] deviceIds = new uint[0];
    readonly Dictionary<int, int> presses = new Dictionary<int, int>();
    readonly IntPtr eventBuffer = Marshal.AllocHGlobal(128);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_PollEvent")] [return: MarshalAs(UnmanagedType.I1)] static extern bool PollEvent(IntPtr e);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_PushEvent")] [return: MarshalAs(UnmanagedType.I1)] static extern bool PushEvent(IntPtr e);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickID")] static extern uint GetId(IntPtr joystick);
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
            deviceIds = new uint[newMap.Devices.Count];
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
                    deviceIds[i] = GetId(h);
                    taken.Add(id);
                    break;
                }
            }
        } finally { Free(ids); }
    }

    public void Poll() {
        if (!started) return;
        Pump();
        // Drain the queue and count button presses per device, so a very short pulse (an encoder detent) is not missed.
        while (PollEvent(eventBuffer)) {
            if ((uint)Marshal.ReadInt32(eventBuffer, 0) != 0x603) continue;     // SDL_EVENT_JOYSTICK_BUTTON_DOWN
            uint which = (uint)Marshal.ReadInt32(eventBuffer, 16);
            int button = Marshal.ReadByte(eventBuffer, 20);
            for (int d = 0; d < deviceIds.Length; d++)
                if (deviceIds[d] == which && handles[d] != IntPtr.Zero) {
                    int key = d * 4096 + button, n;
                    presses.TryGetValue(key, out n);
                    presses[key] = Math.Min(n + 1, 64);
                }
        }
    }

    public int Presses(int d, int i) {
        int key = d * 4096 + i, n;
        if (!presses.TryGetValue(key, out n)) return 0;
        presses.Remove(key);
        return n;
    }

    // Diagnostic: queue a button-down event for a mapped device, as if it had been pressed, to check event counting.
    public bool PushTestPress(int device, int button) {
        IntPtr e = Marshal.AllocHGlobal(128);
        try {
            for (int i = 0; i < 128; i += 8) Marshal.WriteInt64(e, i, 0);
            Marshal.WriteInt32(e, 0, 0x603);
            Marshal.WriteInt32(e, 16, (int)deviceIds[device]);
            Marshal.WriteByte(e, 20, (byte)button);
            Marshal.WriteByte(e, 21, 1);
            return PushEvent(e);
        } finally { Marshal.FreeHGlobal(e); }
    }
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
        Marshal.FreeHGlobal(eventBuffer);
    }
}
