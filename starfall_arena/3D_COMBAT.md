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

Networked runtime rule:

- in network sessions, `NetCombat3D` brokers projectile fire instead of letting every peer run gameplay damage locally
- owning clients spawn immediate cosmetic projectiles for responsiveness
- the server spawns the authoritative gameplay projectile and broadcasts cosmetic spawns to non-owners
- cosmetic projectile instances may render impacts, but must not apply damage, slow, impact force, or shield/hull state changes

Invasion/PvE targeting rule:

- 3D projectile requests now carry both the legacy `targetTag` and a `targetFaction`
- new PvE code should prefer `FactionMember3D` and `Faction3D` over tags for gameplay filtering
- tags remain a compatibility fallback for existing duel/prefab paths
- Invasion defaults to no ally damage: `PlayerTeam` projectiles should not damage players, and `EnemyTeam` projectiles should not damage enemies
- player duel projectile brokering still uses the opposite-player tag path when a request does not explicitly set `targetFaction`; Invasion player weapons that should damage enemies must set `targetFaction = EnemyTeam`
- do not use a generic `"Player"` tag in the 3D path; the current tag set has `Player1`, `Player2`, and `Enemy`

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
  - shared projectile spawn/cooldown ownership for player ships, plus older enemy prefabs that have not been migrated yet
  - routes network-session projectile fire through `NetCombat3D` while preserving local non-network behavior
- `ProjectileWeaponEnemy3D`
  - stripped-down enemy-only direct-fire projectile weapon
  - always fires muzzle-forward and keeps only cooldown, muzzle, projectile, recoil, and muzzle-FX data
  - should be preferred for AI projectile weapons that do not need player slot selection, screen-center aiming, or resource/HUD behavior
- `MissileWeaponEnemy3D`
  - stripped-down enemy-only missile launcher that reuses the same minimal volley/cooldown path as `ProjectileWeaponEnemy3D`
  - expects a projectile prefab with `MissileProjectile3D` and supports multi-muzzle launches for enemy salvos
- `StupidTurret3D`
  - scene-test firing driver for stationary or non-piloted 3D ships
  - should be paired with `ProjectileWeapon3D` aim set to `MuzzleForward` when the goal is "fire where the ship is facing"

### Class 1 path

- `BeamWeapon3D`
  - Class 1 beam weapon entry on the 3D path
- `LaserBeam3D`
  - runtime for the beam visual and hit behavior
  - resolves aim from camera center, not the parent transform's forward
  - separates cosmetic-only beam display from server-authoritative beam damage during network sessions
  - now supports explicit `targetFaction` filtering in addition to the older `targetTag` fallback, which is required for Invasion enemies because the 3D project does not use a generic `"Player"` tag
  - can optionally delegate beam presentation to a `BeamVisualDriver3D` component, so enemy-only beams can use alternate looks such as Forge3D line-renderer visuals without changing gameplay authority or the default player beam path
  - can optionally require forward-only aim, which clamps any backward-facing resolved aim back onto the beam's forward reference; use this on enemy hardpoint beams when camera-style aim data should never let the beam fire behind the muzzle
  - beam visuals may use a lightly smoothed visual endpoint while gameplay damage stays exact; in perspective, long beams exaggerate tiny aim changes at their far tip, so presentation smoothing is preferred over making the gameplay ray less accurate
- `ForgeEnemyBeam3D`
  - enemy-only unified Forge beam runtime
  - owns the authoritative hit ray, damage, line length, and impact placement in one script so the visual endpoint and gameplay endpoint cannot drift apart
  - should stay attached to the authored muzzle/anchor transform so it behaves like the original Forge plasma beam: one stable hardpoint transform, one hit query, one endpoint
  - if gameplay forgiveness is needed for readability, tune the beam's own `hitscanRadius` here rather than on `BeamWeapon3D`; the runtime that owns the cast should also own the forgiveness width
  - like the shared beam runtime, it should smooth its rendered endpoint/direction rather than showing every tiny long-range endpoint hop literally; keep damage exact and make only the presentation slightly forgiving
  - should be used for the artillery enemy Forge beam path instead of layering Forge visuals on top of `LaserBeam3D`
- `BeamWeapon3D`
  - beam-capacity weapons can now enforce a minimum remaining energy threshold before the beam is allowed to start again
  - beam weapons can also keep their rotation penalty alive for a short post-fire linger window, which is useful for AI beam enemies that would otherwise stop firing, instantly pivot at full speed, and then re-fire
  - use that threshold on AI beam enemies so they do not spam start requests every frame while nearly empty
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
  - replicates charge/tier presentation through `NetCombat3D`; release still uses the shared projectile request path
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
  - in network sessions, owner/remote copies can show the beam, but only the server-authoritative copy applies pull velocity

### Class 4 path

- `Class4BurstWeapon3D`
  - Class 4 primary weapon on the `Weapon3D` path
  - fires one tracked attack attempt as `3` timed sub-bursts across `2` muzzles, for `6` total shots per trigger pull
  - uses cooldown-style slot presentation instead of the shared overheat/resource display
- `ConvergeBeamWeapon3D`
  - Class 4 converge beam on the `Weapon3D` path
  - spawns `2` beams by default and `4` while `Empower3D` is active
  - resolves beam direction from the same replicated screen-center aim source used by the other 3D beam path, while still spawning from authored hardpoints on the ship prefab
- `GuidedMissileWeapon3D`
  - Class 4 missile weapon on the `Weapon3D` path
  - launches an authored guided-missile projectile prefab through the shared projectile/network broker instead of bypassing `NetCombat3D`
  - base and empowered missile variants use separate visual types so remote proxies and reflected-projectile cosmetics resolve the correct prefab
- `MissileProjectile3D`
  - full 3D homing projectile runtime for Class 4 missiles
  - reacquires targets from the explicit projectile faction first so the same prefab works in duel and Invasion flows; specific duel player tags remain a compatibility fallback
  - exposes an inspector dropdown so the same 3D missile prefab can be authored as either a guided missile or a straight-flying physical missile
  - owns delayed despawn behavior for missile-body renderers, exhaust particles, trail fade-out, impact explosion prefab spawn, and missile impact audio so real 3D missile prefabs do not have to behave like laser bolts
  - does not use the base projectile hit effect path; missile impact presentation should come from the authored explosion setup instead
- `Dodge3D`
  - Class 4 mobility ability on the `Ability3D` path
  - pressing the ability primes a short input window; the next valid look-stick input chooses one of four ship-relative directions: forward, back, left, or right
  - current 3D implementation is controller-look-stick only; no KBM dodge direction fallback is authored in this pass
  - in network sessions, owner dodge movement is queued through `NetMovement3D` and serialized into `NetInputSnapshot3D` so prediction, server validation, and reconciliation replay all reproduce the same dash; combat RPCs are presentation-only for dodge
- `Empower3D`
  - timed Class 4 empower toggle on the `Ability3D` path
  - while active, it upgrades Converge Beam from `2 -> 4` beams, reduces Dodge cooldown, and switches Guided Missile to its larger/harder-hitting empowered variant

## Networked Combat Authority

Current networked 3D combat uses server authority with owner-side cosmetic prediction:

- projectile and beam damage applies only on the server
- health, shield, hit feedback, slow state, and death presentation replicate from `NetCombat3D`
- recoil, impact force, tractor pull, and teleport warps must update `NetMovement3D` combat helpers so movement reconciliation keeps the combat impulse
- owner combat input is enabled only when the networked prefab has `NetCombat3D`; without it, `NetMovement3D` suppresses combat to avoid false local-only firing
- owner-control recovery must explicitly clear `PlayerInput3D` combat suppression when `NetCombat3D` exists, because movement input can be active while combat input is still blocked
- remote projectile cosmetics should use the local proxy's weapon/prefab bindings and log a one-shot warning if a binding is missing, rather than silently dropping the RPC
- fast projectile validation uses normal 3D spherecasts first, then a short defender-favored rewind against server movement history
- networked 3D beam state must resolve through a shared beam-network contract instead of assuming only `BeamWeapon3D` can receive RPC state
- ability-driven burst accuracy, Class 4 empower state, guided-missile visual type, and movement-affecting actions must stay inside the appropriate authoritative broker so owner prediction does not diverge from server truth
- dodge movement belongs to `NetMovement3D` input prediction, not `NetCombat3D`; remote dodge audio/VFX should be presentation-only while remote motion comes from interpolated movement snapshots

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
- `TargetAwarenessHUD3D` is a local-player combat readability layer for non-local `Entity3D` targets. It should bind through `PlayerHUDManager3D`, read replicated proxy state, and remain presentation-only with no target-awareness RPCs.
- offscreen/far target indicators should use screen-space ellipse clamping instead of rectangular corner snapping; trackers should not disappear because of distance alone. Close visible targets hide target UI, mid-range visible targets show brackets/bars, far visible targets may show an upright floating indicator, and offscreen/occluded targets use directional indicators.
- `PlayerWeaponSelectionHUD3D` should treat each slot explicitly as either remaining-resource display or cooldown-ready progress display
- `PlayerAbilitySelectionHUD3D` should use cooldown-ready progress and ready-state box feedback instead of blindly reusing older cooldown-remaining semantics
- combat HUD elements that live on a scene canvas should bind through `PlayerHUDManager3D`, not by having player prefabs race to claim shared HUD objects
- networked 3D scene HUD managers should auto-bind to the local spawned player and retry briefly after spawn, because ownership/input presentation can settle after `Player3D.OnEnable`
- fullscreen edge-glow presentation is shared by GigaBlast charge and low-health feedback; the renderer feature must enqueue when either effect reports visible shader state
- `PlayerWeaponAbilityHUDSpawner3D` should prefer the bound ship's `ShipData.abilityHUDPrefab` from the 3D roster and only fall back to a direct prefab override when no ship-data match exists

## Combat Documentation Rule

When a 3D combat change adds or changes:

- weapon slot ownership
- ability slot ownership
- aim source rules
- projectile or beam hit rules
- reticle or combat HUD semantics
- ability-driven movement or fire restrictions

update this document and add a corresponding entry to `3D_BUGS.md` if the change was driven by a bug, regression, or repeated implementation pitfall.
