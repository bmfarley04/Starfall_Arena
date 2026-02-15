using System.Collections.Generic;
using UnityEngine;


// Used AI heavily for this feature
[System.Serializable]
public struct RingOfFireConfig
{
    public List<Wave> waves;

    [Header("Game Logic")]
    [Tooltip("Auto-start Ring of Fire when map is enabled")]
    public bool autoStart;

    [Header("Line Renderer Visuals")]
    [Tooltip("Material for the line (Use Particles/Additive or similar)")]
    public Material fireLineMaterial;
    [Tooltip("Width of the fire line")]
    public float lineWidth;

    [Header("Unsafe Area Visuals")]
    [Tooltip("Half-extent (in world units) from the safe zone center to use when drawing outside masks. Increase if your map is larger.")]
    public float maskExtent;
    [Tooltip("Material for the outside safe zone masks (optional, will use a default unlit color if not set)")]
    public Material outsideMaskMaterial;
    [Tooltip("Z value for the mask shader to ensure it renders above the map but below the line renderer")]
    public float maskShaderZValue; // Z value for the mask shader to ensure it renders above the map but below the line renderer

    public RingOfFireConfig(bool init)
    {
        waves = null;
        autoStart = true;
        fireLineMaterial = null;
        lineWidth = 0.2f;
        maskExtent = 50f;
        outsideMaskMaterial = null;
        maskShaderZValue = 0.05f;
    }
}

public class RingOfFireManager : MonoBehaviour
{
    [Header("Ring of Fire Configuration")]
    public RingOfFireConfig config;

    // Ring of Fire state
    private bool _ringOfFireActive = false;
    private int _currentWaveIndex = 0;
    private float _waveTimer = 0f;
    private float _lastDamageTickTime;

    // Interpolation vars
    private Vector2 _currentSafeCenter;
    private float _currentSafeWidth;
    private float _currentSafeLength;
    private float _currentSafeRadius; // For circle shapes
    private Vector2 _targetSafeCenter;
    private float _targetSafeWidth;
    private float _targetSafeLength;
    private float _targetSafeRadius; // For circle shapes
    private Vector2 _startSafeCenter;
    private float _startSafeWidth;
    private float _startSafeLength;
    private float _startSafeRadius; // For circle shapes

    // Circle setting
    private const int _circleSegments = 128;

    // Current shape type tracking
    private WaveShapeType _currentShapeType = WaveShapeType.Box;

    // OPTIMIZATION: Replaced pool with LineRenderer
    private LineRenderer _lineRenderer;
    private GameObject _lineObj;

    // OPTIMIZATION: Cached list of entities to damage
    private List<Entity> _cachedEntities = new List<Entity>();
    private float _lastCacheUpdateTime;
    private const float CACHE_UPDATE_INTERVAL = 1.0f;

    private WaveBox _currentSafeBox;

    // Unsafe-area mask objects
    private GameObject _maskParent;
    private MeshRenderer _maskRenderer;
    private Material _maskMaterial;

    void OnEnable()
    {
        if (config.autoStart && config.waves != null && config.waves.Count > 0)
        {
            StartRingOfFire();
        }
    }

    void Update()
    {
        if (_ringOfFireActive)
            UpdateRingOfFire();
    }

    #region Ring of Fire

    [ContextMenu("Start Ring of Fire")]
    public void StartRingOfFire()
    {
        if (config.waves == null || config.waves.Count == 0)
        {
            Debug.LogWarning("Cannot start Ring of Fire: No waves configured!");
            return;
        }

        // Auto-chain waves based on current wave's autoChainWithPrevious setting
        for (int i = 1; i < config.waves.Count; i++)
        {
            Wave currentWave = config.waves[i];
            
            // Only chain if the current wave has autoChainWithPrevious enabled
            if (currentWave.autoChainWithPrevious)
            {
                Wave previousWave = config.waves[i - 1];

                // Determine area for previous wave's safe shape and its end-center shape
                float safeArea = 0f;
                float endArea = 0f;
                Vector2 safeCenter = previousWave.GetSafeZoneCenter();
                Vector2 endCenter = previousWave.GetEndCenter();

                // Track type and size of the smaller shape
                bool safeIsBox = previousWave.shapeType == WaveShapeType.Box;
                bool endIsBox = previousWave.shapeType == WaveShapeType.Box; // same type in this Wave design

                float safeWidth = 0f, safeLength = 0f, safeRadius = 0f;
                float endWidth = 0f, endLength = 0f, endRadius = 0f;

                if (safeIsBox)
                {
                    safeWidth = previousWave.safeBox.width;
                    safeLength = previousWave.safeBox.length;
                    safeArea = safeWidth * safeLength;
                }
                else
                {
                    safeRadius = previousWave.safeCircle.radius;
                    safeArea = Mathf.PI * safeRadius * safeRadius;
                }

                if (endIsBox)
                {
                    endWidth = previousWave.endCenterBox.width;
                    endLength = previousWave.endCenterBox.length;
                    endArea = endWidth * endLength;
                }
                else
                {
                    endRadius = previousWave.endCenterCircle.radius;
                    endArea = Mathf.PI * endRadius * endRadius;
                }

                // Choose the smaller area and chain to its center
                bool chooseSafe = safeArea <= endArea;
                Vector2 chosenCenter = chooseSafe ? safeCenter : endCenter;

                // Determine chosen shape parameters
                bool chosenIsBox = chooseSafe ? safeIsBox : endIsBox;
                float chosenWidth = chooseSafe ? safeWidth : endWidth;
                float chosenLength = chooseSafe ? safeLength : endLength;
                float chosenRadius = chooseSafe ? safeRadius : endRadius;

                // Set the next wave's safe center to the chosen center
                currentWave.SetSafeZoneCenter(chosenCenter);

                // Lock the end center to the same spot so this wave doesn't move away
                currentWave.SetEndCenter(currentWave.GetSafeZoneCenter());

                // Also adjust the next wave's end-center size so it matches the chosen (smaller) area as closely as possible.
                if (currentWave.shapeType == WaveShapeType.Box)
                {
                    if (chosenIsBox)
                    {
                        // Direct copy of box dimensions
                        currentWave.endCenterBox.width = chosenWidth;
                        currentWave.endCenterBox.length = chosenLength;
                    }
                    else
                    {
                        // Convert circle -> box by using diameter as both width and length (square) to roughly match center footprint
                        float diameter = chosenRadius * 2f;
                        currentWave.endCenterBox.width = diameter;
                        currentWave.endCenterBox.length = diameter;
                    }
                }
                else // current wave is Circle
                {
                    if (chosenIsBox)
                    {
                        // Convert box -> circle by fitting a circle inside the box: use half the smaller box side
                        float radius = Mathf.Min(chosenWidth, chosenLength) / 2f;
                        currentWave.endCenterCircle.radius = radius;
                    }
                    else
                    {
                        // Direct copy of circle radius
                        currentWave.endCenterCircle.radius = chosenRadius;
                    }
                }
            }
        }

        _currentWaveIndex = 0;
        _waveTimer = 0f;
        _ringOfFireActive = true;
        _lastDamageTickTime = Time.time;
        _lastCacheUpdateTime = -10f; // Force update immediately

        Wave firstWave = config.waves[0];
        _currentShapeType = firstWave.shapeType;
        
        if (_currentShapeType == WaveShapeType.Box)
        {
            _currentSafeCenter = firstWave.safeBox.centerPoint;
            _currentSafeWidth = firstWave.safeBox.width;
            _currentSafeLength = firstWave.safeBox.length;
            _currentSafeBox = new WaveBox(_currentSafeCenter, _currentSafeWidth, _currentSafeLength);
        }
        else
        {
            _currentSafeCenter = firstWave.safeCircle.centerPoint;
            _currentSafeRadius = firstWave.safeCircle.radius;
        }

        _startSafeCenter = _currentSafeCenter;
        _startSafeWidth = _currentSafeWidth;
        _startSafeLength = _currentSafeLength;
        _startSafeRadius = _currentSafeRadius;

        SetNextTargetSafeZone();
        InitializeLineRenderer();
        InitializeSafeZoneMask();

        Debug.Log($"Ring of Fire started! Wave 1/{config.waves.Count}");
    }

    [ContextMenu("Stop Ring of Fire")]
    public void StopRingOfFire()
    {
        _ringOfFireActive = false;
        if (_lineObj != null) _lineObj.SetActive(false);
        if (_maskParent != null) _maskParent.SetActive(false);
        Debug.Log("Ring of Fire stopped!");
    }

    private void UpdateRingOfFire()
    {
        if (_currentWaveIndex >= config.waves.Count) return;

        _waveTimer += Time.deltaTime;

        UpdateSafeZoneInterpolation();
        
        if (_currentShapeType == WaveShapeType.Box)
        {
            _currentSafeBox = new WaveBox(_currentSafeCenter, _currentSafeWidth, _currentSafeLength);
        }

        // Update the visual line
        UpdateLineRendererVisuals();

        // Get current wave's damage settings
        Wave currentWave = config.waves[_currentWaveIndex];
        float tickInterval = currentWave.damageTickInterval > 0 ? currentWave.damageTickInterval : 0.5f;
        
        if (Time.time - _lastDamageTickTime >= tickInterval)
        {
            ApplyFireDamage();
            _lastDamageTickTime = Time.time;
        }

        CheckWaveTransition();
    }

    private void SetNextTargetSafeZone()
    {
        if (_currentWaveIndex >= config.waves.Count) return;

        Wave currentWave = config.waves[_currentWaveIndex];
        _currentShapeType = currentWave.shapeType;
        
        if (_currentShapeType == WaveShapeType.Box)
        {
            _targetSafeCenter = currentWave.endCenterBox.GetRandomPoint();
            _targetSafeWidth = currentWave.safeBox.width;
            _targetSafeLength = currentWave.safeBox.length;
        }
        else
        {
            _targetSafeCenter = currentWave.endCenterCircle.GetRandomPoint();
            _targetSafeRadius = currentWave.safeCircle.radius;
        }
    }

    private void UpdateSafeZoneInterpolation()
    {
        if (_currentWaveIndex >= config.waves.Count) return;

        Wave currentWave = config.waves[_currentWaveIndex];
        
        // If stationary is enabled, don't interpolate - keep the shape at its starting position
        if (currentWave.stationary)
        {
            _currentSafeCenter = _startSafeCenter;
            _currentSafeWidth = _startSafeWidth;
            _currentSafeLength = _startSafeLength;
            _currentSafeRadius = _startSafeRadius;
            return;
        }
        
        float progress = Mathf.Clamp01(_waveTimer / currentWave.duration);
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

        _currentSafeCenter = Vector2.Lerp(_startSafeCenter, _targetSafeCenter, smoothProgress);
        _currentSafeWidth = Mathf.Lerp(_startSafeWidth, _targetSafeWidth, smoothProgress);
        _currentSafeLength = Mathf.Lerp(_startSafeLength, _targetSafeLength, smoothProgress);
        _currentSafeRadius = Mathf.Lerp(_startSafeRadius, _targetSafeRadius, smoothProgress);
    }

    private void CheckWaveTransition()
    {
        if (_currentWaveIndex >= config.waves.Count) return;

        Wave currentWave = config.waves[_currentWaveIndex];

        if (_waveTimer >= currentWave.duration)
        {
            _currentWaveIndex++;
            _waveTimer = 0f;

            if (_currentWaveIndex < config.waves.Count)
            {
                _startSafeCenter = _currentSafeCenter;
                _startSafeWidth = _currentSafeWidth;
                _startSafeLength = _currentSafeLength;
                _startSafeRadius = _currentSafeRadius;
                SetNextTargetSafeZone();
            }
        }
    }

    private void InitializeLineRenderer()
    {
        if (_lineObj == null)
        {
            _lineObj = new GameObject("FireRing_Line");
            _lineObj.transform.SetParent(transform, false);
            _lineRenderer = _lineObj.AddComponent<LineRenderer>();
        }

        _lineObj.SetActive(true);

        // Set to world space so we don't have to worry about the MapManager's scale/rotation
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = true;
        
        // Set position count based on shape type
        _lineRenderer.positionCount = _currentShapeType == WaveShapeType.Box ? 4 : _circleSegments;

        // Use a basic unlit material if none is provided
        if (config.fireLineMaterial != null)
        {
            _lineRenderer.material = config.fireLineMaterial;
        }
        else
        {
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _lineRenderer.startWidth = config.lineWidth > 0 ? config.lineWidth : 0.2f;
        _lineRenderer.endWidth = _lineRenderer.startWidth;

        // Remove complex texture modes
        _lineRenderer.textureMode = LineTextureMode.Stretch;
        _lineRenderer.alignment = LineAlignment.View;
    }

    private void UpdateLineRendererVisuals()
    {
        if (_lineRenderer == null) return;

        if (_currentShapeType == WaveShapeType.Box)
        {
            UpdateBoxLineRenderer();
        }
        else
        {
            UpdateCircleLineRenderer();
        }

        UpdateSafeZoneMasks();
    }
    
    private void UpdateBoxLineRenderer()
    {
        if (_lineRenderer.positionCount != 4)
            _lineRenderer.positionCount = 4;
            
        float hw = _currentSafeWidth / 2f;
        float hl = _currentSafeLength / 2f;
        float x = _currentSafeCenter.x;
        float y = _currentSafeCenter.y;
        float z = -0.1f; // Slightly in front of the map

        // Simple rectangular boundary
        _lineRenderer.SetPosition(0, new Vector3(x - hw, y + hl, z)); // Top Left
        _lineRenderer.SetPosition(1, new Vector3(x + hw, y + hl, z)); // Top Right
        _lineRenderer.SetPosition(2, new Vector3(x + hw, y - hl, z)); // Bottom Right
        _lineRenderer.SetPosition(3, new Vector3(x - hw, y - hl, z)); // Bottom Left
    }
    
    private void UpdateCircleLineRenderer()
    {
        const int segments = _circleSegments;
        if (_lineRenderer.positionCount != segments)
            _lineRenderer.positionCount = segments;
            
        float z = -0.1f;
        
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = _currentSafeCenter.x + Mathf.Cos(angle) * _currentSafeRadius;
            float y = _currentSafeCenter.y + Mathf.Sin(angle) * _currentSafeRadius;
            _lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }

    private void UpdateSafeZoneMasks()
    {
        if (_maskParent == null || _maskRenderer == null || _maskMaterial == null) return;
        
        if (!_maskParent.activeSelf) 
            _maskParent.SetActive(true);
        
        // Ensure the quad's world Z stays at the configured value while its X/Y follow the parent (shape)
        if (_maskRenderer != null && _maskParent != null)
        {
            var quadTransform = _maskRenderer.transform;
            Vector3 parentWorld = _maskParent.transform.position;
            quadTransform.position = new Vector3(parentWorld.x, parentWorld.y, config.maskShaderZValue);
        }
        
        // Update shader properties based on current shape
        if (_currentShapeType == WaveShapeType.Box)
        {
            _maskMaterial.SetFloat("_ShapeType", 0f); // Box
            _maskMaterial.SetVector("_SafeCenter", new Vector4(_currentSafeCenter.x, _currentSafeCenter.y, 0f, 0f));
            _maskMaterial.SetVector("_SafeSize", new Vector4(_currentSafeWidth, _currentSafeLength, 0f, 0f));
        }
        else // Circle
        {
            _maskMaterial.SetFloat("_ShapeType", 1f); // Circle
            _maskMaterial.SetVector("_SafeCenter", new Vector4(_currentSafeCenter.x, _currentSafeCenter.y, 0f, 0f));
            _maskMaterial.SetVector("_SafeSize", new Vector4(0f, 0f, _currentSafeRadius, 0f));
        }
    }

    private void InitializeSafeZoneMask()
    {
        if (_maskParent == null)
        {
            _maskParent = new GameObject("FireRing_Mask");
            _maskParent.transform.SetParent(transform, false);

            // Create single full-screen quad
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Mask_Shader";
            
            // Remove collider
            var col = quad.GetComponent<Collider>();
            if (col != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) 
                    DestroyImmediate(col);
                else
#endif
                    Destroy(col);
            }

            quad.transform.SetParent(_maskParent.transform, false);
            
            // Position and scale to cover the entire play area
            float extent = config.maskExtent > 0f ? config.maskExtent : 50f;
            quad.transform.localPosition = Vector3.zero;
            // Set world Z to the configured value while preserving world X/Y
            quad.transform.position = new Vector3(_currentSafeCenter.x, _currentSafeCenter.y, config.maskShaderZValue);
            quad.transform.localScale = new Vector3(extent * 2f, extent * 2f, 1f);
            
            _maskRenderer = quad.GetComponent<MeshRenderer>();
            
            // Create material with custom shader
            Shader maskShader = Shader.Find("Custom/RingOfFireMask");
            if (maskShader == null)
            {
                Debug.LogError("RingOfFireMask shader not found! Make sure RingOfFireMask.shader is in Assets/Shaders/");
                maskShader = Shader.Find("Sprites/Default");
            }
            
            _maskMaterial = new Material(maskShader);
            
            // Set default color from config or use fallback
            if (config.outsideMaskMaterial != null && config.outsideMaskMaterial.HasProperty("_Color"))
            {
                _maskMaterial.SetColor("_Color", config.outsideMaskMaterial.GetColor("_Color"));
            }
            else
            {
                // Fallback: semi-transparent red
                _maskMaterial.SetColor("_Color", new Color(1f, 0f, 0f, 0.3f));
            }
            
            _maskRenderer.material = _maskMaterial;
            _maskRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _maskRenderer.receiveShadows = false;
        }

        _maskParent.SetActive(true);
    }

    private void ApplyFireDamage()
    {
        if (_currentWaveIndex >= config.waves.Count) return;

        Wave currentWave = config.waves[_currentWaveIndex];
        float tickInterval = currentWave.damageTickInterval > 0 ? currentWave.damageTickInterval : 0.5f;
        float damageThisTick = currentWave.fireDamage * tickInterval;

        // Update cache periodically to find new spawned players
        if (Time.time - _lastCacheUpdateTime > CACHE_UPDATE_INTERVAL)
        {
            RefreshEntityCache();
        }

        // Iterate backwards in case entities were destroyed
        for (int i = _cachedEntities.Count - 1; i >= 0; i--)
        {
            Entity entity = _cachedEntities[i];

            // Clean up nulls
            if (entity == null || entity.gameObject == null)
            {
                _cachedEntities.RemoveAt(i);
                continue;
            }

            if (!entity.gameObject.activeInHierarchy) continue;

            if (!IsInsideSafeZone(entity.transform.position))
            {
                entity.TakeDamage(damageThisTick, 0f, entity.transform.position, DamageSource.Other);
            }
        }
    }

    private void RefreshEntityCache()
    {
        _cachedEntities.Clear();
        string[] tagsToFind = new string[] { "Player1", "Player2", "Player", "Enemy" };

        foreach (var tag in tagsToFind)
        {
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(tag); }
            catch { continue; }

            foreach (var obj in found)
            {
                var ent = obj.GetComponent<Entity>();
                if (ent != null) _cachedEntities.Add(ent);
            }
        }
        _lastCacheUpdateTime = Time.time;
    }

    public bool IsInsideSafeZone(Vector3 position)
    {
        if (!_ringOfFireActive) return true;

        if (_currentShapeType == WaveShapeType.Box)
        {
            float halfWidth = _currentSafeWidth / 2f;
            float halfLength = _currentSafeLength / 2f;

            return position.x >= (_currentSafeCenter.x - halfWidth) &&
                   position.x <= (_currentSafeCenter.x + halfWidth) &&
                   position.y >= (_currentSafeCenter.y - halfLength) &&
                   position.y <= (_currentSafeCenter.y + halfLength);
        }
        else
        {
            // Circle check
            float dx = position.x - _currentSafeCenter.x;
            float dy = position.y - _currentSafeCenter.y;
            return (dx * dx + dy * dy) <= (_currentSafeRadius * _currentSafeRadius);
        }
    }

    public bool IsRingOfFireActive() => _ringOfFireActive;

    public Rect GetCurrentSafeZoneBounds()
    {
        if (_currentShapeType == WaveShapeType.Box)
        {
            return new Rect(
                _currentSafeCenter.x - _currentSafeWidth / 2f,
                _currentSafeCenter.y - _currentSafeLength / 2f,
                _currentSafeWidth,
                _currentSafeLength
            );
        }
        else
        {
            // Return bounding box of circle
            return new Rect(
                _currentSafeCenter.x - _currentSafeRadius,
                _currentSafeCenter.y - _currentSafeRadius,
                _currentSafeRadius * 2f,
                _currentSafeRadius * 2f
            );
        }
    }
    
    public WaveShapeType GetCurrentShapeType() => _currentShapeType;
    
    public float GetCurrentSafeRadius() => _currentSafeRadius;

    public int GetCurrentWaveIndex() => _currentWaveIndex;

    public float GetCurrentWaveProgress()
    {
        if (!_ringOfFireActive || _currentWaveIndex >= config.waves.Count) return 1f;

        Wave currentWave = config.waves[_currentWaveIndex];
        return Mathf.Clamp01(_waveTimer / currentWave.duration);
    }
    #endregion
}
