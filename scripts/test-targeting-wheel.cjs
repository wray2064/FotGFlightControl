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
  assert(entries[6].name=="Computer: OFF"
    and entries[6].action=="toggle_targeting_computer")
  AutoAim={SecondaryAutoAimOn=true}
  local _,on_entries=w:view()
  assert(on_entries[6].name=="Computer: ON")
  AutoAim=nil
  w.selected=6
  local before=w.mission_key_sequence
  w:confirm()
  assert(w.mode==nil and w.mission_key=="targeting_computer"
    and w.mission_key_sequence==before+1)
  w.mode="targeting"
  w.selected=8
  w:confirm()
  assert(w.pending_actions[#w.pending_actions]=="TARGET_NEXT_SUBOBJECT"
    and w.mode==nil)
`, 'targeting-wheel-regressions');
console.log('Targeting wheel regression checks passed.');
