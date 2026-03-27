using System.Collections.Generic;
using FORGE3D;
using UnityEngine;

public class TeleportPulsewaveEffect3D : MonoBehaviour
{
    private static readonly Quaternion CameraFacingMeshOffset = Quaternion.Euler(90f, 0f, 0f);

    public enum PlaybackMode
    {
        SmallToLarge,
        LargeToSmall
    }

    private const string ColorPropertyName = "_Color";

    private readonly List<RendererState> _rendererStates = new List<RendererState>();

    private int _colorPropertyId;
    private bool _isInitialized;
    private bool _isFading;
    private float _fadeDelay;
    private float _fadeSpeed;
    private float _scaleSpeed;
    private Vector3 _targetScale;
    private Color _fadeTargetColor;
    private Camera _cachedCamera;

    private struct RendererState
    {
        public Renderer renderer;
        public Material[] materials;
        public Color[] colors;
    }

    public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, PlaybackMode playbackMode)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        TeleportPulsewaveEffect3D controller = instance.AddComponent<TeleportPulsewaveEffect3D>();
        controller.Initialize(playbackMode);
    }

    public void Initialize(PlaybackMode playbackMode)
    {
        _colorPropertyId = Shader.PropertyToID(ColorPropertyName);

        F3DPulsewave[] pulsewaves = GetComponentsInChildren<F3DPulsewave>(true);
        for (int i = 0; i < pulsewaves.Length; i++)
        {
            pulsewaves[i].enabled = false;
        }

        F3DPulsewave sourcePulsewave = pulsewaves.Length > 0 ? pulsewaves[0] : null;
        _fadeDelay = sourcePulsewave != null ? Mathf.Max(0f, sourcePulsewave.FadeOutDelay) : 0f;
        _fadeSpeed = sourcePulsewave != null ? Mathf.Max(0f, sourcePulsewave.FadeOutTime) : 1f;
        _scaleSpeed = sourcePulsewave != null ? Mathf.Max(0f, sourcePulsewave.ScaleTime) : 1f;

        Vector3 fullScale = sourcePulsewave != null ? sourcePulsewave.ScaleSize : transform.localScale;
        Vector3 collapsedScale = Vector3.zero;

        CacheRendererState();

        if (playbackMode == PlaybackMode.LargeToSmall)
        {
            transform.localScale = fullScale;
            _targetScale = collapsedScale;
        }
        else
        {
            transform.localScale = collapsedScale;
            _targetScale = fullScale;
        }

        _fadeTargetColor = ResolveFadeTargetColor();
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        AlignToCamera();
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * _scaleSpeed);

        _fadeDelay -= Time.deltaTime;
        if (!_isFading && _fadeDelay <= 0f)
        {
            _isFading = true;
        }

        if (!_isFading)
        {
            return;
        }

        float highestAlpha = 0f;
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            RendererState state = _rendererStates[i];
            if (state.renderer == null)
            {
                continue;
            }

            int colorCount = Mathf.Min(state.materials.Length, state.colors.Length);
            for (int materialIndex = 0; materialIndex < colorCount; materialIndex++)
            {
                Material material = state.materials[materialIndex];
                if (material == null || !material.HasProperty(_colorPropertyId))
                {
                    continue;
                }

                Color nextColor = Color.Lerp(material.GetColor(_colorPropertyId), _fadeTargetColor, Time.deltaTime * _fadeSpeed);
                material.SetColor(_colorPropertyId, nextColor);
                highestAlpha = Mathf.Max(highestAlpha, nextColor.a);
            }
        }

        if (highestAlpha <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void AlignToCamera()
    {
        Camera targetCamera = ResolveCamera();
        if (targetCamera == null)
        {
            return;
        }

        // The pulsewave mesh is a flat card/ring, so it has to billboard to the gameplay camera
        // instead of inheriting a world-facing rotation that only looks correct from some ship headings.
        transform.rotation = Quaternion.LookRotation(-targetCamera.transform.forward, targetCamera.transform.up) * CameraFacingMeshOffset;
    }

    private void CacheRendererState()
    {
        _rendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            Color[] colors = new Color[materials.Length];
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                colors[materialIndex] = material != null && material.HasProperty(_colorPropertyId)
                    ? material.GetColor(_colorPropertyId)
                    : Color.white;
            }

            _rendererStates.Add(new RendererState
            {
                renderer = renderer,
                materials = materials,
                colors = colors
            });
        }
    }

    private Color ResolveFadeTargetColor()
    {
        for (int i = 0; i < _rendererStates.Count; i++)
        {
            Color[] colors = _rendererStates[i].colors;
            for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
            {
                Color color = colors[colorIndex];
                return new Color(0f, 0f, 0f, Mathf.Min(-0.1f, color.a));
            }
        }

        return new Color(0f, 0f, 0f, -0.1f);
    }

    private Camera ResolveCamera()
    {
        if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
        {
            return _cachedCamera;
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            _cachedCamera = Camera.main;
            return _cachedCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled)
            {
                _cachedCamera = camera;
                return _cachedCamera;
            }
        }

        return null;
    }
}
