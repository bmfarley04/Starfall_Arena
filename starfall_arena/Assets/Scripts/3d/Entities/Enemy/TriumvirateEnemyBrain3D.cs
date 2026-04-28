using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class TriumvirateEnemyBrain3D : NetworkBehaviour
{
    private enum SquadState
    {
        Forming,
        Settling,
        Linking,
        ChargeDelay,
        Firing,
        Cooldown
    }

    private enum FormationSlotPreference
    {
        Auto,
        Top,
        LowerLeft,
        LowerRight
    }

    private static readonly RaycastHit[] BeamHits = new RaycastHit[16];
    private const int TopSlotIndex = 0;
    private const int LowerLeftSlotIndex = 1;
    private const int LowerRightSlotIndex = 2;
    private const int FormationSlotCount = 3;

    [Header("Squad")]
    [Tooltip("Optional authored squad references. Leave empty to auto-link to the closest Triumvirate members with the same Squad Key.")]
    [SerializeField] private TriumvirateEnemyBrain3D[] squadMembers = new TriumvirateEnemyBrain3D[0];
    [Tooltip("Auto-discovery only groups members with the same key. Use a different key for separate Triumvirate groups spawned near each other.")]
    [SerializeField] private string squadKey = "Triumvirate";
    [Tooltip("Maximum distance used when auto-discovering the other two Triumvirate ships.")]
    [SerializeField] private float autoLinkRadius = 35f;
    [Tooltip("Seconds between squad discovery retries while references are missing.")]
    [SerializeField] private float autoLinkRetryInterval = 0.5f;

    [Header("Formation")]
    [Tooltip("Optional fixed slot for this squad member. Leave Auto to let the coordinator assign a stable slot when the squad first forms.")]
    [SerializeField] private FormationSlotPreference formationSlotPreference = FormationSlotPreference.Auto;
    [Tooltip("Distance from the target where the triangle center should settle before linking when Anchor Formation Near Current Squad is disabled.")]
    [SerializeField] private float formationDistanceFromTarget = 32f;
    [Tooltip("Radius of the triangle each surviving member tries to occupy.")]
    [SerializeField] private float triangleRadius = 8f;
    [Tooltip("If true, the squad forms its triangle around its current group center instead of first relocating to Formation Distance From Target.")]
    [SerializeField] private bool anchorFormationNearCurrentSquad = true;
    [Tooltip("Horizontal spacing between the two lower ships in the vertical triangle formation.")]
    [SerializeField] private float verticalTriangleWidth = 8f;
    [Tooltip("Height of the upper ship above the two lower ships in the vertical triangle formation.")]
    [SerializeField] private float verticalTriangleHeight = 3f;
    [Tooltip("How close each ship must be to its assigned triangle point before linking can begin.")]
    [SerializeField] private float formationTolerance = 2.25f;
    [Tooltip("Speed scale used while moving into the triangle formation.")]
    [SerializeField] private float formationSpeedScale = 0.65f;
    [Tooltip("If true, the triangle is kept on the squad's current world-Y plane. Leave off for the intended two-low / one-high vertical triangle.")]
    [SerializeField] private bool keepFormationOnWorldYPlane;
    [Tooltip("Seconds the squad holds formation before the first link appears.")]
    [SerializeField] private float settleDuration = 0.75f;

    [Header("Link Sequence")]
    [Tooltip("Lightning prefab used for cosmetic ship-to-ship links. This can be the enemy lightning beam prefab.")]
    [SerializeField] private GameObject linkLightningPrefab;
    [Tooltip("Seconds between each link appearing.")]
    [SerializeField] private float linkStepDuration = 0.65f;
    [Tooltip("Seconds after the last link before the player-facing beam starts.")]
    [SerializeField] private float finalChargeDelay = 0.55f;
    [Tooltip("Points used by the cosmetic link line.")]
    [SerializeField] private int linkPointCount = 4;
    [Tooltip("World-space jitter amplitude used by cosmetic links.")]
    [SerializeField] private float linkAmplitude = 0.45f;
    [Tooltip("Seconds between link line jitter updates.")]
    [SerializeField] private float linkJitterInterval = 0.05f;

    [Header("Final Beam")]
    [Tooltip("Beam weapon used for the final player-facing lightning beam. Configure its prefab to enemy_lightning_beam and set its damage to 0 if this brain owns damage.")]
    [SerializeField] private BeamWeapon3D finalBeamWeapon;
    [Tooltip("Network combat helper used to replicate the final beam visual.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;
    [Tooltip("Maximum final beam damage range.")]
    [SerializeField] private float finalBeamRange = 70f;
    [Tooltip("Spherecast radius used by the final beam damage check.")]
    [SerializeField] private float finalBeamHitscanRadius = 0.45f;
    [Tooltip("Damage per second when only one member survives to fire.")]
    [SerializeField] private float oneMemberDamagePerSecond = 6f;
    [Tooltip("Damage per second when two members survive to fire.")]
    [SerializeField] private float twoMemberDamagePerSecond = 14f;
    [Tooltip("Damage per second when all three members complete the link.")]
    [SerializeField] private float threeMemberDamagePerSecond = 34f;
    [Tooltip("How long the final beam remains active.")]
    [SerializeField] private float finalBeamDuration = 1.8f;
    [Tooltip("Cooldown before the squad reforms and tries the pattern again.")]
    [SerializeField] private float attackCooldown = 3.25f;
    [Tooltip("Movement slow multiplier applied only by the full three-member beam.")]
    [Range(0f, 1f)]
    [SerializeField] private float fullTriadSlowMultiplier = 0.45f;
    [Tooltip("Slow duration refreshed while the full three-member beam hits a player.")]
    [SerializeField] private float fullTriadSlowDuration = 0.35f;
    [Tooltip("Optional temporary thruster emission multiplier applied with the full-triad slow.")]
    [Range(0f, 1f)]
    [SerializeField] private float fullTriadSlowEngineEmissionScale = 0.35f;
    [Tooltip("Layers considered by the final beam damage ray. Include player colliders and line-of-sight blockers.")]
    [SerializeField] private LayerMask finalBeamMask = ~0;

    [Header("Debug")]
    [Tooltip("If true, logs coordinator state changes and major Triumvirate attack milestones.")]
    [SerializeField] private bool logStateChanges = true;
    [Tooltip("If true, logs formation progress while the squad is trying to reach triangle slots.")]
    [SerializeField] private bool logFormationProgress;
    [Tooltip("Minimum seconds between repeated formation progress logs.")]
    [SerializeField] private float formationProgressLogInterval = 0.5f;

    private readonly List<TriumvirateEnemyBrain3D> _activeMembers = new List<TriumvirateEnemyBrain3D>(3);
    private readonly List<GameObject> _activeLinks = new List<GameObject>(3);
    private readonly List<(TriumvirateEnemyBrain3D A, TriumvirateEnemyBrain3D B)> _pendingLinks = new List<(TriumvirateEnemyBrain3D, TriumvirateEnemyBrain3D)>(3);
    private readonly bool[] _claimedFormationSlots = new bool[FormationSlotCount];

    private Enemy3D _enemy;
    private EnemyAIFlightController3D _flightController;
    private EnemyTargetSensor3D _targetSensor;
    private NetworkObject _networkObject;
    private float _nextAutoLinkTime;
    private float _stateEndTime;
    private SquadState _state;
    private int _nextLinkIndex;
    private int _survivorCountAtBeamStart;
    private int _lastActiveMemberCount;
    private bool _beamActive;
    private float _nextFormationProgressLogTime;
    private int _assignedFormationSlot = -1;
    private bool _loggedDuplicateFormationSlot;

    private bool IsAlive => _enemy != null && _enemy.CurrentHealth > 0f && gameObject.activeInHierarchy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy3D>();
        _flightController = GetComponent<EnemyAIFlightController3D>();
        _targetSensor = GetComponent<EnemyTargetSensor3D>();
        _networkObject = GetComponent<NetworkObject>();
        finalBeamWeapon ??= GetComponent<BeamWeapon3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
    }

    private void OnDisable()
    {
        StopFinalBeam();
        ClearLinkVisuals(replicateToClients: false);
        _flightController?.ClearFlightIntent();
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            StopFinalBeam();
            _flightController?.ClearFlightIntent();
            return;
        }

        RefreshSquadMembers();
        if (!IsCoordinator())
        {
            return;
        }

        Entity3D target = ResolveTarget();
        if (target == null)
        {
            LogStateMessage("No target available; clearing flight intent and waiting in Forming.");
            StopFinalBeam();
            ClearLinkVisuals();
            SetState(SquadState.Forming, 0f);
            ClearSquadFlightIntent();
            return;
        }

        RefreshActiveMembers();
        if (_activeMembers.Count == 0)
        {
            return;
        }

        HandleSurvivorCountChanged();

        switch (_state)
        {
            case SquadState.Forming:
                UpdateFormation(target);
                break;
            case SquadState.Settling:
                HoldFormation(target);
                AdvanceAfterTimer(SquadState.Linking);
                break;
            case SquadState.Linking:
                HoldFormation(target);
                UpdateLinking();
                break;
            case SquadState.ChargeDelay:
                HoldFormation(target);
                AdvanceAfterTimer(SquadState.Firing);
                break;
            case SquadState.Firing:
                UpdateFinalBeam(target);
                break;
            case SquadState.Cooldown:
                HoldFormation(target);
                AdvanceAfterTimer(SquadState.Forming);
                break;
        }
    }

    private void UpdateFormation(Entity3D target)
    {
        bool allInPlace = MoveSquadIntoFormation(target);
        if (allInPlace)
        {
            LogStateMessage("Triangle formation reached; settling before link sequence.");
            SetState(SquadState.Settling, settleDuration);
        }
    }

    private void HoldFormation(Entity3D target)
    {
        MoveSquadIntoFormation(target);
    }

    private bool MoveSquadIntoFormation(Entity3D target)
    {
        bool allInPlace = true;
        EnsureFormationSlotAssignments();
        Vector3 targetPosition = ResolveTargetPoint(target);
        Vector3 squadCenter = ResolveActiveSquadCenter();
        Vector3 centerDirection = (squadCenter - targetPosition).sqrMagnitude > 0.0001f
            ? (squadCenter - targetPosition).normalized
            : -target.transform.forward;
        Vector3 center = anchorFormationNearCurrentSquad
            ? squadCenter
            : targetPosition + centerDirection * Mathf.Max(1f, formationDistanceFromTarget);
        Vector3 toTarget = (targetPosition - center).sqrMagnitude > 0.0001f
            ? (targetPosition - center).normalized
            : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, toTarget);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }
        right.Normalize();
        Vector3 formationUp = keepFormationOnWorldYPlane ? Vector3.zero : Vector3.up;
        float farthestMemberDistance = 0f;

        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive)
            {
                continue;
            }

            int formationSlot = ResolveMemberFormationSlot(member, i);
            Vector3 slot = ResolveFormationSlot(
                center,
                right,
                formationUp,
                formationSlot,
                _activeMembers.Count,
                triangleRadius,
                keepFormationOnWorldYPlane,
                verticalTriangleWidth,
                verticalTriangleHeight);
            Vector3 toSlot = slot - member.transform.position;
            float distance = toSlot.magnitude;
            farthestMemberDistance = Mathf.Max(farthestMemberDistance, distance);
            Vector3 faceDirection = targetPosition - member.transform.position;
            if (distance > formationTolerance)
            {
                allInPlace = false;
                Vector3 slotDirection = toSlot / Mathf.Max(distance, 0.0001f);
                member._flightController?.SetFlightIntent(slotDirection, slotDirection, formationSpeedScale, moveBackward: false);
            }
            else
            {
                member._flightController?.SetFacingDirection(faceDirection);
            }
        }

        LogFormationProgressIfNeeded(farthestMemberDistance);
        return allInPlace;
    }

    private void UpdateLinking()
    {
        if (_pendingLinks.Count == 0)
        {
            BuildLinkSequence();
        }

        if (_pendingLinks.Count == 0)
        {
            LogStateMessage("No valid survivor links to show; advancing to final charge delay.");
            SetState(SquadState.ChargeDelay, finalChargeDelay);
            return;
        }

        if (Time.time < _stateEndTime)
        {
            return;
        }

        if (_nextLinkIndex < _pendingLinks.Count)
        {
            LogStateMessage($"Spawning link {_nextLinkIndex + 1}/{_pendingLinks.Count}: {_pendingLinks[_nextLinkIndex].A.name} -> {_pendingLinks[_nextLinkIndex].B.name}.");
            SpawnLinkVisual(_pendingLinks[_nextLinkIndex].A, _pendingLinks[_nextLinkIndex].B);
            _nextLinkIndex++;
            _stateEndTime = Time.time + Mathf.Max(0.01f, linkStepDuration);
            return;
        }

        SetState(SquadState.ChargeDelay, finalChargeDelay);
    }

    private void UpdateFinalBeam(Entity3D target)
    {
        RefreshActiveMembers();
        if (_activeMembers.Count == 0)
        {
            StopFinalBeam();
            SetState(SquadState.Cooldown, attackCooldown);
            return;
        }

        Vector3 targetPoint = ResolveTargetPoint(target);
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive)
            {
                continue;
            }

            Vector3 aimDirection = targetPoint - member.transform.position;
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = member.transform.forward;
            }

            member._flightController?.SetFacingDirection(aimDirection);
        }

        if (!_beamActive)
        {
            _survivorCountAtBeamStart = Mathf.Clamp(_activeMembers.Count, 1, 3);
            LogStateMessage($"Starting final converged beam from {_survivorCountAtBeamStart} surviving member(s). Total DPS={ResolveDamagePerSecond():F1}, slowEnabled={_survivorCountAtBeamStart >= 3}.");
            StartFinalBeams(targetPoint);
            _stateEndTime = Time.time + Mathf.Max(0.01f, finalBeamDuration);
        }
        else
        {
            RefreshFinalBeamAims(targetPoint);
        }

        ApplyFinalBeamGameplay(targetPoint);

        if (Time.time >= _stateEndTime)
        {
            LogStateMessage("Final beam duration complete; entering cooldown.");
            StopFinalBeam();
            ClearLinkVisuals();
            SetState(SquadState.Cooldown, attackCooldown);
        }
    }

    private void StartFinalBeams(Vector3 targetPoint)
    {
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive || member.finalBeamWeapon == null)
            {
                continue;
            }

            Vector3 aimDirection = ResolveMemberAimDirection(member, targetPoint);
            if (NetTickUtil.IsActive && member.netEnemyCombat != null && member.netEnemyCombat.IsSpawned)
            {
                member.netEnemyCombat.SetBeamState(member.finalBeamWeapon, true, aimDirection);
            }
            else
            {
                member.finalBeamWeapon.ApplyNetworkBeamAim(aimDirection);
                member.finalBeamWeapon.ApplyNetworkBeamState(true, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
            }
        }

        _beamActive = true;
    }

    private void RefreshFinalBeamAims(Vector3 targetPoint)
    {
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive || member.finalBeamWeapon == null)
            {
                continue;
            }

            Vector3 aimDirection = ResolveMemberAimDirection(member, targetPoint);
            if (NetTickUtil.IsActive && member.netEnemyCombat != null && member.netEnemyCombat.IsSpawned)
            {
                member.netEnemyCombat.UpdateBeamAim(member.finalBeamWeapon, aimDirection);
            }
            else
            {
                member.finalBeamWeapon.ApplyNetworkBeamAim(aimDirection);
            }
        }
    }

    private void StopFinalBeam()
    {
        RefreshActiveMembers();
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || member.finalBeamWeapon == null)
            {
                continue;
            }

            if (NetTickUtil.IsActive && member.netEnemyCombat != null && member.netEnemyCombat.IsSpawned)
            {
                member.netEnemyCombat.SetBeamState(member.finalBeamWeapon, false, member.transform.forward);
            }
            else
            {
                member.finalBeamWeapon.ApplyNetworkBeamState(false, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
            }
        }

        _beamActive = false;
    }

    private void ApplyFinalBeamGameplay(Vector3 targetPoint)
    {
        float damagePerSource = ResolveDamagePerSecond() / Mathf.Max(1, _survivorCountAtBeamStart);
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D source = _activeMembers[i];
            if (!CanApplyGameplay(source))
            {
                continue;
            }

            Vector3 direction = ResolveMemberAimDirection(source, targetPoint);
            Vector3 origin = source.finalBeamWeapon != null ? source.finalBeamWeapon.GetBeamOrigin(direction) : source.transform.position;
            int hitCount = finalBeamHitscanRadius > 0.001f
                ? Physics.SphereCastNonAlloc(origin, finalBeamHitscanRadius, direction, BeamHits, finalBeamRange, finalBeamMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(origin, direction, BeamHits, finalBeamRange, finalBeamMask, QueryTriggerInteraction.Ignore);

            Entity3D nearestTarget = null;
            Vector3 nearestPoint = origin + direction * finalBeamRange;
            float nearestDistance = float.MaxValue;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = BeamHits[hitIndex];
                if (hit.collider == null || hit.collider.transform.IsChildOf(source.transform))
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                Entity3D candidate = ResolveHitEntity(hit.collider);
                if (candidate == null || FactionMember3D.ResolveFaction(candidate) != Faction3D.PlayerTeam)
                {
                    continue;
                }

                nearestTarget = candidate;
                nearestPoint = hit.point;
                nearestDistance = hit.distance;
            }

            if (nearestTarget == null)
            {
                continue;
            }

            nearestTarget.TakeDamage(damagePerSource * Time.deltaTime, nearestPoint, source._enemy, DamageSource3D.Beam);
            if (_survivorCountAtBeamStart >= 3)
            {
                nearestTarget.ApplySlow(fullTriadSlowMultiplier, fullTriadSlowDuration);
                if (fullTriadSlowEngineEmissionScale < 1f)
                {
                    nearestTarget.ThrusterVfx?.ApplyTemporaryEmissionRateScale(fullTriadSlowEngineEmissionScale, fullTriadSlowDuration);
                }
            }
        }
    }

    private float ResolveDamagePerSecond()
    {
        return _survivorCountAtBeamStart >= 3
            ? threeMemberDamagePerSecond
            : _survivorCountAtBeamStart == 2
                ? twoMemberDamagePerSecond
                : oneMemberDamagePerSecond;
    }

    private void BuildLinkSequence()
    {
        _pendingLinks.Clear();
        RefreshActiveMembers();
        if (_activeMembers.Count >= 2)
        {
            _pendingLinks.Add((_activeMembers[0], _activeMembers[1]));
        }

        if (_activeMembers.Count >= 3)
        {
            _pendingLinks.Add((_activeMembers[1], _activeMembers[2]));
            _pendingLinks.Add((_activeMembers[2], _activeMembers[0]));
        }

        _nextLinkIndex = 0;
        _stateEndTime = Time.time;
        LogStateMessage($"Built link sequence with {_pendingLinks.Count} link(s) for {_activeMembers.Count} surviving member(s).");
    }

    private void SpawnLinkVisual(TriumvirateEnemyBrain3D a, TriumvirateEnemyBrain3D b)
    {
        SpawnLocalLinkVisual(a, b);

        if (NetTickUtil.IsActive && IsSpawned && IsServer && a != null && b != null && a._networkObject != null && b._networkObject != null)
        {
            SpawnLinkVisualClientRpc(a._networkObject.NetworkObjectId, b._networkObject.NetworkObjectId);
        }
    }

    private void SpawnLocalLinkVisual(TriumvirateEnemyBrain3D a, TriumvirateEnemyBrain3D b)
    {
        if (linkLightningPrefab == null || a == null || b == null)
        {
            return;
        }

        GameObject linkObject = Instantiate(linkLightningPrefab, a.transform.position, Quaternion.identity);
        TriumvirateLightningLinkVisual3D link = linkObject.GetComponent<TriumvirateLightningLinkVisual3D>();
        if (link == null)
        {
            link = linkObject.AddComponent<TriumvirateLightningLinkVisual3D>();
        }

        link.Initialize(a.transform, b.transform, linkPointCount, linkAmplitude, linkJitterInterval);
        _activeLinks.Add(linkObject);
    }

    private void ClearLinkVisuals(bool replicateToClients = true)
    {
        ClearLocalLinkVisuals();
        if (replicateToClients && NetTickUtil.IsActive && IsSpawned && IsServer)
        {
            ClearLinkVisualsClientRpc();
        }
    }

    private void ClearLocalLinkVisuals()
    {
        for (int i = 0; i < _activeLinks.Count; i++)
        {
            if (_activeLinks[i] != null)
            {
                Destroy(_activeLinks[i]);
            }
        }

        _activeLinks.Clear();
        _pendingLinks.Clear();
        _nextLinkIndex = 0;
    }

    [ClientRpc]
    private void SpawnLinkVisualClientRpc(ulong startNetworkObjectId, ulong endNetworkObjectId)
    {
        if (IsServer)
        {
            return;
        }

        if (TryResolveNetworkBrain(startNetworkObjectId, out TriumvirateEnemyBrain3D start)
            && TryResolveNetworkBrain(endNetworkObjectId, out TriumvirateEnemyBrain3D end))
        {
            SpawnLocalLinkVisual(start, end);
        }
    }

    [ClientRpc]
    private void ClearLinkVisualsClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        ClearLocalLinkVisuals();
    }

    private Entity3D ResolveTarget()
    {
        return _targetSensor != null ? _targetSensor.GetTarget() : null;
    }

    private TriumvirateEnemyBrain3D ResolveBeamSource()
    {
        RefreshActiveMembers();
        return _activeMembers.Count > 0 ? _activeMembers[0] : null;
    }

    private void RefreshSquadMembers()
    {
        if (Time.time < _nextAutoLinkTime && squadMembers != null && squadMembers.Length >= 3)
        {
            return;
        }

        _nextAutoLinkTime = Time.time + Mathf.Max(0.05f, autoLinkRetryInterval);
        if (squadMembers != null && squadMembers.Length >= 3)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        TriumvirateEnemyBrain3D[] candidates = FindObjectsByType<TriumvirateEnemyBrain3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        TriumvirateEnemyBrain3D[] candidates = FindObjectsOfType<TriumvirateEnemyBrain3D>();
#endif
        List<TriumvirateEnemyBrain3D> nearest = new List<TriumvirateEnemyBrain3D>(3) { this };
        float radiusSqr = autoLinkRadius * autoLinkRadius;
        for (int i = 0; i < candidates.Length; i++)
        {
            TriumvirateEnemyBrain3D candidate = candidates[i];
            if (candidate == null || candidate == this || candidate.squadKey != squadKey || !candidate.IsAlive)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr <= radiusSqr)
            {
                nearest.Add(candidate);
            }
        }

        nearest.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        while (nearest.Count > 3)
        {
            nearest.RemoveAt(nearest.Count - 1);
        }

        squadMembers = nearest.ToArray();
    }

    private void RefreshActiveMembers()
    {
        _activeMembers.Clear();
        if (squadMembers == null || squadMembers.Length == 0)
        {
            _activeMembers.Add(this);
            return;
        }

        for (int i = 0; i < squadMembers.Length; i++)
        {
            TriumvirateEnemyBrain3D member = squadMembers[i];
            if (member != null && member.IsAlive && !_activeMembers.Contains(member))
            {
                _activeMembers.Add(member);
            }
        }

        _activeMembers.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }

    private void HandleSurvivorCountChanged()
    {
        if (_lastActiveMemberCount == _activeMembers.Count)
        {
            return;
        }

        _lastActiveMemberCount = _activeMembers.Count;
        LogStateMessage($"Survivor count changed to {_activeMembers.Count}.");
        if (_state == SquadState.Forming || _state == SquadState.Settling || _state == SquadState.Cooldown)
        {
            return;
        }

        StopFinalBeam();
        ClearLinkVisuals();
        LogStateMessage("Survivor count changed during attack sequence; restarting formation with remaining members.");
        SetState(SquadState.Forming, 0f);
    }

    private bool IsCoordinator()
    {
        RefreshActiveMembers();
        return _activeMembers.Count > 0 && _activeMembers[0] == this;
    }

    private void SetState(SquadState nextState, float duration)
    {
        if (_state != nextState)
        {
            LogStateMessage($"State {_state} -> {nextState}. Duration={duration:F2}s, survivors={_activeMembers.Count}.");
            if (nextState == SquadState.Forming || nextState == SquadState.Cooldown)
            {
                ClearLinkVisuals();
            }

            if (nextState == SquadState.Linking)
            {
                BuildLinkSequence();
            }
        }

        _state = nextState;
        _stateEndTime = Time.time + Mathf.Max(0f, duration);
    }

    private void AdvanceAfterTimer(SquadState nextState)
    {
        if (Time.time >= _stateEndTime)
        {
            SetState(nextState, 0f);
        }
    }

    private Vector3 ResolveMemberAimDirection(TriumvirateEnemyBrain3D member, Vector3 targetPoint)
    {
        if (member == null)
        {
            return transform.forward;
        }

        Vector3 direction = targetPoint - member.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return member.transform.forward.sqrMagnitude > 0.0001f ? member.transform.forward.normalized : Vector3.forward;
    }

    private void LogFormationProgressIfNeeded(float farthestMemberDistance)
    {
        if (!logFormationProgress || Time.time < _nextFormationProgressLogTime)
        {
            return;
        }

        _nextFormationProgressLogTime = Time.time + Mathf.Max(0.05f, formationProgressLogInterval);
        LogStateMessage($"Forming triangle: farthest member is {farthestMemberDistance:F1}m from slot; tolerance={formationTolerance:F1}m.");
    }

    private void LogStateMessage(string message)
    {
        if (!logStateChanges)
        {
            return;
        }

        Debug.Log($"[{nameof(TriumvirateEnemyBrain3D)}] {name}: {message}", this);
    }

    private Vector3 ResolveActiveSquadCenter()
    {
        RefreshActiveMembers();
        if (_activeMembers.Count == 0)
        {
            return transform.position;
        }

        Vector3 center = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive)
            {
                continue;
            }

            center += member.transform.position;
            count++;
        }

        return count > 0 ? center / count : transform.position;
    }

    private void EnsureFormationSlotAssignments()
    {
        for (int i = 0; i < FormationSlotCount; i++)
        {
            _claimedFormationSlots[i] = false;
        }

        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive)
            {
                continue;
            }

            int explicitSlot = member.ResolveExplicitFormationSlotIndex();
            if (explicitSlot >= 0 && TryClaimFormationSlot(explicitSlot))
            {
                member._assignedFormationSlot = explicitSlot;
                member._loggedDuplicateFormationSlot = false;
            }
            else if (explicitSlot >= 0)
            {
                member._assignedFormationSlot = -1;
                if (!member._loggedDuplicateFormationSlot)
                {
                    member._loggedDuplicateFormationSlot = true;
                    LogStateMessage($"{member.name} has a duplicate explicit Triumvirate formation slot; assigning an open slot instead.");
                }
            }
        }

        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive || member.ResolveExplicitFormationSlotIndex() >= 0)
            {
                continue;
            }

            if (TryClaimFormationSlot(member._assignedFormationSlot))
            {
                continue;
            }

            member._assignedFormationSlot = -1;
        }

        for (int i = 0; i < _activeMembers.Count; i++)
        {
            TriumvirateEnemyBrain3D member = _activeMembers[i];
            if (member == null || !member.IsAlive || member._assignedFormationSlot >= 0)
            {
                continue;
            }

            member._assignedFormationSlot = ClaimFirstOpenFormationSlot();
        }
    }

    private bool TryClaimFormationSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= FormationSlotCount || _claimedFormationSlots[slotIndex])
        {
            return false;
        }

        _claimedFormationSlots[slotIndex] = true;
        return true;
    }

    private int ClaimFirstOpenFormationSlot()
    {
        for (int i = 0; i < FormationSlotCount; i++)
        {
            if (TryClaimFormationSlot(i))
            {
                return i;
            }
        }

        return TopSlotIndex;
    }

    private int ResolveExplicitFormationSlotIndex()
    {
        switch (formationSlotPreference)
        {
            case FormationSlotPreference.Top:
                return TopSlotIndex;
            case FormationSlotPreference.LowerLeft:
                return LowerLeftSlotIndex;
            case FormationSlotPreference.LowerRight:
                return LowerRightSlotIndex;
            default:
                return -1;
        }
    }

    private static int ResolveMemberFormationSlot(TriumvirateEnemyBrain3D member, int fallbackIndex)
    {
        if (member != null && member._assignedFormationSlot >= 0)
        {
            return member._assignedFormationSlot;
        }

        return Mathf.Clamp(fallbackIndex, 0, FormationSlotCount - 1);
    }

    private void ClearSquadFlightIntent()
    {
        RefreshActiveMembers();
        for (int i = 0; i < _activeMembers.Count; i++)
        {
            _activeMembers[i]?._flightController?.ClearFlightIntent();
        }
    }

    private bool CanApplyGameplay(TriumvirateEnemyBrain3D source)
    {
        if (source == null || !source.IsAlive)
        {
            return false;
        }

        return !NetTickUtil.IsActive || NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
    }

    private static bool TryResolveNetworkBrain(ulong networkObjectId, out TriumvirateEnemyBrain3D brain)
    {
        brain = null;
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
        {
            return false;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject)
            || networkObject == null)
        {
            return false;
        }

        brain = networkObject.GetComponent<TriumvirateEnemyBrain3D>();
        return brain != null;
    }

    private bool HasBrainAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_networkObject == null || !_networkObject.IsSpawned)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
    }

    private static Vector3 ResolveFormationSlot(Vector3 center, Vector3 right, Vector3 up, int index, int count, float radius,
        bool planarFormation, float verticalWidth, float verticalHeight)
    {
        if (count <= 1)
        {
            return center;
        }

        if (planarFormation)
        {
            float angle = (Mathf.PI * 2f * index) / count;
            Vector3 forwardOnPlane = Vector3.Cross(right, Vector3.up);
            if (forwardOnPlane.sqrMagnitude <= 0.0001f)
            {
                forwardOnPlane = Vector3.forward;
            }

            return center + ((right * Mathf.Cos(angle)) + (forwardOnPlane.normalized * Mathf.Sin(angle))) * Mathf.Max(0f, radius);
        }

        float resolvedWidth = Mathf.Max(0f, verticalWidth);
        float resolvedHeight = Mathf.Max(0f, verticalHeight);
        if (count == 2)
        {
            return center + right * (index == 0 ? -resolvedWidth * 0.5f : resolvedWidth * 0.5f);
        }

        float upperOffset = resolvedHeight * (2f / 3f);
        float lowerOffset = resolvedHeight * (1f / 3f);
        if (index == 0)
        {
            return center + up * upperOffset;
        }

        float lowerSide = index == 1 ? -1f : 1f;
        return center + (right * (lowerSide * resolvedWidth * 0.5f)) - (up * lowerOffset);
    }

    private static Entity3D ResolveHitEntity(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            Entity3D rigidbodyEntity = hitCollider.attachedRigidbody.GetComponent<Entity3D>();
            if (rigidbodyEntity != null)
            {
                return rigidbodyEntity;
            }
        }

        return hitCollider.GetComponentInParent<Entity3D>();
    }

    private static Vector3 ResolveTargetPoint(Entity3D target)
    {
        Collider targetCollider = target != null ? target.GetComponentInChildren<Collider>() : null;
        return targetCollider != null ? targetCollider.bounds.center : target != null ? target.transform.position : Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, autoLinkRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, formationDistanceFromTarget);
    }
}
