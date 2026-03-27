using UnityEngine;
using UnityEngine.UI;

public class PlayerAimReticle3D : MonoBehaviour
{
    private enum AbilitySpinMode
    {
        AutoDetect,
        ContinuousWhileActive,
        PulseOnly
    }

    [System.Serializable]
    private struct BracketConfig
    {
        [Tooltip("Left outer bracket RectTransform.")]
        public RectTransform leftBracket;
        [Tooltip("Right outer bracket RectTransform.")]
        public RectTransform rightBracket;
        [Tooltip("Offset applied from the authored/open pose when an enemy is under the center-screen aim ray.")]
        public Vector2 leftClosedOffset;
        [Tooltip("Offset applied from the authored/open pose when an enemy is under the center-screen aim ray.")]
        public Vector2 rightClosedOffset;
        [Tooltip("How quickly the brackets smooth toward their target pose.")]
        public float smoothing;
    }

    [System.Serializable]
    private struct SpinConfig
    {
        [Tooltip("Inner reticle image that spins while the primary weapon is firing.")]
        public RectTransform innerCircle;
        [Tooltip("Peak rotation speed in degrees per second while a spin source is active.")]
        public float maxSpinSpeed;
        [Tooltip("How quickly the current spin speed ramps up toward the active speed.")]
        public float acceleration;
        [Tooltip("How quickly the current spin speed ramps down toward zero after firing stops.")]
        public float deceleration;
        [Tooltip("How long projectile shots and pulse-style abilities keep the reticle in its active spin state.")]
        public float pulseHoldDuration;
    }

    [System.Serializable]
    private struct HoverConfig
    {
        [Tooltip("Optional override camera. Defaults to the weapon aim camera, then Camera.main.")]
        public Camera aimCameraOverride;
        [Tooltip("Only hits on these layers are considered for the hover-close test.")]
        public LayerMask hoverMask;
        [Tooltip("Maximum distance for center-screen enemy detection.")]
        public float maxDistance;
        [Tooltip("SphereCast radius used for forgiving center-screen enemy detection.")]
        public float hitscanRadius;
        [Tooltip("Fallback tag used when the hit object does not expose Enemy3D directly.")]
        public string enemyTag;
    }

    [System.Serializable]
    private struct AbilitySpinBinding
    {
        [Tooltip("Ability component that should influence reticle spin.")]
        public MonoBehaviour source;
        [Tooltip("AutoDetect uses IReticleSpinSource3D when available. The explicit modes also work with plain Ability3D references.")]
        public AbilitySpinMode mode;
    }

    private struct CachedSpinBinding
    {
        public MonoBehaviour source;
        public IReticleSpinSource3D spinSource;
        public Ability3D ability;
        public AbilitySpinMode mode;
    }

    [System.Serializable]
    private struct HeatFillConfig
    {
        [Tooltip("Optional fill image for the left bracket overheat meter.")]
        public Image leftFill;
        [Tooltip("Optional fill image for the right bracket overheat meter.")]
        public Image rightFill;
    }

    [Header("References")]
    [SerializeField] private Entity3D entity;
    [SerializeField] private Player3D player;
    [SerializeField] private PlayerInput3D playerInput;
    [SerializeField] private ProjectileWeapon3D primaryWeapon;
    [SerializeField] private Image innerCircleImage;
    [SerializeField] private AbilitySpinBinding[] additionalSpinSources;

    [Header("Bracket Motion")]
    [SerializeField] private BracketConfig bracketConfig = new BracketConfig
    {
        leftClosedOffset = new Vector2(12f, 0f),
        rightClosedOffset = new Vector2(-12f, 0f),
        smoothing = 10f
    };

    [Header("Inner Spin")]
    [SerializeField] private SpinConfig spinConfig = new SpinConfig
    {
        maxSpinSpeed = 240f,
        acceleration = 900f,
        deceleration = 540f,
        pulseHoldDuration = 0.12f
    };

    [Header("Enemy Hover Detection")]
    [SerializeField] private HoverConfig hoverConfig = new HoverConfig
    {
        hoverMask = ~0,
        maxDistance = 1000f,
        hitscanRadius = 1f,
        enemyTag = "Enemy"
    };
    [Header("Heat Fill")]
    [SerializeField] private HeatFillConfig heatFill;

    private Vector2 _leftOpenPosition;
    private Vector2 _rightOpenPosition;
    private float _bracketCloseLerp;
    private float _currentSpinSpeed;
    private CachedSpinBinding[] _cachedSpinSources = System.Array.Empty<CachedSpinBinding>();

    private void Awake()
    {
        entity ??= GetComponent<Entity3D>();
        player ??= GetComponent<Player3D>();
        playerInput ??= GetComponent<PlayerInput3D>();
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();

        CacheOpenBracketPositions();
        CacheSpinSources();
    }

    private void OnEnable()
    {
        CacheOpenBracketPositions();
        CacheSpinSources();
    }

    private void Update()
    {
        UpdateBracketPositions();
        UpdateHeatFill();
        UpdateInnerSpin();
    }

    private void CacheOpenBracketPositions()
    {
        if (bracketConfig.leftBracket != null)
        {
            _leftOpenPosition = bracketConfig.leftBracket.anchoredPosition;
        }

        if (bracketConfig.rightBracket != null)
        {
            _rightOpenPosition = bracketConfig.rightBracket.anchoredPosition;
        }
    }

    private void UpdateBracketPositions()
    {
        bool hoveringEnemy = IsHoveringEnemy();
        float targetCloseLerp = hoveringEnemy ? 1f : 0f;
        float smoothing = Mathf.Max(0.01f, bracketConfig.smoothing);
        _bracketCloseLerp = Mathf.MoveTowards(_bracketCloseLerp, targetCloseLerp, smoothing * Time.deltaTime);

        if (bracketConfig.leftBracket != null)
        {
            bracketConfig.leftBracket.anchoredPosition = Vector2.Lerp(
                _leftOpenPosition,
                _leftOpenPosition + bracketConfig.leftClosedOffset,
                _bracketCloseLerp);
        }

        if (bracketConfig.rightBracket != null)
        {
            bracketConfig.rightBracket.anchoredPosition = Vector2.Lerp(
                _rightOpenPosition,
                _rightOpenPosition + bracketConfig.rightClosedOffset,
                _bracketCloseLerp);
        }
    }

    private void UpdateInnerSpin()
    {
        RectTransform target = spinConfig.innerCircle != null ? spinConfig.innerCircle : innerCircleImage != null ? innerCircleImage.rectTransform : null;
        if (target == null)
        {
            return;
        }

        float targetSpinSpeed = ShouldSpinReticle() ? Mathf.Max(0f, spinConfig.maxSpinSpeed) : 0f;
        float moveRate = targetSpinSpeed > _currentSpinSpeed
            ? Mathf.Max(0f, spinConfig.acceleration)
            : Mathf.Max(0f, spinConfig.deceleration);

        _currentSpinSpeed = moveRate > 0f
            ? Mathf.MoveTowards(_currentSpinSpeed, targetSpinSpeed, moveRate * Time.deltaTime)
            : targetSpinSpeed;

        if (_currentSpinSpeed <= 0.0001f)
        {
            return;
        }

        target.Rotate(0f, 0f, -_currentSpinSpeed * Time.deltaTime, Space.Self);
    }

    private void UpdateHeatFill()
    {
        float normalizedHeat = entity != null ? Mathf.Clamp01(entity.CurrentPrimaryWeaponEnergyNormalized) : 0f;

        if (heatFill.leftFill != null)
        {
            heatFill.leftFill.fillAmount = normalizedHeat;
        }

        if (heatFill.rightFill != null)
        {
            heatFill.rightFill.fillAmount = normalizedHeat;
        }
    }

    private void CacheSpinSources()
    {
        System.Collections.Generic.List<CachedSpinBinding> sources = new();

        if (additionalSpinSources != null && additionalSpinSources.Length > 0)
        {
            for (int i = 0; i < additionalSpinSources.Length; i++)
            {
                AbilitySpinBinding binding = additionalSpinSources[i];
                if (binding.source == null)
                {
                    continue;
                }

                sources.Add(new CachedSpinBinding
                {
                    source = binding.source,
                    spinSource = binding.source as IReticleSpinSource3D,
                    ability = binding.source as Ability3D,
                    mode = binding.mode
                });

                if (binding.mode == AbilitySpinMode.AutoDetect && binding.source is not IReticleSpinSource3D)
                {
                    Debug.LogWarning($"PlayerAimReticle3D AutoDetect source {binding.source.name} does not implement IReticleSpinSource3D.", this);
                }

                if (binding.mode == AbilitySpinMode.PulseOnly && binding.source is not IReticleSpinSource3D)
                {
                    Debug.LogWarning($"PlayerAimReticle3D PulseOnly source {binding.source.name} must implement IReticleSpinSource3D.", this);
                }
            }
        }
        else
        {
            MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is IReticleSpinSource3D spinSource)
                {
                    sources.Add(new CachedSpinBinding
                    {
                        source = localBehaviours[i],
                        spinSource = spinSource,
                        ability = localBehaviours[i] as Ability3D,
                        mode = AbilitySpinMode.AutoDetect
                    });
                }
            }
        }

        _cachedSpinSources = sources.ToArray();
    }

    private bool ShouldSpinReticle()
    {
        return IsPrimaryWeaponSpinActive() || AreAbilitySpinSourcesActive();
    }

    private bool IsPrimaryWeaponSpinActive()
    {
        if (primaryWeapon == null)
        {
            return playerInput != null && playerInput.IsFireHeld;
        }

        if (playerInput == null || !playerInput.IsFireHeld)
        {
            float holdDuration = Mathf.Max(0f, spinConfig.pulseHoldDuration);
            return Time.time <= primaryWeapon.LastSuccessfulFireTime + holdDuration;
        }

        if (player != null && player.IsPrimaryFireDisabledByAbility())
        {
            return false;
        }

        return true;
    }

    private bool AreAbilitySpinSourcesActive()
    {
        float holdDuration = Mathf.Max(0f, spinConfig.pulseHoldDuration);

        for (int i = 0; i < _cachedSpinSources.Length; i++)
        {
            CachedSpinBinding binding = _cachedSpinSources[i];
            bool hasCustomSource = binding.spinSource != null;
            bool hasAbility = binding.ability != null;

            switch (binding.mode)
            {
                case AbilitySpinMode.AutoDetect:
                    if (!hasCustomSource)
                    {
                        continue;
                    }

                    if (binding.spinSource.IsReticleSpinActive())
                    {
                        return true;
                    }

                    if (Time.time <= binding.spinSource.GetReticleSpinPulseTime() + holdDuration)
                    {
                        return true;
                    }

                    break;

                case AbilitySpinMode.ContinuousWhileActive:
                    if (hasAbility && binding.ability.IsAbilityActive())
                    {
                        return true;
                    }

                    break;

                case AbilitySpinMode.PulseOnly:
                    if (hasCustomSource && Time.time <= binding.spinSource.GetReticleSpinPulseTime() + holdDuration)
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private bool IsHoveringEnemy()
    {
        Ray aimRay = GetAimRay();
        float maxDistance = Mathf.Max(0.01f, hoverConfig.maxDistance);
        float hitscanRadius = Mathf.Max(0f, hoverConfig.hitscanRadius);

        bool hitDetected = hitscanRadius > 0f
            ? Physics.SphereCast(aimRay, hitscanRadius, out RaycastHit hit, maxDistance, hoverConfig.hoverMask, QueryTriggerInteraction.Ignore)
            : Physics.Raycast(aimRay, out hit, maxDistance, hoverConfig.hoverMask, QueryTriggerInteraction.Ignore);

        if (!hitDetected)
        {
            return false;
        }

        if (player != null && hit.collider.transform.IsChildOf(player.transform))
        {
            return false;
        }

        Enemy3D enemy = hit.collider.GetComponentInParent<Enemy3D>();
        if (enemy != null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(hoverConfig.enemyTag) && hit.collider.CompareTag(hoverConfig.enemyTag))
        {
            return true;
        }

        Transform root = hit.collider.transform.root;
        return root != null
            && !string.IsNullOrWhiteSpace(hoverConfig.enemyTag)
            && root.CompareTag(hoverConfig.enemyTag);
    }

    private Ray GetAimRay()
    {
        if (hoverConfig.aimCameraOverride != null)
        {
            return hoverConfig.aimCameraOverride.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        if (primaryWeapon != null)
        {
            return primaryWeapon.GetScreenCenterAimRay();
        }

        Camera fallbackCamera = Camera.main;
        if (fallbackCamera != null)
        {
            return fallbackCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        return new Ray(transform.position, transform.forward);
    }
}
