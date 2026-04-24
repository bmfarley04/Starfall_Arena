using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
            if (TryGetCurrentPingMilliseconds(out float pingMs))
            {
                pingText.text = $"{Mathf.RoundToInt(Mathf.Max(0f, pingMs))}{pingSuffix}";
            }
            else
            {
                pingText.text = $"{pingValuePlaceholder}{pingSuffix}";
            }
        }
    }

    private bool TryGetCurrentPingMilliseconds(out float pingMilliseconds)
    {
        pingMilliseconds = 0f;

        if (!NetMgr.IsNetworked)
        {
            return false;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return false;
        }

        UnityTransport transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            return false;
        }

        ulong currentRttMs = transport.GetCurrentRtt(NetworkManager.ServerClientId);

        // Keep parity with the existing 2D/network HUD: display one-way estimate, not full RTT.
        pingMilliseconds = currentRttMs * 0.5f;
        return true;
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
