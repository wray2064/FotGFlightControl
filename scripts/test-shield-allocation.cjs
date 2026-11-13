const {run} = require('./test-dialog.cjs');
run(`
  local function mock_ship(count)
    local shields={CombinedMax=400, [1]=100, [2]=100,
      isValid=function() return true end}
    if count==4 then shields[3],shields[4]=100,100 end
    -- Fengari's table length matches the engine handle's section count.
    return {Shields=shields, isValid=function() return true end,
      isDying=function() return false end,
      getBreedName=function() return 'Ship' end}
  end
  local ship=mock_ship(2)
  FotG={GetPlayerShip=function() return ship end}
  ba.MultiplayerMode=false
  local wheel=FotGWheel
  wheel.state='GS_STATE_GAME_PLAY'
  wheel:open_shields()
  assert(wheel.mode=='shields' and #wheel.shield_values==2)
  wheel.input.rx,wheel.input.ry=0,0
  wheel:shield_frame(0,function() return false end,0.016)
  wheel.input.rx=30000
  wheel:shield_frame(0,function() return false end,0.016)
  assert(wheel.shield_values[1]>100 and wheel.shield_values[2]<100)
  assert(ship.Shields[1]==100 and ship.Shields[2]==100)
  wheel:shield_frame(0,function(flag) return flag==8192 end,0.016)
  assert(wheel.mode==nil and ship.Shields[1]==100)
  wheel:open_shields()
  wheel:shield_frame(0,function(flag) return flag==16384 end,0.016)
  assert(wheel.shield_values[1]==200 and wheel.shield_values[2]==0)
  assert(ship.Shields[1]==100)
  wheel:shield_frame(0,function(flag) return flag==4096 end,0.016)
  assert(wheel.mode==nil and wheel.shield_pending)
  assert(wheel.mission_key=='shield_front')
  -- Damage or recharge before the delayed tutorial key must not create HP.
  ship.Shields[1],ship.Shields[2]=70,90
  local preview=wheel.shield_pending.values
  wheel.shield_pending=nil
  assert(wheel:apply_shields(preview))
  assert(math.abs(ship.Shields[1]+ship.Shields[2]-160)<0.001)
  assert(ship.Shields[1]==160 and ship.Shields[2]==0)
  ship=mock_ship(4)
  for i=1,4 do ship.Shields[i]=50 end
  wheel:open_shields()
  assert(#wheel.shield_values==4)
  wheel.input.rx,wheel.input.ry=0,0
  wheel:shield_frame(0,function() return false end,0.016)
  wheel.input.ry=-30000
  wheel:shield_frame(0,function() return false end,0.016)
  assert(wheel.shield_selected==2)
  wheel:shield_frame(0,function(flag) return flag==16384 end,0.016)
  assert(wheel.shield_values[2]==200)
  assert(math.abs(wheel.shield_values[1]+wheel.shield_values[2]+
    wheel.shield_values[3]+wheel.shield_values[4]-200)<0.001)
  wheel:shield_frame(0,function(flag) return flag==4096 end,0.016)
  assert(wheel.mode==nil and wheel.shield_pending==nil)
  assert(ship.Shields[2]==200)
  assert(math.abs(ship.Shields[1]+ship.Shields[2]+ship.Shields[3]+ship.Shields[4]-200)<0.001)
  ship=mock_ship(2)
  ship.Shields[1],ship.Shields[2]=200,200
  wheel:open_shields()
  wheel:shield_frame(0,function(flag) return flag==16384 end,0.016)
  assert(wheel.shield_values[1]==400 and wheel.shield_values[2]==0)
  assert(ship.Shields[1]==200 and ship.Shields[2]==200)
  wheel:close_panel(false)
  ba.MultiplayerMode=true
  wheel:open_shields()
  assert(wheel.mode==nil)
`, 'shield-allocation-regressions');
console.log('Shield preview, cancel, max, live-total preservation, two/four sections and tutorial relay checks passed.');
