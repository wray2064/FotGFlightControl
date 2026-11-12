const {run} = require('./test-dialog.cjs');
run(`
  local w = FotGWheel
  mn.getMissionFilename = function() return "practice" end
  w.state = "GS_STATE_GAME_PLAY"
  FotGWheelConfig = {macros = {
    {label="One",action="shields_max"}, {label="Two",action="weapons_max"},
    {label="Three",action="balance_energy"}, {label="Four",action="target_nearest_enemy"}
  }, layouts = {macros = {4,3,2,1}}}
  w:open_panel("macros")
  local title, entries = w:view()
  assert(title == "MACROS / MISSION" and #entries == 8 and entries[1].name == "Four")
  local gateways = {[8]="mission_1_5", [2]="mission_6_0", [6]="mission_dock", [4]="mission_other"}
  for sector, mode in pairs(gateways) do
    w.selected=sector; w:confirm(); assert(w.mode == mode)
    title, entries=w:view()
    if mode == "mission_1_5" or mode == "mission_6_0" then
      assert(#entries == 5)
      for i=1,5 do
        local n=mode == "mission_1_5" and i or (i+5)%10
        assert(entries[i].argument == tostring(n))
      end
    elseif mode == "mission_dock" then
      assert(entries[1].argument == "enter" and entries[2].argument == "NAV_CYCLE"
        and entries[3].argument == "AUTO_PILOT_TOGGLE")
    else
      assert(entries[1].argument == "log" and entries[2].argument == "fleet"
        and entries[3].argument == "fleet_page")
    end
    w.mode, w.selected="macros", nil
  end
  w.selected=8; w:confirm(); w.selected=3
  local before=w.mission_key_sequence
  w:confirm()
  assert(w.mode == nil and w.mission_key == "3" and w.mission_key_sequence == before+1)
  w:confirm(); assert(w.mission_key_sequence == before+1)
  w:open_panel("macros"); w.selected=2; w:confirm(); w.selected=5; w:confirm()
  assert(w.mode == nil and w.mission_key == "0")
  w:open_panel("macros"); w.selected=6; w:confirm(); w.selected=1; w:confirm()
  assert(w.mode == nil and w.mission_key == "enter")
  w:open_panel("macros"); w.selected=6; w:confirm(); w.selected=3; w:confirm()
  assert(w.mode == nil and w.pending_actions[#w.pending_actions] == "NAV_CYCLE")
  w:open_panel("macros"); w.selected=4; w:confirm(); w.selected=1; w:confirm()
  assert(w.mode == nil and w.mission_key == "log")
  gr.isVR=function() return false end
  ba.getRealFrametime=function() return 0.025 end
  w.poll_input=function() end
  w.flush_action=function() end
  w:open_panel("macros")
  w.input_age, w.prior_buttons=0,0
  w.input={buttons=4096,rx=0,ry=0,lx=0,ly=0}
  w.selected=8; w:frame(); assert(w.mode == "mission_1_5")
  w.input.buttons=0; w:frame()
  w.input.buttons=8192; w:frame(); assert(w.mode == "macros")
`, 'mission-wheel-regressions');
console.log('Mission wheel regression checks passed.');
