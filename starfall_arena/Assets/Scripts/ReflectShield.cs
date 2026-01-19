using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class ReflectShield : MonoBehaviour
{
    private MeshRenderer _shieldRenderer;
    private MaterialPropertyBlock _propBlock;
    private PlayerScript _player;

    // Shader Property IDs
    private int _hitEffectID;
    private int _inflationID;
    private int _colorID;

    // Ripple slot IDs
    private int[] _hitPosIDs = new int[5];
    private int[] _rippleIDs = new int[5];

    // Ripple state
    private const int MAX_RIPPLES = 5;
    private Vector3[] _hitPositions = new Vector3[MAX_RIPPLES];
    private float[] _rippleProgress = new float[MAX_RIPPLES];
    private bool[] _rippleActive = new bool[MAX_RIPPLES];
    private int _nextRippleIndex = 0;

    // Shield state
    private float _currentAlpha = 0f;
    private bool _isActive = false;

    [Header("Visual Settings")]
    [SerializeField] private Color shieldColor = Color.cyan;
    [SerializeField] private float inflationAmount = 0.1f;
    [SerializeField] private float maxAlpha = 1f;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float rippleDuration = 0.5f;

    private float _activationTime;
    private float _deactivationTime;

    void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (_shieldRenderer != null) return; // Already initialized

        _player = GetComponentInParent<PlayerScript>();
        _shieldRenderer = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();

        // Cache shader property IDs
        _hitEffectID = Shader.PropertyToID("_HitEffect");
        _inflationID = Shader.PropertyToID("_InflationAmount");
        _colorID = Shader.PropertyToID("_ShieldColor");

        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _hitPosIDs[i] = Shader.PropertyToID("_HitPos" + i);
            _rippleIDs[i] = Shader.PropertyToID("_Ripple" + i);
        }

        // Apply initial settings
        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_inflationID, inflationAmount);
        _propBlock.SetColor(_colorID, shieldColor);
        _propBlock.SetFloat(_hitEffectID, 0);

        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _propBlock.SetVector(_hitPosIDs[i], Vector3.zero);
            _propBlock.SetFloat(_rippleIDs[i], 0);
        }

        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    public void Activate(Color color)
    {
        // Ensure components are initialized
        if (_shieldRenderer == null || _propBlock == null)
        {
            InitializeComponents();
        }

        _isActive = true;
        shieldColor = color;
        _activationTime = Time.time;
        _currentAlpha = 0f;

        // Reset ripples
        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _rippleActive[i] = false;
            _rippleProgress[i] = 0f;
        }

        // Update color
        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_colorID, shieldColor);
        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    public void Deactivate()
    {
        _isActive = false;
        _deactivationTime = Time.time;

        // Reset ripples to prevent frozen animations
        for (int i = 0; i < MAX_RIPPLES; i++)
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
        if (!_isActive) return;

        Vector3 localHitPos = transform.InverseTransformPoint(hitPoint);

        _hitPositions[_nextRippleIndex] = localHitPos;
        _rippleProgress[_nextRippleIndex] = 0f;
        _rippleActive[_nextRippleIndex] = true;

        _nextRippleIndex = (_nextRippleIndex + 1) % MAX_RIPPLES;
    }

    public void ReflectProjectile(ProjectileScript projectile)
    {
        if (!_isActive || _player == null) return;
        projectile.Reflect("Enemy", shieldColor, _player);
    }

    void Update()
    {
        if (!_isActive && _currentAlpha <= 0f)
            return;

        bool needsUpdate = false;

        // Handle alpha fade in/out
        if (_isActive)
        {
            // Fade in
            float timeSinceActivation = Time.time - _activationTime;
            if (timeSinceActivation < fadeInDuration)
            {
                _currentAlpha = Mathf.Lerp(0f, maxAlpha, timeSinceActivation / fadeInDuration);
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
            // Fade out
            float timeSinceDeactivation = Time.time - _deactivationTime;
            if (timeSinceDeactivation < fadeOutDuration)
            {
                _currentAlpha = Mathf.Lerp(maxAlpha, 0f, timeSinceDeactivation / fadeOutDuration);
                needsUpdate = true;
            }
            else if (_currentAlpha > 0f)
            {
                _currentAlpha = 0f;
                needsUpdate = true;
            }
        }

        // Update ripples
        float rippleSpeed = 1.0f / Mathf.Max(rippleDuration, 0.001f);

        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            if (_rippleActive[i])
            {
                _rippleProgress[i] += Time.deltaTime * rippleSpeed;

                if (_rippleProgress[i] >= 1f)
                {
                    _rippleProgress[i] = 0f;
                    _rippleActive[i] = false;
                }

                needsUpdate = true;
            }
        }

        if (needsUpdate) UpdateShader();
    }

    private void UpdateShader()
    {
        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_hitEffectID, _currentAlpha);

        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _propBlock.SetVector(_hitPosIDs[i], _hitPositions[i]);
            float visibleProgress = Mathf.Clamp01(_rippleProgress[i]);
            _propBlock.SetFloat(_rippleIDs[i], _rippleActive[i] ? visibleProgress : 0f);
        }

        _shieldRenderer.SetPropertyBlock(_propBlock);
    }
}