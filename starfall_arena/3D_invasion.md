# 3D_invasion.md

This document owns the 3D Invasion mode notes.

Invasion is a 3D PvE mode where two player ships fight finite waves of alien enemies. It is separate from the current 3D duel flow and should live in the `3d_invasion` scene path unless a task explicitly says otherwise.

## Current First Slice

Implemented foundation:

- `FactionMember3D` gives 3D combat objects an explicit team identity: `Neutral`, `PlayerTeam`, or `EnemyTeam`
- 3D projectiles now carry a target faction in addition to the older target tag
- faction checks are preferred for PvE projectile damage, with tags retained only as a compatibility fallback
- `Enemy3D` remains a narrow coordinator over focused enemy systems
- `EnemyAIFlightController3D` is a simple enemy Rigidbody motor: rotate toward a world-space direction, then move forward
- `EnemyTargetSensor3D` selects the nearest visible player-team entity
- `EnemyObstacleAvoidance3D` uses non-alloc 3D physics probes to steer around asteroids/world obstacles
- `BasicShooterEnemyBrain3D` chases the nearest visible player, slows near the target to avoid orbiting, and fires at `PlayerTeam` when aimed and off cooldown
- `ArtilleryBeamEnemyBrain3D` holds a longer standoff range, kites backward when pressured, and sustains a faction-targeted beam only while it still has line-of-sight and aim on a player-team target
- `SuicideDroneEnemyBrain3D` is a dedicated kamikaze brain that always drives at the nearest player at full speed and detonates on contact/proximity using server-authoritative direct damage
- `TankEnemyBrain3D` is a slow, high-HP heavy that advances to a wide hold-band, then sits and pressures the player with two independent weapons: a slow heavy cannon and a homing missile launcher; it reuses `ProjectileWeapon3D` for both slots and `MissileProjectile3D` as the missile prefab
- `RammerEnemyBrain3D` is a fast strike enemy that chases the player at full speed and slams into them on contact for chip damage plus a large knockback, then arcs away to circle back; the knockback routes through the existing `NetMovement3D.ApplyCombatVelocityDelta` recoil hook so the impulse replicates correctly across the network without a new RPC
- `NetEnemyMovement3D` makes enemies server-simulated in network sessions
- `NetEnemyCombat3D` makes enemy projectile damage server-authoritative and broadcasts client cosmetics
- enemy beam prefabs may now use an optional `BeamVisualDriver3D` such as `ForgeBeamVisualDriver3D` for presentation, but enemy beam gameplay still stays inside `LaserBeam3D` / `NetEnemyCombat3D`
- `InvasionWaveManager3D` is a minimal finite-wave spawner for configured enemy prefabs

## Title Screen Entry

- the title-screen 3D host flow now branches through a dedicated 3D sub-select canvas before matchmaking starts
- the duel branch still loads `3d`, while the invasion branch loads `3d_invasion`
- invasion still uses the 3D ship roster; the title flow should not fall back to the 2D roster just because the scene token differs from the duel scene

## Mode Ownership

Networked Invasion is server authoritative:

- the server/host spawns enemies and waves
- the server runs enemy AI and owns enemy Rigidbody movement
- the server spawns gameplay projectiles and applies damage
- clients interpolate enemy movement and show cosmetic projectile spawns
- client-side enemy copies must not run AI or apply gameplay damage

Do not reuse `NetMovement3D` for enemies. It is player-owner prediction/reconciliation code.

Do not reuse `NetCombat3D.ResolveEnemyTag()` for PvE. That helper is duel-shaped and resolves the opposite player slot.

## Friendly Fire Policy

The current Invasion default is no ally damage:

- player projectiles should not damage player-team entities
- enemy projectiles should not damage enemy-team entities
- enemy projectiles target `PlayerTeam`
- player projectiles target `EnemyTeam` or use the existing `"Enemy"` tag fallback until prefabs are explicitly faction-wired

Unity tags do not carry enough meaning for PvE teams. New Invasion work should use `FactionMember3D` for gameplay filtering and keep tags only for compatibility/readability.

Important tag pitfall:

- the project currently has `Player1`, `Player2`, and `Enemy`
- it does not have a generic `"Player"` tag in `ProjectSettings/TagManager.asset`
- enemy targeting or projectile code must not look for `"Player"` in the 3D path

## Wave Direction

The first wave manager supports finite configured waves. Later additions should build on that shape:

- wave entries should spawn enemy prefab counts from authored spawn points
- boss or elite waves should be represented as wave entries, not a separate scene-flow fork
- scoring, rewards, difficulty scaling, revive rules, and objective variants are planned work

Keep menu/mode-entry integration separate from this foundation unless the task specifically asks for it.

## Editor Wiring

For the first basic shooter enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- `ShipFlight3D` may still exist because `Enemy3D` inherits shared `Entity3D`, but `Enemy3D` disables it at runtime so it does not fight enemy movement
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- add `EnemyObstacleAvoidance3D` and set `Obstacle Mask` to asteroids/world-geometry layers
- add `BasicShooterEnemyBrain3D`
- add `ProjectileWeapon3D`; enemy shooter weapons should use `MuzzleForward` aiming so the brain's facing direction controls shots
- optional `ShipThrusterVfx3D` can stay on enemies; it reads `EnemyAIFlightController3D.IsMovingForward` when present
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked enemies
- set the root tag to `Enemy` for compatibility, but do not rely on that tag for new PvE damage rules

For a suicide drone enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- optionally add `EnemyObstacleAvoidance3D` if the drone should weave around asteroids instead of beelining
- add `SuicideDroneEnemyBrain3D`
- add `NetEnemyMovement3D` for networked movement replication
- do not add `ProjectileWeapon3D` or `NetEnemyCombat3D` unless the drone also has a separate ranged attack
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven

For an artillery beam enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- optionally add `EnemyObstacleAvoidance3D` if the artillery ship should path around asteroids while closing distance
- add `ArtilleryBeamEnemyBrain3D`
- add `BeamWeapon3D`; set its `targetFaction` to `PlayerTeam` and keep `targetTag` empty unless you intentionally need legacy fallback
- if you want the Forge line-renderer look, use `Assets/Prefabs/Weapons/Projectiles/3d/projectiles/enemies/beam/enemy_beam_forge_red.prefab` as the beam prefab instead of the older cylinder-based enemy beam
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus beam cosmetic replication
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven

For a tank enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` (gravity off; tune drag for the slow lumbering feel)
- add `EnemyAIFlightController3D`; tune `moveSpeed` low and `rotationDegreesPerSecond` low so the tank visibly turns slowly and players can strafe around it
- add `EnemyTargetSensor3D`
- optionally add `EnemyObstacleAvoidance3D` if the tank should weave around asteroids while advancing
- add **two** `ProjectileWeapon3D` components on the same root: one configured as the slow heavy cannon (slow projectile speed, high damage, short-to-medium cooldown), one configured as the homing missile launcher (longer cooldown, projectile prefab is a `MissileProjectile3D` variant). Set both weapons' `targetFaction` to `PlayerTeam` and use `MuzzleForward` aiming. Wire each weapon's muzzle Transform to its own visible barrel/launcher on the model.
- add `TankEnemyBrain3D` and assign both weapon references in its inspector (cannon slot and missile slot are explicitly serialized, since `GetComponent<ProjectileWeapon3D>()` would only resolve the first one)
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated fire
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set a high `maxHealth` on `Entity3D` to lean into the tank identity

For a rammer enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` (gravity off, low drag for fast pursuit feel)
- add `EnemyAIFlightController3D`; tune `moveSpeed` high and `rotationDegreesPerSecond` high so it can actually arc back around for another pass after a hit
- add `EnemyTargetSensor3D`
- optionally add `EnemyObstacleAvoidance3D` if the rammer should weave around asteroids while charging
- add `RammerEnemyBrain3D`; tune `knockbackVelocity` first since that single value drives the whole feel of the enemy
- add `NetEnemyMovement3D` for networked movement replication; `NetEnemyCombat3D` is **not** required since the rammer has no projectile weapons (and the knockback hook lives on the player's `NetMovement3D`, not on the enemy)
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set moderate `maxHealth` on `Entity3D` (lower than the tank - this is a fast strike unit, not a bruiser)

For player prefabs used in Invasion:

- add `FactionMember3D`
- set faction to `PlayerTeam`
- set player projectile weapon configs that should damage enemies to `targetFaction = EnemyTeam`
- keep existing `Player1` / `Player2` tags for slot compatibility

For `3d_invasion`:

- add `InvasionWaveManager3D`
- assign spawn points
- add at least one wave entry using the basic shooter enemy prefab
- ensure networked enemy prefabs are registered with NGO before network spawning
