# NPC ore cargo — wiki snapshot 2026-11-03

`server_ships.ores` is the source of NPC cargo. The 35 canonical tables and 37 mapped ship IDs are documented, with a source URL per row, in `config/reference/npc-cargo-wiki-20261103.json`. `tools/database/20261103_npc_official_cargo.sql` updates only these ore fields; it does not alter ship stats, kill rewards, player ships or assembly/booty chances.

The local database was updated after a row/name check and a backup of all affected values. Rollback SQL and JSON snapshots are in `H:/Darkorbit-Cpp/build/npc-cargo-before-20261103T231328Z.*`. Database data and the matching server binary must be deployed together. The online host has not been updated.

## Runtime

- `QueryManager.LoadShips()` loads the JSON into `Ship.Drops`.
- NPC destruction snapshots those values and the NPC instance's cargo multiplier into `CargoDrop`. No box is created for empty cargo. Position and contents remain independent of NPC respawn and shared ship definitions.
- Galaxy-gate instance multipliers are retained; invasion uses its existing configured reward multiplier. Kucurbium/Boss Kucurbium base database amounts use the wiki X-1 invasion tables, with X-3/X-5 tables noted in the manifest. No event balancing multipliers were replaced.
- Spaceball keeps its separate existing reward path.
- Ore grants respect available cargo and prefer rarer ores. Remaining contents survive a partial pickup and retain the normal expiry timer. An already collected quantity cannot be granted again.
- Tractor Beam I increases cargo once per box. Resource boosters are not applied to NPC cargo, as described by the wiki Cargo page. Per-resource collection messages/objective progress use the actual amount granted. Cargo state is synchronized and persisted once per pickup batch.

Rule references:
- https://darkorbit.fandom.com/wiki/Cargo
- https://darkorbit.fandom.com/wiki/Tractor_Beam

## Unverified/custom NPCs

No canonical ore table was found for local Boss/Uber Cubikon, Boss Protegit, Aberration variants or the special SaNeJiEwZ. Their existing database values are preserved rather than extrapolating a multiplier and labelling it official. The manifest lists these IDs. Missing/zero values no longer award the old 1/2/3/... test resources.

## Validation

`Tests/Validate-NpcCargo.ps1` checks all 35 canonical tables against the runtime transfer implementation, repeated pickup, immutable contents, full/partial cargo, rarity priority, gate multiplier and single-use cargo skill. The seed was also read back from the database for all 37 IDs. Release and Debug builds pass.
