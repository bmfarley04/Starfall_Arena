using UnityEngine;
using UnityEngine.UI;

namespace StarfallArena.UI
{
    public class AbilitySlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Circle background image - material swaps on cooldown")]
        [SerializeField] private Image circleBackground;
        [Tooltip("Dark fill icon - fillAmount driven by cooldown/resource")]
        [SerializeField] private Image darkFillIcon;

        [Header("Materials")]
        [Tooltip("Material when ability is ready")]
        [SerializeField] private Material readyMaterial;
        [Tooltip("Material when ability is on cooldown")]
        [SerializeField] private Material cooldownMaterial;

        private Ability _ability;
        private bool _wasOnCooldown;

        public void Bind(Ability ability)
        {
            _ability = ability;

            gameObject.SetActive(true);
            _wasOnCooldown = false;

            if (circleBackground != null && readyMaterial != null)
                circleBackground.material = readyMaterial;

            if (darkFillIcon != null)
            {
                darkFillIcon.type = Image.Type.Filled;
                darkFillIcon.fillMethod = Image.FillMethod.Vertical;
                darkFillIcon.fillOrigin = (int)Image.OriginVertical.Top;
                darkFillIcon.fillAmount = 0f;
            }
        }

        void Update()
        {
            if (_ability == null) return;

            float fillRatio = _ability.GetHUDFillRatio();
            bool isOnCooldown = _ability.IsOnCooldown();
            bool isResource = _ability.IsResourceBased();

            // Update dark fill icon
            if (darkFillIcon != null)
            {
                darkFillIcon.fillAmount = fillRatio;
            }

            // Material swap only for cooldown abilities (not resource-based)
            if (!isResource && circleBackground != null)
            {
                if (isOnCooldown && !_wasOnCooldown)
                {
                    if (cooldownMaterial != null)
                        circleBackground.material = cooldownMaterial;
                }
                else if (!isOnCooldown && _wasOnCooldown)
                {
                    if (readyMaterial != null)
                        circleBackground.material = readyMaterial;
                }
                _wasOnCooldown = isOnCooldown;
            }
        }
    }
}
