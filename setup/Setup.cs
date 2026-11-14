using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Flight Control Setup")]
[assembly: System.Reflection.AssemblyDescription("Installs and repairs Flight Control, a gamepad add-on for Fate of the Galaxy.")]

internal sealed class WheelSetup : Form {
    static readonly Color Background = Color.Black;
    static readonly Color PanelColor = Color.FromArgb(13, 15, 11);
    static readonly Color Ink = Color.FromArgb(247, 220, 82);
    static readonly Color Cyan = Color.FromArgb(249, 223, 54);
    readonly TextBox gamePath = new TextBox();
    readonly TextBox details = new TextBox();
    readonly Label verdict = new Label();
    readonly Button install = new Button();
    readonly Button play = new Button();
    readonly Button configure = new Button();
    readonly CheckBox overrideCheck = new CheckBox();
    TableLayoutPanel status;
    string validatedRoot;
    string checkedVersion;      // the release Install will target
    bool usingOverride;         // that release is not on SupportedVersions
    bool suppressOverrideEvent;
    string overrideRoot;        // the folder the override was confirmed for
    bool verifiedOnline;        // the release is not built in, but update.json lists it as checked
    readonly Button updateButton = new Button();
    readonly CheckBox checkUpdates = new CheckBox();
    UpdateInfo pendingUpdate;

    WheelSetup() {
        Text = "FotG Gamepad Wheel Setup";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(790, 520);
        MinimumSize = new Size(720, 490);
        Font = new Font("Consolas", 10);
        BackColor = Background;
        ForeColor = Ink;
        Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "FlightControl.ico"));

        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(24) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(page);

        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var emblem = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        emblem.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "FlightControl.png"));
        brand.Controls.Add(emblem, 0, 0);
        brand.Controls.Add(TextLabel("FLIGHT CONTROL  /  GAMEPAD WHEEL", 18, Cyan), 1, 0);
        page.Controls.Add(brand, 0, 0);
        // Byline, with the update button (shown when a newer release exists) and the update setting.
        var byline = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0) };
        byline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        byline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        byline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        byline.Controls.Add(TextLabel("By Manic Trash Panda · Coded with ChatGPT · v" + BuildInfo.Version, 9, Ink), 0, 0);
        StyleButton(updateButton, true);
        updateButton.Font = new Font("Consolas", 9);
        updateButton.Visible = false;
        updateButton.Click += delegate { InstallUpdate(); };
        byline.Controls.Add(updateButton, 1, 0);
        checkUpdates.Text = "Check for updates";
        checkUpdates.Checked = Updater.CheckEnabled;
        checkUpdates.Dock = DockStyle.Fill;
        checkUpdates.ForeColor = Ink;
        checkUpdates.FlatStyle = FlatStyle.Flat;
        checkUpdates.Padding = new Padding(14, 0, 0, 0);
        checkUpdates.Font = new Font("Consolas", 9);
        checkUpdates.CheckedChanged += delegate { Updater.CheckEnabled = checkUpdates.Checked; if (checkUpdates.Checked) StartUpdateCheck(); else { updateButton.Visible = false; } };
        byline.Controls.Add(checkUpdates, 2, 0);
        page.Controls.Add(byline, 0, 1);

        var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        gamePath.Dock = DockStyle.Fill;
        gamePath.BackColor = PanelColor;
        gamePath.ForeColor = Ink;
        gamePath.TextChanged += delegate { Check(); };
        picker.Controls.Add(gamePath, 0, 0);
        picker.Controls.Add(MakeButton("Browse game…", Browse), 1, 0);
        page.Controls.Add(picker, 0, 2);

        verdict.Dock = DockStyle.Fill;
        verdict.BackColor = PanelColor;
        verdict.ForeColor = Cyan;
        verdict.TextAlign = ContentAlignment.MiddleLeft;
        verdict.Padding = new Padding(12, 0, 12, 0);
        // Shown only when the installed release is not on the supported list.
        overrideCheck.Text = "OVERRIDE VERSION CHECK";
        overrideCheck.Dock = DockStyle.Fill;
        overrideCheck.BackColor = PanelColor;
        overrideCheck.ForeColor = Ink;
        overrideCheck.FlatStyle = FlatStyle.Flat;
        overrideCheck.Padding = new Padding(10, 0, 6, 0);
        overrideCheck.Cursor = Cursors.Hand;
        overrideCheck.Visible = false;
        overrideCheck.CheckedChanged += delegate { OverrideChanged(); };
        status = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        status.Controls.Add(verdict, 0, 0);
        status.Controls.Add(overrideCheck, 1, 0);
        page.Controls.Add(status, 0, 3);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        install.Text = "INSTALL / REPAIR";
        StyleButton(install, true);
        install.Click += delegate { Install(); };
        play.Text = "PLAY WITH WHEEL";
        StyleButton(play, false);
        play.Click += delegate { OpenInstalled(checkedVersion=="2.0.2"?"Hangar Bay.exe":"Play with Gamepad Wheel.exe"); };
        configure.Text = "CONFIGURE";
        StyleButton(configure, false);
        configure.Click += delegate { OpenInstalled("Configure Gamepad Wheel.exe"); };
        actions.Controls.Add(install, 0, 0);
        actions.Controls.Add(play, 1, 0);
        actions.Controls.Add(configure, 2, 0);
        page.Controls.Add(actions, 0, 4);

        details.Dock = DockStyle.Fill;
        details.Multiline = true;
        details.ScrollBars = ScrollBars.Vertical;
        details.ReadOnly = true;
        details.BackColor = PanelColor;
        details.ForeColor = Ink;
        details.BorderStyle = BorderStyle.None;
        details.Font = new Font("Consolas", 10);
        page.Controls.Add(details, 0, 5);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        for (int i = 0; i < 5; i++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        footer.Controls.Add(MakeButton("Check again", Check), 0, 0);
        footer.Controls.Add(MakeButton("Open game folder", OpenGameFolder), 1, 0);
        footer.Controls.Add(MakeButton("Help", delegate { OpenDocument("Help.html"); }), 2, 0);
        footer.Controls.Add(MakeButton("About & credits", delegate { OpenDocument("About.html"); }), 3, 0);
        footer.Controls.Add(MakeButton("Close", Close), 4, 0);
        page.Controls.Add(footer, 0, 6);

        gamePath.Text = FindGame();
        Check();
        if (Updater.CheckEnabled) StartUpdateCheck();
    }

    // Background check against update.json; quiet when offline.
    void StartUpdateCheck() {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            UpdateInfo info = null;
            try { info = Updater.Check(6000); } catch { return; }
            try {
                BeginInvoke((Action)delegate {
                    pendingUpdate = info;
                    updateButton.Text = info != null ? "UPDATE TO " + info.Version : "";
                    updateButton.Visible = info != null && checkUpdates.Checked;
                    Check();
                });
            } catch (InvalidOperationException) { }
        });
    }
    void InstallUpdate() {
        var info = pendingUpdate;
        if (info == null) return;
        var answer = MessageBox.Show(this,
            "Flight Control " + info.Version + " is available (you have " + BuildInfo.Version + ").\r\n\r\n" +
            "Setup will download it from " + (new Uri(info.Url).IsFile ? "a local test file" : new Uri(info.Url).Host) + ", check it against its published SHA-256, and show the new Setup.exe in File Explorer. Run it and choose Install / Repair; your settings are kept and your pilot is backed up.\r\n\r\n" +
            "Windows may warn that the new Setup is from an unknown publisher; it is not code-signed yet.\r\n\r\n" +
            "Release notes: " + info.Notes + "\r\n\r\nDownload and open it now?",
            "Update Flight Control", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (answer != DialogResult.Yes) return;
        updateButton.Enabled = false;
        install.Enabled = false;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            string setup = null; string error = null;
            try { setup = Updater.DownloadAndExtract(info, delegate(string step) { BeginInvoke((Action)delegate { verdict.Text = step.ToUpperInvariant(); }); }); }
            catch (Exception e) { error = e.Message; }
            BeginInvoke((Action)delegate {
                if (error != null) { updateButton.Enabled = true; Check(); MessageBox.Show(this, "The update was not installed.\r\n\r\n" + error, "Update Flight Control"); return; }
                // Hand the verified Setup to the player rather than starting it from here.
                Updater.ShowInExplorer(setup);
                updateButton.Visible = false;
                Check();
                verdict.Text = "UPDATE " + info.Version + " READY";
                details.Text = "Flight Control " + info.Version + " was downloaded and matches its published SHA-256.\r\n\r\nFile Explorer now shows its Setup.exe:\r\n" + setup + "\r\n\r\nClose this window, run that Setup.exe and choose Install / Repair.";
            });
        });
    }

    static Label TextLabel(string value, float size, Color color) {
        return new Label { Text = value, Font = new Font("Consolas", size), ForeColor = color,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    }
    static void StyleButton(Button button, bool prominent) {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(4);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = prominent ? Cyan : Color.FromArgb(112, 91, 24);
        button.BackColor = prominent ? Color.FromArgb(84, 66, 5) : Color.FromArgb(24, 21, 7);
        button.ForeColor = Ink;
        button.Cursor = Cursors.Hand;
    }
    Button MakeButton(string label, Action action) {
        var button = new Button { Text = label };
        StyleButton(button, false);
        button.Click += delegate { try { action(); } catch (Exception e) { MessageBox.Show(e.Message, Text); } };
        return button;
    }
    // Game releases this build supports (Installer.cs holds the list; keep WheelBridge.cs in step).
    // Add a release only after the preset builder in Installer.cs accepts its control defaults.
    static readonly string[] SupportedVersions = Installer.SupportedVersions;
    static string SupportedText() { return String.Join(", ", SupportedVersions.Reverse()) + ", 2.0.2"; }
    // The newest installed release with a manifest, supported or not, for the version override.
    static string NewestInstalledVersion(string root) {
        Version best = null; string bestText = null;
        foreach (string v in InstalledVersions(root)) {
            Version parsed;
            if (!Version.TryParse(v, out parsed) || !File.Exists(GameManifest(root, v))) continue;
            if (best == null || parsed > best) { best = parsed; bestText = v; }
        }
        return bestText;
    }
    // The checkbox column only takes space while the checkbox is offered.
    void ShowOverride(bool visible) {
        overrideCheck.Visible = visible;
        if (status != null) status.ColumnStyles[1].Width = visible ? 250 : 0;
    }
    static string FullPathOrNull(string path) { try { return Path.GetFullPath(path); } catch { return null; } }
    void SetOverride(bool value) { suppressOverrideEvent = true; overrideCheck.Checked = value; suppressOverrideEvent = false; }
    void OverrideChanged() {
        if (suppressOverrideEvent) return;
        if (overrideCheck.Checked) {
            string root = gamePath.Text.Trim().Trim('"');
            string newest = NewestInstalledVersion(root);
            var answer = MessageBox.Show(this,
                "Fate of the Galaxy " + newest + " is not one of the versions this Flight Control build supports (" + SupportedText() + ").\r\n\r\n" +
                "Overriding the version check installs Flight Control for " + newest + " anyway. Compatibility cannot be guaranteed while the override is in use, until Flight Control is updated to support this version.\r\n\r\n" +
                "Setup still checks the game's control defaults and stops if they changed in a way it doesn't recognise, and your pilot is backed up first.\r\n\r\n" +
                "Override the version check?",
                "Override version check", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) { SetOverride(false); return; }
            overrideRoot = FullPathOrNull(root);
        }
        Check();
    }
    static string GameLibrary(string root) { return Path.Combine(root, "kn_portable", "Library", "FotG_R"); }
    static string GameManifest(string root, string version) {
        return version=="2.0.2" ? Path.Combine(root, @"kn_portable\Library\FotG_2\FotG_2-2.0.2\mod.json") : Path.Combine(GameLibrary(root), "FotG_R-" + version, "mod.json");
    }
    // The newest supported release present, or null.
    static string GameVersion(string root) {
        if (GameInstall.HasMyMod(root) && File.Exists(GameManifest(root,"2.0.2"))) return "2.0.2";
        foreach (string version in SupportedVersions) if (File.Exists(GameManifest(root, version))) return version;
        return null;
    }
    static bool LooksLikeGame(string root) { string v = Directory.Exists(root) ? GameVersion(root) : null; return v != null && GameInstall.FindEngine(root, v) != null; }
    // Every installed FotG_R release folder, to tell the player which version they have.
    static string[] InstalledVersions(string root) {
        try { return Directory.GetDirectories(GameLibrary(root), "FotG_R-*").Select(d => Path.GetFileName(d).Substring("FotG_R-".Length)).OrderBy(v => v).ToArray(); }
        catch { return new string[0]; }
    }
    static string FindGame() {
        string here = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string parent = Path.GetDirectoryName(here);
        string[] direct = { here, parent, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Fate of the Galaxy 1.1.0"), @"C:\Games\Fate of the Galaxy 1.1.0", @"C:\Games\FotG" };
        foreach (string path in direct) if (!String.IsNullOrEmpty(path) && LooksLikeGame(path)) return path;
        foreach (string basePath in new[] { parent, @"C:\Games" }) {
            if (String.IsNullOrEmpty(basePath) || !Directory.Exists(basePath)) continue;
            try { foreach (string path in Directory.GetDirectories(basePath, "*Fate*", SearchOption.TopDirectoryOnly)) if (LooksLikeGame(path)) return path; }
            catch (UnauthorizedAccessException) { }
        }
        return "";
    }
    void Browse() { using (var dialog = new FolderBrowserDialog { Description = "Choose the Fate of the Galaxy folder containing kn_portable" }) {
        if (dialog.ShowDialog(this) == DialogResult.OK) gamePath.Text = dialog.SelectedPath;
    } }
    void Check() {
        validatedRoot = null;
        ShowOverride(false);
        install.Enabled = false;
        play.Enabled = false;
        configure.Enabled = false;
        string root = gamePath.Text.Trim().Trim('"');
        if (root.Length == 0) { verdict.Text = "CHOOSE THE FATE OF THE GALAXY GAME FOLDER"; details.Text = "Select the folder that contains kn_portable. The game must be installed separately."; return; }
        try { root = Path.GetFullPath(root); } catch { verdict.Text = "INVALID FOLDER PATH"; details.Text = "Choose the game folder with Browse game."; return; }
        // Report the actual problem: wrong folder, unsupported release, or missing engine.
        if (!Directory.Exists(Path.Combine(root, "kn_portable"))) { verdict.Text = "GAME FOLDER NOT FOUND"; details.Text = "This folder has no kn_portable folder. Choose the Fate of the Galaxy folder that contains kn_portable."; return; }
        string version = GameVersion(root);
        usingOverride = false;
        verifiedOnline = false;
        checkedVersion = null;
        if (version == null) {
            string newest = NewestInstalledVersion(root);
            ShowOverride(newest != null);
            verifiedOnline = newest != null && Updater.LastVerifiedGameVersions.Contains(newest);
            if (verifiedOnline) { version = newest; usingOverride = true; ShowOverride(false); }
            else if (newest != null && overrideCheck.Checked && String.Equals(overrideRoot, root, StringComparison.OrdinalIgnoreCase)) { version = newest; usingOverride = true; }
            else {
                SetOverride(false);
                string[] found = InstalledVersions(root);
                verdict.Text = "UNSUPPORTED GAME VERSION";
                details.Text = (found.Length > 0 ? "Installed: Fate of the Galaxy " + String.Join(", ", found) + ".\r\n" : "No Fate of the Galaxy release was found in kn_portable\\Library\\FotG_R.\r\n")
                    + "This build supports Fate of the Galaxy " + SupportedText() + "." + (pendingUpdate != null ? " Flight Control " + pendingUpdate.Version + " is available: click UPDATE above." : " Check for a newer Flight Control release at manictrashpanda.dev/games/FotGFlightControl.")
                    + (newest != null ? "\r\n\r\nIf you know " + newest + " works, tick OVERRIDE VERSION CHECK to install anyway. Compatibility cannot be guaranteed while the override is in use, until Flight Control is updated for this version." : "");
                return;
            }
        } else {
            ShowOverride(false);
            SetOverride(false);
        }
        checkedVersion = version;
        EngineInfo engine = GameInstall.FindEngine(root, version);
        if (engine == null) { verdict.Text = "GAME ENGINE NOT FOUND"; details.Text = "Fate of the Galaxy " + version + " was found, but not its FSO engine in kn_portable\\Library\\bin. Launch the game once from Knossos (or use Modify > Verify there) so it installs the engine, then click Check again."; return; }
        try {
            var manifest = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(GameManifest(root, version)));
            if (!manifest.ContainsKey("version") || Convert.ToString(manifest["version"]) != version) { verdict.Text = "UNSUPPORTED GAME VERSION"; details.Text = "The FotG_R-" + version + " folder's manifest reports a different version. Repair the game in Knossos, then click Check again."; return; }
        } catch { verdict.Text = "GAME MANIFEST COULD NOT BE READ"; details.Text = "The game installation may be incomplete."; return; }
        validatedRoot = root;
        string prefs = Path.Combine(root, "kn_portable", "HardLightProductions", "FreeSpaceOpen", "data");
        bool ini = File.Exists(Path.Combine(prefs, version=="2.0.2"?"FotG_2_settings.ini":"FotG_R_settings.ini"));
        string pilots = Path.Combine(prefs, "players");
        bool pilot = false;
        if (Directory.Exists(pilots)) foreach (string file in Directory.GetFiles(pilots, "*.json")) {
            try { var data = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(file));
                if (data.ContainsKey("version") && Convert.ToInt32(data["version"]) == 4) { pilot = true; break; } }
            catch { }
        }
        bool installed = version=="2.0.2"?File.Exists(Path.Combine(root,"Hangar Bay.exe")):File.Exists(Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "WheelBridge.exe"));
        play.Enabled = File.Exists(Path.Combine(root, version=="2.0.2"?"Hangar Bay.exe":"Play with Gamepad Wheel.exe"));
        configure.Enabled = File.Exists(Path.Combine(root, "Configure Gamepad Wheel.exe"));
        if (!ini || !pilot) {
            verdict.Text = "FIRST RUN REQUIRED";
            details.Text = "1. Open the game normally and create a pilot.\r\n2. Close the game.\r\n3. Return here and click Check again.\r\n\r\nThe installer needs the game's own pilot and settings files before it can back up and reserve controls.";
            return;
        }
        bool running = Process.GetProcesses().Any(p => p.ProcessName.StartsWith("fs2_open", StringComparison.OrdinalIgnoreCase));
        if (running) { verdict.Text = "CLOSE THE GAME BEFORE INSTALLING"; details.Text = "The game is running. Close it, then click Check again."; return; }
        install.Enabled = true;
        verdict.Text = (installed ? "READY TO REPAIR OR UPDATE" : "READY TO INSTALL") + (verifiedOnline ? " · VERIFIED ONLINE" : usingOverride ? " · OVERRIDDEN" : "");
        details.Text = (verifiedOnline
                ? "Fate of the Galaxy " + version + " is newer than this build's built-in list (" + SupportedText() + "), and has been checked with Flight Control " + BuildInfo.Version + " (listed online at manictrashpanda.dev).\r\n\r\n"
                : usingOverride
                ? "WARNING: VERSION CHECK OVERRIDDEN. Fate of the Galaxy " + version + " is not on this build's supported list (" + SupportedText() + "). Compatibility cannot be guaranteed while the override is in use, until Flight Control is updated for this version.\r\n\r\n"
                : "")
            + "Game: " + root + "\r\nPilot and settings: found\r\nVersion: Fate of the Galaxy " + version + "\r\nEngine: " + engine.Label + "\r\n\r\nInstall backs up native pilot controls and any earlier add-on files. It then creates Play and Configure launchers in the game folder.";
    }
    void Install() {
        Check();
        if (!install.Enabled || validatedRoot == null) return;
        var options = new InstallOptions { GameRoot = validatedRoot, PackageRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            GameVersion = usingOverride ? checkedVersion : null, OverrideVersionCheck = usingOverride };
        string root = validatedRoot;
        install.Enabled = false;
        verdict.Text = "INSTALLING…";
        details.Text = "Backing up pilot settings and copying the add-on. Please wait.";
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            var output = new System.Text.StringBuilder(); bool ok;
            try {
                Installer.Run(options, line => output.AppendLine(line), line => output.AppendLine("WARNING: " + line));
                ok = true;
            } catch (Exception error) { output.AppendLine(error.Message); ok = false; }
            BeginInvoke((Action)delegate {
                Check();
                verdict.Text = ok ? "INSTALLATION COMPLETE" : "INSTALLATION FAILED";
                details.Text = ok
                    ? "Gamepad wheel installed in:\r\n" + root + "\r\n\r\nClick PLAY WITH WHEEL to start the game, or CONFIGURE to adjust flight response and macros.\r\n\r\nYour existing pilot and any previous add-on files were backed up."
                    : output.ToString().Trim();
            });
        });
    }
    // Setup.exe --install --game <folder> [--game-version <v>] [--override-version-check] [--skip-pilot-reservation]
    // installs without the window; output goes to standard output, exit code 0 on success.
    static int InstallCli(string[] args) {
        Func<string, string> value = name => { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; };
        string game = value("--game");
        if (game == null) { Console.Error.WriteLine("Usage: Setup.exe --install --game <folder> [--game-version <version>] [--override-version-check] [--skip-pilot-reservation]"); return 2; }
        try {
            Installer.Run(new InstallOptions { GameRoot = game.Trim('"'), GameVersion = value("--game-version"), PackageRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                OverrideVersionCheck = args.Contains("--override-version-check"), SkipPilotReservation = args.Contains("--skip-pilot-reservation") },
                line => Console.Out.WriteLine(line), line => Console.Out.WriteLine("WARNING: " + line));
            Console.Out.Flush(); return 0;
        } catch (Exception e) { Console.Out.WriteLine(e.Message); Console.Out.Flush(); return 1; }
    }
    void OpenInstalled(string name) {
        if (validatedRoot == null) throw new InvalidOperationException("Choose the game folder first.");
        string path = Path.Combine(validatedRoot, name);
        if (!File.Exists(path)) throw new FileNotFoundException("Install the wheel first.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    void OpenGameFolder() { if (validatedRoot == null) { MessageBox.Show("Choose a complete game installation first.", Text); return; }
        Process.Start(new ProcessStartInfo(validatedRoot) { UseShellExecute = true }); }
    void OpenDocument(string name) { string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        if (!File.Exists(path)) throw new FileNotFoundException(name + " is missing. Extract the complete ZIP.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
    [STAThread] static void Main(string[] args) {
        if (args.Length > 0 && args[0] == "--self-test") { Environment.Exit(Updater.SelfTest() && GameInstall.SelfTest() && ConfigMigration.SelfTest() ? 0 : 1); }
        if (args.Length > 0 && args[0] == "--install") { Environment.Exit(InstallCli(args)); }
        // Setup.exe --build-presets --game <folder> --game-version <v> --out <file> [--axis-map <map>]
        // writes the Classic presets for a game release, to check a new release before supporting it.
        if (args.Length > 0 && args[0] == "--build-presets") {
            Func<string, string> value = name => { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; };
            try {
                File.WriteAllText(value("--out"), Presets.Build(value("--game"), value("--game-version"), value("--axis-map") ?? Presets.Sdl3Axes), new System.Text.UTF8Encoding(false));
                Environment.Exit(0);
            } catch (Exception e) { Console.Out.WriteLine(e.Message); Console.Out.Flush(); Environment.Exit(1); }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new WheelSetup());
    }
}
