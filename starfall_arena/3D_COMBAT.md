# 3D_COMBAT.md

This document records the current combat-specific rules for the 3D implementation path.

Read this when working on 3D weapons, abilities, aiming, projectiles, beams, reticles, or combat HUD behavior inside `Assets/Scripts/3d`.

## Current Projectile Runtime Split

- `ProjectileWeapon3D`
  - owns fire cadence, muzzle selection, aim resolution, projectile spawn, and muzzle FX
  - owns the shared `SoundEffect` hook for 3D projectile fire
  - base projectile fire also spends an overheat-style energy cost against the owning `Entity3D`
- `Projectile3D`
  - owns projectile travel, hit detection, hit results, hit FX, and despawn/pool return

For player-facing readability, projectile fire direction should be resolved from the intended aim target, not from a muzzle transform that may be attached under a visually banked or pitched ship mesh.

Current implementation rule for 3D projectile visuals:

- 3D projectile prefabs should stay lightweight visual/data carriers
- if projectile hit detection is already handled by scripted raycasts or sweep tests, do not also add projectile colliders or rigidbodies
- only keep collision components on the real world targets that need to be hittable

## 3D Weapon And Ability Model

Current implemented rule:

- `Entity3D` uses 3 `Weapon3D` slots and 2 `Ability3D` slots
- `PlayerInput3D` routes `OnWeapon1`-`OnWeapon3` into weapon selection, drives the selected weapon from shared `OnFire`, and only routes `OnAbility1`-`OnAbility2` into `Ability3D`
- cooldown-based combat actions that are intended to be selectable weapons belong on the `Weapon3D` path, not the `Ability3D` path

This differs from an older transition-state assumption where `Entity3D` mirrored the 2.5D path's four generic ability slots. The problem with the old assumption was that cooldown attacks could still spawn projectiles while bypassing weapon selection, shared fire routing, and weapon HUD cooldown presentation. The current 3-weapon / 2-ability split exists specifically to keep combat input, HUD state, and runtime ownership aligned.

## Core Architecture

- `Ability3D`
  - base class for active 3D abilities
  - owns shared input routing, cooldown/duration query hooks, lock/disable flags, thrust/rotation modifiers, HUD state hooks, and sibling-ability coordination helpers
- `Weapon3D`
  - base path for selectable weapon behavior in the 3D runtime
  - should be used when a combat action participates in the active weapon slot model
- `Entity3D`
  - owns the assigned 3D weapon and ability arrays
  - remains the authoritative owner for shared combat state such as primary-weapon overheat and ability-driven movement/fire modifiers
- `PlayerInput3D`
  - owns the player-facing routing between input, weapon selection, fire input, and ability input

Base input rule:

- hold/release abilities should work through the base ability input contract unless there is a concrete reason the base contract is insufficient
- ability-side movement or fire restrictions are not enough on their own; the movement/firing drivers must consume those hooks for the behavior to be real in play

## Current 3D Combat Implementations

### Shared combat support

- `ProjectileWeapon3D`
  - shared projectile spawn/cooldown ownership for player and enemy ships
- `StupidTurret3D`
  - scene-test firing driver for stationary or non-piloted 3D ships
  - should be paired with `ProjectileWeapon3D` aim set to `MuzzleForward` when the goal is "fire where the ship is facing"

### Class 1 path

- `BeamWeapon3D`
  - Class 1 beam weapon entry on the 3D path
- `LaserBeam3D`
  - runtime for the beam visual and hit behavior
  - resolves aim from camera center, not the parent transform's forward
- `Reflector3D`
  - Class 1 reflect ability for the 3D path
  - owns cooldown, active window, projectile reflection rules, and reflected-projectile audio
- `ReflectShield3D`
  - shield-mesh runtime used by `Reflector3D`
  - preserves the shield fade/ripple presentation while applying the reflected projectile state after the ability approves the hit
- `Teleport3D`
  - blink movement ability for the 3D path
  - supports separate origin/destination pulsewave configs so departure and arrival can use different authored visuals
- `GigaBlastWeapon3D`
  - Class 1 charged projectile weapon for the 3D path
  - applies tier-based thrust/rotation penalties while charging and fires tier-specific projectile prefabs through `ProjectileWeapon3D`
- `GigaBlastProjectile3D`
  - optional projectile runtime for tiers that need piercing behavior

### Class 2 path

- `Class2Shield3D`
  - temporary absorb shield for the 3D path
  - uses the shield mesh visual runtime, but consumes incoming explicit-sweep projectiles instead of reflecting them
- `EmpoweredShot3D`
  - empowered projectile weapon on the `Weapon3D` path
  - reuses `ProjectileWeapon3D` aim and muzzle handling while carrying its slow debuff through the 3D projectile/entity runtime
  - when the slow lands, it temporarily scales the victim's thruster emission rate (default authored target: `30 -> 2`) for the slow duration, then automatically restores normal emission
- `PhysicalProjectileAbility3D`
  - shield-bypassing projectile weapon on the `Weapon3D` path
  - reuses the shared 3D spawn, aim, and recoil rules while dealing direct hull damage
- `TractorBeam3D`
  - cone pull ability for the 3D path
  - pulls overlapping `Rigidbody` targets toward the ship with full 3D cone-angle checks (including vertical climb/dive space)
  - resolves cone aim from the same center-screen camera ray path used by 3D projectile weapons (with convergence + direction blend), while still using authored `spawnPoint` for beam origin/visual placement
  - can auto-align `visualRoot` to the same cone direction used by pull logic (`alignVisualToConeDirection`) and can optionally scale visuals to the gameplay cone dimensions (`scaleVisualToCone` + `visualConeScaleMultiplier`)
  - exposes a selected-object Scene gizmo (`drawGameplayConeGizmo`) so cone half-angle/range can be verified against runtime gameplay volume
  - no longer generates cone meshes, materials, or suction particles in code; it now reads an authored `spawnPoint` for cone origin/facing and only toggles an authored `visualRoot`, so the full tractor beam look is built manually in prefab/editor content
  - authored visuals can be particle-based or mesh-based; the current lightweight mesh option is `Assets/Shaders/3d/TractorBeamFresnel.shader`, which expects a cone/cylinder mesh with beam length mapped along UV `V`

## Current Control And Aim Rules

Current player control rule:

- `Anchor` is a dedicated 3D player hold input handled by `Player3D`, not one of the `Ability3D` slots
- while held, Anchor applies `Player3D` inspector-configured thrust and rotation multipliers to `ShipFlight3D`
- while held, Anchor can enable configured `SplitStateLightningRig3D` and `ShipSplitOffsetRig3D` components under the player
- default Anchor tuning is `thrustMultiplier = 0` and `rotationMultiplier = 3`

Current 3D flight rule that affects combat feel:

- the active 3D player flight path uses true climb/dive movement
- assisted steering is the default control law
- cinematic readability depends on `ShipFlight3D`, `ShipVisualTilt3D`, and `PlayerCameraRig3D` being tuned together

Aim rules:

- player projectile direction should be resolved from the same camera-centered aim source used by the reticle and weapon logic
- `LaserBeam3D` resolves aim from `Camera.ViewportPointToRay(0.5, 0.5)`, not from `transform.forward`
- reticle hover/readability feedback must stay aligned with the actual weapon aim path

## Combat HUD Rules

- `PlayerAimReticle3D` should read the same aim source and primary-weapon overheat state that the weapon runtime actually uses
- `PlayerWeaponSelectionHUD3D` should treat each slot explicitly as either remaining-resource display or cooldown-ready progress display
- `PlayerAbilitySelectionHUD3D` should use cooldown-ready progress and ready-state box feedback instead of blindly reusing older cooldown-remaining semantics
- combat HUD elements that live on a scene canvas should bind through `PlayerHUDManager3D`, not by having player prefabs race to claim shared HUD objects

## Combat Documentation Rule

When a 3D combat change adds or changes:

- weapon slot ownership
- ability slot ownership
- aim source rules
- projectile or beam hit rules
- reticle or combat HUD semantics
- ability-driven movement or fire restrictions

update this document and add a corresponding entry to `3D_BUGS.md` if the change was driven by a bug, regression, or repeated implementation pitfall.
