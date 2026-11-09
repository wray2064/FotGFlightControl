# Mission / Macros wheel

D-pad Right retains the four-macro wheel by default. Y while the right wheel
is open swaps to Mission mode or back to Macros. X changes Mission pages.
The selection is stored in data/players/fotg-right-wheel.txt via FSO cfile,
independent of editable macro configuration and layout profiles. Unknown saved
values fall back to Macros on a fresh run.

Page 1: Response 1–8. Page 2: Response 9, Response 0, Mission log (F4), Fleet
info (F11), Fleet page (Space). Right stick selects; A sends the actual key
once and closes the wheel; B cancels. D-pad Right closes either variant.
Both flat and VR drawing show swap/page hints. Existing macro labels, actions
and layout permutations remain intact. This does not add controller text
entry or support GCW Ctrl+Enter planning forms.

The Lua-to-bridge telemetry adds missionkey and missionseq named fields while
preserving the existing positional sequence. The bridge permits only the
listed keys, ignores its initial request baseline, deduplicates sequences,
and consumes/discards requests when focus, connection, dialog or modal state
prevents dispatch. It never sends arbitrary profile-supplied key codes.

Verification: bridge build and --self-test passed (key whitelist, sequence
baseline/deduplication, invalid commands); scripts/test-mission-wheel.cjs passed
against actual wheel Lua in Fengari, including Y/X frame handlers, numbered
choices, page contents, one-shot confirmation, saved mode restore, invalid
saved mode, macro/layout preservation. Existing dialog regressions passed.
Run with: node scripts/test-mission-wheel.cjs <path-to-fengari-package>.

Installed locally in C:\Games\Fate of the Galaxy 1.1.0, backed up first.
No release or merge. Actual mission response/docking/log behavior and visual
layout in flat/Meta VR still require in-game confirmation.
