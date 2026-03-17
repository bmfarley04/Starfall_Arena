# Unity Editor Network Checklist

This checklist captures the serialized Unity editor changes that still need to be made or verified alongside the code migration to networked duels.

## Title Scene

- Ensure the title scene has a persistent `NetworkManager` object.
- Ensure the `NetworkManager` has a `UnityTransport` component.
- Add or wire a `NetworkSessionData` object with a `NetworkObject`.
- Add or wire a persistent `GameDataManager` object with:
  - `knownShips`
  - `knownAugments`
- Keep the main title canvas as the host/join choice screen.
- Main title `Host Game` and `Join Game` should stay on the project's normal clickable-button pattern.
- `NetMgr` no longer needs gameplay prefab or spawn-point assignments in the title scene. It only needs to sit beside the `NetworkManager`/`UnityTransport`.
- Update `TitleScreenManager` references:
  - `joinGameCanvas`
  - `joinGameFirstSelected`
  - `hostWaitingCanvas`
  - `hostWaitingFirstSelected`
  - `ipAddressInputField`
  - optional `networkStatusText`
- Do not rely on Unity `Button.onClick` for the new image-based host/join/back controls.
- `TitleScreenManager` now handles only the special hold-style controls on the join and waiting screens.
- Wire the `TitleScreenManager` hold-button references:
  - `joinConfirmButton.target` + `fillImage`
  - `joinBackButton.target` + `fillImage`
  - `waitingBackButton.target` + `fillImage`
- Wire the manual navigation groups:
  - `joinGameNavigation.targets`
  - `hostWaitingNavigation.targets`
- Action behavior is now manager-owned:
  - selecting `Join Game` on the join canvas and holding confirm triggers `StartJoinFlow()`
  - holding back on the join or waiting canvases triggers `CancelNetworkFlow()`
- Main title actions should be wired through the existing normal clickable flow:
  - main `Host Game` -> `StartHostingFlow()`
  - main `Join Game` -> `TransitionToJoinGame()`
- Input behavior:
  - submit/confirm hold = controller south button / keyboard `X`
  - back hold = controller east button / keyboard `B`

## Ship Select UI

- Add or wire only the `countdownTimerText` reference on `ShipSelectManager`.
- Verify the first-selected object for ship select still works with controller navigation.
- Verify the ship preview model parent and animation setup still behaves correctly when the screen is entered from the new network flow.
- Verify the timer is visible enough to communicate the auto-pick deadline because the extra local/opponent status labels are no longer used.

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
