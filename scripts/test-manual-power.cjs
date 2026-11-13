const {run} = require('./test-dialog.cjs');
run(`
  local ship={EtsEngineIndex=4,EtsShieldIndex=4,EtsWeaponIndex=4,
    isValid=function() return true end,isDying=function() return false end,
    getBreedName=function() return "Ship" end,
    EtsSetIndexes=function(self,e,s,w)
      assert(e>=0 and s>=0 and w>=0 and e+s+w==12)
      self.EtsEngineIndex,self.EtsShieldIndex,self.EtsWeaponIndex=e,s,w
      return true
    end}
  FotG={GetPlayerShip=function() return ship end}
  ba.MultiplayerMode=false
  local wheel=FotGWheel
  wheel.state="GS_STATE_GAME_PLAY"
  wheel:open_power()
  assert(wheel.mode=="power" and wheel.power_values[1]==4)
  assert(wheel.power_selected==3)
  wheel.input.rx,wheel.input.ry=0,0
  wheel:power_frame(0,function() return false end,0.016)
  wheel.input.ry=-30000
  wheel:power_frame(0,function() return false end,0.016)
  assert(wheel.power_selected==2)
  wheel.input.ry=0
  wheel:power_frame(0,function() return false end,0.016)
  wheel.input.ry=-30000
  wheel:power_frame(0,function() return false end,0.016)
  assert(wheel.power_selected==1)
  wheel.input.rx,wheel.input.ry=0,0
  wheel:power_frame(0,function() return false end,0.016)
  wheel.input.rx,wheel.input.ry=30000,0
  wheel:power_frame(0,function() return false end,0.016)
  assert(wheel.power_values[1]==5 and wheel.power_values[2]+wheel.power_values[3]==7)
  -- Adjustments remain a preview until A; B discards and closes.
  assert(ship.EtsShieldIndex==4)
  wheel:power_frame(0,function(flag) return flag==8192 end,0.016)
  assert(wheel.mode==nil and ship.EtsShieldIndex==4)
  wheel:open_power()
  wheel.power_selected=1
  wheel.input.rx=0
  wheel:power_frame(0,function() return false end,0.016)
  wheel.input.rx=30000
  wheel:power_frame(0,function() return false end,0.016)
  wheel:power_frame(0,function(flag) return flag==4096 end,0.016)
  assert(ship.EtsShieldIndex==5 and ship.EtsEngineIndex+ship.EtsWeaponIndex==7)
  assert(wheel.mode==nil)
  -- Every repeated edit conserves the total and respects the 0..12 bounds.
  wheel:open_power()
  wheel.power_selected=2
  for i=1,20 do wheel:power_shift(1) end
  assert(wheel.power_values[2]==12 and wheel.power_values[1]==0 and wheel.power_values[3]==0)
  for i=1,20 do wheel:power_shift(-1) end
  assert(wheel.power_values[2]==0 and wheel.power_values[1]+wheel.power_values[3]==12)
  wheel:power_frame(0,function(flag) return flag==16384 end,0.016)
  assert(wheel.power_values[2]==12 and wheel.power_values[1]==0 and wheel.power_values[3]==0)
  assert(ship.EtsEngineIndex~=12)
  wheel:power_frame(0,function(flag) return flag==4096 end,0.016)
  assert(wheel.mode==nil and ship.EtsEngineIndex==12)
  wheel:open_panel("systems")
  ba.MultiplayerMode=true
  wheel:open_power()
  assert(wheel.mode=="systems")
`, 'manual-power-regressions');
console.log('Manual power selection, preview, apply, cancel, bounds, and multiplayer checks passed.');
