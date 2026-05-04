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
  - adjusts a desired free-flight direction with a simple forward-blocked steering filter
  - uses one non-alloc forward spherecast along the requested movement direction, then samples right/left/up/down escape candidates only when forward travel is blocked
  - blends the chosen escape side back into the original desired direction so enemies keep trying to reach the player instead of switching into full pathfinding behavior
  - briefly holds the chosen escape direction to prevent centered asteroids or world geometry from causing left/right/up/down jitter
  - smooths into blocked avoidance and smooths back out when forward travel clears, so direct-chase enemies arc around obstacles instead of snapping through chevron-shaped steering changes
  - intended for asteroids and world geometry
  - this is steering/avoidance, not Unity NavMesh
  - prefab setup should keep obstacle layers limited to avoidable world geometry, not players, enemies, projectiles, or soft gameplay trigger volumes
- `EnemySteeringSmoother3D`
  - optional final steering polish component for enemies that already compute a desired world-space movement direction
  - mirrors the `ResolveSteeringDirection(Vector3 desired)` API used by separation and obstacle avoidance
  - smooths direction changes only; it does not own speed scales, facing rules, tactics, or Rigidbody movement
  - intended chain for eligible direct-chase enemies is `desired -> separation -> obstacleAvoidance -> steeringSmoother -> flight controller`
  - first-pass intended users are `BasicShooterEnemyBrain3D`, `TankEnemyBrain3D`, `SplitterEnemyBrain3D` approach, and `FlamethrowerEnemyBrain3D` approach
  - keep tuning prefab-local for now through `Turn Smooth Time`, `Release Smooth Time`, and `Max Turn Degrees Per Second`; do not move these values into balance profiles until they prove broadly useful
  - do not add it by default to rammers, suicide drones, Triumvirate formation ships, bosses, or other enemies whose identity depends on committed straight-line or authored movement
- `EnemyVisualTilt3D`
  - enemy-native presentation tilt for ships moved by `EnemyAIFlightController3D`
  - reads enemy move/facing intent, Rigidbody velocity/acceleration, and optional `EnemyStrafeMover3D` velocity instead of relying on active `ShipFlight3D`
  - applies bank/pitch only to the configured visual model child; it must never rotate the enemy root or affect gameplay movement
  - intended v1 prefab users are direct-moving enemies such as Basic, Tank, Splitter, Flamethrower, Swarm Scout, and Gnat-style movers
  - use `EnemyVisualTilt3D` on enemies and keep `ShipVisualTilt3D` for player ships; do not keep both pointed at the same visual model
- `EnemySeparation3D`
  - optional enemy-only inter-agent separation steering helper that mirrors the `EnemyObstacleAvoidance3D` API (`ResolveSteeringDirection(Vector3 desired)`)
  - uses a static registry of enabled `EnemySeparation3D` components instead of physics overlap/layer-mask setup, so adding the component to an eligible enemy prefab is the opt-in switch
  - only considers living `Enemy3D` instances that also have `EnemySeparation3D`; it does not push away from players and should not be added to player prefabs
  - preserves distance falloff and smooths the separation vector over time so clustered enemies fan out without twitching when neighbors cross the edge of the separation radius
  - exposes four prefab-only tuning fields: `Separation Radius`, `Separation Strength`, `Vertical Weight`, and `Unstick Speed Scale`
  - direct-chase and hold-range brains can use its unstick helper to apply a very small movement nudge only when they would otherwise be stopped while overlapping another separated enemy
  - intended chaining: `desired -> separation -> obstacleAvoidance -> flight controller`
  - eligible non-boss brains include `BasicShooterEnemyBrain3D`, `TankEnemyBrain3D`, `SplitterEnemyBrain3D`, `ArtilleryBeamEnemyBrain3D`, `ArtilleryFortressEnemyBrain3D`, `DuelistEnemyBrain3D`, `FlamethrowerEnemyBrain3D`, `GlassCannonInterceptorEnemyBrain3D`, and `SwarmScoutEnemyBrain3D`
  - do not add it to `SuicideDroneEnemyBrain3D`, `RammerEnemyBrain3D`, `TriumvirateEnemyBrain3D`, or boss prefabs; those identities rely on committed collision pressure, authored formation slots, or boss-scale movement ownership
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
- `FlamethrowerEnemyBrain3D`
  - short-range pressure enemy that closes directly until it reaches its flame pocket, then keeps its nose on the target while using `EnemyFlamethrowerWeapon3D`
  - movement bands: no target uses `EnemyPatrol3D`; detected-but-far approaches; `20-30m` preferred range faces the player and can fire, with a small outer charge-start grace distance capped by the weapon range so it does not stall on the exact approach edge; inside the lower band backs away while still facing the player; cooldown continues the same range management without firing
  - while a flame burst is active, it layers a slow orbit through `EnemyStrafeMover3D` so the enemy can slide around the player without giving up its forward flame lane
  - optional `EnemySeparation3D` can bias approach/retreat away from allies, and optional `EnemyObstacleAvoidance3D` can route approach steering around asteroids/world geometry
  - `EnemyFlamethrowerWeapon3D` owns the authored particle visual and the server-authoritative cone DPS; clients only receive replicated visual state through `NetEnemyCombat3D`
- `GlassCannonInterceptorEnemyBrain3D`
  - fragile, high-pressure ranged enemy whose identity is a readable loop: `Reposition` -> `Settle` -> `Burst` -> `Recover`
  - chooses a new world-space perch around the current player target at roughly 40-50m, using lateral plus vertical bias so it relocates through the full 3D flight volume without becoming a noisy continuous orbit
  - stops moving during `Settle`, `Burst`, and `Recover`; those stationary beats are the intended player accuracy-check windows because the prefab should die to one player shot
  - fires short `ProjectileWeaponEnemy3D` bursts only while its nose is within a tight aim tolerance of the player; shots travel toward the player's current position, not with predictive lead, and do not preserve the residual tolerance angle as shot error
  - keeps movement simple and learnable by cycling perch direction deterministically instead of dodging individual player shots
  - should be tuned as a glass cannon on the prefab: very low `Entity3D` health, high `EnemyAIFlightController3D.moveSpeed`, fast turn rate, and a fast high-damage bolt weapon
- `RammerEnemyBrain3D`
  - fast strike enemy whose identity is committed straight-line charges with strong velocity knockback, not collision-avoidance steering
  - state machine: `Approach` -> `WindUp` -> `Charge` -> `Recover` -> `Approach`. Every transition is server-authoritative
    - **Approach**: directly flies toward the detected player at full speed; no obstacle avoidance, separation, or standoff drift is layered into the rammer brain
    - **WindUp**: triggered when inside `chargeStartDistance` and past `chargeCooldown`; the rammer only faces the live target for `windUpDuration`
    - **Charge**: locks one normalized direction from rammer to target at the end of wind-up, then flies that vector until collision, `chargeMaxDuration`, or passing the locked target point plus `chargeOvershootDistance`
    - **Recover**: keeps flying along the same locked charge vector briefly after hit or miss so the ship reads as a high-speed flyby, then returns to approach
  - hit detection is plain Unity collision/trigger contact during `Charge`; the old workaround stack of distance probes, overlap buffers, swept spherecasts, collision exemptions, and reverse-eject disengage has been removed now that the collider issue is fixed
  - on a successful ram hit, applies `ramDamage` and `NetMovement3D.ApplyCombatVelocityDelta(lockedChargeDirection * hitVelocity)` to the player when a network movement component exists; offline fallback adds the same velocity delta to the target Rigidbody
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
  - kit is `ProjectileWeaponEnemy3D` (close), `MissileWeaponEnemy3D` (mid), and `BeamWeapon3D` (long); the brain picks the highest-scoring weapon for the current range with a small `vibesChance` random pick over the rest of the valid set so the choice is not perfectly deterministic, then commits to that weapon for `weaponCommitDuration` before another valid weapon can replace it
  - explicitly does NOT fire multiple weapons in parallel: when the committed weapon is not the beam, any active beam is stopped first
  - holds at 100-200m by default and uses a hybrid flanker loop: the flight controller keeps the nose on the player while `EnemyStrafeMover3D` drives target-relative lateral/vertical movement toward changing flank/rear perches
  - stores perches as `perchDirectionFromTarget + perchRange` instead of one fixed world point, so the selected flank remains meaningful as the player moves through the arena
  - the perch picker scores candidate directions and applies a `forwardArcAvoidanceWeight` penalty to perches inside the target's forward cone so the duelist drifts toward the player's flanks/rear instead of perching head-on
  - preferred-band movement blends direct perch travel with an orbit/weave tangent through `perchMovementWeight`; when it reaches a perch it keeps moving with idle orbit/weave instead of becoming a stationary turret
  - if the player is detected but outside `preferredRangeMax`, it directly approaches at `outOfRangeApproachSpeedScale`; if the player is inside `preferredRangeMin`, it faces the player while backing away at `closeRangeRetreatSpeedScale`
  - can fire any currently valid committed weapon while repositioning so target acquisition or perch refresh does not create a several-second no-combat pause
  - while engaging, drives `EnemyStrafeMover3D` to slide laterally/vertically; this is real world-space strafe motion that runs while `EnemyAIFlightController3D` keeps the nose locked on the target
  - reacts to incoming player fire: every `threatScanInterval` it does a non-alloc `OverlapSphere` against `projectileLayers`, filters for `Projectile3D.TargetFaction == EnemyTeam` (i.e. fired by the player team), keeps only projectiles whose velocity is heading at the duelist (dot >= `threatHeadingDotThreshold`), and rolls `dodgeChancePerThreat` to trigger a perpendicular `EnemyStrafeMover3D` dodge burst at `dodgeSpeed` for `dodgeDuration`
  - dodges are gated by `dodgeCooldown` so the duelist cannot chain-dodge a stream of fire, weapon fire is suppressed for the dodge window so the dodge tell stays readable, and normal perch/weave movement resumes as soon as the dodge strafe duration ends
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
- `SiegeCarrierBossEnemyBrain3D`
  - slow/stationary second Invasion boss that acts like a Siege Carrier rather than a normal chaser
  - runs one random major attack pattern lane for the nearest active player-team target and only starts a second simultaneous lane while a second distinct active player target is detected
  - each lane can run lagging machine-gun rake, lagging beam convergence, formation missile salvo, helix spiral barrage, a two-beam lightning slow attack, or an enemy spawn wave; the two lanes must not choose the same pattern at the same time
  - groups Inspector tuning into serialized foldout sections for weapons, movement, sequencing, rake, beam convergence, lightning slow beam, and orbital pillars; removed fan/curtain and beam telegraph management so the boss prefab has fewer inactive fields to maintain
  - resolves lagging-rake aim independently per shot; the default is precise current/velocity lead fire, with optional history blending only when designers intentionally want a trailing-fire look
  - drives multiple `BeamWeapon3D` hardpoints for lagging beam convergence through indexed `NetEnemyCombat3D` beam replication; every active hardpoint aims from its own muzzle origin toward one slightly delayed target point
  - drives two assigned `BeamWeapon3D` hardpoints for the lightning slow attack; this pattern uses velocity lead plus a profile-tuned positional follow lag so the beams trail the target similarly to the beam fence, while the brain applies a short refreshed movement slow only when its own line-of-sight spherecast confirms a beam lane reaches the player
  - can drive `FormationMissileSalvoWeaponEnemy3D` as a single major pattern; budget cost should match the salvo missile count so simultaneous missile blooms stay performance-accountable
  - can drive assigned `EnemySpawnerWeapon3D` components as a single major pattern; each spawner owns its prefab/count/delay, while the brain only starts the sequence and waits for all active spawners to finish
  - starts orbital energy pillars once when phase two begins: authored sphere prefabs launch from the carrier face, settle into a horizontal ring around the boss with a target-facing escape gap, then transform into tall world-Y cylinders that grow upward and downward from each sphere while damaging player-team entities only on the server until the boss dies or despawns
  - supports either the preserved red/white V1 pillar visual or the blue/white V2 plasma visual; V2 prioritizes internal storm texture with generated fractal lightning channels, branch ribbons, and spark/glint billboards while leaving gameplay timing and server-authoritative damage on the boss brain/profile
  - uses `EnemyAIFlightController3D` only for range maintenance; turret/lane pressure is owned by the boss brain, not independent turret AI
  - movement bands: no target uses patrol/search; detected but beyond `preferredRangeMax` approaches; inside `preferredRangeMin` backs away; inside the preferred range band slowly drifts toward the selected player so the boss stays dynamic even when it starts already inside engagement distance
  - movement is plane-biased: the boss mostly preserves its starting horizontal plane and only follows target height by the serialized vertical-follow weight, while projectile and beam patterns still aim at the target's real world position
  - phase transitions use total durability, not hull alone: `phaseTwoHealthPercent` compares `(currentShield + currentHealth) / (maxShield + maxHealth)`, so shield-heavy carriers do not enter phase two early just because hull is already low
  - performance/readability rule: pattern intensity scales by cooldown multipliers across durability phases, not by silently raising the per-pattern projectile budget
  - exposes `Forced Pattern For Testing` on the brain component; leave it as `None` for normal random selection, or select one rotating attack while testing its prefab wiring, visuals, damage, and avoidance readability. Orbital pillars are intentionally excluded because they are a phase-transition layer, not a selectable attack. In two-player tests, a forced pattern only starts on a lane when the other lane is not already running that same pattern.

## Enemy Movement Range Design

Every future enemy brain needs an explicit movement answer for every target range band. Do not only define the range where the enemy attacks; the transitions before and after engagement are where enemies most often look broken.

Default range bands:

- **Outside detection radius, still inside the arena**
  - The enemy has no active player target from `EnemyTargetSensor3D`.
  - Default behavior should be procedural search through `EnemyPatrol3D`.
  - Attach and configure the `EnemyPatrol3D` component on enemies that should keep moving while players are outside detection. In a large arena, for example a `5000 x 5000 x 5000` volume with a `1000m` detection radius, this is the enemy's normal behavior across most of the arena.
- **Inside detection radius, outside engagement range**
  - The enemy has detected a valid player-team target, but the target is still beyond that enemy's usable weapon/engagement range.
  - Default behavior should be direct full-speed approach toward the player.
  - This approach band should not detour to tactical perches, orbit points, formation flourishes, or idle tells unless the enemy is intentionally a special case. The player has already been detected, so the enemy should visibly commit to closing the gap.
- **Outer engagement range**
  - Define whether the enemy starts firing immediately, charges a tell, settles into a formation, begins a beam lane, or continues closing to a more specific preferred range.
  - Keep weapon range separate from detection range. A large sensor radius is for acquisition; it should not accidentally become infinite attack permission.
- **Preferred range band**
  - Define the enemy's identity here: hold position, orbit, strafe, perch, circle, formation-link, anchor, kite, or pressure forward.
  - If the enemy uses lateral/vertical movement, decide whether that is real `EnemyStrafeMover3D` motion, nose-first flight through `EnemyAIFlightController3D`, or a custom state-machine movement.
  - Decide whether the enemy can fire while relocating inside this band. If not, document the intentional player-facing pause.
- **Inner range / too-close band**
  - Always decide what happens when the player gets inside the lower edge of the preferred range.
  - Valid answers include backing away while facing the player, sitting still and continuing to fire, switching to a close-range weapon, charging through, dodging sideways, disengaging, or intentionally accepting point-blank pressure.
  - Do not leave this band to whatever the perch/orbit/chase code happens to do. That creates indecisive movement and can suppress combat unexpectedly.

When implementing a new enemy brain, document the chosen movement behavior for each relevant band in this file under that enemy's component notes. If a band is intentionally unused, say so explicitly.

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
- probe forward for blockers, then sample simple right/left/up/down escape candidates when blocked
- blend and smooth the selected escape side back into the desired target direction so obstacle routes read as arcs, not hard chevrons
- optionally pass the final steering vector through `EnemySteeringSmoother3D` on direct-chase prefabs that should arc more naturally during normal pursuit
- preserve 3D climb/dive movement
- feed the final world-space direction to the simple enemy flight motor

Do not use Unity NavMesh for the current space-flight enemy path unless the mode intentionally changes to constrained lanes, surfaces, or volumes.

Planned later pathing layers may include:

- authored spawn lanes
- formation anchors
- tactical orbit/kite positions
- flow-field or waypoint goals for large waves

Local separation between enemies is implemented as `EnemySeparation3D`; eligible brains can opt in by adding the component to their prefab and routing their desired steering vector through it before `EnemyObstacleAvoidance3D`. The component is registry-based, enemy-only, and prefab-tuned; do not add it to suicide drones, rammers, Triumvirate formation ships, bosses, or player prefabs.

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
