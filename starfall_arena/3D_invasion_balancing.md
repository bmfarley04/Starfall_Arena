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

## V1 Wave Plan

This is the agreed documentation target for the first seven-wave 3D Invasion setup. It is not yet a prefab, scene, or code implementation plan.

### Enemy Roster Roles

- Basic enemy: standard shooter baseline. Starts around `100` hull / `100` shield with low pressure, currently around `10` damage every `2s` when it hits. It may need a later buff, but should remain the readable "normal" enemy.
- Suicide enemy: fast committed chase enemy that explodes for high damage if it connects. Use it as a movement and awareness check, not as constant unavoidable punishment.
- Artillery enemy: long-range beam enemy that backs up when pressured. It teaches ranged line pressure and rewards closing distance or using cover.
- Simple tank enemy: durable low-to-moderate threat that fires poor missiles and a slow heavy sniping gun. It should feel tanky before it feels lethal.
- Flamethrower enemy: close-range punishment enemy. Its threat should be very high if it reaches the player, but it needs readable approach windows and counterplay.
- Gnat enemy: fragile repositioning burst enemy. It should stay far away, burst, then move, functioning as an aim check.
- Splitter enemy: hybrid projectile/beam enemy that splits into two smaller versions on death, with each child keeping one weapon role.
- Scout enemy: cannon-fodder swarm unit. Scouts should usually die to one hit, fly in satisfying groups of about five, and matter mainly because they alert other enemies at extended range.
- Triumvirate enemy: three-ship coordinated enemy. It should always spawn as a full trio, charge a large slowing beam blast, and reward players for interrupting the formation before the big shot.
- Duelist enemy: high-skill enemy that dodges shots, strafes, and mixes missiles, projectiles, and beams. Use it carefully because its complexity makes it a large difficulty spike.
- Fortress / boss 1: large sniper-style boss enemy with a big cannon, missiles, and staggered gunfire. Later waves may use basic fortress variants as heavy enemies rather than only as bosses.
- Final boss / boss 2: wave 7 boss. It should use multiple beams, slowing beams, staggered shots, shot patterns, add spawning, phase transitions, and other major boss pressure.

### Variant Rules

- Enemy variants should be explicit prefabs, not hidden stat swaps inside wave entries.
- Every variant must be differentiated by size so players can read enemy tier at gameplay distance.
- Default size ladder:
  - Weak: `0.75x`
  - Normal: `1.0x`
  - Elite: `1.25x`
  - Empowered: `1.45x`
- Variant size should scale the visual model and gameplay colliders together. Larger enemies should honestly be physically larger and easier to hit.
- Size implies tier identity: weak variants are smaller and lower pressure, elite variants are larger and tougher, and empowered variants are the largest and should carry stronger offense, spawners, or boss-like pressure.
- Damage and cadence still remain role-specific. Do not blindly multiply every stat just because the variant is larger.
- Variants should eventually have assigned balance profiles and network prefab registration before being added to networked waves.

### Detection And Spawning Model

- Use local aggro as the default target model.
- Normal enemy detection should usually land around `450-600` units.
- Long-range enemies should usually land around `600-800` units.
- Bosses, scout alert behavior, and special long-range pressure may exceed those ranges intentionally.
- Avoid `5000`-range full-map aggro on normal enemies unless a specific test or special role needs it.
- Plan around roughly eight arena spawn zones with vertical variation: north, northeast, east, southeast, south, southwest, west, and northwest. Each zone should support low, mid, and high spawn anchors where practical.
- Enemies should spawn progressively throughout waves from distributed fronts, not all at once and not only around the arena center or a single Y plane.
- Players should not be forced to encounter every active enemy at once. The wave layout should let them disengage from some groups, use obstacles, and choose which pressure to solve first.
- Bosses and mini-bosses should spawn at authored times inside the wave. Do not wait until every previous enemy is dead before introducing them.
- Prefer normal timed sub-waves for boss and mini-boss arrivals unless the wave manager's boss-specific path is deliberately redesigned later.

### Pacing And Active Enemy Budget

- Target a `2-8 minute` ramp across the seven waves.
- Wave 6 and wave 7 should usually target `6-8 minutes`.
- Dense waves should generally stay around `20-30` active enemies.
- Scouts count toward spectacle and target density, but they should remain low-threat fodder.
- Wave length should increase through paced sub-waves and escalating pressure, not by making the final cleanup tedious.

### Wave Progression

Wave 1:

- Target: about `2 minutes`.
- Tone: soft introduction.
- Roster: basic enemies, suicide drones, and a small artillery mini-boss beat.
- Start with a lot of basic enemies, add some suicide drones, then introduce two artillery enemies as the first simple boss-style moment.
- Keep active pressure low enough that players learn the rules without likely deaths.

Wave 2:

- Target: about `3 minutes`.
- Roster: basic, suicide, artillery, scouts, gnats, and tank boss.
- The first three enemies should now feel standard.
- Introduce scout swarms as satisfying fodder with alert utility.
- Introduce gnats as aim-check enemies.
- Use the tank enemy as the boss. Its main lesson is durability and target focus, not overwhelming lethality.

Wave 3:

- Target: about `4 minutes`.
- Roster: previous enemies, two normal tanks, flamethrowers, and a duelist skill-check group.
- Introduce the flamethrower as a close-range danger spike.
- Use several duelists as the boss beat, but tune this as `1` elite duelist plus `2` weak duelists rather than three full-strength duelists.
- This should be a major skill check, but not an unfair spike after only two rewards.
- Multiple-boss health presentation is a future implementation need; the preferred direction is one grouped encounter bar for the duelist group.

Wave 4:

- Target: about `5 minutes`.
- Roster: all previous enemies, duelists, triumvirates, and fortress boss.
- The player has received a high-tier reward after wave 3, so density can rise.
- Introduce triumvirates as full trio prefabs only. Never spawn a solo triumvirate member as a normal wave entry.
- Use scout swarms and lower-tier enemies to make the arena feel busier without making every active target high-complexity.
- Use the fortress enemy as this wave's boss.

Wave 5:

- Target: about `6 minutes`.
- Roster: previous non-fortress enemies, splitters, and empowered tank mini-bosses.
- Introduce the splitter enemy and give players time to learn the split-on-death behavior before the boss pressure lands.
- Boss beat should be two empowered tank variants.
- Empowered tanks should have spawners, but those spawners should begin with weak basics and scouts so the adds create density without flooding the fight with elite mechanics.

Wave 6:

- Target: `6-8 minutes`.
- Roster: every enemy type.
- This is the final check before the final boss and should not be short.
- Start slow as a reminder of the run progression.
- Around one-third into the wave, begin spawning normal tanks.
- Later, add higher-tier threats such as duelists and splitters.
- Around two-thirds into the wave, spawn one or two basic fortress variants spaced across the map.
- End with an empowered fortress variant flanked by two empowered tanks.
- The wave should escalate steadily, but still respect the active enemy budget and allow positional play.

Wave 7:

- Target: `6-8 minutes`.
- Start with a short `60-90s` mixed-enemy prelude.
- Spawn the final boss after the prelude rather than waiting for a full wave cleanup.
- Expand the final boss spawner beyond suicide drones.
- Early boss spawns should favor weak basics and scouts.
- Later boss spawns can add weak suicide drones, gnats, and occasional stronger adds, but the boss patterns should remain the main threat.

### Known Setup Follow-Ups

- Regenerate or reassign enemy balance profiles for all enemy prefabs before serious stat tuning.
- Add missing `EnemyBalanceProfileApplier3D` setup on artillery, duelist, fortress, and boss2 prefabs.
- Assign a real profile to `spawnedSuicideEnemy`.
- Retune prefab sensors away from accidental `5000` detection unless the enemy role explicitly needs map-wide or boss-scale aggro.
- Create explicit variant prefabs for weak, elite, and empowered enemies before authoring the final wave set.
- Register all networked variant prefabs with NGO before adding them to networked Invasion waves.

## V1 Enemy Prefab Tuning Targets

These numbers are the target balance profile values for the V1 wave plan. They are not yet implemented in assets. The next prefab/profile pass should create or regenerate matching `EnemyBalanceProfile3D` assets, then assign them through `EnemyBalanceProfileApplier3D`.

Player-scaling assumptions:

- Baseline single-player DPS is roughly `55-80`.
- Baseline combined co-op DPS is roughly `110-140`.
- Focused late-run co-op DPS after rewards is roughly `200-300+`.
- Common enemies should die quickly under focused fire. Waves should become hard through density, overlap, positioning, and role mix rather than by turning common enemies into sponges.

Variant implementation assumptions:

- Use explicit prefab variants with assigned balance profiles.
- Scale the visual model and gameplay colliders together.
- Register every networked variant prefab with NGO.
- Prioritize durability and role presence before raw outgoing damage when scaling variants upward.

### Variant Size And Stat Intent

| Tier | Size | Stat Intent |
|---|---:|---|
| Weak | `0.75x` | Low durability, lower pressure, fodder/add use |
| Normal | `1.0x` | Main authored enemy |
| Elite | `1.25x` | More durability, slightly better weapons |
| Empowered | `1.45x` | Mini-boss/add-spawner tier, durability-first |

### Basic Shooter Targets

| Variant | H/S | Speed | Detect | Projectile |
|---|---:|---:|---:|---|
| Weak Basic | `60/60` | `30` | `450` | `8 dmg`, `2.2s cd`, `110 speed`, `4s life` |
| Normal Basic | `100/100` | `35` | `500` | `10 dmg`, `1.8s cd`, `135 speed`, `4.5s life` |
| Elite Basic | `150/170` | `38` | `550` | `12 dmg`, `1.6s cd`, `155 speed`, `5s life` |
| Empowered Basic | `210/240` | `42` | `600` | `14 dmg`, `1.45s cd`, `175 speed`, `5s life` |

Use normal basics from wave 1 onward. Use weak basics as spawner adds. Use elite and empowered basics only in late dense waves.

### Suicide Drone Targets

| Variant | H/S | Speed | Detect | Detonation |
|---|---:|---:|---:|---|
| Weak Spawned Suicide | `20/10` | `45` | `450` | `45 dmg`, `3.5 radius` |
| Normal Suicide | `45/35` | `58` | `550` | `70 dmg`, `4 radius` |
| Elite Suicide | `70/50` | `68` | `600` | `85 dmg`, `4.5 radius` |

Use weak spawned suicide for boss/add spawners. Normal suicide is the main wave unit. Elite suicide should be rare and late.

### Artillery Beam Targets

| Variant | H/S | Speed | Detect | Beam |
|---|---:|---:|---:|---|
| Normal Artillery | `70/120` | `32` | `700` | `16 DPS`, `450 range`, `280 cap`, `18 drain`, `18 regen` |
| Elite Artillery | `100/180` | `34` | `800` | `22 DPS`, `520 range`, `320 cap`, `22 drain`, `20 regen` |

Wave 1 artillery mini-boss should use two normal artillery enemies with a grouped encounter bar, not boosted stats.

### Tank Targets

| Variant | H/S | Speed | Detect | Cannon | Missile |
|---|---:|---:|---:|---|---|
| Normal Tank | `450/450` | `18` | `650` | `45 dmg`, `9s cd`, `175 speed` | `25 dmg`, `10s cd`, `125 speed` |
| Empowered Tank | `800/850` | `16` | `700` | `55 dmg`, `8s cd`, `190 speed` | `35 dmg`, `8s cd`, `140 speed` |

Normal tank is the wave 2 boss and later heavy. Empowered tank is the wave 5 and wave 6 mini-boss variant and should spawn only weak basics/scouts in the first implementation.

### Flamethrower Targets

| Variant | H/S | Speed | Detect | Flame |
|---|---:|---:|---:|---|
| Normal Flame | `110/90` | `42` | `550` | `28 DPS`, `30 range`, `1.5s burst`, `3.5s cd` |
| Elite Flame | `160/130` | `46` | `600` | `36 DPS`, `32 range`, `1.75s burst`, `3.25s cd` |

Keep the flamethrower lethal only after it closes. Do not raise detection too high; counterplay is seeing it approach and denying the close-range pocket.

### Gnat Targets

| Variant | H/S | Speed | Detect | Burst |
|---|---:|---:|---:|---|
| Normal Gnat | `12/8` | `95` | `600` | `6 dmg`, `3 shots`, `0.18s interval`, `220 bullet speed` |
| Elite Gnat | `20/15` | `105` | `650` | `7 dmg`, `4 shots`, `0.16s interval`, `245 bullet speed` |

Normal gnats should remain one-shot or near-one-shot for most player weapons. Elite gnats may survive light splash but should still die quickly.

### Splitter Targets

| Variant | H/S | Speed | Detect | Projectile | Beam | Children |
|---|---:|---:|---:|---|---|---|
| Normal Splitter | `140/120` | `35` | `650` | `7 dmg`, `1.8s cd`, `190 speed` | `16 DPS`, `420 range` | `35/25` each |
| Elite Splitter | `220/170` | `38` | `700` | `8 dmg`, `1.55s cd`, `215 speed` | `22 DPS`, `480 range` | `50/40` each |

Use normal splitters in wave 5. Elite splitters are wave 6 only unless testing shows normal splitters are too soft.

### Scout Swarm Targets

| Variant | H/S | Speed | Detect | Alert |
|---|---:|---:|---:|---|
| Scout | `1/0` | `70` | `550` | `850 radius`, `3s warmup`, `5s duration` |
| Elite Scout | `5/0` | `80` | `600` | `950 radius`, `2.5s warmup`, `6s duration` |

Scouts are true one-shot fodder. Their danger is alerting nearby enemies, not direct damage. Use groups of about five.

### Triumvirate Targets

| Variant | H/S Per Ship | Speed | Detect | Final Beam |
|---|---:|---:|---:|---|
| Normal Triumvirate | `60/80` | `30` | `700` | `15/30/50 DPS`, `3s duration`, `6s cd`, `500 range` |
| Elite Triumvirate | `85/120` | `32` | `750` | `20/40/65 DPS`, `3s duration`, `5.5s cd`, `550 range` |

Always spawn as a trio prefab. Normal appears in wave 4. Elite is wave 6 only.

### Duelist Targets

| Variant | H/S | Speed | Detect | Projectile | Missile | Beam |
|---|---:|---:|---:|---|---|---|
| Weak Duelist | `60/75` | `42` | `600` | `6 dmg`, `1.25s cd`, `180 speed` | `8 dmg`, `4s cd`, `130 speed` | `12 DPS`, `400 range` |
| Normal Duelist | `100/125` | `48` | `650` | `8 dmg`, `1.1s cd`, `210 speed` | `10 dmg`, `3.5s cd`, `150 speed` | `18 DPS`, `480 range` |
| Elite Duelist | `145/180` | `52` | `700` | `9 dmg`, `1.0s cd`, `230 speed` | `14 dmg`, `3.25s cd`, `165 speed` | `24 DPS`, `520 range` |

Wave 3 boss group should use `1` elite duelist plus `2` weak duelists. Do not use three normal duelists there.

### Fortress Targets

Boss detail is deferred, but use these temporary baselines for wave planning:

| Variant | H/S | Speed | Detect | Headline Weapons |
|---|---:|---:|---:|---|
| Normal Fortress | `450/800` | `14` | `850` | Cannon `90 dmg / 14s`, missiles `20 dmg / 8s`, turret `8 dmg / 0.3s` |
| Empowered Fortress | `750/1200` | `12` | `900` | Cannon `115 dmg / 13s`, missiles `28 dmg / 7s`, turret `10 dmg / 0.25s` |

Use normal fortress as the wave 4 boss and wave 6 heavy. Use empowered fortress only for the wave 6 finale.

### Later Implementation Checklist

- Create or regenerate balance profiles for every listed variant.
- Assign missing profile appliers on artillery, duelist, fortress, and boss2.
- Assign a real profile to `spawnedSuicideEnemy`.
- Retune accidental `5000` detection values to the plan above.
- Create explicit prefab variants with size/collider scaling.
- Keep boss2/final boss detailed pattern tuning for a separate boss-specific pass.

### Tuning Verification Targets

- Weak and common enemies should die in `1-5s` to one baseline player.
- Medium enemies should die in `5-12s` to one baseline player.
- Heavy enemies should require focused co-op pressure.
- One basic enemy should be chip pressure.
- Three basics can pressure shields but should not instantly kill.
- Flamethrowers and suicide drones should be dangerous only if allowed to connect.
- Normal enemies should not full-map aggro in the `1000x1000x1000` arena.
- Scouts should expand local danger without alerting the whole map.
- Wave 1 should remain soft.
- Wave 3 duelist group should be a skill check, not a wall.
- Wave 6 density should stay under the `20-30` active enemy target.

## Documentation Rules

When changing any tuning-sensitive system, update this document if the change affects:

- player baseline stats
- reward payload strength or offer rules
- enemy health, shield, damage, speed, accuracy, detection, cooldowns, or resource systems
- wave count, sub-wave overlap, spawn timing, or enemy mix
- boss durability, pattern cadence, add spawns, or phase pressure
- ship availability or assumptions about which ships the MVP balances around

If a tuning problem is discovered, document the cause here or in `3D_BUGS.md` depending on whether it is a balancing pitfall or an implementation bug.
