using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-220)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class PortalBossSpawn3D : NetworkBehaviour
{
    public event Action<PortalBossSpawn3D> SequenceCompleted;

    [Header("Portal Spawn")]
    [Tooltip("Portal visual prefab spawned at the boss's authored final spawn point. This should usually be Assets/Prefabs/3d_effects/Portal3D.prefab.")]
    [SerializeField] private GameObject portalPrefab;

    [Tooltip("Uniform scale multiplier applied to the spawned portal root so the portal can be sized per boss without editing the shared prefab.")]
    [Min(0.01f)]
    [SerializeField] private float portalUniformScale = 1f;

    [Tooltip("How far behind the authored spawn point the boss begins before drifting forward out of the portal, measured along the boss's forward direction.")]
    [Min(0f)]
    [SerializeField] private float emergeDistance = 80f;

    [Tooltip("Seconds the boss spends moving from its hidden start position to its authored final spawn point.")]
    [Min(0.01f)]
    [SerializeField] private float emergeDuration = 3f;

    [Tooltip("Seconds the portal spends shrinking away after the boss has fully cleared it.")]
    [Min(0.01f)]
    [SerializeField] private float portalShrinkDuration = 0.75f;

    [Header("Optional Overrides")]
    [Tooltip("Optional renderer override used only for validation/debugging. Leave empty to auto-collect child renderers under this boss.")]
    [SerializeField] private Renderer[] visualRenderers;

    [Tooltip("Optional extra behaviours to disable during the portal intro if this prefab has custom gameplay scripts beyond the standard boss brain/movement/weapon stack.")]
    [SerializeField] private Behaviour[] additionalBehavioursToDisable;

    [Tooltip("Optional collider override for the portal intro. Leave empty to auto-disable every child collider so the boss is not hittable before it fully exits.")]
    [SerializeField] private Collider[] collidersToDisable;

    private Rigidbody _rb;
    private Renderer[] _resolvedVisualRenderers;
    private Behaviour[] _resolvedDisabledBehaviours;
    private bool[] _resolvedDisabledBehaviourStates;
    private Collider[] _resolvedColliders;
    private bool[] _resolvedColliderStates;
    private bool _resolvedOriginalKinematic;
    private bool _originalKinematic;
    private bool _introStateApplied;
    private bool _sequencePrepared;
    private bool _sequenceStarted;
    private bool _sequenceCompleted;
    private double _sequenceStartTime;
    private Vector3 _finalPosition;
    private Quaternion _finalRotation;
    private Vector3 _portalPosition;
    private Vector3 _startPosition;
    private GameObject _portalInstance;
    private Coroutine _portalShrinkRoutine;

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        if (!NetTickUtil.IsActive)
        {
            StartOfflineSequence();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();

        if (!NetTickUtil.IsActive || _sequenceCompleted || !CanPlayPortalSequence())
        {
            return;
        }

        PrepareSequence();

        if (!IsServer)
        {
            return;
        }

        double serverStartTime = ResolveSequenceTime();
        StartPreparedSequence(serverStartTime);
        StartPortalSequenceClientRpc(serverStartTime);
    }

    public override void OnNetworkDespawn()
    {
        CleanupPortalInstance();
        RestoreIntroState();
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!_sequenceStarted || _sequenceCompleted)
        {
            return;
        }

        if (_rb != null && !_rb.isKinematic)
        {
            _rb.isKinematic = true;
        }

        float duration = Mathf.Max(0.01f, emergeDuration);
        float elapsed = Mathf.Max(0f, (float)(ResolveSequenceTime() - _sequenceStartTime));
        float t = Mathf.Clamp01(elapsed / duration);
        transform.SetPositionAndRotation(Vector3.Lerp(_startPosition, _finalPosition, t), _finalRotation);

        if (_rb != null)
        {
            _rb.position = transform.position;
            _rb.rotation = transform.rotation;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        if (t >= 1f)
        {
            CompleteSequence();
        }
    }

    private void OnDisable()
    {
        if (!_sequenceCompleted)
        {
            RestoreIntroState();
        }

        CleanupPortalInstance();
    }

    private void OnDestroy()
    {
        CleanupPortalInstance();
    }

    private void StartOfflineSequence()
    {
        if (_sequenceCompleted || !CanPlayPortalSequence())
        {
            return;
        }

        PrepareSequence();
        StartPreparedSequence(ResolveSequenceTime());
    }

    [ClientRpc]
    private void StartPortalSequenceClientRpc(double serverStartTime)
    {
        if (IsServer)
        {
            return;
        }

        if (!CanPlayPortalSequence())
        {
            return;
        }

        PrepareSequence();
        StartPreparedSequence(serverStartTime);
    }

    private bool CanPlayPortalSequence()
    {
        if (_sequenceCompleted)
        {
            return false;
        }

        if (portalPrefab == null)
        {
            Debug.LogWarning($"[{nameof(PortalBossSpawn3D)}] {name} is missing Portal Prefab, so the boss portal intro was skipped.", this);
            return false;
        }

        if (emergeDistance <= 0f || emergeDuration <= 0f)
        {
            Debug.LogWarning($"[{nameof(PortalBossSpawn3D)}] {name} has a non-positive emerge distance or duration, so the boss portal intro was skipped.", this);
            return false;
        }

        return true;
    }

    private void PrepareSequence()
    {
        if (_sequencePrepared)
        {
            return;
        }

        _sequencePrepared = true;
        _finalPosition = transform.position;
        _finalRotation = transform.rotation;
        _resolvedVisualRenderers = ResolveVisualRenderers();
        if (_resolvedVisualRenderers.Length == 0)
        {
            Debug.LogWarning($"[{nameof(PortalBossSpawn3D)}] {name} could not find any child renderers for the boss portal intro. The motion will still run, but validate the prefab visuals.", this);
        }

        Vector3 forward = _finalRotation * Vector3.forward;
        float rearClearDistance = ResolveRearClearDistance(forward);
        _portalPosition = _finalPosition - (forward * rearClearDistance);
        _startPosition = _portalPosition - (forward * emergeDistance);

        ResolveControlledBehaviours();
        ResolveControlledColliders();
        ApplyIntroState();
        SpawnPortalInstance();
        transform.SetPositionAndRotation(_startPosition, _finalRotation);

        if (_rb != null)
        {
            _rb.position = _startPosition;
            _rb.rotation = _finalRotation;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private void StartPreparedSequence(double sequenceStartTime)
    {
        if (_sequenceStarted)
        {
            return;
        }

        _sequenceStarted = true;
        _sequenceStartTime = sequenceStartTime;

        if (_rb != null)
        {
            _rb.isKinematic = true;
        }

        float duration = Mathf.Max(0.01f, emergeDuration);
        float elapsed = Mathf.Max(0f, (float)(ResolveSequenceTime() - _sequenceStartTime));
        if (elapsed >= duration)
        {
            CompleteSequence();
        }
    }

    private void CompleteSequence()
    {
        if (_sequenceCompleted)
        {
            return;
        }

        _sequenceCompleted = true;
        _sequenceStarted = false;
        transform.SetPositionAndRotation(_finalPosition, _finalRotation);

        if (_rb != null)
        {
            _rb.position = _finalPosition;
            _rb.rotation = _finalRotation;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        RestoreIntroState();
        StartPortalShrink();
        SequenceCompleted?.Invoke(this);
    }

    private void ApplyIntroState()
    {
        if (_introStateApplied)
        {
            return;
        }

        _introStateApplied = true;
        CacheOriginalKinematicState();

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        for (int i = 0; i < _resolvedDisabledBehaviours.Length; i++)
        {
            Behaviour behaviour = _resolvedDisabledBehaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            _resolvedDisabledBehaviourStates[i] = behaviour.enabled;
            behaviour.enabled = false;
        }

        for (int i = 0; i < _resolvedColliders.Length; i++)
        {
            Collider col = _resolvedColliders[i];
            if (col == null)
            {
                continue;
            }

            _resolvedColliderStates[i] = col.enabled;
            col.enabled = false;
        }
    }

    private void RestoreIntroState()
    {
        if (!_introStateApplied)
        {
            return;
        }

        _introStateApplied = false;

        if (_rb != null)
        {
            _rb.isKinematic = ResolveRestoreKinematicState();
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        for (int i = 0; i < _resolvedDisabledBehaviours.Length; i++)
        {
            Behaviour behaviour = _resolvedDisabledBehaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = _resolvedDisabledBehaviourStates[i];
            }
        }

        for (int i = 0; i < _resolvedColliders.Length; i++)
        {
            Collider col = _resolvedColliders[i];
            if (col != null)
            {
                col.enabled = _resolvedColliderStates[i];
            }
        }
    }

    private void ResolveControlledBehaviours()
    {
        if (_resolvedDisabledBehaviours != null)
        {
            return;
        }

        List<Behaviour> resolved = new List<Behaviour>(16);
        Behaviour[] rootBehaviours = GetComponents<Behaviour>();
        for (int i = 0; i < rootBehaviours.Length; i++)
        {
            Behaviour behaviour = rootBehaviours[i];
            if (ShouldDisableBehaviourForIntro(behaviour))
            {
                resolved.Add(behaviour);
            }
        }

        if (additionalBehavioursToDisable != null)
        {
            for (int i = 0; i < additionalBehavioursToDisable.Length; i++)
            {
                Behaviour behaviour = additionalBehavioursToDisable[i];
                if (behaviour != null && behaviour != this && !resolved.Contains(behaviour))
                {
                    resolved.Add(behaviour);
                }
            }
        }

        _resolvedDisabledBehaviours = resolved.ToArray();
        _resolvedDisabledBehaviourStates = new bool[_resolvedDisabledBehaviours.Length];
    }

    private bool ShouldDisableBehaviourForIntro(Behaviour behaviour)
    {
        if (behaviour == null || behaviour == this || behaviour is NetworkObject)
        {
            return false;
        }

        if (behaviour is EnemyAIFlightController3D
            || behaviour is EnemyTargetSensor3D
            || behaviour is EnemyPatrol3D
            || behaviour is NetEnemyMovement3D
            || behaviour is NetEnemyCombat3D
            || behaviour is EnemyProjectileWeaponBase3D
            || behaviour is BeamWeapon3D
            || behaviour is EnemySpawnerWeapon3D)
        {
            return true;
        }

        return behaviour.GetType().Name.EndsWith("EnemyBrain3D", StringComparison.Ordinal);
    }

    private void ResolveControlledColliders()
    {
        if (_resolvedColliders != null)
        {
            return;
        }

        _resolvedColliders = collidersToDisable != null && collidersToDisable.Length > 0
            ? collidersToDisable
            : GetComponentsInChildren<Collider>(true);
        _resolvedColliderStates = new bool[_resolvedColliders.Length];
    }

    private Renderer[] ResolveVisualRenderers()
    {
        if (visualRenderers != null && visualRenderers.Length > 0)
        {
            return visualRenderers;
        }

        return GetComponentsInChildren<Renderer>(true);
    }

    private void SpawnPortalInstance()
    {
        if (_portalInstance != null || portalPrefab == null)
        {
            return;
        }

        _portalInstance = Instantiate(portalPrefab, _portalPosition, _finalRotation);
        _portalInstance.transform.localScale = Vector3.one * Mathf.Max(0.01f, portalUniformScale);
    }

    private float ResolveRearClearDistance(Vector3 forward)
    {
        if (_resolvedVisualRenderers == null || _resolvedVisualRenderers.Length == 0)
        {
            return 0f;
        }

        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        bool hasBounds = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < _resolvedVisualRenderers.Length; i++)
        {
            Renderer renderer = _resolvedVisualRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return 0f;
        }

        Vector3 relativeMin = combinedBounds.min - _finalPosition;
        Vector3 relativeMax = combinedBounds.max - _finalPosition;
        Vector3[] corners =
        {
            new Vector3(relativeMin.x, relativeMin.y, relativeMin.z),
            new Vector3(relativeMin.x, relativeMin.y, relativeMax.z),
            new Vector3(relativeMin.x, relativeMax.y, relativeMin.z),
            new Vector3(relativeMin.x, relativeMax.y, relativeMax.z),
            new Vector3(relativeMax.x, relativeMin.y, relativeMin.z),
            new Vector3(relativeMax.x, relativeMin.y, relativeMax.z),
            new Vector3(relativeMax.x, relativeMax.y, relativeMin.z),
            new Vector3(relativeMax.x, relativeMax.y, relativeMax.z)
        };

        float farthestBehind = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            float behindDistance = Vector3.Dot(-forward, corners[i]);
            if (behindDistance > farthestBehind)
            {
                farthestBehind = behindDistance;
            }
        }

        return Mathf.Max(0f, farthestBehind);
    }

    private void StartPortalShrink()
    {
        if (_portalInstance == null)
        {
            return;
        }

        if (_portalShrinkRoutine != null)
        {
            StopCoroutine(_portalShrinkRoutine);
        }

        _portalShrinkRoutine = StartCoroutine(ShrinkPortalRoutine());
    }

    private IEnumerator ShrinkPortalRoutine()
    {
        if (_portalInstance == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, portalShrinkDuration);
        float elapsed = 0f;
        Transform portalTransform = _portalInstance.transform;
        Vector3 initialScale = portalTransform.localScale;

        while (portalTransform != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            portalTransform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
            yield return null;
        }

        CleanupPortalInstance();
    }

    private void CleanupPortalInstance()
    {
        if (_portalShrinkRoutine != null)
        {
            StopCoroutine(_portalShrinkRoutine);
            _portalShrinkRoutine = null;
        }

        if (_portalInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_portalInstance);
        }
        else
        {
            DestroyImmediate(_portalInstance);
        }

        _portalInstance = null;
    }

    private void CacheReferences()
    {
        _rb ??= GetComponent<Rigidbody>();
    }

    private void CacheOriginalKinematicState()
    {
        if (_resolvedOriginalKinematic || _rb == null)
        {
            return;
        }

        _originalKinematic = _rb.isKinematic;
        _resolvedOriginalKinematic = true;
    }

    private bool ResolveRestoreKinematicState()
    {
        if (NetTickUtil.IsActive && !IsServer)
        {
            return true;
        }

        return _resolvedOriginalKinematic ? _originalKinematic : (_rb != null && _rb.isKinematic);
    }

    private double ResolveSequenceTime()
    {
        if (NetTickUtil.IsActive && NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.ServerTime.Time;
        }

        return Time.timeAsDouble;
    }
}
