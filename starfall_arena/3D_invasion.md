# 3D_invasion.md

This document owns the 3D Invasion mode notes.

Invasion is a 3D PvE mode where two player ships fight finite waves of alien enemies. It is separate from the current 3D duel flow and should live in the `3d_invasion` scene path unless a task explicitly says otherwise.

## Mode Flow

Networked 3D Invasion is planned as a two-player cooperative PvE mode with roughly five finite waves for the current target. The enemy roster comes from the enemies documented in `3D_AI.md`; wave composition is authored on `InvasionWaveManager3D`.

Current implemented beginning flow:

- players enter from the normal title-screen network flow, complete 3D ship select, and load into `3d_invasion`
- `InvasionSceneManager3D` spawns the two selected player ships once, binds the gameplay HUD, resets/starts `ArenaBoundary3D`, and starts the wave manager
- there is no versus/collaboration intro canvas in this slice
- the existing round text canvas is reused only for `WAVE 1`, `WAVE 2`, etc.
- countdown UI is not used
- player HUD elements stay active during gameplay: health, vignette, crosshair, weapon container, ability container, FPS/ping, enemy tracker, optional enemy counter, and optional heart/life counter
- win trackers, round-end screens, game-end screens, and end-of-wave stat summaries are not used
- players are not repositioned, despawned between waves, or movement-locked by the Invasion scene manager
- game-end, wipe, revive, score, reward, and completion presentation are planned later; when the final configured wave is cleared, the current slice leaves gameplay active

Important scene-manager pitfall:

- `SceneManager3D` is PvP duel-shaped: it waits on the versus screen, locks movement for countdown, ends rounds on player death, stops combat, hides HUD for round-end/game-end screens, applies win tracking, and despawns/repositions players between rounds.
- Do not reuse `SceneManager3D` directly for Invasion. Doing so would drag PvP round cleanup into a wave-owned PvE mode and fight `InvasionWaveManager3D`, which must remain the owner of wave progression and alive-enemy tracking.

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
- `ArtilleryFortressEnemyBrain3D` is a limited-range siege enemy that mostly anchors, slowly creeps into cannon range when close enough to engage, lead-aims a slow heavy cannonball, locks its fire direction during charge windup, optionally fires close-range guided missiles and staggered laser-bolt turrets, and replicates the charge telegraph to clients
- `SuicideDroneEnemyBrain3D` is a dedicated kamikaze brain that always drives at the nearest player at full speed and detonates on contact/proximity using server-authoritative direct damage
- `TankEnemyBrain3D` is a slow, high-HP heavy that advances to a wide hold-band, then sits and pressures the player with two independent weapons: a slow heavy cannon and a homing missile launcher; it reuses `ProjectileWeapon3D` for both slots and `MissileProjectile3D` as the missile prefab
- `RammerEnemyBrain3D` is a fast strike enemy that chases the player at full speed and slams into them on contact for chip damage plus a large knockback, then arcs away to circle back; the knockback routes through the existing `NetMovement3D.ApplyCombatVelocityDelta` recoil hook so the impulse replicates correctly across the network without a new RPC
- `SplitterEnemyBrain3D` owns the Splitter enemy identity: the parent hybrid chooses between projectile and beam pressure based on range plus a random overlap band, then on death asks `InvasionWaveManager3D` to spawn the same prefab twice as smaller role-locked children
- `TriumvirateEnemyBrain3D` owns the Triumvirate enemy identity: small linked beam ships form a triangle, reveal cosmetic lightning links, and then fire a survivor-scaled lightning beam where the full three-ship version is the only slow-applying version
- `SwarmScoutEnemyBrain3D` owns the Swarm Scout enemy identity: fragile fast flyers move in linked formations, default to a pentagon-style flyby through/past the player, can fall back to orbit behavior through a movement-pattern dropdown, and only alert nearby enemy sensors if the required survivor count remains alive near the player through the warmup
- `SiegeCarrierBossEnemyBrain3D` owns the second Invasion boss identity: a slow/stationary Siege Carrier that maintains a preferred range band without constantly rotating its hull toward the player, runs one random major bullet-hell-style pattern lane per detected active player up to two simultaneous lanes, mixes targeted projectile pressure with lagging beam convergence, formation missile salvos, a two-hardpoint lightning slow beam, and optional enemy spawn waves, activates boss-centered orbital energy pillars once at the phase-two transition until death/despawn, keeps a hard per-pattern projectile budget, and leaves authored escape lanes instead of flooding the whole arena
- `NetEnemyMovement3D` makes enemies server-simulated in network sessions
- `NetEnemyCombat3D` makes enemy projectile damage server-authoritative and broadcasts client cosmetics
- enemy beam prefabs may now use an optional `BeamVisualDriver3D` such as `ForgeBeamVisualDriver3D` for presentation, but enemy beam gameplay still stays inside `LaserBeam3D` / `NetEnemyCombat3D`
- `EnemySpawnerWeapon3D` is a prefab-local enemy spawning weapon for Invasion enemies. It spawns one configured enemy prefab at a configured spawn point, repeats for the configured count, and spaces the sequence by `Delay Between Spawns`. It delegates spawning to `InvasionWaveManager3D.SpawnEnemyAt(...)` so network spawning and alive-enemy wave tracking stay centralized. Multiple `EnemySpawnerWeapon3D` components may live on the same enemy or carrier prefab.
- `SpawnArrivalEffect3D` is an optional prefab-local one-shot spawn presentation component for Invasion enemies. It can spawn an authored arrival VFX prefab, scale it per ship, hide renderers until reveal, and temporarily disable assigned colliders/brains/weapons so enemies do not act before they visually arrive.
- `PortalBossSpawn3D` is a prefab-local boss entrance component for large Invasion enemies that should emerge from `Portal3D`. It moves the real boss root from behind the portal to the authored spawn point, disables gameplay during the entrance, and expects the portal prefab to include its own depth-mask disk so only the portion inside the portal silhouette is hidden while the boss exits.
- `InvasionWaveManager3D` is a minimal finite-wave spawner for configured enemy prefabs
- `InvasionSceneManager3D` is the dedicated beginning-flow manager for networked Invasion. It owns player spawning, gameplay HUD activation, wave text presentation, optional enemy counter presentation, optional heart/life counter presentation, UI canvas camera/sorting setup, and arena boundary startup.
- `InvasionSceneManager3D` only owns top-level HUD visibility and canvas camera/sorting setup. Actual player HUD data binding still happens through `PlayerHUDManager3D` on the HUD objects themselves, and the ship-specific weapon/ability HUD is runtime-instantiated by `PlayerWeaponAbilityHUDSpawner3D` after a player bind succeeds.
- `TargetAwarenessHUD3D` should be wired to `EnemyTeam` in Invasion and tuned with a finite awareness range so the enemy tracker only reacts to nearby hostile ships instead of every alive entity in the scene.
- Bug note: network Invasion clients must not rely only on the one-shot wave-start presentation event to enable gameplay HUD. If the scene manager subscribes after that event/RPC fires, the client can look like HUD binding failed even though the real problem is that every gameplay HUD root stayed inactive. Recover HUD visibility from replicated session state (`RoundTransition` / `InMatch`) as well.
- Bug note: do not subscribe to replicated network session HUD events before `SetInitialUiState()` runs. In `InvasionSceneManager3D`, subscribing first can correctly re-enable client HUD from replicated state and then immediately hide it again when the initial-state pass calls `SetGameplayHudActive(false)`, which looks like a client-only HUD binding failure even though the bind itself succeeded.
- Bug note: if a HUD canvas root is active but completely invisible in Scene/Game view, inspect its root `RectTransform.localScale` before chasing binding code. `heartCanvas`, `enemyCounterCanvas`, and `waveCanvas` all regressed with a root scale of `(0,0,0)`, which kept the manager references, active state, and text updates working while making the canvases impossible to see.
- Bug note: the Invasion enemy counter must resync from `InvasionWaveManager3D.AliveEnemyCount` immediately after `InvasionSceneManager3D` subscribes to wave-manager events. If the wave manager already started spawning because `Start On Enable` was left on or another setup path ran earlier, the initial `AliveEnemyCountChanged` events are already gone, which makes the first spawned enemy or pre-placed boss look uncounted until some later spawn/death triggers another broadcast.
- Bug note: enemy `NetworkObject` despawn alone is not enough to keep remote clients in sync during combat. In Invasion, enemies used `NetEnemyCombat3D` for attack replication but only player `NetCombat3D` objects were broadcasting `NetCombatState3D`, so clients could see enemies disappear on death without ever receiving intermediate health/shield updates. Enemy damageable entities must broadcast combat-state changes through their own network broker as well.
- Bug note: player beam weapons in networked Invasion must not reuse duel-only target-tag resolution on the authoritative runtime. `BeamWeapon3D` and `ConvergeBeamWeapon3D` previously saw neutral `"Enemy"` targeting and rewrote it to the opposite player tag through `NetCombat3D.GetEnemyTag()`, so the local client beam cosmetic looked correct while the server damage beam silently searched for `Player1`/`Player2` instead of enemies. Resolve player beam targets through the shared player-targeting helper so generic enemy targeting upgrades to `EnemyTeam` when PvE enemies are present.
- Bug note: the network wave-intro path must not rely on the host receiving its own replicated wave-start presentation event in order to show `WAVE N`. In `InvasionSceneManager3D`, the host should play `ShowWaveText(...)` directly after `BroadcastWaveStartServer(...)`, while clients use `OnWaveStartPresentationChanged`. Late client scene subscriptions also need recovery from the last replicated wave payload during `RoundTransition`, or the first wave intro can be missed entirely even though wave spawning continues correctly.

## Title Screen Entry

- the title-screen 3D host flow now branches through a dedicated 3D sub-select canvas before matchmaking starts
- the duel branch still loads `3d`, while the invasion branch loads `3d_invasion`
- invasion still uses the 3D ship roster; the title flow should not fall back to the 2D roster just because the scene token differs from the duel scene
- test-only title shortcuts may also target `3d_invasion`, but they should still wait for the normal network `ShipSelect` state before auto-locking their fixed test ships; skipping that visible state makes the join flow look frozen even when the session is progressing correctly
- the title scene can now optionally auto-start that same 3D invasion test flow on scene load and pick host or client from a serialized default role, so repeated local invasion/network setup does not require manual menu navigation every run
- when that auto-start path runs as a client, the title flow must keep retrying the move out of the join/IP canvas if the network session reaches `ShipSelect` before the join transition animation finishes; otherwise the client appears stuck on the join UI even though the invasion test flow is already live underneath it
- the auto-start invasion test helper should wait for the menu/network singletons to finish scene startup before launching the client path; calling the same helper too early from title-scene `Start()` is not reliably equivalent to pressing the test-client button after the scene is already idle

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
- `InvasionWaveManager3D` owns wave order, enemy spawning, alive-enemy tracking, wave-clear detection, and inter-wave delay
- `InvasionSceneManager3D` owns the synchronized `WAVE N` text beat before each wave, using `NetworkSessionData.BroadcastWaveStartServer(...)` in network sessions
- the optional enemy counter should read `InvasionWaveManager3D`'s tracked alive-enemy count through `InvasionSceneManager3D`; do not make enemies or individual AI brains update HUD directly
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
- add `EnemyPatrol3D` so the enemy searches the arena when players are outside detection range; it generates runtime waypoints from the active `ArenaBoundary3D` or its fallback `5000 x 5000 x 5000` bounds, so no manual scene waypoint placement is needed
- add `EnemyObstacleAvoidance3D` and set `Obstacle Mask` to asteroids/world-geometry layers
- add `BasicShooterEnemyBrain3D`
- add `ProjectileWeaponEnemy3D`; the brain's aim tolerance gates when shots are allowed, and the brain supplies the target direction when firing so the projectile does not inherit the allowed facing error at long range
- optional: add `ProjectileChargeTelegraph3D` plus `EnemyProjectileChargeAttack3D` when this basic enemy should visibly wind up before firing. Keep the charge driver's `Weapon Type` set to `Projectile`, assign `Projectile Weapon Component` to the projectile or missile weapon, assign `Charge Telegraph`, and tune `Charge Duration`; the brain will use the driver automatically when it is present.
- optional `ShipThrusterVfx3D` can stay on enemies; it reads `EnemyAIFlightController3D.IsMovingForward` when present
- optional: add `SpawnArrivalEffect3D` when the enemy should warp in before appearing. Assign `Assets/Asset Packs/FORGE3D/Sci-Fi Effects/Effects/Warp Jump/WarpJumpIn_red_linear.prefab` as the arrival effect prefab, tune the prefab root scale and `Effect Scale Multiplier` per ship size, keep `Force Particle Hierarchy Scaling` enabled for Forge warp prefabs, keep `Multiply By Ship Scale` enabled for scaled variants, set `Reveal Delay Seconds` to the moment the ship should appear, and assign any gameplay colliders/brains/weapons that should stay disabled until reveal.
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked enemies
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but do not rely on that tag for new PvE damage rules

For a suicide drone enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior
- optionally add `EnemyObstacleAvoidance3D` if the drone should weave around asteroids instead of beelining
- add `SuicideDroneEnemyBrain3D`
- add `NetEnemyMovement3D` for networked movement replication
- do not add `ProjectileWeapon3D` or `NetEnemyCombat3D` unless the drone also has a separate ranged attack
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven

For an artillery beam enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior
- optionally add `EnemyObstacleAvoidance3D` if the artillery ship should path around asteroids while closing distance
- add `ArtilleryBeamEnemyBrain3D`
- add `BeamWeapon3D`; set its `targetFaction` to `PlayerTeam` and keep `targetTag` empty unless you intentionally need legacy fallback
- if you want the Forge line-renderer look, use `Assets/Prefabs/Weapons/Projectiles/3d/projectiles/enemies/beam/enemy_beam_forge_red.prefab` as the beam prefab instead of the older cylinder-based enemy beam
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus beam cosmetic replication
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven

For an artillery fortress enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` with gravity off
- add `EnemyAIFlightController3D`; tune `moveSpeed` low, roughly `20-30`, and `rotationDegreesPerSecond` low, roughly `35-60`, so the fortress can creep into range without becoming a chaser
- add `EnemyTargetSensor3D`; set `detectionRadius` to at least `maxFiringRange + approachRangeBuffer` so the fortress can acquire targets before they enter cannon range
- add `EnemyPatrol3D` for no-target arena search behavior; keep its `patrolSpeedScale` low if the fortress should feel like it is sweeping slowly rather than roaming aggressively
- add `ProjectileWeaponEnemy3D`; configure it as the slow heavy cannon with high damage, long lifetime, long cooldown, `targetFaction = PlayerTeam`, empty `targetTag`, and a muzzle wired to the visible barrel
- optionally add `StaggeredMissileWeaponEnemy3D` for a guided missile rack; assign each launcher Transform to `weaponConfig.muzzles`, use a projectile prefab with `MissileProjectile3D` in guided mode, set `targetFaction = PlayerTeam`, leave `targetTag` empty, tune the inherited missile weapon cooldown as the full rack activation cooldown (for example `8-10s`), tune `launcherStaggerInterval` as the spacing between individual launcher shots (for example `0.5-1s`), and enable `Randomize Launcher Selection` if missiles should pick a random launcher each shot instead of looping
- optionally add `MissileLauncherYawTracker3D`; assign the same launcher transforms as `launcherPivots` or leave them empty to auto-use the missile weapon's muzzles, keep `Yaw Only` enabled, start with `yawDegreesPerSecond = 180`, set `localYawOffsetDegrees` only if the model's launcher-forward axis is not local +Z, and set `maxYawFromRestDegrees` to `0` for unlimited yaw or a small clamp if the model should not swivel too far
- optionally add one or more `StaggeredProjectileWeaponEnemy3D` components for small close-range laser-bolt turrets; assign turret muzzle transforms to `weaponConfig.muzzles`, set `targetFaction = PlayerTeam`, tune the inherited cooldown as the full turret-rack activation cooldown, tune `turretStaggerInterval` as the delay between individual turret bolts, and enable `Randomize Turret Selection` if bolts should pick a random turret each shot instead of looping
- optionally add `ProjectileTurretYawTracker3D`; for two-part turrets, add one `Turret Binding` per turret, assign the yawing base to `Base Yaw Pivot`, assign its child barrel/head to `Pitch Pivot`, enable `Use Base X Rotation` only for side-mounted bases whose horizontal swivel axis is local X instead of local Y, tune `localYawOffsetDegrees` if the base forward axis is not local +Z, tune `localPitchOffsetDegrees` or `Invert Pitch` if the barrel elevates the wrong way, and clamp `maxYawFromRestDegrees` / `maxPitchFromRestDegrees` if the model should not swivel too far. If `Turret Bindings` is empty, the component falls back to legacy yaw-only `turretPivots` or the turret weapon's muzzles.
- add `ProjectileChargeTelegraph3D`; assign the ship/body renderers, ensure the material supports emission, keep `Add To Shared Material Emission` off if this fortress should override the shared material's normal glow, leave `Use Charge Color Override` off to preserve the material's emission color while scaling intensity, set `Idle Emission Intensity` to a small nonzero value for a dim idle glow or `0` to leave the original emission color untouched at idle, and set `Max Charge Emission Intensity` around `4-5`; optionally assign a child VFX root or light for a stronger charge tell. Existing prefabs that still carry `ArtilleryFortressChargeTelegraph3D` remain valid through the compatibility wrapper.
- add `ArtilleryFortressEnemyBrain3D`; assign the cannon weapon, optional missile weapon, optional close-range turret weapons, and charge telegraph if auto-assignment does not find them; start with `maxFiringRange = 200`, `approachRangeBuffer = 100`, `outOfRangeApproachSpeedScale = 0.2`, `maxMissileRange = 100-120`, `missileAimToleranceDegrees = 45`, `missileToCannonStaggerDelay = 0.35`, `maxTurretRange = 80-100`, and `turretToCannonStaggerDelay = 0.15`
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated projectile fire and charge presentation
- optional: add `PortalBossSpawn3D` when this fortress should arrive through a dimensional portal instead of simply appearing. Assign `Assets/Prefabs/3d_effects/Portal3D.prefab`, tune `Portal Uniform Scale` large enough to cover the fortress silhouette from the gameplay camera, and tune `Emerge Distance` / `Emerge Duration` for the intended slow drift. Leave the optional override arrays empty unless this prefab gains unusual extra gameplay scripts or collider wiring later.
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set high `maxHealth` on `Entity3D` so the fortress survives a real assault

For a tank enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` (gravity off; tune drag for the slow lumbering feel)
- add `EnemyAIFlightController3D`; tune `moveSpeed` low and `rotationDegreesPerSecond` low so the tank visibly turns slowly and players can strafe around it
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior
- optionally add `EnemyObstacleAvoidance3D` if the tank should weave around asteroids while advancing
- add **two** `ProjectileWeapon3D` components on the same root: one configured as the slow heavy cannon (slow projectile speed, high damage, short-to-medium cooldown), one configured as the homing missile launcher (longer cooldown, projectile prefab is a `MissileProjectile3D` variant). Set both weapons' `targetFaction` to `PlayerTeam` and use `MuzzleForward` aiming. Wire each weapon's muzzle Transform to its own visible barrel/launcher on the model.
- add `TankEnemyBrain3D` and assign both weapon references in its inspector (cannon slot and missile slot are explicitly serialized, since `GetComponent<ProjectileWeapon3D>()` would only resolve the first one)
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated fire
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set a high `maxHealth` on `Entity3D` to lean into the tank identity

For a flamethrower enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` with gravity off
- add `EnemyAIFlightController3D`; start with moderate `moveSpeed` and a strong `rotationDegreesPerSecond` so the enemy can keep the flame lane on the player without feeling impossible to shake
- add `EnemyTargetSensor3D`; start with detection range around `180-250`
- add `EnemyPatrol3D` for no-target arena search behavior
- add `EnemyStrafeMover3D`; this is required for the active-flame orbit because the enemy must face the player while sliding laterally
- optionally add `EnemySeparation3D` so multiple flamethrowers do not stack in the same pocket
- optionally add `EnemyObstacleAvoidance3D` if the enemy should route around asteroids while closing distance
- add `EnemyFlamethrowerWeapon3D`; assign `Assets/Prefabs/3d_weapons/projectiles/enemies/3d_flamethrower.prefab` as the flame visual prefab, assign a muzzle whose local forward points down the intended flame lane, set `Target Faction = PlayerTeam`, and keep `Target Tag` empty
- optional: add `ProjectileChargeTelegraph3D` plus `EnemyProjectileChargeAttack3D` when the flamethrower should visibly wind up before a burst. Set the charge driver's `Weapon Type` to `Flamethrower`, assign `Flamethrower Weapon` to `EnemyFlamethrowerWeapon3D`, assign `Charge Telegraph`, and tune `Charge Duration`; `FlamethrowerEnemyBrain3D` will use the driver automatically when it is present.
- add `FlamethrowerEnemyBrain3D`; start with `Preferred Range Min = 20`, `Preferred Range Max = 30`, `Too Close Retreat Distance = 14`, `Full Approach Distance = 55`, `Aim Tolerance Degrees = 22`, `Flame Orbit Strafe Speed = 10`, and `Flame Orbit Direction Change Interval = 3`
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated flame visual state; only the server applies cone damage
- create a `FlamethrowerEnemyBalanceProfile3D` asset, assign it through `EnemyBalanceProfileApplier3D`, and keep prefab-only wiring such as the flame prefab, muzzle, masks, visuals, audio, collision, and network references on the prefab
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven

For a glass cannon interceptor enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` (gravity off, low drag)
- add `EnemyAIFlightController3D`; tune `moveSpeed` very high and `rotationDegreesPerSecond` high so the ship can relocate quickly, then snap into a firing posture before its burst
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior
- optionally add `EnemySeparation3D` if multiple interceptors may spawn together and should not stack on the same perch
- optionally add `EnemyObstacleAvoidance3D` if the interceptor needs to steer around asteroids during repositioning
- add `ProjectileWeaponEnemy3D`; configure it as the short-burst gun with roughly 10 damage, 0.2s cooldown, fast hitscan-like projectile speed, and `targetFaction = PlayerTeam`; the interceptor brain will gate firing by nose tolerance and launch shots toward the target
- add `GlassCannonInterceptorEnemyBrain3D`; start with `preferredRangeMin = 40`, `preferredRangeMax = 50`, `shotsPerBurst = 3`, `preBurstSettleDuration = 0.35`, and `postBurstRecoverDuration = 0.3`
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated projectile fire
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set `Entity3D` max health to 15 so a single normal player shot can one-shot it

For a rammer enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` (gravity off, low drag for fast pursuit feel)
- add `EnemyAIFlightController3D`; tune `moveSpeed` high and `rotationDegreesPerSecond` high so it can actually arc back around for another pass after a hit
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior
- optionally add `EnemyObstacleAvoidance3D` if the rammer should weave around asteroids while charging
- add `RammerEnemyBrain3D`; tune `knockbackVelocity` first since that single value drives the whole feel of the enemy
- add `NetEnemyMovement3D` for networked movement replication; `NetEnemyCombat3D` is **not** required since the rammer has no projectile weapons (and the knockback hook lives on the player's `NetMovement3D`, not on the enemy)
- rammers are deprecated and currently excluded from `EnemyBalanceProfile3D` extraction; do not add deprecated rammer prefabs to active waves until that path is revived deliberately
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set moderate `maxHealth` on `Entity3D` (lower than the tank - this is a fast strike unit, not a bruiser)

For a Splitter enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior; spawned children inherit the same patrol fallback
- add `ProjectileWeaponEnemy3D`; configure it for the closer-range projectile pressure, with `targetFaction = PlayerTeam`
- add `BeamWeapon3D`; configure it for farther-range laser pressure, with `targetFaction = PlayerTeam`
- add `SplitterEnemyBrain3D`
- assign `Splitter Prefab` to this same prefab asset; this is a self-reference to one prefab, not a second child prefab
- assign the projectile weapon, beam weapon, flight controller, target sensor, and `NetEnemyCombat3D` if auto-assignment does not find them
- keep `Split Count = 2` for the intended current design
- tune `Child Scale Multiplier`, `Child Move Speed Multiplier`, `Child Max Health`, and `Child Max Shield` on the brain; spawned children inherit the same prefab but are shrunk, sped up, and refilled to those child stats
- child `0` becomes beam-only and child `1` becomes projectile-only; their unused weapon component is disabled by the brain
- tune `Projectile Preferred Distance`, `Beam Preferred Distance`, `Mixed Range Beam Chance`, and `Mixed Range Width` to control how often the parent chooses each weapon when both are reasonable
- tune `Projectile Convergence Distance` so multi-muzzle projectile volleys cross at the intended distance in front of the Splitter instead of flying parallel from widely spaced hardpoints
- enable `Log Weapon Choices` temporarily when debugging weapon selection; it reports whether the Splitter chose projectile or beam and whether that choice fired or was blocked
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated projectile/beam fire
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- add the Splitter prefab to `InvasionWaveManager3D` wave entries; spawned children are added to the same alive-enemy tracking automatically and must be cleared before the wave completes

For a Duelist enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` (gravity off, low drag for clean strafe response)
- add `EnemyAIFlightController3D`; tune `moveSpeed` mid-to-high for clean repositioning between perches and `rotationDegreesPerSecond` high enough that the duelist can track the player while strafing laterally
- add `EnemyStrafeMover3D`; this is what enables true sideways/vertical strafe motion. Set `Max Strafe Speed` higher than the brain's `Dodge Speed` so the cap never clips the dodge, leave `Combine With Flight Thrust` on, and match `Lock To World Y Plane` to the flight controller's flag if you are testing in a planar scene
- add `EnemyTargetSensor3D`; set `Detection Range` to at least `Beam Preferred Center + Beam Half Width` (default ~250m) so the duelist can acquire targets out at full beam range
- add `EnemyPatrol3D` for no-target arena search behavior; patrol stops the strafe overlay before taking over so the duelist does not slide while searching
- optionally add `EnemySeparation3D` if multiple duelists may spawn together and should not stack on the same flank
- optionally add `EnemyObstacleAvoidance3D` if the duelist needs to weave around asteroids while repositioning
- add `ProjectileWeaponEnemy3D`; configure as the close-range projectile pressure (fast bolts, short cooldown), `targetFaction = PlayerTeam`
- add `MissileWeaponEnemy3D`; configure as the mid-range guided missile, `targetFaction = PlayerTeam`, and make sure its projectile prefab carries `MissileProjectile3D`
- add `BeamWeapon3D`; configure as the long-range beam, `targetFaction = PlayerTeam`. Use `Direction Reference` if the model's beam muzzle local forward is not the intended shot lane
- add `DuelistEnemyBrain3D`; auto-assignment will pick up the three weapons, the flight controller, the strafe mover, the target sensor, and `NetEnemyCombat3D`. Required wiring you must do by hand:
  - set `Projectile Layers` to the layers your projectile prefabs live on under `Assets/Prefabs/3d_weapons/projectiles/` (the threat scan only reacts to projectiles on those layers)
  - confirm `Preferred Range Min/Max` (defaults `100`/`200`) and the per-weapon `Preferred Center` / `Half Width` bands match the engagement distance you want
  - tune `Vibes Chance` if the duelist feels too predictable (or too erratic) about which weapon it picks
  - tune `Dodge Chance Per Threat` and `Dodge Cooldown` to set how often dodges actually fire when projectiles come in
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated projectile and beam fire
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- set moderate `Entity3D` max health; the duelist is a flanker, not a tank, but it should survive longer than a glass-cannon interceptor
- register the duelist prefab in `Assets/DefaultNetworkPrefabs.asset` for NGO before adding it to wave entries

For a Triumvirate enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody`
- add `EnemyAIFlightController3D`
- add `EnemyTargetSensor3D`
- add `EnemyPatrol3D` for no-target arena search behavior; the coordinator lets all surviving squad members patrol independently until a player is reacquired, then formation logic takes movement back
- add `BeamWeapon3D`; assign `enemy_lightning_beam.prefab`, set `targetFaction = PlayerTeam`, and set the weapon's `damagePerSecond = 0` if `TriumvirateEnemyBrain3D` is owning one/two/three-member damage scaling
- add `TriumvirateEnemyBrain3D`; assign the final beam weapon and `NetEnemyCombat3D`, assign `Link Lightning Prefab` to `enemy_lightning_beam.prefab`, and tune the one/two/three-member damage fields plus full-triad slow fields
- leave `Keep Formation On World Y Plane` off for the intended vertical triangle with one ship high and two low; only enable it if a local test prefab is deliberately locked to a planar Y flight level
- use `Log State Changes` and temporary `Log Formation Progress` while testing scene setup so stuck states report whether the squad is forming, linking, charging, firing, or cooling down
- either assign all three `Squad Members` explicitly after placing/spawning a group, or spawn them close together with the same `Squad Key` and an `Auto Link Radius` large enough for discovery
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus final beam presentation
- create an `EnemyBalanceProfile3D` asset, initialize it from the prefab's current health, movement, detection, weapon, and brain tuning, then add `EnemyBalanceProfileApplier3D` on the prefab root and assign the profile before adding the prefab to waves
- set low `Entity3D` health so the intended counterplay is destroying ships during formation/linking before the full slow beam fires
- add Triumvirate entries to waves in multiples of three; the brain can degrade to two or one survivor, but the intended enemy identity assumes a three-ship group at spawn

For a Swarm Scout enemy prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` with gravity off
- add `EnemyAIFlightController3D`; tune `moveSpeed` and `rotationDegreesPerSecond` very high, and set `moveWhenFacingAngle` wide enough that scouts keep moving through aggressive turns
- add `EnemyTargetSensor3D` with enough detection range to find players before entering orbit
- add `EnemyPatrol3D` for no-target search behavior; the brain also keeps its last/fallback heading if patrol is unavailable so the scout does not intentionally idle
- optionally add `EnemySeparation3D` and `EnemyObstacleAvoidance3D` if swarms need local spreading or asteroid steering
- add `SwarmScoutEnemyBrain3D`; use `Movement Pattern = Formation Flyby` for the pentagon pass-through behavior, or switch to `Orbit Helix` as the fallback if the flyby needs more tuning
- start with `Intended Swarm Size = 5`, `Formation Radius = 26`, `Formation Overshoot Distance = 180`, `Required Survivors For Alert = 5`, `Alert Broadcast Radius = 1400`, `Alert Warmup Seconds = 3`, and `Alert Duration = 6`
- add `NetEnemyMovement3D`; `NetEnemyCombat3D` is not required because scouts do not fire weapons
- create a `SwarmScoutEnemyBalanceProfile3D` asset, assign it through `EnemyBalanceProfileApplier3D`, and keep prefab-only wiring such as visuals, audio, collision, network references, and death effects on the prefab
- set the root tag to `Enemy` for compatibility, but keep target/damage filtering faction-driven
- add Swarm Scout entries to waves in clustered counts of five with little or no spawn delay so the linked movement and full-survivor alert gate read correctly

For a Siege Carrier boss prefab:

- add `NetworkObject` if it will spawn in networked Invasion
- add `FactionMember3D` and set faction to `EnemyTeam`
- add `Enemy3D`
- add `Rigidbody` with gravity off
- add `EnemyAIFlightController3D`; start with `moveSpeed` around `15` and a slow-to-moderate turn rate so the boss reads as a heavy carrier when it relocates, not a chase enemy
- add `EnemyTargetSensor3D`; set detection range at least to `max(preferredRangeMax, engagementRange) + approachRangeBuffer`
- add `EnemyPatrol3D` for no-target search behavior, with low patrol speed if the boss should only drift between sightings
- add `NetEnemyMovement3D` and `NetEnemyCombat3D` for networked movement plus replicated projectile/beam presentation
- add `SiegeCarrierBossEnemyBrain3D`; wire separate projectile weapon component arrays for lagging rake and optional formation missile salvos, wire `BeamWeapon3D` components for the lagging beam convergence pattern, wire exactly two lightning `BeamWeapon3D` components for the slow-beam pattern, and optionally wire `EnemySpawnerWeapon3D` components into the enemy spawn-wave weapon array. The brain runs one pattern lane with one active player and adds a second distinct random pattern lane only while a second active player-team target is detected.
- add `PortalBossSpawn3D` when the carrier should enter through the shared portal asset. Assign `Assets/Prefabs/3d_effects/Portal3D.prefab`, keep the portal scale generous enough to cover the carrier silhouette during emergence, and use the component's authored spawn point as the final post-intro boss location.
- optional: add `FormationMissileSalvoWeaponEnemy3D` for the missile bloom pattern. Assign eight launcher/muzzle transforms if the model has them, or fewer if the salvo should cycle through repeated launch points. Set `Missile Count` to `8`, configure `Target Faction = PlayerTeam`, keep `Target Tag` empty, and use a projectile prefab with `MissileProjectile3D`.
- add optional `ProjectileChargeTelegraph3D` components paired to the beam convergence weapons if the model has warning lights or beam emitters that should glow during the beam telegraph; add separate optional telegraphs for the lightning slow-beam weapons if those two emitters should warn before firing
- add either `Assets/Prefabs/3d_effects/SiegeCarrierOrbitalEnergyPillarVisual3D.prefab` for the preserved red/white V1 look or an equivalent `OrbitalEnergyPillarBluePlasmaVisual3D` child for the blue/white V2 plasma look under the boss prefab, then assign that component to the brain's orbital pillar visual field; set the visual driver's `Launched Sphere Prefab` / `Blue Launched Sphere Prefab` to the authored sphere prefab the carrier should shoot, and tune `Pillar Initial Growth Height`, `Pillar Height Growth Power`, and the sphere transform flash fields so the cylinders visibly grow up/down out of the settled spheres
- create a `SiegeCarrierBossBalanceProfile3D` asset, assign it through `EnemyBalanceProfileApplier3D`, and tune the boss health, slow movement, preferred range min/max, plane bias, detection range, pattern cooldowns, shot budget, lane counts, phase-two orbital pillar count/radius/timings/damage, formation missile budget cost, beam telegraph, active duration, convergence lag, and beam aim smoothing there
- configure all boss projectile and beam weapons with `targetFaction = PlayerTeam` and empty `targetTag`; tune the two lightning beam weapons to moderate damage per second, while the slow multiplier/duration/radius live on the boss brain/profile
- optional: add one or more `EnemySpawnerWeapon3D` components when the boss/carrier should release subordinate enemies. Assign the enemy prefab, count, spawn point, and delay between spawns per component, then add those components to the boss brain's Enemy Spawn Wave Weapons array so the spawn wave joins the normal pattern rotation. Leave `Spawn On Enable` off when the boss brain owns timing. Each spawned prefab still needs normal Invasion enemy wiring and NGO registration if used in a networked session.
- keep the root tag as `Enemy` for compatibility until every player weapon path is fully faction-authored
- register the boss prefab in NGO network prefabs before adding it to a boss/elite wave entry

For player prefabs used in Invasion:

- add `FactionMember3D`
- set faction to `PlayerTeam`
- assign the matching `PlayerBalanceProfile3D` asset through `PlayerBalanceProfileApplier3D` before using the prefab in Invasion, so PvE tuning stays in the profile instead of drifting across prefab inspector fields
- set player projectile weapon configs that should damage enemies to `targetFaction = EnemyTeam`
- keep existing `Player1` / `Player2` tags for slot compatibility

For `3d_invasion`:

- add `InvasionSceneManager3D`
- assign player 1 and player 2 spawn points
- assign the two fallback 3D `ShipData` assets
- assign `InvasionWaveManager3D`
- assign the reused round canvas group/text as the Wave UI references
- optionally enable `Use Enemy Counter`, assign the enemy counter canvas/root, assign its TMP text, and tune the counter text format
- optionally enable `Use Life Counter`, assign the heart/life counter canvas group, assign its TMP text, set the starting player lives, and tune the counter text format. This is currently display-only until life loss, respawn, and wipe rules are implemented.
- assign gameplay HUD roots for health, vignette, crosshair, weapon container, ability container, FPS/ping, enemy tracker, and the enemy counter canvas if it should activate with the rest of gameplay HUD. The life counter is controlled through its canvas group instead of a root active toggle.
- assign UI canvases and optional UI camera so network HUD sorting is deterministic
- do not let child HUD scripts silently reassign those canvases back to `Camera.main`; the scene-level UI camera should remain the single source of truth for screen-space HUD canvas camera binding
- assign `ArenaBoundary3D` so the scene manager can reset/start it once when gameplay begins
- do not wire versus, countdown, win tracker, round-end, or game-end UI into this manager
- add `InvasionWaveManager3D`
- leave `Start On Enable` off when `InvasionSceneManager3D` owns the scene flow; otherwise waves can begin before players are spawned and before `WAVE N` presentation is subscribed
- assign spawn points
- add roughly five finite wave entries for the current target, starting with at least one basic shooter test wave
- ensure networked enemy prefabs are registered with NGO before network spawning
