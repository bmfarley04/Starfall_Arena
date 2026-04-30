using UnityEngine;
using UnityEngine.Rendering;

public class OrbitalEnergyPillarVisual3D : MonoBehaviour
{
    private const int PillarMeshSegments = 64;
    private const int ArcTypeCount = 3;

    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int LayerModeId = Shader.PropertyToID("_LayerMode");
    private static readonly int LayerIntensityId = Shader.PropertyToID("_LayerIntensity");
    private static readonly int BoltLengthId = Shader.PropertyToID("_BoltLength");
    private static readonly int ExternalIntensityId = Shader.PropertyToID("_ExternalIntensity");
    private static readonly int ArcSeedId = Shader.PropertyToID("_ArcSeed");
    private static readonly int BranchChanceId = Shader.PropertyToID("_BranchChance");
    private static readonly int BranchIntensityId = Shader.PropertyToID("_BranchIntensity");

    [Header("Orb Visuals")]
    [Tooltip("Optional glowing orb prefab launched from the carrier face. Assign WhiteDwarfStar3D to reuse the existing white dwarf look.")]
    [SerializeField] private GameObject orbPrefab;

    [Tooltip("Fallback material used when Orb Prefab is empty or has unassigned renderers.")]
    [SerializeField] private Material fallbackOrbMaterial;

    [Tooltip("World-space scale applied to each launched orb.")]
    [SerializeField] [Min(0.01f)] private float orbScale = 10f;

    [Tooltip("Additional world units forward from the carrier origin where orbs visually launch.")]
    [SerializeField] [Min(0f)] private float launchOffset = 18f;

    [Header("Link Visuals")]
    [Tooltip("Additive material used by the carrier-to-orb energy links. These stay as simple straight telegraph links, not the main pillar lightning.")]
    [SerializeField] private Material linkMaterial;

    [Tooltip("Maximum link width while the pillars are charging.")]
    [SerializeField] [Min(0f)] private float linkWidth = 1.4f;

    [Tooltip("HDR color used by link LineRenderers when no material-specific color is authored.")]
    [SerializeField] private Color linkColor = new Color(6f, 0.25f, 0.12f, 1f);

    [Header("Pillar Layer Visuals")]
    [Tooltip("Optional prefab for one pillar body. Use children named Core, Shell, and Halo with MeshRenderers to tune layer materials in prefab mode. Runtime replaces their MeshFilter mesh with the generated open cylinder.")]
    [SerializeField] private GameObject pillarVisualPrefab;

    [Tooltip("Shared material using Starfall/3D/OrbitalEnergyPillar. The script drives core, shell, and halo modes through property blocks.")]
    [SerializeField] private Material pillarMaterial;

    [Tooltip("Visible cylinder height in world units. Keep much taller than the arena so the top and bottom read as endless.")]
    [SerializeField] [Min(1f)] private float pillarVisualHeight = 6000f;

    [Tooltip("Maximum pillar opacity multiplier sent to the layered pillar shader.")]
    [SerializeField] [Range(0f, 2f)] private float pillarOpacity = 1f;

    [Tooltip("Radius multiplier for the white-hot inner core layer relative to the gameplay pillar radius.")]
    [SerializeField] [Range(0.05f, 1f)] private float coreRadiusScale = 0.42f;

    [Tooltip("Brightness multiplier for the white-hot inner core layer.")]
    [SerializeField] [Min(0f)] private float coreLayerIntensity = 1.25f;

    [Tooltip("Radius multiplier for the red/white turbulent shell relative to the gameplay pillar radius.")]
    [SerializeField] [Range(0.25f, 2f)] private float shellRadiusScale = 1f;

    [Tooltip("Brightness multiplier for the turbulent outer shell layer.")]
    [SerializeField] [Min(0f)] private float shellLayerIntensity = 1f;

    [Tooltip("Radius multiplier for the soft outer halo that sells danger at distance.")]
    [SerializeField] [Range(0.5f, 3f)] private float haloRadiusScale = 1.45f;

    [Tooltip("Brightness multiplier for the soft outer halo. Keep lower than the core so texture detail remains visible.")]
    [SerializeField] [Min(0f)] private float haloLayerIntensity = 0.38f;

    [Header("Pillar Lightning Arcs")]
    [Tooltip("Material using Starfall/3D/OrbitalEnergyArc. If empty, the script creates a runtime material from that shader.")]
    [SerializeField] private Material arcMaterial;

    [Tooltip("Number of jagged wraparound arc bolts pooled for each pillar. These are visual only and do not apply damage.")]
    [SerializeField] [Range(0, 24)] private int arcCountPerPillar = 9;

    [Tooltip("Number of centerline segments used by each jagged arc mesh. Higher values allow rougher paths but cost more vertex updates.")]
    [SerializeField] [Range(2, 12)] private int arcSegmentCount = 7;

    [Tooltip("World-space ribbon width for each arc bolt before shader core/glow thinning.")]
    [SerializeField] [Min(0.01f)] private float arcWidth = 5.5f;

    [Tooltip("Shortest arc radius relative to the pillar radius. Values over 1 place arcs just outside the cylinder shell.")]
    [SerializeField] [Min(0.1f)] private float arcOutwardRadiusMin = 1.03f;

    [Tooltip("Largest outward bow relative to the pillar radius. This is what makes arcs leap off the cylinder instead of sticking flat to it.")]
    [SerializeField] [Min(0.1f)] private float arcOutwardRadiusMax = 1.85f;

    [Tooltip("Minimum vertical span in world units for a generated arc.")]
    [SerializeField] [Min(0f)] private float arcVerticalSpanMin = 90f;

    [Tooltip("Maximum vertical span in world units for a generated arc.")]
    [SerializeField] [Min(0f)] private float arcVerticalSpanMax = 340f;

    [Tooltip("Maximum vertical offset from the pillar center where arcs can appear. Keep this near the combat space instead of the full visual height.")]
    [SerializeField] [Min(1f)] private float arcVerticalCenterRange = 460f;

    [Tooltip("How quickly arc groups crawl along world Y before retargeting.")]
    [SerializeField] private float arcVerticalDriftSpeed = 55f;

    [Tooltip("Minimum circumference angle covered by wraparound arcs.")]
    [SerializeField] [Range(0f, 360f)] private float arcWrapAngleMin = 35f;

    [Tooltip("Maximum circumference angle covered by wraparound arcs.")]
    [SerializeField] [Range(0f, 360f)] private float arcWrapAngleMax = 155f;

    [Tooltip("How often each arc chooses a new deterministic jagged path. Low values create a violent crackling read.")]
    [SerializeField] [Min(0.03f)] private float arcRetargetInterval = 0.18f;

    [Tooltip("Portion of each retarget interval used to fade an arc in and out. This avoids hard popping while keeping the lightning snappy.")]
    [SerializeField] [Range(0.01f, 0.49f)] private float arcFadeFraction = 0.16f;

    [Tooltip("Overall brightness multiplier applied to all pillar lightning arcs.")]
    [SerializeField] [Min(0f)] private float arcIntensity = 2.35f;

    [Tooltip("Chance that each arc shader renders short branch forks off its main jagged trunk.")]
    [SerializeField] [Range(0f, 1f)] private float arcBranchChance = 0.75f;

    [Tooltip("Brightness multiplier for branch forks in the arc shader.")]
    [SerializeField] [Min(0f)] private float arcBranchIntensity = 1.1f;

    private GameObject[] _orbInstances;
    private Transform[] _orbTransforms;
    private GameObject[] _pillarRoots;
    private Transform[] _pillarRootTransforms;
    private Transform[] _coreTransforms;
    private Transform[] _shellTransforms;
    private Transform[] _haloTransforms;
    private Renderer[] _coreRenderers;
    private Renderer[] _shellRenderers;
    private Renderer[] _haloRenderers;
    private ArcBolt[][] _arcBoltsByPillar;
    private LineRenderer[] _links;
    private Vector3[] _pillarCenters;
    private MaterialPropertyBlock _propertyBlock;
    private Material _runtimeOrbMaterial;
    private Material _runtimeLinkMaterial;
    private Material _runtimePillarMaterial;
    private Material _runtimeArcMaterial;
    private Mesh _sharedPillarMesh;
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
        }

        if (_arcBoltsByPillar == null)
        {
            return;
        }

        for (int i = 0; i < _arcBoltsByPillar.Length; i++)
        {
            ArcBolt[] arcs = _arcBoltsByPillar[i];
            if (arcs == null)
            {
                continue;
            }

            for (int j = 0; j < arcs.Length; j++)
            {
                if (arcs[j] != null)
                {
                    arcs[j].Dispose();
                }
            }
        }
    }

    private void OnValidate()
    {
        orbScale = Mathf.Max(0.01f, orbScale);
        launchOffset = Mathf.Max(0f, launchOffset);
        linkWidth = Mathf.Max(0f, linkWidth);
        pillarVisualHeight = Mathf.Max(1f, pillarVisualHeight);
        pillarOpacity = Mathf.Clamp(pillarOpacity, 0f, 2f);
        coreRadiusScale = Mathf.Clamp(coreRadiusScale, 0.05f, 1f);
        shellRadiusScale = Mathf.Clamp(shellRadiusScale, 0.25f, 2f);
        haloRadiusScale = Mathf.Clamp(haloRadiusScale, 0.5f, 3f);
        coreLayerIntensity = Mathf.Max(0f, coreLayerIntensity);
        shellLayerIntensity = Mathf.Max(0f, shellLayerIntensity);
        haloLayerIntensity = Mathf.Max(0f, haloLayerIntensity);
        arcCountPerPillar = Mathf.Clamp(arcCountPerPillar, 0, 24);
        arcSegmentCount = Mathf.Clamp(arcSegmentCount, 2, 12);
        arcWidth = Mathf.Max(0.01f, arcWidth);
        arcOutwardRadiusMin = Mathf.Max(0.1f, arcOutwardRadiusMin);
        arcOutwardRadiusMax = Mathf.Max(arcOutwardRadiusMin, arcOutwardRadiusMax);
        arcVerticalSpanMin = Mathf.Max(0f, arcVerticalSpanMin);
        arcVerticalSpanMax = Mathf.Max(arcVerticalSpanMin, arcVerticalSpanMax);
        arcVerticalCenterRange = Mathf.Max(1f, arcVerticalCenterRange);
        arcWrapAngleMin = Mathf.Clamp(arcWrapAngleMin, 0f, 360f);
        arcWrapAngleMax = Mathf.Clamp(Mathf.Max(arcWrapAngleMin, arcWrapAngleMax), 0f, 360f);
        arcRetargetInterval = Mathf.Max(0.03f, arcRetargetInterval);
        arcFadeFraction = Mathf.Clamp(arcFadeFraction, 0.01f, 0.49f);
        arcIntensity = Mathf.Max(0f, arcIntensity);
        arcBranchIntensity = Mathf.Max(0f, arcBranchIntensity);
    }

    public virtual void Play(
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

        EnsurePool(_activeCount);
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

    public virtual void StopImmediate()
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

        Vector3 launchPosition = _origin + _faceForward * launchOffset;
        float travelT = Mathf.Clamp01(elapsed / _travelDuration);
        float linkT = _linkDuration > 0f ? Mathf.Clamp01((elapsed - _travelDuration) / _linkDuration) : (elapsed >= _travelDuration ? 1f : 0f);
        float expandT = Mathf.Clamp01((elapsed - _travelDuration - _linkDuration) / _expandDuration);
        float fadeT = _fadeDuration > 0f ? Mathf.Clamp01((elapsed - fadeStart) / _fadeDuration) : 0f;
        float visibleAlpha = 1f - fadeT;
        bool linksVisible = elapsed >= _travelDuration;
        bool pillarsVisible = elapsed >= _travelDuration + _linkDuration;

        for (int i = 0; i < _activeCount; i++)
        {
            Vector3 center = _pillarCenters[i];
            Vector3 orbPosition = Vector3.Lerp(launchPosition, center, EaseInOut(travelT));
            float chargePulse = 1f + Mathf.Sin((Time.time * 8f) + i) * 0.08f * Mathf.Max(linkT, expandT);

            if (_orbTransforms[i] != null)
            {
                _orbTransforms[i].position = orbPosition;
                _orbTransforms[i].localScale = Vector3.one * orbScale * chargePulse * visibleAlpha;
            }

            if (_links[i] != null)
            {
                _links[i].enabled = linksVisible && visibleAlpha > 0f;
                _links[i].startWidth = linkWidth * Mathf.Max(0.05f, linkT) * visibleAlpha;
                _links[i].endWidth = _links[i].startWidth;
                Color lineColor = linkColor;
                lineColor.a = Mathf.Clamp01(linkT * visibleAlpha);
                _links[i].startColor = lineColor;
                _links[i].endColor = new Color(1f, 1f, 1f, lineColor.a);
                _links[i].SetPosition(0, launchPosition);
                _links[i].SetPosition(1, center);
            }

            bool showPillar = pillarsVisible && visibleAlpha > 0f;
            if (_pillarRoots[i] != null)
            {
                _pillarRoots[i].SetActive(showPillar);
            }

            float currentRadius = Mathf.Max(0.01f, _pillarRadius * EaseOut(expandT));
            if (_pillarRootTransforms[i] != null)
            {
                _pillarRootTransforms[i].SetPositionAndRotation(center, Quaternion.identity);
            }

            ApplyLayerTransform(_coreTransforms[i], currentRadius, coreRadiusScale);
            ApplyLayerTransform(_shellTransforms[i], currentRadius, shellRadiusScale);
            ApplyLayerTransform(_haloTransforms[i], currentRadius, haloRadiusScale);

            float layerOpacity = pillarOpacity * visibleAlpha;
            ApplyLayerProperties(_coreRenderers[i], expandT, layerOpacity, 0f, coreLayerIntensity);
            ApplyLayerProperties(_shellRenderers[i], expandT, layerOpacity, 1f, shellLayerIntensity);
            ApplyLayerProperties(_haloRenderers[i], expandT, layerOpacity, 2f, haloLayerIntensity);

            UpdateArcs(i, center, currentRadius, elapsed, showPillar, visibleAlpha * expandT);
        }
    }

    private void EnsurePool(int count)
    {
        if (_orbInstances != null && _orbInstances.Length >= count)
        {
            EnsureArcPools();
            return;
        }

        int oldCount = _orbInstances != null ? _orbInstances.Length : 0;
        int newCount = Mathf.Max(count, oldCount);
        System.Array.Resize(ref _orbInstances, newCount);
        System.Array.Resize(ref _orbTransforms, newCount);
        System.Array.Resize(ref _pillarRoots, newCount);
        System.Array.Resize(ref _pillarRootTransforms, newCount);
        System.Array.Resize(ref _coreTransforms, newCount);
        System.Array.Resize(ref _shellTransforms, newCount);
        System.Array.Resize(ref _haloTransforms, newCount);
        System.Array.Resize(ref _coreRenderers, newCount);
        System.Array.Resize(ref _shellRenderers, newCount);
        System.Array.Resize(ref _haloRenderers, newCount);
        System.Array.Resize(ref _arcBoltsByPillar, newCount);
        System.Array.Resize(ref _links, newCount);
        System.Array.Resize(ref _pillarCenters, newCount);

        for (int i = oldCount; i < newCount; i++)
        {
            CreateOrb(i);
            CreateLink(i);
            CreatePillar(i);
            SetVisualActive(i, false);
        }

        EnsureArcPools();
    }

    private void EnsureArcPools()
    {
        if (_arcBoltsByPillar == null)
        {
            return;
        }

        for (int pillarIndex = 0; pillarIndex < _arcBoltsByPillar.Length; pillarIndex++)
        {
            ArcBolt[] arcs = _arcBoltsByPillar[pillarIndex];
            int oldCount = arcs != null ? arcs.Length : 0;
            if (oldCount >= arcCountPerPillar)
            {
                continue;
            }

            System.Array.Resize(ref arcs, arcCountPerPillar);
            _arcBoltsByPillar[pillarIndex] = arcs;

            for (int arcIndex = oldCount; arcIndex < arcCountPerPillar; arcIndex++)
            {
                arcs[arcIndex] = new ArcBolt();
                arcs[arcIndex].Initialize(
                    transform,
                    pillarIndex,
                    arcIndex,
                    arcSegmentCount,
                    arcMaterial != null ? arcMaterial : ResolveRuntimeArcMaterial());
                arcs[arcIndex].SetActive(false);
            }
        }
    }

    private void CreateOrb(int index)
    {
        GameObject instance = orbPrefab != null
            ? Instantiate(orbPrefab, transform)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        instance.name = $"Orbital Energy Pillar Orb {index + 1}";
        instance.transform.SetParent(transform, false);
        Collider orbCollider = instance.GetComponent<Collider>();
        if (orbCollider != null)
        {
            Destroy(orbCollider);
        }

        if (fallbackOrbMaterial != null || orbPrefab == null)
        {
            Material material = fallbackOrbMaterial != null ? fallbackOrbMaterial : ResolveRuntimeOrbMaterial();
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
        GameObject linkObject = new GameObject($"Orbital Energy Pillar Link {index + 1}");
        linkObject.transform.SetParent(transform, false);
        LineRenderer line = linkObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.sharedMaterial = linkMaterial != null ? linkMaterial : ResolveRuntimeLinkMaterial();
        _links[index] = line;
    }

    private void CreatePillar(int index)
    {
        if (pillarVisualPrefab != null && TryCreatePillarFromPrefab(index))
        {
            return;
        }

        GameObject pillarRoot = new GameObject($"Orbital Energy Pillar {index + 1}");
        pillarRoot.transform.SetParent(transform, false);

        _pillarRoots[index] = pillarRoot;
        _pillarRootTransforms[index] = pillarRoot.transform;

        CreatePillarLayer(pillarRoot.transform, "Core", out _coreTransforms[index], out _coreRenderers[index]);
        CreatePillarLayer(pillarRoot.transform, "Shell", out _shellTransforms[index], out _shellRenderers[index]);
        CreatePillarLayer(pillarRoot.transform, "Halo", out _haloTransforms[index], out _haloRenderers[index]);
    }

    private bool TryCreatePillarFromPrefab(int index)
    {
        GameObject pillarRoot = Instantiate(pillarVisualPrefab, transform);
        if (pillarRoot == null)
        {
            return false;
        }

        pillarRoot.name = $"Orbital Energy Pillar {index + 1}";
        pillarRoot.transform.SetParent(transform, false);
        _pillarRoots[index] = pillarRoot;
        _pillarRootTransforms[index] = pillarRoot.transform;

        _coreRenderers[index] = FindLayerRenderer(pillarRoot.transform, "Core", out _coreTransforms[index]);
        _shellRenderers[index] = FindLayerRenderer(pillarRoot.transform, "Shell", out _shellTransforms[index]);
        _haloRenderers[index] = FindLayerRenderer(pillarRoot.transform, "Halo", out _haloTransforms[index]);

        bool hasRequiredLayers = _coreRenderers[index] != null
            && _shellRenderers[index] != null
            && _haloRenderers[index] != null;

        if (!hasRequiredLayers)
        {
            Destroy(pillarRoot);
            _pillarRoots[index] = null;
            _pillarRootTransforms[index] = null;
            _coreTransforms[index] = null;
            _shellTransforms[index] = null;
            _haloTransforms[index] = null;
            _coreRenderers[index] = null;
            _shellRenderers[index] = null;
            _haloRenderers[index] = null;
            return false;
        }

        ConfigurePrefabLayer(_coreRenderers[index]);
        ConfigurePrefabLayer(_shellRenderers[index]);
        ConfigurePrefabLayer(_haloRenderers[index]);
        return true;
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

    private void ConfigurePrefabLayer(Renderer renderer)
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
            renderer.sharedMaterial = pillarMaterial != null ? pillarMaterial : ResolveRuntimePillarMaterial();
        }

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void CreatePillarLayer(Transform parent, string layerName, out Transform layerTransform, out Renderer layerRenderer)
    {
        GameObject layerObject = new GameObject(layerName);
        layerObject.transform.SetParent(parent, false);
        MeshFilter meshFilter = layerObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = layerObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = ResolvePillarMesh();
        meshRenderer.sharedMaterial = pillarMaterial != null ? pillarMaterial : ResolveRuntimePillarMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        layerTransform = layerObject.transform;
        layerRenderer = meshRenderer;
    }

    private void SetVisualActive(int index, bool active)
    {
        if (_orbInstances != null && index < _orbInstances.Length && _orbInstances[index] != null)
        {
            _orbInstances[index].SetActive(active);
        }

        if (_pillarRoots != null && index < _pillarRoots.Length && _pillarRoots[index] != null)
        {
            _pillarRoots[index].SetActive(false);
        }

        if (_links != null && index < _links.Length && _links[index] != null)
        {
            _links[index].enabled = false;
        }

        if (_arcBoltsByPillar != null && index < _arcBoltsByPillar.Length && _arcBoltsByPillar[index] != null)
        {
            ArcBolt[] arcs = _arcBoltsByPillar[index];
            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i] != null)
                {
                    arcs[i].SetActive(false);
                }
            }
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
        layerTransform.localScale = new Vector3(visualRadius, pillarVisualHeight * 0.5f, visualRadius);
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

    private void UpdateArcs(int pillarIndex, Vector3 center, float radius, float elapsed, bool showPillar, float opacity)
    {
        if (_arcBoltsByPillar == null
            || pillarIndex >= _arcBoltsByPillar.Length
            || _arcBoltsByPillar[pillarIndex] == null)
        {
            return;
        }

        ArcBolt[] arcs = _arcBoltsByPillar[pillarIndex];
        for (int i = 0; i < arcs.Length; i++)
        {
            ArcBolt arc = arcs[i];
            if (arc == null)
            {
                continue;
            }

            bool active = showPillar && i < arcCountPerPillar && opacity > 0.001f && _mainCamera != null;
            if (!active)
            {
                arc.SetActive(false);
                continue;
            }

            arc.UpdateArc(
                center,
                radius,
                pillarVisualHeight,
                arcWidth,
                elapsed,
                arcRetargetInterval,
                arcFadeFraction,
                opacity * arcIntensity,
                arcBranchChance,
                arcBranchIntensity,
                arcOutwardRadiusMin,
                arcOutwardRadiusMax,
                arcVerticalSpanMin,
                arcVerticalSpanMax,
                arcVerticalCenterRange,
                arcVerticalDriftSpeed,
                arcWrapAngleMin,
                arcWrapAngleMax,
                _mainCamera);
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

        _sharedPillarMesh = new Mesh { name = "OrbitalEnergyPillarOpenCylinder" };
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
        if (shader == null)
        {
            return null;
        }

        _runtimeOrbMaterial = new Material(shader);
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
        if (shader == null)
        {
            return null;
        }

        _runtimeLinkMaterial = new Material(shader);
        if (_runtimeLinkMaterial != null)
        {
            _runtimeLinkMaterial.color = linkColor;
        }
        return _runtimeLinkMaterial;
    }

    private Material ResolveRuntimePillarMaterial()
    {
        if (_runtimePillarMaterial != null)
        {
            return _runtimePillarMaterial;
        }

        Shader shader = Shader.Find("Starfall/3D/OrbitalEnergyPillar");
        shader = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
        shader = shader != null ? shader : Shader.Find("Standard");
        _runtimePillarMaterial = shader != null ? new Material(shader) : null;
        return _runtimePillarMaterial;
    }

    private Material ResolveRuntimeArcMaterial()
    {
        if (_runtimeArcMaterial != null)
        {
            return _runtimeArcMaterial;
        }

        Shader shader = Shader.Find("Starfall/3D/OrbitalEnergyArc");
        shader = shader != null ? shader : Shader.Find("Custom/LightningBolt3D");
        shader = shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit");
        _runtimeArcMaterial = shader != null ? new Material(shader) : null;
        return _runtimeArcMaterial;
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

    private sealed class ArcBolt
    {
        private Vector3[] _centers;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private int[] _triangles;
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

        private GameObject _instance;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private int _pillarIndex;
        private int _arcIndex;
        private int _segmentCount;
        private float _phaseOffset;

        public void Initialize(Transform parent, int pillarIndex, int arcIndex, int segmentCount, Material material)
        {
            _pillarIndex = pillarIndex;
            _arcIndex = arcIndex;
            _segmentCount = Mathf.Max(2, segmentCount);
            _phaseOffset = Hash01((pillarIndex + 1) * 92821 ^ (arcIndex + 1) * 68917);

            int pointCount = _segmentCount + 1;
            _centers = new Vector3[pointCount];
            _vertices = new Vector3[pointCount * 2];
            _uvs = new Vector2[pointCount * 2];
            _triangles = new int[_segmentCount * 6];

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)_segmentCount;
                int vertex = i * 2;
                _uvs[vertex] = new Vector2(t, 0f);
                _uvs[vertex + 1] = new Vector2(t, 1f);

                if (i >= _segmentCount)
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

            _instance = new GameObject($"Orbital Energy Pillar Arc {pillarIndex + 1}-{arcIndex + 1}");
            _instance.transform.SetParent(parent, false);
            MeshFilter meshFilter = _instance.AddComponent<MeshFilter>();
            _renderer = _instance.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = $"OrbitalEnergyArcMesh_{pillarIndex + 1}_{arcIndex + 1}" };
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

        public void UpdateArc(
            Vector3 pillarCenter,
            float pillarRadius,
            float pillarVisualHeight,
            float arcWidth,
            float elapsed,
            float retargetInterval,
            float fadeFraction,
            float intensity,
            float branchChance,
            float branchIntensity,
            float outwardRadiusMin,
            float outwardRadiusMax,
            float verticalSpanMin,
            float verticalSpanMax,
            float verticalCenterRange,
            float verticalDriftSpeed,
            float wrapAngleMin,
            float wrapAngleMax,
            Camera camera)
        {
            if (_renderer == null || _mesh == null || _centers == null || camera == null)
            {
                SetActive(false);
                return;
            }

            retargetInterval = Mathf.Max(0.03f, retargetInterval);
            float shiftedTime = elapsed + _phaseOffset * retargetInterval;
            int phase = Mathf.FloorToInt(shiftedTime / retargetInterval);
            float phaseT = Mathf.Repeat(shiftedTime / retargetInterval, 1f);
            float fadeIn = Smooth01(phaseT / Mathf.Max(0.001f, fadeFraction));
            float fadeOut = Smooth01((1f - phaseT) / Mathf.Max(0.001f, fadeFraction));
            float arcOpacity = intensity * Mathf.Min(fadeIn, fadeOut);

            if (arcOpacity <= 0.001f)
            {
                SetActive(false);
                return;
            }

            int baseSeed = ((_pillarIndex + 1) * 73856093) ^ ((_arcIndex + 1) * 19349663) ^ (phase * 83492791);
            BuildCenterline(
                pillarCenter,
                pillarRadius,
                pillarVisualHeight,
                elapsed,
                baseSeed,
                outwardRadiusMin,
                outwardRadiusMax,
                verticalSpanMin,
                verticalSpanMax,
                verticalCenterRange,
                verticalDriftSpeed,
                wrapAngleMin,
                wrapAngleMax);

            float pathLength = BuildBillboardMesh(camera, Mathf.Max(0.01f, arcWidth));
            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(BoltLengthId, pathLength);
            _propertyBlock.SetFloat(ExternalIntensityId, arcOpacity);
            _propertyBlock.SetFloat(ArcSeedId, baseSeed * 0.001f);
            _propertyBlock.SetFloat(BranchChanceId, branchChance);
            _propertyBlock.SetFloat(BranchIntensityId, branchIntensity);
            _renderer.SetPropertyBlock(_propertyBlock);
            SetActive(true);
        }

        private void BuildCenterline(
            Vector3 pillarCenter,
            float pillarRadius,
            float pillarVisualHeight,
            float elapsed,
            int baseSeed,
            float outwardRadiusMin,
            float outwardRadiusMax,
            float verticalSpanMin,
            float verticalSpanMax,
            float verticalCenterRange,
            float verticalDriftSpeed,
            float wrapAngleMin,
            float wrapAngleMax)
        {
            int arcType = Mathf.Min(ArcTypeCount - 1, Mathf.FloorToInt(Hash01(baseSeed + 1) * ArcTypeCount));
            float sign = Hash01(baseSeed + 2) < 0.5f ? -1f : 1f;
            float startAngle = Hash01(baseSeed + 3) * Mathf.PI * 2f;
            float wrapDegrees = Mathf.Lerp(wrapAngleMin, wrapAngleMax, Hash01(baseSeed + 4)) * sign;
            float verticalSpan = Mathf.Lerp(verticalSpanMin, verticalSpanMax, Hash01(baseSeed + 5));
            float baseYOffset = SignedHash(baseSeed + 6) * verticalCenterRange;
            baseYOffset = RepeatCentered(baseYOffset + elapsed * verticalDriftSpeed * (0.45f + Hash01(baseSeed + 7)), verticalCenterRange);
            float outwardBase = Mathf.Lerp(outwardRadiusMin, outwardRadiusMax, Hash01(baseSeed + 8));
            float outwardBulge = Mathf.Lerp(0.05f, outwardRadiusMax - outwardRadiusMin, Hash01(baseSeed + 9));

            if (arcType == 0)
            {
                wrapDegrees *= 0.22f;
                verticalSpan *= 1.15f;
                outwardBulge *= 0.45f;
            }
            else if (arcType == 1)
            {
                verticalSpan *= 0.35f;
                outwardBulge *= 0.65f;
            }
            else
            {
                wrapDegrees *= 0.55f;
                outwardBase = outwardRadiusMin;
                outwardBulge = Mathf.Max(outwardBulge, outwardRadiusMax - outwardRadiusMin);
            }

            verticalSpan = Mathf.Min(verticalSpan, pillarVisualHeight * 0.35f);
            float angleSpan = wrapDegrees * Mathf.Deg2Rad;

            for (int i = 0; i <= _segmentCount; i++)
            {
                float t = i / (float)_segmentCount;
                float jagAngle = SignedHash(baseSeed + 101 + i * 17) * Mathf.Deg2Rad * 9f;
                float jagY = SignedHash(baseSeed + 211 + i * 19) * verticalSpan * 0.08f;
                float angle = startAngle + angleSpan * t + jagAngle;
                float yOffset = baseYOffset + (t - 0.5f) * verticalSpan + jagY;
                float bulge = Mathf.Sin(t * Mathf.PI);
                float radialScale = outwardBase + bulge * outwardBulge;

                if (arcType == 2)
                {
                    radialScale = Mathf.Lerp(outwardRadiusMin, outwardRadiusMax, bulge);
                }

                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                _centers[i] = pillarCenter + radial * (pillarRadius * radialScale) + Vector3.up * yOffset;
            }
        }

        private float BuildBillboardMesh(Camera camera, float arcWidth)
        {
            float pathLength = 0f;
            Vector3 cameraPosition = camera.transform.position;
            for (int i = 0; i <= _segmentCount; i++)
            {
                Vector3 previous = _centers[Mathf.Max(0, i - 1)];
                Vector3 next = _centers[Mathf.Min(_segmentCount, i + 1)];
                Vector3 tangent = next - previous;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector3.up;
                }

                tangent.Normalize();
                Vector3 toCamera = cameraPosition - _centers[i];
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

                widthAxis = widthAxis.normalized * (arcWidth * 0.5f);
                int vertex = i * 2;
                _vertices[vertex] = _instance.transform.InverseTransformPoint(_centers[i] - widthAxis);
                _vertices[vertex + 1] = _instance.transform.InverseTransformPoint(_centers[i] + widthAxis);

                if (i > 0)
                {
                    pathLength += Vector3.Distance(_centers[i - 1], _centers[i]);
                }
            }

            return pathLength;
        }

    }
}
