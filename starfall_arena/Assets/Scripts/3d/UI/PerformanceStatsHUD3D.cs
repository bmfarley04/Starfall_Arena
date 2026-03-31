using TMPro;
using UnityEngine;

public class PerformanceStatsHUD3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private TMP_Text pingText;

    [Header("Display")]
    [SerializeField] private float refreshInterval = 0.25f;
    [SerializeField] private string fpsSuffix = " FPS";
    [SerializeField] private string pingValuePlaceholder = "--";
    [SerializeField] private string pingSuffix = " ms";

    private float _elapsedSampleTime;
    private int _sampledFrameCount;

    private void Awake()
    {
        targetCanvas ??= GetComponent<Canvas>();
        RefreshCanvasCameraBinding();
        RefreshDisplay(0f);
    }

    private void OnEnable()
    {
        _elapsedSampleTime = 0f;
        _sampledFrameCount = 0;
        RefreshCanvasCameraBinding();
        RefreshDisplay(0f);
    }

    private void Update()
    {
        RefreshCanvasCameraBinding();

        _elapsedSampleTime += Time.unscaledDeltaTime;
        _sampledFrameCount++;

        float safeRefreshInterval = Mathf.Max(0.05f, refreshInterval);
        if (_elapsedSampleTime < safeRefreshInterval)
        {
            return;
        }

        float fps = _sampledFrameCount / Mathf.Max(0.0001f, _elapsedSampleTime);
        RefreshDisplay(fps);

        _elapsedSampleTime = 0f;
        _sampledFrameCount = 0;
    }

    private void RefreshDisplay(float fps)
    {
        if (fpsText != null)
        {
            fpsText.text = $"{Mathf.RoundToInt(Mathf.Max(0f, fps))}{fpsSuffix}";
        }

        if (pingText != null)
        {
            pingText.text = $"{pingValuePlaceholder}{pingSuffix}";
        }
    }

    private void RefreshCanvasCameraBinding()
    {
        if (targetCanvas == null)
        {
            return;
        }

        if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null || ReferenceEquals(targetCanvas.worldCamera, mainCamera))
        {
            return;
        }

        targetCanvas.worldCamera = mainCamera;
    }
}
