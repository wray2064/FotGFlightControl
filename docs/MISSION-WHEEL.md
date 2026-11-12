# Combined Macros / Mission wheel

D-pad Right opens one wheel. The four editable macros occupy the cardinal
directions. The diagonal directions open fixed mission-command groups:

| Direction | Group | Choices |
| --- | --- | --- |
| Up-left | Responses 1–5 | Actual number keys 1, 2, 3, 4, 5 |
| Up-right | Responses 6–0 | Actual number keys 6, 7, 8, 9, 0 |
| Down-left | Dock / nav | Contextual Enter, next nav point, autopilot |
| Down-right | Other mission | Mission log (F4), fleet info (F11), fleet page (Space) |

Select a group with the right stick and A, then select a command and press A.
B returns to the macro wheel from a group; B on the macro wheel closes it.
D-pad Right closes either level. The old Y toggle, X page switch, and saved
`fotg-right-wheel.txt` mode are no longer used. Existing macro configurations
and four-slot layout profiles retain their actions and order.

Enter is context-sensitive in FotG. Some missions use it for docking or
confirmation, while others require a numbered response. The LB+RB hold still
requests hyperspace separately, with FotG's jump-vector checks intact.

Lua publishes only a fixed mission-key token and sequence. The bridge
whitelists its keyboard translation, ignores the initial baseline, consumes
the request once, and discards it if the game lacks focus or a dialog is open.
Navigation and autopilot use native controls through the wheel; no profile can
inject an arbitrary key or control name.

The bridge self-test, actual-Lua mission wheel regression and dialog regression
pass. Campaign-specific behavior and VR legibility still need player testing
before a public release.
