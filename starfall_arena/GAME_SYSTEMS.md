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

Stats note:

- match stats are now authoritative-damage based rather than projectile-payload based
- `damageDealt` and `damageTaken` are recorded from the actual shield-plus-hull damage that was successfully applied
- in network play, combat stats should only be mutated on the server-authoritative gameplay path

Input note:

- controller-first design remains the default
- keyboard-and-mouse support should still be maintained
- future movement/input changes should avoid silently breaking either path

### Ship Classes

`Class1` is currently the simplest concrete player class and inherits almost all live behavior from `Player` plus attached abilities.

`Class1` now exposes its primary-fire cooldown through a class-local serialized backing field and applies that value into the shared `Player` runtime field during `Awake()`. This matches the safer ship-class tuning pattern already used by `Class2`.

`Class2` now also uses the modular ability-component pattern, with its empowered shot, shield, tractor beam, and physical projectile implemented as separate scripts under `Assets/Scripts/Abilities/class2` while the ship class keeps only its custom primary-fire convergence and slot-4 lock behavior.

`Class3` exists in the repo and should be treated as real ship-class architecture, but the current docs should center on the duel systems that are active now rather than implying every class is equally production-complete.

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

Networked duel note:

- in network sessions, projectile visuals can exist as cosmetic-only local or remote instances
- only the server-authoritative projectile instance should decide real hits, damage, slow application, and reflection outcomes
- projectile hit validation now uses recent network movement history with a short defender-favored rewind cap instead of trusting local client trigger collisions

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

Networked duel note:

- beam start and stop still appears immediately for the owning player
- real beam damage now only comes from the server-authoritative beam instance
- cosmetic beam instances on clients are visual-only and should not apply damage or force

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
- `ConvergeBeam`
  - sustained multi-beam weapon that re-aims its convergence point from live look input every frame instead of locking to ship-forward at activation time
  - in network play, the owning client must keep sending the live aim direction while the beam is held, and server/remote copies must derive convergence from the latest replicated direction so authoritative damage and visuals keep following the current target instead of the initial fire point
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

Networked combat note:

- `GigaBlast` projectile release now follows the same network split as primary projectiles: immediate local cosmetic shot, server-authoritative gameplay shot
- `FireWall` hazards now treat the server-spawned hazard as gameplay truth in network play; remote client hazards are cosmetic-only
- `Reflector` activation is now forwarded to the server in network sessions so reflection decisions happen on the authoritative side
- `TriggerBomb`, `FaerieShift`, and `Invisibility` now also route through the network combat/state framework so Class3 ability state is no longer local-only during network play
- combat accuracy now uses per-attack rules instead of per-projectile rules:
  - Class1 counts primary-fire volleys and `GigaBlast`
  - Class1 `Beam` damage contributes to damage stats but does not affect accuracy
  - Class2 counts primary-fire volleys, `EmpoweredShot`, and `PhysicalProjectile`
  - Class3 counts primary-fire volleys and `TriggerBomb`
  - multi-projectile volleys count as one attack, and any hit within that volley awards the full hit credit
- reflected projectiles now transfer damage credit to the reflecting player, but they should not inherit the original projectile's accuracy credit
- Bug note: `Class3_Player.prefab` friction tuning must stay aligned with the shared `Player` friction system. If `frictionDelay` or `frictionDeceleration` are left at zero, the Class3 friction toggle will look broken even when the network/input code is correct.
- Bug note: ship classes must not declare a second serialized field named `fireCooldown` when `Player` already owns the runtime cooldown field. Unity can report "The same field name is serialized multiple times" for that shadowed-name pattern, which breaks inspector editing. Use a class-local backing field such as `_fireCooldown` and copy it into `Player.fireCooldown` in `Awake()`.
- Bug note: networked piercing projectiles must suppress repeat hits on the same target for the lifetime of that flight. Local play relies on `OnTriggerEnter2D`, but the network path uses repeated server-side sweeps against rewind history. Without a per-projectile hit registry, a piercing `GigaBlast` can damage the same player on successive server ticks and look like an instant kill even though the configured single-hit damage is correct.
- Bug note: `ConvergeBeam` must compute its convergence from live aim input and replicate the live aim direction while held, not just reuse the first convergence point or `transform.up`. Reusing a fixed point or ship-forward makes the beams feel sticky because networked server/remote copies keep firing at the initial target instead of the player's current controller aim.
- Bug note: `Invisibility` should explicitly hide and restore ship renderers during activation instead of relying on layer changes alone. The layer swap is still needed for targeting/filtering, but by itself it is not reliable enough as player-facing feedback.
- Bug note: `Invisibility` is enemy-facing concealment, not self-blindness. The owning player should keep seeing their own ship, and invisibility should immediately break when Class3 takes another offensive action such as primary fire, `FireWall`, `FaerieShift`, or `TriggerBomb`.
- reinforced hull now uses a separate `sizeScaleMultiplier` on the augment asset, so ship visual scaling can be tuned independently from the health bonus instead of inheriting `healthMultiplier`.

### Ability 4 Unlock Rule

The round flow currently locks `ability4` until a configured later round.

That means ability availability is not just per-ship data; it is also tied into duel progression.

Bug note:

- if `Class2` ability 4 appears nonfunctional while its other modular abilities work, check the round lock first; `GameSceneManager` still keeps slot 4 locked until the configured unlock round unless a test scene explicitly calls `UnlockAbility4()`

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
- runtime-driven presentation effects (attached prefabs and one-shot particles/sounds) for augments like Blaze of Glory, Cloak, Dagger, Evasion, Regenerator, Rotator, and Thorns
- Bubble Shield now supports an attached bubble prefab that shrinks toward a configurable minimum scale as anchored mitigation approaches stun threshold, a configurable block sound on mitigated hits, and shield-like damage-debt regen after a hit-free delay
- Burst now supports an attached speed-up prefab that is active during the burst window after contact-triggered activation
- Burner now supports a configurable burn-tick prefab spawned at random points on the burning target (renderer bounds first, radius fallback)
- AutoCounter now supports a configurable ready-glow prefab that is shown while the auto-counter window is active and the next incoming projectile can be reflected
- AutoCounter now also broadcasts reflected-projectile visuals through the shared network projectile-reflection path so remote clients see the reflected shot when the server confirms the augment proc
- Twin Fire now applies a persistent primary-damage reduction and schedules a delayed second primary volley per shot
- SoulBinding now transfers your recent health loss into nearby enemies as distance-scaled direct health damage
- MindBinding now listens for nearby enemy primary-fire events and mirrors them by firing your own primary volley
- BodyBinding now applies distance-scaled speed slow multipliers to nearby enemies based on proximity only, so slow player movement does not drop the effect
- Binding augments now share an arc-link visual architecture (per-augment settings plus runtime-driven owner/target anchor offsets and curved line points) so active nearby binds can show persistent light links between ships; BodyBinding now updates those visuals on every active client copy while keeping the slow application server-authoritative
- augment presentation prefabs now scale by `Player` ship size so ships with larger/smaller presentation scale keep augment VFX proportional
- reinforced hull presentation scaling where the ship visual scale tracks the same multiplier used for extra max health while the augment is active
- artificial fairy revival sequencing where lethal damage triggers a flash, temporary intangibility, ship-part scatter, and delayed part regroup to visually reassemble the ship

This means augments are already more than simple stat perks. They are event-driven gameplay modules with persistence across rounds.

### Round Persistence

The duel loop stores augment loadouts between rounds using `AugmentLoadoutEntry`.

Current augment flow:

- player gains augment
- controller creates runtime
- runtime is tagged with the round acquired
- round transitions export and import augment loadouts
- runtime activation can expire based on augment duration in rounds
- in the active network duel flow, augment choice is simultaneous instead of sequential
- the losing player gets 3 choices
- the winning player gets 2 choices
- each player can receive a separate local pool for the same augment phase

Network persistence note:

- the repo now also has a minimal `NetworkAugmentLoadoutEntry` shape that carries stable augment ID plus round acquired for network-safe export/import
- networked player copies now also receive replicated augment loadouts from the authoritative server after spawn and whenever a live player gains a new augment
- the network-safe loadout still stays narrower than the local `AugmentLoadoutEntry`, but it now includes explicit state flags for supported one-shot runtimes such as `ArtificialFairy`
- augments with richer mutable runtime state still need explicit serializer support before they can be treated as fully network-persistent across all networked round transitions

Network execution note:

- augment gameplay remains server authoritative
- owner clients and remote proxies still need local runtime instances for HUD, stat, and presentation consistency
- augments that use `ExecuteEffects()` must keep working when `Player` is disabled on non-owner network copies, because `NetMovement` now manually ticks augment runtimes on those copies
- augments that react to taking damage should assume the real damage event happens on the server; client copies are refreshed from the authoritative combat/state sync path rather than from local `TakeDamage(...)`
- successful evade presentation is now replicated through the authoritative combat broadcast path so non-server clients trigger Evasion flash/sound only when the server actually ignored damage
- artificial fairy revival presentation is also replicated through authoritative combat broadcast flags so remote clients run the same revive scatter/regroup sequence when the server confirms the augment proc

## Known Architectural Notes

- The game is currently duel-first, even though enemy and broader PvE architecture still exists.
- The active direction is a fully networked duel game; local split-screen behavior should be treated as legacy/secondary unless explicitly needed.
- The camera is still described in many places as orthographic, but future work should keep perspective-camera migration in mind.
- Weapon docs must distinguish between projectile-family weapons and beam weapons.
- Ability behavior is component-based and distributed, so changes often require docs updates in both gameplay and UI/flow docs.
- Bug note: modular abilities should initialize their cooldown timer in a ready state. If the base `Ability.lastUsedAbility` starts at `0`, newly spawned ships can have abilities appear broken until each cooldown elapses once.
- Bug note: teleport-style abilities that hide renderers during their effect need an interruption-safe restore path, or a ship can remain hittable while visually invisible if the coroutine is stopped mid-ability.
- Bug note: Chrono Step waypoints must be cleared on round transitions. The component now clears state in `OnDisable()` to avoid leaving waypoint markers between rounds; if markers persist, ensure the ability component is disabled during round freeze.
- Bug note: if duel stats ever start diverging again, check for damage sources bypassing `Entity.TakeDamage(...)` / `TakeDirectDamage(...)` without passing the attacking player through. That was the root cause of mismatched dealt-vs-taken totals and missing ability/reflection credit.
- Bug note: Artificial Fairy regroup visuals can appear oversized if ship parts only restore local scale after reparenting. Preserve each part's original world scale and compute parent-relative local scale on return so reassembled parts match the ship's original size.
- Bug note: when adding thruster particles to the future-facing `Assets/Scripts/3d/Movement3D.cs` path, cache each particle system's original emission/speed/lifetime and drive them from live thrust input. If the 3D ship thrusters look permanently on or refuse to restart cleanly, the usual cause is treating them as static VFX instead of maintaining a thrust-driven intensity/play-stop state.
- `Assets/Scripts/3d` is future-facing groundwork for a more fully 3D version of the game, not a current core maintenance area.
- Bugs in combat, ability timing, or augment behavior should be added here in the relevant section once discovered.
