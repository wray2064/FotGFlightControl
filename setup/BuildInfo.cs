// The release version of this build. Package.ps1 refuses to package if it differs from its $version.
static class BuildInfo {
    // Layout of the saved wheel config. Settings saved by Flight Control 1.1.4 and earlier have no
    // config_version; Setup migrates them once (see ConfigMigration in Installer.cs).
    public const int ConfigVersion = 1;
    public const string Version = "2.0.0-preview.1";
    public const string ProductPage = "https://manictrashpanda.dev/games/FotGFlightControl/";
    public const string UpdateManifest = "https://manictrashpanda.dev/games/FotGFlightControl/update.json";
}
