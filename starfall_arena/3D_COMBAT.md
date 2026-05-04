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
- `Entity3D.TakeDamage(...)` / `TakeDirectDamage(...)` also reject same-faction attacker damage while `InvasionSceneManager3D` is active, so custom direct-damage paths cannot bypass the weapon-level faction checks
- player duel projectile brokering still uses the opposite-player tag path when a request does not explicitly set `targetFaction`; Invasion player weapons that should damage enemies must set `targetFaction = EnemyTeam`
- do not use a generic `"Player"` tag in the 3D path; the current tag set has `Player1`, `Player2`, and `Enemy`

For player-facing readability, projectile fire direction should be resolved from the intended aim target, not from a muzzle transform that may be attached under a visually banked or pitched ship mesh.

Current implementation rule for 3D projectile visuals:

- 3D projectile prefabs should stay lightweight visual/data carriers
- if projectile hit detection is already handled by scripted raycasts or sweep tests, do not also add projectile colliders or rigidbodies unless another system needs projectile discovery
- `Projectile3D` forces its own colliders into trigger mode at runtime and ignores other `Projectile3D` colliders in scripted hit checks, so projectile colliders can remain discoverable by systems such as enemy projectile-threat scans without physically blocking shots
- projectiles with an explicit target tag or target faction ignore non-matching `Entity3D` colliders entirely. In Invasion, player-fired `EnemyTeam` bullets pass through other `PlayerTeam` ships instead of despawning on ally bodies before reaching enemies.
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
  - keeps only cooldown, muzzle, projectile, recoil, muzzle-FX data, and an optional brain-supplied fire direction
  - when an enemy brain supplies a fire direction, aim tolerance is only a permission gate; the projectile launches along the requested target/lead direction instead of inheriting the remaining muzzle-facing error
  - for large or offset cannon muzzles, prefer the convergence-point path when the brain has a locked target/lead point; a root-resolved direction can still produce a parallel muzzle shot that misses a stationary target
  - when no fire direction is supplied, falls back to muzzle-forward firing for simple turret/test cases
  - should be preferred for AI projectile weapons that do not need player slot selection, screen-center aiming, or resource/HUD behavior
- `StaggeredProjectileWeaponEnemy3D`
  - enemy-only direct-fire projectile rack for small hardpoints such as fortress laser-bolt turrets
  - consumes the inherited weapon cooldown once to start a rack sequence, then fires one configured muzzle at a time using `turretStaggerInterval` until the sequence finishes
  - can either walk the configured muzzles sequentially or pick a random turret for each staggered shot
  - uses `EnemySecondaryProjectile` as its network visual type so client cosmetic replay can distinguish small turret bolts from the fortress's heavy cannon projectile
- `HelixSpiralProjectileWeaponEnemy3D`
  - enemy-only Siege Carrier projectile component for corkscrew/drill-style barrages
  - owns the helix tuning directly: shot count, shot interval, spiral cone angle, degrees per shot, target lead, alternating spin direction, and whether one muzzle or all muzzles fire per shot
  - can walk a single component across several configured muzzles one shot at a time, avoiding a pile of separate projectile weapon components for one boss pattern
  - uses its own `EnemyHelixProjectile` network visual type so remote clients resolve the helix projectile prefab from the helix component instead of another enemy secondary projectile weapon
  - resolves the final corkscrew direction per muzzle/spawn point, not from the carrier root, so wide hardpoints do not fire parallel lines that miss a stationary target until the boss body happens to face the player
- `SiegeCarrierBossEnemyBrain3D`
  - boss-level enemy pattern sequencer that reuses enemy projectile weapons instead of spawning bullets from a custom standalone spawner
  - owns the current major patterns: lagging rake, helix spiral barrage, lagging beam convergence, orbital energy pillars, formation missile salvo, a two-hardpoint lightning slow beam, and an optional enemy spawn wave
    - keeps a serialized `maxShotsPerPattern` cap so bullet-hell pressure stays readable and performance-bound
    - keeps boss weapon references in a foldout section while weapon components still own projectile prefabs, muzzle FX, cooldown gates, pooling, and network request building
    - the helix spiral barrage delegates spiral math and muzzle sequencing to one assigned `HelixSpiralProjectileWeaponEnemy3D`, while the brain only starts/ticks the boss pattern
    - the lightning slow-beam pattern uses two assigned `BeamWeapon3D` components for visual/damage authority, aims at a profile-tuned lagged/lead target point, then applies the slow from the boss brain with a separate line-of-sight spherecast so only this boss attack gains slow without changing every beam prefab in the project
    - convergence and lightning slow-beam patterns can enable explicit behind-hardpoint aim on their assigned `BeamWeapon3D` components so wide carrier muzzles still aim at the shared boss-selected target point instead of snapping back to hardpoint-forward when the target leaves that muzzle's forward hemisphere
    - the enemy spawn-wave pattern starts every assigned `EnemySpawnerWeapon3D` once, then waits until those spawner sequences finish before advancing to the next boss pattern
    - active Siege Carrier prefabs that use `EnemyBalanceProfileApplier3D` receive beam convergence and lightning slow-beam lag/smoothing from `SiegeCarrierBossBalanceProfile3D`; tune the profile asset for runtime behavior, because it overwrites the brain component values during scene startup
- `MissileWeaponEnemy3D`
  - stripped-down enemy-only missile launcher that reuses the same minimal volley/cooldown path as `ProjectileWeaponEnemy3D`
  - expects a projectile prefab with `MissileProjectile3D` and supports multi-muzzle launches for enemy salvos
  - suppresses inherited muzzle-FX spawning because missile prefabs should carry their own exhaust/trail/launch presentation instead of using generic gun muzzle flashes
- `ProjectileChargeTelegraph3D`
  - generic visual charge tell for projectile-style attacks; it owns renderer emission ramps, optional VFX roots, and optional charge lights
  - can optionally spawn a warning sphere that follows the chosen player's explicit `Player3D` warning anchor and shrinks over the same charge duration
  - sphere support is opt-in per telegraph component and is still presentation-owned here, not in the enemy brain or weapon component
  - replaces the artillery-named telegraph script for new work, while `ArtilleryFortressChargeTelegraph3D` remains as a compatibility wrapper for existing prefab components
  - presentation only: it does not know which weapon to fire and must be driven by a brain or attack driver
- `EnemyProjectileChargeAttack3D`
  - generic enemy windup/firing driver for configured projectile, beam, or flamethrower attacks
  - for projectile mode, supports any `IEnemyProjectileWeapon3D`, including normal enemy projectiles, staggered projectile racks, missiles, and staggered missile racks
  - without warning-sphere mode, it preserves the older behavior: lock the supplied fire direction at windup start, play `ProjectileChargeTelegraph3D`, then fire through `NetEnemyCombat3D` in networked sessions or directly through the assigned weapon offline
  - with warning-sphere mode enabled on the assigned telegraph, it locks the chosen target entity at windup start instead of allowing retargeting, then re-resolves that same target's explicit warning anchor live through the charge
  - physical projectile release still happens at charge completion, not predicted impact time: the sphere shrinking to near-zero means "the shot is releasing now," and the projectile still uses normal travel/collision after that point
  - beam startup sphere timing means "the first damage frame should start now"; if the live target is no longer reachable at release, the telegraphed beam must cancel instead of starting with stale aim
  - flamethrower mode starts `EnemyFlamethrowerWeapon3D` after the windup while that weapon owns its normal burst duration and damage rules
  - `BasicShooterEnemyBrain3D` can use this optional driver to turn its immediate projectile shot into a telegraphed delayed shot without changing the normal instant-fire path when the driver is absent
- `StaggeredMissileWeaponEnemy3D`
  - enemy-only guided missile launcher variant for racks with several authored launcher transforms
  - consumes the inherited weapon cooldown once to start a rack sequence, then fires one configured muzzle at a time using `launcherStaggerInterval` until the sequence finishes
  - can either walk the configured launchers sequentially or pick a random launcher for each staggered missile
  - uses the same enemy projectile/network contract as `MissileWeaponEnemy3D`; gameplay fire stays server-authoritative through `NetEnemyCombat3D`, while clients receive the normal cosmetic projectile RPC
- `FormationMissileSalvoWeaponEnemy3D`
  - enemy-only simultaneous missile bloom for boss/elite attacks
  - launches a configured missile count at once, cycling through authored muzzle transforms when there are fewer muzzles than missiles
  - each spawned `MissileProjectile3D` receives a radial formation slot, opens into a ring around the target direction, briefly holds formation, then collapses its radial offset toward the player so the salvo converges as one dodge check
  - compresses fan/hold/convergence timings at close range so the pattern still resolves near the boss instead of spending the same full flourish it uses at long range
  - formation missiles adjust speed during the collapse phase toward the same arrival window; this keeps different ring slots from landing one after another because their curved paths differ in length
  - uses its own `EnemyFormationMissile` network visual type so remote clients do not confuse this burst with normal enemy missiles
  - should be budgeted by missile count when driven by a boss brain; one activation may be one weapon call, but it is still several live guided projectiles
- `EnemySpawnerWeapon3D`
  - enemy-side Invasion spawning weapon for carrier/boss-style enemies that release other enemy prefabs
  - exposes one enemy prefab, one spawn point, spawn count, and delay between spawns so a designer can stack multiple components for different enemy types or hardpoints
  - does not use `DisallowMultipleComponent`; multiple spawner weapons on the same GameObject are expected
  - delegates to `InvasionWaveManager3D.SpawnEnemyAt(...)` so spawned enemies are network-spawned by the server and tracked by the wave manager instead of becoming untracked raw scene instances
  - can be started automatically with `Spawn On Enable` for quick tests, but authored combat patterns should usually call `BeginSpawning()` from a brain, animation event, or phase controller
- `OrbitalEnergyPillarVisual3D`
  - Siege Carrier presentation driver for the orbital pillar boss pattern
  - owns pooled launched sphere prefabs, straight carrier-to-sphere link-line, layered pillar cylinder, and wraparound arc-bolt mesh visuals while `SiegeCarrierBossEnemyBrain3D` owns all timing, damage, networking, and profile-applied behavior values
  - launched spheres travel from the carrier face to the target ring positions, then become the center point that the pillar grows out from; during the expand phase the visual cylinder stretches up and down in world Y from that sphere instead of appearing at full height immediately
  - renders pillars as separate white core, turbulent red/white shell, soft halo, and visual-only jagged arc ribbons; the arc meshes are pooled billboard strips that crawl around the cylinder and leap outward, not gameplay line traces
  - pillar damage is a server-only, bounded vertical capsule check; the visual cylinder is intentionally much taller than gameplay space so it appears endless without requiring infinite physics queries
- `OrbitalEnergyPillarBluePlasmaVisual3D`
  - V2 Siege Carrier pillar presentation that subclasses the existing visual driver contract so the boss can still use the same `OrbitalEnergyPillarVisual3D` assignment slot
  - preserves the red/white V1 assets and adds a separate blue/white plasma path: editable `CoreVolume`, `CloudShell`, and `RimGlow` body layers, generated internal major lightning ribbons, smaller branch ribbons, and pooled spark/glint billboards
  - treats shader crackle/clouds as depth texture only; the readable lightning comes from deterministic pooled mesh geometry seeded per pillar/arc, so clients can replay matching broad motion from the replicated pattern timing without networking every bolt
  - carrier-to-sphere links remain simple `LineRenderer` telegraphs, while dangerous pillar lightning is visual-only mesh geometry inside the cylinder volume; server-only bounded pillar damage is unchanged
- `EnemyFlamethrowerWeapon3D`
  - enemy-only short-range cone DPS weapon for Invasion flamethrower enemies
  - treats `3d_flamethrower.prefab` as an authored particle/light visual attached to a muzzle; gameplay damage is a separate non-alloc cone query owned by the weapon script
  - can bend the damage volume with the owner's lateral Rigidbody velocity so the server-authoritative hit area tracks flamethrower particle drift while the enemy strafes/orbits
  - targets `Faction3D.PlayerTeam` by default and should keep legacy target tag empty for new PvE prefabs
  - in networked Invasion, only the server applies cone damage; `NetEnemyCombat3D` replicates flame visual start/stop so clients never run flame hit checks
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
  - networked beam runtimes now receive an explicit server-authoritative gameplay flag, matching the projectile path, so enemy beams driven by `NetEnemyCombat3D` can apply damage without needing a player `NetCombat3D` broker on the enemy prefab
  - now supports explicit `targetFaction` filtering in addition to the older `targetTag` fallback, which is required for Invasion enemies because the 3D project does not use a generic `"Player"` tag
  - can optionally delegate beam presentation to a `BeamVisualDriver3D` component, so enemy-only beams can use alternate looks such as Forge3D line-renderer visuals without changing gameplay authority or the default player beam path
    - can optionally require forward-only aim, which clamps any backward-facing resolved aim back onto the beam's forward reference; use this on enemy hardpoint beams when camera-style aim data should never let the beam fire behind the muzzle
    - beam visuals may use a lightly smoothed visual endpoint while gameplay damage stays exact; in perspective, long beams exaggerate tiny aim changes at their far tip, so presentation smoothing is preferred over making the gameplay ray less accurate
    - explicit AI/network aim can opt out of the forward-only clamp through `IBeamAimConstraint3D`; use this only for authored attacks where side/back hardpoints are intentionally allowed to converge on one world point
- `ForgeEnemyBeam3D`
  - enemy-only unified Forge beam runtime
  - owns the authoritative hit ray, damage, line length, and impact placement in one script so the visual endpoint and gameplay endpoint cannot drift apart
  - should stay attached to the authored muzzle/anchor transform so it behaves like the original Forge plasma beam: one stable hardpoint transform, one hit query, one endpoint
  - if gameplay forgiveness is needed for readability, tune the beam's own `hitscanRadius` here rather than on `BeamWeapon3D`; the runtime that owns the cast should also own the forgiveness width
  - like the shared beam runtime, it should smooth its rendered endpoint/direction rather than showing every tiny long-range endpoint hop literally; keep damage exact and make only the presentation slightly forgiving
  - can render optional jittered lightning segments directly through the unified runtime; keep stock Forge raycast scripts such as `F3DLightning` disabled on gameplay beam prefabs so they do not fight the 3D beam authority
  - lightning-style prefabs with several authored `LineRenderer` components should register every gameplay beam line on `additionalLineRenderers` so all visible strands are driven by the same resolved aim and hit ray; the runtime now also auto-registers child line renderers outside the muzzle/impact effect anchors as a prefab-wiring safeguard and can drive line points in world space so child renderer transforms do not collapse the visible span
    - explicit AI/network aim is resolved before the hardpoint-forward fallback, which lets coordinated enemies such as the Triumvirate converge several anchored beams on one target while still using the Forge beam hit/runtime path
    - explicit AI/network aim can be allowed behind the hardpoint forward reference for boss convergence patterns; keep the normal forward-only clamp for beams that should never fire backward from their muzzle
    - should be used for the artillery enemy Forge beam path instead of layering Forge visuals on top of `LaserBeam3D`
- `TriumvirateLightningLinkVisual3D`
  - cosmetic-only ship-to-ship lightning link driver for Triumvirate-style enemy tells
  - can reuse the `enemy_lightning_beam` visual prefab by disabling stock Forge `F3DLightning` scripts and starting `ForgeEnemyBeam3D` in fixed-endpoint cosmetic link mode between two enemy anchors
  - relies on the same Forge enemy beam visual runtime as the final beam, so line renderer length, jitter, muzzle/impact anchors, and UV scrolling stay consistent between link tells and the damaging beam
  - does not apply damage, slow, force, or network authority; player-facing damage stays in the enemy brain/final beam path
- `BeamWeapon3D`
  - beam-capacity weapons can now enforce a minimum remaining energy threshold before the beam is allowed to start again
  - beam weapons can also keep their rotation penalty alive for a short post-fire linger window, which is useful for AI beam enemies that would otherwise stop firing, instantly pivot at full speed, and then re-fire
  - beam origin and beam direction can be authored separately: `Muzzle` controls where the beam starts, while `Direction Reference` can supply the +Z/forward axis used by AI aim checks and runtime casts when a visual muzzle's local forward is rotated for art placement
  - use that threshold on AI beam enemies so they do not spam start requests every frame while nearly empty
  - `IBeamDirectionSource3D`
    - optional runtime extension for beam prefabs that need a separate transform for aim/cast direction
    - `ForgeEnemyBeam3D` and `LaserBeam3D` consume it so authored beam visuals can keep their muzzle anchor while aiming from a clean forward reference
  - `IBeamAimConstraint3D`
    - optional runtime extension for beam prefabs that need explicit AI/network aim to bypass forward-only clamping while preserving the normal clamp for camera or hardpoint-forward fallback aim
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
  - inherits the base `ProjectileWeapon3D` target faction/tag and runs the shared player projectile targeting normalization before firing, so Invasion GigaBlast shots target `EnemyTeam` instead of damaging allied player-team ships
  - Invasion targeting stays `EnemyTeam` even during enemy-empty timing windows such as pre-wave delays and between-wave clears; do not fall back to duel opponent tags just because no enemies are currently alive
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
  - in Invasion, same-faction entities are ignored so the pull cannot drag a co-op teammate around even when the target mask includes player layers
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
  - suppresses inherited muzzle-FX spawning for the same reason as enemy missile weapons: the missile projectile prefab owns exhaust, trails, delayed renderer despawn, and impact presentation
- `MissileProjectile3D`
  - full 3D homing projectile runtime for Class 4 missiles
  - reacquires targets from the explicit projectile faction first so the same prefab works in duel and Invasion flows; specific duel player tags remain a compatibility fallback
  - exposes an inspector dropdown so the same 3D missile prefab can be authored as either a guided missile or a straight-flying physical missile
  - owns delayed despawn behavior for missile-body renderers, exhaust particles, trail fade-out, impact explosion prefab spawn, and missile impact audio so real 3D missile prefabs do not have to behave like laser bolts
  - can apply configurable area damage at the missile impact point through `AreaDamageConfig3D`, using a bounded non-alloc overlap query and the same faction/tag target filtering as direct projectile hits
    - missile splash uses `DamageSource3D.Projectile` because the 3D damage-source enum does not currently define a separate explosion source
    - splash applies radial combat velocity and the missile slow effect to every valid target caught in the radius
    - splash expands its temporary overlap buffer when saturated and registers unique `Entity3D` targets before applying damage, so complex ship models with many child colliders cannot consume a fixed collider cap and hide other ships inside the explosion radius
  - does not use the base projectile hit effect path; missile impact presentation should come from the authored explosion setup instead
- `Dodge3D`
  - Class 4 mobility ability on the `Ability3D` path
  - pressing the ability primes a short input window; the next valid left-stick movement input chooses one of four ship-relative directions: forward, back, left, or right
  - the controller dodge direction comes from `PlayerInput3D.OnLook` / `MoveInput`, while free-look steering still comes from `OnFreeLook`
  - current 3D implementation is controller-left-stick only; no KBM dodge direction fallback is authored in this pass
  - in network sessions, owner dodge movement is queued through `NetMovement3D` and serialized into `NetInputSnapshot3D` so prediction, server validation, and reconciliation replay all reproduce the same dash; combat RPCs are presentation-only for dodge
- `Empower3D`
  - timed Class 4 empower toggle on the `Ability3D` path
  - while active, it upgrades Converge Beam from `2 -> 4` beams, reduces Dodge cooldown, and switches Guided Missile to its larger/harder-hitting empowered variant

## Networked Combat Authority

Current networked 3D combat uses server authority with owner-side cosmetic prediction:

- projectile, beam, and enemy flamethrower cone damage applies only on the server
- health, shield, hit feedback, slow state, and death presentation replicate from `NetCombat3D`
- recoil, impact force, tractor pull, and teleport warps must update `NetMovement3D` combat helpers so movement reconciliation keeps the combat impulse
- owner combat input is enabled only when the networked prefab has `NetCombat3D`; without it, `NetMovement3D` suppresses combat to avoid false local-only firing
- owner-control recovery must explicitly clear `PlayerInput3D` combat suppression when `NetCombat3D` exists, because movement input can be active while combat input is still blocked
- remote projectile cosmetics should use the local proxy's weapon/prefab bindings and log a one-shot warning if a binding is missing, rather than silently dropping the RPC
- fast projectile validation uses normal 3D spherecasts first, then a short defender-favored rewind against server movement history
- networked 3D beam state must resolve through a shared beam-network contract instead of assuming only `BeamWeapon3D` can receive RPC state
- networked enemy beam state now carries a beam component index so multi-beam enemies such as the Siege Carrier beam convergence pattern can replay the correct hardpoint on clients instead of always resolving the first `BeamWeapon3D`
- ability-driven burst accuracy, Class 4 empower state, guided-missile visual type, and movement-affecting actions must stay inside the appropriate authoritative broker so owner prediction does not diverge from server truth
- dodge movement belongs to `NetMovement3D` input prediction, not `NetCombat3D`; remote dodge audio/VFX should be presentation-only while remote motion comes from interpolated movement snapshots

## Current Control And Aim Rules

Current player control rule:

- `Anchor` is a dedicated 3D player hold input handled by `Player3D`, not one of the `Ability3D` slots
- while held, Anchor applies `Player3D` inspector-configured thrust and rotation multipliers to `ShipFlight3D`
- while held, Anchor can enable configured `SplitStateLightningRig3D` and `ShipSplitOffsetRig3D` components under the player
- default Anchor tuning is `thrustMultiplier = 0` and `rotationMultiplier = 3`
- each active 3D player prefab should have the matching `PlayerBalanceProfile3D`-derived asset and a `PlayerBalanceProfileApplier3D` reference on the prefab root. Tune health, shield regen, Anchor multipliers, flight handling, weapon balance, and class-specific ability numbers in the profile; keep cameras, models, VFX, audio, muzzles, projectile prefabs, UI, layers, and network references on the prefab.

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
- visible target brackets should be sized from the target's projected screen-space mesh bounds, not from distance-only scale curves. Automatic bounds use active `MeshRenderer` and `SkinnedMeshRenderer` children only, while odd-shaped prefabs can add `TargetAwarenessBounds3D` for a simple authored local-space box override.
- `TargetAwarenessWidget3D` should place health/shield bars from the computed bracket edge so bar placement stays consistent across enemy sizes. Offscreen red attack brackets may pulse alpha only when `TargetAwarenessAttackReporter3D` says that target actually attacked, sustained an attack, or explicitly scheduled a charged pre-fire warning against the bound local player; projectile, beam, and flame enemy attack paths should pass their intended target through existing combat state/visual messages instead of adding target-awareness-specific RPCs.
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
