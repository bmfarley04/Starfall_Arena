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
2. online host/join flow
3. host waiting / client connect
4. synchronized ship select
5. gameplay scene load

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
- augment selection timing
- ability 4 unlock timing
- round-end and game-end UI coordination
- cumulative stat collection across rounds

Network migration note:

- the manager now has an initial network-aware path where the server owns the main duel loop
- local split-screen camera activation is skipped when a network session is active
- only the local player's HUD/ability presentation is shown in the active network scene
- round-end presentation is now broadcast to clients through the network session layer instead of only being shown on the host
- round-end and game-end stats in network play should be remapped to the local player's perspective instead of showing the host's seat ordering to everyone

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

## HUD Architecture

### Player HUD

Health and shield HUDs are currently represented with:

- `PlayerHUD`
- `SegmentedBar`
- player-bound text values

These provide the core combat-state display for each dueling player.

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

These are smaller helpers, but they still shape player-facing clarity and should be documented when behavior changes meaningfully.

## UI Data Architecture

### ShipData

`ShipData` is a key ScriptableObject for UI and flow.

It currently carries:

- ship name
- menu preview prefab references
- versus-screen prefab references
- gameplay ship prefab
- ability HUD prefab
- displayed stat values
- ability names, descriptions, and icons

This makes `ShipData` a bridge between:

- ship-select UI
- versus presentation
- gameplay spawning
- HUD configuration

## Current Architectural Notes

- A large amount of duel flow is coordinated through scene managers instead of a formal state-machine framework.
- Several UI systems are heavily controller-specific by design, which is correct for this project.
- Controller-first remains correct, but keyboard-and-mouse support should still remain intact where applicable.
- UI and gameplay flow are tightly coupled around rounds, augments, and unlock timing, so documentation should be updated when any of those rules move.
- Existing split-screen flow should be treated as transitional or legacy-oriented unless a task specifically targets it.
- `TitleScreenManager` now needs serialized references for the join canvas, the host-waiting canvas, the IP input field, and optional status text in addition to the legacy main menu canvas.
- `TitleScreenManager` now owns only the special hold-style join/waiting controls. The main title `Host Game` and `Join Game` controls should stay on the normal clickable title-button pattern.
- `ShipSelectManager` now expects only a countdown timer text reference for the networked ship-select path; once a player locks in, that client should no longer be able to change ships before the timer expires.
- Bug note: the main `Host Game` button must not keep any legacy `ShipSelect` transition wiring in `TitleScreenButton`, or the UI will bypass the waiting screen and appear to host successfully when networking never actually started.
- Active network gameplay now expects a single local-view HUD presentation:
  - the primary HUD/ability canvas is the local player's
  - opponent HUD canvases are not used in the active network scene
  - round/game end screens can temporarily reuse one shared victory-style canvas in network mode until dedicated defeat variants are built
  - that shared canvas should show local-player-first stats on each machine
- Bugs or pitfalls in menu flow, HUD binding, selection order, or round transitions should be documented here in the relevant section.
