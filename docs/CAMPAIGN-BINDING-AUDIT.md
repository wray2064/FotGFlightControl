# Campaign binding audit — FotG 1.1.2

Static audit, 2026-11-08. No bindings changed; no campaign playthrough performed.
Source data: C:\Games\Fate of the Galaxy 1.1.0\kn_portable\Library\FotG_R\FotG_R-1.1.2\data.

Highest priority: raw number-key mission responses (1–9, and 0 where used).
- missions/_SPC_E_Exc_m04.fs2:4054 and :4195 wait for key-pressed "1";
  objective text at :4063 and :4204 says Press $1$ to Dock.
- tables/lua/y_dialogue2-sct.lua:17 maps both number-row and numpad keys to
  response choices; :468 listens to On Key Pressed during gameplay.
- missions/_SPC_S_Exc_m02.fs2 contains absolute-key-pressed checks for 0–9.
- Flight Control does not currently synthesize those number keys. Native
  command-wheel item selection is not equivalent to sending a raw number key.
  The configured boarding-task action is also not a general mission response.
Recommendation: a dedicated Mission / Responses page sending actual number
keys, accessible without a keyboard, with protection against accidental choices.

Docking is context-dependent:
- The mission above uses 1, so the hyperspace Return shortcut does not cover it.
- missions/_SPC__GCW_1base.fs2:915 onward uses Enter for docking and invokes
  GCW-ply-dock-var-update. The new shoulder shortcut can reach this Enter path.
Do not label all docking as one universal END_MISSION control.

Other uncovered controls:
- Active Pause: TOGGLE_GLIDING / P, repurposed by extra_controls-sct.lua.
  This slows mission time nearly to a stop, unlike the Start/Escape dialog.
- Mission information/message log: mission _SPC_R_Exc_m01.fs2 explicitly
  reminds players to press F4 for ongoing mission information and messages.
- GCW fleet info: 0_GCW-sct.lua:27437 defines F11; Spacebar switches pages.
  Its choice interfaces also handle Ctrl+Enter to submit.
- Boarding: 1_GA_ASL-sct.lua binds MULTI_MESSAGE_TARGET / 4 to cycle tasks.
  Flight Control offers boarding_task as a configurable action, not a default.
- Specialist options: capital ship link (MULTI_MESSAGE_FRIENDLY / 2), stationary
  targeting (CUSTOM_CONTROL_5 / X), last transmission sender (Shift+F), escort
  target (F), and next uninspected cargo (U) are not default gamepad actions.

Already covered: RB contextual ship mode includes S-foils, TIE burst and
appropriate weapon/targeting modes; LB secondary selection; targeting and
squad commands via wheels. Do not add separate defaults for those without
checking ship-specific behavior.

Priority: number responses/docking first, mission log next, active pause as a
configurable action. GCW fleet/choice and boarding controls merit contextual
pages rather than occupying additional combat-button combinations.
