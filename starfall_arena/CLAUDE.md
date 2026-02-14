# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Starfall Arena** is a 2.5D space shooter game built with Unity. Players control a ship defending against waves of enemies with various AI behaviors, featuring shield mechanics, weapon systems, and special abilities.

THIS IS A CONTROLLER FIRST GAME - ALL INPUT SHOULD BE PRIMARILY DESIGNED AROUND CONTROLLER INPUT.

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

### Ability HUD System

**Architecture: Poll-based UI driven by virtual methods on `Ability.cs`**

Each ship class has a **unique ability canvas prefab** (`ShipData.abilityHUDPrefab`). At spawn time, the canvas is instantiated and bound to the player via `Player.BindAbilityHUD(panel)`.

**Core Components:**
- `Ability.cs` - Base class exposes 3 virtual HUD methods: `GetHUDFillRatio()`, `IsResourceBased()`, `IsOnCooldown()`
- `AbilitySlotUI.cs` (`Assets/Scripts/UI/`) - Per-slot component that polls its ability every `Update()` and drives 3 UI elements
- `AbilityHUDPanel.cs` (`Assets/Scripts/UI/`) - Groups 4 `AbilitySlotUI` slots, exposes `Bind(Player)`

**Two ability display modes:**

1. **Cooldown-based (default):** Circle background swaps material on cooldown start/end. Dark fill icon animates from fillAmount=1 (fully covered) to 0 (ready) as cooldown progresses.
2. **Resource-based** (Beam, FireWall): Only dark fill icon is used. fillAmount = resource expenditure ratio (0=full, 1=depleted). No material swaps.

**Per-slot UI elements:**
- `circleBackground` (Image) - material swaps between `readyMaterial` and `cooldownMaterial`
- Normal icon (Image) - always visible, unchanged, not referenced by script
- `darkFillIcon` (Image) - filled image, vertical fill, top origin, driven by `GetHUDFillRatio()`

**Adding ability HUD to a new ship class:**
1. Create a canvas prefab with 4 `AbilitySlotUI` children (one per ability slot)
2. Wire each slot's Image references and materials in Inspector
3. Add an `AbilityHUDPanel` component to the canvas root, assign the 4 slots
4. Set `abilityHUDPrefab` on the ship's `ShipData` ScriptableObject
5. At spawn time, instantiate the prefab and call `player.BindAbilityHUD(panel)`

**Overriding HUD state for custom abilities:**
- Abilities using base class cooldown (`lastUsedAbility` + `stats.cooldown`): no override needed
- Abilities with custom cooldown tracking: override `GetHUDFillRatio()` and `IsOnCooldown()`
- Resource-based abilities: override all 3 methods (`IsResourceBased()` returns true, `IsOnCooldown()` returns false)

## Title Screen & Menu System

### Architecture Overview

The title screen uses a **canvas-based transition system** with three main components working together:

**Core Components:**
- `TitleScreenManager.cs` - Orchestrates scene intro, canvas transitions, and component lifecycle
- `TitleScreenButton.cs` - Button component with hover effects and configurable click actions
- `ShipSelectManager.cs` - Manages ship selection screen with 3D model rotation and UI navigation

### TitleScreenManager

**Responsibilities:**
- Scene intro sequence (fade from black → fade UI in)
- Canvas transitions (scale + fade animations between menus)
- Component lifecycle management (enabling/disabling managers at the right time)
- EventSystem selection control
- Ship model preloading

**Canvas Structure:**
```
Scene:
├─ MainMenuCanvas (title, buttons)
├─ ControlsCanvas (control instructions)
└─ ShipSelectCanvas (ship selection with 3D preview)
```

**Transition Flow:**
1. Clear EventSystem selection
2. Disable old canvas (interactable, raycasts, buttons)
3. Activate new canvas GameObject (but non-interactable)
4. **Preload content** (if going to ship select: load UI data, prepare ship)
5. Exit animation (old canvas scales/fades out)
6. Pause (background visible)
7. Enter animation (new canvas scales/fades in)
8. **Activate content** (ship model, if applicable)
9. Enable new canvas (interactable, raycasts, buttons)
10. Set EventSystem selection

**Critical Timing:**
- GameObjects activated early (before transition) but kept non-interactable
- Buttons disabled during transitions to prevent premature EventSystem auto-selection
- Ship models activated at the END of transition (when canvas is fully visible)

### TitleScreenButton

**Features:**
- Fade-in hover effects (flanking circles, overlay highlight)
- Configurable click actions: menu transitions OR scene loads
- Controller and mouse support (IPointerEnter, ISelectHandler)
- Canvas alpha check to prevent sounds during fade-in
- `_isInitialSelection` flag to prevent hover sound on auto-selection

**Click Config Options:**
- **Menu Transition**: `TransitionToShipSelect()`, `TransitionToControls()`, etc.
- **Scene Load**: Load a gameplay scene with delay (for sound)
- **Quit Game**: Application.Quit()
- **UnityEvent**: Custom effects (enable panels, trigger animations)

### ShipSelectManager

**Core Functionality:**
- Ship model spawning and lifecycle management
- 3D ship rotation with controller sticks
- D-pad UI navigation (ability buttons)
- Dynamic UI population from ShipData ScriptableObjects
- Seamless preloading for instant transitions

**Ship Selection Flow:**
```
Scene Load:
  → TitleScreenManager spawns all ship models (inactive)
  → Models ready before player can interact

User clicks "Play Game":
  → Transition starts
  → PreloadShipData() called (loads UI text, prepares transform)
  → Ship stays INACTIVE during transition
  → Canvas fades in
  → ActivateShipWhenVisible() called
  → Ship appears instantly (no delay, not visible early)
```

**Controller Input Architecture:**

**CRITICAL: Input Separation**
- **Sticks (Left/Right)**: ONLY rotate ship (always active)
- **D-pad**: ONLY navigate UI buttons
- **Shoulders (LB/RB)**: Switch between ships
- **B/Escape**: Back to main menu

**Implementation:**
- `HandleShipRotation()` - Reads stick input every frame, applies rotation
- `HandleDPadNavigation()` - Manually handles D-pad, prevents stick from controlling UI
- `DisableEventSystemNavigation()` - Disables Input Module's move action on enable
- `RestoreEventSystemNavigation()` - Re-enables on disable

**Ship Rotation:**
- Left Stick X → Yaw (spin left/right)
- Left Stick Y → Pitch (tilt up/down, inverted)
- Right Stick X → Roll (barrel roll)
- Quaternion slerp for smooth interpolation
- Frame-independent with Time.unscaledDeltaTime

**UI Navigation:**
- Default state: No button selected (ship rotation mode)
- D-pad Down (no selection) → Select first ability button
- D-pad Left/Right/Up/Down (has selection) → Navigate between buttons using explicit navigation
- D-pad Up (top row) → Deselect, return to ship rotation mode

### ShipData ScriptableObject

Stores all ship information for display in ship select:

**Structure:**
```csharp
- shipName (string)
- shipModelPrefab (GameObject)
- stats (ShipStats struct):
  - damage (0-50)
  - hull (0-500)
  - shield (0-500)
  - speed (0-100)
- ability1-4 (AbilityData struct):
  - abilityName
  - abilityDescription
  - abilityIcon (Sprite)
```

**Usage:**
1. Create: Right-click → Create → Starfall Arena → Ship Data
2. Assign to ShipSelectManager's `availableShips` array
3. Manager dynamically populates UI from this data

### Component Lifecycle & Timing

**CRITICAL: Why Timing Matters**
- Components must be enabled/disabled at specific times to prevent:
  - EventSystem auto-selection during transitions
  - Ship models appearing during fade animations
  - OnEnable/OnDisable firing at wrong times
  - HideAllShipModels running after ShowShipModel

**Ship Spawning Timeline:**
```
TitleScreenManager.Start():
  → Calls ShipSelectManager.SpawnShipsAtSceneLoad()
  → All ships instantiated (inactive)
  → Happens during title screen fade-in (invisible to player)

Transition to Ship Select:
  → PreloadShipData() called early
    → Loads UI data (text, stats, abilities)
    → Prepares ship transform (position, rotation, scale)
    → Ship STAYS INACTIVE
  → Transition animation plays
  → Canvas becomes visible
  → ActivateShipWhenVisible() called
    → Ship.SetActive(true)
    → Appears instantly with no pop-in
```

**Component Enable/Disable:**
- ShipSelectManager component starts DISABLED
- Enabled during preload, stays enabled throughout
- OnEnable runs once during preload
- OnDisable only runs when leaving ship select
- This prevents enable/disable cycling that would hide ships

### Best Practices for Menu Systems

**Controller-First Design:**
- Always test with controller first
- Separate stick and D-pad functionality clearly
- Disable EventSystem navigation when manually handling input
- Use `wasPressedThisFrame` for discrete button presses

**Transition Smoothness:**
- Preload heavy content during exit animation
- Activate visuals only when fully visible
- Use scale + fade for professional feel
- Add pause between exit/enter for clarity

**Button Selection Management:**
- Disable buttons during transitions
- Clear selection before disabling canvases
- Set selection AFTER canvas is fully visible
- Use `_isInitialSelection` flag to prevent sounds on auto-select

**Component Lifecycle:**
- Spawn heavy objects at scene load (during black screen)
- Enable components early if needed for preload
- Keep components enabled to avoid enable/disable cycling
- Activate GameObjects at the exact right moment

## Testing & Iteration

- **Main scene:** `Assets/Scenes/SampleScene.unity` - Full game with player ship
- **Title screen scene:** `Assets/Scenes/MainMenu.unity` - Title screen with ship selection
- **Inspector tweaking:** Most balance adjustments happen without recompiling via Inspector parameters
- **Prefab variants:** Ship prefabs are configured per-type with different stats in the Inspector
- **Struct organization:** Collapse all structs, then expand only what you're tuning

## Notes for Future Development

- **Enemy System**: Currently focused on player ships; enemy system to be implemented following similar Entity base class pattern
- **Visual Polish**: Chromatic aberration, screen shake, particle effects are actively tuned parameters
- **Performance**: Background generation is procedural; monitor if adding more visual layers impacts performance
- **Ability Expansion**: Class1 has 4 abilities; future ship classes can have different ability sets
- **HUD Dynamic Binding**: The `HUDConfig` struct currently lives on `Player.cs` (abstract), but Player is never directly instantiated — concrete ships like Class1 are spawned dynamically after ship select. The HUD bars/text exist in the gameplay scene canvas and can't be pre-wired on ship prefabs. **TODO:** Create a `PlayerHUD` MonoBehaviour that sits on each HUD panel in the scene, holds the `SegmentedBar` and `TextMeshProUGUI` references, and has a `playerTag` field (`"Player1"` / `"Player2"`). Move the HUD wiring out of Player and have the spawned player auto-discover its matching `PlayerHUD` in `Start()` via tag match. This supports multiple players (P1 vs P2) and dynamic spawning without needing Inspector references on prefabs. Related files: `Player.cs` (HUDConfig struct + OnHealthChanged/OnShieldChanged), `SegmentedBar.cs` (bar segment logic).

## Augment Architecture (Current)

### Data vs Runtime Split

Augments now follow a strict split:

- `Augment` ScriptableObjects (`Assets/Scripts/Augments/Augment.cs`) are definition-only authoring assets.
- Runtime behavior lives in `IAugmentRuntime` classes under `Assets/Scripts/Augments/Runtime/`.
- Player-owned runtime instances are managed only by `AugmentController` (`Assets/Scripts/entities/AugmentController.cs`).

Do not store per-player mutable state on ScriptableObject augment assets.

### Core Runtime Contracts

- `IAugmentRuntime`: per-augment runtime hooks (`ExecuteEffects`, damage hooks, contact hooks, persistence capture).
- `AugmentLoadoutEntry`: snapshot entry used by SceneManager between rounds (`definition`, `roundAcquired`, `persistentState`).

### Scene Flow Integration

- `AugmentSelectManager` still passes `Augment` definitions (unchanged public contract).
- `SceneManager` stores per-player `List<AugmentLoadoutEntry>` and re-imports them on respawn.
- `Entity` delegates augment hook dispatch to `AugmentController`.

### Rules

- Never write `augmentName`, `description`, or `augmentID` at runtime.
- New augment logic should be added as a new runtime class implementing `IAugmentRuntime`.
- New augment definitions should only expose tunable parameters and return runtime via `CreateRuntime()`.
- Avoid reflection in augment systems; use explicit `Entity` APIs (`SetShieldValue`, `SetMaxHealthAndClampCurrent`, etc.).
