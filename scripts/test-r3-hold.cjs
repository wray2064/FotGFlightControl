const {run} = require('./test-dialog.cjs');
run(`
  local w=FotGWheel
  w.mode=nil
  w.state="GS_STATE_GAME_PLAY"
  FotGWheelConfig={hold_ms=450,hold_enabled=false}
  local r3=128
  local quick=0
  local function tap() quick=quick+1 end
  w:hold_button("R3",r3,r3,0,0.1,tap)
  w:hold_button("R3",r3,0,r3,0.1,tap)
  assert(quick==1 and w.mission_key_sequence==0)
  w:hold_button("R3",r3,r3,0,0.1,tap)
  w:hold_button("R3",r3,r3,r3,0.4,tap)
  assert(quick==1 and w.mission_key=="mouse_mode" and w.mission_key_sequence==1)
  w:hold_button("R3",r3,0,r3,0.1,tap)
  assert(quick==1)
  FotGWheelConfig.r3_mouse_hold_enabled=false
  w:hold_button("R3",r3,r3,0,0.1,tap)
  assert(quick==2)
  FotGWheelConfig={hold_enabled=true,hold_ms=450,hold_bindings={R3="target_next_subsystem"}}
  w:hold_button("R3",r3,r3,0,0.5,tap)
  assert(w.pending_actions[#w.pending_actions]=="TARGET_NEXT_SUBOBJECT" and w.mission_key_sequence==1)
  -- RB defaults to primary-bank cycle on release and ship mode on a hold.
  w.pending_actions={}
  local rb=512
  local primary=0
  local function bank() primary=primary+1 end
  FotGWheelConfig={hold_enabled=false,hold_ms=450}
  w.cycle_ship_mode=function(self) self:queue_action("SHIP_MODE_TEST") end
  w:hold_button("RB",rb,rb,0,0.1,bank)
  w:hold_button("RB",rb,0,rb,0.1,bank)
  assert(primary==1 and #w.pending_actions==0)
  w:hold_button("RB",rb,rb,0,0.1,bank)
  w:hold_button("RB",rb,rb,rb,0.4,bank)
  w:hold_button("RB",rb,0,rb,0.1,bank)
  assert(primary==1 and w.pending_actions[#w.pending_actions]=="SHIP_MODE_TEST")
  FotGWheelConfig.rb_mode_hold_enabled=false
  w:hold_button("RB",rb,rb,0,0.1,bank)
  assert(primary==2)
  FotGWheelConfig={hold_enabled=true,hold_ms=450,hold_bindings={RB="target_next_subsystem"}}
  w:hold_button("RB",rb,rb,0,0.5,bank)
  assert(w.pending_actions[#w.pending_actions]=="TARGET_NEXT_SUBOBJECT")
  local l3,lb=64,256
  local matches,secondaries=0,0
  FotGWheelConfig={hold_enabled=false,hold_ms=450}
  w:hold_button("L3",l3,l3,0,0.1,function() matches=matches+1 end)
  w:hold_button("L3",l3,0,l3,0.1,function() matches=matches+1 end)
  assert(matches==1 and w.mission_key_sequence==1)
  w:hold_button("L3",l3,l3,0,0.2,function() matches=matches+1 end)
  w:hold_button("L3",l3,l3,l3,0.2,function() matches=matches+1 end)
  w:hold_button("L3",l3,l3,l3,0.1,function() matches=matches+1 end)
  assert(w.mission_key=="link_guns" and w.mission_key_sequence==2)
  w:hold_button("L3",l3,0,l3,0.1,function() matches=matches+1 end)
  assert(matches==1)
  w:hold_button("LB",lb,lb,0,0.1,function() secondaries=secondaries+1 end)
  w:hold_button("LB",lb,0,lb,0.1,function() secondaries=secondaries+1 end)
  assert(secondaries==1)
  w:hold_button("LB",lb,lb,0,0.2,function() secondaries=secondaries+1 end)
  w:hold_button("LB",lb,lb,lb,0.2,function() secondaries=secondaries+1 end)
  w:hold_button("LB",lb,lb,lb,0.1,function() secondaries=secondaries+1 end)
  assert(w.mission_key=="link_torpedoes" and w.mission_key_sequence==3)
  w:hold_button("LB",lb,0,lb,0.1,function() secondaries=secondaries+1 end)
  assert(secondaries==1)
  FotGWheelConfig.l3_gun_hold_enabled=false
  FotGWheelConfig.lb_torpedo_hold_enabled=false
  w:hold_button("L3",l3,l3,0,0.1,function() matches=matches+1 end)
  w:hold_button("LB",lb,lb,0,0.1,function() secondaries=secondaries+1 end)
  assert(matches==2 and secondaries==2)
  FotGWheelConfig={hold_enabled=true,hold_ms=450,hold_bindings={L3="target_next_subsystem",LB="target_next_subsystem"}}
  w:hold_button("L3",l3,l3,0,0.5,tap)
  w:hold_button("LB",lb,lb,0,0.5,tap)
  assert(w.pending_actions[#w.pending_actions]=="TARGET_NEXT_SUBOBJECT" and w.mission_key_sequence==3)
`, 'r3-hold-regressions');
console.log('Default tap, hold, disable, and override checks passed.');
