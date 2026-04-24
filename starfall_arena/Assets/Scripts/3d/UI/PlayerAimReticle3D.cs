using UnityEngine;
using UnityEngine.UI;

public class PlayerAimReticle3D : PlayerHUDBindingTarget3D
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
        public RectTransform leftBracket;
        public RectTransform rightBracket;
        public Vector2 leftClosedOffset;
        public Vector2 rightClosedOffset;
        public float smoothing;
    }

    [System.Serializable]
    private struct SpinConfig
    {
        public RectTransform innerCircle;
        public float maxSpinSpeed;
        public float acceleration;
        public float deceleration;
        public float pulseHoldDuration;
    }

    [System.Serializable]
    private struct HoverConfig
    {
        public Camera aimCameraOverride;
        public LayerMask hoverMask;
        public float maxDistance;
        public float hitscanRadius;
        public string enemyTag;
    }

    [System.Serializable]
    private struct AbilitySpinBinding
    {
        public MonoBehaviour source;
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
        public Image leftFill;
        public Image rightFill;
    }

    [System.Serializable]
    private struct FiringVisualConfig
    {
        public DashMarkVisualConfig dashMarks;
        public InnerCircleVisualConfig innerCircle;
    }

    [System.Serializable]
    private struct DashMarkVisualConfig
    {
        public Image[] images;
        public Color baseColor;
        public Color firingColor;
        [Range(0f, 1f)] public float baseAlpha;
        [Range(0f, 1f)] public float firingAlpha;
    }

    [System.Serializable]
    private struct InnerCircleVisualConfig
    {
        public Color baseColor;
        public Color firingColor;
    }

    [Header("References")]
    [SerializeField] private Entity3D entity;
    [SerializeField] private Player3D player;
    [SerializeField] private PlayerInput3D playerInput;
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

    [Header("Firing Visuals")]
    [SerializeField] private FiringVisualConfig firingVisuals = new FiringVisualConfig
    {
        dashMarks = new DashMarkVisualConfig
        {
            baseColor = Color.white,
            firingColor = Color.white,
            baseAlpha = 0.4f,
            firingAlpha = 1f
        },
        innerCircle = new InnerCircleVisualConfig
        {
            baseColor = Color.white,
            firingColor = Color.white
        }
    };

    private Vector2 _leftOpenPosition;
    private Vector2 _rightOpenPosition;
    private float _bracketCloseLerp;
    private float _currentSpinSpeed;
    private CachedSpinBinding[] _cachedSpinSources = System.Array.Empty<CachedSpinBinding>();
    private Entity3D _fallbackEntity;
    private Player3D _fallbackPlayer;
    private PlayerInput3D _fallbackPlayerInput;
    private Camera _resolvedAimCamera;

    protected override void Awake()
    {
        base.Awake();

        entity ??= GetComponent<Entity3D>();
        player ??= GetComponent<Player3D>();
        playerInput ??= GetComponent<PlayerInput3D>();

        _fallbackEntity = entity;
        _fallbackPlayer = player;
        _fallbackPlayerInput = playerInput;

        CacheOpenBracketPositions();
        CacheSpinSources();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheOpenBracketPositions();
        CacheSpinSources();
    }

    private void Update()
    {
        RefreshAimCameraBinding();
        UpdateBracketPositions();
        UpdateHeatFill();
        UpdateInnerSpin();
        UpdateFiringVisuals();
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

    private void UpdateFiringVisuals()
    {
        bool isFiring = ShouldSpinReticle();
        float dashAlpha = Mathf.Clamp01(isFiring ? firingVisuals.dashMarks.firingAlpha : firingVisuals.dashMarks.baseAlpha);
        Color dashColor = WithAlpha(isFiring ? firingVisuals.dashMarks.firingColor : firingVisuals.dashMarks.baseColor, dashAlpha);
        Color circleColor = isFiring ? firingVisuals.innerCircle.firingColor : firingVisuals.innerCircle.baseColor;

        if (firingVisuals.dashMarks.images != null)
        {
            for (int i = 0; i < firingVisuals.dashMarks.images.Length; i++)
            {
                if (firingVisuals.dashMarks.images[i] == null)
                {
                    continue;
                }

                firingVisuals.dashMarks.images[i].color = dashColor;
            }
        }

        if (innerCircleImage != null)
        {
            circleColor.a = 1f;
            innerCircleImage.color = circleColor;
        }
    }

    private void UpdateHeatFill()
    {
        Weapon3D selectedWeapon = GetSelectedWeapon();
        float normalizedHeat = selectedWeapon != null ? selectedWeapon.GetReticleFillRatio() : 0f;
        Color fillColor = selectedWeapon != null ? selectedWeapon.ReticleFillColor : Color.white;

        if (heatFill.leftFill != null)
        {
            heatFill.leftFill.fillAmount = normalizedHeat;
            heatFill.leftFill.color = fillColor;
        }

        if (heatFill.rightFill != null)
        {
            heatFill.rightFill.fillAmount = normalizedHeat;
            heatFill.rightFill.color = fillColor;
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
            MonoBehaviour[] localBehaviours = GetSpinSourceBehaviours();
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is IReticleSpinSource3D spinSource && localBehaviours[i] is Ability3D ability)
                {
                    sources.Add(new CachedSpinBinding
                    {
                        source = localBehaviours[i],
                        spinSource = spinSource,
                        ability = ability,
                        mode = AbilitySpinMode.AutoDetect
                    });
                }
            }
        }

        _cachedSpinSources = sources.ToArray();
    }

    protected override void BindPlayer(Player3D boundPlayer)
    {
        player = boundPlayer;
        entity = boundPlayer;
        playerInput = boundPlayer != null ? boundPlayer.PlayerInput3D : null;
        RefreshAimCameraBinding(force: true);
        CacheSpinSources();
    }

    protected override void UnbindPlayer(Player3D boundPlayer)
    {
    }

    protected override void ClearBinding()
    {
        player = _fallbackPlayer;
        entity = _fallbackEntity;
        playerInput = _fallbackPlayerInput;
        _resolvedAimCamera = null;
        CacheSpinSources();
    }

    private MonoBehaviour[] GetSpinSourceBehaviours()
    {
        if (player != null)
        {
            return player.GetComponents<MonoBehaviour>();
        }

        if (entity != null)
        {
            return entity.GetComponents<MonoBehaviour>();
        }

        return GetComponents<MonoBehaviour>();
    }

    private bool ShouldSpinReticle()
    {
        return IsPrimaryWeaponSpinActive() || AreAbilitySpinSourcesActive();
    }

    private bool IsPrimaryWeaponSpinActive()
    {
        Weapon3D selectedWeapon = GetSelectedWeapon();
        if (selectedWeapon == null)
        {
            return playerInput != null && playerInput.IsFireHeld;
        }

        if (selectedWeapon.IsReticleSpinActive())
        {
            return true;
        }

        float holdDuration = Mathf.Max(0f, spinConfig.pulseHoldDuration);
        return Time.time <= selectedWeapon.GetReticleSpinPulseTime() + holdDuration;
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
        Camera resolvedAimCamera = GetResolvedAimCamera();
        if (resolvedAimCamera != null && hoverConfig.aimCameraOverride != null)
        {
            return resolvedAimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        Weapon3D selectedWeapon = GetSelectedWeapon();
        if (selectedWeapon != null)
        {
            return selectedWeapon.GetAimRay();
        }

        if (resolvedAimCamera != null)
        {
            return resolvedAimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        return new Ray(transform.position, transform.forward);
    }

    private Weapon3D GetSelectedWeapon()
    {
        if (player != null && player.SelectedWeapon != null)
        {
            return player.SelectedWeapon;
        }

        return entity != null ? entity.SelectedWeapon : null;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void RefreshAimCameraBinding(bool force = false)
    {
        Camera resolvedAimCamera = GetResolvedAimCamera();
        if (!force && ReferenceEquals(_resolvedAimCamera, resolvedAimCamera))
        {
            return;
        }

        _resolvedAimCamera = resolvedAimCamera;
        BindAimCameraToPlayerWeapons(resolvedAimCamera);
    }

    private Camera GetResolvedAimCamera()
    {
        if (hoverConfig.aimCameraOverride != null)
        {
            return hoverConfig.aimCameraOverride;
        }

        return Camera.main;
    }

    private void BindAimCameraToPlayerWeapons(Camera aimCamera)
    {
        if (player == null)
        {
            return;
        }

        Weapon3D[] weapons = player.Weapons;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null)
            {
                continue;
            }

            weapons[i].SetAimCamera(aimCamera);
        }
    }
}
