using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Install / Repair, built into Setup so it runs no scripts. This is the former Install.ps1 and
// BuildFlightPreset.ps1, step for step; the preset text it writes is byte-identical to theirs.
// Setup.exe --install --game <folder> [--game-version <v>] [--override-version-check]
//           [--skip-pilot-reservation] runs it without the window, for advanced use and tests.
sealed class InstallOptions {
    public string GameRoot, GameVersion, PackageRoot;
    public bool OverrideVersionCheck, SkipPilotReservation;
}

sealed class InstallException : Exception { public InstallException(string m) : base(m) { } }

static class Installer {
    // Supported game releases, newest first; keep in step with Setup.cs and WheelBridge.cs.
    public static readonly string[] SupportedVersions = { "1.1.2", "1.1.1", "1.1.0" };
    static readonly string[] LauncherNames = { "Play with Gamepad Wheel.exe", "Configure Gamepad Wheel.exe", "Play with Gamepad Wheel VR.exe" };
    static readonly string[] LegacyNames = { "Play with Gamepad Wheel.cmd", "Configure Gamepad Wheel.cmd" };
    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    static string Stamp() { return DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"); }
    static string Release(string root, string version) { return Path.Combine(root, "kn_portable", "Library", "FotG_R", "FotG_R-" + version); }

    public static void Run(InstallOptions o, Action<string> log, Action<string> warn) {
        string root = Path.GetFullPath(o.GameRoot).TrimEnd('\\');
        string supportedText = String.Join(", ", SupportedVersions.Reverse());
        bool versionOverridden = false;
        string gameVersion = o.GameVersion;
        if (!String.IsNullOrEmpty(gameVersion)) {
            if (!SupportedVersions.Contains(gameVersion)) {
                if (!o.OverrideVersionCheck)
                    throw new InstallException("Fate of the Galaxy " + gameVersion + " is not supported by this build (supported: " + supportedText + "). Use the version override to install anyway; compatibility cannot be guaranteed.");
                versionOverridden = true;
            }
            if (!File.Exists(Path.Combine(Release(root, gameVersion), "mod.json")))
                throw new InstallException("Fate of the Galaxy " + gameVersion + " was not found in this game folder.");
        } else {
            gameVersion = SupportedVersions.FirstOrDefault(v => File.Exists(Path.Combine(Release(root, v), "mod.json")));
        }
        if (gameVersion == null)
            throw new InstallException("Select a complete Fate of the Galaxy " + supportedText + " Windows installation (the folder containing kn_portable).");
        if (versionOverridden)
            warn("VERSION CHECK OVERRIDDEN: Fate of the Galaxy " + gameVersion + " is not on this build's supported list (" + supportedText + "). Compatibility cannot be guaranteed until Flight Control is updated for this version.");

        EngineInfo engine = GameInstall.FindEngine(root, gameVersion);
        if (engine == null)
            throw new InstallException("Fate of the Galaxy " + gameVersion + " was found, but not its FSO engine in kn_portable\\Library\\bin. Launch the game once from Knossos so it installs the engine, then try again.");
        string engineSdl = engine.Sdl3 ? "sdl3" : "sdl2";
        log("Engine: " + engine.Label);
        var manifest = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(Release(root, gameVersion), "mod.json")));
        if (manifest == null || !manifest.ContainsKey("version") || Convert.ToString(manifest["version"]) != gameVersion)
            throw new InstallException("The FotG_R-" + gameVersion + " manifest reports a different version. Repair the game in Knossos first.");
        log("Fate of the Galaxy " + gameVersion + " found.");
        if (Running("fs2_open")) throw new InstallException("Close Fate of the Galaxy before installing the command wheel.");
        if (Running("ControllerSetup") || Running("WheelBridge"))
            throw new InstallException("Close the Gamepad Wheel settings app and controller bridge before installing or repairing.");

        // Build the presets once in memory before anything is changed, so control defaults the
        // builder does not recognise (likelier under a version override) change nothing.
        try { Presets.Build(root, gameVersion, Presets.Sdl3Axes); }
        catch (Exception e) {
            throw new InstallException("The control defaults of Fate of the Galaxy " + gameVersion + " are not in a layout Flight Control recognises, so nothing was changed. Wait for a Flight Control update for this version. Details: " + e.Message);
        }

        string prefsData = Path.Combine(root, "kn_portable", "HardLightProductions", "FreeSpaceOpen", "data");
        if (!o.SkipPilotReservation) {
            string players = Path.Combine(prefsData, "players");
            if (!Directory.Exists(players) || !File.Exists(Path.Combine(prefsData, "FotG_R_settings.ini")))
                throw new InstallException("Run Fate of the Galaxy once, create a pilot, then close it before installing the wheel.");
            bool native = Directory.GetFiles(players, "*.json").Any(f => {
                try { var d = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(f)); return d != null && d.ContainsKey("version") && Convert.ToInt32(d["version"]) == 4; }
                catch { return false; }
            });
            if (!native) throw new InstallException("No native version-4 pilot was found. Run the game, create a pilot, then close it before installing the wheel.");
        }

        string package = o.PackageRoot;
        string source = Path.Combine(package, "FotG_Gamepad_Wheel");
        foreach (string name in new[] { "WheelBridge.exe", @"ControllerSetup\ControllerSetup.exe", @"data\tables\lua\zzz_fotg_wheel-sct.lua", @"data\tables\lua\zz_fotg_wheel_config-sct.lua", @"data\tables\zzz_fotg_wheel-hdg.tbm" })
            if (!File.Exists(Path.Combine(source, name))) throw new InstallException("Package is incomplete: " + name);
        foreach (string name in LauncherNames)
            if (!File.Exists(Path.Combine(package, name))) throw new InstallException("Package is incomplete: " + name);

        // FSO names its INI after the first -mod path component. SDL2 and SDL3 give the same
        // pad different GUIDs, so ask the engine's own SDL library. Stick numbering is kept as
        // FSO axis names for left X, left Y, right X, right Y (engine-axes.txt).
        string activeSettings = Path.Combine(prefsData, "GamepadMods_settings.ini");
        string existingActive = File.Exists(activeSettings) ? File.ReadAllText(activeSettings) : null;
        string axisRecord = Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "engine-axes.txt");
        string recordedAxes = File.Exists(axisRecord) ? File.ReadAllText(axisRecord).Trim() : "";
        string axisMap = "X_AXIS,Y_AXIS,Z_AXIS,RX_AXIS";
        List<Pad> devices;
        if (engine.Sdl3) devices = SdlProbe.Sdl3(Path.GetDirectoryName(engine.Exe));
        else {
            devices = SdlProbe.Sdl2(Path.GetDirectoryName(engine.Exe));
            var m = Regex.Match(recordedAxes, "^sdl2 (?<map>[A-Z_,]+)$", RegexOptions.IgnoreCase);
            axisMap = m.Success ? m.Groups["map"].Value : "X_AXIS,Y_AXIS,Z_AXIS,RX_AXIS";
        }
        string selection = null; Pad chosen = null;
        if (devices.Count > 0) {
            chosen = devices.Count == 1 ? devices[0] : devices.FirstOrDefault(d => Regex.IsMatch(d.Name ?? "", "XInput|Controller|Gamepad", RegexOptions.IgnoreCase));
            if (chosen == null) throw new InstallException("Multiple joysticks were found. Connect only the gamepad you want to use, then install again.");
            selection = "{\"guid\":\"" + chosen.Guid + "\",\"id\":" + chosen.Id + "}";
            if (!engine.Sdl3) {
                axisMap = chosen.AxisMap;
                if (!chosen.Detected) warn("The sticks of " + chosen.Name + " could not be told apart from its triggers; the common Xbox layout was assumed. Check the axes in flight.");
            }
        } else if (existingActive == null || !Regex.IsMatch(existingActive, @"(?im)^Joystick\s*=")) {
            throw new InstallException("Connect the gamepad and run Install / Repair again so the engine can identify its flight axes.");
        } else {
            warn("No connected gamepad was found. Existing joystick selection was kept; run Install / Repair with the gamepad connected before testing flight.");
        }
        string oldSelection = existingActive != null ? Regex.Match(existingActive, @"(?im)^Joystick\s*=\s*([^\r\n]+)").Groups[1].Value.Trim() : "";
        if (selection != null && selection != oldSelection) {
            string profileBackup = Path.Combine(prefsData, "..", "ControllerSetupBackups", Stamp() + "-before-wheel-profile");
            Directory.CreateDirectory(profileBackup);
            if (existingActive != null) File.Copy(activeSettings, Path.Combine(profileBackup, "GamepadMods_settings.ini"), true);
            string updated;
            if (existingActive == null) updated = "[Input]\r\nJoystick=" + selection + "\r\n";
            else if (Regex.IsMatch(existingActive, @"(?im)^Joystick\s*=")) updated = Regex.Replace(existingActive, @"(?im)^Joystick\s*=[^\r\n]*", "Joystick=" + selection.Replace("$", "$$"));
            else if (Regex.IsMatch(existingActive, @"(?im)^\[Input\][ \t]*\r?$")) updated = Regex.Replace(existingActive, @"(?im)^(\[Input\][ \t]*\r?\n)", "$1" + "Joystick=" + selection.Replace("$", "$$") + "\r\n");
            else updated = existingActive.TrimEnd() + "\r\n\r\n[Input]\r\nJoystick=" + selection + "\r\n";
            File.WriteAllText(activeSettings, updated, Utf8NoBom);
            log("Selected " + engineSdl.ToUpperInvariant() + " joystick: " + chosen.Name + " (" + chosen.Guid + ")");
        }

        string destination = Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel");
        string parent = Path.GetDirectoryName(destination);
        Directory.CreateDirectory(parent);
        string existingConfig = Path.Combine(destination, "data", "tables", "lua", "zz_fotg_wheel_config-sct.lua");
        byte[] savedConfig = File.Exists(existingConfig) ? File.ReadAllBytes(existingConfig) : null;

        // Keep one dated install snapshot away from the launchers and active mod.
        string backupRoot = Path.Combine(parent, "_WheelInstallBackups");
        string snapshot = null;
        if (Directory.Exists(destination)) {
            snapshot = Path.Combine(backupRoot, Stamp());
            CopyTree(destination, Path.Combine(snapshot, "FotG_Gamepad_Wheel"));
            // A repair replaces the prior add-on tree after a full snapshot.
            string resolved = Path.GetFullPath(destination).TrimEnd('\\'), resolvedParent = Path.GetFullPath(parent).TrimEnd('\\');
            if (!resolved.StartsWith(resolvedParent + "\\", StringComparison.OrdinalIgnoreCase) || !String.Equals(Path.GetFileName(resolved), "FotG_Gamepad_Wheel", StringComparison.OrdinalIgnoreCase))
                throw new InstallException("The add-on destination did not resolve inside GamepadMods.");
            Directory.Delete(resolved, true);
        }
        CopyTree(source, destination);
        if (savedConfig != null) File.WriteAllBytes(existingConfig, savedConfig);
        File.WriteAllText(Path.Combine(destination, "data", "tables", "Controlconfigdefaults.tbl"), Presets.Build(root, gameVersion, axisMap), Utf8NoBom);
        log("Generated FotG Classic Native preset at " + Path.Combine(destination, "data", "tables", "Controlconfigdefaults.tbl"));
        File.WriteAllText(axisRecord, engineSdl + " " + axisMap, Utf8NoBom);
        log("Stick axes (" + engineSdl + "): " + axisMap);
        // The launcher normally picks the newest supported release. An override records the
        // chosen release so the launcher uses it too; a normal install clears that record.
        string overrideRecord = Path.Combine(destination, "version-override.txt");
        if (versionOverridden) File.WriteAllText(overrideRecord, gameVersion, Utf8NoBom);
        else if (File.Exists(overrideRecord)) File.Delete(overrideRecord);
        if (savedConfig != null) {
            string configText = File.ReadAllText(existingConfig);
            if (!Regex.IsMatch(configText, @"\bflight_axes\s*=", RegexOptions.IgnoreCase)) {
                configText = configText.Replace("FotGWheelConfig = {", "FotGWheelConfig = {\r\n    flight_axes = \"classic\",");
                File.WriteAllText(existingConfig, configText, Utf8NoBom);
            }
        }
        string console = Path.Combine(destination, "ControllerSetup", "ControllerSetup.exe");
        // The control table was just regenerated; put back a saved Custom axis layout.
        if (savedConfig != null && Regex.IsMatch(Encoding.UTF8.GetString(savedConfig), "\\bflight_axes\\s*=\\s*\"custom\"", RegexOptions.IgnoreCase)) {
            if (RunConsole(console, "--apply-custom-axes", root) != 0) warn("Your Custom flight axes could not be restored; the game will use Classic until you open Configure and save them again.");
            else log("Custom flight axes restored.");
        }
        if (!o.SkipPilotReservation && RunConsole(console, "--reserve-wheel", root) != 0)
            throw new InstallException("Pilot control reservation failed. The add-on was copied, but launchers were not created. See Controller Setup for details.");

        foreach (string name in LauncherNames.Concat(LegacyNames)) {
            string file = Path.GetFullPath(Path.Combine(root, name));
            if (!file.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) throw new InstallException("Launcher path escaped the selected game folder.");
            if (File.Exists(file)) {
                if (snapshot == null) snapshot = Path.Combine(backupRoot, Stamp());
                Directory.CreateDirectory(snapshot);
                File.Copy(file, Path.Combine(snapshot, name), true);
            }
        }
        foreach (string name in LauncherNames) File.Copy(Path.Combine(package, name), Path.Combine(root, name), true);
        foreach (string name in LegacyNames) { string old = Path.Combine(root, name); if (File.Exists(old)) File.Delete(old); }
        log("Installed addon at " + destination);
        log("Launch with: " + Path.Combine(root, LauncherNames[0]));
        log("Configure with: " + Path.Combine(root, LauncherNames[1]));
        log(o.SkipPilotReservation ? "Before flight, open Controller Setup and use Reserve wheel buttons for each pilot."
            : "Native pilot A, B, X, Y, LB, RB, Start, L3, R3 and D-pad bindings were reserved with backups where needed.");
    }

    static bool Running(string prefix) {
        return Process.GetProcesses().Any(p => { try { return p.ProcessName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase); } catch { return false; } });
    }
    static int RunConsole(string exe, string verb, string root) {
        var info = new ProcessStartInfo(exe, verb + " --game \"" + root + "\"") { UseShellExecute = false, CreateNoWindow = true };
        using (var p = Process.Start(info)) { p.WaitForExit(); return p.ExitCode; }
    }
    static void CopyTree(string from, string to) {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) {
            string target = Path.Combine(to, file.Substring(from.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }
}

// The Classic presets, from the game's own control defaults (formerly BuildFlightPreset.ps1).
static class Presets {
    public const string Sdl3Axes = "X_AXIS,Y_AXIS,Z_AXIS,RX_AXIS";
    static string Bind(string action) { return @"(?ms)^\s*\$Bind:\s*" + action + @"\b.*?(?=^\s*\$Bind:|^#End)"; }

    // Knossos installs a release as package folders (FG_Core\data\...) or one merged data folder.
    public static string TablePath(string root, string version) {
        string release = Path.Combine(root, "kn_portable", "Library", "FotG_R", "FotG_R-" + version);
        foreach (string c in new[] { @"FG_Core\data\tables\Controlconfigdefaults.tbl", @"data\tables\Controlconfigdefaults.tbl" }) {
            string p = Path.Combine(release, c);
            if (File.Exists(p)) return p;
        }
        return Path.Combine(release, @"FG_Core\data\tables\Controlconfigdefaults.tbl");
    }

    // axisMap: FSO axis names of left X, left Y, right X, right Y on the engine in use.
    public static string Build(string root, string version, string axisMap) {
        string stockPath = TablePath(root, version);
        if (!File.Exists(stockPath)) throw new InvalidDataException("FotG " + version + " control defaults were not found.");
        return FromTable(File.ReadAllText(stockPath), axisMap);
    }

    public static string FromTable(string stock, string axisMap) {
        // The D-pad is also the engine's native view hat. Keep the keyboard view arrows, but
        // remove the hat in this overlay; the bridge sends the arrows after a hold.
        foreach (string action in new[] { "PADLOCK_UP", "PADLOCK_DOWN", "PADLOCK_LEFT", "PADLOCK_RIGHT" }) {
            var m = Regex.Match(stock, Bind(action));
            if (!m.Success || !Regex.IsMatch(m.Value, @"\$Input:\s*KEY_(UP|DOWN|LEFT|RIGHT)", RegexOptions.IgnoreCase))
                throw new InvalidDataException("Expected keyboard view binding was not found: " + action);
            string replacement = m.Value.TrimEnd() + "\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n";
            stock = stock.Remove(m.Index, m.Length).Insert(m.Index, replacement);
        }
        var section = Regex.Match(stock, @"(?ms)^#ControlConfigOverride\s*.*?^#End");
        if (!section.Success || Regex.Matches(stock, @"(?m)^\s*\$Name:").Count != 1)
            throw new InvalidDataException("Unexpected control defaults layout; no flight preset generated.");
        string classic = Regex.Replace(section.Value, @"(?m)^\$Name:\s*default\b[^\r\n]*", "$Name: FotG Classic Native", RegexOptions.IgnoreCase);
        if (classic == section.Value) throw new InvalidDataException("Default control preset header was not found.");
        var fire = Regex.Match(classic, Bind("FIRE_PRIMARY"));
        if (!fire.Success) throw new InvalidDataException("Primary fire binding was not found.");
        string safeFire = Regex.Replace(fire.Value, @"(?ms)(\$Primary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:[^\r\n]*\r?\n\s*\$Input:\s*0\s*\r?\n",
            "$1            $Controller: CID_NONE\r\n", RegexOptions.IgnoreCase);
        if (safeFire == fire.Value) throw new InvalidDataException("Expected A-button primary fire binding was not found.");
        classic = classic.Remove(fire.Index, fire.Length).Insert(fire.Index, safeFire);
        // LB, RB, Start, A, B and X belong to the wheel; clear their old native secondary bindings.
        foreach (var c in new[] { Tuple.Create("CYCLE_PRIMARY_WEAPON_SEQUENCE", 4), Tuple.Create("CUSTOM_CONTROL_5", 5), Tuple.Create("SHIELD_EQUALIZE", 7),
                                  Tuple.Create("TARGET_SHIP_IN_RETICLE", 1), Tuple.Create("TARGET_CLOSEST_SHIP_ATTACKING_SELF", 2), Tuple.Create("TARGET_NEXT_CLOSEST_HOSTILE", 3) }) {
            var m = Regex.Match(classic, Bind(Regex.Escape(c.Item1)));
            if (!m.Success) throw new InvalidDataException("Expected control binding was not found: " + c.Item1);
            string edited = Regex.Replace(m.Value, @"(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*" + c.Item2 + @"\s*\r?\n",
                "$1            $Controller: CID_NONE\r\n", RegexOptions.IgnoreCase);
            if (edited == m.Value) throw new InvalidDataException("Expected joystick binding was not changed: " + c.Item1);
            classic = classic.Remove(m.Index, m.Length).Insert(m.Index, edited);
        }
        // FSO treats all four flight axes as relative analog items and warns at startup unless
        // RELATIVE is listed. Right stick yaw/pitch, left stick roll/relative throttle.
        string axes =
            "    $Bind: JOY_HEADING_AXIS\r\n        $Primary:\r\n            $Controller: CID_JOY0\r\n            $Flags: AXIS, RELATIVE\r\n            $Input: __RIGHT_X__\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n" +
            "    $Bind: JOY_PITCH_AXIS\r\n        $Primary:\r\n            $Controller: CID_JOY0\r\n            $Flags: AXIS, RELATIVE\r\n            $Input: __RIGHT_Y__\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n" +
            "    $Bind: JOY_BANK_AXIS\r\n        $Primary:\r\n            $Controller: CID_JOY0\r\n            $Flags: AXIS, RELATIVE\r\n            $Input: __LEFT_X__\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n" +
            "    $Bind: JOY_REL_THROTTLE_AXIS\r\n        $Primary:\r\n            $Controller: CID_JOY0\r\n            $Flags: AXIS, RELATIVE, INVERTED\r\n            $Input: __LEFT_Y__\r\n        $Secondary:\r\n            $Controller: CID_NONE\r\n" +
            "    $Bind: JOY_ABS_THROTTLE_AXIS\r\n        $Primary:\r\n            $Controller: CID_NONE\r\n        $Secondary:\r\n            $Controller: CID_NONE";
        // Drop axis bindings the game's own default already carries (1.1.2 lists the roll axis as unbound).
        foreach (string axis in new[] { "JOY_HEADING_AXIS", "JOY_PITCH_AXIS", "JOY_BANK_AXIS", "JOY_REL_THROTTLE_AXIS", "JOY_ABS_THROTTLE_AXIS" })
            classic = Regex.Replace(classic, @"(?ms)^[ \t]*\$Bind:\s*" + axis + @"\b.*?(?=^\s*\$Bind:|^#End)", "");
        string[] stick = (axisMap ?? "").Split(',').Select(s => s.Trim()).ToArray();
        string[] valid = { "X_AXIS", "Y_AXIS", "Z_AXIS", "RX_AXIS", "RY_AXIS", "RZ_AXIS" };
        if (stick.Length != 4 || stick.Any(s => !valid.Contains(s)) || stick.Distinct().Count() != 4)
            throw new InvalidDataException("The stick map must name four different FSO axes (left X, left Y, right X, right Y): " + axisMap);
        axes = axes.Replace("__LEFT_X__", stick[0]).Replace("__LEFT_Y__", stick[1]).Replace("__RIGHT_X__", stick[2]).Replace("__RIGHT_Y__", stick[3]);
        classic = Regex.Replace(classic, @"(?m)^#End\b", (axes + "\r\n#End").Replace("$", "$$"), RegexOptions.IgnoreCase);
        string inverted = classic.Replace("$Name: FotG Classic Native", "$Name: FotG Classic Inverted");
        var pitch = Regex.Match(inverted, Bind("JOY_PITCH_AXIS"));
        if (!pitch.Success) throw new InvalidDataException("Classic pitch binding was not found.");
        inverted = inverted.Remove(pitch.Index, pitch.Length).Insert(pitch.Index, pitch.Value.Replace("$Flags: AXIS", "$Flags: AXIS, INVERTED"));
        string output = stock.TrimEnd() + "\r\n\r\n" + classic + "\r\n\r\n" + inverted + "\r\n";
        return Regex.Replace(output, @"\r?\n", "\r\n");
    }
}

sealed class Pad { public int Id; public string Guid, Name, AxisMap = Presets.Sdl3Axes; public bool Detected = true; }

// Reads the connected pads through the engine's own SDL library, as the engine sees them.
static class SdlProbe {
    [StructLayout(LayoutKind.Sequential)] struct SdlGuid { public ulong a, b; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Init")] static extern bool S3_Init(uint flags);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoysticks")] static extern IntPtr S3_GetJoysticks(out int count);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickGUIDForID")] static extern SdlGuid S3_Guid(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GUIDToString")] static extern void S3_GuidToString(SdlGuid guid, StringBuilder text, int length);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickNameForID")] static extern IntPtr S3_Name(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")] static extern void S3_Free(IntPtr p);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")] static extern void S3_Quit();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Init")] static extern int S2_Init(uint flags);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_NumJoysticks")] static extern int S2_Count();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetDeviceGUID")] static extern SdlGuid S2_Guid(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetGUIDString")] static extern void S2_GuidToString(SdlGuid guid, StringBuilder text, int length);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickNameForIndex")] static extern IntPtr S2_Name(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickOpen")] static extern IntPtr S2_Open(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickNumAxes")] static extern int S2_Axes(IntPtr j);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickUpdate")] static extern void S2_Update();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetAxis")] static extern short S2_Axis(IntPtr j, int axis);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickClose")] static extern void S2_Close(IntPtr j);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")] static extern void S2_Quit();
    static readonly string[] AxisNames = { "X_AXIS", "Y_AXIS", "Z_AXIS", "RX_AXIS", "RY_AXIS", "RZ_AXIS" };

    public static List<Pad> Sdl3(string dllDir) {
        if (!File.Exists(Path.Combine(dllDir, "SDL3.dll"))) throw new InstallException("The engine's SDL3.dll was not found.");
        SetDllDirectory(dllDir);
        var pads = new List<Pad>();
        if (!S3_Init(0x200)) throw new InstallException("SDL joystick initialization failed.");
        try {
            int count; IntPtr ids = S3_GetJoysticks(out count);
            try {
                for (int i = 0; i < count; i++) {
                    uint id = (uint)Marshal.ReadInt32(ids, 4 * i);
                    var text = new StringBuilder(64); S3_GuidToString(S3_Guid(id), text, text.Capacity);
                    pads.Add(new Pad { Id = i, Guid = text.ToString(), Name = Marshal.PtrToStringAnsi(S3_Name(id)) });
                }
            } finally { S3_Free(ids); }
        } finally { S3_Quit(); }
        return pads;
    }

    // Triggers rest at the minimum, so the other axes in order are left X, left Y, right X, right Y.
    public static List<Pad> Sdl2(string dllDir) {
        if (!File.Exists(Path.Combine(dllDir, "SDL2.dll"))) throw new InstallException("The engine's SDL2.dll was not found.");
        SetDllDirectory(dllDir);
        var pads = new List<Pad>();
        if (S2_Init(0x200) != 0) throw new InstallException("SDL2 joystick initialization failed.");
        try {
            for (int i = 0; i < S2_Count(); i++) {
                var text = new StringBuilder(64); S2_GuidToString(S2_Guid(i), text, text.Capacity);
                string name = Marshal.PtrToStringAnsi(S2_Name(i));
                IntPtr j = S2_Open(i);
                if (j == IntPtr.Zero) continue;
                try {
                    int count = Math.Min(6, S2_Axes(j));
                    var low = new HashSet<int>();
                    for (int n = 0; n < 8; n++) {
                        System.Threading.Thread.Sleep(40); S2_Update();
                        for (int a = 0; a < count; a++) if (n >= 3 && S2_Axis(j, a) <= -30000) low.Add(a);
                    }
                    var sticks = Enumerable.Range(0, count).Where(a => !low.Contains(a)).ToArray();
                    bool detected = count >= 6 && low.Count == 2 && sticks.Length == 4;
                    // Otherwise keep the order FSO 26.0.1's SDL 2.26.5 reports for an Xbox pad.
                    pads.Add(new Pad { Id = i, Guid = text.ToString(), Name = name, Detected = detected,
                        AxisMap = detected ? String.Join(",", sticks.Select(a => AxisNames[a])) : Presets.Sdl3Axes });
                } finally { S2_Close(j); }
            }
        } finally { S2_Quit(); }
        return pads;
    }
}
