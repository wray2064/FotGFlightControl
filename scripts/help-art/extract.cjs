// Runs the real wheel Lua (zzz_fotg_wheel-sct.lua) with a recording graphics layer and writes every draw call
// (circles, lines, rectangles, text, with their colors and positions) for the scenes used in the help pages.
//   node extract.cjs <path-to-fengari-package> draw-calls.json font-metrics.json
// Re-run it, then render.py, whenever the wheel drawing code changes.
const fs = require('fs');
const path = require('path');
const {lua, lauxlib, lualib, to_luastring, to_jsstring} = require(process.argv[2] || 'fengari');
const outPath = process.argv[3] || 'draw-calls.json';
const metrics = JSON.parse(fs.readFileSync(process.argv[4] || path.join(__dirname, 'font-metrics.json'), 'utf8'));
const L = lauxlib.luaL_newstate();
lualib.luaL_openlibs(L);
function run(source, name) {
  if (lauxlib.luaL_loadbuffer(L, to_luastring(source), null, to_luastring(name)) || lua.lua_pcall(L, 0, 0, 0))
    throw new Error(to_jsstring(lua.lua_tostring(L, -1)));
}
function global(name) {
  lua.lua_getglobal(L, to_luastring(name));
  const s = lua.lua_tostring(L, -1);
  const v = s ? to_jsstring(s) : null;
  lua.lua_pop(L, 1);
  return v;
}
const widths = Object.entries(metrics.widths).map(([b, w]) => `[${b}]=${w}`).join(',');

run(`
  WIDTHS = {${widths}}
  hooks, telemetry = {}, {}
  BUTTON_CONTROL_NORMAL, BUTTON_CONTROL_LUA_ADDITIVE = 0, 1
  engine = { addHook = function(name, fn) hooks[name] = fn end }
  mn = { getMissionFilename = function() return "practice" end, CommItems = {} }
  ba = { setControlMode = function() return "" end, MultiplayerMode = false }
  ui = {}
  io.Keybinding = {}
  io.setCursorHidden = function() end
  cf = { openFile = function()
    return { isValid = function() return true end, write = function() end, close = function() end }
  end }
  CALLS = {}
  local color = {255, 255, 255, 255}
  local function rec(...) CALLS[#CALLS + 1] = {...} end
  FONT = { Height = ${metrics.height} }
  gr = {
    isVR = function() return false end,
    getScreenWidth = function() return 1920 end,
    getScreenHeight = function() return 1080 end,
    Fonts = { font_bold_medium = FONT, font_bold_large = FONT, font_medium = FONT, font_large = FONT },
    CurrentFont = FONT,
    setColor = function(r, g, b, a) color = {r, g, b, a or 255} end,
    drawCircle = function(radius, x, y, filled)
      rec("c", radius, x, y, filled and 1 or 0, color[1], color[2], color[3], color[4]) end,
    drawLine = function(x1, y1, x2, y2)
      rec("l", x1, y1, x2, y2, color[1], color[2], color[3], color[4]) end,
    drawRectangle = function(x1, y1, x2, y2, filled)
      rec("r", x1, y1, x2, y2, filled and 1 or 0, color[1], color[2], color[3], color[4]) end,
    drawString = function(s, x, y)
      rec("s", s, x, y, color[1], color[2], color[3], color[4]) end,
    getStringWidth = function(s)
      local w = 0
      for i = 1, #s do w = w + (WIDTHS[string.byte(s, i)] or 10) end
      return w
    end
  }
`, 'mock-engine');
run(fs.readFileSync(path.join(__dirname, '../../mod/FotG_Gamepad_Wheel/data/tables/lua/zzz_fotg_wheel-sct.lua'), 'utf8'), 'wheel');

run(`
  local function valid() return true end
  local function never() return false end
  local function subsystem(turret)
    return { isValid = valid, Targetable = true, HitpointsLeft = 100, isTurret = function() return turret end }
  end
  local target = { isValid = valid, isDying = never, getBreedName = function() return "Ship" end,
    getSubsystemList = function()
      local items, i = { subsystem(false), subsystem(true) }, 0
      return function() i = i + 1; return items[i] end
    end }
  local ship = { isValid = valid, isDying = never, getBreedName = function() return "Ship" end, Target = target }
  FotG = { GetPlayerShip = function() return ship end }
  AutoAim = { SecondaryAutoAimOn = true }
  local function item(name)
    return { isValid = valid, Name = name, Active = true, getMenuTitle = function() return "Give Command To" end }
  end
  mn.CommItems = { item("Ships"), item("Wings"), item("All Ships"), item("Reinforcements"),
    item("Request Rearm/Repair"), item("Abort Rearm/Repair") }

  local function enc(v)
    if type(v) == "number" then return string.format("%.3f", v) end
    local text = tostring(v)
    assert(not string.find(text, '"', 1, true) and not string.find(text, string.char(92), 1, true), "label needs escaping: " .. text)
    return '"' .. text .. '"'
  end
  function capture(name, canvas_w, canvas_h, setup)
    CALLS = {}
    local W = FotGWheel
    W.state = "GS_STATE_GAME_PLAY"
    W.dialog_depth = 0
    W.mode, W.selected, W.page, W.message = nil, nil, 1, nil
    setup(W)
    W:draw()
    local out = {}
    for _, call in ipairs(CALLS) do
      local parts = {}
      for i = 1, #call do parts[i] = enc(call[i]) end
      out[#out + 1] = "[" .. table.concat(parts, ",") .. "]"
    end
    return '{"name":"' .. name .. '","canvas":[' .. canvas_w .. ',' .. canvas_h .. '],"center":[960,540],"calls":['
      .. table.concat(out, ",") .. ']}'
  end
  local scenes = {}
  local function add(name, w, h, setup) scenes[#scenes + 1] = capture(name, w, h, setup) end
  local WHEEL_W, WHEEL_H = 900, 520
  add("wheel-systems", WHEEL_W, WHEEL_H, function(W) W.mode = "systems"; W.selected = 1 end)
  add("wheel-targeting", WHEEL_W, WHEEL_H, function(W) W.mode = "targeting"; W.selected = 3 end)
  add("wheel-squad", WHEEL_W, WHEEL_H, function(W) W.mode = "commands"; W.selected = 3 end)
  add("wheel-macros", WHEEL_W, WHEEL_H, function(W) W.mode = "macros"; W.selected = 1 end)
  add("wheel-responses-1-5", WHEEL_W, WHEEL_H, function(W) W.mode = "mission_1_5"; W.selected = 1 end)
  add("wheel-responses-6-0", WHEEL_W, WHEEL_H, function(W) W.mode = "mission_6_0"; W.selected = 1 end)
  add("wheel-dock-nav", WHEEL_W, WHEEL_H, function(W) W.mode = "mission_dock"; W.selected = 1 end)
  add("wheel-mission-commands", WHEEL_W, WHEEL_H, function(W) W.mode = "mission_other"; W.selected = 1 end)
  add("power-equal", 500, 500, function(W) W.mode = "power"; W.power_values = {4, 4, 4}; W.power_selected = 3 end)
  add("power-adjusted", 500, 500, function(W) W.mode = "power"; W.power_values = {3, 3, 6}; W.power_selected = 3 end)
  add("shields-four-sections", 500, 500, function(W)
    W.mode = "shields"; W.shield_values = {160, 60, 120, 60}; W.shield_max = 400; W.shield_selected = 1 end)
  add("shields-two-sections", 500, 500, function(W)
    W.mode = "shields"; W.shield_values = {140, 60}; W.shield_max = 200; W.shield_selected = 1 end)
  SCENES_JSON = '{"scenes":[' .. table.concat(scenes, ",") .. ']}'
`, 'scenes');

fs.writeFileSync(outPath, global('SCENES_JSON'));
const data = JSON.parse(fs.readFileSync(outPath, 'utf8'));
console.log(data.scenes.map(s => `${s.name}: ${s.calls.length} draw calls`).join('\n'));
