# Public release preparation

The repository contains the original source, build scripts, documentation,
license, notices, and UI screenshots. `dist/` is generated and excluded from Git.

Build on Windows x64 with .NET Framework 4.x and Windows PowerShell:

```powershell
.\Package.ps1
```

The script compiles Setup, the controller console and the bridge, runs the
available self-tests, creates a versioned ZIP under `dist/`, and writes a SHA-256
manifest inside it. Keep the reported ZIP SHA-256 with the release notes.

For a public release, tag the matching source commit. Upload the generated ZIP
as a release asset, along with its checksum. Put the same ZIP on
`manictrashpanda.dev/games/FotGFlightControl` and show the same version and SHA-256
there. Do not use the repository's automatic source ZIP as the player download:
it does not contain the compiled Setup, bridge or console.

Run the real-controller checklist in TESTING.md before each release and say
which campaigns and missions it covered. Do not commit game files, pilot data, backups, logs,
downloaded engine binaries or experimental audio files.

Package.ps1 also writes `dist/update.json`. Publish the ZIP first, check its
download, then publish `update.json` to `/games/FotGFlightControl/update.json`;
Setup and the console offer updates from it. To approve a newer game version for
an existing build without shipping a new one, add it under
`verifiedGameVersions` for that build (for example `"1.1.0": ["1.1.3"]`) after
checking it with BuildFlightPreset.ps1. Keep `setup/BuildInfo.cs` and
`$version` in Package.ps1 equal; packaging refuses otherwise.

The player ZIP has graphical Setup and two game-folder EXE launchers. The
PowerShell script remains in the package for Setup and advanced manual repair;
players do not need to open a command window or use a batch launcher.
