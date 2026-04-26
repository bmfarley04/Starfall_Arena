# 3D_AI.md

This document owns 3D AI architecture and enemy behavior notes.

The 3D AI path should stay modular. Enemy prefabs should compose small scripts instead of growing a single inherited enemy monolith.

## Current AI Components

- `Enemy3D`
  - enemy entity coordinator
  - caches enemy-only systems and connects AI input to `ShipFlight3D`
  - should stay narrow
- `EnemyAIFlightController3D`
  - implements `IShipFlightInputSource`
  - stores look/thrust intent supplied by a brain
  - contains no targeting, pathing, or combat decisions
- `EnemyTargetSensor3D`
  - periodically selects the nearest visible target in a configured faction
  - current basic shooter target faction is `PlayerTeam`
  - uses obstacle line-of-sight blockers instead of tags
- `EnemyObstacleAvoidance3D`
  - adjusts a desired free-flight direction using non-alloc spherecast probes
  - intended for asteroids and world geometry
  - this is steering/avoidance, not Unity NavMesh
- `BasicShooterEnemyBrain3D`
  - first Invasion enemy behavior
  - directly pursues the nearest visible player
  - has no preferred combat range
  - fires its projectile weapon when aimed and off cooldown

## Architecture Rules

- AI brains decide intent; flight controllers only expose intent to `ShipFlight3D`.
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
  - should become separate range-management and beam-attack modules later
- `SuicideEnemyScript`
  - commits to pursuit after detection and explodes on contact/low health
  - should become a dedicated detonation brain plus explosion damage module
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
