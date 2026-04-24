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

Performance is a core product constraint.
The game needs to ship to PS4-class hardware, so systems should be designed with runtime cost, memory churn, and scalability in mind from the start rather than optimized only at the end.

## Documentation Index

Load the smallest relevant set of docs for the task. If it is noted that we are working in the 3d game implementation, you should primarily reference just the 3D files. 

- `AGENTS.md`
  - Start here for repo rules and documentation routing.
- `3D.md`
  - Read first for 3D implementation ground rules and routing into the focused 3D doc set.
- `3D_SYSTEMS.md`
  - Read for 3D architecture, system responsibilities, script placement, and cross-system implementation rules.
- `3D_COMBAT.md`
  - Read for 3D weapons, abilities, aiming, projectiles, beams, and combat HUD expectations.
- `3D_BUGS.md`
  - Read for recurring 3D translation bugs, regressions, and implementation pitfalls.
- `GAME_SYSTEMS.md`
  - Read for entity hierarchy, combat, weapons, abilities, augments, and current gameplay assumptions.
- `UI_MANAGERS.md`
  - Read for scene flow, UI architecture, selection screens, HUD systems, and manager responsibilities.
- `NETWORK.md`
  - Read for networking philosophy, current implementation status, movement prediction/reconciliation, and current networking gaps.

## When To Read What

- Working on the fuller 3D implementation path, perspective-camera gameplay, 3D ship movement, 3D combat readability, or translating an existing 2D/2.5D system into 3D:
  - Read `3D.md` first
  - Then load `3D_SYSTEMS.md`, `3D_COMBAT.md`, and/or `3D_BUGS.md` based on the area being changed
  - Then load `GAME_SYSTEMS.md`, `UI_MANAGERS.md`, and/or `NETWORK.md` only as needed for the shared system being translated
  - Keep code edits inside `Assets/Scripts/3d` by default
  - If the fix appears to require changing non-3D code, ask the user for permission before editing outside `Assets/Scripts/3d`
- Working on player movement, duel flow, combat feel, damage, shields, projectiles, abilities, or augments:
  - Read `GAME_SYSTEMS.md`
- Working on menus, ship select, augment select, HUD, round flow, or manager responsibilities:
  - Read `UI_MANAGERS.md`
- Working on Netcode for GameObjects, prediction, interpolation, reconciliation, spawning, or future projectile sync:
  - Read `NETWORK.md`
- Working on anything cross-cutting or unclear:
  - Read `3D.md` first if the task is part of the 3D transition
  - Otherwise read `GAME_SYSTEMS.md` and `UI_MANAGERS.md` first, then `NETWORK.md` if multiplayer is involved

## Documentation Maintenance Rules

AI contributors must update relevant documentation whenever a notable change is made.

Notable changes include:

- new gameplay systems or removed systems
- changes to combat rules, abilities, augments, damage flow, or weapon behavior
- networking changes or authority-model changes
- UI flow changes, new manager responsibilities, or scene-flow changes
- camera-model changes, especially the eventual orthographic-to-perspective transition
- new 3D-translation rules, 3D-specific constraints, or newly identified 3D-only pitfalls
- new recurring implementation constraints that future contributors need to know

Do not leave docs for “later” if code behavior changed in a meaningful way.

## Bug Documentation Rule

When a bug, regression, pitfall, or misleading architectural gotcha is found, document it in the relevant doc section so future contributors do not repeat it.

Examples:

- movement/networking bug -> `NETWORK.md`
- ability or augment bug -> `GAME_SYSTEMS.md`
- HUD, menu, or round-flow bug -> `UI_MANAGERS.md`
- 3D-translation bug, camera/readability pitfall, or 3D movement gotcha -> `3D_BUGS.md`

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
- when a task is explicitly scoped to the 3D implementation path, do not edit non-3D gameplay scripts without first getting the user's permission
- prioritize readability and maintainability over cleverness
- keep gameplay code iteration-friendly for future tuning
- treat performance as a core requirement, especially for PS4-targeted gameplay paths
- expose tunable gameplay values in the Inspector instead of hardcoding them when they are likely to need balancing
- avoid magic numbers for gameplay, timing, movement, combat, VFX, and audio values
- group related Inspector fields into clear `[System.Serializable]` config structs when that improves clarity
- keep Inspector organization clean and practical, with commonly tuned values easier to find
- use shared patterns already present in the repo when they are reasonable, instead of introducing unnecessary new abstractions
- prefer `SoundEffect` ScriptableObjects for audio configuration rather than scattering raw clip/volume setup through gameplay code
- prefer performance-conscious patterns such as object pooling and avoiding unnecessary allocations, repeated instantiation/destruction, or overly expensive per-frame work
- remove redundant fields, duplicate settings, and unnecessary complexity when possible
- after making edits, ALWAYS tell the user what to change in editor to faciliate the changes. 

## Debugging Rule

When debugging:

- if the solution is not yet clear, add targeted debug statements to gather the missing information
- ask the user to report the debug output when their help is needed to confirm runtime behavior
- keep debug logging focused on the specific uncertainty being investigated
- remove or comment out temporary debug statements once the issue is understood or resolved
- When running builds or tests, do not leave temporary .dotnet or bin/obj folders in the root. Use the existing Unity structure or clean up after yourself

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
  - active implementation path for translating the game into a fuller 3D experience; read `3D.md` before making changes here
- `Assets/Scripts/SceneManager.cs`
  - duel and round flow orchestration

## Current Caution Areas

- The game is still documented partly as a broader combat game, but the active implementation focus is two-player dueling.
- Full networking is the active direction, and older local multiplayer/split-screen assumptions should be treated as mostly deprecated unless a task explicitly depends on them.
- Networking has meaningful movement implementation now, but not every gameplay system is integrated into a complete networked match flow yet.
- The camera summary must stay aligned with the ongoing shift from orthographic presentation toward a future perspective setup.
- 3D implementation work should be routed through `3D.md` first instead of being treated as a minor extension of the current 2.5D path.
- Performance matters throughout the repo because the project needs to build for PS4-class hardware; avoid treating optimization as cleanup for later.
- Both controller and keyboard-and-mouse input paths should continue to work as the game evolves.
