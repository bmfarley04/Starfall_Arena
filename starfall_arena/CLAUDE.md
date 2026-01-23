# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Starfall Arena** is a 2.5D space shooter game built with Unity. Players control a ship defending against waves of enemies with various AI behaviors, featuring shield mechanics, weapon systems, and special abilities.

## Development Environment

**Engine:** Unity 6 (2023.x LTS)

**Key Dependencies:**
- Input System (1.14.1) - Modern input handling
- Cinemachine (3.1.5) - Camera control and screen shake effects
- Universal Render Pipeline (17.2.0) - Modern graphics rendering
- TextMeshPro (UI text rendering)
- 2D Animation & Sprite packages

**Build Profiles:** PC/Windows (project is configured for desktop)

## Technical Approach: 2.5D Space Combat

This project uses **3D objects with Z-depth rendered in an orthographic 2D view** rather than pure 2D sprites. This "2.5D" approach enables:
- Realistic depth-based visual effects (banking, pitching, parallax)
- Natural layering without manual Z-sorting
- Smooth 3D rotation and transformations
- 2D physics with 3D visuals

Objects use `Rigidbody2D` for physics but have 3D models/meshes positioned in 3D space. The camera is orthographic looking down the Z-axis, making the game appear 2D while leveraging 3D rendering capabilities.

## Architecture Overview

### Core Class Hierarchy

The game uses a **base class pattern** with `Entity` as the foundation for all combat ships:

```
Entity (abstract base)
  ├─ Player (abstract player base)
  │   └─ Class1 (specific ship class with 4 abilities)
  │
  └─ Enemy (abstract enemy base) [future]
      └─ [Enemy implementations] [future]
```

- **Entity** (`Assets/Scripts/entities/Entity.cs`) - Abstract base class for all ships
  - Encapsulates shared ship mechanics: movement, rotation, health/shield, combat (projectiles), visual effects (banking, pitching, thrusters)
  - Defines serialized weapon config: `ProjectileWeaponConfig`
  - Handles Rigidbody2D physics, damage system with `DamageSource` enum
  - Uses struct-based organization for Inspector clarity

- **Player** (`Assets/Scripts/entities/player/Player.cs`) - Player-controlled ship base
  - Extends Entity with player-specific features
  - Input handling via Unity's new Input System
  - Shield regeneration mechanics with delay
  - Friction-based deceleration system
  - Visual feedback (chromatic aberration on damage)
  - Screen shake system
  - Audio system with pooled AudioSources
  - Uses SoundEffect scriptable objects

- **Class1** (`Assets/Scripts/entities/player/ship_classes/Class1.cs`) - Specific player ship implementation
  - Extends Player with 4 unique abilities
  - Ability 1: Beam Weapon (continuous laser)
  - Ability 2: Reflect Shield / Parry (deflects enemy projectiles)
  - Ability 3: Teleport (dash with animation)
  - Ability 4: Giga Blast (charged shot with 4 power tiers)
  - All abilities grouped in unified `AbilitiesConfig` struct

### Combat System

**Weapon Architecture:**
- **Primary Weapon**: Projectile-based, velocity-driven, configurable damage/speed/recoil/lifetime
- **Abilities**: Ship-specific special weapons (beam, teleport, giga blast, reflect shield)
- All weapons support recoil mechanics that affect ship movement

**Projectile System** (`Assets/Scripts/Projectiles/`):
- `ProjectileScript.cs` - Velocity-based movement, collision detection, impact force
- `ProjectileVisualController.cs` - Visual feedback (trails, impacts)
- `LaserBeam.cs` - Continuous beam with raycast detection, LineRenderer visuals

**Shield System** (`Assets/Scripts/ShieldController.cs`):
- Separate shield pools per ship
- Regeneration delays (configurable in Player)
- Visual representation with UI bars

**Damage System:**
- `DamageSource` enum tracks damage origin (Projectile, LaserBeam, Explosion, Other)
- All damage flows through Entity's `TakeDamage()` method
- Shields absorb damage first, then health
- Health reaches 0 → `Die()` method → destruction/effects

### Background & Visuals

Procedural background system in `Assets/Scripts/Background/`:
- `ProceduralStarfield` - Runtime-generated starfield
- `NebulaCloud`, `DustField` - Particle effect layers
- `ParallaxLayer` - Depth scrolling
- Various texture generators for dynamic backgrounds

## Code Organization Philosophy

### Inspector Organization Priorities

**Field exposure order (top to bottom):**
1. **Core Combat Stats** - Health, shield, fire rate (most frequently tuned)
2. **Movement** - Thrust, speed, rotation
3. **Primary Weapon** - Main projectile weapon
4. **Abilities** - All special abilities grouped together
5. **Visual Effects** - Banking, pitching, explosions
6. **Thrusters** - Particle effects
7. **Advanced/Technical** - Input, friction, feedback systems
8. **Sound Effects** - Grouped with associated functionality

**Key Principles:**
- **Group related settings** - Don't scatter shield settings across multiple sections
- **Most-tuned fields first** - Damage/health at top, technical details at bottom
- **Logical flow** - Stats → Movement → Combat → Abilities → Polish

### Struct Usage Guidelines

**When to use `[System.Serializable]` structs:**
- ✅ **3+ related fields** that form a logical group
- ✅ **Settings users might want to collapse** to reduce clutter
- ✅ **Configs that repeat** across multiple classes (MovementConfig, etc.)
- ✅ **Complex abilities** with many parameters (GigaBlast, Teleport)

**Struct organization best practices:**
- Use `[Header("...")]` within structs to organize large groups
- Nest structs for complex features (e.g., TeleportAbilityConfig contains AnimationConfig, VisualConfig)
- Keep nesting ≤ 3 levels deep for Inspector performance
- Use descriptive struct names ending in "Config" or "Settings"

**Example:**
```csharp
[System.Serializable]
public struct MovementConfig
{
    public float thrustForce;
    public float maxSpeed;
    public float rotationSpeed;
    public float lateralDamping;
}

[Header("Movement")]
[SerializeField] private MovementConfig movement;
```

Now in code: `movement.thrustForce` instead of `thrustForce`.

### Sound Effect System

**Use SoundEffect scriptable objects** for all audio:

1. **Create scriptable object:**
   - Right-click in Project → Create → Audio → Sound Effect
   - Set AudioClip, volume, pitch range
   - Save in `Assets/Audio/SoundEffects/`

2. **Declare in script:**
   ```csharp
   [SerializeField] private SoundEffect projectileFireSound;
   ```

3. **Play in code:**
   ```csharp
   projectileFireSound.Play(audioSource);
   ```

**Benefits:**
- All audio parameters (volume, pitch, fade) in one asset
- No volume control structs cluttering ship scripts
- Easy to swap sounds without code changes
- Designers can tune audio independently

**Integration with AudioSource pooling:**
- Player maintains pool of AudioSources for overlapping sounds
- Beam/looping sounds use dedicated AudioSources
- Call `GetAvailableAudioSource()` for one-shot sounds

### Field Exposure Rules

**Always expose:**
- ✅ Combat values (damage, fire rate, cooldowns)
- ✅ Movement parameters (thrust, max speed, rotation)
- ✅ Visual parameters (bank angle, particle emission)
- ✅ Timing values (delays, durations, regeneration rates)
- ✅ References (prefabs, transforms, particle systems)

**Never hardcode:**
- ❌ Magic numbers (use `[SerializeField]` constants)
- ❌ Tunable gameplay values
- ❌ Visual effect intensities
- ❌ Audio volumes/pitches

**Consider removing:**
- ❌ Fields controlled by Unity systems (particle emission rates if particle system controls them)
- ❌ Redundant toggles (e.g., "hasShield" if you can check `shieldController != null`)
- ❌ Fields never tuned after initial setup
- ❌ Duplicate settings split across classes

**Use tooltips for clarity:**
```csharp
[Tooltip("Time in seconds before shield starts regenerating after damage")]
[SerializeField] private float shieldRegenDelay = 3f;
```

## Key Design Patterns

### Serializable Configurations

Heavy use of `[System.Serializable]` structs for weapon and ability configs allows designers to tweak parameters in the Inspector without code changes. This is the **primary pattern** for this codebase.

### Scriptable Objects for Assets

Audio, visual effects, and other asset-based configurations use ScriptableObjects (e.g., `SoundEffect`) to separate data from logic and enable asset reuse.

### Input Handling

Uses the modern Unity Input System with `InputAction` and `PlayerInput` component for robust input abstraction across keyboard/mouse/gamepad.

## Project Structure

```
Assets/
├── Scripts/
│   ├── entities/
│   │   ├── Entity.cs                       // Base ship class
│   │   └── player/
│   │       ├── Player.cs                   // Player base class
│   │       └── ship_classes/
│   │           └── Class1.cs               // Specific ship implementation
│   ├── Projectiles/
│   │   ├── ProjectileScript.cs
│   │   ├── LaserBeam.cs
│   │   └── ProjectileVisualController.cs
│   ├── Background/                         // Procedural background scripts
│   ├── ShieldController.cs
│   ├── SoundEffect.cs                      // Audio scriptable object
│   └── [Game managers, UI controllers]
├── Scenes/
│   ├── MainMenu.unity
│   ├── SampleScene.unity                   // Main gameplay scene
│   └── GameOver.unity
├── Prefabs/
│   ├── ParticleEffects/
│   ├── Weapons/
│   └── [Ship prefabs]
├── Audio/
│   └── SoundEffects/                       // SoundEffect scriptable objects
├── Materials/
│   ├── Projectiles/
│   └── Background/
└── Animations/
```

## Common Development Tasks

### Adding a New Ship Class

1. Create a new script extending `Player` (e.g., `Class2.cs`)
2. Define ability configs as serialized structs
3. Group all abilities in a master `AbilitiesConfig` struct
4. Implement ability logic in `Update()` / `FixedUpdate()`
5. Create SoundEffect assets for ability sounds
6. Create a prefab with the script, Rigidbody2D, and visual model

### Adding a New Ability

1. **Define ability config struct:**
   ```csharp
   [System.Serializable]
   public struct MyAbilityConfig
   {
       [Header("Timing")]
       public float cooldown;

       [Header("Sound Effects")]
       public SoundEffect activateSound;
   }
   ```

2. **Add to ship's AbilitiesConfig:**
   ```csharp
   [System.Serializable]
   public struct AbilitiesConfig
   {
       public BeamAbilityConfig beam;
       public MyAbilityConfig myAbility;  // New ability
   }
   ```

3. **Implement ability logic** in ship class
4. **Create SoundEffect assets** for ability sounds
5. **Group sound effects** with ability in struct (not separate section)

### Tweaking Ship Parameters

All parameters are exposed in the Inspector via structs:
- **Entity**: Movement, visual effects, thrusters, primary weapon
- **Player**: Shield regen, input, friction, visual feedback, screen shake
- **Class1**: Primary weapon fire rate, all 4 abilities (beam, reflect, teleport, giga blast)

Collapse structs you're not actively tuning to reduce clutter.

### Adding Sound Effects

1. Create SoundEffect scriptable object in `Assets/Audio/SoundEffects/`
2. Assign AudioClip, set volume (0-1), set pitch range
3. Declare field in script: `[SerializeField] private SoundEffect mySound;`
4. Play in code: `mySound.Play(audioSource);`
5. **Group sound effect field with related functionality** (e.g., teleport sounds with teleport ability)

### Understanding Damage Flow

1. Projectile/Beam hits → `OnTriggerEnter2D()` or raycast detection
2. Calls `TakeDamage(damage, DamageSource)` on Entity
3. Entity deducts from shield first, then health
4. Health reaches 0 → `Die()` method → object destruction/effects
5. Player-specific: Chromatic aberration feedback, screen shake

## Important Implementation Details

### Recoil System

Ships apply recoil force opposite to weapon direction:
- **Projectile weapons**: Recoil per shot (impulse)
- **Beam weapons**: Continuous recoil per second (sustained force)
- Recoil affects movement momentum, creating skill-based movement trade-offs

### Banking & Pitching (2.5D Visual Effects)

Visual 3D effect on 2D ships:
- **Banking (roll)**: `visualModel` child object rotates based on lateral velocity
- **Pitching**: Tilts based on thrust direction
- Configurable sensitivity and smoothing in `VisualEffectsConfig`
- Creates illusion of 3D maneuvers in 2D plane

### Thruster Particle Systems

Ships can have multiple thruster particle systems:
- Emission ramps up/down smoothly based on thrust input
- Ramp time configured in `ThrusterConfig`
- Particle system itself controls emission rates (not script)

### Shield Regeneration (Player Only)

Shields don't regenerate immediately after damage:
- Configurable delay (`shieldRegenDelay`) before regeneration starts
- Regenerates at `shieldRegenRate` per second
- Configured in `ShieldRegenConfig` struct

### Ability Implementation Patterns

**Beam Weapon (Ability 1):**
- Continuous raycast damage
- Drains beam capacity while active
- Regenerates when not firing
- Slows rotation while active

**Reflect Shield / Parry (Ability 2):**
- Active duration with cooldown
- Reflects enemy projectiles back at them
- Changes reflected projectile color and damage
- Looping sound while active

**Teleport (Ability 3):**
- Pre-teleport delay with shrink animation
- Instant position change
- Grow animation at destination with overshoot
- Optional chromatic aberration flash and screen shake

**Giga Blast (Ability 4):**
- Charge-based system with 4 power tiers
- Longer charge → higher tier → more damage/speed
- Movement penalties increase with charge tier
- Each tier has unique projectile prefab and particle effects
- Tier 3 & 4 projectiles pierce enemies with damage falloff

## Testing & Iteration

- **Main scene:** `Assets/Scenes/SampleScene.unity` - Full game with player ship
- **Inspector tweaking:** Most balance adjustments happen without recompiling via Inspector parameters
- **Prefab variants:** Ship prefabs are configured per-type with different stats in the Inspector
- **Struct organization:** Collapse all structs, then expand only what you're tuning

## Notes for Future Development

- **Enemy System**: Currently focused on player ships; enemy system to be implemented following similar Entity base class pattern
- **Visual Polish**: Chromatic aberration, screen shake, particle effects are actively tuned parameters
- **Performance**: Background generation is procedural; monitor if adding more visual layers impacts performance
- **Ability Expansion**: Class1 has 4 abilities; future ship classes can have different ability sets
