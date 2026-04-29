# 3D_AI.md

This document owns 3D AI architecture and enemy behavior notes.

The 3D AI path should stay modular. Enemy prefabs should compose small scripts instead of growing a single inherited enemy monolith.

## Current AI Components

- `Enemy3D`
  - enemy entity coordinator
  - caches enemy-only systems
  - disables the player-style `ShipFlight3D` component so enemy movement has one Rigidbody owner
  - should stay narrow
- `EnemyAIFlightController3D`
  - simple enemy Rigidbody motor
  - receives a world-space move direction, rotates toward it, then directly sets Rigidbody velocity along its own facing direction once facing that direction
  - exposes `IsMovingForward` for enemy thruster VFX so rotating-in-place does not light engines
  - exposes a runtime move-speed override for spawn variants such as Splitter children that reuse the same prefab but need faster movement
  - must still consume the owning `Entity3D` rotation multipliers, or active beam/charge weapons can advertise turn penalties that never affect enemy aim in practice
  - contains no targeting, pathing, or combat decisions
- `EnemyTargetSensor3D`
  - periodically selects the nearest target in a configured faction
  - current basic shooter target faction is `PlayerTeam`
  - does not gate targeting through obstacle line-of-sight checks
- `EnemyObstacleAvoidance3D`
  - adjusts a desired free-flight direction using non-alloc spherecast probes
  - intended for asteroids and world geometry
  - this is steering/avoidance, not Unity NavMesh
  - currently not implemented anywhere since it was extremely buggy
- `EnemySeparation3D`
  - shared inter-agent separation steering helper that mirrors the `EnemyObstacleAvoidance3D` API (`ResolveSteeringDirection(Vector3 desired)`)
  - non-alloc `OverlapSphere` against an `agentMask` LayerMask; pushes the desired direction away from same-faction allies inside `allyRadius` and biases laterally away from non-ally entities (e.g. the player) inside the smaller `playerProximityRadius`
  - intended chaining: `desired -> separation -> obstacleAvoidance -> flight controller`
  - currently consumed by `RammerEnemyBrain3D` (gated behind a `useSeparation` brain toggle); other enemy brains can opt in by adding the component to their prefab and calling its API the same way
- `EnemyPatrol3D`
  - reusable no-target fallback for Invasion enemies so they search the arena instead of freezing when players are outside detection range
  - generates patrol waypoints at runtime; designers do not author scene waypoint lists
  - samples points inside the active `ArenaBoundary3D` bounds when one exists, otherwise uses a serialized fallback arena box (default `5000 x 5000 x 5000`)
  - rejects tiny patrol legs, keeps an edge margin away from force-field walls, biases routes slightly toward the enemy's current forward direction, and uses per-instance waypoint seeds so enemies do not all choose the same route
  - can route patrol steering through `EnemySeparation3D` and `EnemyObstacleAvoidance3D`; this avoids shared waypoint hotspots and helps nearby enemies fan out while searching
  - is consumed by enemy brains only when their `EnemyTargetSensor3D` returns no target; engagement brains still own all chase, attack, formation, and weapon decisions once a player is detected
- `EnemyStrafeMover3D`
  - reusable world-space lateral/vertical strafe overlay that sits beside `EnemyAIFlightController3D` on the same Rigidbody
  - the enemy flight controller can only thrust forward/backward along its facing direction; the strafe mover is what enables true sideways motion while the flight controller continues to drive rotation
  - brain calls `BeginStrafe(worldVelocity, durationSeconds)`; the mover writes the requested velocity to the Rigidbody every `FixedUpdate` until the timer expires, then auto-stops
  - runs at `[DefaultExecutionOrder(100)]` so its velocity write happens after `EnemyAIFlightController3D` and either replaces it or stacks on top, depending on `combineWithFlightThrust`
  - enforces a per-prefab `maxStrafeSpeed` cap and an optional `lockToWorldYPlane` flag so the mover never violates a planar test scene
  - currently consumed by `DuelistEnemyBrain3D` for both the orbit strafe and reactive dodges; other brains can opt in by adding the component to the prefab and calling the same API
- `BasicShooterEnemyBrain3D`
  - first Invasion enemy behavior
  - directly pursues the nearest player
  - slows/stops near the target instead of overshooting into long orbit loops
  - fires its projectile weapon when aimed and off cooldown; its aim tolerance controls when the enemy is allowed to shoot, while the actual projectile direction is the current target direction
  - can optionally route that shot through `EnemyProjectileChargeAttack3D` so basic projectile or missile enemies get a reusable pre-fire telegraph; without that component it keeps the original immediate-fire behavior
  - should use `ProjectileWeaponEnemy3D` for direct-fire guns so the AI path avoids player-only weapon overhead while still letting the brain supply the actual shot direction
- `ArtilleryBeamEnemyBrain3D`
  - long-range beam enemy behavior translated from the old artillery concept
  - maintains a standoff band instead of hard-chasing into point-blank range
  - can face the player while thrusting backward so it keeps pressure on targets that close the gap
  - requires aim plus line-of-sight before sustaining its beam
  - keeps high-level fire/movement decisions on a think interval, but must refresh active beam aim every frame so the beam visual stays locked to moving targets instead of updating at AI cadence
  - treats the beam as a strict forward hardpoint, not a free-aim attack: it should only fire down the current beam forward direction and only when the target falls inside that forward lane
  - should also enforce its own restart-energy threshold so a depleted beam weapon does not spam restart attempts every think tick while it is still nearly empty
  - when using Forge beam visuals, prefer the dedicated `ForgeEnemyBeam3D` runtime so the enemy's visible beam, damage ray, and hit flare all share one endpoint calculation
  - for the Forge artillery beam path, keep the beam transform attached to the muzzle/forward anchor instead of rebuilding its world origin from the current aim vector each frame; the original Forge plasma beam assumes a stable hardpoint transform
  - if the enemy is still "stutter-pivoting" between beam bursts, prefer a short post-fire beam rotation-penalty linger instead of globally lowering the motor's base turn speed; that keeps the artillery identity without making all non-beam turning feel sluggish
- `ArtilleryFortressEnemyBrain3D`
  - long-range siege enemy that mostly anchors in place, but slowly creeps forward when the target is outside cannon range and inside its approach buffer
  - uses lead aim based on the assigned cannon projectile speed and the target Rigidbody velocity, then locks the chosen fire direction at charge start
  - aim tolerance only gates when the charge can begin; the cannonball launches along the locked lead direction after windup so a few degrees of allowed facing error does not become a long-range miss
  - state machine: `Acquiring` faces the live lead direction until target range, aim, and weapon readiness line up; `Charging` holds the locked direction for `chargeWindUpDuration`, then fires once through `NetEnemyCombat3D` in networked Invasion or directly offline
  - cannot start or finish charged shots outside `maxFiringRange`; when outside range but within `maxFiringRange + approachRangeBuffer`, it uses `SetFlightIntent` with `outOfRangeApproachSpeedScale` to lumber into range
  - can optionally fire close-range guided missiles through `MissileWeaponEnemy3D` or `StaggeredMissileWeaponEnemy3D`; missile fire is gated by `maxMissileRange`, a looser missile aim tolerance, and the missile weapon's own cooldown
  - with `StaggeredMissileWeaponEnemy3D`, the missile weapon cooldown starts the full rack sequence and `launcherStaggerInterval` spaces the individual launcher shots inside that sequence
  - after a successful missile launch, briefly delays cannon charge startup with `missileToCannonStaggerDelay` so close-range missiles and the heavy cannon do not begin on the same frame
  - missile rack hardpoints can use `MissileLauncherYawTracker3D` to visually yaw child launcher transforms toward the current target; this is presentation/initial-hardpoint alignment only, not a firing-decision brain
  - can also drive one or more `StaggeredProjectileWeaponEnemy3D` close-range turret weapons for laser-bolt chip pressure; these turrets fire independently while the cannon acquires or charges, as long as the target is inside `maxTurretRange`
  - turret hardpoints can use `ProjectileTurretYawTracker3D` to track the current target without becoming another AI decision owner; use `Turret Bindings` for two-part turrets where the base rotates local Y and the child turret/barrel rotates local X for elevation
  - charge readability is presentation-only through `ProjectileChargeTelegraph3D`; the server replicates start/stop visual state to clients, but clients still do not run AI or firing decisions
- `SuicideDroneEnemyBrain3D`
  - dedicated detonation behavior for kamikaze enemies
  - always commits to the nearest player at full speed instead of using range management
  - detonates on contact or very close proximity so high-speed impacts do not depend entirely on a single collision callback
  - applies damage through `Entity3D` on the authority side, then kills itself through the normal enemy death path
- `TankEnemyBrain3D`
  - slow, high-HP heavy that lumbers toward the player and holds at a wide stop band (default 35m) instead of kiting or rushing in
  - reuses the basic shooter speed-scale shape; turn rate is intentionally tuned slow on the prefab's `EnemyAIFlightController3D` so players can flank
  - drives two independent enemy-only weapons: a `ProjectileWeaponEnemy3D` slow heavy cannon (tight aim tolerance, short cooldown) and a `MissileWeaponEnemy3D` homing launcher whose projectile prefab is a `MissileProjectile3D` (wide aim tolerance, long cooldown)
  - aim tolerances gate when each weapon may fire; the projectile/missile launch direction is still resolved toward the target instead of using the remaining muzzle-forward offset
  - each weapon's own cooldown still gates fire rate, but the brain also applies a small cross-weapon stagger delay after a successful shot so the cannon and missile launcher do not both dump on the same frame when both are ready
  - intentionally cheaper than the artillery brain: no line-of-sight raycast, no per-frame aim refresh, no beam state machine
- `GlassCannonInterceptorEnemyBrain3D`
  - fragile, high-pressure ranged enemy whose identity is a readable loop: `Reposition` -> `Settle` -> `Burst` -> `Recover`
  - chooses a new world-space perch around the current player target at roughly 40-50m, using lateral plus vertical bias so it relocates through the full 3D flight volume without becoming a noisy continuous orbit
  - stops moving during `Settle`, `Burst`, and `Recover`; those stationary beats are the intended player accuracy-check windows because the prefab should die to one player shot
  - fires short `ProjectileWeaponEnemy3D` bursts only while its nose is within a tight aim tolerance of the player; shots travel toward the player's current position, not with predictive lead, and do not preserve the residual tolerance angle as shot error
  - keeps movement simple and learnable by cycling perch direction deterministically instead of dodging individual player shots
  - should be tuned as a glass cannon on the prefab: very low `Entity3D` health, high `EnemyAIFlightController3D.moveSpeed`, fast turn rate, and a fast high-damage bolt weapon
- `RammerEnemyBrain3D`
  - CURRENTLY DEPRECATED DUE TO COLLIDER BUGS
  - fast strike enemy whose identity is committed straight-line charges with strong knockback, not constant pursuit
  - state machine: `Stalk` -> `WindUp` -> `Charge` -> `Disengage` -> `Stalk`. Every transition is server-authoritative
    - **Stalk**: closes at full speed when farther than `stalkDistance`, then drifts at `stalkSpeedScale` once inside that band. The rammer hangs near the player while waiting for the next charge to be ready, so the player can read the breathing room
    - **WindUp**: triggered when the rammer is within `chargeStartDistance` AND past the post-disengage `chargeCooldown`. The rammer faces the target and creeps forward (very low speed scale) for `windUpDuration`, telegraphing the attack
    - **Charge**: at the end of WindUp, the brain LOCKS a charge vector (`target.position - self.position` normalized). For the entire charge state the rammer flies along that locked vector at full speed - it does NOT re-target the player. This gives the player a real dodge window because the rammer cannot track sideways juke. The charge ends when (a) hit detection fires, (b) `chargeMaxDuration` elapses, or (c) the rammer flies past the locked predicted contact point by `chargeOvershootDistance`
    - **Disengage**: triggered by hit OR miss. Hits run the eject + collision exemption path; misses skip both and just steer back to stalk distance. Either way, `chargeCooldown` starts at disengage entry so the rammer cannot immediately re-charge
  - hit detection runs every `FixedUpdate` (only during Stalk and Charge - WindUp is stationary, Disengage is collision-exempted by design). The check has three layers:
    1. distance check between the rammer's transform and the resolved target's transform
    2. layer-masked `OverlapSphereNonAlloc` on `contactDetectionMask` that catches off-center compound colliders the transform-distance check misses
    3. swept `SphereCastNonAlloc` from the previous fixed-update position along the actual movement vector - catches tunneling at high closure rates where the rammer would otherwise pass cleanly through the player between physics ticks
  - on a successful ram hit (in any state), applies `ramDamage` chip damage and `NetMovement3D.ApplyCombatVelocityDelta(knockDir * knockbackVelocity)` to the player so the impulse replicates correctly across the network. `knockbackVelocity` defaults higher than other contact enemies so the hit reads as "sent reeling"
  - on hit, enters a layered disengage: a short reverse-thrust eject window (`ejectDuration`, default 0.35s) where the rammer keeps its nose pointed at the target but is physically pulled backward at full `moveSpeed` via `EnemyAIFlightController3D.SetFlightIntent(awayDirection, toTargetDirection, 1f, moveBackward: true)`, followed by face-away forward disengage for the remainder of `disengageDuration`. The eject prevents the rammer from freezing in place inside the player's collider while rotating around (see `3D_BUGS.md`)
  - while disengaging from a hit, every collider on the rammer is paired with every collider on the rammed entity through `Physics.IgnoreCollision(..., true)` and reverted on disengage end, `OnDisable`, or target loss. Guarantees no physical entanglement after impact. Gated behind `useCollisionExemption` for designer override
  - separation steering (`EnemySeparation3D`) is wired into Stalk and post-eject Disengage. It is intentionally NOT applied during the locked Charge or during the reverse-thrust Eject - any sideways drift would re-open the flight controller's facing-vs-move angle gate and zero the velocity (the freeze documented in `3D_BUGS.md`)
  - `ramDetectionDistance` should be tuned to at least `(rammer collider radius + target collider radius + ~0.5m safety)` so the hit fires before geometric overlap. `contactDetectionMask` should be set to the layer(s) used by player ship colliders for reliable detection in cluttered scenes
  - survives the impact - this is not a kamikaze. Knockback is the entire identity; damage is secondary.
- `SplitterEnemyBrain3D`
  - medium enemy that carries both `ProjectileWeaponEnemy3D` and `BeamWeapon3D`
  - parent role (`ParentHybrid`) chooses projectile pressure closer in and beam pressure farther out, with a tunable mixed-range random roll so it can still use either weapon when both are reasonable
  - beam mode follows the same hardpoint-forward contract as `ArtilleryBeamEnemyBrain3D`: it checks whether the target is in the beam forward lane and starts/updates the beam with `BeamWeapon3D.GetBeamForwardDirection()`
  - if the Splitter's beam muzzle is positioned correctly but its local forward axis is not the intended shot lane, assign `BeamWeapon3D.Direction Reference` to a clean forward-facing child transform; the brain, `ForgeEnemyBeam3D`, and `LaserBeam3D` will all use that direction source while the muzzle remains the origin
  - parent hybrids do not automatically fall back to projectile fire after choosing beam; use `Log Weapon Choices` to see whether a beam choice was blocked by aim, energy, or weapon setup
  - projectile mode supports an authored convergence distance so each muzzle aims toward the same point ahead of the Splitter instead of firing parallel from wide hardpoints
  - subscribes to `Enemy3D.Died` and runs splitting only on the spawn-authority side, then delegates child instantiation/tracking to `InvasionWaveManager3D`
  - spawns the same prefab, not separate child prefabs; configure `Splitter Prefab` as a self-reference to the authored Splitter prefab
  - spawned child `0` becomes beam-only and child `1` becomes projectile-only; each child disables the weapon it is not allowed to use, applies the configured child scale/speed, and overrides max health/shield through `Entity3D.OverrideMaxHealthAndShield(...)`
  - child role/scale/health are applied before network spawn on the server and synchronized to clients for presentation, while movement, targeting, attacks, networking, and wave death tracking remain on the usual Invasion components
- `DuelistEnemyBrain3D`
  - mid-tier flanker enemy that hangs in the player's mid-range pocket and juggles three weapons one at a time
  - kit is `ProjectileWeaponEnemy3D` (close), `MissileWeaponEnemy3D` (mid), and `BeamWeapon3D` (long); the brain picks the highest-scoring weapon for the current range with a small `vibesChance` random pick over the rest of the valid set so the choice is not perfectly deterministic
  - explicitly does NOT fire multiple weapons in parallel: when the chosen weapon for the current think tick is not the beam, any active beam is stopped first
  - holds at 100-200m by default and uses a perch loop similar to `GlassCannonInterceptorEnemyBrain3D`, but the perch picker scores candidate directions and applies a `forwardArcAvoidanceWeight` penalty to perches inside the target's forward cone so the duelist drifts toward the player's flanks/rear instead of perching head-on
  - while engaging, drives `EnemyStrafeMover3D` to slide laterally (with a small vertical tilt) at `orbitStrafeSpeed`; this is real world-space strafe motion that runs while `EnemyAIFlightController3D` keeps the nose locked on the target
  - reacts to incoming player fire: every `threatScanInterval` it does a non-alloc `OverlapSphere` against `projectileLayers`, filters for `Projectile3D.TargetFaction == EnemyTeam` (i.e. fired by the player team), keeps only projectiles whose velocity is heading at the duelist (dot >= `threatHeadingDotThreshold`), and rolls `dodgeChancePerThreat` to trigger a perpendicular `EnemyStrafeMover3D` dodge burst at `dodgeSpeed` for `dodgeDuration`
  - dodges are gated by `dodgeCooldown` so the duelist cannot chain-dodge a stream of fire, and weapon fire is suppressed for the dodge window so the dodge tell stays readable
  - beam path mirrors `SplitterEnemyBrain3D` (`GetBeamForwardDirection` + `GetBeamOrigin` for the precise aim check, `NetEnemyCombat3D.SetBeamState` / `UpdateBeamAim` for replication)
  - there is no projectile-warning sensor today; the threat scan is folded into the brain. If a second enemy needs the same behavior, extract the scan into a reusable `IncomingProjectileSensor3D` component then
- `TriumvirateEnemyBrain3D`
  - coordinated low-health beam enemy intended to spawn in groups of three
  - the lowest-instance surviving member acts as the server-authoritative coordinator and directly assigns formation/facing intents to the surviving squad
  - each member can declare a fixed `Formation Slot Preference` (`Top`, `LowerLeft`, or `LowerRight`); `Auto` members are assigned a stable open slot by the coordinator so the triangle does not reshuffle while the ships are moving
  - the squad first moves into a small triangle formation around its current group center, holds briefly, then reveals cosmetic lightning links in order: member `0 -> 1`, `1 -> 2`, `2 -> 0`
  - each member can bind a `ProjectileChargeTelegraph3D` as `Charge Telegraph`; the coordinator plays it across the link sequence and final charge delay so all surviving ships brighten while preparing the converged beam, then restores their authored idle emission/light baseline when charging ends
  - when using auto-collected renderers for this telegraph, keep `Only Affect Authored Emission Renderers` enabled or explicitly assign only the glow-capable hull renderers so shield/transparent effect materials are not touched
  - formation approach faces each member toward its assigned slot until it arrives, then turns toward the player; this matches `EnemyAIFlightController3D`'s forward-move contract and prevents local test scenes from freezing after target acquisition
  - uses a compact vertical two-low / one-high triangle by default; tune `Vertical Triangle Width` and `Vertical Triangle Height` for the intended read, and leave `Anchor Formation Near Current Squad` enabled unless the squad should intentionally relocate to a fixed target distance before linking
  - final beam fire starts from every surviving member and converges on the target; damage is divided per emitter so the configured one/two/three-member DPS remains the total squad damage
  - `Log State Changes` reports state transitions and major milestones, while `Log Formation Progress` reports repeated slot-distance diagnostics during setup testing
  - if one or two members die before the final beam, the remaining members continue the pattern with fewer links and lower final beam damage; only the full three-member beam applies the configured slow
  - final player-facing damage is owned by the brain's non-alloc beam cast so one/two/three survivor strength can be tuned independently; configure the `BeamWeapon3D` visual beam damage to `0` when the brain owns damage
  - `Squad Members` may be authored directly, but the brain can also auto-link to the closest same-key Triumvirate members within `Auto Link Radius`
- `SwarmScoutEnemyBrain3D`
  - fragile fast flyer intended to spawn in organized groups of five
  - auto-links nearby scouts with the same `Swarm Key`, assigns stable phase slots, and uses the `Movement Pattern` dropdown to choose between the current `FormationFlyby` pass and the original `OrbitHelix` fallback
  - `FormationFlyby` keeps the scouts in a rolling polygon around an empty center, drives that formation center through/past the player, then starts another pass so the player can thread the middle while shooting the ships down
  - `OrbitHelix` preserves the earlier constant-speed orbit/corkscrew around the player target
  - has no weapons, ram, or contact damage; its threat is informational, not direct damage
  - if the linked swarm keeps at least `Required Survivors For Alert` alive near the player for `Alert Warmup Seconds`, it calls `EnemyTargetSensor3D.ReceiveTargetAlert(...)` on enemy sensors near the player so heavier enemies can acquire beyond their normal detection radius for a short duration
  - alerts are server-authoritative in networked Invasion because the brain only runs on the server/host; clients receive movement through `NetEnemyMovement3D`

## Architecture Rules

- AI brains decide intent; `EnemyAIFlightController3D` owns the enemy Rigidbody movement response.
- Enemy movement should not mirror player input, player prediction, visual tilt, or camera-driven flight feel by default.
- The default enemy movement contract is intentionally blunt: choose a world-space direction, rotate the enemy nose toward it, and set velocity to `transform.forward * speed` once the enemy is facing that direction.
- Do not preserve sideways drift, acceleration curves, player-style look input, or inherited velocity in the baseline enemy motor.
- Basic shooter pursuit may scale speed down near the target, but facing gates belong in `EnemyAIFlightController3D`.
- Enemy brains that need kiting must be able to declare facing and movement separately. Artillery enemies need to keep the nose on target while translating backward, so the flight controller now supports a "face here, move there" contract instead of assuming every enemy always flies nose-first.
- Target selection should use `FactionMember3D`, not generic `"Player"` tag lookups.
- Temporary target sharing should go through `EnemyTargetSensor3D.ReceiveTargetAlert(...)` so normal detection remains the primary path and alert-based acquisition expires automatically.
- Obstacle avoidance should use batched/non-alloc physics queries where possible.
- Patrol/search behavior should be procedural and per-enemy, not a manually authored shared waypoint list. Shared waypoint lists create long-term traffic hotspots in a large 3D volume.
- Patrol should only run as a no-target fallback. Once `EnemyTargetSensor3D` reacquires a player, the enemy's normal brain should immediately take movement and combat ownership back.
- Do not put wave logic, scoring, or mode state inside individual enemy brains.
- Do not make clients run gameplay AI in networked Invasion.
- Every new active 3D enemy prefab needs the matching `EnemyBalanceProfile3D`-derived asset and an `EnemyBalanceProfileApplier3D` reference on the prefab root. Tune extracted balance numbers in the profile: health, shield, move speed, turn speed, detection range, weapon damage/speed/cooldowns/lifetime/beam energy, and that enemy's active brain behavior timings/ranges.
- Keep prefab wiring on prefab inspector fields: projectile/beam prefab references, muzzles, visuals, layers, audio, pooling, debug toggles, patrol bounds, network references, and other scene/object bindings stay out of balance profiles.

## Pathing Model

Current pathing is free-flight steering:

- steer toward the desired target direction
- probe forward and along angled whiskers
- bias away from obstacle hits
- preserve 3D climb/dive movement
- feed the final world-space direction to the simple enemy flight motor

Do not use Unity NavMesh for the current space-flight enemy path unless the mode intentionally changes to constrained lanes, surfaces, or volumes.

Planned later pathing layers may include:

- authored spawn lanes
- formation anchors
- tactical orbit/kite positions
- flow-field or waypoint goals for large waves

Local separation between enemies is now implemented as `EnemySeparation3D` (currently consumed by `RammerEnemyBrain3D`); other brains can opt in by adding the component to their prefab and routing their desired steering vector through it the same way they route through `EnemyObstacleAvoidance3D`.

## Old Stellar Onslaught Inspiration

The old backup scripts are useful for behavior vocabulary, not direct code reuse:

- `BasicEnemyScript`
  - simple direct pursuit and basic firing
  - maps to the first `BasicShooterEnemyBrain3D`
- `ArtilleryEnemyScript`
  - kites at range, checks line of sight, fires beams
  - the current 3D equivalent is `ArtilleryBeamEnemyBrain3D`, which keeps the old behavior vocabulary but reuses shared 3D flight, targeting, and beam runtime instead of bundling movement/combat/audio into one script
- `SuicideEnemyScript`
  - commits to pursuit after detection and explodes on contact/low health
  - the current 3D equivalent is `SuicideDroneEnemyBrain3D`; keep it simple and server authoritative instead of porting the old all-in-one enemy script shape
- `BulletHellEnemyScript`
  - cycles projectile/beam patterns and can spin/freeze during attacks
  - should become pattern controller modules, not a single massive class
- `BossBulletHellScript`
  - demonstrates attack phases, vulnerability windows, and pattern sequencing
  - future bosses should use composable phase/pattern modules with server-owned timing

The important lesson from the old project is that behavior, stats, movement, audio, and attacks were too tightly bundled. In 3D Invasion, keep those responsibilities split so many enemy types can share sensors, steering, and attack modules.

## Networking Rules

In networked Invasion:

- only the server runs target selection and enemy brains
- only the server simulates enemy movement truth
- clients interpolate replicated enemy movement
- only the server applies projectile damage
- clients may show cosmetic enemy projectile spawns

If an enemy attack needs special replication later, add an enemy-specific network path or generalize the broker deliberately. Do not route enemy PvE behavior through player-owner-only combat code.
