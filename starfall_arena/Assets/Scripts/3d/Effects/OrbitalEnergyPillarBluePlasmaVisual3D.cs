using UnityEngine;
using UnityEngine.Rendering;

public class OrbitalEnergyPillarBluePlasmaVisual3D : OrbitalEnergyPillarVisual3D
{
    private const int PillarMeshSegments = 64;

    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int LayerModeId = Shader.PropertyToID("_LayerMode");
    private static readonly int LayerIntensityId = Shader.PropertyToID("_LayerIntensity");
    private static readonly int ExternalIntensityId = Shader.PropertyToID("_ExternalIntensity");
    private static readonly int SparkSeedId = Shader.PropertyToID("_SparkSeed");

    [Header("Blue Plasma Orbs")]
    [Tooltip("Optional glowing orb prefab launched from the carrier face. Assign WhiteDwarfStar3D to keep the existing launch orb look.")]
    [SerializeField] private GameObject blueOrbPrefab;

    [Tooltip("Fallback material used when Orb Prefab is empty or has unassigned renderers.")]
    [SerializeField] private Material blueFallbackOrbMaterial;

    [Tooltip("World-space scale applied to each launched orb.")]
    [SerializeField] [Min(0.01f)] private float blueOrbScale = 10f;

    [Tooltip("Additional world units forward from the carrier origin where orbs visually launch.")]
    [SerializeField] [Min(0f)] private float blueLaunchOffset = 18f;

    [Header("Blue Plasma Links")]
    [Tooltip("Additive material used by the carrier-to-orb energy links. These remain simple telegraph links.")]
    [SerializeField] private Material blueLinkMaterial;

    [Tooltip("Maximum link width while the pillars are charging.")]
    [SerializeField] [Min(0f)] private float blueLinkWidth = 1.3f;

    [Tooltip("HDR color used by link LineRenderers when no material-specific color is authored.")]
    [SerializeField] private Color blueLinkColor = new Color(0.55f, 2.4f, 7f, 1f);

    [Header("Blue Plasma Body")]
    [Tooltip("Editable prefab for one pillar body. Expected child renderer names are CoreVolume, CloudShell, and RimGlow.")]
    [SerializeField] private GameObject pillarBodyPrefab;

    [Tooltip("Fallback material for generated or missing CoreVolume renderers.")]
    [SerializeField] private Material coreVolumeMaterial;

    [Tooltip("Fallback material for generated or missing CloudShell renderers.")]
    [SerializeField] private Material cloudShellMaterial;

    [Tooltip("Fallback material for generated or missing RimGlow renderers.")]
    [SerializeField] private Material rimGlowMaterial;

    [Tooltip("Visible cylinder height in world units. Keep much taller than the arena so the top and bottom read as endless.")]
    [SerializeField] [Min(1f)] private float bluePillarVisualHeight = 6000f;

    [Tooltip("Maximum opacity multiplier sent to the blue plasma body shaders.")]
    [SerializeField] [Range(0f, 2f)] private float bluePillarOpacity = 0.86f;

    [Tooltip("Radius multiplier for the dense white-blue center volume.")]
    [SerializeField] [Range(0.05f, 1f)] private float blueCoreRadiusScale = 0.5f;

    [Tooltip("Radius multiplier for the cloudy translucent plasma shell.")]
    [SerializeField] [Range(0.25f, 2f)] private float blueShellRadiusScale = 0.98f;

    [Tooltip("Radius multiplier for the bright cyan-white rim shell.")]
    [SerializeField] [Range(0.5f, 3f)] private float blueRimRadiusScale = 1.04f;

    [Tooltip("Brightness multiplier for the core volume layer.")]
    [SerializeField] [Min(0f)] private float blueCoreLayerIntensity = 0.72f;

    [Tooltip("Brightness multiplier for the cloudy shell layer.")]
    [SerializeField] [Min(0f)] private float blueShellLayerIntensity = 1.05f;

    [Tooltip("Brightness multiplier for the rim glow layer.")]
    [SerializeField] [Min(0f)] private float blueRimLayerIntensity = 1.75f;

    [Header("Major Internal Lightning")]
    [Tooltip("Material used by large white-blue internal lightning ribbon meshes.")]
    [SerializeField] private Material lightningRibbonMaterial;

    [Tooltip("Number of large internal lightning channels generated inside each pillar.")]
    [SerializeField] [Range(0, 12)] private int majorBoltCountPerPillar = 5;

    [Tooltip("Number of jagged line segments per major bolt. Use powers of two for the strongest fractal subdivision read.")]
    [SerializeField] [Range(4, 32)] private int majorBoltSegments = 16;

    [Tooltip("World-space ribbon width for major internal lightning channels.")]
    [SerializeField] [Min(0.01f)] private float majorBoltWidth = 7.5f;

    [Tooltip("Minimum vertical height covered by a major bolt in world units.")]
    [SerializeField] [Min(0f)] private float majorBoltVerticalSpanMin = 520f;

    [Tooltip("Maximum vertical height covered by a major bolt in world units.")]
    [SerializeField] [Min(0f)] private float majorBoltVerticalSpanMax = 1450f;

    [Tooltip("Maximum distance from the pillar centerline where internal major bolts may start or end, expressed as a fraction of pillar radius.")]
    [SerializeField] [Range(0.05f, 1.2f)] private float majorBoltRadiusFraction = 0.72f;

    [Tooltip("Maximum angular wander for large internal lightning channels.")]
    [SerializeField] [Range(0f, 360f)] private float majorBoltAngleWander = 95f;

    [Tooltip("How far recursive midpoint displacement can push major channels sideways, as a fraction of pillar radius.")]
    [SerializeField] [Range(0f, 2f)] private float majorBoltJaggedness = 0.85f;

    [Tooltip("How often major channels choose a new jagged path.")]
    [SerializeField] [Min(0.03f)] private float majorBoltRetargetInterval = 0.42f;

    [Tooltip("Intensity multiplier applied to major lightning channels.")]
    [SerializeField] [Min(0f)] private float majorBoltIntensity = 3.7f;

    [Header("Branch Lightning")]
    [Tooltip("Number of smaller branch lightning ribbons generated inside each pillar.")]
    [SerializeField] [Range(0, 48)] private int branchBoltCountPerPillar = 24;

    [Tooltip("Number of jagged line segments per branch bolt.")]
    [SerializeField] [Range(2, 16)] private int branchBoltSegments = 8;

    [Tooltip("World-space ribbon width for smaller branch bolts.")]
    [SerializeField] [Min(0.01f)] private float branchBoltWidth = 3.1f;

    [Tooltip("Minimum branch length in world units.")]
    [SerializeField] [Min(0f)] private float branchBoltLengthMin = 90f;

    [Tooltip("Maximum branch length in world units.")]
    [SerializeField] [Min(0f)] private float branchBoltLengthMax = 420f;

    [Tooltip("Maximum distance from the pillar centerline where branches may live, expressed as a fraction of pillar radius.")]
    [SerializeField] [Range(0.05f, 1.2f)] private float branchBoltRadiusFraction = 0.9f;

    [Tooltip("How far recursive midpoint displacement can push branch channels sideways, as a fraction of pillar radius.")]
    [SerializeField] [Range(0f, 2f)] private float branchBoltJaggedness = 0.65f;

    [Tooltip("How often branch channels choose new jagged paths.")]
    [SerializeField] [Min(0.03f)] private float branchBoltRetargetInterval = 0.16f;

    [Tooltip("Intensity multiplier applied to branch lightning channels.")]
    [SerializeField] [Min(0f)] private float branchBoltIntensity = 2.4f;

    [Header("Spark Field")]
    [Tooltip("Material used by tiny blue-white glints inside each pillar.")]
    [SerializeField] private Material sparkMaterial;

    [Tooltip("Number of pooled spark/glint billboards per pillar.")]
    [SerializeField] [Range(0, 160)] private int sparkCountPerPillar = 96;

    [Tooltip("Base world-space size for tiny spark/glint billboards.")]
    [SerializeField] [Min(0.01f)] private float sparkSize = 4.2f;

    [Tooltip("How quickly sparks drift vertically through the pillar.")]
    [SerializeField] private float sparkVerticalDriftSpeed = 72f;

    [Tooltip("Maximum distance from the pillar centerline where sparks may appear, expressed as a fraction of pillar radius.")]
    [SerializeField] [Range(0.05f, 1.2f)] private float sparkRadiusFraction = 0.92f;

    [Tooltip("Intensity multiplier applied to the spark field.")]
    [SerializeField] [Min(0f)] private float sparkIntensity = 1.45f;

    private GameObject[] _orbInstances;
    private Transform[] _orbTransforms;
    private GameObject[] _pillarInstances;
    private Transform[] _pillarTransforms;
    private Transform[] _coreTransforms;
    private Transform[] _shellTransforms;
    private Transform[] _rimTransforms;
    private Renderer[] _coreRenderers;
    private Renderer[] _shellRenderers;
    private Renderer[] _rimRenderers;
    private LineRenderer[] _links;
    private LightningRibbon[][] _majorBolts;
    private LightningRibbon[][] _branchBolts;
    private SparkField[] _sparkFields;
    private Vector3[] _pillarCenters;
    private MaterialPropertyBlock _propertyBlock;
    private Mesh _sharedPillarMesh;
    private Material _runtimeOrbMaterial;
    private Material _runtimeLinkMaterial;
    private Material _runtimeBodyMaterial;
    private Material _runtimeLightningMaterial;
    private Material _runtimeSparkMaterial;
    private Camera _mainCamera;

    private Vector3 _origin;
    private Vector3 _faceForward;
    private Vector3 _gapDirection;
    private int _activeCount;
    private float _ringRadius;
    private float _gapDegrees;
    private float _pillarRadius;
    private float _travelDuration;
    private float _linkDuration;
    private float _expandDuration;
    private float _activeDuration;
    private float _fadeDuration;
    private float _localStartTime;
    private bool _isPlaying;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        StopImmediate();
    }

    private void OnDestroy()
    {
        if (_sharedPillarMesh != null)
        {
            Destroy(_sharedPillarMesh);
            _sharedPillarMesh = null;
        }

        DisposeRibbonPools(_majorBolts);
        DisposeRibbonPools(_branchBolts);

        if (_sparkFields == null)
        {
            return;
        }

        for (int i = 0; i < _sparkFields.Length; i++)
        {
            _sparkFields[i]?.Dispose();
        }
    }

    private void OnValidate()
    {
        blueOrbScale = Mathf.Max(0.01f, blueOrbScale);
        blueLaunchOffset = Mathf.Max(0f, blueLaunchOffset);
        blueLinkWidth = Mathf.Max(0f, blueLinkWidth);
        bluePillarVisualHeight = Mathf.Max(1f, bluePillarVisualHeight);
        bluePillarOpacity = Mathf.Clamp(bluePillarOpacity, 0f, 2f);
        blueCoreRadiusScale = Mathf.Clamp(blueCoreRadiusScale, 0.05f, 1f);
        blueShellRadiusScale = Mathf.Clamp(blueShellRadiusScale, 0.25f, 2f);
        blueRimRadiusScale = Mathf.Clamp(blueRimRadiusScale, 0.5f, 3f);
        blueCoreLayerIntensity = Mathf.Max(0f, blueCoreLayerIntensity);
        blueShellLayerIntensity = Mathf.Max(0f, blueShellLayerIntensity);
        blueRimLayerIntensity = Mathf.Max(0f, blueRimLayerIntensity);
        majorBoltCountPerPillar = Mathf.Clamp(majorBoltCountPerPillar, 0, 12);
        branchBoltCountPerPillar = Mathf.Clamp(branchBoltCountPerPillar, 0, 48);
        majorBoltSegments = Mathf.Clamp(majorBoltSegments, 4, 32);
        branchBoltSegments = Mathf.Clamp(branchBoltSegments, 2, 16);
        majorBoltWidth = Mathf.Max(0.01f, majorBoltWidth);
        branchBoltWidth = Mathf.Max(0.01f, branchBoltWidth);
        majorBoltVerticalSpanMin = Mathf.Max(0f, majorBoltVerticalSpanMin);
        majorBoltVerticalSpanMax = Mathf.Max(majorBoltVerticalSpanMin, majorBoltVerticalSpanMax);
        branchBoltLengthMin = Mathf.Max(0f, branchBoltLengthMin);
        branchBoltLengthMax = Mathf.Max(branchBoltLengthMin, branchBoltLengthMax);
        majorBoltRetargetInterval = Mathf.Max(0.03f, majorBoltRetargetInterval);
        branchBoltRetargetInterval = Mathf.Max(0.03f, branchBoltRetargetInterval);
        majorBoltIntensity = Mathf.Max(0f, majorBoltIntensity);
        branchBoltIntensity = Mathf.Max(0f, branchBoltIntensity);
        sparkCountPerPillar = Mathf.Clamp(sparkCountPerPillar, 0, 160);
        sparkSize = Mathf.Max(0.01f, sparkSize);
        sparkIntensity = Mathf.Max(0f, sparkIntensity);
    }

    public override void Play(
        Vector3 origin,
        Vector3 faceForward,
        Vector3 gapDirection,
        int pillarCount,
        float ringRadius,
        float gapDegrees,
        float pillarRadius,
        float travelDuration,
        float linkDuration,
        float expandDuration,
        float activeDuration,
        float fadeDuration,
        float elapsed)
    {
        _activeCount = Mathf.Max(0, pillarCount);
        if (_activeCount <= 0)
        {
            StopImmediate();
            return;
        }

        _origin = origin;
        _faceForward = ResolvePlanarDirection(faceForward, Vector3.forward);
        _gapDirection = ResolvePlanarDirection(gapDirection, _faceForward);
        _ringRadius = Mathf.Max(0f, ringRadius);
        _gapDegrees = Mathf.Clamp(gapDegrees, 0f, 330f);
        _pillarRadius = Mathf.Max(0.01f, pillarRadius);
        _travelDuration = Mathf.Max(0.01f, travelDuration);
        _linkDuration = Mathf.Max(0f, linkDuration);
        _expandDuration = Mathf.Max(0.01f, expandDuration);
        _activeDuration = Mathf.Max(0.01f, activeDuration);
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _localStartTime = Time.time - Mathf.Max(0f, elapsed);
        _isPlaying = true;

        EnsurePools(_activeCount);
        BuildPillarCenters();

        for (int i = 0; i < _activeCount; i++)
        {
            SetVisualActive(i, true);
        }

        for (int i = _activeCount; _orbInstances != null && i < _orbInstances.Length; i++)
        {
            SetVisualActive(i, false);
        }

        TickVisuals();
    }

    public override void StopImmediate()
    {
        _isPlaying = false;
        if (_orbInstances == null)
        {
            return;
        }

        for (int i = 0; i < _orbInstances.Length; i++)
        {
            SetVisualActive(i, false);
        }
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        TickVisuals();
    }

    private void TickVisuals()
    {
        float elapsed = Time.time - _localStartTime;
        float activeStart = _travelDuration + _linkDuration + _expandDuration;
        float fadeStart = activeStart + _activeDuration;
        float totalDuration = fadeStart + _fadeDuration;

        if (elapsed >= totalDuration)
        {
            StopImmediate();
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        Vector3 launchPosition = _origin + _faceForward * blueLaunchOffset;
        float travelT = Mathf.Clamp01(elapsed / _travelDuration);
        float linkT = _linkDuration > 0f ? Mathf.Clamp01((elapsed - _travelDuration) / _linkDuration) : (elapsed >= _travelDuration ? 1f : 0f);
        float expandT = Mathf.Clamp01((elapsed - _travelDuration - _linkDuration) / _expandDuration);
        float fadeT = _fadeDuration > 0f ? Mathf.Clamp01((elapsed - fadeStart) / _fadeDuration) : 0f;
        float visibleAlpha = 1f - fadeT;
        bool linksVisible = elapsed >= _travelDuration;
        bool pillarsVisible = elapsed >= _travelDuration + _linkDuration;

        for (int pillarIndex = 0; pillarIndex < _activeCount; pillarIndex++)
        {
            Vector3 center = _pillarCenters[pillarIndex];
            Vector3 orbPosition = Vector3.Lerp(launchPosition, center, EaseInOut(travelT));
            float chargePulse = 1f + Mathf.Sin((Time.time * 8.5f) + pillarIndex) * 0.08f * Mathf.Max(linkT, expandT);

            if (_orbTransforms[pillarIndex] != null)
            {
                _orbTransforms[pillarIndex].position = orbPosition;
                _orbTransforms[pillarIndex].localScale = Vector3.one * blueOrbScale * chargePulse * visibleAlpha;
            }

            UpdateLink(pillarIndex, launchPosition, center, linksVisible, linkT, visibleAlpha);

            bool showPillar = pillarsVisible && visibleAlpha > 0f;
            if (_pillarInstances[pillarIndex] != null)
            {
                _pillarInstances[pillarIndex].SetActive(showPillar);
            }

            float currentRadius = Mathf.Max(0.01f, _pillarRadius * EaseOut(expandT));
            if (_pillarTransforms[pillarIndex] != null)
            {
                _pillarTransforms[pillarIndex].SetPositionAndRotation(center, Quaternion.identity);
            }

            ApplyLayerTransform(_coreTransforms[pillarIndex], currentRadius, blueCoreRadiusScale);
            ApplyLayerTransform(_shellTransforms[pillarIndex], currentRadius, blueShellRadiusScale);
            ApplyLayerTransform(_rimTransforms[pillarIndex], currentRadius, blueRimRadiusScale);

            float layerOpacity = bluePillarOpacity * visibleAlpha;
            ApplyLayerProperties(_coreRenderers[pillarIndex], expandT, layerOpacity, 0f, blueCoreLayerIntensity);
            ApplyLayerProperties(_shellRenderers[pillarIndex], expandT, layerOpacity, 1f, blueShellLayerIntensity);
            ApplyLayerProperties(_rimRenderers[pillarIndex], expandT, layerOpacity, 2f, blueRimLayerIntensity);

            UpdateLightningAndSparks(pillarIndex, center, currentRadius, elapsed, showPillar, visibleAlpha * expandT);
        }
    }

    private void UpdateLink(int index, Vector3 start, Vector3 end, bool visible, float linkT, float alpha)
    {
        LineRenderer line = _links[index];
        if (line == null)
        {
            return;
        }

        line.enabled = visible && alpha > 0f;
        if (!line.enabled)
        {
            return;
        }

        line.startWidth = blueLinkWidth * Mathf.Max(0.05f, linkT) * alpha;
        line.endWidth = line.startWidth;
        Color startColor = blueLinkColor;
        startColor.a = Mathf.Clamp01(linkT * alpha);
        Color endColor = new Color(0.88f, 1f, 1f, startColor.a);
        line.startColor = startColor;
        line.endColor = endColor;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void EnsurePools(int count)
    {
        if (_orbInstances != null && _orbInstances.Length >= count)
        {
            EnsureLightningPools();
            EnsureSparkPools();
            return;
        }

        int oldCount = _orbInstances != null ? _orbInstances.Length : 0;
        int newCount = Mathf.Max(count, oldCount);
        System.Array.Resize(ref _orbInstances, newCount);
        System.Array.Resize(ref _orbTransforms, newCount);
        System.Array.Resize(ref _pillarInstances, newCount);
        System.Array.Resize(ref _pillarTransforms, newCount);
        System.Array.Resize(ref _coreTransforms, newCount);
        System.Array.Resize(ref _shellTransforms, newCount);
        System.Array.Resize(ref _rimTransforms, newCount);
        System.Array.Resize(ref _coreRenderers, newCount);
        System.Array.Resize(ref _shellRenderers, newCount);
        System.Array.Resize(ref _rimRenderers, newCount);
        System.Array.Resize(ref _links, newCount);
        System.Array.Resize(ref _majorBolts, newCount);
        System.Array.Resize(ref _branchBolts, newCount);
        System.Array.Resize(ref _sparkFields, newCount);
        System.Array.Resize(ref _pillarCenters, newCount);

        for (int i = oldCount; i < newCount; i++)
        {
            CreateOrb(i);
            CreateLink(i);
            CreatePillarBody(i);
            SetVisualActive(i, false);
        }

        EnsureLightningPools();
        EnsureSparkPools();
    }

    private void EnsureLightningPools()
    {
        if (_majorBolts == null || _branchBolts == null)
        {
            return;
        }

        for (int pillarIndex = 0; pillarIndex < _majorBolts.Length; pillarIndex++)
        {
            EnsureRibbonPool(ref _majorBolts[pillarIndex], majorBoltCountPerPillar, majorBoltSegments, pillarIndex, "Major");
            EnsureRibbonPool(ref _branchBolts[pillarIndex], branchBoltCountPerPillar, branchBoltSegments, pillarIndex, "Branch");
        }
    }

    private void EnsureRibbonPool(ref LightningRibbon[] ribbons, int desiredCount, int segmentCount, int pillarIndex, string label)
    {
        int oldCount = ribbons != null ? ribbons.Length : 0;
        if (oldCount >= desiredCount)
        {
            return;
        }

        System.Array.Resize(ref ribbons, desiredCount);
        Material material = lightningRibbonMaterial != null ? lightningRibbonMaterial : ResolveRuntimeLightningMaterial();
        for (int i = oldCount; i < desiredCount; i++)
        {
            ribbons[i] = new LightningRibbon();
            ribbons[i].Initialize(transform, material, Mathf.Max(2, segmentCount), $"Blue Plasma {label} Bolt {pillarIndex + 1}-{i + 1}");
            ribbons[i].SetActive(false);
        }
    }

    private void EnsureSparkPools()
    {
        if (_sparkFields == null)
        {
            return;
        }

        for (int pillarIndex = 0; pillarIndex < _sparkFields.Length; pillarIndex++)
        {
            if (_sparkFields[pillarIndex] != null)
            {
                continue;
            }

            _sparkFields[pillarIndex] = new SparkField();
            _sparkFields[pillarIndex].Initialize(
                transform,
                sparkMaterial != null ? sparkMaterial : ResolveRuntimeSparkMaterial(),
                sparkCountPerPillar,
                $"Blue Plasma Spark Field {pillarIndex + 1}");
            _sparkFields[pillarIndex].SetActive(false);
        }
    }

    private void CreateOrb(int index)
    {
        GameObject instance = blueOrbPrefab != null
            ? Instantiate(blueOrbPrefab, transform)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        instance.name = $"Blue Plasma Pillar Orb {index + 1}";
        instance.transform.SetParent(transform, false);
        Collider orbCollider = instance.GetComponent<Collider>();
        if (orbCollider != null)
        {
            Destroy(orbCollider);
        }

        if (blueFallbackOrbMaterial != null || blueOrbPrefab == null)
        {
            Material material = blueFallbackOrbMaterial != null ? blueFallbackOrbMaterial : ResolveRuntimeOrbMaterial();
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial == null)
                {
                    renderers[i].sharedMaterial = material;
                }
            }
        }

        _orbInstances[index] = instance;
        _orbTransforms[index] = instance.transform;
    }

    private void CreateLink(int index)
    {
        GameObject linkObject = new GameObject($"Blue Plasma Pillar Link {index + 1}");
        linkObject.transform.SetParent(transform, false);
        LineRenderer line = linkObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.sharedMaterial = blueLinkMaterial != null ? blueLinkMaterial : ResolveRuntimeLinkMaterial();
        _links[index] = line;
    }

    private void CreatePillarBody(int index)
    {
        if (pillarBodyPrefab != null && TryCreatePillarBodyFromPrefab(index))
        {
            return;
        }

        GameObject pillarRoot = new GameObject($"Blue Plasma Pillar Body {index + 1}");
        pillarRoot.transform.SetParent(transform, false);
        _pillarInstances[index] = pillarRoot;
        _pillarTransforms[index] = pillarRoot.transform;

        CreateBodyLayer(pillarRoot.transform, "CoreVolume", coreVolumeMaterial, out _coreTransforms[index], out _coreRenderers[index]);
        CreateBodyLayer(pillarRoot.transform, "CloudShell", cloudShellMaterial, out _shellTransforms[index], out _shellRenderers[index]);
        CreateBodyLayer(pillarRoot.transform, "RimGlow", rimGlowMaterial, out _rimTransforms[index], out _rimRenderers[index]);
    }

    private bool TryCreatePillarBodyFromPrefab(int index)
    {
        GameObject pillarRoot = Instantiate(pillarBodyPrefab, transform);
        if (pillarRoot == null)
        {
            return false;
        }

        pillarRoot.name = $"Blue Plasma Pillar Body {index + 1}";
        pillarRoot.transform.SetParent(transform, false);
        _pillarInstances[index] = pillarRoot;
        _pillarTransforms[index] = pillarRoot.transform;

        _coreRenderers[index] = FindLayerRenderer(pillarRoot.transform, "CoreVolume", out _coreTransforms[index]);
        _shellRenderers[index] = FindLayerRenderer(pillarRoot.transform, "CloudShell", out _shellTransforms[index]);
        _rimRenderers[index] = FindLayerRenderer(pillarRoot.transform, "RimGlow", out _rimTransforms[index]);

        if (_coreRenderers[index] == null || _shellRenderers[index] == null || _rimRenderers[index] == null)
        {
            Destroy(pillarRoot);
            _pillarInstances[index] = null;
            _pillarTransforms[index] = null;
            _coreTransforms[index] = null;
            _shellTransforms[index] = null;
            _rimTransforms[index] = null;
            _coreRenderers[index] = null;
            _shellRenderers[index] = null;
            _rimRenderers[index] = null;
            return false;
        }

        ConfigureBodyLayer(_coreRenderers[index], coreVolumeMaterial);
        ConfigureBodyLayer(_shellRenderers[index], cloudShellMaterial);
        ConfigureBodyLayer(_rimRenderers[index], rimGlowMaterial);
        return true;
    }

    private void CreateBodyLayer(Transform parent, string layerName, Material material, out Transform layerTransform, out Renderer layerRenderer)
    {
        GameObject layerObject = new GameObject(layerName);
        layerObject.transform.SetParent(parent, false);
        MeshFilter meshFilter = layerObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = layerObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = ResolvePillarMesh();
        meshRenderer.sharedMaterial = material != null ? material : ResolveRuntimeBodyMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        layerTransform = layerObject.transform;
        layerRenderer = meshRenderer;
    }

    private Renderer FindLayerRenderer(Transform root, string layerName, out Transform layerTransform)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root || child.name != layerName)
            {
                continue;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                layerTransform = child;
                return renderer;
            }
        }

        layerTransform = null;
        return null;
    }

    private void ConfigureBodyLayer(Renderer renderer, Material fallbackMaterial)
    {
        if (renderer == null)
        {
            return;
        }

        if (renderer.TryGetComponent(out MeshFilter meshFilter))
        {
            meshFilter.sharedMesh = ResolvePillarMesh();
        }

        if (renderer.sharedMaterial == null)
        {
            renderer.sharedMaterial = fallbackMaterial != null ? fallbackMaterial : ResolveRuntimeBodyMaterial();
        }

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void SetVisualActive(int index, bool active)
    {
        if (_orbInstances != null && index < _orbInstances.Length && _orbInstances[index] != null)
        {
            _orbInstances[index].SetActive(active);
        }

        if (_pillarInstances != null && index < _pillarInstances.Length && _pillarInstances[index] != null)
        {
            _pillarInstances[index].SetActive(false);
        }

        if (_links != null && index < _links.Length && _links[index] != null)
        {
            _links[index].enabled = false;
        }

        SetRibbonPoolActive(_majorBolts, index, false);
        SetRibbonPoolActive(_branchBolts, index, false);

        if (_sparkFields != null && index < _sparkFields.Length && _sparkFields[index] != null)
        {
            _sparkFields[index].SetActive(false);
        }
    }

    private void ApplyLayerTransform(Transform layerTransform, float radius, float radiusScale)
    {
        if (layerTransform == null)
        {
            return;
        }

        float visualRadius = Mathf.Max(0.01f, radius * radiusScale);
        layerTransform.localPosition = Vector3.zero;
        layerTransform.localRotation = Quaternion.identity;
        layerTransform.localScale = new Vector3(visualRadius, bluePillarVisualHeight * 0.5f, visualRadius);
    }

    private void ApplyLayerProperties(Renderer renderer, float reveal, float opacity, float layerMode, float layerIntensity)
    {
        if (renderer == null)
        {
            return;
        }

        _propertyBlock.Clear();
        renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(RevealId, Mathf.Clamp01(reveal));
        _propertyBlock.SetFloat(OpacityId, Mathf.Max(0f, opacity));
        _propertyBlock.SetFloat(LayerModeId, layerMode);
        _propertyBlock.SetFloat(LayerIntensityId, Mathf.Max(0f, layerIntensity));
        renderer.SetPropertyBlock(_propertyBlock);
    }

    private void UpdateLightningAndSparks(int pillarIndex, Vector3 center, float radius, float elapsed, bool showPillar, float opacity)
    {
        bool active = showPillar && opacity > 0.001f && _mainCamera != null;
        UpdateRibbonPool(
            _majorBolts,
            pillarIndex,
            active,
            center,
            radius,
            elapsed,
            opacity,
            true);

        UpdateRibbonPool(
            _branchBolts,
            pillarIndex,
            active,
            center,
            radius,
            elapsed,
            opacity,
            false);

        if (_sparkFields != null && pillarIndex < _sparkFields.Length && _sparkFields[pillarIndex] != null)
        {
            if (!active)
            {
                _sparkFields[pillarIndex].SetActive(false);
                return;
            }

            _sparkFields[pillarIndex].UpdateField(
                center,
                radius * sparkRadiusFraction,
                bluePillarVisualHeight,
                sparkSize,
                elapsed,
                sparkVerticalDriftSpeed,
                opacity * sparkIntensity,
                pillarIndex,
                _mainCamera);
        }
    }

    private void UpdateRibbonPool(
        LightningRibbon[][] pool,
        int pillarIndex,
        bool active,
        Vector3 center,
        float radius,
        float elapsed,
        float opacity,
        bool major)
    {
        if (pool == null || pillarIndex >= pool.Length || pool[pillarIndex] == null)
        {
            return;
        }

        LightningRibbon[] ribbons = pool[pillarIndex];
        int activeCount = major ? majorBoltCountPerPillar : branchBoltCountPerPillar;
        for (int i = 0; i < ribbons.Length; i++)
        {
            LightningRibbon ribbon = ribbons[i];
            if (ribbon == null)
            {
                continue;
            }

            if (!active || i >= activeCount)
            {
                ribbon.SetActive(false);
                continue;
            }

            float retarget = major ? majorBoltRetargetInterval : branchBoltRetargetInterval;
            float intensity = opacity * (major ? majorBoltIntensity : branchBoltIntensity);
            float width = major ? majorBoltWidth : branchBoltWidth;
            int seedBase = (major ? 1500007 : 2500009) ^ ((pillarIndex + 1) * 73856093) ^ ((i + 1) * 19349663);

            if (major)
            {
                ribbon.UpdateMajor(
                    center,
                    radius * majorBoltRadiusFraction,
                    bluePillarVisualHeight,
                    majorBoltVerticalSpanMin,
                    majorBoltVerticalSpanMax,
                    majorBoltAngleWander,
                    majorBoltJaggedness,
                    width,
                    elapsed,
                    retarget,
                    intensity,
                    seedBase,
                    _mainCamera);
            }
            else
            {
                ribbon.UpdateBranch(
                    center,
                    radius * branchBoltRadiusFraction,
                    bluePillarVisualHeight,
                    branchBoltLengthMin,
                    branchBoltLengthMax,
                    branchBoltJaggedness,
                    width,
                    elapsed,
                    retarget,
                    intensity,
                    seedBase,
                    _mainCamera);
            }
        }
    }

    private void BuildPillarCenters()
    {
        if (_pillarCenters == null)
        {
            return;
        }

        float coveredArc = Mathf.Max(0f, 360f - _gapDegrees);
        for (int i = 0; i < _activeCount; i++)
        {
            float angle;
            if (_activeCount <= 1)
            {
                angle = 180f;
            }
            else if (_gapDegrees <= 0.01f)
            {
                angle = i * (360f / _activeCount);
            }
            else
            {
                float t = i / (float)(_activeCount - 1);
                angle = (_gapDegrees * 0.5f) + t * coveredArc;
            }

            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * _gapDirection;
            _pillarCenters[i] = _origin + direction.normalized * _ringRadius;
        }
    }

    private Mesh ResolvePillarMesh()
    {
        if (_sharedPillarMesh != null)
        {
            return _sharedPillarMesh;
        }

        int vertexCount = (PillarMeshSegments + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[PillarMeshSegments * 6];

        for (int i = 0; i <= PillarMeshSegments; i++)
        {
            float t = i / (float)PillarMeshSegments;
            float angle = t * Mathf.PI * 2f;
            Vector3 normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            int bottom = i * 2;
            int top = bottom + 1;
            vertices[bottom] = new Vector3(normal.x, -1f, normal.z);
            vertices[top] = new Vector3(normal.x, 1f, normal.z);
            normals[bottom] = normal;
            normals[top] = normal;
            uvs[bottom] = new Vector2(t, 0f);
            uvs[top] = new Vector2(t, 1f);

            if (i >= PillarMeshSegments)
            {
                continue;
            }

            int tri = i * 6;
            triangles[tri] = bottom;
            triangles[tri + 1] = top;
            triangles[tri + 2] = bottom + 2;
            triangles[tri + 3] = top;
            triangles[tri + 4] = top + 2;
            triangles[tri + 5] = bottom + 2;
        }

        _sharedPillarMesh = new Mesh { name = "BluePlasmaPillarOpenCylinder" };
        _sharedPillarMesh.vertices = vertices;
        _sharedPillarMesh.normals = normals;
        _sharedPillarMesh.uv = uvs;
        _sharedPillarMesh.triangles = triangles;
        _sharedPillarMesh.RecalculateBounds();
        return _sharedPillarMesh;
    }

    private Material ResolveRuntimeOrbMaterial()
    {
        if (_runtimeOrbMaterial != null)
        {
            return _runtimeOrbMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        shader = shader != null ? shader : Shader.Find("Sprites/Default");
        shader = shader != null ? shader : Shader.Find("Standard");
        _runtimeOrbMaterial = shader != null ? new Material(shader) : null;
        if (_runtimeOrbMaterial != null)
        {
            _runtimeOrbMaterial.color = Color.white;
        }
        return _runtimeOrbMaterial;
    }

    private Material ResolveRuntimeLinkMaterial()
    {
        if (_runtimeLinkMaterial != null)
        {
            return _runtimeLinkMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        shader = shader != null ? shader : Shader.Find("Sprites/Default");
        shader = shader != null ? shader : Shader.Find("Standard");
        _runtimeLinkMaterial = shader != null ? new Material(shader) : null;
        if (_runtimeLinkMaterial != null)
        {
            _runtimeLinkMaterial.color = blueLinkColor;
        }
        return _runtimeLinkMaterial;
    }

    private Material ResolveRuntimeBodyMaterial()
    {
        if (_runtimeBodyMaterial != null)
        {
            return _runtimeBodyMaterial;
        }

        Shader shader = Shader.Find("Starfall/3D/BluePlasmaPillarVolume");
        shader = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
        _runtimeBodyMaterial = shader != null ? new Material(shader) : null;
        return _runtimeBodyMaterial;
    }

    private Material ResolveRuntimeLightningMaterial()
    {
        if (_runtimeLightningMaterial != null)
        {
            return _runtimeLightningMaterial;
        }

        Shader shader = Shader.Find("Starfall/3D/BluePlasmaLightningRibbon");
        shader = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
        _runtimeLightningMaterial = shader != null ? new Material(shader) : null;
        return _runtimeLightningMaterial;
    }

    private Material ResolveRuntimeSparkMaterial()
    {
        if (_runtimeSparkMaterial != null)
        {
            return _runtimeSparkMaterial;
        }

        Shader shader = Shader.Find("Starfall/3D/BluePlasmaSpark");
        shader = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
        _runtimeSparkMaterial = shader != null ? new Material(shader) : null;
        return _runtimeSparkMaterial;
    }

    private static void DisposeRibbonPools(LightningRibbon[][] pool)
    {
        if (pool == null)
        {
            return;
        }

        for (int i = 0; i < pool.Length; i++)
        {
            LightningRibbon[] ribbons = pool[i];
            if (ribbons == null)
            {
                continue;
            }

            for (int j = 0; j < ribbons.Length; j++)
            {
                ribbons[j]?.Dispose();
            }
        }
    }

    private static void SetRibbonPoolActive(LightningRibbon[][] pool, int pillarIndex, bool active)
    {
        if (pool == null || pillarIndex >= pool.Length || pool[pillarIndex] == null)
        {
            return;
        }

        LightningRibbon[] ribbons = pool[pillarIndex];
        for (int i = 0; i < ribbons.Length; i++)
        {
            ribbons[i]?.SetActive(active);
        }
    }

    private static Vector3 ResolvePlanarDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static float EaseInOut(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOut(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - ((1f - value) * (1f - value));
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float Hash01(int value)
    {
        unchecked
        {
            uint x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00ffffff) / 16777215f;
        }
    }

    private static float SignedHash(int value)
    {
        return Hash01(value) * 2f - 1f;
    }

    private static float RepeatCentered(float value, float halfRange)
    {
        float range = Mathf.Max(0.01f, halfRange) * 2f;
        return Mathf.Repeat(value + halfRange, range) - halfRange;
    }

    private static Vector3 RandomInsideDisk(int seed, float radius)
    {
        float angle = Hash01(seed) * Mathf.PI * 2f;
        float distance = Mathf.Sqrt(Hash01(seed + 17)) * radius;
        return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
    }

    private static Vector3 RandomUnitXZ(int seed)
    {
        float angle = Hash01(seed) * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    private sealed class LightningRibbon
    {
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

        private GameObject _instance;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _points;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private int[] _triangles;
        private int _segments;

        public void Initialize(Transform parent, Material material, int segments, string name)
        {
            _segments = Mathf.Max(2, segments);
            int pointCount = _segments + 1;
            _points = new Vector3[pointCount];
            _vertices = new Vector3[pointCount * 2];
            _uvs = new Vector2[pointCount * 2];
            _triangles = new int[_segments * 6];

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)_segments;
                int vertex = i * 2;
                _uvs[vertex] = new Vector2(t, 0f);
                _uvs[vertex + 1] = new Vector2(t, 1f);

                if (i >= _segments)
                {
                    continue;
                }

                int tri = i * 6;
                _triangles[tri] = vertex;
                _triangles[tri + 1] = vertex + 1;
                _triangles[tri + 2] = vertex + 3;
                _triangles[tri + 3] = vertex;
                _triangles[tri + 4] = vertex + 3;
                _triangles[tri + 5] = vertex + 2;
            }

            _instance = new GameObject(name);
            _instance.transform.SetParent(parent, false);
            MeshFilter meshFilter = _instance.AddComponent<MeshFilter>();
            _renderer = _instance.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = $"{name} Mesh" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;
            meshFilter.sharedMesh = _mesh;
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void SetActive(bool active)
        {
            if (_renderer != null)
            {
                _renderer.enabled = active;
            }
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        public void UpdateMajor(
            Vector3 center,
            float radius,
            float visualHeight,
            float spanMin,
            float spanMax,
            float angleWanderDegrees,
            float jaggedness,
            float width,
            float elapsed,
            float retargetInterval,
            float intensity,
            int seedBase,
            Camera camera)
        {
            float shiftedTime = elapsed + Hash01(seedBase + 3) * retargetInterval;
            int phase = Mathf.FloorToInt(shiftedTime / retargetInterval);
            float phaseT = Mathf.Repeat(shiftedTime / retargetInterval, 1f);
            float flash = ComputeFlashEnvelope(phaseT);
            if (flash <= 0.001f || intensity <= 0.001f)
            {
                SetActive(false);
                return;
            }

            int seed = seedBase ^ (phase * 83492791);
            float verticalLimit = visualHeight * 0.34f;
            float span = Mathf.Min(Mathf.Lerp(spanMin, spanMax, Hash01(seed + 5)), verticalLimit * 2f);
            float baseY = RepeatCentered(SignedHash(seed + 7) * verticalLimit + elapsed * Mathf.Lerp(12f, 45f, Hash01(seed + 9)), verticalLimit);
            float startAngle = Hash01(seed + 11) * Mathf.PI * 2f;
            float endAngle = startAngle + SignedHash(seed + 13) * angleWanderDegrees * Mathf.Deg2Rad;
            Vector3 start = center + RandomInsideDisk(seed + 17, radius * 0.72f) + Vector3.up * (baseY - span * 0.5f);
            Vector3 end = center + RandomInsideDisk(seed + 23, radius * 0.72f) + Vector3.up * (baseY + span * 0.5f);

            Vector3 startRadial = new Vector3(Mathf.Cos(startAngle), 0f, Mathf.Sin(startAngle)) * radius * 0.28f;
            Vector3 endRadial = new Vector3(Mathf.Cos(endAngle), 0f, Mathf.Sin(endAngle)) * radius * 0.28f;
            start += startRadial;
            end += endRadial;

            BuildFractalPath(start, end, center, radius, jaggedness, seed);
            UpdateMesh(camera, width);
            ApplyProperties(intensity * flash);
            SetActive(true);
        }

        public void UpdateBranch(
            Vector3 center,
            float radius,
            float visualHeight,
            float lengthMin,
            float lengthMax,
            float jaggedness,
            float width,
            float elapsed,
            float retargetInterval,
            float intensity,
            int seedBase,
            Camera camera)
        {
            float shiftedTime = elapsed + Hash01(seedBase + 29) * retargetInterval;
            int phase = Mathf.FloorToInt(shiftedTime / retargetInterval);
            float phaseT = Mathf.Repeat(shiftedTime / retargetInterval, 1f);
            float flash = ComputeFlashEnvelope(phaseT);
            if (flash <= 0.001f || intensity <= 0.001f)
            {
                SetActive(false);
                return;
            }

            int seed = seedBase ^ (phase * 193939);
            float verticalLimit = visualHeight * 0.32f;
            Vector3 start = center + RandomInsideDisk(seed + 31, radius * 0.78f);
            start.y += RepeatCentered(SignedHash(seed + 37) * verticalLimit + elapsed * Mathf.Lerp(25f, 90f, Hash01(seed + 41)), verticalLimit);

            Vector3 radial = RandomUnitXZ(seed + 43);
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            float length = Mathf.Lerp(lengthMin, lengthMax, Hash01(seed + 47));
            Vector3 direction = (radial * SignedHash(seed + 53) * 0.42f)
                + (tangent * SignedHash(seed + 59) * 0.55f)
                + (Vector3.up * SignedHash(seed + 61) * 0.78f);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }

            Vector3 end = start + direction.normalized * length;
            end = ClampInsideCylinder(end, center, radius, verticalLimit);
            BuildFractalPath(start, end, center, radius, jaggedness, seed);
            UpdateMesh(camera, width);
            ApplyProperties(intensity * flash);
            SetActive(true);
        }

        private void BuildFractalPath(Vector3 start, Vector3 end, Vector3 center, float radius, float jaggedness, int seed)
        {
            _points[0] = start;
            _points[_segments] = end;

            for (int step = _segments; step > 1; step /= 2)
            {
                int halfStep = step / 2;
                float displacement = radius * jaggedness * (step / (float)_segments);
                for (int i = 0; i < _segments; i += step)
                {
                    int mid = i + halfStep;
                    Vector3 midpoint = (_points[i] + _points[i + step]) * 0.5f;
                    Vector3 offset = RandomUnitXZ(seed + mid * 113 + step * 17) * SignedHash(seed + mid * 41) * displacement;
                    offset += Vector3.up * SignedHash(seed + mid * 67) * displacement * 0.32f;
                    _points[mid] = ClampInsideCylinder(midpoint + offset, center, radius, Mathf.Abs(midpoint.y - center.y) + displacement);
                }
            }

            for (int i = 1; i < _segments; i++)
            {
                if (_points[i] == Vector3.zero)
                {
                    float t = i / (float)_segments;
                    _points[i] = Vector3.Lerp(start, end, t);
                }
            }
        }

        private void UpdateMesh(Camera camera, float width)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 cameraPosition = camera.transform.position;
            for (int i = 0; i <= _segments; i++)
            {
                Vector3 previous = _points[Mathf.Max(0, i - 1)];
                Vector3 next = _points[Mathf.Min(_segments, i + 1)];
                Vector3 tangent = next - previous;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector3.up;
                }

                tangent.Normalize();
                Vector3 toCamera = cameraPosition - _points[i];
                if (toCamera.sqrMagnitude < 0.0001f)
                {
                    toCamera = Vector3.forward;
                }

                toCamera.Normalize();
                Vector3 widthAxis = Vector3.Cross(tangent, toCamera);
                if (widthAxis.sqrMagnitude < 0.0001f)
                {
                    widthAxis = Vector3.Cross(tangent, camera.transform.up);
                }

                if (widthAxis.sqrMagnitude < 0.0001f)
                {
                    widthAxis = Vector3.right;
                }

                widthAxis = widthAxis.normalized * (width * 0.5f);
                int vertex = i * 2;
                _vertices[vertex] = _instance.transform.InverseTransformPoint(_points[i] - widthAxis);
                _vertices[vertex + 1] = _instance.transform.InverseTransformPoint(_points[i] + widthAxis);
            }

            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
        }

        private void ApplyProperties(float intensity)
        {
            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ExternalIntensityId, Mathf.Max(0f, intensity));
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        private static float ComputeFlashEnvelope(float phaseT)
        {
            float attack = Smooth01(phaseT / 0.08f);
            float decay = 1f - Smooth01((phaseT - 0.68f) / 0.32f);
            float flicker = 0.76f + 0.24f * Mathf.Sin(phaseT * Mathf.PI * 22f);
            return Mathf.Clamp01(attack * decay * flicker);
        }

        private static Vector3 ClampInsideCylinder(Vector3 position, Vector3 center, float radius, float halfHeight)
        {
            Vector3 local = position - center;
            Vector2 xz = new Vector2(local.x, local.z);
            float magnitude = xz.magnitude;
            if (magnitude > radius && magnitude > 0.0001f)
            {
                xz = xz / magnitude * radius;
            }

            local.x = xz.x;
            local.z = xz.y;
            local.y = Mathf.Clamp(local.y, -halfHeight, halfHeight);
            return center + local;
        }
    }

    private sealed class SparkField
    {
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

        private GameObject _instance;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private int[] _triangles;
        private int _sparkCount;

        public void Initialize(Transform parent, Material material, int sparkCount, string name)
        {
            _sparkCount = Mathf.Max(0, sparkCount);
            _vertices = new Vector3[_sparkCount * 4];
            _uvs = new Vector2[_sparkCount * 4];
            _triangles = new int[_sparkCount * 6];

            for (int i = 0; i < _sparkCount; i++)
            {
                int vertex = i * 4;
                _uvs[vertex] = new Vector2(0f, 0f);
                _uvs[vertex + 1] = new Vector2(0f, 1f);
                _uvs[vertex + 2] = new Vector2(1f, 1f);
                _uvs[vertex + 3] = new Vector2(1f, 0f);

                int tri = i * 6;
                _triangles[tri] = vertex;
                _triangles[tri + 1] = vertex + 1;
                _triangles[tri + 2] = vertex + 2;
                _triangles[tri + 3] = vertex;
                _triangles[tri + 4] = vertex + 2;
                _triangles[tri + 5] = vertex + 3;
            }

            _instance = new GameObject(name);
            _instance.transform.SetParent(parent, false);
            MeshFilter meshFilter = _instance.AddComponent<MeshFilter>();
            _renderer = _instance.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = $"{name} Mesh" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;
            meshFilter.sharedMesh = _mesh;
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void SetActive(bool active)
        {
            if (_renderer != null)
            {
                _renderer.enabled = active;
            }
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        public void UpdateField(
            Vector3 center,
            float radius,
            float visualHeight,
            float sparkSize,
            float elapsed,
            float verticalDriftSpeed,
            float intensity,
            int pillarIndex,
            Camera camera)
        {
            if (_renderer == null || _mesh == null || camera == null || _sparkCount <= 0 || intensity <= 0.001f)
            {
                SetActive(false);
                return;
            }

            Vector3 cameraRight = camera.transform.right;
            Vector3 cameraUp = camera.transform.up;
            float halfHeight = visualHeight * 0.34f;

            for (int i = 0; i < _sparkCount; i++)
            {
                int seed = ((pillarIndex + 1) * 915488749) ^ ((i + 1) * 734287);
                Vector3 local = RandomInsideDisk(seed + 3, radius);
                local.y = RepeatCentered(SignedHash(seed + 11) * halfHeight + elapsed * verticalDriftSpeed * Mathf.Lerp(0.45f, 1.35f, Hash01(seed + 17)), halfHeight);
                Vector3 position = center + local;

                float pulse = 0.25f + 0.75f * Hash01(Mathf.FloorToInt(elapsed * Mathf.Lerp(6f, 18f, Hash01(seed + 23))) + seed);
                float size = sparkSize * Mathf.Lerp(0.45f, 1.65f, Hash01(seed + 29)) * pulse;
                Vector3 right = cameraRight * size * 0.5f;
                Vector3 up = cameraUp * size * 0.5f;
                int vertex = i * 4;
                _vertices[vertex] = _instance.transform.InverseTransformPoint(position - right - up);
                _vertices[vertex + 1] = _instance.transform.InverseTransformPoint(position - right + up);
                _vertices[vertex + 2] = _instance.transform.InverseTransformPoint(position + right + up);
                _vertices[vertex + 3] = _instance.transform.InverseTransformPoint(position + right - up);
            }

            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ExternalIntensityId, intensity);
            _propertyBlock.SetFloat(SparkSeedId, pillarIndex * 31.17f);
            _renderer.SetPropertyBlock(_propertyBlock);
            SetActive(true);
        }
    }
}
