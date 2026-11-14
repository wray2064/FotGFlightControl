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
    // Repair run from inside Hangar Bay: it is open, so it is neither a blocker nor replaced.
    public bool KeepHangarBay;
}

sealed class InstallException : Exception { public InstallException(string m) : base(m) { } }

static class Installer {
    // Supported game releases, newest first; keep in step with Setup.cs and WheelBridge.cs.
    public static readonly string[] SupportedVersions = { "1.1.3", "1.1.2", "1.1.1", "1.1.0" };
    static readonly string[] LauncherNames = { "Play with Gamepad Wheel.exe", "Configure Gamepad Wheel.exe", "Play with Gamepad Wheel VR.exe" };
    static readonly string[] LegacyNames = { "Play with Gamepad Wheel.cmd", "Configure Gamepad Wheel.cmd" };
    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    static string Stamp() { return DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"); }
    static string Release(string root, string version) { return Path.Combine(root, "kn_portable", "Library", "FotG_R", "FotG_R-" + version); }

    public static void Run(InstallOptions o, Action<string> log, Action<string> warn) {
        string root = Path.GetFullPath(o.GameRoot).TrimEnd('\\');
        if (GameInstall.HasMyMod(root)) { InstallMyMod(o, root, log); return; }
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
        Match preferredMatch = Regex.Match(existingActive ?? "", @"(?im)^XInputSlot\s*=\s*([0-3])\s*$");
        int preferredSlot = preferredMatch.Success ? Int32.Parse(preferredMatch.Groups[1].Value) : -1;
        string oldSelection = existingActive != null ? Regex.Match(existingActive, @"(?im)^Joystick\s*=\s*([^\r\n]+)").Groups[1].Value.Trim() : "";
        string axisRecord = Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "engine-axes.txt");
        string recordedAxes = File.Exists(axisRecord) ? File.ReadAllText(axisRecord).Trim() : "";
        string axisMap = "X_AXIS,Y_AXIS,Z_AXIS,RX_AXIS";
        List<SdlPad> devices;
        if (engine.Sdl3) devices = SdlProbe.Sdl3(Path.GetDirectoryName(engine.Exe));
        else {
            devices = SdlProbe.Sdl2(Path.GetDirectoryName(engine.Exe));
            var m = Regex.Match(recordedAxes, "^sdl2 (?<map>[A-Z_,]+)$", RegexOptions.IgnoreCase);
            axisMap = m.Success ? m.Groups["map"].Value : "X_AXIS,Y_AXIS,Z_AXIS,RX_AXIS";
        }
        string selection = null; SdlPad chosen = null;
        if (devices.Count > 0) {
            chosen = preferredSlot >= 0 ? devices.FirstOrDefault(d => d.PlayerIndex == preferredSlot)
                : devices.Count == 1 ? devices[0] : devices.FirstOrDefault(d => Regex.IsMatch(d.Name ?? "", "XInput|Controller|Gamepad", RegexOptions.IgnoreCase));
            if (chosen == null) {
                if (preferredSlot < 0 || String.IsNullOrEmpty(oldSelection))
                    throw new InstallException("Multiple joysticks were found. Select the active gamepad in Hangar Bay (Service > Choose controller).");
                warn("The saved controller for XInput slot " + preferredSlot + " is unavailable; its FSO joystick selection was preserved.");
                selection = oldSelection;
            } else {
                selection = "{\"guid\":\"" + chosen.Guid + "\",\"id\":" + chosen.Id + "}";
                if (!engine.Sdl3) {
                    axisMap = chosen.AxisMap;
                    if (!chosen.Detected) warn("The sticks of " + chosen.Name + " could not be told apart from its triggers; the common Xbox layout was assumed. Check the axes in flight.");
                }
            }
        } else if (existingActive == null || !Regex.IsMatch(existingActive, @"(?im)^Joystick\s*=")) {
            throw new InstallException("Connect the gamepad and run Install / Repair again so the engine can identify its flight axes.");
        } else {
            warn("No connected gamepad was found. Existing joystick selection was kept; run Install / Repair with the gamepad connected before testing flight.");
        }
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
            // Settings from Flight Control 1.1.4 or earlier: keep their old behaviour, back up, and say so once.
            var notes = new List<string>();
            string migrated = ConfigMigration.Migrate(configText, notes);
            if (migrated != configText) {
                File.WriteAllText(existingConfig, migrated, Utf8NoBom);
                log("Updated your saved settings for this version. Your previous settings are saved in " + snapshot + ".");
                foreach (string note in notes) log("  " + note);
                File.WriteAllText(Path.Combine(destination, "migration-notice.txt"),
                    "Flight Control was updated and kept your settings.\r\n\r\n" + String.Join("\r\n\r\n", notes.ToArray()) +
                    "\r\n\r\nYour previous settings were saved in:\r\n" + snapshot + "\r\n", Utf8NoBom);
            }
        }
        string console = Path.Combine(destination, "ControllerSetup", "ControllerSetup.exe");
        // The control table was just regenerated; put back a saved Custom axis layout.
        if (savedConfig != null && Regex.IsMatch(Encoding.UTF8.GetString(savedConfig), "\\bflight_axes\\s*=\\s*\"custom\"", RegexOptions.IgnoreCase)) {
            if (RunConsole(console, "--apply-custom-axes", root) != 0) warn("Your Custom flight axes could not be restored; the game will use Classic until you open Configure and save them again.");
            else log("Custom flight axes restored.");
        }
        if (!o.SkipPilotReservation && RunConsole(console, "--reserve-wheel", root) != 0)
            throw new InstallException("Pilot control reservation failed. The add-on was copied, but launchers were not created. Open the Hangar Bay Console for details.");

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
        foreach (string name in new[] { "Hangar Bay.exe", "Setup.exe" }) {
            string from = Path.Combine(package, name);
            if (File.Exists(from)) {
                try { File.Copy(from, Path.Combine(root, name == "Setup.exe" ? "Flight Control Setup.exe" : name), true); }
                catch (IOException) { if (name != "Hangar Bay.exe") throw; warn("Hangar Bay is open; its executable will be refreshed the next time Setup runs while it is closed."); }
            }
        }
        foreach (string name in LegacyNames) { string old = Path.Combine(root, name); if (File.Exists(old)) File.Delete(old); }
        log("Installed addon at " + destination);
        log("Launch with: " + Path.Combine(root, LauncherNames[0]));
        log("Configure with: " + Path.Combine(root, LauncherNames[1]));
        log(o.SkipPilotReservation ? "Before flight, open the Hangar Bay Console and use Reserve wheel buttons for each pilot."
            : "Native pilot A, B, X, Y, LB, RB, Start, L3, R3 and D-pad bindings were reserved with backups where needed.");
    }

    static void InstallMyMod(InstallOptions o, string root, Action<string> log) {
        if (Running("fs2_open") || Running("WheelBridge") || (!o.KeepHangarBay && Running("Hangar Bay")))
            throw new InstallException("Close the game, WheelBridge, and Hangar Bay before installing.");
        string prefs = Path.Combine(root, @"kn_portable\HardLightProductions\FreeSpaceOpen");
        string ini = Path.Combine(prefs, @"data\FotG_2_settings.ini");
        string players = Path.Combine(prefs, @"data\players");
        if (!File.Exists(ini) || !Directory.Exists(players) || !Directory.GetFiles(players, "*.json").Any())
            throw new InstallException("Launch FotG 2 and create a pilot before installing.");
        if (GameInstall.FindEngine(root, "2.0.2") == null)
            throw new InstallException("The FotG 2 SDL3 engine is missing.");
        string source = Path.Combine(o.PackageRoot, "FotG_Gamepad_Wheel");
        string support = Path.Combine(root, "Flight Control Console");
        string core = GameInstall.MyModCore(root);
        string stock = Path.Combine(root, @"kn_portable\Library\FotG_2\FotG_2-2.0.2\FG_Core\data\tables\Controlconfigdefaults.tbl");
        string table = Path.Combine(core, @"data\tables\Controlconfigdefaults.tbl");
        string config = Path.Combine(core, @"data\tables\lua\zz_fotg_wheel_config-sct.lua");
        if (!File.Exists(stock))
            throw new InstallException("This Flight Control build supports Fate of the Galaxy 2.0.2 only, and its control table was not found at " + stock + ". Nothing was changed.");
        string generated = Presets.FromTable(File.ReadAllText(stock), Presets.Sdl3Axes, true);
        var presetSections = Regex.Matches(generated, @"(?ms)^#ControlConfigOverride\b.*?^#End\b");
        if (presetSections.Count != 3) throw new InstallException("Unexpected FotG 2 control presets.");
        generated = Presets.DefaultSectionLast(generated.Remove(presetSections[0].Index, presetSections[0].Length)
            .Insert(presetSections[0].Index, presetSections[1].Value.Replace("$Name: FotG Classic Native", "$Name: default")));
        string backup = Path.Combine(root, "Flight Control Backups", Stamp());
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string data = Path.Combine(source, "data");
        foreach (string file in Directory.GetFiles(data, "*", SearchOption.AllDirectories))
            files.Add(Path.Combine(core, "data", file.Substring(data.Length + 1)), file);
        foreach (string file in new[] { "ControllerSetup.exe", "Help.html", "About.html", @"Assets\FlightControl.ico", @"Assets\FlightControl.png" })
            files.Add(Path.Combine(support, file), Path.Combine(source, "ControllerSetup", file));
        string helpArt = Path.Combine(source, "ControllerSetup", "Assets", "help");
        if (Directory.Exists(helpArt))
            foreach (string file in Directory.GetFiles(helpArt, "*.png"))
                files.Add(Path.Combine(support, "Assets", "help", Path.GetFileName(file)), file);
        files.Add(Path.Combine(support, "WheelBridge.exe"), Path.Combine(source, "WheelBridge.exe"));
        if (!o.KeepHangarBay) files.Add(Path.Combine(root, "Hangar Bay.exe"), Path.Combine(o.PackageRoot, "Hangar Bay.exe"));
        foreach (string file in files.Values)
            if (!File.Exists(file)) throw new InstallException("Package file missing: " + file);
        bool preserve = File.Exists(config) && File.Exists(table)
            && File.Exists(Path.Combine(core, @"data\tables\lua\zzz_fotg_wheel-sct.lua"));
        if (preserve) files.Remove(config);
        if (!preserve) files.Add(table, null);
        string keptTable = preserve ? File.ReadAllText(table) : null;
        string reordered = keptTable != null ? Presets.DefaultSectionLast(keptTable) : null;
        bool fixOrder = reordered != null && reordered != keptTable;
        var written = new List<string>();
        try {
            if (fixOrder) {
                string savedTable = Path.Combine(backup, table.Substring(root.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(savedTable));
                File.Copy(table, savedTable, true);
                written.Add(table);
                File.WriteAllText(table, reordered, Utf8NoBom);
                log("Reordered the FotG 2 control presets so the pilot's default preset is found. The previous table is in Flight Control Backups.");
            }
            foreach (var entry in files) {
                string target = entry.Key;
                if (File.Exists(target)) {
                    string saved = Path.Combine(backup, target.Substring(root.Length + 1));
                    Directory.CreateDirectory(Path.GetDirectoryName(saved));
                    File.Copy(target, saved);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                written.Add(target);
                if (entry.Value == null) File.WriteAllText(target, generated, Utf8NoBom);
                else File.Copy(entry.Value, target, true);
            }
            if (!o.SkipPilotReservation && RunConsole(Path.Combine(support, "ControllerSetup.exe"), "--reserve-wheel", root) != 0)
                throw new InstallException("Could not reserve the pilot's wheel buttons.");
        } catch {
            foreach (string target in written) {
                string saved = Path.Combine(backup, target.Substring(root.Length + 1));
                if (File.Exists(saved)) File.Copy(saved, target, true);
                else File.Delete(target);
            }
            throw;
        }
        log("Installed Flight Control into MyMod. Open Hangar Bay.exe beside Knossos.NET.exe.");
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

// Settings saved by Flight Control 1.1.4 and earlier carry no config_version. Updating must not change what
// those players' buttons and wheels do, so the first Install / Repair after the update writes their old
// behaviour into the config: the new R3/RB/L3/LB holds off, RB's tap on ship mode, and the Targeting order
// back to the default (its slot 6 changed from "Subsystem in sight" to "Targeting computer", so an old saved
// order would silently point at a different action). Systems, Macros and Squad orders are kept. Fresh installs
// ship config_version and the new defaults.
static class ConfigMigration {
    static readonly string[] NewHoldKeys = { "r3_mouse_hold_enabled", "rb_mode_hold_enabled", "l3_gun_hold_enabled", "lb_torpedo_hold_enabled" };
    const string OldTapBindings = "A=\"target_reticle\", B=\"target_attacker\", X=\"target_next\", Y=\"third_gun\", LB=\"cycle_secondary\", RB=\"cycle_ship_mode\", L3=\"match_speed\", R3=\"target_next_subsystem\"";
    const string DefaultTargeting = "targeting = { 1, 2, 3, 4, 5, 6, 7 }";

    // Returns the config unchanged when it is already versioned (or is not a config we recognise).
    public static string Migrate(string config, List<string> notes) {
        if (Regex.IsMatch(config, @"\bconfig_version\s*=")) return config;
        if (!Regex.IsMatch(config, @"FotGWheelConfig\s*=\s*\{")) return config;
        string nl = config.Contains("\r\n") ? "\r\n" : "\n";
        var add = new List<string>();

        foreach (string key in NewHoldKeys)
            if (!Regex.IsMatch(config, @"\b" + key + @"\s*=")) add.Add("  " + key + " = false,");
        if (add.Count > 0)
            notes.Add("The new button holds (R3, RB, L3 and LB) are switched off on this install, so your controls keep working as before. Turn them on in Hangar Bay > Console > Buttons.");

        Match taps = Regex.Match(config, @"\btap_bindings\s*=\s*\{(?<body>[^}]*)\}");
        if (!taps.Success) {
            add.Add("  tap_bindings = { " + OldTapBindings + " },");
            notes.Add("RB still cycles ship mode when you tap it.");
        } else if (!Regex.IsMatch(taps.Groups["body"].Value, @"\bRB\s*=")) {
            int at = taps.Groups["body"].Index;
            config = config.Substring(0, at) + " RB=\"cycle_ship_mode\"," + config.Substring(at);
            notes.Add("RB still cycles ship mode when you tap it.");
        }

        Match layouts = Regex.Match(config, @"\blayouts\s*=\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}");
        if (layouts.Success) {
            Match t = Regex.Match(layouts.Groups["body"].Value, @"\btargeting\s*=\s*\{(?<order>[^}]*)\}");
            if (t.Success) {
                string order = String.Join(",", Regex.Matches(t.Groups["order"].Value, @"\d+").Cast<Match>().Select(m => m.Value).ToArray());
                if (order != "1,2,3,4,5,6,7") {
                    int at = layouts.Groups["body"].Index + t.Index;
                    config = config.Substring(0, at) + DefaultTargeting + config.Substring(at + t.Length);
                    notes.Add("The Targeting wheel order was set back to the default, because its slot 6 changed (Subsystem in sight became Targeting computer). Your Systems, Macros and Squad orders were kept.");
                }
            }
        }

        Match open = Regex.Match(config, @"FotGWheelConfig\s*=\s*\{");
        string header = nl + "  config_version = " + BuildInfo.ConfigVersion + "," + String.Concat(add.Select(a => nl + a).ToArray());
        return config.Insert(open.Index + open.Length, header);
    }

    public static bool SelfTest() {
        string legacy = String.Join("\n", new[] {
            "-- Generated by the Hangar Bay Console. Edit through the Macro Deck tab.",
            "FotGWheelConfig = {",
            "  flight_axes = \"classic\",",
            "  tap_bindings = { A=\"countermeasure\", B=\"none\", X=\"target_next\", Y=\"third_gun\", LB=\"cycle_secondary\", RB=\"cycle_ship_mode\", L3=\"match_speed\", R3=\"target_next_subsystem\" },",
            "  hold_enabled = true,",
            "  hold_bindings = { A=\"none\", B=\"none\", X=\"none\", LB=\"afterburner\", RB=\"none\", L3=\"none\", R3=\"none\" },",
            "  layouts = {",
            "    systems = { 2, 1, 3, 4, 5, 6, 7, 8 },",
            "    targeting = { 7, 2, 3, 4, 5, 6, 1 },",
            "    macros = { 4, 2, 3, 1 },",
            "    commands = { 1, 2, 3, 4, 5, 6 },",
            "  },",
            "  macros = {",
            "    { label = \"Shields maximum\", action = \"shields_max\", argument = \"\" },",
            "  }",
            "}", "" });
        var notes = new List<string>();
        string m = Migrate(legacy, notes);
        if (!m.Contains("config_version = " + BuildInfo.ConfigVersion + ",")) return false;
        foreach (string key in NewHoldKeys) if (!m.Contains(key + " = false,")) return false;
        if (!m.Contains("RB=\"cycle_ship_mode\"") || !m.Contains("A=\"countermeasure\"")) return false;      // saved taps kept
        if (!m.Contains("targeting = { 1, 2, 3, 4, 5, 6, 7 }") || m.Contains("7, 2, 3, 4, 5, 6, 1")) return false;
        if (!m.Contains("systems = { 2, 1, 3, 4, 5, 6, 7, 8 }") || !m.Contains("macros = { 4, 2, 3, 1 }")) return false;
        if (!m.Contains("hold_enabled = true,") || !m.Contains("LB=\"afterburner\"")) return false;           // custom holds kept
        if (notes.Count != 2) return false;
        var again = new List<string>();
        if (Migrate(m, again) != m || again.Count != 0) return false;                                          // idempotent
        // No tap table at all: the 1.1.4 defaults are written, including RB on ship mode.
        notes = new List<string>();
        string bare = Migrate("FotGWheelConfig = {\r\n  hold_enabled = false,\r\n  layouts = {\r\n    targeting = { 1, 2, 3, 4, 5, 6, 7 },\r\n  },\r\n}\r\n", notes);
        if (!bare.Contains("tap_bindings = { " + OldTapBindings + " },") || bare.Contains("\n  config_version") == false || notes.Count != 2) return false;
        if (notes.Any(n => n.StartsWith("The Targeting"))) return false;                                       // identity order: no reset note
        // Explicit values saved by a newer Console are never overridden.
        string saved = Migrate("FotGWheelConfig = {\n  r3_mouse_hold_enabled = true,\n  tap_bindings = { A=\"x\", RB=\"cycle_primary\" },\n}\n", new List<string>());
        if (!saved.Contains("r3_mouse_hold_enabled = true,") || !saved.Contains("RB=\"cycle_primary\"") || saved.Contains("r3_mouse_hold_enabled = false")) return false;
        // FotG 2 preset order: "default" goes last, once, and nothing else moves.
        string tbl = "#ControlConfigOverride\r\n$Name: default\r\n  $Bind: A\r\n#End\r\n\r\n#ControlConfigOverride\r\n$Name: FotG Classic Native\r\n  $Bind: A\r\n#End\r\n\r\n#ControlConfigOverride\r\n$Name: FotG Classic Inverted\r\n  $Bind: B\r\n#End\r\n";
        string ordered = Presets.DefaultSectionLast(tbl);
        if (ordered == tbl || Presets.DefaultSectionLast(ordered) != ordered) return false;
        if (!(ordered.IndexOf("FotG Classic Native") < ordered.IndexOf("FotG Classic Inverted") && ordered.IndexOf("FotG Classic Inverted") < ordered.IndexOf("$Name: default"))) return false;
        if (Regex.Matches(ordered, @"#ControlConfigOverride").Count != 3 || Regex.Matches(ordered, @"#End").Count != 3 || !ordered.TrimEnd().EndsWith("#End")) return false;
        if (Presets.DefaultSectionLast("#ControlConfigOverride\r\n$Name: FotG Classic Native\r\n#End\r\n") != "#ControlConfigOverride\r\n$Name: FotG Classic Native\r\n#End\r\n") return false;   // no default section: untouched
        // Versioned and unrecognised files are left alone.
        string v = "FotGWheelConfig = {\n  config_version = 1,\n}\n";
        if (Migrate(v, new List<string>()) != v || Migrate("not a config", new List<string>()) != "not a config") return false;
        return true;
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

    // FreeSpace Open treats an override section whose bindings equal an earlier one as a duplicate and renames
    // that earlier entry. In FotG 2 the "default" section is a copy of Classic Native, so when it comes first the
    // later "FotG Classic Native" section renames it away, and the pilot's saved preset "default" is "not found"
    // (plr.cpp warning at start-up). With "default" last every name survives. Unchanged when already in order.
    public static string DefaultSectionLast(string table) {
        Match last = null;
        foreach (Match m in Regex.Matches(table, @"(?ms)^#ControlConfigOverride\b.*?^#End\b"))
            if (Regex.IsMatch(m.Value, @"(?m)^\$Name:[ \t]*default\b")) last = m;
        if (last == null || last.Index + last.Length >= table.TrimEnd().Length) return table;
        string nl = table.Contains("\r\n") ? "\r\n" : "\n";
        string rest = Regex.Replace(table.Remove(last.Index, last.Length), @"(\r?\n){3,}", nl + nl).TrimEnd();
        return rest + nl + nl + last.Value + nl;
    }
    public static string FromTable(string stock, string axisMap, bool myMod = false) {
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
            if (!m.Success && myMod) continue;
            if (!m.Success) throw new InvalidDataException("Expected control binding was not found: " + c.Item1);
            string edited = Regex.Replace(m.Value, @"(?ms)(\$Secondary:\s*\r?\n)\s*\$Controller:\s*CID_JOY0\s*\r?\n\s*\$Flags:\s*NONE\s*\r?\n\s*\$Input:\s*" + c.Item2 + @"\s*\r?\n",
                "$1            $Controller: CID_NONE\r\n", RegexOptions.IgnoreCase);
            if (edited == m.Value && !myMod) throw new InvalidDataException("Expected joystick binding was not changed: " + c.Item1);
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

sealed class SdlPad { public int Id, PlayerIndex = -1; public string Guid, Name, AxisMap = Presets.Sdl3Axes; public bool Detected = true; }

// Reads the connected pads through the engine's own SDL library, as the engine sees them.
static class SdlProbe {
    [StructLayout(LayoutKind.Sequential)] struct SdlGuid { public ulong a, b; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Init")] static extern bool S3_Init(uint flags);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoysticks")] static extern IntPtr S3_GetJoysticks(out int count);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickGUIDForID")] static extern SdlGuid S3_Guid(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GUIDToString")] static extern void S3_GuidToString(SdlGuid guid, StringBuilder text, int length);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickNameForID")] static extern IntPtr S3_Name(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetJoystickPlayerIndexForID")] static extern int S3_Player(uint id);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_free")] static extern void S3_Free(IntPtr p);
    [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")] static extern void S3_Quit();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Init")] static extern int S2_Init(uint flags);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_NumJoysticks")] static extern int S2_Count();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetDeviceGUID")] static extern SdlGuid S2_Guid(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetGUIDString")] static extern void S2_GuidToString(SdlGuid guid, StringBuilder text, int length);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickNameForIndex")] static extern IntPtr S2_Name(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetDevicePlayerIndex")] static extern int S2_Player(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickOpen")] static extern IntPtr S2_Open(int index);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickNumAxes")] static extern int S2_Axes(IntPtr j);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickUpdate")] static extern void S2_Update();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickGetAxis")] static extern short S2_Axis(IntPtr j, int axis);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_JoystickClose")] static extern void S2_Close(IntPtr j);
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")] static extern void S2_Quit();
    static readonly string[] AxisNames = { "X_AXIS", "Y_AXIS", "Z_AXIS", "RX_AXIS", "RY_AXIS", "RZ_AXIS" };

    public static List<SdlPad> Sdl3(string dllDir) {
        if (!File.Exists(Path.Combine(dllDir, "SDL3.dll"))) throw new InstallException("The engine's SDL3.dll was not found.");
        SetDllDirectory(dllDir);
        var pads = new List<SdlPad>();
        if (!S3_Init(0x200)) throw new InstallException("SDL joystick initialization failed.");
        try {
            int count; IntPtr ids = S3_GetJoysticks(out count);
            try {
                for (int i = 0; i < count; i++) {
                    uint id = (uint)Marshal.ReadInt32(ids, 4 * i);
                    var text = new StringBuilder(64); S3_GuidToString(S3_Guid(id), text, text.Capacity);
                    pads.Add(new SdlPad { Id = i, Guid = text.ToString(), Name = Marshal.PtrToStringAnsi(S3_Name(id)), PlayerIndex = S3_Player(id) });
                }
            } finally { S3_Free(ids); }
        } finally { S3_Quit(); }
        return pads;
    }

    // Triggers rest at the minimum, so the other axes in order are left X, left Y, right X, right Y.
    public static List<SdlPad> Sdl2(string dllDir) {
        if (!File.Exists(Path.Combine(dllDir, "SDL2.dll"))) throw new InstallException("The engine's SDL2.dll was not found.");
        SetDllDirectory(dllDir);
        var pads = new List<SdlPad>();
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
                    pads.Add(new SdlPad { Id = i, Guid = text.ToString(), Name = name, Detected = detected, PlayerIndex = S2_Player(i),
                        AxisMap = detected ? String.Join(",", sticks.Select(a => AxisNames[a])) : Presets.Sdl3Axes });
                } finally { S2_Close(j); }
            }
        } finally { S2_Quit(); }
        return pads;
    }
}
