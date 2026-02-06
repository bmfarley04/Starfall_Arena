using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to ability buttons to show/hide tooltips on hover (mouse and controller).
/// Controller-first design with gamepad navigation support.
/// Tooltips are already configured - this just activates/deactivates them.
/// </summary>
[RequireComponent(typeof(Button))]
public class AbilityTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("Tooltip GameObject to show/hide on hover (text already configured by manager)")]
    [SerializeField] private GameObject tooltip;

    private void Awake()
    {
        if (tooltip != null)
            tooltip.SetActive(false);
    }

    // Mouse hover - show immediately
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    // Controller/keyboard navigation - show immediately
    public void OnSelect(BaseEventData eventData)
    {
        ShowTooltip();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        HideTooltip();
    }

    private void ShowTooltip()
    {
        if (tooltip != null)
            tooltip.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltip != null)
            tooltip.SetActive(false);
    }

    private void OnDisable()
    {
        HideTooltip();
    }
}
