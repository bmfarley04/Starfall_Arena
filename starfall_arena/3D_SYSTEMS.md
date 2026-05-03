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

- `SceneManager3D`
  - 3D duel-flow coordinator for the active networked 3D scene
  - owns best-of-five round progression, versus-screen wait, round intro/countdown, per-round spawning/despawning, round-end presentation, win tracking, and game-end presentation
  - reuses the shared versus, round-end, game-end, win tracker, and UI camera/canvas setup
  - intentionally omits the 2D map cycle, augment phases, and ability-4 unlock cadence; 3D duels are straight combat rounds using prefab-authored ship kits
- `ArenaBoundary3D`
  - optional six-sided rectangular arena boundary for duel scenes
  - owns current arena center, starting width/height/length, percent-based shrink waves, generated visual geometry, network-synced active/shrinking state, and server-authoritative outside damage
  - procedurally generates one inward-facing six-sided visual box mesh and optional six thin `BoxCollider` blocker children at runtime
  - uses `Starfall/3D/ProceduralHexArenaBoundary` with a tileable hex mask texture for local-player proximity reveal
  - default gameplay is a soft boundary: players can pass through, local players get a maximum red arena-boundary vignette through `PlayerLowHealthVignetteHUD3D`, and the authoritative side applies configurable percent-of-total-durability damage over time outside the arena
  - keeps the hex force-field mesh cosmetic; generated blocker colliders are disabled by default and should only be enabled if hard containment is intentionally restored
- `ShipFlight3D`
  - shared rigidbody flight
  - assisted pitch/yaw steering driven by filtered input and acceleration-limited turn rates
  - full 3D forward thrust, local drift damping, velocity alignment assist, and max-speed clamping
  - supports a precision throttle/brake path: low positive thrust input is capped to a slower forward drift speed with reduced acceleration, while negative thrust input brakes local forward speed toward zero
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
- `BlackHoleMenaceVisual3D`
  - optional visual-only black hole controller for 3D Invasion
  - drives the accretion disk `_MidColor`, `_OuterColor`, and `_HotStreakColor` from a blue start palette toward a red/orange final palette as enemy defeat progress increases
  - blends authored disk colors through a tunable mid-menace palette instead of directly lerping blue to red, which avoids the unwanted purple midpoint without washing the disk fully white at 50% menace
  - keeps `_InnerColor` pure white through an exposed HDR intensity value
  - uses a runtime material instance plus the same renderer property-block override path in play mode and edit mode so preview and runtime color application stay visually consistent without dirtying shared material assets
  - syncs menace progress through a server-written NetworkVariable when the black hole root has a `NetworkObject`

### Player-only 3D systems

- `PlayerInput3D`
  - local input adapter
  - feeds movement and weapon systems instead of movement reading raw input directly
  - maps left-stick Y from the look command into precision forward drift / braking while keeping left trigger as the primary full-thrust control
  - when the active `PlayerInput` control scheme is `key+mouse`, free-look is sourced from locked mouse delta instead of an unlocked pointer position
- `PlayerCameraRig3D`
  - Cinemachine follow-offset, damping, and FOV behavior
  - intentionally allows turn-driven off-center framing so hard maneuvers can push the ship across the screen before the camera recenters
  - only belongs on the local player path
- `PlayerScreenShake3D`
  - local-player Cinemachine impulse feedback controller for high-speed flight pulses and confirmed incoming damage
  - reads speed from `ShipFlight3D` and receives hit-shake requests from `Player3D` after real shield/hull damage has been applied
  - gates impulses to the local network owner so remote player proxies do not shake the viewer's camera
- `Player3D`
  - owns local-player-only 3D coordination, victim-side hit audio, and the dedicated `OnAnchor` input state
  - Anchor is a hold input that suppresses thrust while applying a configurable rotation multiplier for fast facing changes
  - while Anchor is active, `Player3D` can also drive split-state presentation rigs
  - owns the generic left-stick flick dodge path; this must stay separate from any class-specific dodge ability component in cooldown, movement resolution, and presentation routing
  - owns player shield regeneration timing/rate config (`regenDelay`, `regenRate`) and applies regen with server authority in networked matches
- `PlayerCombatStats3D`
  - lightweight 3D combat-stat counter attached to spawned 3D players by `SceneManager3D`
  - tracks shots fired, shots hit, damage dealt, and damage taken for shared round-end/game-end UI
  - uses tracked attack ids so one trigger pull, beam activation, or multi-muzzle volley counts as one accuracy attempt and at most one hit
  - records stats only on the gameplay-authoritative side, which is the server during networked matches
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
- `TargetAwarenessHUD3D`
  - local-player target readability HUD for non-local `Entity3D` objects
  - binds through `PlayerHUDManager3D`, pools one canvas widget per active target, and reads replicated proxy transform/health/shield state without sending network messages
  - transitions between edge indicator, floating far/occluded indicator, close visible hidden state, and mid-range visible bracket states
  - uses screen-space ellipse clamping with independently tunable top/bottom padding for offscreen indicators and occlusion checks to avoid showing brackets/bars through world geometry
  - sizes visible brackets from projected active `MeshRenderer`/`SkinnedMeshRenderer` bounds, or from an optional `TargetAwarenessBounds3D` local-box override on unusual enemy prefabs
  - reads `TargetAwarenessAttackReporter3D` locally so offscreen attack brackets can pulse only for enemies that recently or actively attacked the bound local player
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
  - simple enemy Rigidbody motor
  - rotates toward AI-authored world-space move direction and moves forward along the enemy's facing direction
  - intentionally contains no target selection, pathing, or attack logic
- `EnemyTargetSensor3D`
  - selects the nearest visible target in a configured `Faction3D`
  - current Invasion use targets `PlayerTeam`
- `EnemyObstacleAvoidance3D`
  - adjusts free-flight steering with non-alloc 3D spherecast probes against asteroid/world obstacle layers
- `BasicShooterEnemyBrain3D`
  - first Invasion enemy brain
  - directly pursues the nearest visible player and fires at `PlayerTeam` when aimed and off cooldown
- `SiegeCarrierBossEnemyBrain3D`
  - boss-pattern coordinator for rakes, beam convergence, missile salvos, lightning slow beams, and orbital energy pillars
  - owns the orbital pillar presentation directly by pooling a launch spear prefab and blue pillar prefab; the old separate pillar visual component and its spark/link/lightning runtime meshes are removed
  - treats orbital pillars as a persistent phase-two transition layer: pillars keep a fixed ring radius around the carrier, slowly orbit the boss, and do not use active/fade duration tuning
- `NetEnemyMovement3D`
  - server Rigidbody-state replication for networked Invasion enemies
- `NetEnemyCombat3D`
  - server-authoritative enemy projectile spawning/damage with client cosmetic projectile broadcasts

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
  - examples: `Entity3D`, `Ship3DTypes`, `FactionMember3D`
- `Entities/Player`
  - player-only 3D entity coordination, input, and local camera ownership
  - examples: `Player3D`, `PlayerInput3D`, `PlayerCameraRig3D`, `PlayerHUDManager3D`
- `UI`
  - local-player 3D HUD widgets and HUD-binding targets
  - examples: `PlayerAimReticle3D`, `PlayerHealthShieldHUD3D`, `PlayerLowHealthVignetteHUD3D`, `PlayerWeaponSelectionHUD3D`, `PlayerWeaponAbilityHUDSpawner3D`
- `Entities/Enemy`
  - enemy-only 3D entity coordination and AI flight intent
  - examples: `Enemy3D`, `EnemyAIFlightController3D`, `EnemyTargetSensor3D`, `EnemyObstacleAvoidance3D`, `BasicShooterEnemyBrain3D`
- `Invasion`
  - 3D Invasion mode flow and wave helpers
  - examples: `InvasionWaveManager3D`
- `Environment`
  - local-only authored environment population and static obstacle helpers
  - examples: `AsteroidFieldSpawner3D`
  - prefer explicit seeded generation for local obstacle fields when layout determinism matters for testing, iteration, or future network-safe scene reconstruction
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
  - examples: `ShipVisualTilt3D`, `ShipThrusterVfx3D`, `ShipSpeedFx3D`, `PlayerScreenShake3D`, `DeathEffects3D`, `ShipPartScatter3D`, `TimedEffectCleanup3D`
- `Effects/ShaderControllers`
  - shader-driven 3D effect controllers
  - examples: `LightningBolt3D`, `SplitStateLightningRig3D`
- `Boundaries`
  - 3D arena containment and boundary presentation
  - examples: `ArenaBoundary3D`, `ForceFieldBoundaryWall3D` for older authored-wall experiments
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

## Black Hole Effect Shader Authoring

- the 3D black hole effect uses two materials: `Starfall/3D/BlackHole/AccretionDisk` for a flat local-XZ disk/ring and `Starfall/3D/BlackHole/SingularityLensing` for an inflated sphere around the event horizon
- the accretion disk shader is additive, but intentionally renders in the opaque render-queue range so URP includes it in `_CameraOpaqueTexture`; this is required for the singularity shader to bend the disk image in screen space
- because the disk renders before the skybox for lensing capture, it keeps depth writes enabled and clips faint pixels with `Depth Clip Threshold`; otherwise the skybox overwrites the disk anywhere there is no opaque object already behind it
- the singularity/lensing shader renders after the disk, samples `_CameraOpaqueTexture`, bends samples radially around the sphere center, blacks out the event horizon, and adds a tunable HDR photon ring for bloom
- in the current Unity 6 / URP 17 path, URP copies `_CameraOpaqueTexture` after the skybox, so skybox stars and nebulae are available for the black hole lensing pass when the active renderer asset has opaque texture enabled
- `BlackHoleLensSourceRendererFeature` is the preferred Invasion black-hole lensing path: it renders only the configured lens-source layers into `_BlackHoleLensSourceTexture`, and the singularity shader bends that texture instead of the full camera color when the feature is active
- keep gameplay ships, HUD/cursors, normal asteroids, and other non-background objects out of the lens-source layer mask; the black-hole accretion disk prefab child currently uses `Background1` so the real disk can be bent without also bending the player or UI
- skybox pixels are intentionally not captured by this layer-filtered path; add any future lensable starfield/nebula cards or meshes to `Background1`/`Background2` if they should bend, while leaving gameplay objects on their normal layers
- transparent background-only particles such as `StarParticle_Mat` should render before the singularity shell, currently queue 2800 / `Transparent-200`, so the black hole can cover them while normal gameplay transparencies still render later
- the singularity/lensing material renders at queue 2900 (`Transparent-100`) so it can bend captured background/disk color before normal transparent gameplay VFX, ship flames, world reticles, and HUD-like overlays draw on top of it
- foreground rejection uses `_CameraDepthTexture` to gate bent samples against the current lens fragment's eye depth; keep the active renderer/camera depth texture available when `Foreground Rejection` is above 0, otherwise the lens cannot reliably distinguish behind-the-hole skybox/background pixels from ships, asteroids, cursors, and other foreground objects
- `_CameraOpaqueTexture` is still not a true final-frame grab: transparent VFX rendered after the opaque texture copy will not be bent by the singularity unless a dedicated renderer feature copies scene color later and the shader is changed to sample that custom texture
- `Bend Strength` can be negative to pull scene samples inward toward the event horizon; this is the useful direction for lifting the rear disk into the upper/lower Einstein-ring arcs
- the `Lensed Disk Arc` controls add an art-directed procedural horizon band with repeated lane lines and noise similar to the flat disk, making the rear accretion disk read above and below the black core even when the pure screen-space bend samples the disk's central hole
- the lensed disk arc has matching spin, infall, and spiral controls; keep those close to the accretion disk material values so the wrapped arcs animate like the same disk light rather than a separate static halo
- the disk material now has explicit relativistic-look controls: `One-Side Hot Boost`, `Far-Side Dimming`, `Hot-Side Focus`, and `Hot Side Angle` are the primary knobs for Doppler-style brightness asymmetry
- `Back-Half Inner Fade`, `Back-Half Fade Reach`, and `Back-Half Angle` are art-direction controls that hide the inner rear disk enough for the singularity's lensed arc to read as bent light instead of a flat ring running through the black core
- the singularity material's `Lensed Background Brightening` boosts screen-sampled skybox/scene color near the lensing shear so sparse starfields show visible gravitational arcs instead of only a black cutout
- prefer keeping `Disk Arc Intensity` at 0 when the real screen-sampled disk is visible; use `Lensed Source Thickness`, `Lensed Source Threshold`, `Lensed Source Boost`, and `Lensed Source Ring Width` to thicken the actual sampled disk color without painting a separate procedural arc over it
- the lensed source thickening is a small brightest-sample gather around the bent scene UV, so tune it conservatively; it costs extra scene-color samples only on the singularity shell and should stay focused near the photon ring rather than becoming a broad fullscreen blur
- `BlackHoleMenaceVisual3D` belongs on the black hole root when Invasion should color-shift the disk over the run; assign its accretion disk renderer and the scene's `InvasionWaveManager3D`
- menace progress uses `InvasionWaveManager3D`'s authored enemy total as the denominator and defeated enemies as the numerator; runtime child-spawn kills may advance the numerator, but the denominator intentionally remains the authored wave total
- menace colors use Start, Mid, and Final palettes; keep the Mid palette warm/amber if the transition should feel threatening without passing through purple or pure white
- menace only tints the accretion disk material colors and does not change lensing, photon ring, disk geometry, damage, gravity, wave rules, or enemy behavior
- author the singularity sphere slightly larger than the visible black core; tune `Event Horizon Radius` on the material to decide how much of that sphere is pure black versus lensing falloff
- the disk shader is quad-friendly: it converts centered UVs to polar coordinates, uses polar angle/radius for circular swirl motion, and masks the square corners away; keep the black hole centered at UV `(0.5, 0.5)`
- the current PC/3D URP path has opaque texture enabled; any renderer asset used for this effect must keep opaque texture enabled or the lensing shader will not have a valid scene color source

## White Dwarf Star Shader Authoring

- the 3D white dwarf centerpiece uses three materials: `Starfall/3D/WhiteDwarf/CoreSurface` for the opaque emissive sphere, `Starfall/3D/WhiteDwarf/CoronaShell` for the inflated additive atmosphere, and optional `Starfall/3D/WhiteDwarf/CompactLensing` for near-field screen-space shimmer
- `Assets/Prefabs/3d_effects/WhiteDwarfStar3D.prefab` wires these layers as `Core`, `CoronaShell`, and `LensingShell`; the lensing renderer starts disabled and is intended only for close gameplay-centerpiece shots
- `WhiteDwarfStarVisual3D` pushes `_ExternalPulseIntensity` through material property blocks so shared material assets are not duplicated at runtime; keep this effect visual-only unless a separate gameplay system explicitly owns hazards or gravity
- tune visible size through child transform scale, not shader displacement, so combat readability bounds and any future gameplay volumes stay predictable
- bloom is not owned by the shader; the materials output HDR values, but the active camera must render post-processing and the scene/global volume must have Bloom intensity above zero
- optional lensing samples `_CameraOpaqueTexture`; use `PC_RPAsset.asset` or another URP asset with opaque texture enabled when enabling the lensing shell, because the default `UniversalRP.asset` keeps opaque texture disabled
- the v1 color profile is material-driven white/cyan HDR tuning; a planned 1D blackbody LUT can be added later if multiple authored star temperatures become a real content need

## Pulsar Effect Authoring

- the 3D pulsar centerpiece uses `Starfall/3D/Pulsar/CoreSurface` for the opaque emissive neutron-star sphere and `Starfall/3D/Pulsar/Jet` for two additive transparent polar jet cylinders
- `Assets/Prefabs/3d_effects/Pulsar3D.prefab` wires the root `PulsarVisual3D`, a `Core` sphere, and `NorthJet` / `SouthJet` children with trigger `CapsuleCollider` volumes matched to the visible jet cylinders
- keep pulsar rotation on `PulsarVisual3D` / transform rotation instead of baking the spin entirely into shader math; this keeps the visible beam direction, trigger volumes, and future gameplay hit checks aligned
- `PulsarVisual3D` pushes `_ExternalPulseIntensity` into each assigned shared material; the prefab uses separate north/south jet materials so `_OutwardSign` can be baked per jet without requiring material property blocks during prefab import
- the jet shader supports an optional seamless noise texture, but still has procedural noise so the prefab remains usable if the texture is replaced or temporarily missing
- tune visible size through child transform scale and the `PulsarVisual3D` gameplay query radii/length, not shader displacement, so future damage volumes stay predictable
- this prefab is currently visual plus trigger-volume authoring; any actual damage, networking authority, or scoring behavior should be owned by a separate gameplay system before it is treated as a match hazard
- bloom is not owned by the shader; the materials output HDR values, but the active camera must render post-processing and the scene/global volume must have Bloom intensity above zero

## Portal Effect Authoring

- the reusable 3D portal effect is a visual-only prefab at `Assets/Prefabs/3d_effects/Portal3D.prefab`; it does not own teleport logic, triggers, damage, networking, audio, or scene flow
- the prefab uses two thin quad layers with `Starfall/3D/PortalDisk`: `InnerSurface` for a near-black center that gradients to dark purple near the edge, and `OuterRim` for the bright purple-white HDR edge glow
- the base portal intentionally has no inner swirl/noise animation; keep the center calm so the effect reads like a dark portal surface instead of a cartoon vortex
- the rim can use shader-time angular segment modulation to read as spinning; keep that motion on `OuterRim` and avoid moving texture/noise in the inner surface
- the inner surface should remain opaque inside its circular mask so scene objects and skybox do not show through the portal center; use the rim material for bloom-heavy glow, not inner transparency
- scale and rotate the prefab transform for placement; keep the disk fixed in world space unless a future task explicitly asks for billboarding or gameplay-facing behavior
- bloom is not owned by the prefab; tune the HDR material brightness together with the active camera and scene/global Bloom settings
- avoid adding particles, screen-space distortion, opaque-texture sampling, or gameplay trigger behavior to the base portal prefab unless the effect is intentionally expanded by a later task

## Change Classification Rule

3D work should explicitly call out whether it is:

- purely visual
- a control-feel change
- a camera/readability change
- a real gameplay-rule change
- a networking-impacting change

## Procedural Hex Arena Boundary Authoring

- add one `ArenaBoundary3D` to the 3D gameplay scene and assign it to `SceneManager3D` if the boundary should run with each round
- keep `NetworkObject` on `ArenaBoundary3D` in network scenes so active/shrinking state and current dimensions replicate to clients
- do not author six wall prefab children for the current path; `ArenaBoundary3D` generates the inward-facing box visual and blocker colliders itself at runtime
- leave `Block Players At Boundary` disabled for the current soft-boundary design; players outside the arena receive warning vignette plus configurable damage instead of being physically stopped
- teleport and network teleport clamping only run when `Block Players At Boundary` is enabled, so soft-boundary matches can still move or blink through the field
- author each shrink wave with `Duration`, `Time Until Next Wave`, and `Target Size Percent`; the arena starts at 100 percent of its configured width/height/length, interpolates all three dimensions to the wave target over `Duration`, then waits at that size for `Time Until Next Wave` before starting the next wave
- tune `Outside Penalty Interval` for how often outside damage is applied, and tune `Outside Damage Percent Per Second` as a fraction of `MaxHealth + MaxShield`; for example `0.05` removes 5 percent of total durability per second while outside
- tune `Outside Vignette Alpha` and `Outside Vignette Color` for the local-player HUD warning shown through `PlayerLowHealthVignetteHUD3D`
- assign a material using `Starfall/3D/ProceduralHexArenaBoundary`, or let the component create a runtime fallback material from that shader
- assign a tileable black/white hex texture to `Hex Mask`; `Assets/Sprites/hex_texture_1024.png` is the current repo candidate if no custom arena texture is ready yet
- import the hex texture as a regular texture with `Wrap Mode = Repeat`; `Sprite (2D and UI)` plus `Clamp` can make the mask look like it is not tiling or not being respected
- tune `Reveal Distance` for how far away from the barrier the local player starts seeing it; tune `Visible Patch Radius` separately for how large the visible spot on the wall becomes near the player
- proximity reveal uses independent samples for each arena face that is within `Reveal Distance`, so adjacent walls can fade together without a closest-wall pop or forward-facing spotlight feel
- while the local player is outside the arena, the closest barrier face gets an outside reveal override using `Outside Reveal Distance` and `Outside Visible Patch Radius` until the player returns inside
- tune `Texture World Size` for pattern scale, `Mask Threshold` / `Mask Softness` / `Mask Power` for line cutoff and softness, and the proximity/shrink HDR colors for cyan vs red warning reads
- tune `Pulse Speed` / `Pulse Strength` and `Crackle Scale` / `Crackle Speed` / `Crackle Strength` for lightweight shader-only energy motion; these are visual-only and should not drive gameplay state
- keep `Idle Visibility` at or near zero when the arena should mostly disappear until approached; shrinking changes only arena dimensions and does not force full-wall visibility or flashing
- shrink behavior belongs to `ArenaBoundary3D` dimensions and generated collider placement, not to shader displacement; the shader only decides visibility/color
- the generated visual mesh is cosmetic and additive/transparent; containment still comes from generated `BoxCollider` blockers plus server-side clamp correction
- keep `Start Active On Enable` enabled for standalone arena testing; turn it off only when another scene-flow owner deliberately calls `StartBoundary()` and `StopBoundary()`
