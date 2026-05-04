using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the whole project visually constrained to the authored 16:9 frame.
/// Cameras are fitted into a shared safe rect, while overlay bars cover and block
/// input outside that rect for Screen Space - Overlay UI.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class ProjectAspectRatioEnforcer : MonoBehaviour
{
    [SerializeField, Tooltip("Target width divided by height. Starfall Arena is authored for 16:9.")]
    private float targetAspect = 16f / 9f;

    [SerializeField, Tooltip("Color used for the letterbox or pillarbox area outside the 16:9 safe frame.")]
    private Color barColor = Color.black;

    private const int InitialCameraCapacity = 32;
    private static ProjectAspectRatioEnforcer _instance;

    private readonly List<CameraViewportState> _cameraStates = new List<CameraViewportState>(InitialCameraCapacity);
    private Camera[] _cameraBuffer = new Camera[InitialCameraCapacity];
    private Canvas _barCanvas;
    private Image _leftBar;
    private Image _rightBar;
    private Image _topBar;
    private Image _bottomBar;
    private Rect _safeRect = new Rect(0f, 0f, 1f, 1f);
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject root = new GameObject(nameof(ProjectAspectRatioEnforcer));
        _instance = root.AddComponent<ProjectAspectRatioEnforcer>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureBarCanvas();
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _instance = null;
        }
    }

    private void Start()
    {
        ApplyAspectRatio();
    }

    private void LateUpdate()
    {
        ApplyAspectRatio();
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        ApplyAspectRatio();
    }

    private void ApplyAspectRatio()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        UpdateSafeRectIfNeeded();
        ApplySafeRectToCameras();
        UpdateBarPresentation();
    }

    private void UpdateSafeRectIfNeeded()
    {
        if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height)
        {
            return;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        float screenAspect = (float)Screen.width / Screen.height;
        if (screenAspect > targetAspect)
        {
            float normalizedWidth = targetAspect / screenAspect;
            _safeRect = new Rect((1f - normalizedWidth) * 0.5f, 0f, normalizedWidth, 1f);
            return;
        }

        float normalizedHeight = screenAspect / targetAspect;
        _safeRect = new Rect(0f, (1f - normalizedHeight) * 0.5f, 1f, normalizedHeight);
    }

    private void ApplySafeRectToCameras()
    {
        int cameraCount = GetAllCamerasNonAlloc();
        RemoveDestroyedCameraStates();

        for (int i = 0; i < cameraCount; i++)
        {
            Camera targetCamera = _cameraBuffer[i];
            if (targetCamera == null || targetCamera.targetTexture != null)
            {
                continue;
            }

            CameraViewportState state = GetOrCreateCameraState(targetCamera);
            Rect currentRect = targetCamera.rect;
            if (!Approximately(currentRect, state.LastAppliedRect))
            {
                state.SourceRect = currentRect;
            }

            Rect fittedRect = FitRectInsideSafeFrame(state.SourceRect);
            targetCamera.rect = fittedRect;
            state.LastAppliedRect = fittedRect;
        }
    }

    private int GetAllCamerasNonAlloc()
    {
        int count = Camera.GetAllCameras(_cameraBuffer);
        while (count == _cameraBuffer.Length)
        {
            _cameraBuffer = new Camera[_cameraBuffer.Length * 2];
            count = Camera.GetAllCameras(_cameraBuffer);
        }

        return count;
    }

    private Rect FitRectInsideSafeFrame(Rect source)
    {
        return new Rect(
            _safeRect.x + source.x * _safeRect.width,
            _safeRect.y + source.y * _safeRect.height,
            source.width * _safeRect.width,
            source.height * _safeRect.height);
    }

    private CameraViewportState GetOrCreateCameraState(Camera targetCamera)
    {
        for (int i = 0; i < _cameraStates.Count; i++)
        {
            CameraViewportState state = _cameraStates[i];
            if (state.Camera == targetCamera)
            {
                return state;
            }
        }

        CameraViewportState newState = new CameraViewportState(targetCamera);
        _cameraStates.Add(newState);
        return newState;
    }

    private void RemoveDestroyedCameraStates()
    {
        for (int i = _cameraStates.Count - 1; i >= 0; i--)
        {
            if (_cameraStates[i].Camera == null)
            {
                _cameraStates.RemoveAt(i);
            }
        }
    }

    private void EnsureBarCanvas()
    {
        if (_barCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("16x9 Aspect Bars");
        canvasObject.transform.SetParent(transform, false);

        _barCanvas = canvasObject.AddComponent<Canvas>();
        _barCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _barCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasObject.AddComponent<GraphicRaycaster>();

        _leftBar = CreateBar("Left Bar", canvasObject.transform);
        _rightBar = CreateBar("Right Bar", canvasObject.transform);
        _topBar = CreateBar("Top Bar", canvasObject.transform);
        _bottomBar = CreateBar("Bottom Bar", canvasObject.transform);
    }

    private Image CreateBar(string barName, Transform parent)
    {
        GameObject barObject = new GameObject(barName);
        barObject.transform.SetParent(parent, false);

        Image image = barObject.AddComponent<Image>();
        image.color = barColor;
        image.raycastTarget = true;
        return image;
    }

    private void UpdateBarPresentation()
    {
        EnsureBarCanvas();

        SetBarRect(_leftBar, 0f, 0f, _safeRect.xMin, 1f);
        SetBarRect(_rightBar, _safeRect.xMax, 0f, 1f, 1f);
        SetBarRect(_bottomBar, _safeRect.xMin, 0f, _safeRect.xMax, _safeRect.yMin);
        SetBarRect(_topBar, _safeRect.xMin, _safeRect.yMax, _safeRect.xMax, 1f);

        _leftBar.color = barColor;
        _rightBar.color = barColor;
        _topBar.color = barColor;
        _bottomBar.color = barColor;
    }

    private static void SetBarRect(Image image, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(anchorMinX, anchorMinY);
        rectTransform.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        bool hasArea = anchorMaxX > anchorMinX && anchorMaxY > anchorMinY;
        image.enabled = hasArea;
        image.raycastTarget = hasArea;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x)
            && Mathf.Approximately(a.y, b.y)
            && Mathf.Approximately(a.width, b.width)
            && Mathf.Approximately(a.height, b.height);
    }

    private sealed class CameraViewportState
    {
        public readonly Camera Camera;
        public Rect SourceRect;
        public Rect LastAppliedRect;

        public CameraViewportState(Camera camera)
        {
            Camera = camera;
            SourceRect = camera != null ? camera.rect : new Rect(0f, 0f, 1f, 1f);
            LastAppliedRect = SourceRect;
        }
    }
}
