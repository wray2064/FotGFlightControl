// Run: node scripts/test-dialog.cjs <path-to-fengari-package>
const fs = require('fs');
const path = require('path');
const {lua, lauxlib, lualib, to_luastring, to_jsstring} = require(process.argv[2] || 'fengari');
const L = lauxlib.luaL_newstate();
lualib.luaL_openlibs(L);
function run(source, name) {
  if (lauxlib.luaL_loadbuffer(L, to_luastring(source), null, to_luastring(name)) || lua.lua_pcall(L, 0, 0, 0))
    throw new Error(to_jsstring(lua.lua_tostring(L, -1)));
}
run(`
  hooks, telemetry, cursor_calls = {}, {}, 0
  BUTTON_CONTROL_NORMAL, BUTTON_CONTROL_LUA_ADDITIVE = 0, 1
  engine = { addHook = function(name, fn) hooks[name] = fn end }
  mn = { getMissionFilename = function() return "practice" end }
  ba = { setControlMode = function() return "" end }
  gr = { isVR = function() return false end }
  ui = {}
  io.Keybinding = {}
  io.setCursorHidden = function(hidden, grab)
    assert(hidden == false and grab == false)
    cursor_calls = cursor_calls + 1
  end
  cf = { openFile = function()
    return { isValid = function() return true end,
      write = function(_, body) telemetry[#telemetry+1] = body end,
      close = function() end }
  end }
`, 'mock-engine');
run(fs.readFileSync(path.join(__dirname, '../mod/FotG_Gamepad_Wheel/data/tables/lua/zzz_fotg_wheel-sct.lua'), 'utf8'), 'wheel');
run(`
  local w = FotGWheel
  w.state = "GS_STATE_GAME_PLAY"
  w:publish_state()
  assert(telemetry[#telemetry]:match("^FLIGHT|"))
  w.mode = "macros"
  w.pending_actions = {"FIRE_PRIMARY"}
  w.hold_elapsed = {A = 1}
  hooks["On Dialog Init"]()
  assert(w.dialog_depth == 1 and w.mode == nil)
  assert(next(w.pending_actions) == nil and next(w.hold_elapsed) == nil)
  assert(telemetry[#telemetry]:match("^MENU|GS_STATE_GAME_PLAY|practice|wheel=0|%d+|dialog=1|"))
  -- Dialog frames work even when VR simulation is paused. They must neither
  -- run flight logic nor override the engine's dialog choices.
  gr.isVR = function() return true end
  assert(hooks["On Dialog Frame"]() == nil)
  w:frame()
  w:draw()
  w:draw_vr(nil, "text")
  assert(cursor_calls == 1)
  hooks["On Dialog Init"]()
  hooks["On Dialog Close"]()
  assert(w.dialog_depth == 1 and telemetry[#telemetry]:match("^MENU|"))
  w.input_sequence, w.last_tap_sequence = 20, 3
  hooks["On Dialog Close"]()
  assert(w.dialog_depth == 0 and telemetry[#telemetry]:match("^FLIGHT|"))
  assert(w.input_age == 10 and w.input_sequence == nil and w.last_tap_sequence == nil)
  hooks["On Dialog Close"]()
  assert(w.dialog_depth == 0)
  -- A popup in a normal menu must stay in menu mode when dismissed.
  w.state = "GS_STATE_MAIN_MENU"
  hooks["On Dialog Init"]()
  hooks["On Dialog Close"]()
  assert(telemetry[#telemetry]:match("^MENU|"))
  -- Campaign interface missions also keep their existing menu classification.
  w.state = "GS_STATE_GAME_PLAY"
  mn.getMissionFilename = function() return "_spc__gcw_intro" end
  hooks["On Dialog Init"]()
  hooks["On Dialog Close"]()
  assert(telemetry[#telemetry]:match("^MENU|"))
`, 'dialog-regressions');
console.log('Dialog regression checks passed (actual wheel Lua, mocked engine).');
module.exports = {run};
