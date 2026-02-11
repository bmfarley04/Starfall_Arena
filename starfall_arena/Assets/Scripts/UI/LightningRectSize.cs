using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Auto-sets _RectWidth and _RectHeight on the material used by the
/// sibling UI Image so the LightningCrackle shader has correct pixel dimensions.
/// Attach this to the same GameObject as the UI Image with the lightning material.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class LightningRectSize : MonoBehaviour
{
    private Image _image;
    private RectTransform _rect;
    private MaterialPropertyBlock _propBlock;
    private Vector2 _lastSize;

    void OnEnable()
    {
        _image = GetComponent<Image>();
        _rect = GetComponent<RectTransform>();
        UpdateSize();
    }

    void Update()
    {
        Vector2 size = _rect.rect.size;
        if (size != _lastSize)
            UpdateSize();
    }

    void UpdateSize()
    {
        if (_image.material == null) return;

        Vector2 size = _rect.rect.size;
        _lastSize = size;

        _image.material.SetFloat("_RectWidth", size.x);
        _image.material.SetFloat("_RectHeight", size.y);
    }
}
