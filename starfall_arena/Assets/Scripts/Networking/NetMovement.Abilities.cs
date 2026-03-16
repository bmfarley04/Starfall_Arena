using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Partial class handling all ability networking: request methods (owner → server),
/// ServerRpc forwarding, server-side handlers, and ClientRpc broadcasts.
/// </summary>
public partial class NetMovement
{
    // ===== ABILITY REQUESTS (Owner → Server) =====

    public void RequestBeamState(bool isFiring)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetBeamState state = new NetBeamState
        {
            Tick = NetTickUtil.CurrentTick,
            IsFiring = isFiring
        };

        if (IsServer)
        {
            HandleBeamStateServer(state);
            return;
        }

        SubmitBeamStateServerRpc(state);
    }

    public void RequestFireTrailState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetFireTrailState state = new NetFireTrailState
        {
            Tick = NetTickUtil.CurrentTick,
            IsActive = isActive
        };

        if (IsServer)
        {
            HandleFireTrailStateServer(state);
            return;
        }

        SubmitFireTrailStateServerRpc(state);
    }

    public void RequestReflectActivation()
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandleReflectActivationServer();
            return;
        }

        SubmitReflectActivationServerRpc();
    }

    public void RequestTeleport(Vector2 targetPosition)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTeleportState state = new NetTeleportState
        {
            TargetPosition = targetPosition
        };

        if (IsServer)
        {
            HandleTeleportServer(state);
            return;
        }

        SubmitTeleportServerRpc(state);
    }

    public void RequestClass2ShieldActivation()
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandleClass2ShieldServer(new NetClass2ShieldState { IsActive = true });
            return;
        }

        SubmitClass2ShieldServerRpc(new NetClass2ShieldState { IsActive = true });
    }

    public void RequestTractorBeamState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTractorBeamState state = new NetTractorBeamState
        {
            IsActive = isActive
        };

        if (IsServer)
        {
            HandleTractorBeamServer(state);
            return;
        }

        SubmitTractorBeamServerRpc(state);
    }

    public void RequestTriggerBombLaunch(Vector2 spawnPosition, Vector2 velocity)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTriggerBombLaunchState state = new NetTriggerBombLaunchState
        {
            SpawnPosition = spawnPosition,
            Velocity = velocity
        };

        if (IsServer)
        {
            HandleTriggerBombLaunchServer(state);
            return;
        }

        SubmitTriggerBombLaunchServerRpc(state);
    }

    public void RequestTriggerBombDetonate(Vector2 position)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTriggerBombDetonateState state = new NetTriggerBombDetonateState
        {
            Position = position
        };

        if (IsServer)
        {
            HandleTriggerBombDetonateServer(state);
            return;
        }

        SubmitTriggerBombDetonateServerRpc(state);
    }

    public void RequestFaerieShiftState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetAbilityToggleState state = new NetAbilityToggleState { IsActive = isActive };
        if (IsServer)
        {
            HandleFaerieShiftServer(state);
            return;
        }

        SubmitFaerieShiftServerRpc(state);
    }

    public void RequestInvisibilityState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetAbilityToggleState state = new NetAbilityToggleState { IsActive = isActive };
        if (IsServer)
        {
            HandleInvisibilityServer(state);
            return;
        }

        SubmitInvisibilityServerRpc(state);
    }

    public void SetInvisibilityStateAuthoritative(bool isActive)
    {
        if (!IsServer)
        {
            return;
        }

        HandleInvisibilityServer(new NetAbilityToggleState { IsActive = isActive });
    }

    // ===== ABILITY SERVER RPCs =====

    [ServerRpc]
    private void SubmitBeamStateServerRpc(NetBeamState state, ServerRpcParams rpcParams = default)
    {
        HandleBeamStateServer(state);
    }

    [ServerRpc]
    private void SubmitFireTrailStateServerRpc(NetFireTrailState state, ServerRpcParams rpcParams = default)
    {
        HandleFireTrailStateServer(state);
    }

    [ServerRpc]
    private void SubmitReflectActivationServerRpc(ServerRpcParams rpcParams = default)
    {
        HandleReflectActivationServer();
    }

    [ServerRpc]
    private void SubmitTeleportServerRpc(NetTeleportState state, ServerRpcParams rpcParams = default)
    {
        HandleTeleportServer(state);
    }

    [ServerRpc]
    private void SubmitClass2ShieldServerRpc(NetClass2ShieldState state, ServerRpcParams rpcParams = default)
    {
        HandleClass2ShieldServer(state);
    }

    [ServerRpc]
    private void SubmitTractorBeamServerRpc(NetTractorBeamState state, ServerRpcParams rpcParams = default)
    {
        HandleTractorBeamServer(state);
    }

    [ServerRpc]
    private void SubmitTriggerBombLaunchServerRpc(NetTriggerBombLaunchState state, ServerRpcParams rpcParams = default)
    {
        HandleTriggerBombLaunchServer(state);
    }

    [ServerRpc]
    private void SubmitTriggerBombDetonateServerRpc(NetTriggerBombDetonateState state, ServerRpcParams rpcParams = default)
    {
        HandleTriggerBombDetonateServer(state);
    }

    [ServerRpc]
    private void SubmitFaerieShiftServerRpc(NetAbilityToggleState state, ServerRpcParams rpcParams = default)
    {
        HandleFaerieShiftServer(state);
    }

    [ServerRpc]
    private void SubmitInvisibilityServerRpc(NetAbilityToggleState state, ServerRpcParams rpcParams = default)
    {
        HandleInvisibilityServer(state);
    }

    // ===== ABILITY SERVER HANDLERS =====

    private void HandleBeamStateServer(NetBeamState state)
    {
        Beam beamAbility = GetComponent<Beam>();
        if (beamAbility == null)
        {
            return;
        }

        beamAbility.ApplyNetworkBeamState(state.IsFiring, authoritative: true, requestedTick: state.Tick);
        BroadcastBeamStateClientRpc(state);
    }

    private void HandleFireTrailStateServer(NetFireTrailState state)
    {
        FireWall fireWall = GetComponent<FireWall>();
        if (fireWall == null)
        {
            return;
        }

        fireWall.ApplyNetworkTrailState(state.IsActive, authoritative: true);
        BroadcastFireTrailStateClientRpc(state);
    }

    private void HandleReflectActivationServer()
    {
        Reflector reflector = GetComponent<Reflector>();
        if (reflector == null)
        {
            return;
        }

        reflector.ApplyNetworkReflectActivation(authoritative: true);
        BroadcastReflectActivationClientRpc();
    }

    private void HandleTeleportServer(NetTeleportState state)
    {
        Teleport teleportAbility = GetComponent<Teleport>();
        if (teleportAbility == null)
        {
            return;
        }

        teleportAbility.ApplyNetworkTeleport(state.TargetPosition, authoritative: true);
        BroadcastTeleportClientRpc(state);
    }

    private void HandleClass2ShieldServer(NetClass2ShieldState state)
    {
        Class2Shield shieldAbility = GetComponent<Class2Shield>();
        if (shieldAbility == null || !state.IsActive)
        {
            return;
        }

        shieldAbility.ApplyNetworkShieldActivation(authoritative: true);
        BroadcastClass2ShieldClientRpc(state);
    }

    private void HandleTractorBeamServer(NetTractorBeamState state)
    {
        TractorBeam tractorBeam = GetComponent<TractorBeam>();
        if (tractorBeam == null)
        {
            return;
        }

        tractorBeam.ApplyNetworkTractorBeamState(state.IsActive, authoritative: true);
        BroadcastTractorBeamClientRpc(state);
    }

    private void HandleTriggerBombLaunchServer(NetTriggerBombLaunchState state)
    {
        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        if (triggerBomb == null)
        {
            return;
        }

        triggerBomb.ApplyNetworkBombLaunch(state, authoritative: true);
        BroadcastTriggerBombLaunchClientRpc(state);
    }

    private void HandleTriggerBombDetonateServer(NetTriggerBombDetonateState state)
    {
        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        if (triggerBomb == null)
        {
            return;
        }

        triggerBomb.ApplyNetworkBombDetonation(state, authoritative: true);
        BroadcastTriggerBombDetonateClientRpc(state);
    }

    private void HandleFaerieShiftServer(NetAbilityToggleState state)
    {
        FaerieShift faerieShift = GetComponent<FaerieShift>();
        if (faerieShift == null)
        {
            return;
        }

        faerieShift.ApplyNetworkShiftState(state.IsActive, authoritative: true);
        BroadcastFaerieShiftClientRpc(state);
    }

    private void HandleInvisibilityServer(NetAbilityToggleState state)
    {
        Invisibility invisibility = GetComponent<Invisibility>();
        if (invisibility == null)
        {
            return;
        }

        invisibility.ApplyNetworkInvisibilityState(state.IsActive, authoritative: true);
        BroadcastInvisibilityClientRpc(state);
    }

    // ===== ABILITY CLIENT RPC BROADCASTS =====

    [ClientRpc]
    private void BroadcastBeamStateClientRpc(NetBeamState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Beam beamAbility = GetComponent<Beam>();
        beamAbility?.ApplyNetworkBeamState(state.IsFiring, authoritative: false, requestedTick: state.Tick);
    }

    [ClientRpc]
    private void BroadcastFireTrailStateClientRpc(NetFireTrailState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        FireWall fireWall = GetComponent<FireWall>();
        fireWall?.ApplyNetworkTrailState(state.IsActive, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastReflectActivationClientRpc()
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Reflector reflector = GetComponent<Reflector>();
        reflector?.ApplyNetworkReflectActivation(authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTeleportClientRpc(NetTeleportState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Teleport teleportAbility = GetComponent<Teleport>();
        teleportAbility?.ApplyNetworkTeleport(state.TargetPosition, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastClass2ShieldClientRpc(NetClass2ShieldState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Class2Shield shieldAbility = GetComponent<Class2Shield>();
        shieldAbility?.ApplyNetworkShieldActivation(authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTractorBeamClientRpc(NetTractorBeamState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        TractorBeam tractorBeam = GetComponent<TractorBeam>();
        tractorBeam?.ApplyNetworkTractorBeamState(state.IsActive, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTriggerBombLaunchClientRpc(NetTriggerBombLaunchState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        triggerBomb?.ApplyNetworkBombLaunch(state, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTriggerBombDetonateClientRpc(NetTriggerBombDetonateState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        triggerBomb?.ApplyNetworkBombDetonation(state, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastFaerieShiftClientRpc(NetAbilityToggleState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        FaerieShift faerieShift = GetComponent<FaerieShift>();
        faerieShift?.ApplyNetworkShiftState(state.IsActive, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastInvisibilityClientRpc(NetAbilityToggleState state)
    {
        // Server already applied in handler. Owner skips activation (applied locally)
        // but receives deactivation (server can force-end invisibility).
        if (IsServer) return;
        if (IsOwner && state.IsActive) return;

        Invisibility invisibility = GetComponent<Invisibility>();
        invisibility?.ApplyNetworkInvisibilityState(state.IsActive, authoritative: false);
    }

    // ===== FIRE HAZARD (Firewall-specific) =====

    public void BroadcastFireHazardSpawn(NetFireHazardSpawnData spawnData)
    {
        if (!IsServer)
        {
            return;
        }

        BroadcastFireHazardSpawnClientRpc(spawnData);
    }

    [ClientRpc]
    private void BroadcastFireHazardSpawnClientRpc(NetFireHazardSpawnData spawnData)
    {
        if (IsServer)
        {
            return;
        }

        FireWall fireWall = GetComponent<FireWall>();
        fireWall?.SpawnRemoteHazard(spawnData);
    }
}
