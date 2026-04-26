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
- the host-selected gameplay scene name is now broadcast to clients at connection / update time so title and ship-select mode-specific routing (such as 2D roster vs 3D roster) stays consistent on both peers before lock-in

### Network Scene Note

The active network gameplay scene now assumes:

- one gameplay camera plus UI overlay, not split-screen cameras
- one local HUD presentation only
- server-selected map activation must be mirrored to clients by map index, because scene-object `SetActive` state is not replicated automatically
- round-start presentation must be broadcast explicitly so clients see the same `ROUND X` banner and opening countdown as the host
- round-end presentation must be broadcast explicitly
- augment selection is a synchronized timed phase with separate local pools for each player
- game-end presentation must also be broadcast explicitly and remapped to local-player perspective on each client
- local result presentation should be derived from `payload.WinningPlayer` versus the local slot, while the actual game-end canvas selection can still stay slot-based (`player1` on host, `player2` on remote client)

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
- `NetDarkMatterHazardSpawnData`
  - spawn position, direction, DPS, lifetime, impact force, slow rate, launch speed
  - dampening flag plus server spawn time so clients subtract transit latency from the hazard lifetime
- `NetFlameWaveCastState`
  - charge request payload for Flame Wave when the owner is not the server
- `NetFlameWaveHazardSpawnData`
  - spawn position, direction, DPS, lifetime, impact force, slow rate, launch speed
  - dampening flag plus server spawn time so clients subtract transit latency from the hazard lifetime
- `NetBatteryRamState`
  - tick, active flag, broken flag, charge-grant flag, and an owner-skip flag so the owner can predict locally while server echoes state to the rest
- `NetFireRequest` / `NetProjectileSpawnData`
  - now include owner-prediction metadata for projectile visuals (`OwnerPredicted`) so owner-predicted shots skip owner replay while server-triggered augment volleys can still be rendered on the owner client
  - now include augment fire metadata (`IgnoreCooldown`, `FireSource`) so server-authoritative augment-driven primary fire chains can request controlled cooldown bypass and source tagging

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
- Dark Matter follows this path: the owner predicts hazards locally, the server spends charges and spawns authoritative hazards, and hazard lifetimes are latency-adjusted on clients using the server spawn time
- Flame Wave now follows the same pattern as Dark Matter: owner prediction is cosmetic, the server is authoritative for spending charges and spawning hazards, and client lifetimes are corrected by subtracting transit time from the spawn payload
- Empower is networked as a pure state toggle through `NetMovement` using the shared `NetAbilityToggleState` payload. The owner predicts the activation locally for immediate emission glow, the server is the authority that runs the duration coroutine on its own copy and broadcasts deactivation at timeout / death, and non-owner clients mirror the emission-pulse cosmetics via `ApplyNetworkEmpowerState`. Because `Player` is disabled on remote copies, Empower still drives its own coroutine and emission-color `MaterialPropertyBlock` writes directly so other abilities that read `Empower.IsEmpoweredActive` (e.g. `GuidedMissile`, `ConvergeBeam`, `Dodge`) see the correct buff state on whichever copy is running their logic
- networked augment loadouts are also pushed from the server to each player replica after spawn and on live augment acquisition so owner/proxy copies do not silently miss augment runtimes
- authoritative combat-state broadcasts now also include an explicit "evasion triggered" flag so remote copies can play Evasion success flash/sound only when the server actually ignored incoming damage
- authoritative combat-state broadcasts now also include an explicit "artificial fairy triggered" flag so remote copies run the same revive flash/scatter/regroup sequence when the server prevents lethal damage

This is intentionally a bridge step that keeps combat authority aligned with the already-implemented movement authority model without requiring every weapon prefab to become a separate NGO network object first.

### Ability Networking Pattern (template for porting an ability)

Use this template when porting any remaining ability through `NetMovement`. It captures what the Empower, BatteryRam, DarkMatter, FlameWave, and Beam ports all have in common so future abilities stay consistent and local multiplayer keeps working unchanged.

**Step 1 — classify the ability.** Pick the closest category; the template scales with the category:

- *Buff / state toggle* (Empower, Reflector, FaerieShift, Invisibility): one bool of authoritative state, no spawns. Simplest. Reuse `NetAbilityToggleState`.
- *Resource-spending cast* (DarkMatter, FlameWave): owner requests, server spends charges and spawns hazards, cosmetic spawn RPC to clients with server spawn time for latency-adjusted lifetimes. Needs a dedicated cast struct and a spawn-data struct.
- *Ongoing combat stream* (Beam, ConvergeBeam): start/stop toggle plus server-authoritative damage instance split from cosmetic client instance. Needs a fire-state struct, and the beam/laser prefab must support a "cosmetic only, no damage" mode. See `LaserBeam` / `Beam.ApplyNetworkBeamState`.
- *Self-state movement* (Dodge, Teleport, ChronoStep): owner predicts the displacement immediately, server replays the same displacement with authoritative final position; non-owners replay the same displacement purely for visuals. Must coexist with `NetMovement`'s own movement authority — do not let the ability fight the reconciliation path.
- *Projectile spawn* (GuidedMissile, GigaBlast): server-authoritative spawn of the real damage-dealing projectile plus cosmetic client spawn. Targets that are `Transform`s must be passed as `NetworkObjectReference`, not by reference.

**Step 2 — add the payload to `NetworkStructs.cs`.** Keep it minimal: only what prediction replay or visual sync actually needs. Follow the shape of `NetBatteryRamState` (tick + flags + `SkipOwner` if the owner predicted locally) or `NetAbilityToggleState` (just `IsActive`) for trivial cases.

**Step 3 — add the broker methods to `NetMovement.Abilities.cs`.** For each ability you add exactly five members, mirroring the Empower / BatteryRam blocks:

- `public void Request<Ability>State(...)` — called by the owner. Early-outs if `!NetTickUtil.IsActive || !IsOwner`. If `IsServer`, calls the server handler directly; otherwise fires the ServerRpc. This is the single entry point the ability script calls.
- `public void Broadcast<Ability>State(...)` (only if the server needs to push a state change that was not owner-requested, e.g. end-of-duration expiry, ram break). Early-outs if `!IsServer`.
- `[ServerRpc] private void Submit<Ability>StateServerRpc(...)` — thin forwarder to the handler.
- `private void Handle<Ability>StateServer(...)` — the server-authoritative entry. Calls the ability's `ApplyNetworkXState(..., authoritative: true)` on the server's copy, then calls the ClientRpc to mirror to non-owners.
- `[ClientRpc] private void Broadcast<Ability>StateClientRpc(...)` — **must** begin with `if (IsServer || IsOwner) return;` (see the ClientRpc guard bug note below). Calls the ability's `ApplyNetworkXState(..., authoritative: false)`.

For cosmetic spawn RPCs (projectiles, hazards), also stamp `ServerSpawnTime` on the server before broadcasting and subtract it from the lifetime on the client side, as `NetFireHazardSpawnData` / `NetFlameWaveHazardSpawnData` already do.

**Step 4 — restructure the ability script.** Three things every ported ability has in common:

- cache a `NetMovement _netMovement` reference in `Awake()` and add two helpers:

  ```csharp
  private bool HasNetworkPath() =>
      NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned;
  private bool HasAuthority() =>
      !NetTickUtil.IsActive || (_netMovement != null && _netMovement.IsSpawned && _netMovement.IsServer);
  ```

  `HasAuthority()` returning `true` when networking is not active is what keeps the local-multiplayer path intact: every authority-gated branch (spend charge, spawn damage projectile, start server timer, apply damage) is still reached in local mode.

- in `TryUseAbility` (or `UseAbility`), branch on `HasNetworkPath()`:
  - *network path, non-owner*: return false immediately — only the owning client can trigger.
  - *network path, owner*: run owner-side prediction locally (cosmetic/input feedback), then call `_netMovement.Request<Ability>State(...)`. Do not spend charges, spawn damage, or mutate authoritative state here; that happens on the server handler.
  - *no network path (local MP)*: run the original non-networked code path unchanged.

- expose `public void ApplyNetworkXState(..., bool authoritative)` that the broker calls. It must be safe to invoke on: (a) the server copy of a client-owned player, (b) a remote proxy on a non-owning client, and (c) the local owner as a reconciliation echo. Idempotence matters — start-if-not-started, stop-if-active.

**Step 5 — split cosmetic vs authoritative for damage-dealing spawns.** If your ability spawns a prefab that deals damage (projectile, beam, hazard, ram collider), the client-side cosmetic instance must *not* apply damage or recoil. Pattern:

- server spawn: full behavior enabled, gameplay simulation runs.
- client cosmetic spawn: collision/damage disabled; visuals, audio, lifetime driven from the broadcast payload.
- on the host, guard against double-application — the server handler already did the work; the ClientRpc `if (IsServer || IsOwner) return;` guard prevents re-entry.

**Step 6 — preserve local multiplayer.** Every network branch must fall through to the existing local code when `NetTickUtil.IsActive` is false. Do not move charge spending, damage application, or spawns *out* of the original flow — wrap them in `HasAuthority()` checks instead so they run in both modes. The `HasAuthority()` helper shown above already does this because it returns `true` when networking is inactive.

**Step 7 — verify in editor in both modes.** Play in a local-multiplayer scene (no `NetMgr` session) and confirm the ability still works for both players. Then play in a networked session (host + client) and verify: owner sees immediate response, server sees the authoritative effect on its copy, the remote client sees the cosmetic mirror, no double-damage on the host, no stale state on early termination.

**Known gotchas when porting** (these have already bitten the repo once and are documented under `Known Networking Notes`):

- ClientRpc guard must be `if (IsServer || IsOwner) return;`, not the inverted form. The host is both and will double-execute otherwise.
- `Transform` targets are not replicated. Pass `NetworkObjectReference` and resolve on each peer.
- `gameObject.tag` is not replicated by NGO — rely on `NetMovement`'s `_networkPlayerIndex` path that calls `RefreshCombatTags()`, don't set tags yourself after spawn.
- If your ability mutates the local `Player` or its rigidbody, remember that remote proxies have `Player` disabled — the ability must tolerate running with a disabled `Player` component on those copies, or be driven only on owner + server.
- For displacement abilities, do not bypass `NetMovement`'s reconciliation by teleporting the rigidbody on a remote copy; route the displacement through the same path ChronoStep/Teleport already use.
- End-of-duration broadcasts must come from the authority side only. Guard broadcast calls with `HasNetworkPath() && HasAuthority()` so local multiplayer does not attempt to RPC.

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
- The same disabled-copy rule now applies to augments: `ExecuteEffects()` runtimes are manually ticked by `NetMovement` on server-authoritative non-owner copies and on remote proxies, instead of assuming `Entity.FixedUpdate()` is still running there.
- Evasion presentation is network-safe only when the successful-evade signal rides through the server combat-state broadcast; relying on local random rolls on clients will desync flash/sound feedback.
- Artificial Fairy presentation is network-safe only when the revive proc signal comes from the authoritative server combat broadcast; client-only lethal checks can drift from server truth and cause fake revive visuals.
- Bug note: round-intro movement locking in network gameplay must not rely only on a one-shot RPC during spawn. If the owner misses that transient lock state, they can move during the opening countdown. Keep the authoritative movement-lock state persisted on `NetMovement` so newly spawned owners apply it immediately on `OnNetworkSpawn`.
- Bug note: HUD/win-indicator visuals are not implicitly synchronized by NGO just because the match state changed on the server. Client HUD layering needs deterministic local canvas camera/sorting setup, and round-win indicators need an explicit replicated payload/event for the current win counts.
- `Entity` banking/pitching on the 3D visual model now rides in `NetStateSnapshot`; remote proxies should consume the replicated visual tilt instead of recomputing it from interpolated root movement, or turns/recoil can look flat or mismatched.
- For client-owned ships, the server/display side should forward the owner's reported visual tilt instead of recomputing bank from the server copy's root rotation, or the host view can over-bank badly.
- Host mode requires special care to avoid double-simulating owner movement, and `NetMovement` already includes host-specific handling for that.
- Host mode now also needs special care for combat visuals versus gameplay authority: owner-side local weapon visuals must not be allowed to double-apply gameplay recoil or damage when the host is also the authoritative server.
- Current UX note: the gameplay ping label is sourced from NGO transport RTT and intentionally displayed as `RTT / 2` because this project wants a smoothed one-way estimate on-screen. That means the shown value is not the same number many tools call "ping" (full RTT), so keep that distinction explicit if the label behavior changes later.
- Bug note: host gameplay copies that are not locally owned must disable `PlayerInput`, not just `Player`. The client proxy branch already did this, but the host's server-authoritative copy of the remote player previously left `PlayerInput` enabled. That created multiple active `PlayerInput` components on the host machine, which broke controller ownership for host gameplay even though the standalone client still worked. Keep non-owner gameplay replicas input-disabled on every peer.
- Bug note: do not call `SetActive(false)` on spawned network player objects as a round-transition visibility shortcut. Scene activation is not replicated by NGO, so the host can locally disable a player while clients keep rendering a frozen copy. That stale object can also poison tag-based combat lookup if networking helpers only filter by tag instead of also requiring a live spawned `NetworkObject`.
- `ProjectileScript`, `LaserBeam`, and `FireHazard` now have a split between cosmetic-only instances and server-authoritative gameplay instances during network sessions. Future combat work should preserve that separation instead of letting client visuals deal damage directly.
- Bug note: server-side projectile rewind sweeps do not have `OnTriggerEnter2D`'s built-in "entry once" behavior. Any projectile that survives impact, especially piercing shots like Tier 3/4 `GigaBlast`, must keep its own "already hit this target during this flight" registry or the same target can be damaged again on later server ticks while the sweep still intersects the defender's rewind radius.
- `NetStateSnapshot` now carries both shield and health so augment-driven healing / max-health changes stay visible on non-authoritative copies instead of living only on the server.
- Bug note: if a networked augment seems to work only for the host, check both halves of the integration: the server copy must execute the runtime even when `Player` is disabled, and the client copies must receive the augment loadout/state explicitly because NGO does not replicate those `AugmentController` runtimes for you.
- Bug note: the title-screen join flow can be triggered by more than one UI/input path on the same client. Guard repeated join presses while `_awaitingClientConnect` is true, otherwise a second `StartClientForMenu()` call will hit NGO after the first call already set `IsListening` and falsely report that a session is already active.
- Bug note: if 2D and 3D use different ship rosters, the selected gameplay scene/mode must be synchronized to clients before ship select opens. Leaving clients on a local default roster can show invalid options that do not match the host's intended mode.
- Bug note: ship-select preview browsing must stay local-only. Do not replicate non-committed ship picks (`lockIn=false`) through `NetworkSessionData`, or one peer's shoulder-button browsing can drive the other peer's preview state. Only committed lock-ins should update replicated ship selection payloads.
- Any networking migration work should assume full-network play is the target and treat old split-screen assumptions as secondary unless specifically required.
- Ability ClientRpc guards must use `if (IsServer || IsOwner) return;`, NOT `if (IsOwner && !IsServer) return;`. The latter fails to catch the host (IsServer && IsOwner), causing double-execution: the server handler already applied the ability, then the RPC applies it again. For abilities with coroutines (Teleport, Reflector), this stops the first coroutine mid-execution, corrupting state (e.g. Teleport's collider stays permanently disabled because the second coroutine captures the already-disabled collider and never re-enables it).
- Bug note (teleport visibility): The teleport coroutine previously used `authoritative` to decide whether to hide renderers during the instant position warp. On the host, when `HandleTeleportServer` ran a remote player's teleport with `authoritative: true`, it would hide that player's renderers on the host machine. Although hide/restore happened within the same coroutine frame, the split-screen camera activation in network mode (`ActivateSplitScreen` called unconditionally in `SceneManager`) combined with the renderer hiding to cause the other player to appear invisible. Fix: only the local owner hides renderers (`_netMovement == null || _netMovement.IsOwner`), and `ActivateSplitScreen` is now guarded with `!useNetworkSession`.
- Chrono Step uses `NetChronoStepState` (plant + teleport) through `NetMovement`. Owners predict locally, the server applies authoritative state, and non-owners replay the plant/teleport without hiding renderers on the host.
- Bug note (teleport Z-coordinate loss): The teleport network path sends the target position as `Vector2` (via `RequestTeleport` / `NetTeleportState`), which drops the Z coordinate. When `ApplyNetworkTeleport` received this as `Vector2` and passed it to `ExecuteTeleport(Vector3)`, the implicit conversion set Z to 0. This could shift the ship's depth layer and cause the `OnTargetObjectWarped` camera warp delta to include an erroneous Z component. Fix: `ApplyNetworkTeleport` now reconstructs the Z from `transform.position.z` before executing the teleport.
- Bug note (cosmetic projectile/beam pass-through): Unity `gameObject.tag` is not replicated by NGO. `SpawnNetworkPlayer()` sets the tag and calls `RefreshCombatTags()` only on the server. On clients, remote proxies had `Player` disabled before `Start()` ran, so `enemyTag` was never set. Cosmetic projectiles (`BroadcastProjectileSpawnClientRpc`) and beams (`Beam.ApplyNetworkBeamState`) read `_player.enemyTag` which was empty/wrong on clients, causing `collider.CompareTag(targetTag)` to fail and projectiles/beams to pass through ships visually. Fix: `NetMovement` now carries a `NetworkVariable<byte> _networkPlayerIndex` that replicates the player index (1=Player1, 2=Player2) to all clients. On value change, clients set `gameObject.tag` and call `RefreshCombatTags()`, ensuring `enemyTag` is correct everywhere.
- Class5 charges are server-authoritative. Passive regen ticks on the server copy (even when the local Player component is disabled), and a dedicated `Class5NetworkBridge` now handles charge count/audio replication plus the four-shot primary fire burst for remote listeners so Class5-specific logic no longer lives in `NetMovement`.
- Empower pairing note: `GuidedMissile`, `ConvergeBeam`, and `Dodge` each cache an `Empower empowerAbility` reference and read `IsEmpoweredActive` to decide between base and empowered variants. When porting those three, make sure the *same side that runs the ability's authoritative logic* is also the side whose `Empower` copy has the correct `_isEmpoweredActive`. In practice this means the server-authoritative handler reads `Empower.IsEmpoweredActive` from the same GameObject (the server's copy of the client-owned player), which works because `Empower` already replicates the activation to the server via `NetMovement.RequestEmpowerState`. Do not try to read the owner's empower state from across peers.
- Bug note: in network gameplay, `SceneManager.player1` was being used for two different things — the canonical "Player 1 slot" (used by spawn/lock/HUD/augment bookkeeping on the host) and the HUD-binding's "local owned player" handle. `BindNetworkPresentation` / `ClearLocalNetworkHudBinding` were writing/nulling that same field, so on round transitions the HUD path could clobber the host's authoritative reference. Specifically, the post-spawn `BindNetworkPresentation()` call ran while `session.CurrentState` was still `AugmentPhase`, hit the early-out, and nulled `player1` between the spawn and `SetAbility4Locked` — so the host's player1 silently kept ability 4 unlocked on round 2. Fix: gate the HUD path's `player1` writes behind `!isAuthoritativeNetworkController` so the host's slot bookkeeping is never overwritten by the local-owned-player lookup. The non-authoritative client still uses `player1` as its local-owned handle for HUD wiring as before.
- Future bugs or drift issues should be documented here with the exact subsystem affected: prediction, reconciliation, interpolation, spawn flow, or combat replication.
