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
- `BasicShooterEnemyBrain3D`
  - first Invasion enemy behavior
  - directly pursues the nearest player
  - slows/stops near the target instead of overshooting into long orbit loops
  - fires its projectile weapon when aimed and off cooldown
  - should use `ProjectileWeaponEnemy3D` for direct-fire guns so the AI path stays muzzle-forward and avoids player-only weapon overhead
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
- `SuicideDroneEnemyBrain3D`
  - dedicated detonation behavior for kamikaze enemies
  - always commits to the nearest player at full speed instead of using range management
  - detonates on contact or very close proximity so high-speed impacts do not depend entirely on a single collision callback
  - applies damage through `Entity3D` on the authority side, then kills itself through the normal enemy death path
- `TankEnemyBrain3D`
  - slow, high-HP heavy that lumbers toward the player and holds at a wide stop band (default 35m) instead of kiting or rushing in
  - reuses the basic shooter speed-scale shape; turn rate is intentionally tuned slow on the prefab's `EnemyAIFlightController3D` so players can flank
  - drives two independent enemy-only weapons: a `ProjectileWeaponEnemy3D` slow heavy cannon (tight aim tolerance, short cooldown) and a `MissileWeaponEnemy3D` homing launcher whose projectile prefab is a `MissileProjectile3D` (wide aim tolerance, long cooldown)
  - each weapon's own cooldown still gates fire rate, but the brain also applies a small cross-weapon stagger delay after a successful shot so the cannon and missile launcher do not both dump on the same frame when both are ready
  - intentionally cheaper than the artillery brain: no line-of-sight raycast, no per-frame aim refresh, no beam state machine
- `RammerEnemyBrain3D`
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

## Architecture Rules

- AI brains decide intent; `EnemyAIFlightController3D` owns the enemy Rigidbody movement response.
- Enemy movement should not mirror player input, player prediction, visual tilt, or camera-driven flight feel by default.
- The default enemy movement contract is intentionally blunt: choose a world-space direction, rotate the enemy nose toward it, and set velocity to `transform.forward * speed` once the enemy is facing that direction.
- Do not preserve sideways drift, acceleration curves, player-style look input, or inherited velocity in the baseline enemy motor.
- Basic shooter pursuit may scale speed down near the target, but facing gates belong in `EnemyAIFlightController3D`.
- Enemy brains that need kiting must be able to declare facing and movement separately. Artillery enemies need to keep the nose on target while translating backward, so the flight controller now supports a "face here, move there" contract instead of assuming every enemy always flies nose-first.
- Target selection should use `FactionMember3D`, not generic `"Player"` tag lookups.
- Obstacle avoidance should use batched/non-alloc physics queries where possible.
- Do not put wave logic, scoring, or mode state inside individual enemy brains.
- Do not make clients run gameplay AI in networked Invasion.
- Keep tuning on prefab inspector fields for now. ScriptableObject enemy profiles are not part of the first slice.

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
