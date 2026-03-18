# Local Multiplayer Legacy

This document summarizes the older local multiplayer implementation that powered split-screen duels before the active network-first migration.

Status:

- deprecated for the active product direction
- preserved as reference in case local multiplayer is revived later

## Old Flow Summary

The original local-versus flow assumed two players were physically on the same machine:

1. title screen transitioned into a sequential two-seat ship select flow
2. Player 1 selected a ship
3. Player 2 selected a ship on the same screen
4. `GameDataManager.selectedShipClasses` carried the two chosen `ShipData` assets into gameplay
5. `GameSceneManager` instantiated both players locally in the duel scene
6. `SplitScreenManager` activated one camera per local player during gameplay

## Core Architecture

### GameDataManager handoff

The old `GameDataManager` role was very small:

- store two `ShipData` references in order
- let ship select and gameplay scenes share that temporary state

That worked because both players existed in one process and there was no per-client authority problem.

### GameSceneManager local spawning

The old duel loop assumed the scene manager owned both players directly:

- instantiate Player 1 and Player 2 locally from `ShipData.shipPrefab`
- tag them as `Player1` / `Player2`
- bind each player to its own HUD and ability HUD
- lock/unlock both players directly during round transitions
- destroy and respawn both players each round

This design was simple for couch play, but it does not map cleanly to network authority because one machine should not own both live gameplay actors.

### SplitScreenManager camera model

Local multiplayer used:

- one gameplay camera for Player 1
- one gameplay camera for Player 2
- camera rect splitting during live combat
- a whole-screen camera plus optional UI overlay camera for versus/augment/end screens

Ability HUD canvases were also assigned to different split-screen cameras, which is why legacy HUD prefabs often assume per-player camera routing.

### PlayerInputManager / controller assumptions

The legacy scene setup also assumed local device ownership:

- both players joined from the same machine
- controller assignment could be inferred from local `PlayerInput` devices
- augment-select handoff used captured local gamepad references
- some fallback logic relied on `Gamepad.all` ordering

That is not safe as the main multiplayer architecture for a networked duel.

## Why It Was Deprecated

The active game direction is server-authoritative online duels. The local split-screen implementation was deprecated because:

- title/menu flow assumed one machine owned both seats
- gameplay spawning assumed one scene manager instantiated both players locally
- camera and HUD presentation assumed split-screen instead of one local view plus replicated opponent
- input ownership relied on local device ordering instead of client ownership

## If We Revive It Later

The likely reactivation path would be:

- keep the current network-first duel path as primary
- add a clearly separate local-versus bootstrap path instead of mixing it into the online session flow
- restore a dedicated local spawn mode in `GameSceneManager`
- restore split-screen camera routing and per-seat HUD canvas assignments
- replace any `Gamepad.all` fallback assumptions with explicit local-seat assignment rules

The safest future approach is to treat local multiplayer as a separate mode with its own scene/presentation configuration, not as a hidden branch inside the online duel flow.
