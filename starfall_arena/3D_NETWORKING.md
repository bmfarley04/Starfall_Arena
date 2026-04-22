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
  - does not cross-apply directly for phase 1 because it is responsible for the broader round loop, maps, HUDs, round intros, augment flow, win states, and later-match teardown
  - phase 1 only needs a basic 3D network scene bootstrap that resolves the selected ships and spawns them correctly

## First-Phase 3D Networking Structure

New 3D-specific networking code lives under `Assets/Scripts/3d/Networking`.

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
  - interpolates between authoritative snapshots

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

`NetworkSceneManager3D` is the intentionally minimal scene bootstrap for phase 1.

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

Those stay out of phase 1 on purpose.

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
   - keep `Player3D`, `PlayerInput3D`, `PlayerInput`, `ShipFlight3D`, and the existing camera/FX components on the prefab
2. On the scene `NetworkManager`:
   - keep NGO player prefab auto-spawn disabled through `NetMgr`
   - register both 3D ship prefabs in the prefab list so NGO can spawn them by hash
3. On the 3D gameplay scene:
   - add `NetworkSceneManager3D`
   - assign player 1 / player 2 spawn points
   - assign the two fallback 3D `ShipData` assets
4. In `GameDataManager`:
   - make sure the 3D roster contains only the two supported 3D ships for now
   - make sure each selected 3D `ShipData` points `shipPrefab` at the correct network-ready 3D gameplay prefab
5. In the title screen / host flow:
   - continue routing 3D games to the 3D gameplay scene name already used by `GameDataManager.SetShipRosterForGameplayScene(...)`

## Current Limitations

- combat is not networked in the 3D scene yet
- round flow is not ported to a 3D-specific network scene manager yet
- health/shield/game-end replication is intentionally out of scope for this first phase
- the current 3D networking deliverable is movement-only, not a full duel loop
