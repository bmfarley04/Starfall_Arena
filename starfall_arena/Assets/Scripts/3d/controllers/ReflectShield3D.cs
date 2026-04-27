using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class ReflectShield3D : MonoBehaviour
{
    private const int MaxRipples = 5;
    private const float MinDirectionSqrMagnitude = 0.0001f;

    private static readonly FieldInfo ProjectileDirectionField = typeof(Projectile3D).GetField("_direction", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ProjectileVelocityField = typeof(Projectile3D).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ProjectileShooterField = typeof(Projectile3D).GetField("_shooter", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ProjectileDamageField = typeof(Projectile3D).GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic);

    [Header("Visual Settings")]
    [SerializeField] private Color shieldColor = Color.cyan;
    [SerializeField] private float inflationAmount = 0.1f;
    [SerializeField] private float maxAlpha = 1f;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float rippleDuration = 0.5f;

    private MeshRenderer _shieldRenderer;
    private MaterialPropertyBlock _propBlock;
    private Entity3D _owner;
    private Reflector3D _reflector;

    private int _hitEffectId;
    private int _inflationId;
    private int _colorId;
    private readonly int[] _hitPosIds = new int[MaxRipples];
    private readonly int[] _rippleIds = new int[MaxRipples];

    private readonly Vector3[] _hitPositions = new Vector3[MaxRipples];
    private readonly float[] _rippleProgress = new float[MaxRipples];
    private readonly bool[] _rippleActive = new bool[MaxRipples];
    private int _nextRippleIndex;

    private float _currentAlpha;
    private bool _isActive;
    private float _activationTime;
    private float _deactivationTime;

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (_shieldRenderer != null)
        {
            return;
        }

        _owner = GetComponentInParent<Entity3D>();
        _reflector = GetComponentInParent<Reflector3D>();
        _shieldRenderer = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();

        _hitEffectId = Shader.PropertyToID("_HitEffect");
        _inflationId = Shader.PropertyToID("_InflationAmount");
        _colorId = Shader.PropertyToID("_ShieldColor");

        for (int i = 0; i < MaxRipples; i++)
        {
            _hitPosIds[i] = Shader.PropertyToID("_HitPos" + i);
            _rippleIds[i] = Shader.PropertyToID("_Ripple" + i);
        }

        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_inflationId, inflationAmount);
        _propBlock.SetColor(_colorId, shieldColor);
        _propBlock.SetFloat(_hitEffectId, 0f);

        for (int i = 0; i < MaxRipples; i++)
        {
            _propBlock.SetVector(_hitPosIds[i], Vector3.zero);
            _propBlock.SetFloat(_rippleIds[i], 0f);
        }

        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    public void Activate(Color color)
    {
        if (_shieldRenderer == null || _propBlock == null)
        {
            InitializeComponents();
        }

        _isActive = true;
        shieldColor = color;
        _activationTime = Time.time;
        _currentAlpha = 0f;

        for (int i = 0; i < MaxRipples; i++)
        {
            _rippleActive[i] = false;
            _rippleProgress[i] = 0f;
        }

        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_colorId, shieldColor);
        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    public void Deactivate()
    {
        _isActive = false;
        _deactivationTime = Time.time;

        for (int i = 0; i < MaxRipples; i++)
        {
            _rippleActive[i] = false;
            _rippleProgress[i] = 0f;
        }
    }

    public bool IsActive()
    {
        return _isActive;
    }

    public void OnReflectHit(Vector3 hitPoint)
    {
        if (!_isActive)
        {
            return;
        }

        Vector3 localHitPos = transform.InverseTransformPoint(hitPoint);
        _hitPositions[_nextRippleIndex] = localHitPos;
        _rippleProgress[_nextRippleIndex] = 0f;
        _rippleActive[_nextRippleIndex] = true;
        _nextRippleIndex = (_nextRippleIndex + 1) % MaxRipples;
    }

    public bool TryReflectProjectile(Projectile3D projectile, Vector3 hitPoint)
    {
        if (_reflector == null)
        {
            _reflector = GetComponentInParent<Reflector3D>();
        }

        return _reflector != null && _reflector.TryReflectProjectile(projectile, hitPoint);
    }

    public bool ReflectProjectile(Projectile3D projectile, Color reflectedColor, float damageMultiplier)
    {
        if (!_isActive || _owner == null || projectile == null)
        {
            return false;
        }

        if (ProjectileDirectionField == null || ProjectileVelocityField == null || ProjectileShooterField == null || ProjectileDamageField == null)
        {
            Debug.LogWarning("ReflectShield3D could not resolve Projectile3D runtime fields. Reflection was skipped.", this);
            return false;
        }

        Vector3 currentDirection = (Vector3)ProjectileDirectionField.GetValue(projectile);
        Vector3 currentVelocity = (Vector3)ProjectileVelocityField.GetValue(projectile);
        Entity3D originalShooter = ProjectileShooterField.GetValue(projectile) as Entity3D;

        Vector3 reflectedDirection = ResolveReflectedDirection(projectile.transform.position, currentDirection, originalShooter);
        float reflectedSpeed = currentVelocity.magnitude;
        Vector3 reflectedVelocity = reflectedDirection * reflectedSpeed;
        float currentDamage = (float)ProjectileDamageField.GetValue(projectile);

        projectile.targetTag = ResolveReflectedTargetTag(projectile.targetTag, originalShooter);
        projectile.TargetFaction = FactionMember3D.ResolveFaction(originalShooter);

        ProjectileDirectionField.SetValue(projectile, reflectedDirection);
        ProjectileVelocityField.SetValue(projectile, reflectedVelocity);
        ProjectileShooterField.SetValue(projectile, _owner);
        ProjectileDamageField.SetValue(projectile, Mathf.Max(0f, currentDamage * Mathf.Max(0f, damageMultiplier)));
        projectile.transform.position += reflectedDirection * 0.05f;
        projectile.transform.rotation = Quaternion.LookRotation(reflectedDirection, ResolveUpVector(reflectedDirection));

        shieldColor = reflectedColor;
        _owner.GetComponent<NetCombat3D>()?.BroadcastReflectedProjectile(projectile, reflectedColor);

        return true;
    }

    private void Update()
    {
        if (!_isActive && _currentAlpha <= 0f)
        {
            return;
        }

        bool needsUpdate = false;

        if (_isActive)
        {
            float timeSinceActivation = Time.time - _activationTime;
            if (timeSinceActivation < fadeInDuration)
            {
                _currentAlpha = Mathf.Lerp(0f, maxAlpha, timeSinceActivation / Mathf.Max(fadeInDuration, 0.001f));
                needsUpdate = true;
            }
            else if (_currentAlpha < maxAlpha)
            {
                _currentAlpha = maxAlpha;
                needsUpdate = true;
            }
        }
        else
        {
            float timeSinceDeactivation = Time.time - _deactivationTime;
            if (timeSinceDeactivation < fadeOutDuration)
            {
                _currentAlpha = Mathf.Lerp(maxAlpha, 0f, timeSinceDeactivation / Mathf.Max(fadeOutDuration, 0.001f));
                needsUpdate = true;
            }
            else if (_currentAlpha > 0f)
            {
                _currentAlpha = 0f;
                needsUpdate = true;
            }
        }

        float rippleSpeed = 1f / Mathf.Max(rippleDuration, 0.001f);
        for (int i = 0; i < MaxRipples; i++)
        {
            if (!_rippleActive[i])
            {
                continue;
            }

            _rippleProgress[i] += Time.deltaTime * rippleSpeed;
            if (_rippleProgress[i] >= 1f)
            {
                _rippleProgress[i] = 0f;
                _rippleActive[i] = false;
            }

            needsUpdate = true;
        }

        if (needsUpdate)
        {
            UpdateShader();
        }
    }

    private void UpdateShader()
    {
        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_hitEffectId, _currentAlpha);

        for (int i = 0; i < MaxRipples; i++)
        {
            _propBlock.SetVector(_hitPosIds[i], _hitPositions[i]);
            _propBlock.SetFloat(_rippleIds[i], _rippleActive[i] ? Mathf.Clamp01(_rippleProgress[i]) : 0f);
        }

        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    private Vector3 ResolveReflectedDirection(Vector3 projectilePosition, Vector3 currentDirection, Entity3D originalShooter)
    {
        if (originalShooter != null && originalShooter != _owner)
        {
            Vector3 towardShooter = originalShooter.transform.position - projectilePosition;
            if (towardShooter.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                return towardShooter.normalized;
            }
        }

        if (currentDirection.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            return -currentDirection.normalized;
        }

        Vector3 fallback = -transform.forward;
        return fallback.sqrMagnitude > MinDirectionSqrMagnitude ? fallback.normalized : Vector3.back;
    }

    private string ResolveReflectedTargetTag(string currentTargetTag, Entity3D originalShooter)
    {
        if (originalShooter != null && !string.IsNullOrEmpty(originalShooter.tag))
        {
            return originalShooter.tag;
        }

        if (_owner != null)
        {
            if (_owner.CompareTag("Player1"))
            {
                return "Player2";
            }

            if (_owner.CompareTag("Player2"))
            {
                return "Player1";
            }

            if (_owner.CompareTag("Player"))
            {
                return "Enemy";
            }

            if (_owner.CompareTag("Enemy"))
            {
                return "Player";
            }
        }

        return currentTargetTag;
    }

    private Vector3 ResolveUpVector(Vector3 direction)
    {
        float verticalAlignment = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up));
        if (verticalAlignment > 0.98f)
        {
            return transform.right.sqrMagnitude > MinDirectionSqrMagnitude ? transform.right.normalized : Vector3.right;
        }

        return Vector3.up;
    }
}
