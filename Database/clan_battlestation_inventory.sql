CREATE TABLE IF NOT EXISTS `clan_battlestation_inventory` (
    `clan_id` INT NOT NULL,
    `item_id` INT NOT NULL,
    `module_type` SMALLINT NOT NULL,
    `upgrade_level` INT NOT NULL DEFAULT 0,
    `in_use` TINYINT(1) NOT NULL DEFAULT 0,
    `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`clan_id`, `item_id`),
    CONSTRAINT `fk_clan_battlestation_inventory_clan`
        FOREIGN KEY (`clan_id`) REFERENCES `server_clans` (`id`)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;