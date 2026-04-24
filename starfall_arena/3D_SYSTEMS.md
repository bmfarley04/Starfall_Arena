# 3D_SYSTEMS.md

This document records the current system-level structure of the 3D implementation path.

Read this when working on 3D movement, camera behavior, presentation, folder layout, or cross-system implementation rules inside `Assets/Scripts/3d`.

## Current 3D Direction

The current 3D effort should be treated as an implementation track for the same game, not as a separate game mode.

The intended direction is:

- move from orthographic 2.5D presentation toward a perspective-driven 3D experience
- preserve combat readability and fast duel clarity
- preserve current game logic where possible
- replace only the assumptions that are specifically tied to the 2D plane

Current likely focus areas include:

- 3D ship movement and steering
- camera framing and perspective readability
- translating thrust, banking, pitch, and visual feedback into 3D
- adapting aiming, target readability, projectile readability, and combat spacing
- determining which existing 2D physics assumptions can remain and which need true 3D replacements

## Relationship To Shared Docs

`GAME_SYSTEMS.md`, `UI_MANAGERS.md`, and `NETWORK.md` still describe the current live game.

This document describes how the 3D implementation is currently structured so those shared systems can be adapted without losing track of 3D-only constraints.

If a 3D change directly alters shared gameplay, UI, or networking behavior, update both this 3D doc set and the shared subsystem doc that owns the behavior.

## Current 3D Architecture

The 3D ship path uses the same top-level identity split as the 2D game, but distributes real behavior across focused components instead of a monolithic movement script.

```text
Entity3D
  -> Player3D
  -> Enemy3D
```

These classes should stay narrow and mostly coordinate dedicated 3D systems.

### Shared 3D ship systems

- `ShipFlight3D`
  - shared rigidbody flight
  - assisted pitch/yaw steering driven by filtered input and acceleration-limited turn rates
  - full 3D forward thrust, local drift damping, velocity alignment assist, and max-speed clamping
  - optional world-Y plane lock remains only as a fallback/debug path, not the active player-flight default
  - reusable by local player, AI, or future network drivers
- `ShipVisualTilt3D`
  - banking and pitch lean on the ship visual model
  - reads steering intent plus flight telemetry so the mesh leads turns instead of only reacting after rigidbody motion
- `ShipThrusterVfx3D`
  - thrust-driven thruster playback and intensity
- `ShipSpeedFx3D`
  - shared speed-based VFX such as dust/trails
  - can auto-build layered `TrailRenderer` ribbons from authored wing source transforms
- `DeathEffects3D`
  - shared ship death explosion/audio trigger and ship-part scatter kickoff
- `ShipPartScatter3D`
  - detached 3D ship-fragment tumble, drift, shrink, and cleanup runtime
- `SplitStateLightningRig3D`
  - shared split-state lightning coordinator for grouped anchored bolts on a 3D ship
  - toggles child `LightningBolt3D` renderers together and pushes a shared intensity multiplier through material property blocks
- `TimedEffectCleanup3D`
  - auto lifetime cleanup for one-shot 3D VFX, with destroy vs pool-despawn handling
- `ShipSplitOffsetRig3D`
  - shared split-state offset coordinator for grouped ship pieces
  - caches each piece's authored local position and applies inspector-configured additive local offsets while the split state is active

### Player-only 3D systems

- `PlayerInput3D`
  - local input adapter
  - feeds movement and weapon systems instead of movement reading raw input directly
  - when the active `PlayerInput` control scheme is `key+mouse`, free-look is sourced from locked mouse delta instead of an unlocked pointer position
- `PlayerCameraRig3D`
  - Cinemachine follow-offset, damping, and FOV behavior
  - intentionally allows turn-driven off-center framing so hard maneuvers can push the ship across the screen before the camera recenters
  - only belongs on the local player path
- `Player3D`
  - owns local-player-only 3D coordination, victim-side hit audio, and the dedicated `OnAnchor` input state
  - Anchor is a hold input that suppresses thrust while applying a configurable rotation multiplier for fast facing changes
  - while Anchor is active, `Player3D` can also drive split-state presentation rigs
  - owns player shield regeneration timing/rate config (`regenDelay`, `regenRate`) and applies regen with server authority in networked matches
- `PlayerHUDManager3D`
  - shared local-player binding source for scene HUD objects in the 3D path
  - resolves the correct player once and broadcasts that binding to dedicated HUD element scripts
  - routes player-originated HUD messages (for example vignette channel updates) to HUD-side receivers so prefab gameplay scripts do not need direct scene-UI references
  - should move to NGO ownership once the 3D network player path exists
- `PlayerHealthShieldHUD3D`
  - scene HUD health/shield presenter for the local player
  - subscribes directly to `Player3D.HealthChanged` and `Player3D.ShieldChanged`
- `PlayerWeaponSelectionHUD3D`
  - selected-weapon outline/select-bar presenter for the local player
  - supports both remaining-resource and cooldown-ready fill behavior per slot
- `PlayerAbilitySelectionHUD3D`
  - cooldown-only ability outline/select-bar presenter for the local player
  - each slot drives cooldown ready progress on the fill bar and swaps the surrounding box between base vs ready visuals
- `PlayerWeaponAbilityHUDSpawner3D`
  - spawns the local player's weapon/ability HUD prefab under the scene HUD once a player binding exists
- `PerformanceStatsHUD3D`
  - standalone scene performance overlay for the 3D path
  - keeps ping as a placeholder until the 3D network HUD path exists
- `PlayerAimReticle3D`
  - local-player HUD reticle controller for center-screen aiming readability
  - binds through `PlayerHUDManager3D`
  - uses the same camera-centered aim source as the 3D weapon path
  - supports enemy-hover feedback, firing pulse feedback, and primary-weapon overheat bracket fill
- `GigablastChargeEdgeGlow3D`
  - local-player fullscreen Gigablast edge-glow controller
  - reads the charged-shot progress and drives shader globals for the 3D charge-screen effect
- `PlayerLowHealthVignetteHUD3D`
  - local-player HUD-image low-health vignette controller for the 3D path
  - binds through `PlayerHUDManager3D` and drives a configured `Image` color/alpha
  - fades in when hull is below 50% and shields are depleted, then fades out when recovery conditions are met
- `PlayerChromaticAberration3D`
  - local-player chromatic-aberration hit feedback controller for the 3D path
  - should stay on the local player camera path and prefer an explicit `Volume` reference
- `GigablastEdgeGlowRendererFeature`
  - URP fullscreen render pass for the 3D charge-screen effect
  - adds HDR edge glow before post-processing so bloom can catch it

### Enemy-only 3D systems

- `EnemyAIFlightController3D`
  - produces the same movement-intent shape as player input so enemies can reuse `ShipFlight3D`

### Compatibility path

`Assets/Scripts/3d/Movement3D.cs` remains a compatibility coordinator for older prefabs/scenes that were wired against the previous monolithic script. New 3D work should prefer the componentized scripts directly.

## Implementation Principles

- preserve existing gameplay behavior first, then expand
- avoid rewriting stable combat systems just because the presentation changed
- prefer adaptation layers over full-system replacement when possible
- keep inspector-driven tuning for movement, camera, feedback, and combat readability
- preserve controller-first input feel
- do not silently break networking assumptions while experimenting with 3D movement or camera work
- treat networkability as a first-class engineering constraint for movement, aiming, combat timing, camera-driven assist logic, and stateful VFX
- treat performance as a first-class engineering constraint for VFX counts, spawned objects, update-loop cost, memory churn, and expensive camera or physics helpers

### Performance rules

- prefer object pooling over repeated instantiate/destroy loops for gameplay objects and effects
- avoid unnecessary per-frame allocations or hidden editor-friendly patterns that scale poorly
- be careful with particle counts, post-processing, camera effects, and repeated physics queries
- design shared systems so they scale to real match conditions rather than only isolated test scenes
- call out early when a visually attractive 3D feature has a likely platform-cost problem
- for ship split-state lightning, prefer anchored bolt meshes driven by explicit start/end transforms instead of large free-floating particle clouds

### Networking rules

- favor deterministic or clearly authoritative gameplay state over presentation-driven hidden state
- keep ownership and authority boundaries clear
- avoid coupling core gameplay results to local camera-only calculations when those results must later match across clients
- prefer data that can be replicated, predicted, reconciled, or reconstructed cleanly
- call out early when a 3D feature needs explicit network support instead of assuming it can be bolted on later

The current component split supports that direction:

- flight intent can come from player input, AI, or future networking drivers without changing the flight system
- camera behavior is isolated from shared ship movement so non-local ships do not inherit local-only state
- projectile firing is owned by a reusable weapon component instead of duplicated across ship types

## 3D Script Folder Layout

`Assets/Scripts/3d` should stay organized by subsystem responsibility instead of collecting every 3D script in one flat folder.

Current folder contract:

- `Core`
  - shared 3D base types and config/data definitions
  - examples: `Entity3D`, `Ship3DTypes`
- `Entities/Player`
  - player-only 3D entity coordination, input, and local camera ownership
  - examples: `Player3D`, `PlayerInput3D`, `PlayerCameraRig3D`, `PlayerHUDManager3D`
- `UI`
  - local-player 3D HUD widgets and HUD-binding targets
  - examples: `PlayerAimReticle3D`, `PlayerHealthShieldHUD3D`, `PlayerLowHealthVignetteHUD3D`, `PlayerWeaponSelectionHUD3D`, `PlayerWeaponAbilityHUDSpawner3D`
- `Entities/Enemy`
  - enemy-only 3D entity coordination and AI flight intent
  - examples: `Enemy3D`, `EnemyAIFlightController3D`
- `Flight`
  - shared 3D ship movement and reusable flight behavior
  - examples: `ShipFlight3D`
- `Combat`
  - projectile firing, projectile behavior, and other 3D combat-runtime scripts
  - examples: `Projectile3D`, `PhysicalProjectile3D`, `LaserBeam3D`, `GigaBlastProjectile3D`
- `Abilities`
  - reusable 3D weapon and ability definitions plus class-specific combat actions
  - examples: `Ability3D`, `Weapon3D`, `ProjectileWeapon3D`, `Teleport3D`, `TractorBeam3D`
- `Effects`
  - shared ship presentation and combat/VFX support scripts
  - examples: `ShipVisualTilt3D`, `ShipThrusterVfx3D`, `ShipSpeedFx3D`, `DeathEffects3D`, `ShipPartScatter3D`, `TimedEffectCleanup3D`
- `Effects/ShaderControllers`
  - shader-driven 3D effect controllers
  - examples: `LightningBolt3D`, `SplitStateLightningRig3D`
- `Pooling`
  - shared 3D object-pool helpers
  - examples: `GameObjectPool3D`, `PooledObject3D`
- `Legacy`
  - temporary compatibility shims kept only to avoid breaking older prefabs/scenes during the transition

When adding a new 3D script, place it under the subsystem it serves first. Do not add new scripts back into the root `Assets/Scripts/3d` folder unless the folder structure is being intentionally redesigned and this doc is updated with that change.

## Split-State Lightning Authoring Rule

- put the lightning look in the shader/material assets, but do not treat the shader alone as placement logic
- place one child `LightningBolt3D` object per visible hull seam or split, with explicit start/end anchor transforms on the ship prefab
- use `SplitStateLightningRig3D` on the ship root or effect root when multiple bolts should enable/disable together
- keep split-state activation outside the shader; the shader stays visual-only while the rig or gameplay presenter decides when bolts are active

## Change Classification Rule

3D work should explicitly call out whether it is:

- purely visual
- a control-feel change
- a camera/readability change
- a real gameplay-rule change
- a networking-impacting change
