# 3D_NETWORKING.md

This document records the current 3D networking decisions, the 2D networking audit findings that cross-apply, and the first-phase implementation scope for the 3D duel scene.

## Current Goal

The current 3D networking goal is narrower than the full 2D networked duel loop:

- keep the title-screen ship-select flow the same
- keep `GameDataManager` / `ShipData` selection as the source of truth for the chosen 3D ships
- reuse the shared NGO session bootstrap that already exists
- get both 3D player ships spawned as NGO player objects
- get fully networked 3D movement working for both ships
- defer augments, combat replication, and the broader round-flow port to later phases

The current 3D networking path should not be described as a full networked match loop yet.

## Reuse Audit

### Shared systems reused without 3D-specific changes

- `NetMgr`
  - still owns host/client startup, shutdown, connection lifecycle, and manual NGO player spawning
  - the 3D scene continues to rely on `SpawnPlayerNetworked(...)` instead of introducing a second session bootstrap
- `NetworkSessionData`
  - still owns synchronized ship selections and the chosen gameplay scene name
  - this already fits the 3D ship-select flow because the title screen selects ships before the gameplay scene loads
- `NetTickUtil`
  - still provides the NGO tick/time access the 3D movement layer needs
- `GameDataManager`
  - already separates 2D vs 3D rosters and still resolves selected ships by `ShipData`

### Shared systems that do not cross-apply directly

- `NetMovement`
  - does not cross-apply directly because it is tightly coupled to `Player`, `Rigidbody2D`, 2D rotation, 2D combat brokers, augment ticking, and 2D HUD/state side effects
  - copying it directly into the 3D path would have imported a large amount of unfinished 2D combat authority code that the 3D scene does not use yet
- `MovementSimulation`
  - does not cross-apply directly because it reproduces the 2D thrust/friction/anchor math, not the `ShipFlight3D` pitch/yaw/local-drift model
- `SceneManager`
  - does not cross-apply directly because it is responsible for 2D-specific maps, augment flow, split-screen behavior, 2D `Player` stat counters, and ability unlock timing
  - the 3D path now has a dedicated `SceneManager3D` that copies the reusable duel cadence while omitting maps/augments/unlocks and using 3D player/stat types

## First-Phase 3D Networking Structure

New 3D-specific networking code lives under `Assets/Scripts/3d/Networking`.

Current tick timing:

- the active `NetworkManager` prefab is configured for `50hz`
- Unity's fixed timestep is also effectively `0.02s`, so the current 3D network movement path should be tuned around 50 server snapshots per second
- older 60hz notes are historical intent, not the current runtime truth

### `NetMovement3D`

`NetMovement3D` preserves the same movement authority model as the 2D path:

- owner
  - reads local 3D input from `PlayerInput3D`
  - predicts locally
  - stores input/prediction history
  - sends tick input to the server
- server
  - re-simulates the same 3D movement tick authoritatively
  - publishes authoritative state snapshots
- remote non-owner
  - buffers authoritative snapshots on the server tick timeline
  - renders at a small adaptive visual delay behind `NetTickUtil.ServerTick`
  - interpolates between the snapshots surrounding that render tick instead of always blending the newest two packets
  - briefly extrapolates from the newest snapshot only within a capped window, then holds/snaps for discontinuities

Movement-affecting 3D abilities must be replayable from movement input history when they need owner prediction. Class 4 dodge is encoded as a one-tick dodge request plus direction in `NetInputSnapshot3D`, so owner prediction, server simulation, and reconciliation replay all start the dash from the same tick. Combat RPCs may play presentation, but they must not inject dodge movement outside `NetMovement3D`.

Important phase-1 constraint:

- combat input is intentionally suppressed while `NetMovement3D` is active
- this prevents false confidence from local-only 3D weapon firing before combat replication exists

### `MovementSimulation3D`

`MovementSimulation3D` is the 3D equivalent of the 2D pure-math simulator.

It reproduces the `ShipFlight3D` movement rules that matter for networking:

- filtered look input
- pitch/yaw turn-rate acceleration
- local-space thrust acceleration
- local drift damping
- velocity alignment assist
- max-speed clamping
- optional world-Y plane lock

This duplication is necessary because the original `ShipFlight3D` updates a live `Rigidbody` directly and cannot be replayed deterministically for reconciliation unless the math is exposed separately.

### `NetworkSceneManager3D`

`NetworkSceneManager3D` is the intentionally minimal scene bootstrap retained for low-level spawn/camera fallback behavior.

It:

- waits for the two expected clients
- resolves the selected 3D ships from `NetworkSessionData` first, then `GameDataManager`, then explicit scene fallbacks
- validates that the resolved ships belong to the registered 3D roster
- spawns the two selected 3D ship prefabs as NGO player objects
- assigns player-slot metadata (`Player1` / `Player2`) through `NetMovement3D`

It does not own:

- rounds
- win logic
- augment phases
- networked combat
- map selection

The active 3D match loop now belongs to `SceneManager3D`. In scenes using `SceneManager3D`, disable `NetworkSceneManager3D.spawnPlayersOnStart` to avoid spawning ships before the versus/round flow begins.

### `SceneManager3D`

`SceneManager3D` owns the 3D duel flow:

- resolves selected 3D ships from `NetworkSessionData`, then `GameDataManager`, then scene fallbacks
- runs a best-of-five match with shared versus, round-start, round-end, win tracker, and game-end UI
- spawns/despawns the two 3D network player objects each round through `NetMgr.SpawnPlayerNetworked(...)`
- locks owner movement/combat input during round intro and round transition through `NetMovement3D`
- omits map activation, augment selection, augment persistence, and ability-4 unlock timing
- records per-round and cumulative stats through `PlayerCombatStats3D` instead of relying on the 2D `Player` counters

### `NetCombat3D`

`NetCombat3D` is the 3D combat broker that sits beside `NetMovement3D`.

It keeps movement and combat authority separate:

- owner
  - keeps immediate local firing, beam, and ability presentation responsive
  - sends projectile, beam, teleport, shield, reflect, tractor-beam, and GigaBlast charge requests to the server
- server
  - owns real projectile / beam damage
  - owns combat-state replication for health, shield, slow state, and death
  - owns authoritative projectile spawns and short rewind checks against `NetMovement3D` history
- remote non-owner
  - receives cosmetic projectile / beam / ability state RPCs
  - never applies gameplay damage from those cosmetic instances

The 3D path intentionally uses brokered cosmetic projectile and beam instances instead of making every projectile or beam an NGO-spawned `NetworkObject`. This keeps fast raycast/sweep weapons responsive while avoiding per-shot network-object overhead.

### Invasion enemy networking

Invasion enemies use separate enemy-specific networking:

- `NetEnemyMovement3D`
  - server publishes enemy Rigidbody position/rotation/velocity snapshots
  - clients use the same adaptive server-timeline interpolation helper as remote player proxies
  - clients do not run enemy gameplay AI
  - client enemy proxies are kinematic and apply sampled position/rotation only; enemy `Rigidbody.linearVelocity` remains server-owned
- `NetEnemyCombat3D`
  - server spawns enemy gameplay projectiles and owns damage
  - clients receive cosmetic projectile spawns
  - enemy combat uses target factions instead of the player-duel `ResolveEnemyTag()` path

Do not reuse `NetMovement3D` or `MovementSimulation3D` for enemies. They are owner-predicted player movement paths. Enemy movement defaults to a simple server-owned Rigidbody motor plus snapshot interpolation.

Do not route enemy PvE projectiles through `NetCombat3D.ResolveEnemyTag()`, because that method intentionally resolves the opposite player slot for duels.

Important current implementation constraints:

- `NetCombat3D` must be present on networked 3D player prefabs, or `NetMovement3D` keeps owner combat input suppressed.
- owner presentation recovery must compare the `PlayerInput3D` combat-suppression flag against `NetCombat3D` presence; enabled input components alone do not prove combat input is usable.
- `NetCombat3D.OnNetworkSpawn()` re-runs owner local-control readiness so spawn-order differences do not leave a client owner stuck in movement-only input.
- client cosmetic projectile RPCs resolve local proxy bindings from the serialized `Entity3D` weapon slots first, then fall back to root `ProjectileWeapon3D`; missing source weapon or projectile prefab bindings log one-shot warnings instead of failing silently.
- `Projectile3D` and `LaserBeam3D` now split cosmetic-only instances from server-authoritative gameplay instances.
- combat velocity changes such as recoil, impact force, tractor pull, and teleport warps must pass through `NetMovement3D` helpers so prediction/reconciliation state is not immediately overwritten.
- raw combat velocity writes are only for non-networked dynamic Rigidbody fallbacks; networked targets should use `NetMovement3D.ApplyCombatVelocityDelta(...)`, which ignores kinematic proxy bodies while preserving owner/server movement state.
- owner-predicted ability movement must enter the movement input stream. Do not apply dash-style movement only through combat RPCs or direct Rigidbody writes, because reconciliation cannot reproduce that side channel during replay.
- slow state is treated as server-owned during network movement simulation; the server copy overrides the owner's submitted slow multiplier.
- beam and tractor-beam aim must be replicated, not resolved from a local camera on the server or on remote proxies. The owner sends its center-screen aim direction with the initial activation RPC and with per-tick `UpdateBeamAim` / `UpdateTractorBeamAim` updates. `LaserBeam3D` and `TractorBeam3D` consume that replicated direction on every non-owner peer so damage casts and cone pulls match what the firing player actually aimed at. The owner's local cosmetic instance keeps using its own camera so local fire stays responsive.
- `NetMovement3D.logOwnerCorrections` intentionally defaults on during the current jitter investigation. Every owner-side reconciliation correction logs a `[NetMovement3D Correction]` `Debug.Log` entry with prediction-buffer status, tick context, input at the corrected tick, error magnitudes, movement-lock/player-slot state, and recent combat/warp/boundary/dodge side effects.
- `NetDiagnosticsOverlay3D` is an optional local playtest overlay for movement networking. Add it to a scene object when diagnosing jitter; it reports snapshot buffer depth, adaptive delay, starvation, extrapolated frames, hard snaps, and owner correction counts for spawned 3D players/enemies.
- `NetCombat3D` projectile visual-type history must stay sized to the full `NetProjectileVisualType3D` enum range. If new visual types are added without growing the accepted-tick history, multiple visual types can collapse into the same cooldown/deduplication slot.

### Local camera binding

The local peer's Cinemachine gameplay camera now needs to be rebound after network spawn, not just left on a prefab-authored target.

Current binding path:

- `NetMovement3D` binds the owner camera/input/cinemachine tracking target when ownership becomes active
- `NetMovement3D` asks auto-bound `PlayerHUDManager3D` instances to rebind after owner presentation is configured, so scene HUDs follow the spawned local network player instead of stale scene references
- `NetworkSceneManager3D` also runs a local-owner retry loop after scene load so the camera still follows the newly spawned ship if spawn order or ownership timing is late

## 3D-Specific Integration Decisions

### Decision: Keep shared session code, duplicate movement code

What went wrong in the direct-reuse idea:

- the session layer is generic enough to reuse
- the movement layer is not
- the 2D movement netcode is mixed with 2D physics assumptions and later combat authority work

Why it matters:

- trying to reuse `NetMovement` directly would have produced a fragile 3D system that still depended on 2D-only contracts

How to avoid it:

- reuse shared session/bootstrap code only where the code is already scene-agnostic
- duplicate and isolate the movement driver where physics/model assumptions changed

### Decision: Do not disable the 3D telemetry consumers

What went wrong in the naive approach:

- a naive 3D net driver can disable `ShipFlight3D` entirely on all peers and move the rigidbody directly
- that breaks camera framing, tilt, thruster state, and speed FX because those systems read `ShipFlight3D` telemetry every frame

Why it matters:

- the ship can move correctly at the root while still looking visually broken or lifeless

How to avoid it:

- `ShipFlight3D` now supports an external-simulation mode
- networking owns the motion, but still pushes telemetry back into the shared 3D presentation systems

### Decision: Rebind Cinemachine after network spawn

What went wrong in the first 3D network pass:

- the local owner path re-enabled input and camera scripts, but it did not explicitly retarget Cinemachine to the newly spawned network ship

Why it matters:

- the gameplay camera can stay pointed at an old scene target or an unspawned/default object, which makes the movement path look broken even when replication is working

How to avoid it:

- always bind the local Cinemachine tracking target from the owner network path after spawn/ownership resolution
- keep a scene-level retry as a fallback because NGO spawn order and camera activation order are not guaranteed to line up perfectly

### Decision: Replicate player-slot identity explicitly

What went wrong in the 2D path and still applies in 3D:

- Unity tags do not replicate through NGO

Why it matters:

- any future 3D combat or HUD logic that keys off `Player1` / `Player2` tags will silently break on remote proxies if the slot identity is only assigned on the server

How to avoid it:

- `NetMovement3D` replicates the player slot and applies the tag locally on each peer

### Decision: Disable `PlayerInput` on all non-owners

What went wrong in the 2D path and still applies in 3D:

- leaving multiple active `PlayerInput` components alive on the same machine causes ownership/device confusion

Why it matters:

- the host can lose reliable control of the local ship as soon as the remote replica also has an active input component

How to avoid it:

- non-owner 3D replicas disable both `PlayerInput` and `PlayerInput3D`

## Obvious Shared Networking Weaknesses Worth Considering For 3D Follow-Up

These are not blockers for phase 1, but they are architectural issues the 3D path inherits if the shared networking stack stays unchanged.

### `NetworkSessionData` is still a broad state bucket

- cause
  - ship select, timers, augment state, round UI payloads, and game-end payloads all live in one object
- risk
  - later 3D scene-flow work will keep accumulating responsibilities in the same place
- likely fix direction
  - split long-lived session state from gameplay-presentation state before the 3D round-flow port grows

### The 2D networking stack mixes movement and combat authority too aggressively

- cause
  - the existing `NetMovement` grew into a movement driver plus combat RPC broker plus augment/runtime sync bridge
- risk
  - if the 3D path copied that shape, the same class would quickly become another monolith
- likely fix direction
  - keep `NetMovement3D` movement-only and move later 3D combat authority into separate 3D networking components

### Manual player spawning still assumes exactly one NGO player object per connected client

- cause
  - the spawn helper is built around a strict two-player duel with one controlled ship per client
- risk
  - later spectating, reconnect handling, or multi-entity ownership work will fight that assumption
- likely fix direction
  - keep the assumption for the duel, but call it out explicitly instead of letting it stay implicit

If you want, those shared issues are the next places worth tightening before the 3D networking layer grows much further.

## Editor Wiring For Phase 1

After pulling these changes, the scene/prefabs need the following setup:

1. On each of the two playable 3D ship prefabs:
   - add a `NetworkObject`
   - add `NetMovement3D`
   - add `NetCombat3D`
   - keep `Player3D`, `PlayerInput3D`, `PlayerInput`, `ShipFlight3D`, and the existing camera/FX components on the prefab
2. On the scene `NetworkManager`:
   - keep NGO player prefab auto-spawn disabled through `NetMgr`
   - register both 3D ship prefabs in the prefab list so NGO can spawn them by hash
3. On the 3D gameplay scene:
   - add `SceneManager3D`
   - assign player 1 / player 2 spawn points
   - assign the two fallback 3D `ShipData` assets
   - assign the shared versus, round-end, game-end, round text/countdown, win tracker, gameplay HUD root, and UI canvas references
   - either remove `NetworkSceneManager3D` or leave it with `spawnPlayersOnStart` disabled so `SceneManager3D` owns player spawn timing
4. In `GameDataManager`:
   - make sure the 3D roster contains only the two supported 3D ships for now
   - make sure each selected 3D `ShipData` points `shipPrefab` at the correct network-ready 3D gameplay prefab
5. In the title screen / host flow:
   - continue routing 3D games to the 3D gameplay scene name already used by `GameDataManager.SetShipRosterForGameplayScene(...)`

## Current Limitations

- 3D combat has a first brokered network path for projectiles, beams, several class abilities, health/shield state, slow state, and network-safe death
- 3D round flow now has a dedicated `SceneManager3D` for best-of-five duels without maps, augments, or unlock phases
- game-end and round-end presentation reuse the shared `NetworkSessionData` payloads and shared UI managers
- reconnect/late-join handling for mid-match 3D duels is not complete
