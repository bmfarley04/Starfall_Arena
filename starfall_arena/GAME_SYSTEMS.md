# GAME_SYSTEMS.md

This document summarizes the current gameplay architecture and the main combat systems implemented in code.

## Game Summary

Starfall Arena is currently being built as a two-player space dueling game.

The presentation is currently 2.5D:

- ships and effects are 3D assets
- gameplay motion happens in a 2D combat plane
- `Rigidbody2D` drives physics
- the camera is currently orthographic

The project is expected to transition toward a perspective camera later, so visual and camera assumptions should be treated as evolving.

The active product direction is:

- fully networked two-player duels
- PC/computer support
- PS4/controller-oriented support

Older local multiplayer and split-screen assumptions still exist in the codebase, but they are no longer the primary design target.

## Entity Hierarchy

Core gameplay still follows the shared entity base-class structure:

```text
Entity
  ├─ Player
  │   ├─ Class1
  │   ├─ Class2
  │   └─ Class3
  └─ Enemy
      └─ FighterEnemyScript
```

### Entity

`Entity` is the shared gameplay base for ships and damageable combat actors.

It owns:

- health and shield state
- shield controller reference
- movement config
- primary projectile weapon config
- common VFX and thruster setup
- damage handling
- slow effects
- shared augment modifier dictionaries

Important design note:

- `Entity` is where core combat and movement stats live
- `Player` adds controller input, HUD, feedback, and player-only gameplay behavior
- `Enemy` exists as a reusable architecture path, but enemies are not the current live gameplay focus

### Player

`Player` extends `Entity` with:

- Input System integration
- controller-driven thrust and look input
- shield regeneration
- friction behavior
- HUD binding
- audio pooling
- stats tracking
- ability slots `ability1` to `ability4`
- an `externalMovementControl` handoff used by networking

The player base is the true center of current duel gameplay.

Input note:

- controller-first design remains the default
- keyboard-and-mouse support should still be maintained
- future movement/input changes should avoid silently breaking either path

### Ship Classes

`Class1` is currently the simplest concrete player class and inherits almost all live behavior from `Player` plus attached abilities.

`Class2` and `Class3` exist in the repo and should be treated as real ship-class architecture, but the current docs should center on the duel systems that are active now rather than implying every class is equally production-complete.

### Enemy

`Enemy` extends `Entity` with:

- detection and pursuit settings
- combat aiming settings
- audio setup for enemy actions
- state-based AI scaffolding

This architecture is still useful reference context, but enemies are not the current gameplay focus. The active game loop is two-player dueling, not PvE waves.

## Combat Architecture

Combat is split between:

- shared ship damage and shield rules in `Entity`
- player-only input and ability flow in `Player`
- projectile prefabs in `Projectiles`
- ability implementations in `Abilities`
- augment-driven modifiers in `Augments`

### Damage Model

Current damage sources are:

- `Projectile`
- `LaserBeam`
- `Explosion`
- `Other`

General damage flow:

- most incoming damage goes through `Entity.TakeDamage(...)`
- shields absorb damage first unless a system explicitly bypasses shields
- hull health is reduced after shields are depleted or bypassed
- direct-health effects use `TakeDirectDamage(...)`
- death eventually routes through `Die()`

### Shields

Shields are separate from health and support:

- current shield pool tracking
- shield regeneration behavior
- shield-hit visual feedback
- beam-hit ripple behavior
- reflect-shield interactions

## Weapon Architecture

The high-level summary is:

- the primary weapon path is projectile-based
- projectile prefabs share a common base class
- continuous beams are a separate weapon path and do not derive from `ProjectileScript`

### Projectile Family

All standard projectile-style weapons derive from `ProjectileScript`.

`ProjectileScript` provides:

- velocity-based initialization
- target-tag ownership
- shooter tracking
- lifetime handling
- impact force application
- collision and damage dispatch
- optional piercing
- optional slow application
- reflection state handling

This is the main reusable base for projectile weapons.

#### PhysicalProjectile

`PhysicalProjectile` inherits from `ProjectileScript` but changes the damage rule:

- it bypasses shields
- it applies direct hull damage via `TakeDirectDamage(...)`

This means projectile behavior is not a single ruleset. “Projectile” in this repo really means “shared moving-hitbox weapon family,” with subclasses able to change damage semantics.

### Laser / Beam Path

`LaserBeam` is its own continuous beam system and is not part of the projectile inheritance chain.

Key differences from projectiles:

- uses a `LineRenderer`
- raycasts every frame while firing
- deals damage over time
- applies impact force continuously
- manages muzzle and impact FX separately
- has special shield ripple timing behavior
- can be stopped by valid blockers without behaving like a spawned projectile body

That distinction is important: projectile docs and beam docs should stay separate.

### Current Note On Future Weapons

There is already evidence that the projectile family will continue to branch:

- `PhysicalProjectile` exists today
- another branch has a missile implementation planned or in progress

Future docs should keep the pattern as:

- base projectile family behavior
- per-projectile subclass behavior

instead of flattening everything into one generic projectile description.

## Ability System

Abilities are implemented as `MonoBehaviour` components derived from the base `Ability` class and mounted directly on player ships.

### Base Ability Architecture

`Ability` provides:

- shared cooldown and duration data
- `TryUseAbility(...)` gatekeeping
- `UseAbility(...)` override point
- ability lock support
- checks for whether another ability is active
- hooks for movement/damage/collision modification
- HUD state methods like cooldown fill and resource fill

The player exposes four slots:

- `ability1`
- `ability2`
- `ability3`
- `ability4`

`Player` forwards input callbacks to these ability components directly.

### Current Ability Pattern

Abilities are not purely data-driven yet. The pattern is:

- ship has concrete ability components attached
- `Player` holds references to the four components
- editor tooling helps assign those components
- each ability class owns its own config struct, runtime state, FX, and cooldown logic

This is flexible, but it also means behavior is distributed across several concrete scripts rather than centralized in a single ability manager.

### Current Implemented Ability Types

Current ability scripts in the repo include:

- `Beam`
- `Reflector`
- `Teleport`
- `GigaBlast`
- `FireWall`
- `FaerieShift`
- `Invisibility`
- `TriggerBomb`

The current duel-oriented documentation should especially treat these as established patterns:

- `Beam`
  - sustained beam weapon
  - uses a capacity/overheat style resource instead of a simple cooldown-only model
  - reduces rotation while active
- `Reflector`
  - timed reflect shield
  - modifies projectile collisions
  - can reflect projectiles back to the opposing target tag
- `Teleport`
  - short-range reposition
  - temporarily disables collider during teleport execution
  - coordinates VFX, audio, and camera warp handling
- `GigaBlast`
  - charge-and-release projectile attack
  - multiple charge tiers
  - movement penalties while charging
  - tier-based projectile scaling and optional piercing at higher tiers

### Ability 4 Unlock Rule

The round flow currently locks `ability4` until a configured later round.

That means ability availability is not just per-ship data; it is also tied into duel progression.

## Augment System

The augment system is important enough to treat as a first-class subsystem.

### High-Level Model

Augments use a two-layer architecture:

- `Augment` ScriptableObjects are authoring definitions
- `IAugmentRuntime` instances hold per-player runtime behavior

This is a strong separation and should be preserved.

### Authoring Layer

`Augment` assets contain:

- icon
- name
- description
- stable generated ID
- round duration

Important rule:

- augment assets must not hold per-player mutable runtime state

### Runtime Layer

`AugmentController` lives on the player side and owns active runtimes.

It is responsible for:

- creating runtime instances from augment definitions
- importing and exporting per-round loadouts
- relaying events such as damage, direct damage, and contact
- executing ongoing effects each tick
- resetting modifier dictionaries when loadouts are rebuilt

`AugmentRuntimeBase` supplies shared runtime behavior such as:

- round-based activation windows
- persistent-state restore hooks
- helper methods for adding and removing stat multipliers

### Current Runtime Pattern

Current augment runtimes include behaviors such as:

- damage boosts under health thresholds
- shield restoration triggers
- temporary speed or damage boosts after taking damage
- evasion chances that ignore shield or health damage
- anchored healing over time
- max health increases
- rotation and thorn-style contact behavior
- fairy and augment-enhancement style effects

This means augments are already more than simple stat perks. They are event-driven gameplay modules with persistence across rounds.

### Round Persistence

The duel loop stores augment loadouts between rounds using `AugmentLoadoutEntry`.

Current augment flow:

- player gains augment
- controller creates runtime
- runtime is tagged with the round acquired
- round transitions export and import augment loadouts
- runtime activation can expire based on augment duration in rounds

## Known Architectural Notes

- The game is currently duel-first, even though enemy and broader PvE architecture still exists.
- The active direction is a fully networked duel game; local split-screen behavior should be treated as legacy/secondary unless explicitly needed.
- The camera is still described in many places as orthographic, but future work should keep perspective-camera migration in mind.
- Weapon docs must distinguish between projectile-family weapons and beam weapons.
- Ability behavior is component-based and distributed, so changes often require docs updates in both gameplay and UI/flow docs.
- `Assets/Scripts/3d` is future-facing groundwork for a more fully 3D version of the game, not a current core maintenance area.
- Bugs in combat, ability timing, or augment behavior should be added here in the relevant section once discovered.
