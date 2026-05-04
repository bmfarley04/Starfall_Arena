using UnityEngine;

[DisallowMultipleComponent]
public class TriumvirateLightningLinkVisual3D : MonoBehaviour
{
    [Header("Anchors")]
    [Tooltip("World-space transform where the cosmetic link begins.")]
    [SerializeField] private Transform startAnchor;
    [Tooltip("World-space transform where the cosmetic link ends.")]
    [SerializeField] private Transform endAnchor;

    [Header("Lightning Shape")]
    [Tooltip("Total line points including start and end. Higher values make the link more jagged.")]
    [SerializeField] private int pointCount = 4;
    [Tooltip("Maximum sideways offset for intermediate points in world units.")]
    [SerializeField] private float amplitude = 0.45f;
    [Tooltip("Seconds between link randomization steps.")]
    [SerializeField] private float jitterInterval = 0.05f;

    private ForgeEnemyBeam3D _forgeBeam;
    private bool _linkStarted;

    private void Awake()
    {
        _forgeBeam = GetComponent<ForgeEnemyBeam3D>();
        DisableStockForgeLightning();
    }

    private void OnEnable()
    {
        StartForgeLink();
    }

    private void OnDisable()
    {
        _forgeBeam?.StopFiring();
        _linkStarted = false;
    }

    private void LateUpdate()
    {
        if (!_linkStarted)
        {
            StartForgeLink();
        }
    }

    public void Initialize(Transform start, Transform end, int linePointCount, float lineAmplitude, float lineJitterInterval)
    {
        startAnchor = start;
        endAnchor = end;
        pointCount = Mathf.Max(2, linePointCount);
        amplitude = Mathf.Max(0f, lineAmplitude);
        jitterInterval = Mathf.Max(0.01f, lineJitterInterval);
        _linkStarted = false;
        StartForgeLink();
    }

    private void StartForgeLink()
    {
        if (_forgeBeam == null || startAnchor == null || endAnchor == null)
        {
            return;
        }

        _forgeBeam.StartCosmeticLink(startAnchor, endAnchor, pointCount, amplitude, jitterInterval);
        _linkStarted = true;
    }

    private void DisableStockForgeLightning()
    {
        FORGE3D.F3DLightning[] forgeLightnings = GetComponentsInChildren<FORGE3D.F3DLightning>(true);
        for (int i = 0; i < forgeLightnings.Length; i++)
        {
            if (forgeLightnings[i] != null)
            {
                forgeLightnings[i].enabled = false;
            }
        }
    }
}
