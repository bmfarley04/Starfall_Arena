# UI_MANAGERS.md

This document summarizes the current UI architecture, screen flow, and manager responsibilities for the duel game.

Important status note:

- the project is moving toward fully networked duels
- older local multiplayer and split-screen flows are now mostly deprecated as the primary target experience
- some split-screen systems still exist and remain useful reference, but they should not be treated as the long-term center of the game

## High-Level Flow

The current game flow is organized around versus duels:

1. ship selection
2. versus intro screen
3. round start flow
4. live duel HUD
5. round-end and augment phases
6. game-end summary

This flow is orchestrated mostly through scene-level manager scripts rather than a single centralized state machine asset.

## Main Manager Responsibilities

### GameDataManager

`GameDataManager` is a persistent singleton carrying cross-scene selection data.

Current responsibility:

- storing selected ship classes between menu flow and gameplay flow

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

If a task changes duel progression, this file is usually relevant.

### SplitScreenManager

This controls camera presentation between:

- whole-screen presentation for non-combat phases
- split-screen presentation during live duels

This area still matters for understanding current scene flow, but it is no longer the main long-term multiplayer target. Full networking is the current direction.

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

### AugmentSelectManager

`AugmentSelectManager` is the main augment drafting UI.

It handles:

- tiered augment pools
- per-game randomized tier order
- sequential two-player selection
- gamepad-gated navigation
- card animation and highlight behavior
- timeout and countdown handling
- surfacing the selected augment back to the duel flow

Important system note:

- augment selection is not just a UI surface; it is part of round progression and player advantage flow

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
- Bugs or pitfalls in menu flow, HUD binding, selection order, or round transitions should be documented here in the relevant section.
