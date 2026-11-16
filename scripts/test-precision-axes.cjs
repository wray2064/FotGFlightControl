// Run: node scripts/test-precision-axes.cjs <path-to-fengari-package>
// Checks the bridge's shaped flight axes reaching the ship through Lua steering, and the encoder wheel step.
const {run} = require('./test-dialog.cjs');
run(`
  local w = FotGWheel
  local ci = { Pitch = 0, Heading = 0, Bank = 0, ForwardCruise = 40 }
  local modes = {}
  ba.getControlInfo = function() return ci end
  ba.setButtonControlMode = function() return "" end
  ba.setControlMode = function(mode) if mode ~= nil then modes[#modes + 1] = mode end return "" end
  local function reset()
    ci.Pitch, ci.Heading, ci.Bank, ci.ForwardCruise = 0, 0, 0, 40
    modes = {}
    w.precision_mode, w.precision_throttle, w.precision_failed = nil, nil, nil
    w.state, w.dialog_depth, w.input_age = "GS_STATE_GAME_PLAY", 0, 0
    w.mode = nil
    mn.getMissionFilename = function() return "practice" end
  end
  local function near(a, b) return math.abs(a - b) < 1e-9 end

  -- The bridge line: the old fields, then the shaped axes and their mask.
  local body
  cf.openFile = function() return { isValid = function() return true end, read = function() return body end, write = function() end, close = function() end } end
  body = "5|0|0|0|0|0|0|123|0|0|500|-250|100|1000|15\\n"
  reset(); w:poll_input(0.016)
  assert(w.input.axis_mask == 15 and w.input.axis_pitch == 500 and w.input.axis_yaw == -250
    and w.input.axis_roll == 100 and w.input.axis_throttle == 1000)
  body = "6|0|0|0|0|0|0|123|0|0\\n"            -- an older bridge: no axes
  reset(); w:poll_input(0.016)
  assert(w.input.axis_mask == 0 and w.input.axis_pitch == nil)
  body = "7|0|0|0|0|0|0|123|0|0|1500|0|0|0|1\\n" -- out of range: ignored
  reset(); w:poll_input(0.016)
  assert(w.input.axis_mask == 0 and w.input.axis_pitch == nil)

  -- Steering: signs and the bank factor follow the game's own joystick handling.
  reset()
  w.input = { axis_mask = 7, axis_pitch = 500, axis_yaw = -250, axis_roll = 200 }
  w:precision_frame(0.016)
  assert(near(ci.Pitch, -0.5) and near(ci.Heading, -0.25) and near(ci.Bank, -0.3))
  assert(#modes == 1 and modes[1] == FLIGHT_CONTROL_LUA_STEERING and ci.ForwardCruise == 40)
  w:precision_frame(0.016)
  assert(#modes == 1)                           -- the mode is set once, not every frame
  w.input = { axis_mask = 1, axis_pitch = -1000 }
  ci.Heading, ci.Bank = 0.7, 0.7
  w:precision_frame(0.016)
  assert(near(ci.Pitch, 1) and ci.Heading == 0.7 and ci.Bank == 0.7)   -- axes outside the mask are left alone

  -- Absolute throttle: the lever wins only when it has moved; the engine's own changes (keys) are kept otherwise.
  reset()
  w.input = { axis_mask = 15, axis_pitch = 0, axis_yaw = 0, axis_roll = 0, axis_throttle = 800 }
  w:precision_frame(0.016)
  assert(modes[#modes] == FLIGHT_CONTROL_LUA_FULL and near(ci.ForwardCruise, 80))
  ci.ForwardCruise = 60                          -- a key changed the throttle
  w:precision_frame(0.016)
  assert(near(ci.ForwardCruise, 60))
  w.input.axis_throttle = 500                    -- the lever moved
  w:precision_frame(0.016)
  assert(near(ci.ForwardCruise, 50))
  w.input.axis_throttle = 505                    -- jitter inside the tolerance
  ci.ForwardCruise = 55
  w:precision_frame(0.016)
  assert(near(ci.ForwardCruise, 55))

  -- Relative throttle: a rate, clamped to the range.
  reset()
  w.input = { axis_mask = 16, axis_throttle = 500 }
  w:precision_frame(0.1)
  assert(modes[#modes] == FLIGHT_CONTROL_LUA_FULL and near(ci.ForwardCruise, 45))
  ci.ForwardCruise = 99.9
  w:precision_frame(0.1)
  assert(ci.ForwardCruise == 100)
  w.input.axis_throttle = -1000
  ci.ForwardCruise = 2
  w:precision_frame(0.1)
  assert(ci.ForwardCruise == 0)

  -- Control goes back to the engine whenever the data is stale, a dialog opens, the axes vanish or the mission ends.
  reset()
  w.input = { axis_mask = 7, axis_pitch = 100, axis_yaw = 0, axis_roll = 0 }
  w:precision_frame(0.016)
  assert(w.precision_mode == FLIGHT_CONTROL_LUA_STEERING)
  w.input_age = 0.5
  w:precision_frame(0.016)
  assert(w.precision_mode == nil and modes[#modes] == FLIGHT_CONTROL_NORMAL)
  reset()
  w.input = { axis_mask = 7, axis_pitch = 100, axis_yaw = 0, axis_roll = 0 }
  w:precision_frame(0.016)
  w:dialog_open()
  assert(w.precision_mode == nil and modes[#modes] == FLIGHT_CONTROL_NORMAL)
  w.dialog_depth = 0
  reset()
  w.input = { axis_mask = 7, axis_pitch = 100, axis_yaw = 0, axis_roll = 0 }
  w:precision_frame(0.016)
  w.input = { axis_mask = 0 }
  w:precision_frame(0.016)
  assert(w.precision_mode == nil)
  reset()
  w.input = { axis_mask = 7, axis_pitch = 100, axis_yaw = 0, axis_roll = 0 }
  w:precision_frame(0.016)
  w:mission_end()
  assert(w.precision_mode == nil and modes[#modes] == FLIGHT_CONTROL_NORMAL)
  reset()
  w.state = "GS_STATE_MAIN_MENU"
  w.input = { axis_mask = 7, axis_pitch = 100, axis_yaw = 0, axis_roll = 0 }
  w:precision_frame(0.016)
  assert(w.precision_mode == nil and #modes == 0 and ci.Pitch == 0)   -- never steers outside flight

  -- A fault gives control back, tells the player, and does not retry.
  reset()
  ba.getControlInfo = function() error("engine said no") end
  w.input = { axis_mask = 7, axis_pitch = 100, axis_yaw = 0, axis_roll = 0 }
  w:precision_frame(0.016)
  assert(w.precision_failed == true and w.precision_mode == nil and modes[#modes] == FLIGHT_CONTROL_NORMAL)
  assert(w.message == "Precision axes unavailable")
  local count = #modes
  w:precision_frame(0.016)
  assert(#modes == count)
  ba.getControlInfo = function() return ci end
  w:mission_end()
  assert(w.precision_failed == nil)             -- the next mission starts clean

  -- Encoder: each step moves the highlight to the next filled sector and wraps.
  w.view = function() return "TEST", { { sector = 1 }, { sector = 3 }, { sector = 5 } } end
  w.selected = nil
  w:step_selection(1); assert(w.selected == 1)
  w:step_selection(1); assert(w.selected == 3)
  w:step_selection(1); assert(w.selected == 5)
  w:step_selection(1); assert(w.selected == 1)     -- wraps
  w:step_selection(-1); assert(w.selected == 5)    -- and back
  w:step_selection(-1); assert(w.selected == 3)
  w.selected = nil
  w:step_selection(-1); assert(w.selected == 5)    -- anticlockwise from nothing starts at the end
  w.view = function() return "EMPTY", {} end
  w.selected = 2
  w:step_selection(1); assert(w.selected == 2)     -- nothing to move to: unchanged, no error
`, 'precision-axes');
console.log('Precision axes and encoder step checks passed.');
