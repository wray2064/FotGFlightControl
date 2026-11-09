// Uses the same actual-Lua harness and engine mocks as the dialog regression.
const {run} = require('./test-dialog.cjs');
run(`
  local w = FotGWheel
  local files = {}
  cf.openFile = function(name, mode)
    if mode == "r" and not files[name] then return nil end
    return { isValid = function() return true end,
      read = function() return files[name] end,
      write = function(_, value) files[name] = value end,
      close = function() end }
  end
  mn.getMissionFilename = function() return "practice" end
  w.state = "GS_STATE_GAME_PLAY"
  w.mode, w.right_wheel, w.right_wheel_loaded = nil, "macros", false
  FotGWheelConfig = { macros = {
    {label="My macro", action="target_nearest_enemy"},
    {label="Two", action="balance_energy"},
    {label="Three", action="shields_max"},
    {label="Four", action="weapons_max"}
  }, layouts = {macros = {4,3,2,1}} }
  local original = FotGWheelConfig.macros
  w:open_panel("macros")
  local title, entries = w:view()
  assert(title == "MACROS" and #entries == 4 and entries[1].name == "Four")
  w:swap_right_wheel()
  assert(w.mode == "mission" and files["fotg-right-wheel.txt"] == "mission")
  title, entries = w:view()
  assert(title == "MISSION 1/2" and #entries == 8)
  for i, entry in ipairs(entries) do
    assert(entry.name == "Response " .. i and entry.argument == tostring(i) and entry.sector == i)
  end
  w.selected = 3
  local before = w.mission_key_sequence
  w:confirm()
  assert(w.mode == nil and w.mission_key == "3" and w.mission_key_sequence == before + 1)
  assert(files["fotg-wheel-state.txt"]:find("|missionkey=3|missionseq=" .. (before+1),1,true))
  w:confirm() -- closed wheel cannot repeat a choice
  assert(w.mission_key_sequence == before+1)
  w:open_panel("macros")
  assert(w.mode == "mission")
  w.page = 2
  title, entries = w:view()
  assert(title == "MISSION 2/2" and #entries == 5)
  assert(entries[1].argument == "9" and entries[2].argument == "0")
  assert(entries[3].argument == "log" and entries[4].argument == "fleet" and entries[5].argument == "fleet_page")
  w.selected = 3
  w:confirm()
  assert(w.mission_key == "log" and w.mode == nil)
  w:open_panel("macros")
  w:swap_right_wheel()
  assert(w.mode == "macros" and files["fotg-right-wheel.txt"] == "macros")
  title, entries = w:view()
  assert(entries[4].name == "My macro" and FotGWheelConfig.macros == original)
  -- Simulate a fresh Lua instance restoring the user's choice, and reject
  -- unknown/corrupt saved values without executing them.
  w:close_panel(false)
  w.right_wheel_loaded = false
  files["fotg-right-wheel.txt"] = "mission"
  w:open_panel("macros")
  assert(w.mode == "mission")
  w:close_panel(false)
  w.right_wheel_loaded, w.right_wheel = false, "macros"
  files["fotg-right-wheel.txt"] = "unknown"
  w:open_panel("macros")
  assert(w.mode == "macros")
  -- Exercise the real frame handlers for Y swap and X page switching.
  gr.isVR = function() return false end
  ba.getRealFrametime = function() return 0.025 end
  w.poll_input = function() end
  w.flush_action = function() end
  w.input_age, w.prior_buttons = 0, 0
  w.input = {buttons=32768, rx=0, ry=0, lx=0, ly=0}
  w:frame()
  assert(w.mode == "mission" and w.page == 1)
  w.input.buttons = 0; w:frame()
  w.input.buttons = 16384; w:frame()
  assert(w.page == 2)
  w:frame() -- holding X must not toggle repeatedly
  assert(w.page == 2)
  w.input.buttons = 0; w:frame()
  w.input.buttons = 32768; w:frame()
  assert(w.mode == "macros")
`, 'mission-wheel-regressions');
console.log('Mission wheel regression checks passed.');
