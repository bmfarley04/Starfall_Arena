# AGENTS.md

This file is the AI-facing index for the Starfall Arena repo. Read this first, then load only the docs that match the task.

## Project Snapshot

Starfall Arena is currently a 2.5D space dueling game built in Unity with:

- 3D ships and effects presented in a mostly 2D combat plane
- `Rigidbody2D` gameplay physics
- an orthographic gameplay camera today
- a planned transition to a perspective camera later
- current gameplay focus on 1v1 duels between two players
- active development focus on a fully networked duel experience
- target platforms currently centered on PC/computer and PS4-style controller play

Enemy architecture still exists in code, but enemies are not part of the current playable focus.

This is a controller-first game. Input, UI flow, and gameplay readability should default to controller support first.
Keyboard-and-mouse movement/input support also needs to be maintained alongside controller support.

## Documentation Index

Load the smallest relevant set of docs for the task.

- `AGENTS.md`
  - Start here for repo rules and documentation routing.
- `GAME_SYSTEMS.md`
  - Read for entity hierarchy, combat, weapons, abilities, augments, and current gameplay assumptions.
- `UI_MANAGERS.md`
  - Read for scene flow, UI architecture, selection screens, HUD systems, and manager responsibilities.
- `NETWORK.md`
  - Read for networking philosophy, current implementation status, movement prediction/reconciliation, and current networking gaps.

## When To Read What

- Working on player movement, duel flow, combat feel, damage, shields, projectiles, abilities, or augments:
  - Read `GAME_SYSTEMS.md`
- Working on menus, ship select, augment select, HUD, round flow, or manager responsibilities:
  - Read `UI_MANAGERS.md`
- Working on Netcode for GameObjects, prediction, interpolation, reconciliation, spawning, or future projectile sync:
  - Read `NETWORK.md`
- Working on anything cross-cutting or unclear:
  - Read `GAME_SYSTEMS.md` and `UI_MANAGERS.md` first, then `NETWORK.md` if multiplayer is involved

## Documentation Maintenance Rules

AI contributors must update relevant documentation whenever a notable change is made.

Notable changes include:

- new gameplay systems or removed systems
- changes to combat rules, abilities, augments, damage flow, or weapon behavior
- networking changes or authority-model changes
- UI flow changes, new manager responsibilities, or scene-flow changes
- camera-model changes, especially the eventual orthographic-to-perspective transition
- new recurring implementation constraints that future contributors need to know

Do not leave docs for “later” if code behavior changed in a meaningful way.

## Bug Documentation Rule

When a bug, regression, pitfall, or misleading architectural gotcha is found, document it in the relevant doc section so future contributors do not repeat it.

Examples:

- movement/networking bug -> `NETWORK.md`
- ability or augment bug -> `GAME_SYSTEMS.md`
- HUD, menu, or round-flow bug -> `UI_MANAGERS.md`

Keep bug notes short and actionable. Prefer “what went wrong, why it matters, how to avoid it”.

## Documentation Style

When updating docs:

- describe current implemented behavior first
- clearly label planned or future work as planned
- keep summaries high-signal and concrete
- prefer subsystem-level summaries over line-by-line code narration
- avoid claiming a system is fully networked unless the full path is actually integrated

## Code Rules

When writing or changing code in this repo:

- prefer the simplest solution that fully solves the stated task
- prioritize readability and maintainability over cleverness
- keep gameplay code iteration-friendly for future tuning
- expose tunable gameplay values in the Inspector instead of hardcoding them when they are likely to need balancing
- avoid magic numbers for gameplay, timing, movement, combat, VFX, and audio values
- group related Inspector fields into clear `[System.Serializable]` config structs when that improves clarity
- keep Inspector organization clean and practical, with commonly tuned values easier to find
- use shared patterns already present in the repo when they are reasonable, instead of introducing unnecessary new abstractions
- prefer `SoundEffect` ScriptableObjects for audio configuration rather than scattering raw clip/volume setup through gameplay code
- remove redundant fields, duplicate settings, and unnecessary complexity when possible

## Debugging Rule

When debugging:

- if the solution is not yet clear, add targeted debug statements to gather the missing information
- ask the user to report the debug output when their help is needed to confirm runtime behavior
- keep debug logging focused on the specific uncertainty being investigated
- remove or comment out temporary debug statements once the issue is understood or resolved

## Codebase Orientation

Primary code areas:

- `Assets/Scripts/entities`
  - entity base classes, player base, ship classes, enemy base, augment controller
- `Assets/Scripts/Abilities`
  - ship ability implementations
- `Assets/Scripts/Augments`
  - augment definitions and runtime behaviors
- `Assets/Scripts/Projectiles`
  - projectile and beam weapon behavior
- `Assets/Scripts/UI`
  - HUDs, selection screens, and UI-specific logic
- `Assets/Scripts/Networking`
  - current networking implementation for movement and session helpers
- `Assets/Scripts/3d`
  - future-facing scripts for a fuller 3D version of the game; not an active maintenance focus yet
- `Assets/Scripts/SceneManager.cs`
  - duel and round flow orchestration

## Current Caution Areas

- The game is still documented partly as a broader combat game, but the active implementation focus is two-player dueling.
- Full networking is the active direction, and older local multiplayer/split-screen assumptions should be treated as mostly deprecated unless a task explicitly depends on them.
- Networking has meaningful movement implementation now, but not every gameplay system is integrated into a complete networked match flow yet.
- The camera summary must stay aligned with the ongoing shift from orthographic presentation toward a future perspective setup.
- Both controller and keyboard-and-mouse input paths should continue to work as the game evolves.
