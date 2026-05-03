# 3D_invasion_balancing.md

This document owns 3D Invasion tuning guidance.

Read this whenever changing wave numbers, enemy stats, reward payloads, player balance profiles, enemy balance profiles, ship availability, boss pressure, or any mechanic that changes how strong players or enemies are during a run.

## Current MVP Scope

The first balancing target is a cooperative 3D Invasion run built around two playable ships:

- `VX-Atlas` / Class 1
- `Mako` / Class 4

Enemy prefab numbers are not treated as final yet. Current enemy prefabs mostly define behavior identity, wiring, tells, movement style, and attack shape. Balance should be driven through `EnemyBalanceProfile3D` assets and wave composition rather than assuming current prefab values are authoritative.

## Player Baselines

These are the current v1 tuning anchors for the two ships. Use them as the starting point when estimating enemy time-to-kill, incoming damage budgets, wave density, and boss durability.

### VX-Atlas / Class 1

- Durability: `100` hull, `100` shield
- Damage immunity: `0.2s` invulnerability frames on a `1s` cooldown
- Flight: `40` acceleration, `45` max speed
- Primary projectile: `0.25s` cooldown with energy limits, `20` damage per shot, `300` projectile speed
- Primary projectile theoretical ceiling: `80 DPS` before energy limits and misses
- Beam: `55 DPS`, `800` max distance, energy system
- Giga Blast: up to `200` max damage, pierce, `1800` range, `8s` cooldown
- Reflector Shield: reflects incoming projectiles back at the shooter for the same damage, `15s` cooldown
- Teleport: `50` unit blink, `5s` cooldown

Design read:

- VX has higher baseline durability and more defensive agency.
- VX has a dangerous burst spike through Giga Blast, so elite enemies and bosses should not be tuned only around sustained primary DPS.
- Reflector makes enemy projectile burst windows risky if they are too concentrated and too easy to reflect. Projectile enemies should be balanced with readable cadence, mixed angles, or enough spacing that reflect feels strong without deleting whole waves by accident.
- Teleport makes positional pressure less reliable against VX. Enemies that depend on slow tracking, projectile travel time, or short pursuit windows need enough persistence to remain relevant.

### Mako / Class 4

- Durability: `75` hull, `100` shield
- Damage immunity: `0.2s` invulnerability frames on a `1s` cooldown
- Flight: `55` acceleration, `50` max speed
- Burst weapon / slot 0: `1s` cooldown, `350` bullet speed, `10` damage per projectile, `1.5s` lifetime
- Burst weapon theoretical baseline: `60 DPS`
- Beam: `50 DPS` total, `25 DPS` per beam, `100` energy, `750` range, `25` drain rate
- Missile: `40` damage, strong area value, `5s` cooldown
- Dodge: `50s` cooldown
- Empower: `32s` cooldown, `14s` duration
- Empower effect: raises normal pressure from roughly `50-60 DPS` into roughly `75-80+ DPS`, with extra value when missiles hit clustered enemies

Design read:

- Mako is faster and more fragile than VX.
- Mako's baseline damage is steadier and less spiky than VX, but Empower creates long windows where its throughput and area pressure matter.
- Mako's missile should make tightly packed basic enemies feel rewarding to punish. Do not make every early wave a loose scatter unless the goal is specifically to reduce missile value.
- Dodge is too long-cooldown to be treated as a normal defensive rhythm. Mako survival should come mostly from flight, shields, player aim, and wave readability.

## Reward Scaling Model

The current 3D Invasion reward layer is run-only and applies additive modifier totals back onto each player's captured base snapshot.

Important implementation facts:

- Rewards are offered after cleared waves, except the final configured wave.
- Reward tiers cycle `Common -> Epic -> High`, then repeat.
- Normal stat rewards are repeatable and should keep at least one reliable scaling pick in each generated offer.
- CRAIZAN CONTRACTS are repeatable trade-off cards that can appear in Common/Epic/High offers while using tier 4 visuals per card.
- Percent rewards add together first, then apply to the captured base stat. They do not multiply reward-by-reward.
- `Future Investment` increases future normal stat boost payloads only, so early selection can raise later normal scaling.

Current repeatable normal stat buckets include:

- Damage Calibration: all weapon damage, roughly `+6% / +10% / +15%`
- Energy Cycle Tuning: projectile cooldown reduction, projectile energy cost reduction, beam capacity, beam regen, and ability cooldown, roughly `+5-12%` cadence and `+8-20%` resource economy depending on tier
- Reinforced Frame: max hull and max shield, roughly `+10% / +18% / +28%`
- projectile handling: projectile speed/lifetime/hit forgiveness style scaling
- ship handling: acceleration, top speed, and turn response, roughly `+8-22%` acceleration, `+6-15%` top speed, and `+5-14%` turn response depending on tier

Current one-time or perk-style reward hooks include:

- aim assist expansion
- primary projectile pierce
- full shield restore on shield break once per wave
- shield overcharge
- execution lottery against non-boss enemies
- future stat boost scaling
- shield leech from applied damage
- hull repair on non-boss kills
- post-dodge speed/acceleration boost
- target momentum damage against repeated hits on one target
- field repair, shield refill, and extra life bonuses

Current CRAIZAN CONTRACT examples include:

- Glass Cannon: large damage gain, durability loss
- Unstable Reactor: major cooldown/resource/ability gains, durability and shield recovery penalties
- Redline Engine: major movement gain, survival/control trade-off
- Fortress Bargain: major durability/mitigation gain, movement or offense trade-off

Balancing implication:

- By rewards 1-2, a typical player is only modestly stronger.
- By rewards 3-4, a focused player can be meaningfully ahead in one axis.
- By rewards 5-6, a focused or contract-heavy player can be dramatically stronger, but mixed builds should still be viable.
- Enemy scaling should assume approximate effective player strength of `1.0x` on wave 1, `1.1-1.3x` after two rewards, `1.35-1.7x` after four rewards, and `1.8-2.4x` after six rewards for focused builds.
- Boss tuning should assume six rewards, but not a perfect build. Optional hazards, spawns, or phase pressure can challenge high-roll builds without making mixed builds impossible.

## Enemy Baseline Anchor

The most basic enemies may start at player-like durability:

- `100` hull
- `100` shield
- intentionally poor DPS

This means one basic enemy has roughly one full player durability bar, but not one full player's threat. That is a good MVP anchor as long as early waves control count and firing uptime.

For early tuning, think in terms of enemy effective pressure:

- Basic enemies should die in roughly `3-5s` of clean focused fire from one baseline player, depending on shield rules, misses, and energy limits.
- Two players focusing a basic enemy should remove it quickly enough that target prioritization feels rewarding.
- Basic enemy DPS should start low enough that two or three simultaneous enemies create pressure through attention splitting, not raw unavoidable damage.
- Enemy projectile speed, range, and aim tolerance are as important as damage. A low-DPS enemy with unavoidable shots can feel harsher than a higher-DPS enemy with readable, dodgeable fire.

## Wave Scaling Guidance

For the MVP, scale difficulty mostly with composition and overlap before increasing raw enemy stats.

Preferred wave levers, in order:

1. Enemy count
2. Sub-wave overlap and spawn timing
3. Enemy role mix
4. Spawn angles and vertical spread
5. Enemy accuracy, cooldowns, and movement persistence
6. Enemy health/shield multipliers
7. Enemy damage multipliers

Early waves should teach:

- basic pursuit and basic shooting
- missile and Giga Blast value against clustered targets
- target-awareness HUD reading
- the importance of clearing enemies before overlap grows

Mid waves should test:

- mixed ranges
- enemies arriving from more than one direction
- one durable or disruptive unit protected by weaker units
- player reward specialization starting to matter

Late waves should test:

- sustained attention management
- elite enemies with specific counterplay
- pressure that forces both players to divide roles or communicate
- stronger reward builds without requiring perfect reward luck

Avoid early MVP traps:

- Do not make wave difficulty depend on current enemy prefab numbers being final.
- Do not scale only enemy health. That makes strong player builds feel flat and weak builds feel tedious.
- Do not scale only enemy damage. That makes mistakes too punishing, especially for Mako's lower hull.
- Do not pack every enemy tightly once Mako missiles and VX Giga Blast are online unless the wave is intentionally a power moment.
- Do not make every late wave a spread-out sniper problem. That over-favors long-range beams and undercuts area weapons.

## Practical Tuning Targets

Use these rough targets until playtest data replaces them.

### Wave 1

- Goal: baseline validation and onboarding.
- Player strength: `1.0x`.
- Use mostly basic enemies.
- Keep active enemy count low.
- Basic enemy DPS should be bad enough that the player can make positioning mistakes and recover.
- A basic `100/100` enemy is acceptable if only a few are active and their firing uptime is low.

### Waves 2-3

- Goal: introduce mild composition pressure.
- Player strength: roughly `1.1-1.4x`.
- Add a second behavior type or timed sub-wave overlap.
- Slightly raise enemy count before touching durability.
- Start using spawn angles that prevent both players from tunneling one lane for the whole wave.

### Waves 4-5

- Goal: make reward picks matter.
- Player strength: roughly `1.35-1.9x`.
- Mix basics with one durable, disruptive, or long-range enemy.
- Increase overlap so wave DPS is real, but keep individual attacks readable.
- Consider modest elite durability increases before large damage increases.

### Final/Boss Wave

- Goal: test a six-reward build without requiring perfect picks.
- Player strength: roughly `1.8-2.4x` for focused builds, lower for mixed builds.
- Boss durability should be tuned against two-player combined output, not one player's baseline DPS.
- Boss pressure should come from patterns, spacing, and add timing more than raw unavoidable DPS.
- Boss add spawns should be budgeted as part of total damage uptime and performance cost.

## Damage Budget Heuristics

For non-boss enemies:

- Basic shooter damage should begin as chip pressure.
- A single basic enemy should not break a full shield quickly unless the player ignores it for a long time.
- Three or more active basics can threaten shields through combined fire, but should still be recoverable with movement and target priority.
- High-damage enemies need readable tells, limited uptime, or clear range constraints.
- Area attacks should be lower single-target DPS than direct attacks unless their cooldown is long and the visual/readability cost is high.

For player-facing burst:

- VX can delete or heavily chunk a basic enemy with Giga Blast. That is acceptable.
- Mako can punish clusters with missile AOE. That is acceptable.
- If a wave falls apart from one Giga Blast or missile, adjust formation spacing, stagger timing, or mix in one sturdier target before globally nerfing player weapons.

## Documentation Rules

When changing any tuning-sensitive system, update this document if the change affects:

- player baseline stats
- reward payload strength or offer rules
- enemy health, shield, damage, speed, accuracy, detection, cooldowns, or resource systems
- wave count, sub-wave overlap, spawn timing, or enemy mix
- boss durability, pattern cadence, add spawns, or phase pressure
- ship availability or assumptions about which ships the MVP balances around

If a tuning problem is discovered, document the cause here or in `3D_BUGS.md` depending on whether it is a balancing pitfall or an implementation bug.
