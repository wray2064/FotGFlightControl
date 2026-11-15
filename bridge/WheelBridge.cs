// FotG command wheel companion: XInput to an additive Lua mod, with menu pointer support.
// No hooks, injected DLLs, or game executable changes.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Flight Control controller bridge")]
[assembly: System.Reflection.AssemblyDescription("Reads the gamepad for Flight Control's command wheels and menus, and starts Fate of the Galaxy with the add-on.")]

[StructLayout(LayoutKind.Sequential)] struct XPad {
    public ushort Buttons;
    public byte LeftTrigger, RightTrigger;
    public short LeftX, LeftY, RightX, RightY;
}
[StructLayout(LayoutKind.Sequential)] struct XState { public uint Packet; public XPad Pad; }

sealed class DpadGesture {
    public struct Decision { public int Tap, Look; }
    int heldDirection;
    long pressedAt;
    bool tapSent, lookHeld;
    public void Reset() { heldDirection=0; pressedAt=0; tapSent=false; lookHeld=false; }
    public Decision Update(int direction, long now, int holdMs, bool lookEnabled, bool wheelOpen) {
        Decision result = new Decision();
        if (direction != heldDirection) {
            // A delayed UI tick can skip across the threshold; a press that
            // never actually entered look mode still counts as a tap.
            if (heldDirection != 0 && direction == 0 && !tapSent && !lookHeld)
                result.Tap = heldDirection;
            heldDirection = direction;
            pressedAt = now;
            tapSent = false;
            lookHeld = false;
            if (direction != 0 && (!lookEnabled || wheelOpen)) {
                result.Tap = direction;
                tapSent = true;
            }
        }
        if (direction != 0 && lookEnabled && !wheelOpen && !tapSent && now-pressedAt >= holdMs)
            lookHeld = true;
        result.Look = direction != 0 && lookHeld && !wheelOpen ? direction : 0;
        return result;
    }
}

// Delay only shoulder input while deciding whether this is a single press or
// a chord. Axes, face buttons and triggers remain live throughout.
sealed class HyperspaceGesture {
    const int Shoulders = 256 | 512;
    enum Phase { Idle, Pending, Single, Chord, Locked }
    Phase phase = Phase.Locked;
    int first;
    long pressedAt, pairedAt;
    public struct Decision { public int Buttons; public bool Jump; }
    public void Reset() { phase = Phase.Locked; }
    public Decision Update(int buttons, long now) {
        int shoulders = buttons & Shoulders;
        Decision result = new Decision { Buttons = buttons & ~Shoulders };
        if (phase == Phase.Locked) {
            if (shoulders == 0) phase = Phase.Idle;
            return result;
        }
        if (phase == Phase.Idle && shoulders != 0) {
            first = shoulders; pressedAt = now;
            phase = shoulders == Shoulders ? Phase.Chord : Phase.Pending;
            pairedAt = now;
        }
        if (phase == Phase.Pending) {
            if (shoulders == Shoulders && now - pressedAt <= 120) {
                phase = Phase.Chord; pairedAt = now;
            } else if (shoulders == 0 || shoulders != first || now - pressedAt >= 120) {
                // Preserve even a quick tap released inside the decision window.
                result.Buttons |= first;
                phase = Phase.Single;
                return result;
            }
        }
        if (phase == Phase.Single) {
            result.Buttons |= shoulders;
            if (shoulders == 0) phase = Phase.Idle;
        } else if (phase == Phase.Chord) {
            if (shoulders != Shoulders) phase = Phase.Locked;
            else if (now - pairedAt >= 300) {
                result.Jump = true;
                phase = Phase.Locked;
            }
        }
        return result;
    }
}

sealed class MissionKeys {
    string lastSequence;
    public static ushort Key(string action) {
        if (action != null && action.Length == 1 && action[0] >= '0' && action[0] <= '9') return (ushort)action[0];
        if (action == "log") return 0x73; // F4
        if (action == "fleet") return 0x7A; // F11
        if (action == "fleet_page") return 0x20;
        if (action == "enter") return 0x0D; // Contextual docking / mission confirmation
        if (action == "mouse_mode") return 0x4E; // N: tutorial checks the physical key
        if (action == "link_guns") return 0x45; // E: primary firing sequence
        if (action == "link_torpedoes") return 0x51; // Q: missile salvo count
        if (action == "shield_front") return 0x100; // Alt-W: tutorial objective
        if (action == "targeting_computer") return 0x5A; // Z: FotG control and tutorial
        if (action == "power_shields") return 0x101; // End, Shift-End
        if (action == "power_lasers") return 0x102; // Page Up, Shift-Page Up
        if (action == "power_engines") return 0x103; // Page Down, Shift-Page Down
        return 0;
    }
    public ushort Read(string state) {
        Match seq = Regex.Match(state, @"\|missionseq=(\d+)(?=\||$)");
        if (!seq.Success) return 0;
        string next = seq.Groups[1].Value;
        if (lastSequence == null) { lastSequence = next; return 0; }
        if (next == lastSequence) return 0;
        lastSequence = next;
        Match key = Regex.Match(state, @"\|missionkey=([^|]+)(?=\||$)");
        return next == "0" || !key.Success ? (ushort)0 : Key(key.Groups[1].Value);
    }
}

static class WinInput {
    [DllImport("xinput1_4.dll")] public static extern uint XInputGetState(uint index, out XState state);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern uint SendInput(uint count, INPUT[] input, int size);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public DATA data; }
    [StructLayout(LayoutKind.Explicit)] public struct DATA {
        [FieldOffset(0)] public MOUSE mouse;
        [FieldOffset(0)] public KEYBOARD key;
    }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSE {
        public int dx, dy;
        public uint data, flags, time;
        public UIntPtr extra;
    }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBOARD {
        public ushort vk, scan;
        public uint flags, time;
        public UIntPtr extra;
    }
    public static bool DryRun;
    static void Send(INPUT value) {
        if (DryRun) return;
        if (SendInput(1, new[] { value }, Marshal.SizeOf(typeof(INPUT))) != 1)
            throw new InvalidOperationException("Windows rejected controller input. Run the game and bridge at the same privilege level.");
    }
    public static void Mouse(uint flags, int dx = 0, int dy = 0) {
        INPUT value = new INPUT(); value.type = 0; value.data.mouse.flags = flags;
        value.data.mouse.dx = dx; value.data.mouse.dy = dy; Send(value);
    }
    public static void Key(ushort vk, bool down) {
        INPUT value = new INPUT(); value.type = 1;
        // SDL3 recognizes the dedicated arrows by their E0 scan codes. A
        // virtual-key SendInput event reaches Windows but not FSO's padlock
        // control, even if the extended-key bit is set.
        value.data.key.scan = ArrowScan(vk);
        if (value.data.key.scan == 0) value.data.key.vk = vk;
        value.data.key.flags = KeyFlags(vk, down); Send(value);
    }
    public static ushort ArrowScan(ushort vk) {
        switch (vk) {
            case 0x25: return 0x4b; // Left
            case 0x26: return 0x48; // Up
            case 0x27: return 0x4d; // Right
            case 0x28: return 0x50; // Down
            default: return 0;
        }
    }
    public static uint KeyFlags(ushort vk, bool down) {
        return (ArrowScan(vk) != 0 ? 9u : 0u) | (down ? 0u : 2u);
    }
    public static void Scan(ushort scan, bool down, bool extended = false) {
        INPUT value = new INPUT(); value.type = 1; value.data.key.scan = scan;
        value.data.key.flags = 8u | (extended ? 1u : 0u) | (down ? 0u : 2u); Send(value);
    }
    public static void ScanTap(ushort scan, bool extended = false) {
        Scan(scan, true, extended); Scan(scan, false, extended);
    }
    public static void ShiftExtendedScanTap(ushort scan) {
        Scan(0x2A, true);
        try { ScanTap(scan, true); } finally { Scan(0x2A, false); }
    }
    public static void Tap(ushort vk) { Key(vk, true); Key(vk, false); }
    public static void ShiftTab() { Key(0x10, true); try { Tap(0x09); } finally { Key(0x10, false); } }
    public static bool MoveToGamePoint(double x, double y) {
        IntPtr hwnd = GetForegroundWindow(); RECT rect;
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out rect)) return false;
        POINT point = new POINT { x = (int)Math.Round((rect.right-rect.left)*x), y = (int)Math.Round((rect.bottom-rect.top)*y) };
        if (!ClientToScreen(hwnd, ref point)) return false;
        return DryRun || SetCursorPos(point.x, point.y);
    }
    public static bool MoveMenuCursor(int dx, int dy) {
        IntPtr hwnd = GetForegroundWindow(); RECT rect; POINT cursor;
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out rect) || !GetCursorPos(out cursor)) return false;
        POINT origin = new POINT();
        if (!ClientToScreen(hwnd, ref origin)) return false;
        int x = Math.Max(origin.x, Math.Min(origin.x + rect.right - rect.left - 1, cursor.x + dx));
        int y = Math.Max(origin.y, Math.Min(origin.y + rect.bottom - rect.top - 1, cursor.y + dy));
        return DryRun || SetCursorPos(x, y);
    }
}

sealed class Bridge : ApplicationContext {
    const int Up=1, Down=2, Left=4, Right=8, Start=16, Back=32,
        LB=256, RB=512, A=4096, B=8192, X=16384, Y=32768;
    static readonly double[] MainHallX = {.141,.283,.430,.575,.719,.815,.895};
    readonly string root, preferences, inputPath, statePath, extraMod;
    readonly bool vrMode;
    readonly int lookHoldMs;
    readonly bool lookEnabled;
    readonly bool cameraHoldEnabled;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly DpadGesture dpad = new DpadGesture();
    readonly HyperspaceGesture hyperspace = new HyperspaceGesture();
    readonly MissionKeys missionKeys = new MissionKeys();
    ushort pendingMissionKey;
    ushort pendingPowerScan;
    long pendingPowerSecondAt;
    readonly DateTime startedUtc = DateTime.UtcNow;
    Process game;
    long lastTick, lastStateRead;
    readonly int preferredSlot;
    int slot = -1, sequence, previousButtons;
    int lookKey, tapSequence, tapDirection;
    long pendingWheelUntil;
    bool wheelReported;
    bool startHeld, startConsumed;
    long startPressedAt, cameraFinishAt;
    int cameraStage;
    int menuSelection = -1, menuDirection;
    long nextMenuRepeat;
    bool menuKeyboardFocus, menuPointerMoved;
    string gameState = "";
    double moveRemainderX, moveRemainderY;
    bool leftMouse, shift, tab, escape, enter, backspace, wasFocused, modal;
    string mode = "WAIT", latestStatus = "", latestState = "";

    public Bridge(string gameRoot, bool launch, bool vrMode, string extraMod) {
        if (!launch) startedUtc = DateTime.MinValue;
        this.vrMode = vrMode;
        this.extraMod = extraMod;
        root = Path.GetFullPath(gameRoot);
        preferences = Path.Combine(root, "kn_portable", "HardLightProductions", "FreeSpaceOpen");
        inputPath = Path.Combine(preferences, "data", "players", "fotg-wheel-input.txt");
        statePath = Path.Combine(preferences, "data", "players", "fotg-wheel-state.txt");
        bool myMod = GameInstall.HasMyMod(root);
        string wheelRoot = myMod ? GameInstall.MyModCore(root) : Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel");
        string configPath = Path.Combine(wheelRoot, "data", "tables", "lua", "zz_fotg_wheel_config-sct.lua");
        string config = File.Exists(configPath) ? File.ReadAllText(configPath) : "";
        lookHoldMs = ParseLookHoldMs(config);
        lookEnabled = !Regex.IsMatch(config, @"\blook_enabled\s*=\s*false\b");
        cameraHoldEnabled = !Regex.IsMatch(config, @"\bcamera_hold_enabled\s*=\s*false\b");
        preferredSlot = ReadPreferredSlot(Path.Combine(preferences, "data", myMod ? "FotG_2_settings.ini" : "GamepadMods_settings.ini"));
        slot = preferredSlot;
        Directory.CreateDirectory(Path.GetDirectoryName(inputPath));
        tray.Icon = SystemIcons.Application;
        tray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Open wheel help", null, delegate {
            string help = Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "Help.html");
            if (File.Exists(help)) Process.Start(help);
        });
        menu.Items.Add("Exit controller bridge", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { if (File.Exists(statePath)) Process.Start("notepad.exe", statePath); };
        if (launch) Launch();
        timer.Interval = 25;
        timer.Tick += Tick;
        timer.Start();
    }
    void Launch() {
        if (GameInstall.HasMyMod(root)) {
            LaunchMyMod();
            return;
        }
        string mod = Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "data", "tables", "lua", "zzz_fotg_wheel-sct.lua");
        if (!File.Exists(mod)) throw new FileNotFoundException("Command wheel mod was not found", mod);
        string version = GameVersion(root);
        if (version == null) throw new FileNotFoundException("No supported Fate of the Galaxy release (" + String.Join(", ", SupportedVersions) + ") was found in this game folder. If the game was updated, run Flight Control Setup again.");
        EngineInfo engine = GameInstall.FindEngine(root, version);
        if (engine == null) throw new FileNotFoundException("The Fate of the Galaxy engine (FSO) was not found in kn_portable\\Library\\bin. Launch the game once from Knossos, then try again.");
        string exe = engine.Exe;
        string basePath = @"kn_portable\Library\FotG_R\FotG_R-" + version;
        StringBuilder paths = new StringBuilder(@"GamepadMods\FotG_Gamepad_Wheel");
        if (!String.IsNullOrEmpty(extraMod)) {
            string selected = Path.GetFullPath(Path.Combine(root, extraMod));
            string library = Path.GetFullPath(Path.Combine(root, "kn_portable", "Library"));
            if (!selected.StartsWith(library + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(selected, "mod.json")))
                throw new InvalidOperationException("The selected mission pack must be an installed Knossos mod in this game library.");
            string relative = selected.Substring(root.TrimEnd('\\').Length + 1);
            string[] folders = GameDataFolders(root, relative);
            foreach (string folder in folders) paths.Append(',').Append(folder);
        }
        foreach (string folder in GameDataFolders(root, basePath)) paths.Append(',').Append(folder);
        ProcessStartInfo info = new ProcessStartInfo(exe);
        info.WorkingDirectory = root;
        info.UseShellExecute = false;
        info.EnvironmentVariables["FSO_PREFERENCES_PATH"] = preferences;
        info.Arguments = "-mod \"" + paths + "\"" + EngineFlags(vrMode);
        game = Process.Start(info);
    }
    void LaunchMyMod() {
        string library = Path.Combine(root, "kn_portable", "Library");
        const string wheel = @"FotG_2\MyMod-1.0.0\core";
        const string basePath = @"FotG_2\FotG_2-2.0.2";
        if (!File.Exists(Path.Combine(library, wheel, @"data\tables\lua\zzz_fotg_wheel-sct.lua")))
            throw new FileNotFoundException("The command wheel overlay is missing from MyMod.");
        EngineInfo engine = GameInstall.FindEngine(root, null);
        if (engine == null) throw new FileNotFoundException("The FotG 2 SDL3 engine was not found.");
        var paths = new System.Collections.Generic.List<string> { wheel };
        if (!String.IsNullOrEmpty(extraMod)) {
            string selected = Path.GetFullPath(Path.Combine(root, extraMod));
            if (!selected.StartsWith(library + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(selected, "mod.json")))
                throw new InvalidOperationException("Select an installed Knossos mission pack.");
            paths.AddRange(GameDataFolders(library, selected.Substring(library.Length + 1)));
        }
        paths.AddRange(GameDataFolders(library, basePath));
        var info = new ProcessStartInfo(engine.Exe) { WorkingDirectory = library, UseShellExecute = false };
        info.EnvironmentVariables["FSO_PREFERENCES_PATH"] = preferences;
        info.Arguments = "-mod \"" + String.Join(",", paths) + "\"" + EngineFlags(vrMode);
        game = Process.Start(info);
    }
    internal static string EngineFlags(bool vr) {
        // FSO's -vr flag enables OpenXR and forces the needed window, mouse
        // capture and vsync settings. Stretching keeps its square menu usable.
        return " -aa -soft_particles -deferred_cockpit" + (vr ? " -vr -stretch_menu" : "");
    }
    // Supported game releases, newest first; keep in step with setup\Installer.cs.
    static readonly string[] SupportedVersions = { "1.1.3", "1.1.2", "1.1.1", "1.1.0" };
    static readonly string[] GamePackages = {"FG_Core", "FG_Effects", "FG_Maps1", "FG_Maps2", "FG_Maps3", "FG_Maps4", "FG_Maps5", "FG_Models", "FG_Music", "FG_SndVA", "FG_UI"};
    static bool HasRelease(string root, string version) {
        return File.Exists(Path.Combine(root, "kn_portable", "Library", "FotG_R", "FotG_R-" + version, "mod.json"));
    }
    // An install with the version check overridden records its release in version-override.txt;
    // otherwise the newest supported release present is used.
    internal static string GameVersion(string root) {
        try {
            string record = Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "version-override.txt");
            if (File.Exists(record)) {
                string overridden = File.ReadAllText(record).Trim();
                if (Regex.IsMatch(overridden, @"^\d+(\.\d+){1,3}$") && HasRelease(root, overridden)) return overridden;
            }
        } catch (IOException) { }
        foreach (string version in SupportedVersions) if (HasRelease(root, version)) return version;
        return null;
    }
    // Knossos installs a release either as one folder per package (FG_Core, FG_Effects, ...) or merged
    // into a single data folder at the release root, as 1.1.2 is. Pass whichever is on disk.
    internal static string[] GameDataFolders(string root, string basePath) {
        string[] packages = GamePackages.Where(p => Directory.Exists(Path.Combine(root, basePath, p))).Select(p => basePath + @"\" + p).ToArray();
        if (packages.Length > 0) return packages;
        if (Directory.Exists(Path.Combine(root, basePath, "data"))) return new[] { basePath };
        string[] additional = Directory.GetDirectories(Path.Combine(root, basePath))
            .Where(p => Directory.Exists(Path.Combine(p, "data")))
            .Select(p => basePath + @"\" + Path.GetFileName(p)).ToArray();
        if (additional.Length > 0) return additional;
        throw new DirectoryNotFoundException("The game data for " + basePath + " was not found. Repair the game in Knossos.");
    }
    static double Axis(short value) {
        double n = Math.Max(-1.0, value / 32767.0);
        double a = Math.Abs(n);
        return a < .18 ? 0.0 : Math.Sign(n) * Math.Pow((a-.18)/.82, 1.35);
    }
    static int ParseLookHoldMs(string config) {
        Match match = Regex.Match(config, @"\blook_hold_ms\s*=\s*(\d+)");
        int value;
        return match.Success && Int32.TryParse(match.Groups[1].Value, out value)
            ? Math.Max(150, Math.Min(600, value)) : 250;
    }
    static int ReadPreferredSlot(string settings) {
        try {
            if (!File.Exists(settings)) return -1;
            Match match = Regex.Match(File.ReadAllText(settings), @"(?im)^XInputSlot\s*=\s*([0-3])\s*$");
            int value;
            return match.Success && Int32.TryParse(match.Groups[1].Value, out value) ? value : -1;
        } catch (IOException) { return -1; }
    }
    void ReleaseLook() {
        if (lookKey != 0) WinInput.Key((ushort)lookKey, false);
        lookKey = 0;
    }
    void ResetDpad() {
        ReleaseLook(); dpad.Reset();
    }
    void Release() {
        ResetDpad();
        pendingPowerScan = 0;
        hyperspace.Reset();
        startHeld = startConsumed = false; cameraFinishAt = 0;
        if (leftMouse) WinInput.Mouse(4);
        if (shift) WinInput.Key(0x10, false);
        if (tab) WinInput.Key(0x09, false);
        if (escape) WinInput.Key(0x1B, false);
        if (enter) WinInput.Key(0x0D, false);
        if (backspace) WinInput.Key(0x08, false);
        leftMouse = shift = tab = escape = enter = backspace = false;
        moveRemainderX = moveRemainderY = 0;
    }
    static void HoldKey(ref bool held, bool want, ushort vk) {
        if (held == want) return;
        WinInput.Key(vk, want);
        held = want;
    }
    bool waitingForDialogRelease;
    bool dialogReported;
    void ReadState(long now) {
        if (now-lastStateRead < 150) return;
        lastStateRead = now;
        try {
            if (!File.Exists(statePath)) return;
            DateTime writeTime = File.GetLastWriteTimeUtc(statePath);
            // The game publishes on state transitions, so an unchanged menu state
            // remains valid for the whole run. Ignore only a previous run's file.
            if (startedUtc != DateTime.MinValue && writeTime < startedUtc.AddSeconds(-1)) {
                mode = "WAIT"; modal = false; Release(); return;
            }
            string state = File.ReadAllText(statePath).Trim();
            pendingMissionKey = missionKeys.Read(state);
            bool nextDialog = Regex.IsMatch(state, @"\|dialog=[1-9][0-9]*(\||$)");
            if (nextDialog != dialogReported) {
                Release();
                waitingForDialogRelease = true;
                pendingWheelUntil = 0;
            }
            dialogReported = nextDialog;
            wheelReported = state.Contains("wheel=1");
            if (wheelReported) pendingWheelUntil = 0;
            modal = wheelReported || now < pendingWheelUntil;
            if (modal) ReleaseLook();
            if (state == latestState) return;
            latestState = state;
            string[] fields = state.Split('|');
            string next = fields.Length > 0 && fields[0] == "FLIGHT" ? "FLIGHT" :
                fields.Length > 0 && fields[0] == "MENU" ? "MENU" : "WAIT";
            string nextState = fields.Length > 1 ? fields[1] : "";
            if (next != mode || nextState != gameState) {
                Release(); menuSelection=-1; menuDirection=0; nextMenuRepeat=0; menuKeyboardFocus=false; menuPointerMoved=false;
                if (next == "FLIGHT" && mode != "FLIGHT") cameraStage=0;
            }
            mode = next;
            gameState = nextState;
            if (nextState=="GS_STATE_INITIAL_PLAYER_SELECT") menuKeyboardFocus=true;
        } catch (IOException) { }
    }
    static void Atomic(string path, string value) {
        // FSO opens this file every frame. Replacing its directory entry needs
        // exclusive access and can starve; a short direct write is accepted.
        // Lua validates the complete record and ignores any partial read.
        File.WriteAllText(path, value, new UTF8Encoding(false));
    }
    void Publish(XPad pad, bool active, long now) {
        // Constant heartbeat lets Lua discard stale input after a companion crash.
        // Tick already runs at 25 ms. Do not drop a gesture's one-frame tap
        // when Windows delivers adjacent timer ticks slightly under 25 ms.
        ++sequence;
        string line = String.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}\n", sequence,
            active ? pad.Buttons : 0, active ? pad.RightX : 0, active ? pad.RightY : 0,
            active ? pad.LeftX : 0, active ? pad.LeftY : 0, active ? slot : -1,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), tapSequence, tapDirection);
        Atomic(inputPath, line);
    }
    void Tick(object sender, EventArgs e) {
        try {
            if (game != null && game.HasExited) { ExitThread(); return; }
            long now = clock.ElapsedMilliseconds;
            double dt = Math.Max(.001, Math.Min(.05, (now-lastTick)/1000.0));
            lastTick = now;
            ReadState(now);
            uint pid; WinInput.GetWindowThreadProcessId(WinInput.GetForegroundWindow(), out pid);
            bool focused = game != null ? pid == (uint)game.Id : IsFotGProcess(pid);
            XState state = new XState(); bool connected = false;
            if (slot >= 0) connected = WinInput.XInputGetState((uint)slot, out state) == 0;
            if (!connected && preferredSlot < 0) {
                slot = -1;
                for (uint i=0; i<4; i++) if (WinInput.XInputGetState(i, out state) == 0) { slot=(int)i; connected=true; break; }
            }
            bool active = focused && connected;
            // A held fire trigger or confirmation button must not become a
            // dialog click, or a flight action when that dialog closes.
            if (waitingForDialogRelease && active) {
                if (state.Pad.Buttons == 0 && state.Pad.LeftTrigger <= 48 && state.Pad.RightTrigger <= 48)
                    waitingForDialogRelease = false;
                else active = false;
            }
            if (!active && wasFocused) { Release(); previousButtons=0; modal=false; pendingWheelUntil=0; }
            wasFocused = active;
            // Consume requests even when unfocused/disconnected: never replay
            // a mission choice later in another application or game context.
            if (pendingMissionKey != 0) {
                if (active && mode != "WAIT" && !dialogReported && !modal) {
                    if (pendingMissionKey == 0x100) {
                        WinInput.Key(0x12, true); // Alt
                        try { WinInput.Tap(0x57); } finally { WinInput.Key(0x12, false); }
                    } else if (pendingMissionKey >= 0x101 && pendingMissionKey <= 0x103) {
                        pendingPowerScan = pendingMissionKey == 0x101 ? (ushort)0x4F :
                            pendingMissionKey == 0x102 ? (ushort)0x49 : (ushort)0x51;
                        WinInput.ScanTap(pendingPowerScan, true);
                        pendingPowerSecondAt = now + 140;
                    } else WinInput.Tap(pendingMissionKey);
                }
                pendingMissionKey = 0;
            }
            if (pendingPowerScan != 0 && now >= pendingPowerSecondAt) {
                if (active && mode != "WAIT" && !dialogReported && !modal)
                    WinInput.ShiftExtendedScanTap(pendingPowerScan);
                pendingPowerScan = 0;
            }
            if (active && mode != "WAIT") HandleInput(state.Pad, dt, now);
            XPad publishedPad = state.Pad;
            if (active && mode == "FLIGHT" && !modal) {
                HyperspaceGesture.Decision gesture = hyperspace.Update(state.Pad.Buttons, now);
                publishedPad.Buttons = (ushort)gesture.Buttons;
                // Use Return so FotG's END_MISSION keybinding hook retains its
                // normal jump-vector checks and mission-specific behavior.
                if (gesture.Jump) WinInput.Tap(0x0D);
            } else hyperspace.Reset();
            // The game can have the state file open at the same instant we replace
            // it. Keep the bridge alive and retry on the next timer tick.
            try { Publish(publishedPad, active, now); } catch (IOException) { }
            string status = !connected ? (preferredSlot >= 0 ? "Wheel: XInput " + preferredSlot + " disconnected" : "Wheel: controller disconnected") : !focused ? "Wheel: waiting for FotG" : "Wheel: " + mode + " / XInput " + slot;
            if (status != latestStatus) { tray.Text = status; latestStatus = status; }
        } catch (Exception ex) {
            try { Release(); Publish(new XPad(), false, clock.ElapsedMilliseconds+100); } catch { }
            timer.Stop();
            File.AppendAllText(Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "bridge-error.log"), DateTime.Now + " " + ex + Environment.NewLine);
            MessageBox.Show(ex.Message, "FotG controller bridge");
            ExitThread();
        }
    }
    static bool IsFotGProcess(uint pid) {
        try { return Process.GetProcessById((int)pid).ProcessName.StartsWith("fs2_open_26_0_1_sdl3", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    static ushort DirectionKey(int direction) {
        if (direction == Up) return 0x26;
        if (direction == Down) return 0x28;
        if (direction == Left) return 0x25;
        return 0x27;
    }
    static int SingleDirection(int buttons) {
        int direction = buttons & (Up|Down|Left|Right);
        return direction == Up || direction == Down || direction == Left || direction == Right ? direction : 0;
    }
    void EmitWheelTap(int direction, long now) {
        ++tapSequence; tapDirection = direction;
        if (!wheelReported) pendingWheelUntil = now + 450;
        modal = true;
        ReleaseLook();
    }
    void HandleFlightDpad(int buttons, long now) {
        int direction = SingleDirection(buttons);
        DpadGesture.Decision result = dpad.Update(direction, now, lookHoldMs, lookEnabled, modal);
        int requestedKey = result.Look == 0 ? 0 : DirectionKey(result.Look);
        if (requestedKey != lookKey) {
            ReleaseLook();
            if (requestedKey != 0) { lookKey = requestedKey; WinInput.Key((ushort)lookKey, true); }
        }
        if (result.Tap != 0) EmitWheelTap(result.Tap, now);
    }
    void HandleFlightStart(int buttons, long now) {
        if (!cameraHoldEnabled || modal) {
            startHeld = startConsumed = false;
            HoldKey(ref escape, (buttons & Start)!=0, 0x1B);
            return;
        }
        HoldKey(ref escape, false, 0x1B);
        bool down = (buttons & Start)!=0;
        if (down && !startHeld) { startPressedAt=now; startConsumed=false; }
        if (down && !startConsumed && now-startPressedAt>=500) {
            startConsumed=true;
            if (cameraStage==0) WinInput.ScanTap(0x0c); // Chase
            else if (cameraStage==1) WinInput.ScanTap(0x0d); // External
            else { WinInput.ScanTap(0x0d); cameraFinishAt=now+100; } // Leave external, then chase
            cameraStage=(cameraStage+1)%3;
        }
        if (!down && startHeld && !startConsumed) WinInput.Tap(0x1B);
        startHeld=down;
        if (cameraFinishAt!=0 && now>=cameraFinishAt) { cameraFinishAt=0; WinInput.ScanTap(0x0c); }
    }
    int RepeatingDirection(int buttons, long now) {
        int direction = (buttons & (Up|Down|Left|Right));
        if (direction != Up && direction != Down && direction != Left && direction != Right) direction=0;
        if (direction==0) { menuDirection=0; return 0; }
        if (direction!=menuDirection) { menuDirection=direction; nextMenuRepeat=now+360; return direction; }
        if (now>=nextMenuRepeat) { nextMenuRepeat=now+120; return direction; }
        return 0;
    }
    void MoveMainHall(int direction) {
        int delta = (direction==Left || direction==Up) ? -1 : 1;
        menuSelection = menuSelection<0 ? 0 : Math.Max(0,Math.Min(MainHallX.Length-1,menuSelection+delta));
        menuPointerMoved=false;
        WinInput.MoveToGamePoint(MainHallX[menuSelection], .870);
    }
    void HandleInput(XPad pad, double dt, long now) {
        int rising = pad.Buttons & ~previousButtons;
        if (mode == "MENU") {
            ResetDpad();
            bool mainHall = gameState=="GS_STATE_MAIN_MENU";
            int direction=RepeatingDirection(pad.Buttons,now);
            if (mainHall && direction!=0) MoveMainHall(direction);
            else if (direction!=0) { WinInput.Tap(DirectionKey(direction)); menuKeyboardFocus=true; }
            double x = Axis(pad.LeftX) + Axis(pad.RightX)*.45;
            double y = Axis(pad.LeftY) + Axis(pad.RightY)*.45;
            if (Math.Abs(x)>.01 || Math.Abs(y)>.01) { menuKeyboardFocus=false; menuPointerMoved=true; if (mainHall) menuSelection=-1; }
            moveRemainderX += x * 1000 * dt;
            moveRemainderY -= y * 1000 * dt;
            int dx=(int)moveRemainderX, dy=(int)moveRemainderY;
            moveRemainderX -= dx; moveRemainderY -= dy;
            if (dx!=0 || dy!=0) WinInput.MoveMenuCursor(dx, dy);
            bool click = pad.RightTrigger > 48 || (((mainHall && (menuSelection>=0 || menuPointerMoved)) || (!mainHall && !menuKeyboardFocus)) && (pad.Buttons & A)!=0);
            if (click != leftMouse) { WinInput.Mouse(click ? 2u : 4u); leftMouse=click; }
            if (mainHall && menuSelection<0 && !menuPointerMoved && (rising & A)!=0) WinInput.Tap(0x0D);
            if (!mainHall && menuKeyboardFocus && (rising & A)!=0) WinInput.Tap(0x0D);
            if ((rising & RB)!=0) WinInput.Tap(0x09);
            if ((rising & LB)!=0) WinInput.ShiftTab();
            HoldKey(ref escape, (pad.Buttons & B)!=0, 0x1B);
            HoldKey(ref enter, (pad.Buttons & (X|Start))!=0, 0x0D);
            HoldKey(ref backspace, (pad.Buttons & Y)!=0, 0x08);
            HoldKey(ref shift, false, 0x10);
            HoldKey(ref tab, false, 0x09);
        } else {
            HandleFlightDpad(pad.Buttons, now);
            bool fire = !modal && pad.RightTrigger > 48;
            if (fire != leftMouse) { WinInput.Mouse(fire ? 2u : 4u); leftMouse=fire; }
            // FotG binds FIRE_SECONDARY to Tab. Keep it held for repeat fire,
            // and release it whenever a command wheel opens.
            HoldKey(ref tab, !modal && pad.LeftTrigger > 48, 0x09);
            HandleFlightStart(pad.Buttons, now);
            HoldKey(ref enter, false, 0x0D);
            HoldKey(ref backspace, false, 0x08);
            HoldKey(ref shift, false, 0x10);
        }
        previousButtons = pad.Buttons;
    }
    protected override void ExitThreadCore() {
        timer.Stop(); Release();
        try { Publish(new XPad(), false, clock.ElapsedMilliseconds+100); } catch { }
        tray.Visible=false; tray.Dispose();
        base.ExitThreadCore();
    }
}

static class Program {
    static string NormalizeGameRoot(string value) { return Path.GetFullPath(value.Trim().Trim('"')); }
    [STAThread] static int Main(string[] args) {
        try {
            if (args.Length > 0 && args[0] == "--self-test") return SelfTest();
            // Diagnostics: which engine the launcher will start (the same rule Setup uses).
            // Prints "<exe>|sdl3|<label>" or "<exe>|sdl2|<label>"; exit code 2 when none is found.
            if (args.Length > 0 && args[0] == "--engine") {
                int at = Array.IndexOf(args, "--game");
                if (at < 0 || at + 1 >= args.Length) return 1;
                string game = NormalizeGameRoot(args[at + 1]).TrimEnd('\\');
                EngineInfo found = GameInstall.FindEngine(game, Bridge.GameVersion(game));
                if (found == null) return 2;
                Console.Out.Write(found.Exe + "|" + (found.Sdl3 ? "sdl3" : "sdl2") + "|" + found.Label);
                Console.Out.Flush();
                return 0;
            }
            string root = null, extraMod = null; bool launch = true, vrMode = false;
            for (int i=0; i<args.Length; i++) {
                if (args[i] == "--game" && i+1<args.Length) root=args[++i];
                else if (args[i] == "--attach") launch=false;
                else if (args[i] == "--vr") vrMode=true;
                else if (args[i] == "--extra-mod" && i+1<args.Length) extraMod=args[++i];
            }
            if (String.IsNullOrWhiteSpace(root)) throw new ArgumentException("Use --game <Fate of the Galaxy game folder>.");
            // Windows command-line parsing can leave a literal quote when a
            // quoted folder argument ends in a backslash.
            root = NormalizeGameRoot(root);
            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\FotGGamepadWheelBridge", out created)) {
                if (!created) throw new InvalidOperationException("The controller bridge is already running.");
                Application.EnableVisualStyles();
                Application.Run(new Bridge(root, launch, vrMode, extraMod));
            }
            return 0;
        } catch (Exception ex) { MessageBox.Show(ex.Message, "FotG controller bridge"); return 1; }
    }
    // Both Knossos layouts and the override record resolve to the right launch folders.
    static bool SelfTestGameLayouts() {
        string temp = Path.Combine(Path.GetTempPath(), "FotGWheelSelfTest-" + Guid.NewGuid().ToString("N"));
        try {
            string lib = Path.Combine(temp, "kn_portable", "Library", "FotG_R");
            Directory.CreateDirectory(Path.Combine(lib, "FotG_R-1.1.1", "FG_Core"));
            Directory.CreateDirectory(Path.Combine(lib, "FotG_R-1.1.1", "FG_UI"));
            File.WriteAllText(Path.Combine(lib, "FotG_R-1.1.1", "mod.json"), "{}");
            if (Bridge.GameVersion(temp) != "1.1.1") return false;
            string[] split = Bridge.GameDataFolders(temp, @"kn_portable\Library\FotG_R\FotG_R-1.1.1");
            if (split.Length != 2 || !split[0].EndsWith(@"\FG_Core") || !split[1].EndsWith(@"\FG_UI")) return false;
            Directory.CreateDirectory(Path.Combine(lib, "FotG_R-1.1.2", "data"));
            File.WriteAllText(Path.Combine(lib, "FotG_R-1.1.2", "mod.json"), "{}");
            if (Bridge.GameVersion(temp) != "1.1.2") return false;
            string[] merged = Bridge.GameDataFolders(temp, @"kn_portable\Library\FotG_R\FotG_R-1.1.2");
            if (merged.Length != 1 || merged[0] != @"kn_portable\Library\FotG_R\FotG_R-1.1.2") return false;
            Directory.CreateDirectory(Path.Combine(lib, "FotG_R-9.9.9", "data"));
            File.WriteAllText(Path.Combine(lib, "FotG_R-9.9.9", "mod.json"), "{}");
            if (Bridge.GameVersion(temp) != "1.1.2") return false;
            string mod = Path.Combine(temp, "GamepadMods", "FotG_Gamepad_Wheel");
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, "version-override.txt"), "9.9.9");
            if (Bridge.GameVersion(temp) != "9.9.9") return false;
            File.WriteAllText(Path.Combine(mod, "version-override.txt"), "8.8.8");
            if (Bridge.GameVersion(temp) != "1.1.2") return false;
            Directory.CreateDirectory(Path.Combine(lib, "FotG_R-1.1.3", "data"));
            File.WriteAllText(Path.Combine(lib, "FotG_R-1.1.3", "mod.json"), "{}");
            return Bridge.GameVersion(temp) == "1.1.3";
        } catch { return false; }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }
    static int SelfTest() {
        WinInput.DryRun=true;
        MissionKeys mission = new MissionKeys();
        if (mission.Read("FLIGHT|missionkey=1|missionseq=8") != 0 ||
            mission.Read("FLIGHT|missionkey=1|missionseq=9") != 0x31 ||
            mission.Read("FLIGHT|missionkey=1|missionseq=9") != 0 ||
            mission.Read("FLIGHT|missionkey=arbitrary|missionseq=10") != 0 ||
            mission.Read("FLIGHT|missionkey=log|missionseq=11") != 0x73 ||
            mission.Read("FLIGHT|missionkey=fleet|missionseq=12") != 0x7A ||
            mission.Read("FLIGHT|missionkey=fleet_page|missionseq=13") != 0x20 ||
            mission.Read("FLIGHT|missionkey=enter|missionseq=131") != 0x0D ||
            mission.Read("FLIGHT|missionkey=mouse_mode|missionseq=132") != 0x4E ||
            mission.Read("FLIGHT|missionkey=link_guns|missionseq=133") != 0x45 ||
            mission.Read("FLIGHT|missionkey=link_torpedoes|missionseq=134") != 0x51 ||
            mission.Read("FLIGHT|missionkey=shield_front|missionseq=135") != 0x100 ||
            mission.Read("FLIGHT|missionkey=targeting_computer|missionseq=136") != 0x5A ||
            mission.Read("FLIGHT|missionkey=power_shields|missionseq=137") != 0x101 ||
            mission.Read("FLIGHT|missionkey=power_lasers|missionseq=138") != 0x102 ||
            mission.Read("FLIGHT|missionkey=power_engines|missionseq=139") != 0x103 ||
            mission.Read("FLIGHT|missionkey=0|missionseq=14") != 0x30 ||
            mission.Read("FLIGHT|missionkey=9|missionseq=15") != 0x39 ||
            mission.Read("FLIGHT|missionkey=9|missionseq=0") != 0 ||
            mission.Read("FLIGHT|missionkey=9|missionseq=bad") != 0) return 15;
        if (!SelfTestHyperspace()) return 14;
        if (Marshal.SizeOf(typeof(WinInput.INPUT)) != 40) return 1;
        if (Marshal.SizeOf(typeof(XPad)) != 12 || Marshal.SizeOf(typeof(XState)) != 16) return 2;
        if (Math.Abs(Math.Max(-1.0, (short)-32768 / 32767.0)) != 1) return 3;
        if (!SelfTestGameLayouts()) return 11;
        if (!GameInstall.SelfTest()) return 12;
        if (!Bridge.EngineFlags(true).Contains(" -vr -stretch_menu") || Bridge.EngineFlags(false).Contains(" -vr")) return 13;
        if (NormalizeGameRoot("C:\\Games\\Fate of the Galaxy 1.1.0\\\"") != @"C:\Games\Fate of the Galaxy 1.1.0\") return 4;
        if (WinInput.ArrowScan(0x26)!=0x48 || WinInput.ArrowScan(0x25)!=0x4b ||
            WinInput.ArrowScan(0x27)!=0x4d || WinInput.ArrowScan(0x28)!=0x50 ||
            WinInput.KeyFlags(0x26,true)!=9 || WinInput.KeyFlags(0x26,false)!=11 ||
            WinInput.KeyFlags(0x41,true)!=0 || WinInput.KeyFlags(0x41,false)!=2) return 10;
        DpadGesture gesture = new DpadGesture();
        if (gesture.Update(1,0,250,true,false).Tap!=0 || gesture.Update(1,249,250,true,false).Look!=0 ||
            gesture.Update(0,249,250,true,false).Tap!=1) return 5;
        if (gesture.Update(8,300,250,true,false).Tap!=0 || gesture.Update(8,550,250,true,false).Look!=8 ||
            gesture.Update(0,600,250,true,false).Tap!=0) return 6;
        gesture.Reset();
        if (gesture.Update(4,700,250,false,false).Tap!=4 || gesture.Update(0,900,250,false,false).Tap!=0) return 7;
        gesture.Reset();
        if (gesture.Update(2,1000,250,true,true).Tap!=2 || gesture.Update(2,1500,250,true,true).Look!=0) return 8;
        gesture.Reset();
        if (gesture.Update(1,2000,250,true,false).Tap!=0 || gesture.Update(0,2250,250,true,false).Tap!=1) return 9;
        return 0;
    }
    static bool SelfTestHyperspace() {
        const int lb=256, rb=512, both=768, a=4096;
        HyperspaceGesture g = new HyperspaceGesture();
        g.Update(0,0);
        if (g.Update(lb|a,10).Buttons != a) return false;
        if (g.Update(both|a,110).Buttons != a) return false;
        if (g.Update(both,409).Jump) return false;
        if (!g.Update(both,410).Jump || g.Update(both,900).Jump) return false;
        if (g.Update(lb,920).Buttons != 0 || g.Update(both,930).Jump) return false;
        g.Update(0,950);
        // A short single tap survives the decision window exactly once.
        if (g.Update(rb,1000).Buttons != 0) return false;
        if (g.Update(0,1050).Buttons != rb || g.Update(0,1075).Buttons != 0) return false;
        // Single holds resume normally; a late second shoulder is not a jump.
        g.Update(lb,1100);
        if (g.Update(lb,1220).Buttons != lb) return false;
        if (g.Update(both,1230).Buttons != both || g.Update(both,1800).Jump) return false;
        g.Update(0,1900);
        // A canceled chord consumes both individual actions until release.
        g.Update(both,2000);
        if (g.Update(rb,2100).Buttons != 0 || g.Update(both,2500).Jump) return false;
        g.Update(0,2600);
        g.Update(both,2700);
        g.Reset(); // focus loss, disconnect, popup or wheel opening
        if (g.Update(both,3200).Jump || g.Update(both,3200).Buttons != 0) return false;
        g.Update(0,3300);
        // Exact window boundary and reverse order work; unrelated buttons pass.
        g.Update(rb,3400);
        if (g.Update(both|a,3520).Buttons != a || !g.Update(both,3820).Jump) return false;
        g.Update(0,3900);
        g.Update(lb,4000);
        if (g.Update(both,4121).Jump || g.Update(both,4600).Jump) return false;
        return true;
    }
}
