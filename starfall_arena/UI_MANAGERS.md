# UI_MANAGERS.md

This document summarizes the current UI architecture, screen flow, and manager responsibilities for the duel game.

Important status note:

- the project is moving toward fully networked duels
- older local multiplayer and split-screen flows are now mostly deprecated as the primary target experience
- some split-screen systems still exist and remain useful reference, but they should not be treated as the long-term center of the game
- the title flow now has a direct-IP LAN session path layered over the existing menu canvases

## High-Level Flow

The current game flow is organized around versus duels:

1. ship selection
2. versus intro screen
3. round start flow
4. live duel HUD
5. round-end and augment phases
6. game-end summary

This flow is orchestrated mostly through scene-level manager scripts rather than a single centralized state machine asset.

Current menu/network note:

1. title screen main menu
2. `Host Game` opens the Duel/Invasion host choice canvas, while `Join Game` opens the direct-IP join canvas
3. host waiting / client connect
4. synchronized ship select
5. gameplay scene load (Duel -> normal 2D PvP scene, Invasion -> `3d_invasion`)

## Main Manager Responsibilities

### GameDataManager

`GameDataManager` is a persistent singleton carrying cross-scene selection data.

Current responsibility:

- storing selected ship classes between menu flow and gameplay flow
- acting as the registry for stable ship IDs and augment IDs used by network/session systems

### GameSceneManager

`GameSceneManager` is the central duel-flow coordinator.

It currently owns:

- spawn points
- map activation
- split-screen transitions
- round text and countdown flow
- player spawn lifecycle
- win tracking
- persistent augment icon tracking alongside win trackers
- augment selection timing
- ability 4 unlock timing
- round-end and game-end UI coordination
- cumulative stat collection across rounds
- rolling-average FPS and ping label updates for the gameplay scene overlay

Network migration note:

- the manager now has an initial network-aware path where the server owns the main duel loop
- local split-screen camera activation is skipped when a network session is active
- only the local player's HUD/ability presentation is shown in the active network scene
- round-end presentation is now broadcast to clients through the network session layer instead of only being shown on the host
- round-start `ROUND X` and opening 3-2-1 presentation in network gameplay must also be broadcast through the network session layer so clients see the same intro timing as the host
- round-end and game-end stats in network play should be remapped to the local player's perspective instead of showing the host's seat ordering to everyone
- game-end presentation now also resolves victory/defeat text from the local player's result, not from the server winner slot alone

If a task changes duel progression, this file is usually relevant.

### SplitScreenManager

This controls camera presentation between:

- whole-screen presentation for non-combat phases
- split-screen presentation during live duels

This area still matters for understanding current scene flow, but it is no longer the main long-term multiplayer target. Full networking is the current direction.

Deprecation note:

- active duel flow should no longer depend on split-screen presentation
- the split-screen manager remains as legacy support/reference while the network duel scene is migrated to a single gameplay camera

### MapManagerScript and Map-Specific Managers

Maps are activated through scene flow and can carry their own local management scripts.

Current examples include:

- `MapManagerScript`
- `RingOfFireManager`

These should be thought of as gameplay-environment managers, not just passive visuals.

## Selection Screens

### ShipSelectManager

`ShipSelectManager` is a substantial controller-first UI system, not a simple menu script.

It handles:

- navigating available ships
- showing ship stats
- displaying per-ship ability information
- spawning and animating preview models
- player-by-player selection flow
- hold-to-confirm interactions
- transition into gameplay scene flow

Networked-duel note:

- the same screen now supports a synchronized network path
- each client controls only their own preview and lock-in state
- host/server authority decides when ship select begins, when it times out, and which final ship pair gets carried into gameplay
- UI can now surface the shared countdown timer
- keyboard shoulder-style fallback now supports both `Q/E` and `K/L` alongside controller shoulder navigation

Important architectural note:

- `ShipData` is the main data source for ship-select presentation
- the select screen is driven by ScriptableObject metadata plus scene references

### VersusScreenManager

`VersusScreenManager` handles the duel intro presentation between ship select and gameplay.

It is responsible for:

- reading selected ships from `GameDataManager`
- spawning preview models
- showing ship names
- playing the intro card and VS animation sequence
- notifying the game flow when the sequence is complete

Networked-duel note:

- versus presentation should read the authoritative ship pair from persistent session/game data rather than assuming two local seats selected in order on one machine

### AugmentSelectManager

`AugmentSelectManager` is the main augment drafting UI.

It handles:

- tiered augment pools
- per-game randomized tier order
- simultaneous network-aware selection
- gamepad-gated navigation
- card animation and highlight behavior
- timeout and countdown handling
- surfacing the selected augment back to the duel flow

Important system note:

- augment selection is not just a UI surface; it is part of round progression and player advantage flow
- in the active network duel flow, both players pick simultaneously on the same timer
- the losing player receives 3 augment choices
- the winning player receives 2 augment choices
- those pools are separate, even when both players are drawing from the same tier for the round
- augment cards should remain pointer-hoverable and mouse-clickable during the active flow
- Bug note: `AugmentSelectManager` should keep an explicit selected-card index while manually polling controller/keyboard input. Relying only on `EventSystem.currentSelectedGameObject` can let pointer hover, stale UI-module selection, and manual navigation fight each other, which makes controller selection flicker between adjacent cards in reused reward screens.

## HUD Architecture

### Player HUD

Health and shield HUDs are currently represented with:

- `PlayerHUD`
- `SegmentedBar`
- player-bound text values

These provide the core combat-state display for each dueling player.

3D/network HUD ownership note:

- in the 3D path, scene managers such as `InvasionSceneManager3D` own top-level HUD root activation and deterministic canvas camera/sorting setup
- actual player-data binding is separate and happens through `PlayerHUDManager3D` plus `PlayerHUDBindingTarget3D` listeners on the HUD objects
- ship-specific weapon/ability HUD content is not always a preauthored static child in the scene; `PlayerWeaponAbilityHUDSpawner3D` can instantiate the correct ship HUD prefab at runtime after local-player binding succeeds
- `TargetAwarenessHUD3D` now has two hostile-target presentation contracts in the 3D path: normal enemies may still use brackets/bars/offscreen indicators, while bosses identified through `BossHealthBar3D` use only a dedicated offscreen boss tracker icon and never the normal awareness brackets or health/shield bars
- bug note: if a network client appears to have "no HUD" but manual reactivation of `player-hud` immediately restores it, check scene-manager activation order before investigating bind targets. A scene manager can accidentally hide correctly bound HUD by running its initial inactive-state pass after replicated session-state callbacks already re-enabled gameplay UI.
- bug note: active HUD objects that remain invisible can be a scene-authoring issue rather than a UI-manager issue. In the 3D Invasion scene, zero-scale root `RectTransform`s on `heartCanvas` / `enemyCounterCanvas` made those HUDs look unbound even though the manager references and update logic were fine.

### Ability HUD

Ability HUDs are structured around:

- `AbilityHUDPanel`
- `AbilitySlotUI`
- ship-specific ability HUD prefabs referenced by `ShipData`

This means the ability HUD is partly standardized and partly ship-authored.

Current behavior includes:

- binding the HUD to a spawned player
- tracking cooldown or resource fill
- supporting the separate fourth-slot unlock flow

### Tooltip / Interaction Helpers

Supporting UI scripts include pieces like:

- `AbilityTooltipTrigger`
- `TitleScreenButton`
- `WinTracker`
- `AugmentIconTracker`

Augment tracker behavior note:

- in network play, if only one `AugmentIconTracker` is assigned on `GameSceneManager`, it now auto-binds to the local player's side (slot 0 vs slot 1)
- `AugmentIconTracker` now auto-creates missing child `Image` slots at runtime, so UI can adapt if maximum augment count changes between modes without manual hierarchy edits
- `AugmentIconTracker` can bind to a runtime player reference and validates that player's tag is `Player1` or `Player2` before reading augments
- when bound to a runtime player, `AugmentIconTracker` continuously refreshes from that player's augment list so HUD icons react immediately to augment add/remove changes
- in network play, the single assigned augment tracker should read the local owned player's exported augment loadout, not the scene manager's cached round list, so client HUD icons stay aligned with the replicated runtime state
- in network play with two augment trackers assigned, the trackers should bind to the canonical network player tags (`Player1` and `Player2`) so host and client both show the same slot-to-player mapping

These are smaller helpers, but they still shape player-facing clarity and should be documented when behavior changes meaningfully.

### Hold-Buttons

Several menu flows use custom hold-buttons instead of standard TMP button components.

Current behavior:

- the controls-screen back affordance is a hold-button
- the controls screen now has two separate canvas pages for 2D and 3D control layouts; LB/RB should page between them, while the hold-back path returns to the title menu from either page
- the join-game confirm/back affordances are hold-buttons
- the host-waiting back affordance is a hold-button
- the game-end return-to-title affordance is a hold-button
- these controls are built from images plus a draining fill image, not from a TMP button component
- input handling for these hold-buttons lives in manager scripts such as `TitleScreenManager` and `GameEndScreenManager`, not in the TMP UI element itself

Bug note:

- do not assume a hold-button can be fixed by editing TMP button settings, submit events, or navigation alone; first confirm which image object is acting as the hold-button target and which image is acting as the fill
- when adding or changing a hold-button, document its input mapping and keep the serialized target/fill references aligned with the actual image hierarchy in the scene

## UI Data Architecture

### ShipData

`ShipData` is a key ScriptableObject for UI and flow.

It currently carries:

- ship name
- menu preview prefab references
- versus-screen prefab references
- per-seat additive versus-screen position offsets
- per-seat additive versus-screen rotation offsets
- gameplay ship prefab
- ability HUD prefab
- displayed stat values
- ability names, descriptions, and icons

This makes `ShipData` a bridge between:

- ship-select UI
- versus presentation
- gameplay spawning
- HUD configuration
- networked ship selection lookups now depend on `ShipData` exposing a deterministic stable ID; blank/generated-per-machine IDs will cause clients to fall back to stale ship data
- versus preview transforms should keep shared base framing in `VersusScreenManager` and use `ShipData` offsets only for ship-specific correction; replacing the base rotation per ship makes cards drift into inconsistent framing and turns a data-tuning problem into scene-specific reauthoring

## Current Architectural Notes

- A large amount of duel flow is coordinated through scene managers instead of a formal state-machine framework.
- Several UI systems are heavily controller-specific by design, which is correct for this project.
- Controller-first remains correct, but keyboard-and-mouse support should still remain intact where applicable.
- UI and gameplay flow are tightly coupled around rounds, augments, and unlock timing, so documentation should be updated when any of those rules move.
- Existing split-screen flow should be treated as transitional or legacy-oriented unless a task specifically targets it.
- `TitleScreenManager` now needs serialized references for the join canvas, the host-waiting canvas, the IP input field, and optional status text in addition to the legacy main menu canvas.
- `TitleScreenManager` now also needs a serialized 2D controls canvas plus a separate 3D controls canvas, with LB/RB paging between them and 2D staying the default landing page.
- `TitleScreenManager` now routes `Host Game` directly to the Duel/Invasion host choice canvas. Duel uses the normal 2D PvP scene, while Invasion uses `3d_invasion`.
- `TitleScreenManager` now also has a dedicated host-waiting TMP label that shows `WAITING ON OPPONENT...` for duel hosts and swaps to `WAITING ON TEAMMATE...` when Invasion is selected.
- `TitleScreenManager` keeps the old 2D/3D host-mode fields only as legacy serialized compatibility. The main title path should not route through that old canvas now that 3D duel has been removed.
- `TitleScreenManager` still keeps compatibility methods for old UnityEvent wiring: `StartHosting3DDuelFlow()` now routes to the normal 2D duel host flow instead of loading the removed 3D duel path.
- `TitleScreenManager` now owns only the special hold-style join/waiting controls. The main title `Host Game` and `Join Game` controls should stay on the normal clickable title-button pattern.
- `TitleScreenManager` now slides horizontally between the 2D and 3D controls canvases when the player presses LB/RB, while the back hold on either page still returns to the main menu.
- `TitleScreenManager` now routes hosted matches through a selected gameplay scene before `StartHostForMenu` (`Duel` uses the configured normal 2D PvP scene, and `Invasion` uses `3d_invasion` while still using the 3D ship roster).
- `TitleScreenManager` now also exposes two test-only title shortcuts: `start3dhostflow()` hosts the configured 3D test scene immediately, and `start3dclientflow()` starts a client against the configured direct-IP test address. Those shortcuts still wait for the normal synchronized `ShipSelect` session state, then auto-lock the fixed 3D test pair (`3d_class1` for host / player 1, `3d_class2` for client / player 2) so both peers advance into gameplay without manual picks.
- `TitleScreenManager` now also exposes an auto-start test helper for scene startup. When enabled in the Inspector, the title scene skips its normal intro/menu idle state and immediately enters the configured 3D test flow as either host or client based on a serialized default role.
- `TitleScreenManager` can swap title background roots based on the local `PlayerPrefs` Invasion completion flag. When enabled, `Default Title Background` is active before the player has cleared Invasion and `Invasion Won Title Background` is active after completion.
- `GameDataManager` now owns mode-specific ship registries (`2D` and `3D`) and `ShipSelectManager` resolves the active roster from that mode at screen entry instead of assuming one static list.
- Bug note: hold-to-go-back UI on nonstandard title/menu surfaces such as the controls canvas must listen for the same back input as the rest of the controller-first UI (`B` on keyboard, controller east / Circle). The game-end return hold is a separate confirmation path and should use controller south / A with keyboard `X`, not the east / Circle back button. Do not swap these flows to Enter or left/right-face-button input.
- Bug note: the controls screen page switch is not the same thing as the back hold. LB/RB should only swap between the 2D and 3D controls canvases; if those inputs are routed through the back-hold path, the menu will exit to the main title instead of paging.
- Bug note: title host routing should not reintroduce the old 2D/3D choice. `Host Game` should open the Duel/Invasion canvas directly, and the Duel button should route to the 2D PvP scene rather than the removed 3D duel scene.
- Bug note: the scene-start 3D test helper must route both host and client through the same shared setup path. Splitting startup automation into separate ad hoc host/client code paths makes their ship selection, scene label, and waiting/join UI behavior drift apart over time.
- Bug note: the client-side 3D test helper can hit `ShipSelect` while the join canvas is still animating in. If the title flow only attempts the ship-select transition once and drops requests while another menu transition is active, the client gets stranded on the IP screen even though the session is already progressing. Queue or retry the ship-select transition after the current animation completes.
- Bug note: scene-start auto test entry is not equivalent to pressing the test button later in the frame. If `TitleScreenManager` launches the auto client flow directly inside its own `Start()`, it can race the networking/session startup order and behave differently from the manual button path. Wait until the menu/network singletons are live before invoking the shared test helper.
- Local multiplayer entry should use a title-menu transition into the existing `ShipSelectManager` flow rather than loading the split-screen gameplay scene directly, because the local path still expects sequential Player 1 then Player 2 ship confirmation before scene load.
- Bug note: local gameplay ship prefabs currently ship with `PlayerInput` disabled for the network path, so local gameplay spawning must explicitly re-enable and pair devices or `SampleSceneSplitScreen` will load with both ships unresponsive.
- `ShipSelectManager` now expects only a countdown timer text reference for the networked ship-select path; once a player locks in, that client should no longer be able to change ships before the timer expires.
- Bug note: the main `Host Game` button must not keep any legacy `ShipSelect` transition wiring in `TitleScreenButton`, or the UI will bypass the waiting screen and appear to host successfully when networking never actually started.
- Bug note: the main `Host Game` button must transition into the Duel/Invasion host choice canvas first; do not start hosting directly from that button or the player will lose mode selection and the server can load the wrong gameplay scene after ship select.
- Bug note: 3D menu routing and 3D ship-roster selection are separate pieces of configuration. Pointing the title flow at `3d_invasion` or a dedicated 3D test scene is not enough by itself; `GameDataManager` also has to recognize that scene token as a 3D roster scene or ship select will quietly fall back to the 2D roster.
- Bug note: the 3D test title shortcuts must still leave the join/waiting canvases when the session reaches `ShipSelect`. If they auto-lock ships in the background but stay on the IP/join screen, the client appears stalled even though the network session is actually waiting on ship-select lock-ins.
- Bug note: when 2D and 3D ship rosters differ, ship select must switch to the active mode roster before loading previews/icons. Reusing a stale roster from the previous mode leads to wrong ship options and mismatched preview state when returning to title and hosting again in a different mode.
- Bug note: the main `Local Multiplayer` button must not use direct scene loading to `SampleSceneSplitScreen`, or it will skip local ship select and never capture the two ship choices into `GameDataManager`.
- Bug note: title-screen performance can regress badly on lower-spec hardware if `TitleScreenManager` eagerly spawns every ship-select preview model at scene start while return-to-title still uses a blocking `SceneManager.LoadScene`. In that setup, gameplay-to-title waits are usually dominated by title scene activation, asset upload, and preview-object initialization rather than by the ship-select-to-gameplay preload path. Avoid treating gameplay async preload as proof the menu return path is cheap; profile title-scene startup separately and keep menu preview content lazy or pooled.
- Active network gameplay now expects a single local-view HUD presentation:
- the primary HUD/ability canvas is the local player's
- opponent HUD canvases are not used in the active network scene
- round-start text and countdown should be driven by a replicated network session cue rather than a host-only scene coroutine
- game-end screens should use the local player's canvas slot in network mode for now (`player1` canvas on host, `player2` canvas on the remote client)
- `GameEndScreenManager` now treats its legacy Player 1 canvas wiring as the Victory screen and its legacy Player 2 canvas wiring as the Defeat screen. Result labels should be static text/art on those canvases, while the manager fills only variable stat fields.
- `GameEndScreenManager` can receive an optional final-record text override. The duel flow still uses the normal wins-losses record, while 3D Invasion uses the same wired text field to show the local player's enemy-kill count.
- round-end screens should keep the legacy local-multiplayer winner-canvas behavior, but in network mode they now use the local player's canvas slot (`player1` on host, `player2` on remote client), show owner-perspective stats, and need a per-canvas result label for `WIN` / `LOSS`
- defeat presentation can reuse ship-part scatter from the ship preview prefab, but only if that prefab's visual pieces include `ShipPartScatter` components
- local-player-first stats should still be shown on each machine
- Bug note: `GameEndScreenManager` itself must be active in hierarchy before it starts its spawn/despawn coroutines. Keeping the result canvases inactive is fine, but disabling the manager object or one of its parent UI containers prevents `StartCoroutine(...)` from running and can stop the end screen before it activates its canvas. The manager now activates its host path when presentation starts.
- Bug note: game-end presentation in both local and network flow must explicitly hide gameplay HUD canvases and any runtime-instantiated ability HUD objects before showing the final screen, or the client HUD can reappear under the end-screen canvas.
- Bug note: in network gameplay, the local gameplay HUD must resolve its ability HUD prefab from the spawned `NetworkObject.OwnerClientId` / session slot, not from a hardcoded `Player1` canvas tag. Otherwise clients can instantiate the host ship's ability HUD and then keep it because the scene manager thinks a HUD already exists.
- Bug note: the client HUD presentation refresh must not re-bind an already-bound `AbilityHUDPanel` to the same local player every polling tick. Rebinding resets the slot visuals, which makes locked or cooling-down ability 4 flicker between ready and cooldown on clients.
- Bug note: round-intro movement locking in network gameplay must stay active for the full replicated intro window. Do not rely only on a transient spawn-time lock call, or clients can move before the countdown finishes.
- Bug note: round-end freeze must explicitly clear latched player input and stop active abilities, not just zero velocity. Otherwise held thrust or other hold-style actions can keep simulating into the round-end window until the player object is destroyed.
- Bug note: the network gameplay HUD and runtime ability HUDs must be forced into a deterministic camera/sorting configuration on each client. Leaving them as `Screen Space - Camera` canvases with implicit camera assignment or default sorting can make asteroid/map visuals render over client HUD elements even when the host looks correct.
- Bug note: once a gameplay scene manager assigns a dedicated UI camera to screen-space HUD canvases, child HUD scripts must not immediately overwrite those canvases back to `Camera.main`. Keep UI-camera ownership centralized or the HUD will behave differently between host, client, and scene setups.
- Bug note: top-right win indicators in network gameplay need explicit replicated win-count updates. Host-local `UpdateWinTrackers()` calls do not automatically refresh client visuals unless the counts are broadcast through the session layer.
- Bug note: the client-side network HUD rebinding loop must stop re-showing gameplay HUD/ability UI during non-combat presentation states such as augment selection. If polling only checks "local player exists", the client can resurrect the gameplay ability HUD underneath whole-screen UI even though the scene manager already hid it for the phase.
- Bug note: the network gameplay HUD on clients must treat a local-player respawn as a full rebind, not as "same owner, keep existing HUD." The losing client has a brief no-owner gap after death; if the old HUD/ability panel survives that gap, the next round can stay stuck on the dead ship's last health/shield values and a hidden ability panel because the new player belongs to the same client but is a different `NetworkObject`.
- Bug note: the host's network gameplay HUD bind cannot rely on a single "wait one frame, then bind" call after respawn. The host authoritative scene manager does not run the client polling loop, so if the new owner object has not completed network spawn yet, the host can enter the round with the previous round's zeroed health/shield text and no runtime ability HUD until another explicit bind happens.
- Bug note: when a host-owned network player despawns between rounds, its old `PlayerInput` must be explicitly deactivated. Leaving the dead owner's input component alive into the respawn window can prevent the new host-owned ship from becoming the active local control/HUD target, which shows up as missing ability HUD, stale `0` stats, and nonresponsive host input.
- Bug note: `SegmentedBar` damage-flash visuals must be cleared when a HUD is rebound for a new round/player. Reinitializing only the fill alpha is not enough; any in-flight flash coroutine or temporary flash material can survive the previous round and make a fresh full bar render with stale white/depleted-looking segments.
- Bug note: gameplay-scene FPS/ping overlay text is now updated by `GameSceneManager`, and it must be hidden when the game-end screen is shown so the final presentation is not rendered with leftover live-match telemetry.
- Bugs or pitfalls in menu flow, HUD binding, selection order, or round transitions should be documented here in the relevant section.
