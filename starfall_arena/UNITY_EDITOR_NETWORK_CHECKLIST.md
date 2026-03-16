# Unity Editor Network Checklist

This checklist captures the serialized Unity editor changes that still need to be made or verified alongside the code migration to networked duels.

## Title Scene

- Ensure the title scene has a persistent `NetworkManager` object.
- Ensure the `NetworkManager` has a `UnityTransport` component.
- Add or wire a `NetworkSessionData` object with a `NetworkObject`.
- Add or wire a persistent `GameDataManager` object with:
  - `knownShips`
  - `knownAugments`
- Update `TitleScreenManager` references:
  - `onlineMenuCanvas`
  - `onlineMenuFirstSelected`
  - `joinGameCanvas`
  - `joinGameFirstSelected`
  - `hostWaitingCanvas`
  - `hostWaitingFirstSelected`
  - `ipAddressInputField`
  - `networkStatusText`
- Add button events for:
  - `TransitionToOnlineMenu()`
  - `TransitionToJoinGame()`
  - `StartHostingFlow()`
  - `StartJoinFlow()`
  - `CancelNetworkFlow()`

## Ship Select UI

- Add or wire `ShipSelectManager` text references:
  - `countdownTimerText`
  - `localSelectionStatusText`
  - `remoteSelectionStatusText`
  - `timeoutStatusText`
- Verify the first-selected object for ship select still works with controller navigation.
- Verify the ship preview model parent and animation setup still behaves correctly when the screen is entered from the new network flow.

## Gameplay Scene

- Remove or disable `PlayerInputManager` local-join behavior for the active network duel path.
- Rework the active duel presentation away from split-screen camera rects.
- Decide which camera is the network gameplay `MainCamera` and ensure `NetMovement.AssignOwnerCameraAndTracking()` can find it.
- Verify the gameplay scene contains the right HUD canvases for:
  - local player status
  - opponent status
  - local ability HUD
  - optional opponent ability/status panel
- Revisit any existing `SplitScreenManager` references and disable them where the network path should remain single-screen.

## Network Prefabs

- Register every ship gameplay prefab used by `ShipData.shipPrefab` in the `NetworkManager` prefab list.
- Verify each gameplay ship prefab includes:
  - `NetworkObject`
  - `NetMovement`
  - disabled-by-default `PlayerInput` if that is still the intended ownership pattern

## Data Assets

- Open all `ShipData` assets once so the new stable `ShipId` is generated and serialized.
- Verify the `GameDataManager.knownShips` list includes every selectable ship.
- Verify the `GameDataManager.knownAugments` list includes every augment used in augment drafts.

## Verification Pass

- Host can enter the waiting screen.
- Client can enter an IP and connect.
- Both peers transition into ship select when the second client joins.
- Countdown/status texts update correctly.
- Final ship choices carry into gameplay.
- Network-spawned ships despawn correctly on death instead of lingering as stale NGO objects.
