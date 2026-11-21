using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Diagnostic mode. WheelBridge.exe --diagnose records what the devices report, what the bridge sends and what the game
// script sees while the player flies, then builds one ZIP the developer can read. Nothing leaves the PC by itself.
//
//   devices.txt    every joystick SDL3 sees (name, ids, GUID, axis/button/hat counts)
//   joystick.csv   raw axes, hats and buttons of every device, written when something changes
//   bridge.csv     what the bridge decided each moment: focus, game state, the shaped flight axes it sent
//   events.csv     markers (F9 problem, F8 lever fully forward, F7 lever fully back), focus and device changes
//   lua-*.csv      the game script's own numbers: the game's bindings, the throttle the ship really had
//   settings\      the HOTAS profile, map, control-table block and joystick lines of the game's ini (sanitized)
//   report.txt     a plain summary with automatic findings
sealed class DiagDevice {
    public int Index;
    public uint Id;
    public IntPtr Handle;
    public string Name = "", Guid = "";
    public int Vendor, Product, Axes, Buttons, Hats;
    public string LastRow = "";
    public long LastWrite = -100000;
}

sealed class DiagJoysticks : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct DiagGuid { public ulong A, B; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Init")] [return: MarshalAs(UnmanagedType.I1)] static extern bool Init(uint flags);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")] static extern void Quit();
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")] static extern void Free(IntPtr p);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoysticks")] static extern IntPtr List(out int count);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickNameForID")] static extern IntPtr NameFor(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickVendorForID")] static extern ushort VendorFor(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickProductForID")] static extern ushort ProductFor(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickGUIDForID")] static extern DiagGuid GuidFor(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GUIDToString")] static extern void GuidText(DiagGuid guid, StringBuilder text, int length);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_OpenJoystick")] static extern IntPtr Open(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_CloseJoystick")] static extern void Close(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickConnected")] [return: MarshalAs(UnmanagedType.I1)] static extern bool IsConnected(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetNumJoystickAxes")] static extern int NumAxes(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetNumJoystickButtons")] static extern int NumButtons(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetNumJoystickHats")] static extern int NumHats(IntPtr joystick);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickAxis")] static extern short GetAxis(IntPtr joystick, int index);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickButton")] [return: MarshalAs(UnmanagedType.I1)] static extern bool GetButton(IntPtr joystick, int index);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickHat")] static extern byte GetHat(IntPtr joystick, int index);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_PumpEvents")] static extern void Pump();

    readonly List<DiagDevice> devices = new List<DiagDevice>();
    bool started;
    public List<DiagDevice> Devices { get { return devices; } }

    public DiagJoysticks(string engineFolder) {
        if (!SetDllDirectory(engineFolder)) throw new InvalidOperationException("Could not select the engine folder.");
        try {
            if (!Init(0x200)) throw new InvalidOperationException("SDL3 could not initialize joystick input.");
            started = true;
        } finally { SetDllDirectory(null); }
    }

    static string Utf8(IntPtr p) {
        if (p == IntPtr.Zero) return "";
        int n = 0; while (Marshal.ReadByte(p, n) != 0 && n < 512) n++;
        byte[] bytes = new byte[n]; Marshal.Copy(p, bytes, 0, n);
        return Encoding.UTF8.GetString(bytes);
    }

    // Opens every joystick that is plugged in and not open yet. Returns the devices that were added.
    public List<DiagDevice> Refresh() {
        var added = new List<DiagDevice>();
        Pump();
        int count; IntPtr list = List(out count);
        if (list == IntPtr.Zero) return added;
        try {
            for (int i = 0; i < count; i++) {
                uint id = (uint)Marshal.ReadInt32(list, i * 4);
                if (devices.Any(d => d.Id == id && d.Handle != IntPtr.Zero && IsConnected(d.Handle))) continue;
                IntPtr handle = Open(id);
                if (handle == IntPtr.Zero) continue;
                var text = new StringBuilder(64); GuidText(GuidFor(id), text, 64);
                var device = new DiagDevice { Index = devices.Count, Id = id, Handle = handle, Name = Utf8(NameFor(id)), Guid = text.ToString(),
                    Vendor = VendorFor(id), Product = ProductFor(id), Axes = NumAxes(handle), Buttons = NumButtons(handle), Hats = NumHats(handle) };
                devices.Add(device); added.Add(device);
            }
        } finally { Free(list); }
        return added;
    }

    public void Poll() { Pump(); }
    public bool Connected(DiagDevice d) { return d.Handle != IntPtr.Zero && IsConnected(d.Handle); }

    // One line: axes;hats;buttons (two 64-bit hex words).
    public string Snapshot(DiagDevice d) {
        var axes = new StringBuilder();
        for (int i = 0; i < d.Axes; i++) { if (i > 0) axes.Append(';'); axes.Append(GetAxis(d.Handle, i)); }
        var hats = new StringBuilder();
        for (int i = 0; i < d.Hats; i++) { if (i > 0) hats.Append(';'); hats.Append(GetHat(d.Handle, i) & 15); }
        ulong lo = 0, hi = 0;
        for (int i = 0; i < d.Buttons && i < 128; i++) if (GetButton(d.Handle, i)) { if (i < 64) lo |= 1UL << i; else hi |= 1UL << (i - 64); }
        return axes + "," + hats + "," + hi.ToString("x") + lo.ToString("x16");
    }

    public void Dispose() {
        foreach (DiagDevice d in devices) if (d.Handle != IntPtr.Zero) Close(d.Handle);
        devices.Clear();
        if (started) { Quit(); started = false; }
    }
}

// Writes the capture files during a session.
sealed class DiagRecorder : IDisposable {
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    public readonly string Dir;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly StreamWriter joy, bridge, events;
    readonly DiagJoysticks pads;
    readonly bool[] keyDown = new bool[3];
    string lastBridgeRow = "";
    long lastBridgeWrite = -100000, nextScan;
    public long Now { get { return clock.ElapsedMilliseconds; } }

    public DiagRecorder(string dir, string engineFolder) {
        Dir = dir;
        Directory.CreateDirectory(dir);
        joy = Open("joystick.csv", "t_ms,dev,axes,hats,buttons_hi_lo");
        bridge = Open("bridge.csv", "t_ms,seq,focused,active,mode,state,hotas_up,pitch,yaw,roll,throttle,mask,buttons,rx,ry,lx,ly");
        events = Open("events.csv", "t_ms,type,text");
        pads = new DiagJoysticks(engineFolder);
        Rescan();
    }

    StreamWriter Open(string name, string header) {
        var w = new StreamWriter(Path.Combine(Dir, name), false, new UTF8Encoding(false));
        w.AutoFlush = true;
        w.WriteLine(header);
        return w;
    }

    public void Event(string type, string text) {
        events.WriteLine(Now + "," + type + "," + (text ?? "").Replace(',', ';').Replace('\r', ' ').Replace('\n', ' '));
    }

    void Rescan() {
        foreach (DiagDevice d in pads.Refresh())
            Event("device", "#" + d.Index + " " + d.Name + " axes=" + d.Axes + " buttons=" + d.Buttons + " hats=" + d.Hats);
        WriteDevices();
        nextScan = Now + 2000;
    }

    void WriteDevices() {
        var text = new StringBuilder("index|name|vendor:product|guid|axes|buttons|hats\r\n");
        foreach (DiagDevice d in pads.Devices)
            text.AppendLine(d.Index + "|" + d.Name + "|" + d.Vendor.ToString("x4") + ":" + d.Product.ToString("x4") + "|" + d.Guid + "|" + d.Axes + "|" + d.Buttons + "|" + d.Hats);
        File.WriteAllText(Path.Combine(Dir, "devices.txt"), text.ToString(), new UTF8Encoding(false));
    }

    // Called every bridge tick: devices, markers and one bridge row.
    public void Tick(int seq, bool focused, bool active, string mode, string state, bool hotasUp, int pitch, int yaw, int roll, int throttle, int mask,
                     int buttons, int rx, int ry, int lx, int ly) {
        long now = Now;
        if (now >= nextScan) Rescan();
        pads.Poll();
        foreach (DiagDevice d in pads.Devices) {
            if (!pads.Connected(d)) continue;
            string row = pads.Snapshot(d);
            if (row != d.LastRow || now - d.LastWrite >= 500) {
                joy.WriteLine(now + "," + d.Index + "," + row);
                d.LastRow = row; d.LastWrite = now;
            }
        }
        string[] names = { "problem", "forward", "back" };
        int[] keys = { 0x78, 0x77, 0x76 };   // F9, F8, F7
        for (int i = 0; i < 3; i++) {
            bool down = (GetAsyncKeyState(keys[i]) & 0x8000) != 0;
            if (down && !keyDown[i]) Event("marker", names[i]);
            keyDown[i] = down;
        }
        string bridgeRow = (focused ? 1 : 0) + "," + (active ? 1 : 0) + "," + mode + "," + state + "," + (hotasUp ? 1 : 0) + "," + pitch + "," + yaw + "," + roll + "," + throttle + "," + mask
            + "," + buttons + "," + rx + "," + ry + "," + lx + "," + ly;
        if (bridgeRow != lastBridgeRow || now - lastBridgeWrite >= 500) {
            bridge.WriteLine(now + "," + seq + "," + bridgeRow);
            lastBridgeRow = bridgeRow; lastBridgeWrite = now;
        }
    }

    public void Dispose() {
        try { joy.Dispose(); bridge.Dispose(); events.Dispose(); } catch { }
        pads.Dispose();
    }
}

static class DiagReport {
    // ---- parsing ----
    sealed class Sample { public long T; public short[] Axes; public string Buttons = ""; }
    sealed class Marker { public long T; public string Kind; }
    sealed class LuaRow { public long Seq; public double Thrust = double.NaN, Cruise = double.NaN, Speed = double.NaN; public string State = "", Mode = "", Mask = ""; public double AxisThrottle = double.NaN, Prec = double.NaN; }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static double Num(string s) { double v; return double.TryParse(s, NumberStyles.Float, Inv, out v) ? v : double.NaN; }
    static long Lng(string s) { long v; return long.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : -1; }

    static string[] Lines(string path) { return File.Exists(path) ? File.ReadAllLines(path) : new string[0]; }

    // Removes what identifies the player: Windows user name, computer name, serial numbers.
    internal static string Sanitize(string text) {
        if (text == null) return "";
        text = Regex.Replace(text, @"[A-Za-z]:\\Users\\[^\\/""'\r\n|]+", @"C:\Users\<user>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(""Serial""\s*:\s*)""[^""]*""", "$1\"\"");
        try { if (Environment.UserName.Length > 2) text = text.Replace(Environment.UserName, "<user>"); } catch { }
        try { if (Environment.MachineName.Length > 2) text = text.Replace(Environment.MachineName, "<pc>"); } catch { }
        return text;
    }

    static void ReadSession(string dir, out List<string[]> devices, out Dictionary<int, List<Sample>> samples, out List<Marker> markers,
                            out Dictionary<long, long> seqTime, out List<LuaRow> lua, out List<string> bindings, out long lastT, out double focusedShare, out int inactiveRuns) {
        devices = new List<string[]>();
        foreach (string line in Lines(Path.Combine(dir, "devices.txt")).Skip(1)) { string[] f = line.Split('|'); if (f.Length >= 7) devices.Add(f); }
        samples = new Dictionary<int, List<Sample>>();
        lastT = 0;
        foreach (string line in Lines(Path.Combine(dir, "joystick.csv")).Skip(1)) {
            string[] f = line.Split(',');
            if (f.Length < 5) continue;
            long t = Lng(f[0]); int dev = (int)Lng(f[1]);
            if (t < 0 || dev < 0) continue;
            string[] a = f[2].Length == 0 ? new string[0] : f[2].Split(';');
            var s = new Sample { T = t, Axes = a.Select(x => { short v; return short.TryParse(x, out v) ? v : (short)0; }).ToArray(), Buttons = f[4] };
            if (!samples.ContainsKey(dev)) samples[dev] = new List<Sample>();
            samples[dev].Add(s);
            if (t > lastT) lastT = t;
        }
        markers = new List<Marker>();
        foreach (string line in Lines(Path.Combine(dir, "events.csv")).Skip(1)) {
            string[] f = line.Split(new[] { ',' }, 3);
            if (f.Length == 3 && f[1] == "marker") markers.Add(new Marker { T = Lng(f[0]), Kind = f[2] });
        }
        seqTime = new Dictionary<long, long>();
        long focusedMs = 0, totalMs = 0, prevT = -1; bool prevFocused = false; inactiveRuns = 0; bool prevActive = true;
        foreach (string line in Lines(Path.Combine(dir, "bridge.csv")).Skip(1)) {
            string[] f = line.Split(',');
            if (f.Length < 6) continue;
            long t = Lng(f[0]), seq = Lng(f[1]);
            if (t < 0) continue;
            if (seq >= 0 && !seqTime.ContainsKey(seq)) seqTime[seq] = t;
            if (prevT >= 0) { totalMs += t - prevT; if (prevFocused) focusedMs += t - prevT; }
            prevT = t; prevFocused = f[2] == "1";
            bool active = f[3] == "1";
            if (prevActive && !active) inactiveRuns++;
            prevActive = active;
            if (t > lastT) lastT = t;
        }
        focusedShare = totalMs > 0 ? (double)focusedMs / totalMs : 0;
        lua = new List<LuaRow>(); bindings = new List<string>();
        foreach (string file in Directory.GetFiles(dir, "lua-*.csv").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            foreach (string line in Lines(file)) {
                string[] f = line.Split('|');
                if (f.Length >= 3 && f[0] == "B") bindings.Add(f[1] + " = " + f[2]);
                else if (f.Length >= 11 && f[0] == "S") lua.Add(new LuaRow { Seq = Lng(f[1]), State = f[3], Mode = f[4], Mask = f[5], AxisThrottle = Num(f[6]), Cruise = Num(f[7]), Prec = Num(f[8]), Speed = Num(f[9]), Thrust = Num(f[10]) });
            }
    }

    static short ValueAt(List<Sample> list, int axis, long t) {
        int lo = 0, hi = list.Count - 1, best = -1;
        while (lo <= hi) { int mid = (lo + hi) / 2; if (list[mid].T <= t) { best = mid; lo = mid + 1; } else hi = mid - 1; }
        if (best < 0) best = 0;
        return axis < list[best].Axes.Length ? list[best].Axes[axis] : (short)0;
    }

    static double Pearson(List<double> x, List<double> y) {
        int n = x.Count; if (n < 8) return double.NaN;
        double mx = x.Average(), my = y.Average(), sxx = 0, syy = 0, sxy = 0;
        for (int i = 0; i < n; i++) { double dx = x[i] - mx, dy = y[i] - my; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; }
        return sxx < 1e-9 || syy < 1e-9 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    static double ShipThrottle(LuaRow r) {
        if (!double.IsNaN(r.Thrust)) return r.Thrust * 100.0;      // the ship's real forward thrust, 0 to 1
        if (!double.IsNaN(r.Cruise)) return r.Cruise;              // the throttle the script saw, 0 to 100
        return r.Speed;
    }

    // ---- the report ----
    public static string Build(string dir, string settingsSummary, string environment) {
        List<string[]> devices; Dictionary<int, List<Sample>> samples; List<Marker> markers; Dictionary<long, long> seqTime; List<LuaRow> lua; List<string> bindings;
        long lastT; double focusedShare; int inactiveRuns;
        ReadSession(dir, out devices, out samples, out markers, out seqTime, out lua, out bindings, out lastT, out focusedShare, out inactiveRuns);

        var findings = new List<string>();
        var detail = new StringBuilder();
        var flightLua = lua.Where(r => r.State.IndexOf("FLIGHT", StringComparison.OrdinalIgnoreCase) >= 0 || r.State.IndexOf("GS_STATE_GAME_PLAY", StringComparison.OrdinalIgnoreCase) >= 0 || r.State.Length == 0).ToList();
        if (lua.Count == 0) findings.Add("The game script wrote no numbers, so there is nothing from inside the game. Was the mission flown, and was the game started by this diagnostic?");
        if (devices.Count == 0) findings.Add("No joystick was seen by SDL3. The game's engine cannot see the device either.");
        if (focusedShare < 0.2 && lastT > 5000) findings.Add("The game window was in front only " + (int)(focusedShare * 100) + "% of the session, so the bridge held its output most of the time.");
        if (inactiveRuns > 3) findings.Add("The bridge stopped sending input " + inactiveRuns + " times (window focus or a dialog). That can look like the controls stopping.");

        // Which axes moved, and what the ship's throttle did.
        var movers = new List<string>();
        foreach (var pair in samples.OrderBy(p => p.Key)) {
            int dev = pair.Key; var list = pair.Value;
            string name = dev < devices.Count ? devices[dev][1] : "device " + dev;
            int axes = list.Max(s => s.Axes.Length);
            for (int a = 0; a < axes; a++) {
                int min = list.Min(s => a < s.Axes.Length ? (int)s.Axes[a] : 0), max = list.Max(s => a < s.Axes.Length ? (int)s.Axes[a] : 0);
                if (max - min < 4000) continue;
                string line = "  #" + dev + " " + name + " axis " + a + ": raw " + min + " to " + max + ", first " + list[0].Axes[Math.Min(a, list[0].Axes.Length - 1)] + ", last " + list[list.Count - 1].Axes[Math.Min(a, list[list.Count - 1].Axes.Length - 1)];
                // correlation with the ship's throttle
                var xs = new List<double>(); var ys = new List<double>();
                foreach (LuaRow r in flightLua) {
                    long t; if (!seqTime.TryGetValue(r.Seq, out t)) continue;
                    double ship = ShipThrottle(r); if (double.IsNaN(ship)) continue;
                    xs.Add(ValueAt(list, a, t)); ys.Add(ship);
                }
                double c = Pearson(xs, ys);
                if (!double.IsNaN(c)) line += ", follows the ship's throttle with correlation " + c.ToString("0.00", Inv) + (c <= -0.6 ? " (raw LOW = more throttle, the game's normal direction)" : c >= 0.6 ? " (raw HIGH = more throttle, an inverted lever)" : "");
                movers.Add(line);
            }
        }

        // Markers F8 (lever fully forward) and F7 (fully back): what the ship's throttle was at those moments.
        Func<long, double> shipAt = delegate(long t) {
            double best = double.NaN; long bestGap = long.MaxValue;
            foreach (LuaRow r in flightLua) { long rt; if (!seqTime.TryGetValue(r.Seq, out rt)) continue; long gap = Math.Abs(rt - t); if (gap < bestGap) { bestGap = gap; best = ShipThrottle(r); } }
            return bestGap <= 1500 ? best : double.NaN;
        };
        var fwd = markers.Where(m => m.Kind == "forward").ToList(); var back = markers.Where(m => m.Kind == "back").ToList();
        if (fwd.Count > 0 && back.Count > 0) {
            double sf = shipAt(fwd[fwd.Count - 1].T), sb = shipAt(back[back.Count - 1].T);
            detail.AppendLine("Lever marked fully forward (F8) at " + fwd[fwd.Count - 1].T + " ms: ship throttle " + Fmt(sf) + "%. Marked fully back (F7) at " + back[back.Count - 1].T + " ms: ship throttle " + Fmt(sb) + "%.");
            if (!double.IsNaN(sf) && !double.IsNaN(sb)) {
                if (sf + 15 < sb) findings.Add("PUSHING THE LEVER FORWARD GAVE LESS THROTTLE (" + Fmt(sf) + "% forward, " + Fmt(sb) + "% back). The lever reads the wrong way round: tick \"Invert this axis\" on the throttle in Axes... (or choose the other end in the game).");
                else if (sf > sb + 15) findings.Add("The throttle direction is right: forward gave " + Fmt(sf) + "% and back " + Fmt(sb) + "%.");
                else findings.Add("The lever did not change the ship's throttle between forward (" + Fmt(sf) + "%) and back (" + Fmt(sb) + "%). The game is not using this lever for throttle.");
            }
            foreach (var pair in samples) {
                int dev = pair.Key; var list = pair.Value; int axes = list.Max(s => s.Axes.Length);
                for (int a = 0; a < axes; a++) {
                    int vf = ValueAt(list, a, fwd[fwd.Count - 1].T), vb = ValueAt(list, a, back[back.Count - 1].T);
                    if (Math.Abs(vf - vb) < 8000) continue;
                    detail.AppendLine("  #" + dev + " axis " + a + ": raw " + vf + " when forward, " + vb + " when back, so physical FORWARD is raw " + (vf > vb ? "HIGH" : "LOW") + ".");
                }
            }
        } else if (flightLua.Count > 0) findings.Add("Tip: next time press F8 with the lever fully forward and F7 fully back, and F9 when something goes wrong. It makes the report much clearer.");

        foreach (Marker m in markers.Where(x => x.Kind == "problem")) {
            detail.AppendLine("Problem marker (F9) at " + m.T + " ms:");
            foreach (var pair in samples.OrderBy(p => p.Key)) {
                int axes = pair.Value.Max(s => s.Axes.Length);
                for (int a = 0; a < axes; a++) {
                    int min = pair.Value.Min(s => a < s.Axes.Length ? (int)s.Axes[a] : 0), max = pair.Value.Max(s => a < s.Axes.Length ? (int)s.Axes[a] : 0);
                    if (max - min < 4000) continue;
                    detail.AppendLine("    #" + pair.Key + " axis " + a + ": " + ValueAt(pair.Value, a, m.T - 2000) + " (2 s before), " + ValueAt(pair.Value, a, m.T) + " (at the marker), " + ValueAt(pair.Value, a, m.T + 2000) + " (2 s after)");
                }
            }
            double s0 = shipAt(m.T - 2000), s1 = shipAt(m.T), s2 = shipAt(m.T + 2000);
            detail.AppendLine("    ship throttle: " + Fmt(s0) + "% before, " + Fmt(s1) + "% at the marker, " + Fmt(s2) + "% after");
        }
        if (movers.Count == 0 && devices.Count > 0) findings.Add("No axis moved by a useful amount during the session. Move the throttle lever through its whole range while flying.");
        if (findings.Count == 0) findings.Add("No problem was found automatically. Read the details below.");

        var report = new StringBuilder();
        report.AppendLine("Flight Control diagnostic report");
        report.AppendLine("================================");
        report.AppendLine(environment);
        report.AppendLine();
        report.AppendLine("FINDINGS");
        foreach (string f in findings) report.AppendLine("- " + f);
        report.AppendLine();
        report.AppendLine("SESSION");
        report.AppendLine("Length " + (lastT / 1000) + " s; game window in front " + (int)(focusedShare * 100) + "% of the time; game-script samples: " + lua.Count + "; markers: " + markers.Count);
        report.AppendLine();
        report.AppendLine("DEVICES SEEN BY SDL3");
        foreach (string[] d in devices) report.AppendLine("  #" + d[0] + " " + d[1] + " (" + d[2] + ") axes " + d[4] + ", buttons " + d[5] + ", hats " + d[6]);
        report.AppendLine();
        report.AppendLine("AXES THAT MOVED");
        if (movers.Count == 0) report.AppendLine("  none");
        foreach (string m in movers) report.AppendLine(m);
        report.AppendLine();
        report.AppendLine("DETAILS");
        report.Append(detail.Length == 0 ? "  none\r\n" : detail.ToString());
        report.AppendLine();
        report.AppendLine("THE GAME'S OWN BINDINGS (from inside the game)");
        if (bindings.Count == 0) report.AppendLine("  not available");
        foreach (string b in bindings.Distinct()) report.AppendLine("  " + b);
        report.AppendLine();
        report.AppendLine("SETTINGS THE TOOL READ");
        report.AppendLine(settingsSummary);
        return Sanitize(report.ToString());
    }

    static string Fmt(double v) { return double.IsNaN(v) ? "?" : Math.Round(v).ToString(Inv); }

    // ---- packaging ----
    // Copies the settings that matter into <dir>\settings (sanitized) and returns a short summary of them.
    public static string CollectSettings(string dir, string root, string prefsData, string tablePath, string wheelConfig) {
        string target = Path.Combine(dir, "settings");
        Directory.CreateDirectory(target);
        var summary = new StringBuilder();
        Action<string, string, string> copy = delegate(string label, string path, string name) {
            try {
                if (path == null || !File.Exists(path)) { summary.AppendLine("  " + label + ": not found"); return; }
                File.WriteAllText(Path.Combine(target, name), Sanitize(File.ReadAllText(path)), new UTF8Encoding(false));
                summary.AppendLine("  " + label + ": copied (" + name + ")");
            } catch (Exception e) { summary.AppendLine("  " + label + ": " + e.Message); }
        };
        string players = Path.Combine(prefsData, "players");
        copy("HOTAS profile", Path.Combine(players, "fotg-hotas-profile.hotas"), "hotas-profile.json");
        copy("HOTAS map", Path.Combine(players, HotasMapFile.FileName), "hotas-virtual.map");
        copy("HOTAS state", Path.Combine(players, "fotg-hotas-state.hotas"), "hotas-state.json");
        copy("Flight Control config", wheelConfig, "wheel-config.lua");
        try {
            if (tablePath != null && File.Exists(tablePath)) {
                string table = File.ReadAllText(tablePath);
                var m = Regex.Match(table, @"(?ms)^;; Flight Control HOTAS BEGIN\r?\n.*?^;; Flight Control HOTAS END");
                File.WriteAllText(Path.Combine(target, "control-table-hotas-block.txt"), Sanitize(m.Success ? m.Value : "(no HOTAS block in the control table)"), new UTF8Encoding(false));
                summary.AppendLine("  Control table HOTAS block: " + (m.Success ? "copied" : "none (HOTAS is not applied)"));
            }
        } catch (Exception e) { summary.AppendLine("  Control table: " + e.Message); }
        try {
            string ini = Directory.GetFiles(prefsData, "FotG_*_settings.ini").FirstOrDefault();
            if (ini != null) {
                var keep = File.ReadAllLines(ini).Where(l => !Regex.IsMatch(l, "player|pilot|callsign|name", RegexOptions.IgnoreCase));
                File.WriteAllText(Path.Combine(target, "game-settings-ini.txt"), Sanitize(String.Join(Environment.NewLine, keep.ToArray())), new UTF8Encoding(false));
                summary.AppendLine("  Game settings ini: copied (pilot and player names left out)");
            }
        } catch (Exception e) { summary.AppendLine("  Game ini: " + e.Message); }
        try {
            string pilotFile = Directory.Exists(players) ? Directory.GetFiles(players, "*.json").Where(f => !Path.GetFileName(f).StartsWith("fotg-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault() : null;
            if (pilotFile != null) {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 16777216 };
                var pilot = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(pilotFile));
                var sections = pilot["sections"] as Dictionary<string, object>;
                var controls = sections["controls"] as Dictionary<string, object>;
                var settings = sections.ContainsKey("settings") ? sections["settings"] as Dictionary<string, object> : new Dictionary<string, object>();
                var extract = new Dictionary<string, object>();
                extract["preset"] = controls.ContainsKey("preset") ? controls["preset"] : "";
                extract["axes"] = controls.ContainsKey("axes") ? controls["axes"] : null;
                var joy = new Dictionary<string, object>();
                foreach (var pair in settings) if (pair.Key.StartsWith("joy", StringComparison.OrdinalIgnoreCase)) joy[pair.Key] = pair.Value;
                extract["joy_settings"] = joy;
                var bound = new List<object>();
                var list = controls.ContainsKey("controls") ? controls["controls"] as System.Collections.IList : null;
                if (list != null)
                    for (int i = 0; i < list.Count; i++) {
                        var entry = list[i] as Dictionary<string, object>;
                        if (entry != null && entry.ContainsKey("joystick") && Convert.ToInt32(entry["joystick"]) != -1) bound.Add(new Dictionary<string, object> { { "action_index", i }, { "binding", entry } });
                    }
                extract["actions_bound_to_a_joystick_button"] = bound;
                File.WriteAllText(Path.Combine(target, "pilot-controls.json"), Sanitize(serializer.Serialize(extract)), new UTF8Encoding(false));
                summary.AppendLine("  Pilot controls: preset " + extract["preset"] + ", joystick dead zone/sensitivity, axis bindings (pilot name left out)");
            }
        } catch (Exception e) { summary.AppendLine("  Pilot controls: " + e.Message); }
        try {
            string log = Path.Combine(prefsData, "fs2_open.log");
            if (File.Exists(log)) {
                string[] all = File.ReadAllLines(log);
                File.WriteAllText(Path.Combine(target, "fs2_open-log-tail.txt"), Sanitize(String.Join("\r\n", all.Skip(Math.Max(0, all.Length - 400)).ToArray())), new UTF8Encoding(false));
                summary.AppendLine("  Game log: last " + Math.Min(400, all.Length) + " lines copied");
            }
        } catch (Exception e) { summary.AppendLine("  Game log: " + e.Message); }
        return summary.ToString().TrimEnd();
    }

    public static string Package(string dir, string report) {
        File.WriteAllText(Path.Combine(dir, "report.txt"), report, new UTF8Encoding(false));
        string zip = dir.TrimEnd('\\') + ".zip";
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Optimal, false);
        return zip;
    }

    // ---- self-test ----
    // Synthetic sessions: a lever whose forward end is LOW (the game's normal direction) and one whose forward end is HIGH
    // (the Warthog case that gives less throttle when pushed forward).
    internal static bool SelfTest() {
        string root = Path.Combine(Path.GetTempPath(), "FotGDiagTest-" + Guid.NewGuid().ToString("N"));
        try {
            string a = MakeSession(Path.Combine(root, "normal"), false), b = MakeSession(Path.Combine(root, "inverted"), true);
            string ra = Build(a, "  none", "test"), rb = Build(b, "  none", "test");
            if (ra.IndexOf("The throttle direction is right") < 0 || ra.IndexOf("GAVE LESS THROTTLE") >= 0) return false;
            if (rb.IndexOf("GAVE LESS THROTTLE") < 0 || rb.IndexOf("physical FORWARD is raw HIGH") < 0) return false;
            if (ra.IndexOf("physical FORWARD is raw LOW") < 0) return false;
            if (Sanitize(@"C:\Users\Someone\Documents\x ""Serial"": ""ABC123""").IndexOf("Someone") >= 0) return false;
            if (Sanitize("\"Serial\": \"ABC123\"").IndexOf("ABC123") >= 0) return false;
            string zip = Package(b, rb);
            using (ZipArchive z = ZipFile.OpenRead(zip)) if (z.GetEntry("report.txt") == null || z.GetEntry("joystick.csv") == null) return false;
            return true;
        } catch { return false; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    static string MakeSession(string dir, bool forwardIsHigh) {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "devices.txt"), "index|name|vendor:product|guid|axes|buttons|hats\r\n0|Test Throttle|044f:0404|guid|4|32|1\r\n");
        var joy = new StringBuilder("t_ms,dev,axes,hats,buttons_hi_lo\r\n");
        var bridge = new StringBuilder("t_ms,seq,focused,active,mode,state,hotas_up,pitch,yaw,roll,throttle,mask,buttons,rx,ry,lx,ly\r\n");
        var lua = new StringBuilder("B|JOY_ABS_THROTTLE_AXIS|Joy 0 axis 2\r\n");
        var events = new StringBuilder("t_ms,type,text\r\n");
        // 40 s: lever back for 10 s, then pushed fully forward over 10 s, held, then pulled back and forward again.
        for (int i = 0; i <= 400; i++) {
            long t = i * 100;
            double forward = t < 10000 ? 0 : t < 20000 ? (t - 10000) / 10000.0 : t < 30000 ? 1 - (t - 20000) / 10000.0 : (t - 30000) / 10000.0;   // 0 = back, 1 = fully forward
            int raw = (int)Math.Round(forwardIsHigh ? -32768 + forward * 65535 : 32767 - forward * 65535);
            // the game's absolute throttle: LOW raw = full throttle
            double lowEnd = (32767 - raw) / 65535.0;
            joy.AppendLine(t + ",0,0;0;" + raw + ";0,0,0");
            bridge.AppendLine(t + "," + (1000 + i) + ",1,1,FLIGHT,GS_STATE_GAME_PLAY,0,0,0,0,0,0,0,0,0,0,0");
            lua.AppendLine("S|" + (1000 + i) + "|" + (t / 1000.0).ToString("0.0", Inv) + "|GS_STATE_GAME_PLAY|1|0|0|" + (lowEnd * 100).ToString("0.0", Inv) + "|-1|" + (lowEnd * 40).ToString("0.0", Inv) + "|" + lowEnd.ToString("0.000", Inv));
        }
        events.AppendLine("19900,marker,forward");
        events.AppendLine("9900,marker,back");
        File.WriteAllText(Path.Combine(dir, "joystick.csv"), joy.ToString());
        File.WriteAllText(Path.Combine(dir, "bridge.csv"), bridge.ToString());
        File.WriteAllText(Path.Combine(dir, "lua-001.csv"), lua.ToString());
        File.WriteAllText(Path.Combine(dir, "events.csv"), events.ToString());
        return dir;
    }
}
