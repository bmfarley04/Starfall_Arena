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
        private Player _player;
        private int _slotIndex;
        private bool _wasOnCooldown;

        public void Bind(Ability ability)
        {
            _ability = ability;
            _player = null;
            _slotIndex = 0;

            InitializeVisualState();
        }

        public void Bind(Player player, int slotIndex)
        {
            _player = player;
            _slotIndex = slotIndex;
            _ability = null;

            InitializeVisualState();
        }

        private void InitializeVisualState()
        {

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
            float fillRatio;
            bool isOnCooldown;
            bool isResource;

            if (_player != null && _slotIndex >= 1 && _slotIndex <= 4)
            {
                fillRatio = _player.GetAbilityHUDFillRatio(_slotIndex);
                isOnCooldown = _player.IsAbilityOnCooldownForHUD(_slotIndex);
                isResource = _player.IsAbilityResourceBasedForHUD(_slotIndex);
            }
            else if (_ability != null)
            {
                bool isLocked = _ability.isLocked;
                fillRatio = isLocked ? 1f : _ability.GetHUDFillRatio();
                isOnCooldown = isLocked || _ability.IsOnCooldown();
                isResource = _ability.IsResourceBased() && !isLocked;
            }
            else
            {
                return;
            }

            fillRatio = Mathf.Clamp01(fillRatio);

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
