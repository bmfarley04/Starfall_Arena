using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class ShieldController : MonoBehaviour
{
    private MeshRenderer _shieldRenderer;
    private MaterialPropertyBlock _propBlock;
    
    // Shader Property IDs
    private int _hitEffectID;
    private int _inflationID;
    private int _colorID;
    private int _breakRadiusID; 
    
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
    private float _currentBreakRadius = -1f; 
    
    // Logic Flags
    private bool _isRegenerating = false;
    private bool _isBreaking = false; 

    [Header("Visual Settings")]
    [SerializeField] private Color shieldColor = Color.cyan;
    [SerializeField] private float inflationAmount = 0.1f;
    
    [Header("Animation Settings")]
    [SerializeField] private float shieldVisibleDuration = 0.5f;
    [SerializeField] private float rippleDuration = 0.5f;

    [Header("Regeneration Settings")]
    [SerializeField] private float regenMaxAlpha = 0.2f;
    [SerializeField] private float regenPulseSpeed = 4.0f;

    [Header("Break Effect")]
    [Tooltip("Total time for the break ripple to travel from center to edge")]
    [SerializeField] private float breakDuration = 0.8f;

    [Tooltip("If true, calculates radius from the mesh bounds automatically.")]
    [SerializeField] private bool autoCalculateSize = true;

    [Tooltip("The size of the hole when the shield is fully gone. (Ignored if Auto Calculate is true)")]
    [SerializeField] private float maxBreakRadius = 1.5f;

    [Header("Always On Mode (HUD Effects)")]
    [Tooltip("If true, shield stays permanently visible at a constant alpha (for HUD effects)")]
    [SerializeField] private bool alwaysOn = false;

    [Tooltip("Alpha value when always on mode is enabled (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float alwaysOnAlpha = 0.3f; 

    void Start()
    {
        _shieldRenderer = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();
        
        // --- NEW: Auto-calculate radius ---
        if (autoCalculateSize)
        {
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            if (mesh != null)
            {
                // bounds.extents is the vector from center to edge of the AABB.
                // magnitude gives us the distance to the furthest corner.
                // This ensures the ripple covers the whole shape.
                maxBreakRadius = mesh.bounds.extents.magnitude;
            }
        }
        // ----------------------------------

        // Cache IDs
        _hitEffectID = Shader.PropertyToID("_HitEffect");
        _inflationID = Shader.PropertyToID("_InflationAmount");
        _colorID = Shader.PropertyToID("_ShieldColor");
        _breakRadiusID = Shader.PropertyToID("_BreakRadius"); 
        
        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _hitPosIDs[i] = Shader.PropertyToID("_HitPos" + i);
            _rippleIDs[i] = Shader.PropertyToID("_Ripple" + i);
        }

        // Apply initial settings
        _shieldRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_inflationID, inflationAmount);
        _propBlock.SetColor(_colorID, shieldColor);

        // Initialize alpha based on alwaysOn mode
        if (alwaysOn)
        {
            _currentAlpha = alwaysOnAlpha;
            _propBlock.SetFloat(_hitEffectID, alwaysOnAlpha);
        }
        else
        {
            _propBlock.SetFloat(_hitEffectID, 0);
        }

        _propBlock.SetFloat(_breakRadiusID, -1f); 
        
        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _propBlock.SetVector(_hitPosIDs[i], Vector3.zero);
            _propBlock.SetFloat(_rippleIDs[i], 0);
        }
        
        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    public void OnHit(Vector3 hitPoint)
    {
        if (_isBreaking) return;

        // Ensure hole is closed and alpha is up
        _currentBreakRadius = -1f;
        _currentAlpha = 1f;
        
        Vector3 localHitPos = transform.InverseTransformPoint(hitPoint);
        
        _hitPositions[_nextRippleIndex] = localHitPos;
        _rippleProgress[_nextRippleIndex] = 0f;
        _rippleActive[_nextRippleIndex] = true;
        
        _nextRippleIndex = (_nextRippleIndex + 1) % MAX_RIPPLES;
        
        UpdateShader();
    }

    public void BreakShield()
    {
        _isBreaking = true;
        _isRegenerating = false; 
        _currentAlpha = 1f; 

        // 1. Reset all ripples
        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _rippleActive[i] = false;
            _rippleProgress[i] = 0f;
        }

        // 2. Activate ONLY the first ripple at the center
        _hitPositions[0] = Vector3.zero; 
        _rippleActive[0] = true;
        _rippleProgress[0] = 0f;
        
        // 3. Start the hole at 0 (center)
        _currentBreakRadius = 0f;
    }

    public void SetRegeneration(bool active)
    {
        if (_isBreaking) return;

        // If alwaysOn is enabled, ignore regeneration state changes
        // (shield should stay visible regardless)
        if (alwaysOn) return;

        if (active)
        {
            // Reset the hole so the shield can fade back in visually
            _currentBreakRadius = -1f;
        }

        _isRegenerating = active;
    }

    void Update()
    {
        if (_isBreaking)
        {
            HandleBreakSequence();
            return; 
        }

        bool needsUpdate = false;

        // --- REGEN & DECAY ---
        float targetPulseAlpha = 0f;

        if (_isRegenerating)
        {
            float pulse = (Mathf.Sin(Time.time * regenPulseSpeed) + 1f) * 0.5f;
            targetPulseAlpha = pulse * regenMaxAlpha;
            needsUpdate = true;
        }
        else if (alwaysOn)
        {
            // Always On mode: maintain constant alpha
            targetPulseAlpha = alwaysOnAlpha;
            needsUpdate = true;
        }

        if (_currentAlpha > targetPulseAlpha)
        {
            float decayRate = 1.0f / Mathf.Max(shieldVisibleDuration, 0.001f);
            _currentAlpha -= Time.deltaTime * decayRate;

            if (_currentAlpha < targetPulseAlpha) _currentAlpha = targetPulseAlpha;
            needsUpdate = true;
        }
        else if (_isRegenerating || alwaysOn)
        {
            _currentAlpha = targetPulseAlpha;
            needsUpdate = true;
        }
        else if (_currentAlpha < 0)
        {
             _currentAlpha = 0;
        }
        
        // --- NORMAL RIPPLES ---
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
        
        // Safety: Ensure radius is reset if we aren't breaking
        if (!_isBreaking && _currentBreakRadius > -0.5f) 
        {
             _currentBreakRadius = -1f;
             needsUpdate = true;
        }
        
        if (needsUpdate) UpdateShader();
    }

    private void HandleBreakSequence()
    {
        float expansionSpeed = maxBreakRadius / Mathf.Max(breakDuration, 0.001f);
        
        // 1. Expand the hole
        _currentBreakRadius += Time.deltaTime * expansionSpeed;

        // 2. Expand the visual ring (Ripple 0) to match the hole edge
        // Note: Shader ripple usually goes 0-1.
        // We normalize the radius (0 to max) back to 0-1 range for the ripple progress
        _rippleProgress[0] = _currentBreakRadius / Mathf.Max(1f, maxBreakRadius); 
        
        // Ensure ripple is active so the ring draws
        _hitPositions[0] = Vector3.zero;
        _rippleActive[0] = true;

        // 3. End Condition
        // We add a tiny buffer (1.1x) to ensure it fully clears any interpolation artifacts
        if (_currentBreakRadius >= maxBreakRadius * 1.1f)
        {
            _isBreaking = false;
            _currentAlpha = 0f; // Turn off global alpha
            _rippleActive[0] = false;
        }
        
        UpdateShader();
    }
    
    private void UpdateShader()
    {
        _shieldRenderer.GetPropertyBlock(_propBlock);
        
        // Standard Properties
        _propBlock.SetFloat(_hitEffectID, _currentAlpha);
        _propBlock.SetFloat(_breakRadiusID, _currentBreakRadius); // Update the hole size
        
        // Ripple Arrays
        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            _propBlock.SetVector(_hitPosIDs[i], _hitPositions[i]);
            float visibleProgress = Mathf.Clamp01(_rippleProgress[i]);
            _propBlock.SetFloat(_rippleIDs[i], _rippleActive[i] ? visibleProgress : 0f);
        }
        
        _shieldRenderer.SetPropertyBlock(_propBlock);
    }

    public void OnLaserHit(Vector3 hitPoint)
    {
        // Keep shield visible at reduced alpha so beam effects are visible
        _currentAlpha = 0.7f;
        
        Vector3 localHitPos = transform.InverseTransformPoint(hitPoint);
        
        // Find an inactive slot, or the oldest slot
        int slotToUse = -1;
        
        // First, try to find an inactive slot
        for (int i = 0; i < MAX_RIPPLES; i++)
        {
            if (!_rippleActive[i])
            {
                slotToUse = i;
                break;
            }
        }
        
        // If all slots active, use round-robin
        if (slotToUse == -1)
        {
            slotToUse = _nextRippleIndex;
            _nextRippleIndex = (_nextRippleIndex + 1) % MAX_RIPPLES;
        }
        
        _hitPositions[slotToUse] = localHitPos;
        _rippleProgress[slotToUse] = 0f;
        _rippleActive[slotToUse] = true;
    }
}