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
- `BasicShooterEnemyBrain3D`
  - first Invasion enemy behavior
  - directly pursues the nearest player
  - slows/stops near the target instead of overshooting into long orbit loops
  - fires its projectile weapon when aimed and off cooldown
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
- local separation between enemies

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
