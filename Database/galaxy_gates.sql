CREATE TABLE IF NOT EXISTS server_galaxy_gate_templates (
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(64) NOT NULL,
    entry_map_id INT NOT NULL,
    visual_map_id INT NOT NULL DEFAULT 0,
    entry_x INT NOT NULL DEFAULT 11100,
    entry_y INT NOT NULL DEFAULT 6500,
    entry_map_id_mmo INT NOT NULL DEFAULT 0,
    entry_x_mmo INT NOT NULL DEFAULT 0,
    entry_y_mmo INT NOT NULL DEFAULT 0,
    entry_map_id_eic INT NOT NULL DEFAULT 0,
    entry_x_eic INT NOT NULL DEFAULT 0,
    entry_y_eic INT NOT NULL DEFAULT 0,
    entry_map_id_vru INT NOT NULL DEFAULT 0,
    entry_x_vru INT NOT NULL DEFAULT 0,
    entry_y_vru INT NOT NULL DEFAULT 0,
    entry_graphic_id INT NOT NULL DEFAULT 41,
    base_map_id INT NOT NULL DEFAULT 1,
    base_x INT NOT NULL DEFAULT 1600,
    base_y INT NOT NULL DEFAULT 1600,
    wave_portal_graphic_id INT NOT NULL DEFAULT 41,
    exit_portal_graphic_id INT NOT NULL DEFAULT 41,
    center_x INT NOT NULL DEFAULT 11100,
    center_y INT NOT NULL DEFAULT 6500,
    center_x_mmo INT NOT NULL DEFAULT 11100,
    center_y_mmo INT NOT NULL DEFAULT 6500,
    center_x_eic INT NOT NULL DEFAULT 11100,
    center_y_eic INT NOT NULL DEFAULT 6500,
    center_x_vru INT NOT NULL DEFAULT 11100,
    center_y_vru INT NOT NULL DEFAULT 6500,
    npc_suffix VARCHAR(32) NOT NULL DEFAULT 'GG',
    max_lives INT NOT NULL DEFAULT 5
);

CREATE TABLE IF NOT EXISTS server_galaxy_gate_waves (
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    gate_id INT NOT NULL,
    wave_id INT NOT NULL,
    npc_id INT NOT NULL,
    npc_count INT NOT NULL,
    multiplier INT NOT NULL DEFAULT 1,
    key_npc INT NOT NULL DEFAULT 0,
    minions_id INT NOT NULL DEFAULT 0,
    minions_count INT NOT NULL DEFAULT 0,
    minions_multiplier INT NOT NULL DEFAULT 1,
    UNIQUE KEY uq_gate_wave (gate_id, wave_id)
);

CREATE TABLE IF NOT EXISTS player_galaxy_gate_instances (
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    player_id INT NOT NULL,
    owner_faction_id INT NOT NULL DEFAULT 0,
    template_id INT NOT NULL,
    map_id INT NOT NULL,
    current_wave INT NOT NULL DEFAULT 1,
    lives_left INT NOT NULL DEFAULT 5,
    is_completed TINYINT(1) NOT NULL DEFAULT 0,
    is_failed TINYINT(1) NOT NULL DEFAULT 0,
    destroyed_npcs_json LONGTEXT NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_player_template (player_id, template_id)
);

-- Exemplo de gate:
INSERT INTO server_galaxy_gate_templates
    (name, entry_map_id, visual_map_id, entry_x, entry_y, entry_map_id_mmo, entry_x_mmo, entry_y_mmo, entry_map_id_eic, entry_x_eic, entry_y_eic, entry_map_id_vru, entry_x_vru, entry_y_vru, entry_graphic_id, base_map_id, base_x, base_y, wave_portal_graphic_id, exit_portal_graphic_id, center_x, center_y, center_x_mmo, center_y_mmo, center_x_eic, center_y_eic, center_x_vru, center_y_vru, npc_suffix, max_lives)
VALUES
    ('ALPHA', 1, 51, 11100, 6500, 1, 11100, 6500, 5, 11100, 6500, 9, 11100, 6500, 41, 1, 1600, 1600, 42, 43, 11100, 6500, 11100, 6500, 11100, 6500, 11100, 6500, 'ALPHA', 5);

INSERT INTO server_galaxy_gate_waves
    (gate_id, wave_id, npc_id, npc_count, multiplier, key_npc, minions_id, minions_count, minions_multiplier)
VALUES
    (1, 1, 21, 8, 1, 0, 0, 0, 1),
    (1, 2, 22, 12, 1, 0, 0, 0, 1),
    (1, 3, 23, 8, 2, 1, 21, 4, 1);
