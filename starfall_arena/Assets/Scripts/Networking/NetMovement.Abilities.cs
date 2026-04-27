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

    public void RequestConvergeBeamState(bool isFiring)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetConvergeBeamState state = new NetConvergeBeamState
        {
            Tick = NetTickUtil.CurrentTick,
            IsFiring = isFiring
        };

        if (IsServer)
        {
            HandleConvergeBeamStateServer(state);
            return;
        }

        SubmitConvergeBeamStateServerRpc(state);
    }

    public void RequestConvergeBeamAim(Vector2 aimPoint)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetConvergeBeamAimState state = new NetConvergeBeamAimState
        {
            Tick = NetTickUtil.CurrentTick,
            AimPoint = aimPoint
        };

        if (IsServer)
        {
            HandleConvergeBeamAimServer(state);
            return;
        }

        SubmitConvergeBeamAimServerRpc(state);
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

    public void RequestGuidedMissile(NetGuidedMissileState state)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        state.Tick = NetTickUtil.CurrentTick;

        if (IsServer)
        {
            HandleGuidedMissileServer(state);
            return;
        }

        SubmitGuidedMissileServerRpc(state);
    }

    public void RequestDodge(Vector2 direction)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetDodgeState state = new NetDodgeState
        {
            Direction = direction
        };

        if (IsServer)
        {
            HandleDodgeServer(state);
            return;
        }

        SubmitDodgeServerRpc(state);
    }

    public void RequestChronoStepState(NetChronoStepState state)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandleChronoStepServer(state);
            return;
        }

        SubmitChronoStepServerRpc(state);
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

    public void RequestGigaBlastChargeState(bool isCharging, int tier)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetGigaBlastChargeState state = new NetGigaBlastChargeState
        {
            IsCharging = isCharging,
            Tier = tier
        };

        if (IsServer)
        {
            HandleGigaBlastChargeServer(state);
            return;
        }

        SubmitGigaBlastChargeServerRpc(state);
    }

    public void RequestDarkMatterCast(int chargesSpent)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetDarkMatterCastState state = new NetDarkMatterCastState
        {
            ChargesSpent = Mathf.Max(chargesSpent, 1)
        };

        if (IsServer)
        {
            HandleDarkMatterCastServer(state);
            return;
        }

        SubmitDarkMatterCastServerRpc(state);
    }

    public void RequestFlameWaveCast(int chargesSpent)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetFlameWaveCastState state = new NetFlameWaveCastState
        {
            ChargesSpent = Mathf.Max(chargesSpent, 1)
        };

        if (IsServer)
        {
            HandleFlameWaveCastServer(state);
            return;
        }

        SubmitFlameWaveCastServerRpc(state);
    }

    public void RequestEmpowerState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetAbilityToggleState state = new NetAbilityToggleState
        {
            IsActive = isActive
        };

        if (IsServer)
        {
            HandleEmpowerStateServer(state);
            return;
        }

        SubmitEmpowerStateServerRpc(state);
    }

    public void BroadcastEmpowerState(bool isActive)
    {
        if (!IsServer)
        {
            return;
        }

        NetAbilityToggleState state = new NetAbilityToggleState
        {
            IsActive = isActive
        };

        BroadcastEmpowerStateClientRpc(state);
    }

    public void RequestBatteryRamState(NetBatteryRamState state)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        state.Tick = NetTickUtil.CurrentTick;

        if (IsServer)
        {
            HandleBatteryRamStateServer(state);
            return;
        }

        SubmitBatteryRamStateServerRpc(state);
    }

    public void BroadcastBatteryRamState(NetBatteryRamState state)
    {
        if (!IsServer)
        {
            return;
        }

        BroadcastBatteryRamStateClientRpc(state);
    }

    // ===== ABILITY SERVER RPCs =====

    [ServerRpc]
    private void SubmitGigaBlastChargeServerRpc(NetGigaBlastChargeState state, ServerRpcParams rpcParams = default)
    {
        HandleGigaBlastChargeServer(state);
    }

    [ServerRpc]
    private void SubmitBeamStateServerRpc(NetBeamState state, ServerRpcParams rpcParams = default)
    {
        HandleBeamStateServer(state);
    }

    [ServerRpc]
    private void SubmitConvergeBeamStateServerRpc(NetConvergeBeamState state, ServerRpcParams rpcParams = default)
    {
        HandleConvergeBeamStateServer(state);
    }

    [ServerRpc]
    private void SubmitConvergeBeamAimServerRpc(NetConvergeBeamAimState state, ServerRpcParams rpcParams = default)
    {
        HandleConvergeBeamAimServer(state);
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
    private void SubmitGuidedMissileServerRpc(NetGuidedMissileState state, ServerRpcParams rpcParams = default)
    {
        HandleGuidedMissileServer(state);
    }

    [ServerRpc]
    private void SubmitDodgeServerRpc(NetDodgeState state, ServerRpcParams rpcParams = default)
    {
        HandleDodgeServer(state);
    }

    [ServerRpc]
    private void SubmitChronoStepServerRpc(NetChronoStepState state, ServerRpcParams rpcParams = default)
    {
        HandleChronoStepServer(state);
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

    [ServerRpc]
    private void SubmitDarkMatterCastServerRpc(NetDarkMatterCastState state, ServerRpcParams rpcParams = default)
    {
        HandleDarkMatterCastServer(state);
    }

    [ServerRpc]
    private void SubmitBatteryRamStateServerRpc(NetBatteryRamState state, ServerRpcParams rpcParams = default)
    {
        HandleBatteryRamStateServer(state);
    }

    [ServerRpc]
    private void SubmitEmpowerStateServerRpc(NetAbilityToggleState state, ServerRpcParams rpcParams = default)
    {
        HandleEmpowerStateServer(state);
    }

    [ServerRpc]
    private void SubmitFlameWaveCastServerRpc(NetFlameWaveCastState state, ServerRpcParams rpcParams = default)
    {
        HandleFlameWaveCastServer(state);
    }

    // ===== ABILITY SERVER HANDLERS =====

    private void HandleGigaBlastChargeServer(NetGigaBlastChargeState state)
    {
        GigaBlast gigaBlast = GetComponent<GigaBlast>();
        if (gigaBlast == null)
        {
            return;
        }

        // Host already applied charge state locally in UseAbility/Update.
        // Only apply on the server's copy of a client-owned player.
        if (!IsOwner)
        {
            gigaBlast.ApplyNetworkChargeState(state.IsCharging, state.Tier);
        }

        BroadcastGigaBlastChargeClientRpc(state);
    }

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

    private void HandleConvergeBeamStateServer(NetConvergeBeamState state)
    {
        ConvergeBeam convergeBeam = GetComponent<ConvergeBeam>();
        if (convergeBeam == null)
        {
            return;
        }

        convergeBeam.ApplyNetworkConvergeBeamState(state.IsFiring, authoritative: true, requestedTick: state.Tick);
        BroadcastConvergeBeamStateClientRpc(state);
    }

    private void HandleConvergeBeamAimServer(NetConvergeBeamAimState state)
    {
        ConvergeBeam convergeBeam = GetComponent<ConvergeBeam>();
        if (convergeBeam == null)
        {
            return;
        }

        convergeBeam.ApplyNetworkConvergeBeamAim(state.AimPoint, authoritative: true);
        BroadcastConvergeBeamAimClientRpc(state);
    }

    private void HandleFireTrailStateServer(NetFireTrailState state)
    {
        FireWall fireWall = GetComponent<FireWall>();
        if (fireWall == null)
        {
            return;
        }

        // Broadcast trail state BEFORE applying locally, because applying
        // calls StartFireTrail → SpawnFireHazard → BroadcastFireHazardSpawnClientRpc.
        // Clients need the trail state (which creates the audio group) before
        // receiving hazard spawns so hazards are tracked in the group for audio.
        BroadcastFireTrailStateClientRpc(state);
        fireWall.ApplyNetworkTrailState(state.IsActive, authoritative: true);
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

    private void HandleGuidedMissileServer(NetGuidedMissileState state)
    {
        GuidedMissile guidedMissile = GetComponent<GuidedMissile>();
        if (guidedMissile == null)
        {
            return;
        }

        guidedMissile.ApplyNetworkGuidedMissile(state, authoritative: true);
        BroadcastGuidedMissileClientRpc(state);
    }

    private void HandleDodgeServer(NetDodgeState state)
    {
        Dodge dodge = GetComponent<Dodge>();
        if (dodge == null)
        {
            return;
        }

        dodge.ApplyNetworkDodge(state.Direction, authoritative: true);
        BroadcastDodgeClientRpc(state);
    }

    private void HandleChronoStepServer(NetChronoStepState state)
    {
        ChronoStep chronoStep = GetComponent<ChronoStep>();
        if (chronoStep == null)
        {
            return;
        }

        chronoStep.ApplyNetworkChronoStepState(state, authoritative: true);
        BroadcastChronoStepClientRpc(state);
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

    private void HandleDarkMatterCastServer(NetDarkMatterCastState state)
    {
        DarkMatter darkMatter = GetComponent<DarkMatter>();
        if (darkMatter == null)
        {
            return;
        }

        // Server spends charges and spawns authoritative hazards, then broadcasts spawns.
        darkMatter.ApplyNetworkDarkMatterCast(state.ChargesSpent, authoritative: true, chargesAlreadySpent: false);
    }

    private void HandleFlameWaveCastServer(NetFlameWaveCastState state)
    {
        FlameWave flameWave = GetComponent<FlameWave>();
        if (flameWave == null)
        {
            return;
        }

        flameWave.ApplyNetworkFlameWaveCast(state.ChargesSpent, authoritative: true, chargesAlreadySpent: false);
    }

    private void HandleBatteryRamStateServer(NetBatteryRamState state)
    {
        BatteryRam batteryRam = GetComponent<BatteryRam>();
        if (batteryRam == null)
        {
            return;
        }

        batteryRam.ApplyNetworkRamState(state, authoritative: true);
        BroadcastBatteryRamStateClientRpc(state);
    }

    private void HandleEmpowerStateServer(NetAbilityToggleState state)
    {
        Empower empower = GetComponent<Empower>();
        if (empower == null)
        {
            return;
        }

        empower.ApplyNetworkEmpowerState(state.IsActive, authoritative: true);
        BroadcastEmpowerStateClientRpc(state);
    }

    // ===== ABILITY CLIENT RPC BROADCASTS =====

    [ClientRpc]
    private void BroadcastGigaBlastChargeClientRpc(NetGigaBlastChargeState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        GigaBlast gigaBlast = GetComponent<GigaBlast>();
        gigaBlast?.ApplyNetworkChargeState(state.IsCharging, state.Tier);
    }

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
    private void BroadcastConvergeBeamStateClientRpc(NetConvergeBeamState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        ConvergeBeam convergeBeam = GetComponent<ConvergeBeam>();
        convergeBeam?.ApplyNetworkConvergeBeamState(state.IsFiring, authoritative: false, requestedTick: state.Tick);
    }

    [ClientRpc]
    private void BroadcastConvergeBeamAimClientRpc(NetConvergeBeamAimState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        ConvergeBeam convergeBeam = GetComponent<ConvergeBeam>();
        convergeBeam?.ApplyNetworkConvergeBeamAim(state.AimPoint, authoritative: false);
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
    private void BroadcastGuidedMissileClientRpc(NetGuidedMissileState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        GuidedMissile guidedMissile = GetComponent<GuidedMissile>();
        guidedMissile?.ApplyNetworkGuidedMissile(state, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastDodgeClientRpc(NetDodgeState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Dodge dodge = GetComponent<Dodge>();
        dodge?.ApplyNetworkDodge(state.Direction, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastChronoStepClientRpc(NetChronoStepState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        ChronoStep chronoStep = GetComponent<ChronoStep>();
        chronoStep?.ApplyNetworkChronoStepState(state, authoritative: false);
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

    // ===== DARK MATTER HAZARD =====

    public void BroadcastDarkMatterHazardSpawn(NetDarkMatterHazardSpawnData spawnData)
    {
        if (!IsServer)
        {
            return;
        }

        spawnData.ServerSpawnTime = NetworkManager.Singleton.ServerTime.Time;
        BroadcastDarkMatterHazardSpawnClientRpc(spawnData);
    }

    [ClientRpc]
    private void BroadcastDarkMatterHazardSpawnClientRpc(NetDarkMatterHazardSpawnData spawnData)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        float elapsed = (float)(NetworkManager.Singleton.ServerTime.Time - spawnData.ServerSpawnTime);
        if (elapsed > 0f)
        {
            spawnData.Lifetime = Mathf.Max(spawnData.Lifetime - elapsed, 0f);
        }

        DarkMatter darkMatter = GetComponent<DarkMatter>();
        darkMatter?.SpawnRemoteHazard(spawnData);
    }

    // ===== FLAME WAVE HAZARD =====

    public void BroadcastFlameWaveHazardSpawn(NetFlameWaveHazardSpawnData spawnData)
    {
        if (!IsServer)
        {
            return;
        }

        spawnData.ServerSpawnTime = NetworkManager.Singleton.ServerTime.Time;
        BroadcastFlameWaveHazardSpawnClientRpc(spawnData);
    }

    [ClientRpc]
    private void BroadcastFlameWaveHazardSpawnClientRpc(NetFlameWaveHazardSpawnData spawnData)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        float elapsed = (float)(NetworkManager.Singleton.ServerTime.Time - spawnData.ServerSpawnTime);
        if (elapsed > 0f)
        {
            spawnData.Lifetime = Mathf.Max(spawnData.Lifetime - elapsed, 0f);
        }

        FlameWave flameWave = GetComponent<FlameWave>();
        flameWave?.SpawnRemoteHazard(spawnData);
    }

    [ClientRpc]
    private void BroadcastBatteryRamStateClientRpc(NetBatteryRamState state)
    {
        if (IsServer)
        {
            return;
        }

        if (IsOwner && state.SkipOwner)
        {
            return;
        }

        BatteryRam batteryRam = GetComponent<BatteryRam>();
        batteryRam?.ApplyNetworkRamState(state, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastEmpowerStateClientRpc(NetAbilityToggleState state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Empower empower = GetComponent<Empower>();
        empower?.ApplyNetworkEmpowerState(state.IsActive, authoritative: false);
    }

    // ===== FIRE HAZARD (Firewall-specific) =====

    public void BroadcastFireHazardSpawn(NetFireHazardSpawnData spawnData)
    {
        if (!IsServer)
        {
            return;
        }

        spawnData.ServerSpawnTime = NetworkManager.Singleton.ServerTime.Time;
        BroadcastFireHazardSpawnClientRpc(spawnData);
    }

    [ClientRpc]
    private void BroadcastFireHazardSpawnClientRpc(NetFireHazardSpawnData spawnData)
    {
        if (IsServer)
        {
            return;
        }

        // Subtract the network transit time from the lifetime so hazards
        // expire at the same real moment on server and client.
        float elapsed = (float)(NetworkManager.Singleton.ServerTime.Time - spawnData.ServerSpawnTime);
        if (elapsed > 0f)
        {
            spawnData.Lifetime = Mathf.Max(spawnData.Lifetime - elapsed, 0f);
        }

        FireWall fireWall = GetComponent<FireWall>();
        fireWall?.SpawnRemoteHazard(spawnData);
    }
}
