using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum PrimaryFireExecutionSource : byte
{
    PlayerInput = 0,
    TwinFire = 1,
    MindBinding = 2,
    AugmentOther = 3
}

public static class PrimaryFireExecutionBus
{
    public static event Action<Player, PrimaryFireExecutionSource> PrimaryFireExecuted;

    public static void Raise(Player shooter, PrimaryFireExecutionSource source)
    {
        if (shooter == null)
        {
            return;
        }

        PrimaryFireExecuted?.Invoke(shooter, source);
    }
}

public interface IProjectileReflectorAugment
{
    bool TryReflectProjectile(ProjectileScript projectile, Vector2 hitPoint);
}

[Serializable]
public class BindingLinkVisualSettings
{
    [Tooltip("Enable or disable binding link arcs")]
    public bool enabled = true;

    [Tooltip("Material used by each binding arc line renderer")]
    public Material lineMaterial;

    [Tooltip("Arc color at owner side")]
    public Color startColor = new Color(0.4f, 0.9f, 1f, 0.9f);

    [Tooltip("Arc color at target side")]
    public Color endColor = new Color(1f, 0.7f, 1f, 0.9f);

    [Tooltip("Base width of the arc line")]
    public float lineWidth = 0.1f;

    [Tooltip("How much the arc bows outward in world units")]
    public float arcHeight = 0.8f;

    [Tooltip("Optional per-link wobble amount")]
    public float wobbleAmplitude = 0.05f;

    [Tooltip("Wobble speed in cycles per second")]
    public float wobbleFrequency = 6f;

    [Tooltip("Owner-local offset for where the arc starts")]
    public Vector3 ownerLocalOffset = new Vector3(0f, 0.4f, 0f);

    [Tooltip("Target-local offset for where the arc ends")]
    public Vector3 targetLocalOffset = new Vector3(0f, 0.4f, 0f);

    [Tooltip("Fade alpha with distance")]
    public bool fadeByDistance = true;

    [Tooltip("Alpha multiplier at point-blank")]
    public float nearAlpha = 1f;

    [Tooltip("Alpha multiplier at max range")]
    public float farAlpha = 0.2f;

    [Tooltip("Line renderer sorting order")]
    public int sortingOrder = 20;

    [Header("Optional LightningBolt Asset")]
    [Tooltip("Optional prefab from the LightningBolt asset pack. If assigned and enabled, this is used instead of the built-in line arc renderer")]
    public GameObject lightningBoltPrefab;

    [Tooltip("Use the LightningBolt prefab mode when a compatible prefab is assigned")]
    public bool useLightningBoltPrefab = true;

    [Tooltip("Apply these values to the instantiated LightningBoltScript to control flicker intensity")]
    public bool overrideLightningBoltSettings = false;

    [Range(0, 8)]
    [Tooltip("Lower generations = less busy/noisy bolt")]
    public int lightningGenerations = 6;

    [Range(0.01f, 0.25f)]
    [Tooltip("How long each generated bolt frame persists")]
    public float lightningDuration = 0.05f;

    [Range(0f, 1f)]
    [Tooltip("Lower chaos = steadier line shape")]
    public float lightningChaosFactor = 0.15f;

    [Range(1, 64)]
    [Tooltip("Texture rows for animated lightning sheets. Set to 1 for static frame")]
    public int lightningRows = 1;

    [Range(1, 64)]
    [Tooltip("Texture columns for animated lightning sheets. Set to 1 for static frame")]
    public int lightningColumns = 1;

    [Tooltip("0=None, 1=Random, 2=Loop, 3=PingPong. LightningBolt default is PingPong (3)")]
    [Range(0, 3)]
    public int lightningAnimationMode = 3;
}

public sealed class BindingLinkVisualController : MonoBehaviour
{
    private sealed class LinkState
    {
        public Entity target;
        public LineRenderer line;
        public float seed;
        public GameObject boltRoot;
        public LineRenderer boltLine;
        public Transform boltStartAnchor;
        public Transform boltEndAnchor;
        public Component boltScript;
    }

    private readonly Dictionary<int, LinkState> _links = new Dictionary<int, LinkState>();
    private readonly List<int> _staleIds = new List<int>();

    private Player _owner;
    private BindingLinkVisualSettings _settings;
    private string _sourceId;
    private static readonly Type LightningBoltType = Type.GetType("DigitalRuby.LightningBolt.LightningBoltScript, Assembly-CSharp");
    private static readonly FieldInfo StartObjectField = LightningBoltType != null ? LightningBoltType.GetField("StartObject") : null;
    private static readonly FieldInfo EndObjectField = LightningBoltType != null ? LightningBoltType.GetField("EndObject") : null;
    private static readonly FieldInfo ManualModeField = LightningBoltType != null ? LightningBoltType.GetField("ManualMode") : null;
    private static readonly FieldInfo GenerationsField = LightningBoltType != null ? LightningBoltType.GetField("Generations") : null;
    private static readonly FieldInfo DurationField = LightningBoltType != null ? LightningBoltType.GetField("Duration") : null;
    private static readonly FieldInfo ChaosFactorField = LightningBoltType != null ? LightningBoltType.GetField("ChaosFactor") : null;
    private static readonly FieldInfo RowsField = LightningBoltType != null ? LightningBoltType.GetField("Rows") : null;
    private static readonly FieldInfo ColumnsField = LightningBoltType != null ? LightningBoltType.GetField("Columns") : null;
    private static readonly FieldInfo AnimationModeField = LightningBoltType != null ? LightningBoltType.GetField("AnimationMode") : null;

    public void Initialize(Player owner, BindingLinkVisualSettings settings, string sourceId)
    {
        _owner = owner;
        _settings = settings;
        _sourceId = sourceId;
    }

    public void SetTargets(List<Entity> targets, float maxDistance)
    {
        if (_owner == null || _settings == null || !_settings.enabled)
        {
            HideAll();
            return;
        }

        float safeDistance = Mathf.Max(0.001f, maxDistance);

        _staleIds.Clear();
        foreach (KeyValuePair<int, LinkState> pair in _links)
        {
            _staleIds.Add(pair.Key);
        }

        if (targets == null)
        {
            targets = new List<Entity>();
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Entity target = targets[i];
            if (target == null)
            {
                continue;
            }

            int targetId = target.GetInstanceID();
            LinkState state = GetOrCreateLink(targetId, target);
            state.target = target;
            UpdateLink(state, safeDistance);
            _staleIds.Remove(targetId);
        }

        for (int i = 0; i < _staleIds.Count; i++)
        {
            RemoveLink(_staleIds[i]);
        }
    }

    public void HideAll()
    {
        foreach (KeyValuePair<int, LinkState> pair in _links)
        {
            SetLinkVisible(pair.Value, false);
        }
    }

    private LinkState GetOrCreateLink(int targetId, Entity target)
    {
        if (_links.TryGetValue(targetId, out LinkState existing))
        {
            return existing;
        }

        LinkState created = new LinkState
        {
            target = target,
            seed = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI)
        };

        if (ShouldUseLightningBoltPrefab())
        {
            ConfigureLightningPrefabMode(created, targetId);
        }
        else
        {
            ConfigureLineRendererMode(created, targetId);
        }

        _links[targetId] = created;
        return created;
    }

    private bool ShouldUseLightningBoltPrefab()
    {
        return _settings != null &&
               _settings.useLightningBoltPrefab &&
               _settings.lightningBoltPrefab != null &&
               LightningBoltType != null;
    }

    private void ConfigureLineRendererMode(LinkState state, int targetId)
    {
        GameObject go = new GameObject($"BindingArc_{_sourceId}_{targetId}");
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 3;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = _settings.sortingOrder;
        line.material = _settings.lineMaterial != null ? _settings.lineMaterial : new Material(Shader.Find("Sprites/Default"));
        state.line = line;
    }

    private void ConfigureLightningPrefabMode(LinkState state, int targetId)
    {
        GameObject bolt = Instantiate(_settings.lightningBoltPrefab, transform);
        bolt.name = $"BindingBolt_{_sourceId}_{targetId}";
        bolt.transform.localPosition = Vector3.zero;
        bolt.transform.localRotation = Quaternion.identity;

        Transform startAnchor = new GameObject("BindingBoltStart").transform;
        startAnchor.SetParent(bolt.transform, false);

        Transform endAnchor = new GameObject("BindingBoltEnd").transform;
        endAnchor.SetParent(bolt.transform, false);

        Component boltScript = bolt.GetComponent(LightningBoltType);
        if (boltScript != null)
        {
            StartObjectField?.SetValue(boltScript, startAnchor.gameObject);
            EndObjectField?.SetValue(boltScript, endAnchor.gameObject);
            ManualModeField?.SetValue(boltScript, false);

            if (_settings.overrideLightningBoltSettings)
            {
                GenerationsField?.SetValue(boltScript, Mathf.Clamp(_settings.lightningGenerations, 0, 8));
                DurationField?.SetValue(boltScript, Mathf.Clamp(_settings.lightningDuration, 0.01f, 0.25f));
                ChaosFactorField?.SetValue(boltScript, Mathf.Clamp01(_settings.lightningChaosFactor));
                RowsField?.SetValue(boltScript, Mathf.Clamp(_settings.lightningRows, 1, 64));
                ColumnsField?.SetValue(boltScript, Mathf.Clamp(_settings.lightningColumns, 1, 64));
                AnimationModeField?.SetValue(boltScript, _settings.lightningAnimationMode);
            }
        }

        state.boltRoot = bolt;
        state.boltLine = bolt.GetComponent<LineRenderer>();
        state.boltStartAnchor = startAnchor;
        state.boltEndAnchor = endAnchor;
        state.boltScript = boltScript;
    }

    private void UpdateLink(LinkState state, float maxDistance)
    {
        if (state == null || state.target == null || _owner == null)
        {
            return;
        }

        Vector3 ownerStart = _owner.transform.TransformPoint(_settings.ownerLocalOffset);
        Vector3 targetEnd = state.target.transform.TransformPoint(_settings.targetLocalOffset);
        Vector3 delta = targetEnd - ownerStart;
        float distance = delta.magnitude;

        if (distance <= 0.001f)
        {
            SetLinkVisible(state, false);
            return;
        }

        float distance01 = Mathf.Clamp01(distance / maxDistance);
        float alphaMultiplier = _settings.fadeByDistance
            ? Mathf.Lerp(Mathf.Max(0f, _settings.nearAlpha), Mathf.Max(0f, _settings.farAlpha), distance01)
            : 1f;

        Vector3 normal = new Vector3(-delta.y, delta.x, 0f).normalized;
        float wobble = Mathf.Sin((Time.time * Mathf.Max(0f, _settings.wobbleFrequency)) + state.seed) * Mathf.Max(0f, _settings.wobbleAmplitude);
        Vector3 mid = (ownerStart + targetEnd) * 0.5f + normal * (Mathf.Max(0f, _settings.arcHeight) + wobble);

        if (state.line != null)
        {
            float width = Mathf.Max(0.005f, _settings.lineWidth * Mathf.Max(0.01f, _owner.ShipSize));
            state.line.startWidth = width;
            state.line.endWidth = width;

            Color start = _settings.startColor;
            Color end = _settings.endColor;
            start.a *= alphaMultiplier;
            end.a *= alphaMultiplier;
            state.line.startColor = start;
            state.line.endColor = end;

            state.line.SetPosition(0, ownerStart);
            state.line.SetPosition(1, mid);
            state.line.SetPosition(2, targetEnd);
            state.line.enabled = true;
            return;
        }

        if (state.boltRoot != null)
        {
            if (state.boltStartAnchor != null)
            {
                state.boltStartAnchor.position = ownerStart;
            }

            if (state.boltEndAnchor != null)
            {
                state.boltEndAnchor.position = targetEnd;
            }

            if (state.boltLine != null)
            {
                float width = Mathf.Max(0.005f, _settings.lineWidth * Mathf.Max(0.01f, _owner.ShipSize));
                state.boltLine.startWidth = width;
                state.boltLine.endWidth = width;
                state.boltLine.sortingOrder = _settings.sortingOrder;

                Color start = _settings.startColor;
                Color end = _settings.endColor;
                start.a *= alphaMultiplier;
                end.a *= alphaMultiplier;
                state.boltLine.startColor = start;
                state.boltLine.endColor = end;
            }

            state.boltRoot.SetActive(true);
        }
    }

    private void SetLinkVisible(LinkState state, bool visible)
    {
        if (state == null)
        {
            return;
        }

        if (state.line != null)
        {
            state.line.enabled = visible;
        }

        if (state.boltRoot != null)
        {
            state.boltRoot.SetActive(visible);
        }
    }

    private void RemoveLink(int targetId)
    {
        if (!_links.TryGetValue(targetId, out LinkState state))
        {
            return;
        }

        _links.Remove(targetId);
        if (state.line != null)
        {
            Destroy(state.line.gameObject);
        }

        if (state.boltRoot != null)
        {
            Destroy(state.boltRoot);
        }
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<int, LinkState> pair in _links)
        {
            if (pair.Value.line != null)
            {
                Destroy(pair.Value.line.gameObject);
            }

            if (pair.Value.boltRoot != null)
            {
                Destroy(pair.Value.boltRoot);
            }
        }

        _links.Clear();
        _staleIds.Clear();
    }
}

public sealed class WeakmakerExposureTracker : MonoBehaviour
{
    private struct ExposureState
    {
        public float multiplier;
        public float expiresAt;
    }

    private readonly Dictionary<string, ExposureState> _statesBySource = new Dictionary<string, ExposureState>();

    public void ApplyExposure(string sourceId, float multiplier, float duration)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return;

        _statesBySource[sourceId] = new ExposureState
        {
            multiplier = Mathf.Max(1f, multiplier),
            expiresAt = Time.time + Mathf.Max(0.01f, duration)
        };
    }

    public float GetCombinedMultiplier()
    {
        PruneExpired();

        float total = 1f;
        foreach (ExposureState state in _statesBySource.Values)
        {
            total *= state.multiplier;
        }

        return total;
    }

    private void LateUpdate()
    {
        PruneExpired();
    }

    private void PruneExpired()
    {
        if (_statesBySource.Count == 0) return;

        List<string> expired = null;
        foreach (KeyValuePair<string, ExposureState> entry in _statesBySource)
        {
            if (Time.time > entry.Value.expiresAt)
            {
                expired ??= new List<string>();
                expired.Add(entry.Key);
            }
        }

        if (expired == null) return;
        foreach (string key in expired)
        {
            _statesBySource.Remove(key);
        }
    }
}

public sealed class BurnerDebuffController : MonoBehaviour
{
    private sealed class BurnState
    {
        public Player owner;
        public float damagePerSecond;
        public float tickInterval;
        public float nextTickAt;
        public float expiresAt;
        public GameObject tickEffectPrefab;
        public float tickEffectRandomRadius;
    }

    private readonly Dictionary<string, BurnState> _activeBurns = new Dictionary<string, BurnState>();
    private Entity _target;
    private NetMovement _targetNetMovement;

    private void Awake()
    {
        _target = GetComponent<Entity>();
        _targetNetMovement = GetComponent<NetMovement>();
    }

    public void ApplyBurn(string sourceId, Player owner, float dps, float duration, float tickInterval, GameObject tickEffectPrefab, float tickEffectRandomRadius)
    {
        if (_target == null || string.IsNullOrWhiteSpace(sourceId)) return;

        float safeTickInterval = Mathf.Max(0.05f, tickInterval);
        float safeDps = Mathf.Max(0f, dps);
        float refreshedExpireTime = Time.time + Mathf.Max(0.05f, duration);

        if (_activeBurns.TryGetValue(sourceId, out BurnState existingState))
        {
            existingState.owner = owner ?? existingState.owner;
            existingState.damagePerSecond = safeDps;
            existingState.tickInterval = safeTickInterval;
            existingState.tickEffectPrefab = tickEffectPrefab;
            existingState.tickEffectRandomRadius = tickEffectRandomRadius;

            // Refresh duration without pushing the next scheduled tick farther out.
            existingState.expiresAt = Mathf.Max(existingState.expiresAt, refreshedExpireTime);
            existingState.nextTickAt = Mathf.Min(existingState.nextTickAt, Time.time + safeTickInterval);
            return;
        }

        _activeBurns[sourceId] = new BurnState
        {
            owner = owner,
            damagePerSecond = safeDps,
            tickInterval = safeTickInterval,
            nextTickAt = Time.time + safeTickInterval,
            expiresAt = refreshedExpireTime,
            tickEffectPrefab = tickEffectPrefab,
            tickEffectRandomRadius = tickEffectRandomRadius
        };
    }

    private void Update()
    {
        if (_target == null || _activeBurns.Count == 0) return;
        if (!HasDamageAuthority()) return;

        float totalTickDamage = 0f;
        List<string> expired = null;
        Player firstOwner = null;

        foreach (KeyValuePair<string, BurnState> entry in _activeBurns)
        {
            BurnState state = entry.Value;
            if (Time.time > state.expiresAt)
            {
                expired ??= new List<string>();
                expired.Add(entry.Key);
                continue;
            }

            float interval = Mathf.Max(0.05f, state.tickInterval);
            if (Time.time < state.nextTickAt)
            {
                continue;
            }

            int dueTicks = Mathf.Max(1, Mathf.FloorToInt((Time.time - state.nextTickAt) / interval) + 1);
            state.nextTickAt += dueTicks * interval;
            entry.Value.nextTickAt = state.nextTickAt;

            totalTickDamage += state.damagePerSecond * interval * dueTicks;
            if (firstOwner == null && state.owner != null)
            {
                firstOwner = state.owner;
            }

            for (int tick = 0; tick < dueTicks; tick++)
            {
                SpawnBurnTickEffect(state.tickEffectPrefab, state.tickEffectRandomRadius);
            }
        }

        if (expired != null)
        {
            foreach (string key in expired)
            {
                _activeBurns.Remove(key);
            }
        }

        if (totalTickDamage <= 0f) return;

        _target.TakeDamage(totalTickDamage, 0f, _target.transform.position, DamageSource.Other, firstOwner);
        try
        {
            string ownerName = firstOwner != null ? firstOwner.name : "(unknown)";
            float currentHealth = 0f;
            float currentShield = 0f;
            try
            {
                if (_target != null)
                {
                    currentHealth = _target.CurrentHealth;
                    currentShield = _target.currentShield;
                }
            }
            catch
            {
                currentHealth = 0f;
                currentShield = 0f;
            }

            Debug.Log($"[Burner] Applied burn tick {totalTickDamage:F2} to {_target.gameObject.name} (health={currentHealth:F2}, shield={currentShield:F2}) from {ownerName}");
        }
        catch (Exception)
        {
        }
    }

    private bool HasDamageAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _targetNetMovement != null && _targetNetMovement.IsServer;
    }

    private void SpawnBurnTickEffect(GameObject tickEffectPrefab, float randomRadius)
    {
        if (_target == null || tickEffectPrefab == null)
        {
            return;
        }

        Vector3 spawnPos = ResolveRandomTargetPoint(randomRadius);
        GameObject effect = Instantiate(tickEffectPrefab, spawnPos, Quaternion.identity);

        float size = 1f;
        if (_target is Player targetPlayer)
        {
            size = Mathf.Max(0.01f, targetPlayer.ShipSize);
        }
        effect.transform.localScale = tickEffectPrefab.transform.localScale * size;

        ParticleSystem particle = effect.GetComponent<ParticleSystem>();
        float lifetime = 1.5f;
        if (particle != null)
        {
            lifetime = Mathf.Max(0.1f, particle.main.duration + particle.main.startLifetime.constantMax);
        }

        Destroy(effect, lifetime);
    }

    private Vector3 ResolveRandomTargetPoint(float fallbackRadius)
    {
        if (_target == null)
        {
            return Vector3.zero;
        }

        Transform visualModel = _target.visualEffects.visualModel != null ? _target.visualEffects.visualModel : _target.transform;
        Renderer[] renderers = visualModel.GetComponentsInChildren<Renderer>();

        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                _target.transform.position.z);
        }

        Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, fallbackRadius);
        return _target.transform.position + new Vector3(offset.x, offset.y, 0f);
    }
}

public sealed class BodyBindingSlowController : MonoBehaviour
{
    private readonly Dictionary<string, float> _sourceMultipliers = new Dictionary<string, float>();
    private Entity _target;
    private float _baseMaxSpeed;

    private void Awake()
    {
        _target = GetComponent<Entity>();
        _baseMaxSpeed = _target != null ? _target.movement.maxSpeed : 0f;
    }

    public void SetSourceMultiplier(string sourceId, float multiplier)
    {
        if (_target == null || string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        _sourceMultipliers[sourceId] = Mathf.Clamp(multiplier, 0.01f, 1f);
        ApplyCurrentMultiplier();
    }

    public void ClearSource(string sourceId)
    {
        if (_target == null || string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        if (_sourceMultipliers.Remove(sourceId))
        {
            ApplyCurrentMultiplier();
        }
    }

    private void ApplyCurrentMultiplier()
    {
        if (_target == null)
        {
            return;
        }

        float total = 1f;
        foreach (float multiplier in _sourceMultipliers.Values)
        {
            total *= multiplier;
        }

        _target.movement.maxSpeed = Mathf.Max(0.01f, _baseMaxSpeed) * total;
    }
}

public sealed class AutoCounterReflectorController : MonoBehaviour, IProjectileReflectorAugment
{
    public event Action<Vector2> OnProjectileReflected;

    private Player _owner;
    private ReflectShield _shield;
    private Color _reflectColor;
    private bool _active;

    public void Initialize(Player owner, ReflectShield shieldPrefab, Color reflectColor)
    {
        _owner = owner;
        _reflectColor = reflectColor;

        if (_shield != null)
        {
            Destroy(_shield.gameObject);
            _shield = null;
        }

        if (shieldPrefab != null)
        {
            _shield = Instantiate(shieldPrefab, owner.transform);
            _shield.transform.localPosition = Vector3.zero;
            _shield.transform.localRotation = Quaternion.identity;
            _shield.gameObject.SetActive(true);
            _shield.Deactivate();
        }
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (_shield == null) return;

        if (active)
        {
            _shield.Activate(_reflectColor);
        }
        else
        {
            _shield.Deactivate();
        }
    }

    public bool TryReflectProjectile(ProjectileScript projectile, Vector2 hitPoint)
    {
        if (!_active || projectile == null || _owner == null) return false;

        if (_shield != null)
        {
            _shield.OnReflectHit(hitPoint);
            _shield.ReflectProjectile(projectile, _owner.enemyTag);
        }
        else
        {
            projectile.Reflect(_owner.enemyTag, _reflectColor, _owner);
        }

        projectile.MarkAsReflected();
        OnProjectileReflected?.Invoke(hitPoint);
        return true;
    }

    private void OnDestroy()
    {
        if (_shield != null)
        {
            Destroy(_shield.gameObject);
        }
    }
}

public sealed class FlyersSwarmController : MonoBehaviour
{
    private sealed class FlyerState
    {
        public Transform visual;
        public int orbitSlotIndex;
        public bool launched;
        public float launchStartTime;
        public float respawnTime;
    }

    private readonly List<FlyerState> _flyers = new List<FlyerState>();

    private Player _owner;
    private NetMovement _ownerNetMovement;
    private Flyers _config;
    private Entity _targetEntity;
    private float _nextRetargetTime;
    private float _orbitPhase;

    public void Initialize(Player owner, Flyers config)
    {
        _owner = owner;
        _ownerNetMovement = owner != null ? owner.GetComponent<NetMovement>() : null;
        _config = config;
        _targetEntity = null;
        _nextRetargetTime = 0f;
        _orbitPhase = 0f;

        ClearFlyers();

        int count = Mathf.Max(1, config != null ? config.flyerCount : 1);
        for (int i = 0; i < count; i++)
        {
            _flyers.Add(new FlyerState
            {
                orbitSlotIndex = i,
                launched = false,
                launchStartTime = -999f,
                respawnTime = 0f,
                visual = CreateVisual(i)
            });
        }
    }

    private Transform CreateVisual(int index)
    {
        GameObject go;
        if (_config != null && _config.flyerPrefab != null)
        {
            go = Instantiate(_config.flyerPrefab, transform);
        }
        else
        {
            go = new GameObject($"Flyer_{index}");
            go.transform.SetParent(transform, false);
        }

        return go.transform;
    }

    private void Update()
    {
        if (_owner == null || _config == null) return;

        UpdateTarget();

        float orbitRadius = Mathf.Max(0.1f, _config.orbitRadius);
        float orbitSpeed = _config.orbitSpeed;
        float hitRadius = Mathf.Max(0.05f, _config.hitRadius);
        float homingSpeed = Mathf.Max(0.1f, _config.homingSpeed);
        float orbitSlotSpacing = _flyers.Count > 0 ? 360f / _flyers.Count : 360f;

        _orbitPhase += orbitSpeed * Time.deltaTime;
        if (_orbitPhase > 360f || _orbitPhase < -360f)
        {
            _orbitPhase %= 360f;
        }

        foreach (FlyerState flyer in _flyers)
        {
            if (flyer.visual == null) continue;

            if (flyer.respawnTime > Time.time)
            {
                flyer.visual.gameObject.SetActive(false);
                continue;
            }

            flyer.visual.gameObject.SetActive(true);

            if (!flyer.launched)
            {
                float slotAngle = _orbitPhase + (flyer.orbitSlotIndex * orbitSlotSpacing);
                Vector2 orbitOffset = Quaternion.Euler(0f, 0f, slotAngle) * Vector2.up * orbitRadius;
                flyer.visual.position = _owner.transform.position + (Vector3)orbitOffset;

                if (_targetEntity != null)
                {
                    float distanceToTarget = Vector2.Distance(flyer.visual.position, _targetEntity.transform.position);
                    if (distanceToTarget <= _config.engageRange)
                    {
                        flyer.launched = true;
                        flyer.launchStartTime = Time.time;
                    }
                }

                continue;
            }

            if (_targetEntity == null)
            {
                flyer.launched = false;
                flyer.launchStartTime = -999f;
                continue;
            }

            float homingDuration = Mathf.Max(0.05f, _config.homingDuration);
            if (Time.time >= flyer.launchStartTime + homingDuration)
            {
                flyer.launched = false;
                flyer.launchStartTime = -999f;
                flyer.respawnTime = Time.time + Mathf.Max(0.1f, _config.autocastInterval);
                continue;
            }

            Vector2 current = flyer.visual.position;
            Vector2 target = _targetEntity.transform.position;
            Vector2 next = Vector2.MoveTowards(current, target, homingSpeed * Time.deltaTime);
            flyer.visual.position = next;

            if (Vector2.Distance(next, target) <= hitRadius)
            {
                ApplyHit(_targetEntity, next);
                flyer.launched = false;
                flyer.launchStartTime = -999f;
                flyer.respawnTime = Time.time + Mathf.Max(0.1f, _config.autocastInterval);
            }
        }
    }

    private void ApplyHit(Entity target, Vector2 hitPoint)
    {
        if (target == null || !HasDamageAuthority()) return;

        target.TakeDamage(_config.hitDamage, _config.impactForce, hitPoint, DamageSource.Other, _owner);
    }

    private void UpdateTarget()
    {
        if (_targetEntity != null &&
            _targetEntity.CurrentHealth > 0f &&
            _targetEntity.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Time.time < _nextRetargetTime)
        {
            return;
        }

        _nextRetargetTime = Time.time + 0.35f;
        _targetEntity = null;

        if (string.IsNullOrWhiteSpace(_owner.enemyTag))
        {
            return;
        }

        GameObject targetObj = GameObject.FindGameObjectWithTag(_owner.enemyTag);
        if (targetObj == null)
        {
            return;
        }

        _targetEntity = targetObj.GetComponent<Entity>();
    }

    private bool HasDamageAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _ownerNetMovement != null && _ownerNetMovement.IsServer;
    }

    private void OnDisable()
    {
        foreach (FlyerState flyer in _flyers)
        {
            if (flyer?.visual != null)
            {
                flyer.visual.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        ClearFlyers();
    }

    private void ClearFlyers()
    {
        foreach (FlyerState flyer in _flyers)
        {
            if (flyer?.visual != null)
            {
                Destroy(flyer.visual.gameObject);
            }
        }

        _flyers.Clear();
    }
}