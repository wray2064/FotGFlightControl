-- Editable four-slot macro preset for the Fate of the Galaxy Gamepad Wheel.
-- The Hangar Bay Console exports this file after validating action types.
-- The runtime does not execute label or argument text as Lua code.
FotGWheelConfig = {
    camera_hold_enabled = true,
    hold_enabled = false,
    hold_ms = 450,
    hold_bindings = { A="none", B="none", X="none", LB="none", RB="none", L3="none", R3="none" },
    flight_axes = "classic", -- classic: tested pitch; classic_inverted: reverse pitch; official: saved game bindings
    look_hold_ms = 250, -- 150-600 ms; a shorter D-pad press opens a wheel
    look_enabled = true, -- false: D-pad opens wheels immediately and never moves the view
    font_size = "medium", -- medium (20px) or large (30px)
    font_bold = true,
    macros = {
        { label = "Shields maximum", action = "shields_max", argument = "" },
        { label = "Nearest enemy", action = "target_nearest_enemy", argument = "" },
        { label = "Enemy engines", action = "target_enemy_subsystem", argument = "engines" },
        { label = "Balance energy", action = "balance_energy", argument = "" }
    }
}
