# Campaign utilities handoff — 1.1.4 candidate

## Branch and scope

Work continues on feature/campaign-utilities, based on the HOTAS investigation
branch at its unchanged 1.1.3 Hangar Bay baseline (a8d9892). No HOTAS backend
was implemented. Keep feature/hotas-support and vr-prep intact. This candidate
is preparation for an update; it has not been merged, pushed, or published.

## Changes ready for campaign testing

- In-mission popup cursor: dialog hooks publish MENU, keep the cursor visible,
  suspend wheel input/drawing, and restore the prior input classification.
  The bridge waits for held buttons/triggers to release on entry and exit.
- Default hyperspace shortcut: LB + RB within 120 ms; hold together for 300 ms
  to send Return once. Both shoulders must release to rearm. Their individual
  mappings remain with a 120 ms decision delay. Steering/fire stay available.
  Actual jump eligibility remains the game's decision. Return must retain its
  normal Engage Hyperdrive binding. See HYPERSPACE-SHORTCUT.md.
- Swappable right wheel: D-pad Right opens; Y swaps Macros/Mission; X switches
  Mission pages; right stick chooses, A sends, B cancels. Macros remains the
  initial default. Mission offers 1–8 on page one; 9, 0, F4 mission log, F11
  fleet info, Space fleet page on page two. It sends actual keys, not orders.
- Selected wheel is saved separately in data/players/fotg-right-wheel.txt.
  Existing macro JSON, custom actions, and layout permutations are unchanged.
- Whitelisted mission requests use sequence-based deduplication. Requests are
  discarded if unfocused/disconnected or blocked by a dialog/modal state.

## Campaign findings and limits

Some missions dock with Return; others explicitly require 1. Number responses
are therefore essential in addition to hyperspace. See CAMPAIGN-BINDING-AUDIT.md.
Active Pause (P), GCW planning text entry/Ctrl+Enter and specialized controls
remain uncovered by the new wheel. No full campaign playthrough is claimed.

SteamVR remains a separate upstream graphics-session problem: the launcher
passed -vr correctly; SteamVR found the Quest 3S, then failed internal OpenGL
context creation and xrCreateSession. Do not spend launcher time on a blind
flag change. Meta Link stereo/head tracking worked per previous testing/user
report. See STEAMVR-TRIAGE.md. Raw SteamVR logs are local-only and excluded
from source commits and candidate player packaging.

## Validation and local installation

Bridge build/self-tests and actual-Lua dialog/Mission regression tests passed.
The bridge tests cover the hyperspace gesture timing/cancellation/rearming and
mission request key whitelist/deduplication. Lua checks cover dialog lifecycle,
Mission page contents, Y/X controls, persistence and macro/layout preservation.
The local game at C:\Games\Fate of the Galaxy 1.1.0 has the functionality
installed, with timestamped backups in updated/cursor-fix-backups,
updated/hyperspace-shortcut-backups and updated/mission-wheel-backups. Those
installed test files preceded the candidate version bump; use candidate Setup
for a version-consistent install.

## Update preparation and release gate

BuildInfo.cs and Package.ps1 target 1.1.4. Package.ps1 produces the candidate ZIP,
SHA256SUMS.txt and draft update.json under dist. These are preparation artifacts;
do not publish the manifest until the tested ZIP is available at its final URL.
Existing 1.1.3 handoff/site files remain historical; use this handoff and the
1.1.4 release notes for the next update. The website has not been changed.

Before release, verify in an actual campaign:
1. Pause/exit dialog cursor, No/Quit/Restart, focus/disconnect cleanup.
2. Shoulder jump under fire, no extra ship-mode/weapon cycle, normal singles.
3. Dock/response number choices, log/fleet access, both Mission pages, swap and
   saved mode after restart, unchanged custom macros and layout profiles.
4. Repeat relevant cases in standard flight and working Meta OpenXR VR.
5. Install/Repair, profile backups/restore and uninstall with the candidate.
6. Review final help/page copy, ZIP contents/hash, then release and update site.

Do not label this stable or mark those manual checks passed without evidence.
