# Download safety and the 1.1.1 Defender alert

On 7 November 2026, Microsoft Defender identified `Setup.exe` in the public
Flight Control 1.1.1 ZIP as `Trojan:Win32/Wacatac.B!ml` and quarantined a
download on a Windows PC. This is a malware alert, not the ordinary SmartScreen
warning for an unknown publisher. Do not disable Defender or add an exclusion
to install an alerted build.

The affected player ZIP has SHA-256
`5309b155f0d286d1be6d55e2d1faa65ad89cd8d6cd9516a5f84a11e39877b832`.
Its `Setup.exe` has SHA-256
`d7e4031a3742834f1a7c37072362e0ec462ecadf63eb83338c54cc5aff65fbb5`.

Flight Control 1.1.2 replaces the Setup code path that invoked a hidden
PowerShell installer. Installation now runs in the compiled, published C#
source. The updater verifies ZIP size and SHA-256 and shows a new Setup in File
Explorer for the player to run; it does not start the downloaded program.
The 1.1.2 candidate passed local Microsoft Defender scans of the player ZIP
and each executable. Local scanning cannot establish that another device or
download service will accept it. Report any new alert with the exact product,
detection name, version, and file hash to dev@manictrashpanda.dev.

The Windows executables are currently unsigned. A SmartScreen unknown-publisher
warning is separate from a Defender malware detection. Signing future releases
with a trusted publisher certificate may improve publisher identification and
reputation, but signing does not prove that a program is safe.
