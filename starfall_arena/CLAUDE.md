# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**StellarOnslaught** is a 2D space shooter game built with Unity. The game features a player-controlled ship defending against waves of enemies with various AI behaviors, shield mechanics, and weapon systems (projectiles and laser beams).

## Development Environment

**Engine:** Unity 6 (2023.x LTS)

**Key Dependencies:**
- Input System (1.14.1) - Modern input handling
- Cinemachine (3.1.5) - Camera control and screen shake effects
- Universal Render Pipeline (17.2.0) - Modern graphics rendering
- TextMeshPro (UI text rendering)
- 2D Animation & Sprite packages

**Build Profiles:** PC/Windows (project is configured for desktop)

## Technical Approach: 3D Objects in a 2D Game

This project uses 3D objects (with Z-depth) rendered in a 2D orthographic view rather than pure 2D sprites. This approach enables:
- Realistic depth-based visual effects (banking, pitching, parallax)
- Natural layering without manual Z-sorting
- Smooth 3D rotation and transformations

Objects use Rigidbody2D for physics but have 3D models/meshes positioned in 3D space. The camera is orthographic looking down the Z-axis, making the game appear 2D while leveraging 3D rendering capabilities. This hybrid approach allows for sophisticated visual effects while maintaining 2D gameplay physics.

## Architecture Overview

### Core Class Hierarchy

The game uses a **base class pattern** with `ShipBase` as the foundation for all controllable entities:

- **ShipBase** (`Assets/Scripts/ShipBase.cs`) - Abstract base class for all ships (player and enemies)
  - Encapsulates shared ship mechanics: movement, rotation, health/shield, combat (projectiles/beams), visual effects (banking, pitching, thrusters)
  - Defines serialized weapon configs: `ProjectileWeaponConfig` and `BeamWeaponConfig`
  - Handles Rigidbody2D physics, damage system with `DamageSource` enum

- **PlayerScript** (`Assets/Scripts/PlayerScript.cs`) - Player-controlled ship
  - Extends ShipBase with player-specific features
  - Input handling via Unity's new Input System
  - UI integration (health/shield bars)
  - Shield regeneration mechanics with delay
  - Friction-based deceleration system
  - Chromatic aberration visual feedback on damage
  - Beam weapon rotation slowdown penalty

- **EnemyScript** (`Assets/Scripts/EnemyScript.cs`) - Abstract base for all enemies
  - Extends ShipBase with enemy-specific AI
  - State machine: `Patrolling`, `Pursuing`, `Searching`
  - Detection system (detection range, lose target range)
  - Lead targeting capability
  - Fire rate and aim tolerance settings
  - Wandering behavior with random direction changes

### Enemy Type Implementations

Different enemy types extend EnemyScript with unique behaviors:

- **BasicEnemyScript** - Standard enemy, minimal code
- **ArtilleryEnemyScript** - Uses turret weapons (multi-barrel firing)
- **SuicideEnemyScript** - Kamikaze-style behavior
- **BoundaryGuardianScript** - Boss-like enemy
- **BulletHellEnemyScript** - Fires patterns of projectiles

### Game Management

- **MapManagerScript** (`Assets/Scripts/MapManagerScript.cs`) - Wave system controller
  - Manages enemy spawning via serialized `WaveConfig` with `EnemySpawnConfig`
  - Handles asteroid patterns (Field, Belt, Ring, Grid, Cluster)
  - Implements spawn safety to prevent spawning near player
  - Wave text animations (fade in/out with glow)
  - Enemy and asteroid cleanup

- **SceneManagerScript** (`Assets/Scripts/SceneManagerScript.cs`) - Global game state and scene transitions
- **MainMenuManager** & **GameOverSceneManager** - Menu/UI controllers

### Combat System

**Weapon Architecture:**
- Ships can have a primary projectile weapon and a beam weapon
- Projectiles: velocity-based, impact force, configurable lifetime (Assets/Scripts/Projectiles/)
- Beam weapons: continuous damage, max range, recoil force
- Both weapon types support recoil mechanics that affect ship movement

**Shield System** (`Assets/Scripts/ShieldController.cs`):
- Separate shield pools per ship
- Regeneration delays (configurable in PlayerScript)
- Visual representation with UI bars

**Damage System:**
- `DamageSource` enum tracks damage origin (Projectile, LaserBeam, Explosion, Other)
- All damage flows through ShipBase health/shield deductions

### Background & Visuals

Procedural background system in `Assets/Scripts/Background/`:
- ProceduralStarfield - Runtime-generated starfield
- NebulaCloud, DustField - Particle effect layers
- ParallaxLayer - Depth scrolling
- Various texture generators (CreateBackgroundTextures, CreateStarSprite, etc.)

## Code Quality & Style

**Priorities:**
- Keep code simple and concise. Avoid over-engineering or premature abstractions
- Expose all tunable parameters as serialized fields in the Inspector—no magic numbers
- Prefer inheritance and composition patterns already in the codebase (ShipBase, EnemyScript)
- Use meaningful variable names; avoid cryptic abbreviations
- Add comments only where logic isn't self-evident

**Parameter Exposure:**
All gameplay-affecting values should be editable in the Inspector to enable rapid iteration without recompilation. Examples:
- Movement values (thrust, rotation speed, max speed, damping)
- Combat values (damage, fire rate, projectile speed, recoil)
- Detection ranges and thresholds for AI
- Visual parameters (bank angle, pitch intensity, particle emission)
- Timing values (cooldowns, regeneration delays, animation durations)

This approach enables designers and developers to balance the game without touching code.

## Key Design Patterns

### Serializable Configurations

Heavy use of `[System.Serializable]` structs for weapon and wave configs allows designers to tweak parameters in the Inspector without code changes.

### Enemy Spawning

Waves are defined as lists of prefabs with positional offsets, enabling flexible formation layouts without hardcoding.

### Input Handling

Uses the modern Unity Input System with `InputAction` and `PlayerInput` component for robust input abstraction.

## Project Structure

```
Assets/
├── Scripts/
│   ├── PlayerScript.cs                    // Player ship
│   ├── ShipBase.cs                        // Ship base class
│   ├── EnemyScript.cs                     // Enemy base class
│   ├── [Enemy Types]*Script.cs            // Specific enemy implementations
│   ├── MapManagerScript.cs                // Wave/level manager
│   ├── SceneManagerScript.cs              // Game state/transitions
│   ├── MainMenuManager.cs, GameOverSceneManager.cs
│   ├── ShieldController.cs                // Shield mechanics
│   ├── Background/                        // Procedural background scripts
│   └── Projectiles/
│       ├── ProjectileScript.cs
│       ├── LaserBeam.cs
│       └── ProjectileVisualController.cs
├── Scenes/
│   ├── MainMenu.unity
│   ├── AllWaves.unity                     // Main gameplay scene
│   ├── GameOver.unity
│   └── Deprecated/
├── Prefabs/
│   ├── ParticleEffects/
│   └── [Enemy & Weapon prefabs]
├── Materials/
│   ├── Projectiles/
│   ├── Asteroids/
│   └── Background/
└── Animations/
```

## Common Development Tasks

### Adding a New Enemy Type

1. Create a new script extending `EnemyScript` (e.g., `CustomEnemyScript.cs`)
2. Override `FixedUpdate()` for movement logic and `TryFire()` for custom firing patterns
3. Set weapon configs in serialized fields (inherited from ShipBase)
4. Create a prefab with the script, Rigidbody2D, and visual model
5. Add to MapManagerScript wave configs in the Inspector

**Important Implementation Patterns:**

- **No Aim Requirement:** Override `TryFire()` and skip `IsAimedAtTarget()` check if enemy should fire continuously in all directions
- **Disable Default Aiming:** Override `RotateTowardTarget()` to prevent unwanted rotation behavior
- **Spinning/Stationary Behavior:** If enemy should stay in place while spinning:
  - Create `_isSpinning` flag to control behavior state
  - Override `RotateTowardTarget()` to call custom spin method when needed
  - Override `FixedUpdate()` to return early when spinning (skip `base.FixedUpdate()`) and freeze velocity: `_rb.linearVelocity = Vector2.zero; _rb.angularVelocity = 0f`
  - Lock position in `MovePursuit()` by setting velocities to zero when in special states
- **Multiple Firing Patterns:** Use an enum to track pattern state and switch between them based on volleys fired
- **Laser Beams:** If using multiple laser beams, store in an array and manage lifecycle (spawn, update recoil, cleanup on destroy)
- **Pattern-Specific Fire Rates:** Expose individual fire rate settings for each pattern instead of relying on base `fireRate`

### Tweaking Ship Parameters

All movement, combat, and visual parameters are exposed in the Inspector:
- Thrust, rotation speed, max speed in ShipBase
- Weapon damage, cooldown, projectile speed in weapon configs
- Shield regeneration (PlayerScript only)
- Enemy detection ranges in EnemyScript

### Adding Weapon Effects

Weapon configs reference GameObject prefabs. Modify `Projectiles/ProjectileVisualController.cs` for visual feedback (trails, impacts). Laser beams are LineRenderers in `LaserBeam.cs`.

### Understanding Damage Flow

1. Projectile/Beam hits → `OnTriggerEnter2D()` or raycast detection
2. Calls `TakeDamage(damage, DamageSource)` on ShipBase
3. ShipBase deducts from shield first, then health
4. Health reaches 0 → `Die()` method → object destruction/effects

## Important Implementation Details

### Recoil System

Ships apply recoil force opposite to weapon direction. Projectile weapons apply recoil per shot; beam weapons apply continuous recoil per second. This affects movement momentum.

### Banking & Pitching

Visual 3D effect on 2D ships: the `visualModel` child object rotates based on lateral velocity (banking) and thrust direction (pitching). Configurable sensitivity and smoothing.

### Thruster Particle Systems

Ships can have multiple thruster particle systems that ramp emission up/down smoothly based on thrust input. Configured in ShipBase serialized fields.

### Shield Regeneration (Player Only)

Shields don't regenerate immediately after damage—configurable delay (`shieldRegenDelay`) before regeneration starts, then regenerates at `shieldRegenRate` per second.

### Enemy States & Transitions

Enemies transition between Patrolling (random wandering), Pursuing (player in range), and Searching (lost player, check last position) automatically based on detection ranges and proximity.

## Testing & Iteration

- **Main scene:** `Assets/Scenes/AllWaves.unity` - Full game with all waves
- **Inspector tweaking:** Most balance adjustments happen without recompiling via Inspector parameters
- **Prefab variants:** Enemy prefabs are configured per-type with different stats in the Inspector

## Notes for Future Development

- **Object Pooling Considerations:** Recent commits indicate exploration of object pooling; currently objects are instantiated/destroyed directly
- **Visual Polish:** Chromatic aberration, screen shake, particle effects are actively tuned parameters
- **Wave Design:** The WaveConfig system is flexible—new enemy types automatically work in waves once prefabs are created
- **Performance:** Background generation is procedural; monitor if adding more visual layers impacts performance
