// Run: node scripts/test-diagnostics.cjs <path-to-fengari-package>
// Checks the script side of diagnostic mode: inert unless players/fotg-diag-on.txt exists, then it records the game's
// bindings and the throttle the ship has, in chunk files, and stops cleanly when the file goes away.
const {run} = require('./test-dialog.cjs');
run(`
  local w = FotGWheel
  w.state, w.dialog_depth = "GS_STATE_GAME_PLAY", 0
  local on = false
  local written = {}
  local requested = {}
  cf.openFile = function(name, mode)
    requested[#requested + 1] = name .. ":" .. mode
    if name == "fotg-diag-on.txt" then
      if not on then return nil end
      return { isValid = function() return true end, close = function() end }
    end
    return { isValid = function() return true end,
             write = function(_, body) written[name] = (written[name] or "") .. body end,
             close = function() end }
  end
  ba.getControlInfo = function() return { ForwardCruise = 40 } end
  ba.setControlMode = function() return "" end
  mn.getMissionFilename = function() return "practice" end

  -- off: only the cheap marker check, no output and no state
  w:diag_frame(3)
  assert(not w.diag_on)
  assert(next(written) == nil)

  -- on: starts at the next check and records the bindings
  on = true
  w.input = { sequence = 7, axis_mask = 8, axis_throttle = 500 }
  w.precision_throttle = 50
  w:diag_frame(2.1)
  assert(w.diag_on)
  local first = written["fotg-diag-001.csv"]
  assert(first and string.find(first, "B|JOY_ABS_THROTTLE_AXIS|", 1, true), "the game's bindings were recorded first")
  for i = 1, 12 do w:diag_frame(0.12) end
  local chunk = written["fotg-diag-002.csv"]
  assert(chunk, "a chunk with samples was written")
  assert(string.find(chunk, "S|7|", 1, true))
  assert(string.find(chunk, "|500|40.000|50.000|", 1, true), chunk)

  -- a fault anywhere in the game API must never reach the player: an API that throws still yields rows
  ba.getControlInfo = function() error("boom") end
  for i = 1, 12 do w:diag_frame(0.12) end
  assert(written["fotg-diag-003.csv"], "another chunk was written even with a failing API")

  -- the marker goes away: flush and stop
  on = false
  w:diag_frame(2.1)
  assert(not w.diag_on)
`, 'diagnostics');
console.log('Diagnostic mode checks passed.');
