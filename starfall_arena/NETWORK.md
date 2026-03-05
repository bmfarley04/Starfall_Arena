# Networking Philosophy — Space Dueling Game

## Overview

This document summarizes the networking architecture and design philosophy for our 2.5D space dueling game. We use **Netcode for GameObjects** as our base layer, with custom networking code for player movement and projectiles.

---

## Tick Rate

The server runs at **60hz** (one tick every ~16ms). All networked state is stamped with a tick number, which serves as the common time reference across all clients and the server.

## Authority Model

The **server is authoritative**. Clients simulate ahead locally but are always subject to server correction. No client-submitted result is trusted without server validation.

---

## Player Movement

- **Local player** uses full **client-side prediction**: inputs are applied immediately on the client without waiting for server confirmation. The client stores a rolling buffer of recent inputs and replays them on top of server corrections when reconciling.
- **Remote players** are displayed using **interpolation only** — no extrapolation or dead reckoning. We buffer 2 ticks (~33ms) of incoming state and interpolate smoothly between confirmed positions. This trades a small, fixed visual delay for stability and correctness.
- Each player maintains a **position history buffer** of the last 120 ticks (~2 seconds) on the server for use in lag compensation.

---

## Projectiles

Because all projectile motion is fully deterministic, projectiles require **no ongoing network synchronization** after creation.

- When a player fires, the client spawns a **local ghost projectile** immediately for visual responsiveness.
- A `FireCommand` (containing tick, origin, direction, and velocity) is sent to the server.
- The server validates and spawns the **authoritative projectile**, broadcasting initial conditions to all clients.
- All clients simulate the projectile independently using identical deterministic physics. No NetworkTransform is used.
- Destruction (hit or expiry) is communicated via RPC.

---

## Hit Detection & Lag Compensation

We use **server-side lag compensation** for projectile hit detection.

When a projectile potentially hits a player, the server:
1. Rewinds that player's position to account for the **shooter's network latency** — so shooters are registering hits against what they actually saw on their screen.
2. Checks collision against that rewound historical position.
3. Does **not** additionally rewind for projectile travel time — the projectile is a real object in the world, and if the target moved out of its path, the shot misses.

### Design Philosophy: Favor the Dodger

Since dodging is a core skill in this game, our lag compensation is intentionally **conservative**. Near-misses are awarded to the defender. We do not rewind aggressively beyond accounting for the shooter's own latency. This ensures that skillful evasion is always meaningful, regardless of network conditions.

---

## Summary of Key Choices

| Decision | Choice | Reason |
|---|---|---|
| Tick rate | 60hz | Sufficient for our game speed; manageable bandwidth |
| Remote player display | Interpolation (no prediction) | Stable, no correction snaps; 33ms delay is imperceptible |
| Interpolation buffer | 2 ticks (~33ms) | Minimum smooth buffer; revisit if players report stutter |
| Projectile sync | Initial conditions only | Deterministic motion eliminates need for ongoing sync |
| Lag comp philosophy | Favor dodger | Dodging is a skill; conservative rewind preserves that |