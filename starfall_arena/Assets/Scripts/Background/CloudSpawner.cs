using UnityEngine;

/// <summary>
/// Spawns space clouds as GameObjects with configurable gradients and sizes.
/// Dynamically updates when parameters change in play mode.
/// Clouds shift from calm to menacing colors as enemies are killed.
/// </summary>
public class CloudSpawner : MonoBehaviour
{
    /// <summary>
    /// Stores deterministic color data for each cloud to enable real-time gradient shifts.
    /// </summary>
    public class CloudData : MonoBehaviour
    {
        public float gradientPosition; // Position on gradient (0-1)
        public float alpha; // Alpha value for this cloud
    }

    [System.Serializable]
    public class GradientPreset
    {
        public string name;
        public Gradient gradient;

        public GradientPreset(string name)
        {
            this.name = name;
            this.gradient = new Gradient();
        }
    }

    [Header("Cloud Configuration")]
    [SerializeField] private Texture2D cloudTexture;
    [SerializeField] private Material cloudMaterial;
    [SerializeField] private int cloudCount = 20;

    [Header("Spawn Area (600x400)")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(600f, 400f);
    [SerializeField] private bool visualizeSpawnArea = true;

    [Header("Distribution")]
    [SerializeField] private bool useGridDistribution = true;
    [SerializeField] private Vector2 gridJitter = new Vector2(50f, 50f);
    [SerializeField] private int randomSeed = 0;

    [Header("Cloud Size")]
    [SerializeField] private Vector2 sizeRange = new Vector2(10f, 15f);

    [Header("Color Gradient")]
    [SerializeField] private bool usePreset = true;
    [SerializeField] private int selectedPresetIndex = 0;
    [SerializeField] private GradientPreset[] presets;
    [SerializeField] private Gradient customGradient;

    [Header("Cloud Appearance")]
    [SerializeField] private Vector2 alphaRange = new Vector2(0.3f, 0.7f);
    [SerializeField] private float rotationMin = 0f;
    [SerializeField] private float rotationMax = 360f;

    [Header("Menace Gradient Shift")]
    [SerializeField] private Gradient calmGradient;
    [SerializeField] private Gradient menacingGradient;
    [SerializeField] private float menaceProgress = 0f;
    [SerializeField] private float menaceStepPerKill = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float maxMenaceProgress = 1f;

    private GameObject cloudContainer;
    private Texture2D lastTexture;
    private Material lastMaterial;
    private int lastCloudCount;
    private Vector2 lastSpawnAreaSize;
    private Vector2 lastSizeRange;
    private bool lastUsePreset;
    private int lastSelectedPresetIndex;
    private Gradient lastCustomGradient;
    private Vector2 lastAlphaRange;
    private bool lastUseGridDistribution;
    private Vector2 lastGridJitter;
    private int lastRandomSeed;

    void Start()
    {
        InitializePresets();
        InitializeMenaceGradients();
        SpawnClouds();
        CacheParameters();
    }

    void OnValidate()
    {
        // Ensure selected preset is in valid range
        if (presets != null && presets.Length > 0)
        {
            selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presets.Length - 1);
        }
    }

    void Update()
    {
        // Check if any parameters changed
        if (HasParametersChanged())
        {
            SpawnClouds();
            CacheParameters();
        }
    }

    void InitializePresets()
    {
        if (presets == null || presets.Length == 0)
        {
            presets = new GradientPreset[5];

            // Preset 0: Purple Nebula
            presets[0] = new GradientPreset("Purple Nebula");
            presets[0].gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.4f, 0.2f, 0.8f), 0.0f),
                    new GradientColorKey(new Color(0.7f, 0.3f, 0.9f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.2f, 0.6f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            // Preset 1: Blue Nebula
            presets[1] = new GradientPreset("Blue Nebula");
            presets[1].gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.3f, 0.8f), 0.0f),
                    new GradientColorKey(new Color(0.3f, 0.6f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.4f, 0.7f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            // Preset 2: Pink-Red Nebula
            presets[2] = new GradientPreset("Pink-Red Nebula");
            presets[2].gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.9f, 0.2f, 0.4f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.4f, 0.6f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.3f, 0.5f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            // Preset 3: Green-Cyan Nebula
            presets[3] = new GradientPreset("Green-Cyan Nebula");
            presets[3].gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.2f, 0.8f, 0.6f), 0.0f),
                    new GradientColorKey(new Color(0.3f, 1.0f, 0.8f), 0.5f),
                    new GradientColorKey(new Color(0.1f, 0.7f, 0.5f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );

            // Preset 4: Orange-Yellow Nebula
            presets[4] = new GradientPreset("Orange-Yellow Nebula");
            presets[4].gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.5f, 0.1f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.8f, 0.3f), 0.5f),
                    new GradientColorKey(new Color(0.9f, 0.6f, 0.2f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
        }

        // Initialize custom gradient if null
        if (customGradient == null)
        {
            customGradient = new Gradient();
            customGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
        }
    }

    void InitializeMenaceGradients()
    {
        // Initialize calm gradient (blue nebula)
        if (calmGradient == null)
        {
            calmGradient = new Gradient();
            calmGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.3f, 0.8f), 0.0f),
                    new GradientColorKey(new Color(0.3f, 0.6f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.4f, 0.7f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
        }

        // Initialize menacing gradient (red/orange tones)
        if (menacingGradient == null)
        {
            menacingGradient = new Gradient();
            menacingGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.8f, 0.1f, 0.1f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.3f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(0.9f, 0.2f, 0.1f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
        }
    }

    void SpawnClouds()
    {
        // Clean up old clouds
        if (cloudContainer != null)
        {
            if (Application.isPlaying)
            {
                Destroy(cloudContainer);
            }
            else
            {
                DestroyImmediate(cloudContainer);
            }
        }

        // Validate requirements
        if (cloudTexture == null || cloudMaterial == null)
        {
            Debug.LogWarning($"CloudSpawner on {gameObject.name}: Missing texture or material");
            return;
        }

        // Set random seed for reproducible results
        Random.InitState(randomSeed);

        // Create container
        cloudContainer = new GameObject("Cloud Container");
        cloudContainer.transform.SetParent(transform);
        cloudContainer.transform.localPosition = Vector3.zero;

        // Get active gradient
        Gradient activeGradient = usePreset && presets != null && presets.Length > 0
            ? presets[selectedPresetIndex].gradient
            : customGradient;

        // Spawn clouds
        for (int i = 0; i < cloudCount; i++)
        {
            CreateCloud(i, activeGradient);
        }
    }

    void CreateCloud(int index, Gradient gradient)
    {
        // Create GameObject
        GameObject cloud = new GameObject($"Cloud_{index}");
        cloud.transform.SetParent(cloudContainer.transform);
        
        // Set cloud layer to match spawner
        cloud.layer = gameObject.layer;

        // Position - grid or random
        Vector2 position;
        if (useGridDistribution)
        {
            // Calculate grid dimensions
            int gridCols = Mathf.CeilToInt(Mathf.Sqrt(cloudCount * (spawnAreaSize.x / spawnAreaSize.y)));
            int gridRows = Mathf.CeilToInt((float)cloudCount / gridCols);

            // Calculate cell size
            float cellWidth = spawnAreaSize.x / gridCols;
            float cellHeight = spawnAreaSize.y / gridRows;

            // Get grid position
            int row = index / gridCols;
            int col = index % gridCols;

            // Center position in cell
            float baseX = -spawnAreaSize.x / 2f + cellWidth * (col + 0.5f);
            float baseY = -spawnAreaSize.y / 2f + cellHeight * (row + 0.5f);

            // Add jitter
            float jitterX = Random.Range(-gridJitter.x, gridJitter.x);
            float jitterY = Random.Range(-gridJitter.y, gridJitter.y);

            position = new Vector2(baseX + jitterX, baseY + jitterY);
        }
        else
        {
            // Pure random distribution
            position = new Vector2(
                Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
                Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f)
            );
        }

        cloud.transform.localPosition = new Vector3(position.x, position.y, 0);

        // Random size
        float size = Random.Range(sizeRange.x, sizeRange.y);
        cloud.transform.localScale = new Vector3(size, size, 1f);

        // Random rotation
        float rotation = Random.Range(rotationMin, rotationMax);
        cloud.transform.localRotation = Quaternion.Euler(0, 0, rotation);

        // Add SpriteRenderer
        SpriteRenderer sr = cloud.AddComponent<SpriteRenderer>();

        // Create sprite from texture
        Sprite cloudSprite = Sprite.Create(
            cloudTexture,
            new Rect(0, 0, cloudTexture.width, cloudTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sr.sprite = cloudSprite;
        sr.material = cloudMaterial;

        // Sample color from gradient
        float gradientPosition = Random.Range(0f, 1f);
        float alpha = Random.Range(alphaRange.x, alphaRange.y);

        // Store gradient data in CloudData component for later updates
        CloudData cloudData = cloud.AddComponent<CloudData>();
        cloudData.gradientPosition = gradientPosition;
        cloudData.alpha = alpha;

        // Evaluate color using blended gradient based on menace progress
        Color sampledColor = EvaluateMenaceGradient(gradientPosition);
        sampledColor.a = alpha;

        sr.color = sampledColor;

        // Set sorting layer/order if needed
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -10;
    }

    /// <summary>
    /// Evaluates the current blended gradient between calm and menacing based on menace progress.
    /// </summary>
    Color EvaluateMenaceGradient(float position)
    {
        if (calmGradient == null || menacingGradient == null)
        {
            return Color.white;
        }

        Color calmColor = calmGradient.Evaluate(position);
        Color menacingColor = menacingGradient.Evaluate(position);

        return Color.Lerp(calmColor, menacingColor, menaceProgress);
    }

    /// <summary>
    /// Increments menace progress and updates all cloud colors. Call this on each enemy kill.
    /// </summary>
    public void IncrementMenace()
    {
        menaceProgress = Mathf.Min(menaceProgress + menaceStepPerKill, maxMenaceProgress);
        UpdateCloudColors();
    }

    /// <summary>
    /// Updates colors of all existing clouds based on current menace progress.
    /// </summary>
    void UpdateCloudColors()
    {
        if (cloudContainer == null) return;

        CloudData[] cloudDataArray = cloudContainer.GetComponentsInChildren<CloudData>();

        foreach (CloudData cloudData in cloudDataArray)
        {
            SpriteRenderer sr = cloudData.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color newColor = EvaluateMenaceGradient(cloudData.gradientPosition);
                newColor.a = cloudData.alpha;
                sr.color = newColor;
            }
        }
    }

    /// <summary>
    /// Resets menace progress to 0 and updates cloud colors. Call this on game restart.
    /// </summary>
    public void ResetMenace()
    {
        menaceProgress = 0f;
        UpdateCloudColors();
    }

    bool HasParametersChanged()
    {
        return cloudTexture != lastTexture ||
               cloudMaterial != lastMaterial ||
               cloudCount != lastCloudCount ||
               spawnAreaSize != lastSpawnAreaSize ||
               sizeRange != lastSizeRange ||
               usePreset != lastUsePreset ||
               selectedPresetIndex != lastSelectedPresetIndex ||
               alphaRange != lastAlphaRange ||
               useGridDistribution != lastUseGridDistribution ||
               gridJitter != lastGridJitter ||
               randomSeed != lastRandomSeed ||
               (!usePreset && !GradientsEqual(customGradient, lastCustomGradient));
    }

    void CacheParameters()
    {
        lastTexture = cloudTexture;
        lastMaterial = cloudMaterial;
        lastCloudCount = cloudCount;
        lastSpawnAreaSize = spawnAreaSize;
        lastSizeRange = sizeRange;
        lastUsePreset = usePreset;
        lastSelectedPresetIndex = selectedPresetIndex;
        lastAlphaRange = alphaRange;
        lastUseGridDistribution = useGridDistribution;
        lastGridJitter = gridJitter;
        lastRandomSeed = randomSeed;
        lastCustomGradient = CopyGradient(customGradient);
    }

    bool GradientsEqual(Gradient a, Gradient b)
    {
        if (a == null || b == null) return a == b;

        if (a.colorKeys.Length != b.colorKeys.Length) return false;
        if (a.alphaKeys.Length != b.alphaKeys.Length) return false;

        for (int i = 0; i < a.colorKeys.Length; i++)
        {
            if (a.colorKeys[i].color != b.colorKeys[i].color ||
                !Mathf.Approximately(a.colorKeys[i].time, b.colorKeys[i].time))
                return false;
        }

        for (int i = 0; i < a.alphaKeys.Length; i++)
        {
            if (!Mathf.Approximately(a.alphaKeys[i].alpha, b.alphaKeys[i].alpha) ||
                !Mathf.Approximately(a.alphaKeys[i].time, b.alphaKeys[i].time))
                return false;
        }

        return true;
    }

    Gradient CopyGradient(Gradient source)
    {
        if (source == null) return null;

        Gradient copy = new Gradient();
        copy.SetKeys(source.colorKeys, source.alphaKeys);
        return copy;
    }

    void OnDrawGizmosSelected()
    {
        if (visualizeSpawnArea)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0));
        }
    }
}
