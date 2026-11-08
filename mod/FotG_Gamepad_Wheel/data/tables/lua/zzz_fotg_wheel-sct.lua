-- Fate of the Galaxy Gamepad Wheel, for FotG 1.1.0, 1.1.1 and 1.1.2 / FSO 26.0.1 SDL3.
-- Additive HUD/input script. It does not replace FotG's own scripts or tables.

FotGWheel = {
    mode = nil,
    page = 1,
    selected = nil,
    stick_ready = false,
    prior_buttons = 0,
    input_sequence = nil,
    last_tap_sequence = nil,
    hold_elapsed = {},
    hold_fired = {},
    input_age = 10,
    input = { buttons = 0, rx = 0, ry = 0, lx = 0, ly = 0 },
    comm_open_requested = false,
    comm_wait = 0,
    comm_signature = nil,
    pending_actions = {},
    confirm_depart = nil,
    target_mode = "enemy",
    message = nil,
    message_age = 0,
    state = "GS_STATE_MAIN_MENU",
    axes_applied_state = nil,
    telemetry_sequence = 0,
    vr_gauge = nil,
    vr_art = nil,
    vr_image_ok = 0,
    vr_image_fail = 0,
    vr_draws = 0,
    vr_debug_elapsed = 0
}

local W = FotGWheel
local D_UP, D_DOWN, D_LEFT, D_RIGHT = 1, 2, 4, 8
local B_L3, B_R3 = 64, 128
local B_LB, B_RB, B_A, B_B, B_X, B_Y = 256, 512, 4096, 8192, 16384, 32768
local HOLD_FLAGS = { A=B_A, B=B_B, X=B_X, LB=B_LB, RB=B_RB, L3=B_L3, R3=B_R3 }
-- Buttons with a configurable quick-press action, in a fixed order.
local TAP_BUTTONS = { { "A", B_A }, { "B", B_B }, { "X", B_X }, { "Y", B_Y },
    { "LB", B_LB }, { "RB", B_RB }, { "L3", B_L3 }, { "R3", B_R3 } }
local PI = math.pi
local SECTORS = 8
local BUTTON_MODE_NORMAL = BUTTON_CONTROL_NORMAL
local BUTTON_MODE_ADDITIVE = BUTTON_CONTROL_LUA_ADDITIVE
local TARGET_ACTIONS = {
    all = "TARGET_NEXT",
    enemy = "TARGET_NEXT_CLOSEST_HOSTILE",
    friend = "TARGET_NEXT_CLOSEST_FRIENDLY"
}
-- Every action a face button, bumper or stick click can run on a quick press or
-- a long press. native: a game control sent through the control system.
-- script: handled by W:execute. run: a function. held: repeats every frame while
-- the button is down (such actions take no long press). The Flight Control
-- Console offers the same ids; anything else in the config is ignored.
local BUTTON_ACTIONS = {
    none = {},
    target_reticle = { native = "TARGET_SHIP_IN_RETICLE" },
    target_attacker = { native = "TARGET_CLOSEST_SHIP_ATTACKING_SELF" },
    target_next = { run = function(self) self:queue_action(TARGET_ACTIONS[self.target_mode] or TARGET_ACTIONS.enemy) end },
    target_nearest_enemy = { script = true },
    target_nearest_friendly = { native = "TARGET_NEXT_CLOSEST_FRIENDLY" },
    target_targets_target = { native = "TARGET_TARGETS_TARGET" },
    target_newest = { native = "TARGET_NEWEST_SHIP" },
    target_next_bomb = { native = "TARGET_NEXT_BOMB" },
    target_clear = { script = true },
    target_subsystem_toggle = { script = true },
    target_subsystem_reticle = { script = true },
    target_next_subsystem = { native = "TARGET_NEXT_SUBOBJECT" },
    target_live_turret = { script = true },
    match_speed = { script = true },
    cycle_secondary = { native = "CYCLE_SECONDARY" },
    cycle_primary = { native = "CYCLE_NEXT_PRIMARY" },
    cycle_ship_mode = { run = function(self) self:cycle_ship_mode() end },
    third_gun = { held = true, run = function(self) self:fire_third_bank() end },
    afterburner = { held = true, native = "AFTERBURNER" },
    countermeasure = { native = "LAUNCH_COUNTERMEASURE" },
    shields_max = { script = true },
    weapons_max = { script = true },
    engines_max = { script = true },
    balance_energy = { script = true },
    shield_equalize = { script = true },
    transfer_shields = { script = true },
    transfer_weapons = { script = true },
    increase_weapons = { native = "INCREASE_WEAPON" },
    decrease_weapons = { native = "DECREASE_WEAPON" },
    increase_shields = { native = "INCREASE_SHIELD" },
    decrease_shields = { native = "DECREASE_SHIELD" },
    increase_engines = { native = "INCREASE_ENGINE" },
    decrease_engines = { native = "DECREASE_ENGINE" },
    max_throttle = { native = "MAX_THROTTLE" },
    zero_throttle = { native = "ZERO_THROTTLE" },
    view_chase = { native = "VIEW_CHASE" },
    view_external = { native = "VIEW_EXTERNAL" },
    toggle_hud = { native = "TOGGLE_HUD" },
    radar_range = { native = "RADAR_RANGE_CYCLE" },
    nav_cycle = { native = "NAV_CYCLE" },
    auto_pilot = { native = "AUTO_PILOT_TOGGLE" },
    boarding_task = { script = true }
}
-- Quick-press actions when the config names none: the 1.0 layout.
local TAP_DEFAULTS = {
    A = "target_reticle", B = "target_attacker", X = "target_next", Y = "third_gun",
    LB = "cycle_secondary", RB = "cycle_ship_mode", L3 = "match_speed", R3 = "target_next_subsystem"
}

local function down(buttons, flag)
    return math.floor(buttons / flag) % 2 == 1
end

local function clipped(value, low, high)
    return math.max(low, math.min(high, value))
end

local function player_ship()
    if not FotG or not FotG.GetPlayerShip then return nil end
    local ship = FotG:GetPlayerShip()
    if not ship or not ship:isValid() or ship:getBreedName() ~= "Ship" then return nil end
    if ship:isDying() then return nil end
    return ship
end

local function target_ship(player)
    local target = player and player.Target
    if target and target:isValid() and target:getBreedName() == "Ship"
        and not target:isDying() then return target end
    return nil
end

local function active_subsystem(player)
    local subsystem = player and player.TargetSubsystem
    if subsystem and subsystem:isValid() then return subsystem end
    return nil
end

local function has_targetable_subsystem(ship, turret_only)
    if not ship then return false end
    for subsystem in ship:getSubsystemList() do
        if subsystem and subsystem:isValid() and subsystem.Targetable
            and subsystem.HitpointsLeft > 0
            and (not turret_only or subsystem:isTurret()) then return true end
    end
    return false
end

local function mission_name()
    local ok, name = pcall(mn.getMissionFilename)
    return ok and tostring(name or "") or ""
end

local function is_flight_state(state)
    if state ~= "GS_STATE_GAME_PLAY" then return false end
    local mission = string.lower(mission_name())
    -- FotG's campaign interface missions use the gameplay state but need a cursor.
    if string.find(mission, "_spc__gcw_intro", 1, true)
        or string.find(mission, "_spc__gcw_0", 1, true) then
        return false
    end
    return true
end

function W:ensure_flight_axes()
    local config = FotGWheelConfig or {}
    local presets = { classic = "FotG Classic Native", classic_inverted = "FotG Classic Inverted", custom = "FotG Custom" }
    local preset = presets[config.flight_axes]
    if not preset then return end
    if not ui or not ui.ControlConfig then return end
    -- A Custom preset missing from the control table (for example after a repair that
    -- predates it) falls back to Classic once instead of retrying every frame.
    if preset == "FotG Custom" and self.custom_axes_missing then preset = "FotG Classic Native" end
    -- A saved pilot can still report this preset name while retaining axis
    -- bindings from an earlier build. Apply once on each menu/flight entry.
    if self.axes_applied_state ~= self.state
        or ui.ControlConfig.getCurrentPreset() ~= preset then
        if ui.ControlConfig.usePreset(preset) then
            self.axes_applied_state = self.state
            self:publish_state()
        elseif preset == "FotG Custom" and is_flight_state(self.state) then
            -- usePreset also fails before a pilot is loaded (pilot selection), so only a
            -- failure in flight means the preset is missing from the control table.
            self.custom_axes_missing = true
            self:notice("Custom axes not found: using Classic. Save Wheel in Configure.")
        end
    end
end

function W:publish_state()
    self.telemetry_sequence = self.telemetry_sequence + 1
    local kind = is_flight_state(self.state) and "FLIGHT" or "MENU"
    local axis_names = {}
    for _, action in ipairs({ "JOY_HEADING_AXIS", "JOY_PITCH_AXIS",
            "JOY_BANK_AXIS", "JOY_REL_THROTTLE_AXIS" }) do
        local binding = io.Keybinding[action]
        local ok, name = pcall(function()
            return binding and binding:getInputName(true) or ""
        end)
        axis_names[#axis_names + 1] = ok and tostring(name or ""):gsub("[|,]", "/") or "?"
    end
    local body = kind .. "|" .. self.state .. "|" .. mission_name()
        .. "|wheel=" .. (self.mode and "1" or "0")
        .. "|" .. tostring(self.telemetry_sequence)
        .. "|preset=" .. tostring(ui and ui.ControlConfig and ui.ControlConfig.getCurrentPreset() or "")
        .. "|axes=" .. (self.axes_applied_state == self.state and "applied" or "pending")
        .. "|binds=" .. table.concat(axis_names, ",")
        .. "|control=" .. tostring(ba.setControlMode())
        .. "|vr=" .. (gr.isVR() and "1" or "0")
        .. "|gauge=" .. (self.vr_gauge and "1" or "0")
        .. "|draws=" .. tostring(self.vr_draws)
        .. "|art=" .. (self.vr_art and tostring(self.vr_art.wheel:getFilename()) or "none")
        .. "|images=" .. tostring(self.vr_image_ok) .. "/" .. tostring(self.vr_image_fail)
    local file = cf.openFile("fotg-wheel-state.txt", "w", "data/players")
    if file and file:isValid() then
        file:write(body)
        file:close()
    end
end

local function read_bridge()
    local file = cf.openFile("fotg-wheel-input.txt", "r", "data/players")
    if not file or not file:isValid() then return nil end
    local ok, body = pcall(function() return file:read("*a") end)
    file:close()
    if not ok or type(body) ~= "string" then return nil end
    local seq, buttons, rx, ry, lx, ly, slot, tap_seq, tap_direction = string.match(body,
        "^(%d+)|(%d+)|([%-]?%d+)|([%-]?%d+)|([%-]?%d+)|([%-]?%d+)|(%d+)|%d+|(%d+)|(%d+)")
    seq, buttons, rx, ry, lx, ly, slot, tap_seq, tap_direction = tonumber(seq), tonumber(buttons),
        tonumber(rx), tonumber(ry), tonumber(lx), tonumber(ly), tonumber(slot),
        tonumber(tap_seq), tonumber(tap_direction)
    if not seq or not buttons or not rx or not ry or not lx or not ly or not slot
        or not tap_seq or not tap_direction then return nil end
    if buttons < 0 or buttons > 65535 or slot > 3 or slot < 0 then return nil end
    if tap_direction ~= 0 and tap_direction ~= D_UP and tap_direction ~= D_DOWN
        and tap_direction ~= D_LEFT and tap_direction ~= D_RIGHT then return nil end
    if math.abs(rx) > 32768 or math.abs(ry) > 32768
        or math.abs(lx) > 32768 or math.abs(ly) > 32768 then return nil end
    return { sequence = seq, buttons = buttons, rx = rx, ry = ry, lx = lx, ly = ly,
        tap_sequence = tap_seq, tap_direction = tap_direction }
end

function W:poll_input(dt)
    local ok, value = pcall(read_bridge)
    if ok and value then
        if value.sequence ~= self.input_sequence then
            self.input_sequence = value.sequence
            self.input_age = 0
        else
            self.input_age = self.input_age + dt
        end
        if self.input_age <= 0.35 then
            self.input = value
            return
        end
    else
        self.input_age = self.input_age + dt
    end
    self.input = { buttons = 0, rx = 0, ry = 0, lx = 0, ly = 0 }
end

function W:notice(message)
    self.message = message
    self.message_age = 2.8
end

function W:queue_action(action)
    self.pending_actions[#self.pending_actions + 1] = action
end

function W:run_action(action)
    local spec = BUTTON_ACTIONS[action]
    if not spec or action == "none" then return end
    if spec.run then spec.run(self)
    elseif spec.native then self:queue_action(spec.native)
    elseif spec.script then
        local ok, message = self:execute(action)
        if not ok then self:notice(message or "Action unavailable") end
    end
end

-- The quick-press action configured for a button, or its 1.0 default.
function W:tap_action(name)
    local config = FotGWheelConfig or {}
    local taps = type(config.tap_bindings) == "table" and config.tap_bindings or {}
    local action = taps[name]
    if action == nil or not BUTTON_ACTIONS[action] then action = TAP_DEFAULTS[name] end
    return action
end

-- A long press can run any one-shot action; held actions have no long press.
local function is_hold_action(action)
    local spec = action and BUTTON_ACTIONS[action]
    return spec ~= nil and action ~= "none" and not spec.held
end

function W:hold_button(name, flag, buttons, previous, dt, base)
    local config = FotGWheelConfig or {}
    local bindings = type(config.hold_bindings) == "table" and config.hold_bindings or {}
    local action = config.hold_enabled and bindings[name] or nil
    if not is_hold_action(action) then
        self.hold_elapsed[name], self.hold_fired[name] = nil, nil
        if down(buttons, flag) and not down(previous, flag) and base then base() end
        return
    end
    local now_down, was_down = down(buttons, flag), down(previous, flag)
    if now_down and not was_down then self.hold_elapsed[name], self.hold_fired[name] = 0, false end
    if now_down then
        self.hold_elapsed[name] = (self.hold_elapsed[name] or 0) + dt
        local threshold = clipped(tonumber(config.hold_ms) or 450, 250, 1000) / 1000
        if self.hold_elapsed[name] >= threshold and not self.hold_fired[name] then
            self.hold_fired[name] = true
            self:run_action(action)
        end
    elseif was_down then
        if not self.hold_fired[name] and base then base() end
        self.hold_elapsed[name], self.hold_fired[name] = nil, nil
    end
end

function W:flush_action()
    -- In ADDITIVE mode, Lua button bits are merged with native button bits.
    -- Clearing here avoids replaying a previous one-shot action next frame.
    local ci = ba.getControlInfo()
    ci:clearLuaButtonInfo()
    for _, action in ipairs(self.pending_actions) do
        -- FSO's control_info function expects the action number as its first
        -- Lua argument; this binding is called with a dot, not a colon.
        ci.useButtonControl(-1, action)
    end
    self.pending_actions = {}
end

function W:fire_third_bank()
    local ship = player_ship()
    if not ship then return end
    local class = string.filterInvisible(ship.Class.Name)
    if class ~= "ASF-01 B-wing" and class ~= "T-Blade Bomber" then return end
    local banks = ship.PrimaryBanks
    if not banks or not banks:isValid() or #banks < 3 then return end
    local selected = nil
    for i = 1, #banks do
        if banks[i].Armed then selected = i; break end
    end
    if selected ~= 3 then banks[3].Armed = true end
    local ok = pcall(function() ship:firePrimary() end)
    if selected and selected ~= 3 then banks[selected].Armed = true
    elseif not selected then banks[3].Armed = false end
    if not ok then self:notice("Third weapon unavailable") end
end

function W:cycle_ship_mode()
    local ship = player_ship()
    if not ship then return end
    local name = ship.Class.Name
    if Falcon_Guns and Falcon_Guns:ValidPly(ship) then
        self:queue_action("CYCLE_NEXT_PRIMARY")
    elseif TIE_Burst and TIE_Burst:ClassValid(name) then
        self:queue_action("CYCLE_PRIMARY_WEAPON_SEQUENCE")
    elseif Sfoils and Sfoils:ShipEquipped(name) then
        self:queue_action("CUSTOM_CONTROL_3")
    elseif ship.SecondaryBanks and ship.SecondaryBanks:isValid()
        and #ship.SecondaryBanks > 0 then
        self:queue_action("CUSTOM_CONTROL_1")
    else
        self:notice("No ship mode available")
    end
end

function W:close_panel(close_native)
    if close_native and self.mode == "commands" and hu.isCommMenuOpen() then
        self:queue_action("SQUADMSG_MENU")
    end
    if self.mode then
        self.mode = nil
        self.page = 1
        self.selected = nil
        self.stick_ready = false
        self.comm_open_requested = false
        self.comm_wait = 0
        self.comm_signature = nil
        self.confirm_depart = nil
        self:publish_state()
    end
end

function W:open_panel(mode)
    if self.mode == "commands" and mode ~= "commands" and hu.isCommMenuOpen() then
        self:queue_action("SQUADMSG_MENU")
    end
    self.mode = mode
    self.page = 1
    self.selected = nil
    self.stick_ready = false
    self.comm_signature = nil
    self.comm_root_signature = nil
    self.comm_submenu = false
    self.confirm_depart = nil
    self.comm_wait = 0
    self.comm_open_requested = false
    if mode == "targeting" then
        -- Reopening the panel highlights the class that X will cycle.
        local wanted = self.target_mode == "all" and "target_all"
            or (self.target_mode == "friend" and "target_friend" or "target_enemy")
        local configured = type(FotGWheelConfig) == "table" and FotGWheelConfig.layouts or nil
        local order = type(configured) == "table" and configured.targeting or nil
        local source = wanted == "target_all" and 1 or (wanted == "target_friend" and 7 or 3)
        if type(order) == "table" and #order == 7 then
            local seen, valid = {}, true
            for i = 1, 7 do
                local n = tonumber(order[i])
                if not n or n < 1 or n > 7 or n ~= math.floor(n) or seen[n] then valid = false; break end
                seen[n] = true
            end
            if valid then for i = 1, 7 do if tonumber(order[i]) == source then self.selected = i; break end end end
        end
        if not self.selected then self.selected = source end
    end
    if mode == "commands" and not hu.isCommMenuOpen() then
        self:queue_action("SQUADMSG_MENU")
        self.comm_open_requested = true
    end
    self:publish_state()
end

local function available_comm_items()
    local count = #mn.CommItems
    local entries = {}
    local title = "COMMANDS"
    for index = 1, count do
        local item = mn.CommItems[index]
        if item and item:isValid() then
            if index == 1 then title = tostring(item:getMenuTitle() or title) end
            entries[#entries + 1] = {
                name = tostring(item.Name or ""), active = item.Active,
                native_index = index
            }
        end
    end
    if ASL and ASL.enabled and ASL.dynamic and not ASL.dynamic.finished then
        entries[#entries + 1] = { name = "Cycle boarding party task", action = "boarding_task", active = true }
    end
    return title, entries
end

local function static_entries(mode)
    if mode == "systems" then
        return "SHIP SYSTEMS", {
            { name = "All energy: shields", action = "shields_max" },
            { name = "All energy: weapons", action = "weapons_max" },
            { name = "All energy: engines", action = "engines_max" },
            { name = "Balance energy", action = "balance_energy" },
            { name = "Equalize shields", action = "shield_equalize" },
            { name = "Match target speed", action = "match_speed" },
            { name = "Transfer to shields", action = "transfer_shields" },
            { name = "Transfer to lasers", action = "transfer_weapons" }
        }
    elseif mode == "targeting" then
        local player = player_ship()
        local target = target_ship(player)
        local has_subsystems = has_targetable_subsystem(target, false)
        local subsystem_on = active_subsystem(player) ~= nil
        return "TARGETING: " .. string.upper(W.target_mode), {
            { name = "All ships", action = "target_all", sector = 1 },
            { name = subsystem_on and "Target hull" or "Target subsystem",
                action = "target_subsystem_toggle", sector = 2,
                active = subsystem_on or has_subsystems },
            { name = "Enemies", action = "target_enemy", sector = 3 },
            { name = "Next live turret", action = "target_live_turret", sector = 4,
                active = has_targetable_subsystem(target, true) },
            { name = "Clear target", action = "target_clear", sector = 5 },
            { name = "Subsystem in sight", action = "target_subsystem_reticle",
                sector = 6, active = has_subsystems },
            { name = "Friendlies", action = "target_friend", sector = 7 }
        }
    elseif mode == "macros" then
        local defaults = {
            { label = "Shields maximum", action = "shields_max" },
            { label = "Nearest enemy", action = "target_nearest_enemy" },
            { label = "Enemy engines", action = "target_enemy_subsystem", argument = "engines" },
            { label = "Balance energy", action = "balance_energy" }
        }
        local cfg = type(FotGWheelConfig) == "table" and FotGWheelConfig.macros or nil
        local entries = {}
        for i = 1, 4 do
            local value = type(cfg) == "table" and cfg[i] or nil
            if type(value) ~= "table" then value = defaults[i] end
            entries[#entries + 1] = {
                name = tostring(value.label or ("Macro " .. i)),
                action = tostring(value.action or ""),
                argument = tostring(value.argument or ""),
                sector = (i - 1) * 2 + 1
            }
        end
        return "MACROS", entries
    end
    return "", {}
end

-- A layout is a permutation of visible choices, never executable profile code.
-- Squad layouts affect the first communications page only; later pages and
-- native follow-on menus retain their engine order.
local function arrange_entries(mode, entries, count)
    local config = FotGWheelConfig or {}
    local layouts = type(config.layouts) == "table" and config.layouts or {}
    local order = type(layouts[mode]) == "table" and layouts[mode] or nil
    local expected = mode == "commands" and 6 or count
    if not order or #order ~= expected then return entries end
    local seen, arranged = {}, {}
    for position = 1, expected do
        local source = tonumber(order[position])
        if not source or source < 1 or source > expected or source ~= math.floor(source) or seen[source] then
            return entries
        end
        seen[source] = true
    end
    local position = 0
    for i = 1, expected do
        local source = tonumber(order[i])
        if source <= count then position = position + 1; arranged[position] = entries[source] end
    end
    for position = count + 1, #entries do arranged[position] = entries[position] end
    for position = 1, count do
        if arranged[position] then
            arranged[position].sector = mode == "macros" and ((position - 1) * 2 + 1) or position
        end
    end
    return arranged
end

function W:view()
    local title, all
    if self.mode == "commands" then
        title, all = available_comm_items()
    else
        title, all = static_entries(self.mode)
    end
    if #all <= SECTORS then
        self.page = 1
        if self.mode == "systems" then all = arrange_entries("systems", all, 8)
        elseif self.mode == "targeting" then all = arrange_entries("targeting", all, 7)
        elseif self.mode == "macros" then all = arrange_entries("macros", all, 4)
        elseif self.mode == "commands" and not self.comm_submenu then all = arrange_entries("commands", all, math.min(6, #all)) end
        for i, entry in ipairs(all) do entry.sector = entry.sector or i end
        return title, all, 1, 1
    end
    local page_size = 6
    local pages = math.ceil(#all / page_size)
    self.page = clipped(self.page, 1, pages)
    local visible = {}
    local first = (self.page - 1) * page_size + 1
    for i = first, math.min(#all, first + page_size - 1) do
        local entry = all[i]
        entry.sector = #visible + 1
        visible[#visible + 1] = entry
    end
    if self.mode == "commands" and not self.comm_submenu and self.page == 1 and #visible == 6 then
        visible = arrange_entries("commands", visible, 6)
    end
    visible[#visible + 1] = {
        sector = 7, name = "Previous page", action = "page_prev",
        active = self.page > 1
    }
    visible[#visible + 1] = {
        sector = 8, name = "Next page", action = "page_next",
        active = self.page < pages
    }
    return title, visible, self.page, pages
end

function W:refresh_comm()
    if self.mode ~= "commands" then return end
    if not hu.isCommMenuOpen() then
        if self.comm_open_requested then
            self.comm_wait = self.comm_wait + ba.getRealFrametime()
            if self.comm_wait > 0.8 then
                self:notice("Communications unavailable")
                self:close_panel(false)
            end
        else
            self:close_panel(false)
        end
        return
    end
    self.comm_open_requested = false
    self.comm_wait = 0
    local title, entries = available_comm_items()
    local parts = { title, tostring(#entries) }
    for _, entry in ipairs(entries) do parts[#parts + 1] = entry.name end
    local signature = table.concat(parts, "\31")
    if not self.comm_root_signature then self.comm_root_signature = signature
    elseif signature ~= self.comm_root_signature then self.comm_submenu = true end
    if self.comm_signature and self.comm_signature ~= signature then
        self.page = 1
        self.selected = nil
        self.stick_ready = false
        self.confirm_depart = nil
    end
    self.comm_signature = signature
end

local subsystem_names = {
    engines = { "engine" },
    weapons = { "weapon", "gun", "laser", "turret" },
    shields = { "shield" },
    navigation = { "navigation", "nav" },
    turrets = { "turret" },
    sensors = { "sensor", "radar" }
}

local function is_targetable_enemy(player, candidate)
    if not candidate or not candidate:isValid() or candidate:isPlayer()
        or candidate:isDying() or candidate:isDepartingWarp()
        or candidate:isArrivingWarp() or candidate.Stealthed
        or candidate.HiddenFromSensors then return false end
    if not player.Team:attacks(candidate.Team) then return false end
    return candidate:checkVisibility(player) == 2
end

local function find_matching_subsystem(candidate, alias)
    local patterns = subsystem_names[alias]
    if not patterns then return nil end
    for sub in candidate:getSubsystemList() do
        if sub and sub:isValid() and sub.Targetable and sub.HitpointsLeft > 0 then
            local name = string.lower(tostring(sub.CanonicalName or ""))
            for _, pattern in ipairs(patterns) do
                if string.find(name, pattern, 1, true) then return sub end
            end
        end
    end
    return nil
end

local function target_enemy_subsystem(alias)
    if ba.MultiplayerMode then return false, "Subsystem macro is single-player only" end
    alias = string.lower(tostring(alias or ""))
    if not subsystem_names[alias] then return false, "Unknown subsystem type" end
    local player = player_ship()
    if not player then return false, "No controllable ship" end
    local best, best_sub, best_distance
    local current = player.Target
    if current and current:isValid() and current:getBreedName() == "Ship"
        and is_targetable_enemy(player, current) then
        local sub = find_matching_subsystem(current, alias)
        if sub then best, best_sub = current, sub end
    end
    if not best then
        for candidate in mn.getShipList() do
            if is_targetable_enemy(player, candidate) then
                local sub = find_matching_subsystem(candidate, alias)
                if sub then
                    local distance = player.Position:getDistanceSquared(candidate.Position)
                    if not best_distance or distance < best_distance then
                        best, best_sub, best_distance = candidate, sub, distance
                    end
                end
            end
        end
    end
    if not best or not best_sub then return false, "No visible enemy " .. alias end
    -- FotG itself sets these properties for scripted targeting in missions-sct.lua.
    player.Target = best
    player.TargetSubsystem = best_sub
    return true, "Targeting " .. alias
end

function W:execute(action, argument)
    local player = player_ship()
    if not player then return false, "No controllable ship" end
    if action == "target_all" or action == "target_enemy" or action == "target_friend" then
        self.target_mode = action == "target_all" and "all"
            or (action == "target_friend" and "friend" or "enemy")
        return true, "X cycles " .. self.target_mode .. " ships"
    end
    if action == "target_subsystem_toggle" then
        if active_subsystem(player) then
            self:queue_action("STOP_TARGETING_SUBSYSTEM")
            return true, "Targeting ship hull"
        end
        if not has_targetable_subsystem(target_ship(player), false) then
            return false, "No targetable subsystem"
        end
        self:queue_action("TARGET_NEXT_SUBOBJECT")
        return true, "Targeting next subsystem"
    end
    if action == "target_live_turret" then
        if not has_targetable_subsystem(target_ship(player), true) then
            return false, "No live turret on target"
        end
        self:queue_action("TARGET_NEXT_LIVE_TURRET")
        return true, "Targeting next live turret"
    end
    if action == "target_subsystem_reticle" then
        if not has_targetable_subsystem(target_ship(player), false) then
            return false, "No targetable subsystem"
        end
        self:queue_action("TARGET_SUBOBJECT_IN_RETICLE")
        return true, "Targeting subsystem in sight"
    end
    if action == "shields_max" or action == "weapons_max"
        or action == "engines_max" or action == "balance_energy" then
        if ba.MultiplayerMode then return false, "Energy macro is single-player only" end
        local indexes = {
            shields_max = { 0, 12, 0 },
            weapons_max = { 0, 0, 12 },
            engines_max = { 12, 0, 0 },
            balance_energy = { 4, 4, 4 }
        }
        local v = indexes[action]
        if not player:EtsSetIndexes(v[1], v[2], v[3]) then
            return false, "Energy transfer unavailable on this ship"
        end
        return true, "Energy updated"
    elseif action == "target_enemy_subsystem" then
        return target_enemy_subsystem(argument)
    end
    local native = {
        target_clear = "STOP_TARGETING_SHIP",
        target_nearest_enemy = "TARGET_NEXT_CLOSEST_HOSTILE",
        match_speed = "MATCH_TARGET_SPEED",
        shield_equalize = "SHIELD_EQUALIZE",
        transfer_shields = "XFER_SHIELD",
        transfer_weapons = "XFER_LASER",
        boarding_task = "MULTI_MESSAGE_TARGET"
    }
    if not native[action] then return false, "Macro action unavailable" end
    if action == "boarding_task" and (not ASL or not ASL.enabled or not ASL.dynamic or ASL.dynamic.finished) then
        return false, "Boarding parties unavailable"
    end
    if action == "match_speed" then
        local target = player.Target
        if not target or not target:isValid() then return false, "Target a ship first" end
    end
    self:queue_action(native[action])
    return true, "Action requested"
end

function W:confirm()
    local _, entries = self:view()
    local chosen
    for _, entry in ipairs(entries) do
        if entry.sector == self.selected then chosen = entry; break end
    end
    if not chosen then return end
    if chosen.active == false then
        self:notice("Unavailable now")
        return
    end
    if chosen.action == "page_next" or chosen.action == "page_prev" then
        self.page = self.page + (chosen.action == "page_next" and 1 or -1)
        self.selected = nil
        self.stick_ready = false
        self.confirm_depart = nil
        return
    end
    if self.mode == "commands" then
        if chosen.action == "boarding_task" then
            if not ASL or not ASL.enabled or not ASL.dynamic or ASL.dynamic.finished then
                self:notice("Boarding parties unavailable"); return
            end
            self:queue_action("MULTI_MESSAGE_TARGET")
            self:close_panel(true)
            return
        end
        if not hu.isCommMenuOpen() or not chosen.native_index then return end
        local item = mn.CommItems[chosen.native_index]
        if not item or not item:isValid() or not item.Active
            or tostring(item.Name) ~= chosen.name then
            self:notice("Order changed; select again")
            self.selected = nil
            return
        end
        local dangerous = string.find(string.lower(chosen.name), "depart", 1, true)
            or string.find(string.lower(chosen.name), "warp", 1, true)
        if dangerous and self.confirm_depart ~= chosen.native_index then
            self.confirm_depart = chosen.native_index
            self:notice("Press A again to confirm " .. chosen.name)
            return
        end
        self.confirm_depart = nil
        local ok, selected = pcall(function() return item:selectItem() end)
        if not ok or not selected then self:notice("Order unavailable"); return end
        self.selected = nil
        self.stick_ready = false
        return
    end
    local ok, success, message = pcall(function()
        return self:execute(chosen.action, chosen.argument)
    end)
    if not ok then self:notice("Action failed"); return end
    self:notice(message or (success and "Action requested" or "Unavailable"))
    if success then self:close_panel(false) end
end

local function sector_from_stick(rx, ry)
    local x, y = rx / 32768, ry / 32768
    local magnitude = math.sqrt(x * x + y * y)
    if magnitude < 0.26 then return nil, magnitude end
    if magnitude < 0.52 then return 0, magnitude end
    local angle
    if y == 0 then
        angle = x >= 0 and PI / 2 or 3 * PI / 2
    elseif y > 0 then
        angle = math.atan(x / y)
    else
        angle = math.atan(x / y) + PI
    end
    if angle < 0 then angle = angle + PI * 2 end
    return math.floor((angle + PI / 8) / (PI / 4)) % 8 + 1, magnitude
end

function W:frame()
    local dt = clipped(ba.getRealFrametime() or 0.016, 0, 0.2)
    if gr.isVR() then
        self.vr_debug_elapsed = self.vr_debug_elapsed + dt
        if self.vr_debug_elapsed >= 1 then
            self.vr_debug_elapsed = 0
            self:publish_state()
        end
    end
    if self.message_age > 0 then self.message_age = self.message_age - dt end
    if not is_flight_state(self.state) then
        self:close_panel(true)
        self.prior_buttons = 0
        self:flush_action()
        return
    end
    self:poll_input(dt)
    if self.input_age > 0.35 then self:close_panel(true) end
    self:refresh_comm()
    local buttons = self.input.buttons or 0
    local previous = self.prior_buttons
    local function pressed(flag)
        return down(buttons, flag) and not down(previous, flag)
    end
    local wheel_was_open = self.mode ~= nil
    local changed_category = false
    local tap_direction = 0
    if self.input.tap_sequence then
        if self.last_tap_sequence ~= nil and self.last_tap_sequence ~= self.input.tap_sequence then
            tap_direction = self.input.tap_direction or 0
        end
        self.last_tap_sequence = self.input.tap_sequence
    end
    local category = tap_direction == D_DOWN and "commands"
        or tap_direction == D_UP and "systems"
        or tap_direction == D_LEFT and "targeting"
        or tap_direction == D_RIGHT and "macros"
    if category and player_ship() then
        changed_category = true
        if self.mode == category then self:close_panel(true)
        else self:open_panel(category) end
    end
    if self.mode and not changed_category then
        local sector = sector_from_stick(self.input.rx, self.input.ry)
        if sector == nil then
            self.stick_ready = true
        elseif sector ~= 0 and self.stick_ready then
            self.selected = sector
        end
        if pressed(B_B) then
            if self.confirm_depart then self.confirm_depart = nil
            else self:close_panel(true) end
        elseif pressed(B_A) then
            self:confirm()
        end
    end
    if not self.mode and not wheel_was_open then
        for _, entry in ipairs(TAP_BUTTONS) do
            local name, flag = entry[1], entry[2]
            local action = self:tap_action(name)
            if BUTTON_ACTIONS[action].held then
                -- Held actions repeat every frame and never wait for a long press.
                self.hold_elapsed[name], self.hold_fired[name] = nil, nil
                if down(buttons, flag) then self:run_action(action) end
            else
                self:hold_button(name, flag, buttons, previous, dt,
                    action ~= "none" and function() self:run_action(action) end or nil)
            end
        end
    else
        self.hold_elapsed, self.hold_fired = {}, {}
        -- A wheel confirmation or cancel must not become a delayed short
        -- press when that same button is released after the wheel closes.
        for name, flag in pairs(HOLD_FLAGS) do
            if down(buttons, flag) then self.hold_fired[name] = true end
        end
    end
    self.prior_buttons = buttons
    self:flush_action()
end

local function short_label(value)
    value = tostring(value or "")
    if #value > 20 then return string.sub(value, 1, 19) .. "..." end
    return value
end

function W:draw()
    if gr.isVR() then return end
    local previous_font = gr.CurrentFont
    local config = FotGWheelConfig or {}
    local font_name = (config.font_bold == false and "font_" or "font_bold_")
        .. (config.font_size == "large" and "large" or "medium")
    gr.CurrentFont = gr.Fonts[font_name]
    if not self.mode then
        if self.message and self.message_age > 0 then
            local mx = math.floor(gr.getScreenWidth() * 0.5 - gr.getStringWidth(self.message) * 0.5)
            local my = math.floor(gr.getScreenHeight() * 0.72)
            gr.setColor(3, 12, 22, 244)
            gr.drawRectangle(mx - 14, my - 9, mx + gr.getStringWidth(self.message) + 14,
                my + gr.CurrentFont.Height + 9, true)
            gr.setColor(255, 219, 123, 255)
            gr.drawString(self.message, mx, my)
        end
        gr.CurrentFont = previous_font
        return
    end
    local title, entries, page, pages = self:view()
    local width, height = gr.getScreenWidth(), gr.getScreenHeight()
    local scale = clipped(height / 1080, 0.72, 1.3)
    local cx, cy = math.floor(width * 0.5), math.floor(height * 0.50)
    local radius = math.floor((config.font_size == "large" and 185 or 166) * scale)
    -- The labels need their own backing: the former dark center left them over
    -- explosions, laser bloom and the bright parts of the HUD.
    gr.setColor(2, 10, 19, 236)
    gr.drawCircle(math.floor(radius * 1.19), cx, cy, true)
    gr.setColor(4, 18, 30, 250)
    gr.drawCircle(math.floor(radius * 0.73), cx, cy, true)
    gr.setColor(58, 221, 255, 255)
    gr.drawCircle(radius, cx, cy, false)
    gr.drawCircle(math.floor(radius * 0.73), cx, cy, false)
    for sector = 1, SECTORS do
        local theta = -PI / 2 + (sector - 1) * PI / 4
        local x1 = cx + math.cos(theta) * radius * 0.72
        local y1 = cy + math.sin(theta) * radius * 0.72
        local x2 = cx + math.cos(theta) * radius
        local y2 = cy + math.sin(theta) * radius
        gr.setColor(87, 172, 207, 245)
        gr.drawLine(math.floor(x1), math.floor(y1), math.floor(x2), math.floor(y2))
    end
    for _, entry in ipairs(entries) do
        local theta = -PI / 2 + (entry.sector - 1) * PI / 4
        local x = cx + math.cos(theta) * radius * 1.10
        local y = cy + math.sin(theta) * radius * 1.10
        local label = short_label(entry.name)
        local selected = self.selected == entry.sector
        local label_width = gr.getStringWidth(label)
        local label_height = gr.CurrentFont.Height
        local offset = math.cos(theta) > 0.3 and 8 or
            (math.cos(theta) < -0.3 and -label_width - 8 or -label_width * 0.5)
        local tx, ty = math.floor(x + offset), math.floor(y - label_height * 0.5)
        gr.setColor(2, 9, 17, 249)
        gr.drawRectangle(tx - 9, ty - 5, tx + label_width + 9, ty + label_height + 5, true)
        if selected then
            gr.setColor(255, 203, 82, 255)
            gr.drawRectangle(tx - 9, ty - 5, tx + label_width + 9, ty + label_height + 5, false)
        end
        if entry.active == false then gr.setColor(139, 158, 168, 255)
        elseif selected then gr.setColor(255, 225, 145, 255)
        else gr.setColor(242, 249, 252, 255) end
        gr.drawString(label, tx, ty)
    end
    gr.setColor(255, 196, 89, 255)
    gr.drawString(title, cx - math.floor(gr.getStringWidth(title) * 0.5), cy - math.floor(30 * scale))
    local detail = self.comm_open_requested and "Opening communications..."
        or (pages > 1 and ("Page " .. page .. "/" .. pages) or "Right stick: choose")
    gr.setColor(223, 243, 250, 255)
    gr.drawString(detail, cx - math.floor(gr.getStringWidth(detail) * 0.5), cy + math.floor(2 * scale))
    local hint = "A  Confirm    B  Cancel"
    gr.drawString(hint, cx - math.floor(gr.getStringWidth(hint) * 0.5), cy + math.floor(29 * scale))
    if self.message and self.message_age > 0 then
        local mw = gr.getStringWidth(self.message)
        local mx = cx - math.floor(mw * 0.5)
        local my = cy + radius + math.floor(76 * scale)
        gr.setColor(2, 9, 17, 249)
        gr.drawRectangle(mx - 10, my - 5, mx + mw + 10, my + gr.CurrentFont.Height + 5, true)
        gr.setColor(255, 196, 89, 255)
        gr.drawString(self.message, mx, my)
    end
    gr.CurrentFont = previous_font
end

-- A scripted HUD gauge is rendered by FSO in each eye's HUD space. Direct gr
-- drawing at fixed screen pixels produces doubled, blurry text in OpenXR.
function W:draw_vr(draw, layer)
    if not gr.isVR() or not self.vr_gauge then return end
    if layer == "text" then self.vr_draws = self.vr_draws + 1 end
    local previous_font = gr.CurrentFont
    gr.CurrentFont = gr.Fonts["font_bold_medium"]
    -- HUD draw primitives ignore Lua color state. Precolored textures keep
    -- the backdrop and selection plates legible in both OpenXR eyes.
    if not self.mode then
        if self.message and self.message_age > 0 then
            local width = gr.getStringWidth(self.message)
            local height = gr.CurrentFont.Height
            if layer == "back" and self.vr_art then
                local plate_width = self:vr_plate_width(width + 24)
                draw:drawImage(self.vr_art.labels[plate_width], -plate_width / 2, 190)
            elseif layer == "text" then
                draw:drawString(self.message, -width / 2, 196)
            end
        end
        gr.CurrentFont = previous_font
        return
    end
    local title, entries, page, pages = self:view()
    local radius = 220
    local function centered(message, y)
        draw:drawString(message, -math.floor(gr.getStringWidth(message) * 0.5), y)
    end
    if layer == "back" and self.vr_art then
        if draw:drawImage(self.vr_art.wheel, -224, -224) then
            self.vr_image_ok = self.vr_image_ok + 1
        else
            self.vr_image_fail = self.vr_image_fail + 1
        end
    end
    for _, entry in ipairs(entries) do
        local theta = -PI / 2 + (entry.sector - 1) * PI / 4
        local x = math.cos(theta) * radius * 1.25
        local y = math.sin(theta) * radius * 1.25
        local label = short_label(entry.name)
        local width, height = gr.getStringWidth(label), gr.CurrentFont.Height
        local offset = math.cos(theta) > 0.3 and 8 or
            (math.cos(theta) < -0.3 and -width - 8 or -width * 0.5)
        local tx, ty = math.floor(x + offset), math.floor(y - height * 0.5)
        if layer == "back" and self.vr_art then
            local plate_width = self:vr_plate_width(width + 24)
            local plates = self.selected == entry.sector and self.vr_art.selected or self.vr_art.labels
            draw:drawImage(plates[plate_width],
                tx + math.floor((width - plate_width) * 0.5),
                ty + math.floor((height - 48) * 0.5))
        elseif layer == "text" then
            draw:drawString(label, tx, ty)
        end
    end
    if layer == "text" then
        centered(title, -28)
        centered(self.comm_open_requested and "Opening communications..."
            or (pages > 1 and ("Page " .. page .. "/" .. pages) or "Right stick: choose"), 4)
        centered("A  Confirm    B  Cancel", 32)
    end
    gr.CurrentFont = previous_font
end

function W:vr_plate_width(width)
    for _, candidate in ipairs({ 128, 192, 256, 320, 384, 448, 512, 640 }) do
        if width <= candidate then return candidate end
    end
    return 640
end

function W:ensure_vr_art()
    if self.vr_art then return end
    local art = { labels = {}, selected = {} }
    art.wheel = gr.loadTexture("fotg_vr_wheel")
    for _, width in ipairs({ 128, 192, 256, 320, 384, 448, 512, 640 }) do
        art.labels[width] = gr.loadTexture("fotg_vr_label_" .. width)
        art.selected[width] = gr.loadTexture("fotg_vr_selected_" .. width)
    end
    self.vr_art = art
end

function W:ensure_vr_gauge()
    if not gr.isVR() or self.vr_gauge then return end
    -- This gauge is attached to the active ship HUD; FSO can look it up by
    -- Name: there. A default scripted gauge would not render for FotG ships.
    local gauges = {}
    for _, layer in ipairs({ "back", "lines", "text" }) do
        local gauge = hu.getHUDGaugeHandle("FotG VR " .. layer)
        if not gauge then return end
        gauges[layer] = gauge
    end
    self.vr_gauge = gauges
    self:ensure_vr_art()
    for layer, gauge in pairs(gauges) do
        local this_layer = layer
        gauge.RenderFunction = function(draw) W:draw_vr(draw, this_layer) end
    end
end

function W:mission_start()
    self.state = "GS_STATE_GAME_PLAY"
    ba.setButtonControlMode(BUTTON_MODE_ADDITIVE)
    self:close_panel(false)
    self.prior_buttons = 0
    self.input_sequence = nil
    self.last_tap_sequence = nil
    self.hold_elapsed, self.hold_fired = {}, {}
    self.input_age = 10
    self.vr_gauge = nil
    self:ensure_vr_gauge()
    self:publish_state()
end

function W:mission_end()
    self:close_panel(false)
    self.pending_actions = {}
    self.vr_gauge = nil
    -- FotG's stock scripts do not set button-control mode; restore the engine
    -- default when a mission ends so the addon has no menu-side control state.
    ba.setButtonControlMode(BUTTON_MODE_NORMAL)
    self.state = "GS_STATE_MAIN_MENU"
    self:publish_state()
end

if not IS_STANDALONE then
    -- The bridge reads the stick for selection. Suppress flight actions while a
    -- wheel is visible, even if the pilot also bound that physical stick here.
    for _, action in ipairs({ "JOY_HEADING_AXIS", "JOY_PITCH_AXIS", "JOY_BANK_AXIS" }) do
        local binding = io.Keybinding[action]
        if binding then
            binding:registerHook(function() return W.mode ~= nil end, true, true)
        end
    end
    -- A is reserved by the wheel profile, but catch a leftover primary-fire
    -- binding as a second line of defense while a panel is open.
    local primary = io.Keybinding["FIRE_PRIMARY"]
    if primary then
        primary:registerHook(function() return W.mode ~= nil end, true, true)
    end
    engine.addHook("On State Start", function()
        W.state = tostring(hv.NewState)
        W:publish_state()
    end)
    engine.addHook("On Frame", function() W:ensure_flight_axes() end,
        { State = "GS_STATE_MAIN_MENU" })
    engine.addHook("On Gameplay Start", function() W:mission_start() end)
    engine.addHook("On Mission About To End", function() W:mission_end() end)
    engine.addHook("On Frame", function()
        if not gr.isVR() then W:ensure_flight_axes(); W:frame() end
    end,
        { State = "GS_STATE_GAME_PLAY" })
    -- In OpenXR, On Frame runs once per eye. Input and timers must run once per
    -- simulation step or held actions can fire twice.
    engine.addHook("On Simulation", function()
        if gr.isVR() then W:ensure_vr_gauge(); W:ensure_flight_axes(); W:frame() end
    end,
        { State = "GS_STATE_GAME_PLAY" })
    engine.addHook("On HUD Draw", function() W:draw() end,
        { State = "GS_STATE_GAME_PLAY" })
end
