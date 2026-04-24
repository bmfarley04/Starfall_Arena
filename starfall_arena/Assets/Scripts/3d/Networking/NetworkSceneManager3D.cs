using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkSceneManager3D : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Fallback Ships")]
    [SerializeField] private ShipData defaultPlayer1Ship;
    [SerializeField] private ShipData defaultPlayer2Ship;

    [Header("Bootstrap")]
    [SerializeField] private bool spawnPlayersOnStart = true;
    [SerializeField] private float spawnRetryIntervalSeconds = 0.1f;

    private bool _playersSpawned;

    private void Start()
    {
        if (!NetMgr.IsNetworked || NetworkManager.Singleton == null)
        {
            return;
        }

        StartCoroutine(BindLocalOwnerCameraWhenReady());

        if (!spawnPlayersOnStart || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        StartCoroutine(SpawnPlayersWhenReady());
    }

    private IEnumerator SpawnPlayersWhenReady()
    {
        while (!_playersSpawned)
        {
            if (!AreRequiredClientsConnected())
            {
                yield return new WaitForSeconds(spawnRetryIntervalSeconds);
                continue;
            }

            if (NetMovement3D.TryGetPlayerBySlot(1, out _) && NetMovement3D.TryGetPlayerBySlot(2, out _))
            {
                _playersSpawned = true;
                yield break;
            }

            ShipData player1Data = ResolveShipDataForSlot(0, defaultPlayer1Ship);
            ShipData player2Data = ResolveShipDataForSlot(1, defaultPlayer2Ship);
            ulong player1OwnerId = ResolveOwnerClientIdForSlot(0);
            ulong player2OwnerId = ResolveOwnerClientIdForSlot(1);

            if (player1Data == null || player2Data == null)
            {
                Debug.LogError("[NetworkSceneManager3D] Unable to resolve both 3D ship selections. Check the GameDataManager roster and default ship references.", this);
                yield break;
            }

            GameDataManager.Instance?.SetSelectedShips(player1Data, player2Data);

            SpawnPlayer(player1Data, player1SpawnPoint, player1OwnerId, 1);
            SpawnPlayer(player2Data, player2SpawnPoint, player2OwnerId, 2);

            _playersSpawned = true;
        }
    }

    private bool AreRequiredClientsConnected()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || NetworkManager.Singleton.ConnectedClientsIds == null)
        {
            return false;
        }

        return NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
    }

    private ShipData ResolveShipDataForSlot(int slotIndex, ShipData fallback)
    {
        ShipData resolved = null;
        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null)
        {
            resolved = slotIndex == 0 ? session.Player1Selection?.ShipData : session.Player2Selection?.ShipData;
        }

        if (resolved == null && GameDataManager.Instance != null && GameDataManager.Instance.selectedShipClasses.Count > slotIndex)
        {
            resolved = GameDataManager.Instance.selectedShipClasses[slotIndex];
        }

        if (!IsKnown3DShip(resolved))
        {
            if (resolved != null)
            {
                Debug.LogWarning($"[NetworkSceneManager3D] Ship '{resolved.ShipId}' is not part of the registered 3D roster. Falling back to the configured default for slot {slotIndex + 1}.", this);
            }

            resolved = fallback;
        }

        if (resolved != null && resolved.shipPrefab == null)
        {
            Debug.LogError($"[NetworkSceneManager3D] Ship '{resolved.ShipId}' has no gameplay prefab assigned in ShipData.", this);
            return null;
        }

        return resolved;
    }

    private static bool IsKnown3DShip(ShipData shipData)
    {
        if (shipData == null || GameDataManager.Instance == null)
        {
            return false;
        }

        IReadOnlyList<ShipData> knownShips = GameDataManager.Instance.Known3DShips;
        for (int i = 0; i < knownShips.Count; i++)
        {
            if (knownShips[i] == shipData)
            {
                return true;
            }
        }

        return false;
    }

    private ulong ResolveOwnerClientIdForSlot(int slotIndex)
    {
        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null)
        {
            ulong sessionClientId = slotIndex == 0
                ? session.Player1Selection?.ClientId ?? ulong.MaxValue
                : session.Player2Selection?.ClientId ?? ulong.MaxValue;
            if (sessionClientId != ulong.MaxValue)
            {
                return sessionClientId;
            }
        }

        List<ulong> connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        connectedClients.Sort();
        if (slotIndex >= 0 && slotIndex < connectedClients.Count)
        {
            return connectedClients[slotIndex];
        }

        return NetworkManager.ServerClientId;
    }

    private void SpawnPlayer(ShipData shipData, Transform spawnPoint, ulong ownerClientId, byte playerSlot)
    {
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject instance = NetMgr.SpawnPlayerNetworked(shipData.shipPrefab, spawnPosition, spawnRotation, ownerClientId);
        if (instance == null)
        {
            Debug.LogError($"[NetworkSceneManager3D] Failed to spawn player slot {playerSlot} using ship '{shipData.ShipId}'.", this);
            return;
        }

        instance.name = $"NetworkPlayer3D_{playerSlot}_{shipData.name}";
        instance.tag = playerSlot == 1 ? "Player1" : "Player2";

        NetMovement3D netMovement = instance.GetComponent<NetMovement3D>();
        if (netMovement == null)
        {
            Debug.LogError($"[NetworkSceneManager3D] Spawned ship '{shipData.ShipId}' is missing NetMovement3D.", instance);
            return;
        }

        if (instance.GetComponent<NetCombat3D>() == null)
        {
            Debug.LogWarning($"[NetworkSceneManager3D] Spawned ship '{shipData.ShipId}' is missing NetCombat3D; 3D combat input will remain suppressed for this player.", instance);
        }

        netMovement.SetNetworkPlayerIndex(playerSlot);
    }

    private IEnumerator BindLocalOwnerCameraWhenReady()
    {
        while (NetMgr.IsNetworked && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetMovement3D[] movements = FindObjectsByType<NetMovement3D>(FindObjectsSortMode.None);
            for (int i = 0; i < movements.Length; i++)
            {
                NetMovement3D movement = movements[i];
                if (movement == null || !movement.IsSpawned || !movement.IsOwner)
                {
                    continue;
                }

                movement.EnsureOwnerLocalControlReady();
                movement.BindOwnerCameraAndTracking();
                yield break;
            }

            yield return new WaitForSeconds(spawnRetryIntervalSeconds);
        }
    }
}
