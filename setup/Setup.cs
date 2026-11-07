using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

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
        page.Controls.Add(TextLabel("By Manic Trash Panda · Coded with ChatGPT · Independent add-on", 10, Ink), 0, 1);

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
        play.Click += delegate { OpenInstalled("Play with Gamepad Wheel.exe"); };
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
    // Game releases this build supports, newest first. Each installs to kn_portable\Library\FotG_R\FotG_R-<version>
    // and runs on the same FSO 26.0.1 SDL3 engine. Keep this list in step with Install.ps1, BuildFlightPreset.ps1
    // and WheelBridge.cs, and add a release only after BuildFlightPreset.ps1 accepts its control defaults.
    static readonly string[] SupportedVersions = { "1.1.2", "1.1.1", "1.1.0" };
    static string SupportedText() { return String.Join(", ", SupportedVersions.Reverse()); }
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
    static string GameManifest(string root, string version) { return Path.Combine(GameLibrary(root), "FotG_R-" + version, "mod.json"); }
    static string GameEngine(string root) { return Path.Combine(root, "kn_portable", "Library", "bin", "FSO_2601SDL3-1.0.0", "windows-x64", "fs2_open_26_0_1_sdl3_x64_SSE2.exe"); }
    // The newest supported release present, or null.
    static string GameVersion(string root) {
        foreach (string version in SupportedVersions) if (File.Exists(GameManifest(root, version))) return version;
        return null;
    }
    static bool LooksLikeGame(string root) { return Directory.Exists(root) && GameVersion(root) != null && File.Exists(GameEngine(root)); }
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
        checkedVersion = null;
        if (version == null) {
            string newest = NewestInstalledVersion(root);
            ShowOverride(newest != null);
            if (newest != null && overrideCheck.Checked && String.Equals(overrideRoot, root, StringComparison.OrdinalIgnoreCase)) { version = newest; usingOverride = true; }
            else {
                SetOverride(false);
                string[] found = InstalledVersions(root);
                verdict.Text = "UNSUPPORTED GAME VERSION";
                details.Text = (found.Length > 0 ? "Installed: Fate of the Galaxy " + String.Join(", ", found) + ".\r\n" : "No Fate of the Galaxy release was found in kn_portable\\Library\\FotG_R.\r\n")
                    + "This build supports Fate of the Galaxy " + SupportedText() + ". Check for a newer Flight Control release at manictrashpanda.dev/games/FotGFlightControl."
                    + (newest != null ? "\r\n\r\nIf you know " + newest + " works, tick OVERRIDE VERSION CHECK to install anyway. Compatibility cannot be guaranteed while the override is in use, until Flight Control is updated for this version." : "");
                return;
            }
        } else {
            ShowOverride(false);
            SetOverride(false);
        }
        checkedVersion = version;
        if (!File.Exists(GameEngine(root))) { verdict.Text = "SDL3 ENGINE NOT FOUND"; details.Text = "Fate of the Galaxy " + version + " was found, but not its FSO 26.0.1 SDL3 engine. Launch the game once from Knossos so it installs the engine, then click Check again."; return; }
        try {
            var manifest = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(GameManifest(root, version)));
            if (!manifest.ContainsKey("version") || Convert.ToString(manifest["version"]) != version) { verdict.Text = "UNSUPPORTED GAME VERSION"; details.Text = "The FotG_R-" + version + " folder's manifest reports a different version. Repair the game in Knossos, then click Check again."; return; }
        } catch { verdict.Text = "GAME MANIFEST COULD NOT BE READ"; details.Text = "The game installation may be incomplete."; return; }
        validatedRoot = root;
        string prefs = Path.Combine(root, "kn_portable", "HardLightProductions", "FreeSpaceOpen", "data");
        bool ini = File.Exists(Path.Combine(prefs, "FotG_R_settings.ini"));
        string pilots = Path.Combine(prefs, "players");
        bool pilot = false;
        if (Directory.Exists(pilots)) foreach (string file in Directory.GetFiles(pilots, "*.json")) {
            try { var data = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(file));
                if (data.ContainsKey("version") && Convert.ToInt32(data["version"]) == 4) { pilot = true; break; } }
            catch { }
        }
        bool installed = File.Exists(Path.Combine(root, "GamepadMods", "FotG_Gamepad_Wheel", "WheelBridge.exe"));
        play.Enabled = File.Exists(Path.Combine(root, "Play with Gamepad Wheel.exe"));
        configure.Enabled = File.Exists(Path.Combine(root, "Configure Gamepad Wheel.exe"));
        if (!ini || !pilot) {
            verdict.Text = "FIRST RUN REQUIRED";
            details.Text = "1. Open the game normally and create a pilot.\r\n2. Close the game.\r\n3. Return here and click Check again.\r\n\r\nThe installer needs the game's own pilot and settings files before it can back up and reserve controls.";
            return;
        }
        bool running = Process.GetProcesses().Any(p => p.ProcessName.StartsWith("fs2_open", StringComparison.OrdinalIgnoreCase));
        if (running) { verdict.Text = "CLOSE THE GAME BEFORE INSTALLING"; details.Text = "The game is running. Close it, then click Check again."; return; }
        install.Enabled = true;
        verdict.Text = (installed ? "READY TO REPAIR OR UPDATE" : "READY TO INSTALL") + (usingOverride ? " · OVERRIDDEN" : "");
        details.Text = (usingOverride
                ? "WARNING: VERSION CHECK OVERRIDDEN. Fate of the Galaxy " + version + " is not on this build's supported list (" + SupportedText() + "). Compatibility cannot be guaranteed while the override is in use, until Flight Control is updated for this version.\r\n\r\n"
                : "")
            + "Game: " + root + "\r\nPilot and settings: found\r\nVersion: Fate of the Galaxy " + version + " / FSO 26.0.1 SDL3\r\n\r\nInstall backs up native pilot controls and any earlier add-on files. It then creates Play and Configure launchers in the game folder.";
    }
    void Install() {
        Check();
        if (!install.Enabled || validatedRoot == null) return;
        string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Install.ps1");
        if (!File.Exists(script)) { MessageBox.Show("Install.ps1 is missing. Extract the complete ZIP before running Setup.exe.", Text); return; }
        string root = validatedRoot;
        string versionArgs = usingOverride ? " -GameVersion " + checkedVersion + " -OverrideVersionCheck" : "";
        install.Enabled = false;
        verdict.Text = "INSTALLING…";
        details.Text = "Backing up pilot settings and copying the add-on. Please wait.";
        System.Threading.ThreadPool.QueueUserWorkItem(delegate {
            string output; int exitCode;
            try {
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                var info = new ProcessStartInfo(powershell, "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -GameRoot \"" + root + "\"" + versionArgs);
                info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardOutput = true; info.RedirectStandardError = true;
                using (var process = Process.Start(info)) {
                    output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    process.WaitForExit(); exitCode = process.ExitCode;
                }
            } catch (Exception error) { output = error.Message; exitCode = -1; }
            BeginInvoke((Action)delegate {
                Check();
                verdict.Text = exitCode == 0 ? "INSTALLATION COMPLETE" : "INSTALLATION FAILED";
                details.Text = exitCode == 0
                    ? "Gamepad wheel installed in:\r\n" + root + "\r\n\r\nClick PLAY WITH WHEEL to start the game, or CONFIGURE to adjust flight response and macros.\r\n\r\nYour existing pilot and any previous add-on files were backed up."
                    : (output.Length == 0 ? "No details were returned. Check the game folder and try again." : output.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            });
        });
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
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new WheelSetup());
    }
}
