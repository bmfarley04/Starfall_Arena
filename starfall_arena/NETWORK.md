# NETWORK.md

This document summarizes the networking philosophy and the current implementation status in the repo.

## Current Scope

The project is moving from a local duel game toward a networked two-player duel game.

Important distinction:

- the networking philosophy is broader than what is fully integrated today
- movement networking has real implementation now
- full networking is the active direction for the project
- older local multiplayer/split-screen play should be treated as mostly deprecated for now
- full networked match flow, spawning integration, and weapon/combat replication are not all complete yet

Do not describe the entire duel as fully networked unless those missing pieces are actually integrated.

## Networking Philosophy

The intended model remains:

- server authoritative
- client responsiveness for the local player
- conservative correction
- stability over flashy extrapolation
- dodging should remain skillful and trustworthy

This is still the right philosophy for a duel game where movement precision matters.

Input/platform note:

- networking changes must preserve both controller and keyboard-and-mouse control paths
- current target deployment context is PC/computer plus controller-oriented console-style play

## Tick Model

Networking is built around NGO ticks.

Current expectations:

- tick rate target: `60hz`
- tick identity is the shared time reference for simulation and reconciliation
- network simulation should prefer tick time over ad hoc timing

`NetTickUtil` wraps NGO tick access so code can query:

- current local tick
- server tick
- tick interval
- tick rate
- whether networking is currently active

## Authority Model

The intended authority model is already reflected in movement code:

- the server is authoritative
- the owning client predicts locally
- remote non-owners are rendered from server snapshots
- the owning client can be corrected and replay inputs when prediction drifts

## Current Implemented Pieces

### NetMgr

`NetMgr` is the current session lifecycle helper.

It currently provides:

- host/client/server startup helpers
- shutdown helper
- connection and disconnection callbacks
- join-order-based manual player prefab spawning
- a helper for server-side spawning of player `NetworkObject`s

Current caveat:

- the file explicitly notes that gameplay scene spawn integration is still pending
- this means the networking helper exists, but the full scene flow is not yet switched over to a complete networked spawn path
- `NetMgr` now disables NGO's default player prefab assignment and manually spawns the configured first-player prefab for the first connected player and the configured second-player prefab for the second connected player; both prefabs still need `NetworkObject` components and registration in the `NetworkManager` prefab list

### NetMgrTest

`NetMgrTest` is a separate scene-test harness for legacy networking and movement testing.

Use it only in dedicated test scenes that still rely on:

- debug host/client hotkeys
- explicit player 1 / player 2 prefab references
- explicit player spawn-point references

Do not use `NetMgrTest` for the production title-screen host/join flow.

### NetworkSessionData

`NetworkSessionData` is the persistent network session authority object.

It currently carries:

- synchronized ship-select state and timer
- gameplay scene load state
- selected map index replication
- round-end presentation payloads
- augment-phase presentation payloads, timers, and resolved choices

This means client-visible non-movement match flow now depends on explicit session replication rather than assuming the host's local UI calls will appear automatically on clients.

Round-start note:

- round intro presentation in network gameplay must also be broadcast explicitly
- the `ROUND X` banner and 3-2-1 countdown should be triggered from `NetworkSessionData`, not only from the host's local scene coroutine

Ship-select timing note:

- the server should advance out of ship select immediately once both connected players are locked in
- the countdown is now only a fallback for missing or late selections, not a required wait even when both players already chose

### Network Scene Note

The active network gameplay scene now assumes:

- one gameplay camera plus UI overlay, not split-screen cameras
- one local HUD presentation only
- server-selected map activation must be mirrored to clients by map index, because scene-object `SetActive` state is not replicated automatically
- round-start presentation must be broadcast explicitly so clients see the same `ROUND X` banner and opening countdown as the host
- round-end presentation must be broadcast explicitly
- augment selection is a synchronized timed phase with separate local pools for each player
- game-end presentation must also be broadcast explicitly and remapped to local-player perspective on each client

### NetMovement

`NetMovement` is the main implemented networked gameplay system right now.

It is attached beside `Player` and takes over movement when a network session is active.

Behavior by role:

- owner
  - reads local input from `Player`
  - predicts movement immediately
  - stores input and predicted-state history
  - sends input snapshots to the server
- server
  - runs authoritative movement simulation
  - publishes authoritative state snapshots
  - stores state history for future networking features
- remote non-owner
  - disables local gameplay-driving `Player` logic
  - becomes interpolation-driven
  - uses buffered authoritative snapshots for smooth display

This is the most important “recent context” addition versus the older doc.

### MovementSimulation

`MovementSimulation` is the deterministic movement math layer shared by networking code.

It exists to make movement simulation:

- pure-value driven
- replayable during reconciliation
- aligned with the intended local movement rules

It currently handles:

- thrust acceleration
- lateral damping
- delayed friction
- anchor drag
- max-speed clamping
- rotation toward look input

This script is the core of the prediction and replay approach.

### NetworkStructs

Current network payloads include:

- `NetInputSnapshot`
  - tick
  - thrust
  - look input
  - anchor state
  - friction-enabled state
  - owner visual bank angle
  - owner visual pitch angle
- `NetStateSnapshot`
  - tick
  - authoritative position
  - authoritative rotation
  - authoritative velocity
  - authoritative visual bank angle
  - authoritative visual pitch angle
  - anchor drag accumulator
  - friction timer
  - thrust state (for remote thruster visuals)
  - shield value (for remote shield regen visuals)

These structs are intentionally minimal and focused on what movement replay and visual sync needs.

## Current Movement Networking Flow

### Owner Flow

Per tick, the owner:

1. reads movement-relevant state from `Player`
2. stores the input in a circular buffer
3. runs local prediction through `MovementSimulation`
4. applies velocity and rotation locally
5. stores predicted state for reconciliation
6. sends input to the server, or publishes authoritative state directly when host-owned

### Server Flow

The server:

1. receives input snapshots
2. simulates authoritative movement
3. applies authoritative velocity and rotation
4. broadcasts authoritative state snapshots to clients
5. stores authoritative history in a circular buffer

### Owner Reconciliation

When the owner receives authoritative state:

- predicted and authoritative positions are compared
- if the error is below threshold, no correction happens
- if the error exceeds threshold, the client rewinds to server state
- stored inputs are replayed forward through `MovementSimulation`
- corrected state is written back to the rigidbody

### Remote Proxy Display

Remote non-owners:

- buffer authoritative snapshots
- interpolate between snapshots
- apply the server-authored visual bank/pitch state to the ship's child visual model
- do not run local movement logic
- do not perform forward prediction

This stays aligned with the original stability-first networking philosophy.

### Networked Combat Broker

`NetMovement` now also acts as the temporary combat RPC and history broker for duel weapons.

Current behavior:

- owning clients still play immediate local weapon visuals
- the server receives fire/start/stop requests for projectile, beam, fire-trail, and reflect actions
- the server spawns and owns the real gameplay projectile / beam / fire hazard behavior
- non-owning clients receive cosmetic spawn/state RPCs so they can render the same weapon events without applying gameplay damage

This is intentionally a bridge step that keeps combat authority aligned with the already-implemented movement authority model without requiring every weapon prefab to become a separate NGO network object first.

### Ring of Fire (RingOfFireManager)

`RingOfFireManager` is networked as a `NetworkBehaviour` using `NetworkVariable`s for state sync.

Server behavior:

- runs all wave logic: wave chaining, interpolation, wave transitions, damage ticks
- writes interpolated ring state (center, width, length, radius, shape type, active flag) to `NetworkVariable`s each frame
- calls `Entity.TakeDamage()` for entities outside the safe zone; damage propagation to clients is handled by the existing `Entity` combat broadcast path

Client behavior:

- reads `NetworkVariable`s each frame and updates local display state
- renders the line renderer boundary and unsafe-area mask from the synced values
- initializes visuals on `OnNetworkSpawn` (late join) or via `OnValueChanged` callback when the ring activates
- does not run wave logic, interpolation, or damage

Non-networked behavior:

- all original local behavior is preserved behind `NetMgr.IsNetworked` checks
- runs identically to the pre-networking implementation

Design notes:

- `NetworkVariable`s are ideal here because ring state changes slowly (interpolated over wave durations of many seconds) and auto-sync to late joiners
- no RPCs, tick-based sync, or prediction needed — the ring is not player-controlled
- `IsInsideSafeZone()` works on all peers because clients update `_currentSafe*` fields from `NetworkVariable`s
- the RingOfFireManager's GameObject requires a `NetworkObject` component in the Maps prefab

## Current Implementation Limits

Based on the current repo, these pieces are not yet represented as fully integrated networking systems:

- gameplay scene flow is not fully wired to the network spawn helper
- the broader duel loop in `GameSceneManager` still reads as primarily local/splitscreen-oriented
- full projectile/gameplay replication for every weapon family is still incomplete
- the current networked combat path covers player projectile shots, GigaBlast projectile shots, beam start/stop, fire-trail hazard authority, and reflector activation
- the current networked combat path also covers teleport, Class2 inline abilities, and Class3 bomb / self-state abilities
- projectile and beam lag compensation currently uses a short, defender-favored history window from `NetMovement` state history rather than a full world rewind system

That means `NETWORK.md` should describe those pieces as planned or intended, not current fact.

## Current Combat Validation Direction

The active implementation now follows this direction:

- client-side prediction for local player movement
- interpolation-only display for remote players
- immediate local cosmetic weapon feedback for the owner
- server-authoritative projectile, beam, and fire-hazard damage
- conservative lag compensation that favors the dodger

Current projectile / beam note:

- projectile hits are validated on the server against recent target history using a short rewind cap
- beams also use a short rewind cap and only apply real damage from the server-owned beam instance
- if the rewind result is outside the stored safety window, the current implementation falls back toward a miss instead of stretching further into the past

The existing movement implementation is a real first step toward that architecture.

## Known Networking Notes

- `Player.externalMovementControl` is the bridge that lets networking take over physics without discarding the existing input callbacks.
- Remote proxies disable `Player` and rely on interpolation instead of duplicating gameplay logic client-side. Because `Player` is disabled, systems that normally run in `Player.Update()` must be driven externally by `NetMovement` for remote/server copies. Currently this includes thruster visuals (driven via `ApplyNetworkThrustState` from state snapshot thrust flag) and shield regeneration (driven via `TickShieldRegeneration` on the server, with shield value broadcast in state snapshots for remote regen visuals).
- Bug note: round-intro movement locking in network gameplay must not rely only on a one-shot RPC during spawn. If the owner misses that transient lock state, they can move during the opening countdown. Keep the authoritative movement-lock state persisted on `NetMovement` so newly spawned owners apply it immediately on `OnNetworkSpawn`.
- `Entity` banking/pitching on the 3D visual model now rides in `NetStateSnapshot`; remote proxies should consume the replicated visual tilt instead of recomputing it from interpolated root movement, or turns/recoil can look flat or mismatched.
- For client-owned ships, the server/display side should forward the owner's reported visual tilt instead of recomputing bank from the server copy's root rotation, or the host view can over-bank badly.
- Host mode requires special care to avoid double-simulating owner movement, and `NetMovement` already includes host-specific handling for that.
- Host mode now also needs special care for combat visuals versus gameplay authority: owner-side local weapon visuals must not be allowed to double-apply gameplay recoil or damage when the host is also the authoritative server.
- `ProjectileScript`, `LaserBeam`, and `FireHazard` now have a split between cosmetic-only instances and server-authoritative gameplay instances during network sessions. Future combat work should preserve that separation instead of letting client visuals deal damage directly.
- Bug note: the title-screen join flow can be triggered by more than one UI/input path on the same client. Guard repeated join presses while `_awaitingClientConnect` is true, otherwise a second `StartClientForMenu()` call will hit NGO after the first call already set `IsListening` and falsely report that a session is already active.
- Any networking migration work should assume full-network play is the target and treat old split-screen assumptions as secondary unless specifically required.
- Ability ClientRpc guards must use `if (IsServer || IsOwner) return;`, NOT `if (IsOwner && !IsServer) return;`. The latter fails to catch the host (IsServer && IsOwner), causing double-execution: the server handler already applied the ability, then the RPC applies it again. For abilities with coroutines (Teleport, Reflector), this stops the first coroutine mid-execution, corrupting state (e.g. Teleport's collider stays permanently disabled because the second coroutine captures the already-disabled collider and never re-enables it).
- Bug note (teleport visibility): The teleport coroutine previously used `authoritative` to decide whether to hide renderers during the instant position warp. On the host, when `HandleTeleportServer` ran a remote player's teleport with `authoritative: true`, it would hide that player's renderers on the host machine. Although hide/restore happened within the same coroutine frame, the split-screen camera activation in network mode (`ActivateSplitScreen` called unconditionally in `SceneManager`) combined with the renderer hiding to cause the other player to appear invisible. Fix: only the local owner hides renderers (`_netMovement == null || _netMovement.IsOwner`), and `ActivateSplitScreen` is now guarded with `!useNetworkSession`.
- Bug note (teleport Z-coordinate loss): The teleport network path sends the target position as `Vector2` (via `RequestTeleport` / `NetTeleportState`), which drops the Z coordinate. When `ApplyNetworkTeleport` received this as `Vector2` and passed it to `ExecuteTeleport(Vector3)`, the implicit conversion set Z to 0. This could shift the ship's depth layer and cause the `OnTargetObjectWarped` camera warp delta to include an erroneous Z component. Fix: `ApplyNetworkTeleport` now reconstructs the Z from `transform.position.z` before executing the teleport.
- Future bugs or drift issues should be documented here with the exact subsystem affected: prediction, reconciliation, interpolation, spawn flow, or combat replication.
