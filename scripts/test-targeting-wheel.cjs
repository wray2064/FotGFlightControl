const {run} = require('./test-dialog.cjs');
run(`
  local part={isValid=function() return true end, isTurret=function() return false end,
    Targetable=true, HitpointsLeft=10}
  local target={isValid=function() return true end,
    isDying=function() return false end,
    getBreedName=function() return "Ship" end,
    getSubsystemList=function()
      local used=false
      return function() if not used then used=true; return part end end
    end}
  local player={isValid=function() return true end,
    isDying=function() return false end,
    getBreedName=function() return "Ship" end, Target=target}
  FotG={GetPlayerShip=function() return player end}
  FotGWheelConfig={layouts={targeting={4,3,2,1,5,6,7}}}
  local w=FotGWheel
  w.mode, w.target_mode="targeting", "enemy"
  local _,entries=w:view()
  assert(#entries==8 and entries[1].name=="Next live turret")
  assert(entries[8].sector==8 and entries[8].name=="Next subsystem"
    and entries[8].active)
  w.selected=8
  w:confirm()
  assert(w.pending_actions[#w.pending_actions]=="TARGET_NEXT_SUBOBJECT"
    and w.mode==nil)
`, 'targeting-wheel-regressions');
console.log('Targeting wheel regression checks passed.');
