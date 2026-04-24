using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCameraRig3D))]
public class GigablastChargeEdgeGlow3D : MonoBehaviour
{
    [System.Serializable]
    public struct TierColorConfig
    {
        [ColorUsage(true, true)] public Color tier1Color;
        [ColorUsage(true, true)] public Color tier2Color;
        [ColorUsage(true, true)] public Color tier3Color;
        [ColorUsage(true, true)] public Color tier4Color;
    }

    [Header("References")]
    [SerializeField] private Player3D player;
    [SerializeField] private GigaBlastWeapon3D gigaBlast;

    [Header("Glow")]
    [ColorUsage(true, true)]
    [SerializeField] private Color edgeColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private bool useTierColors = true;
    [SerializeField] private TierColorConfig tierColors = new TierColorConfig
    {
        tier1Color = new Color(0.2f, 0.6f, 1f, 1f),
        tier2Color = new Color(0.35f, 0.75f, 1f, 1f),
        tier3Color = new Color(0.85f, 0.65f, 1f, 1f),
        tier4Color = new Color(1f, 0.82f, 0.45f, 1f)
    };

    [Header("Vignette")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.5f;

    [Header("Charge Response")]
    [SerializeField, Min(0.01f)] private float fadeInSpeed = 3f;
    [SerializeField, Min(0.01f)] private float fadeOutSpeed = 4f;
    [SerializeField] private AnimationCurve chargeRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Pulse")]
    [SerializeField] private bool pulseWhileCharging = false;
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.1f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 6f;

    private float _currentCharge;
    private bool _warnedMissingGigablast;
    private Color _currentEdgeColor;

    public static bool IsEffectVisible { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsEffectVisible = false;
    }

    private void Reset()
    {
        chargeRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        _currentCharge = 0f;
        _currentEdgeColor = ResolveTierColor(0);
        PublishVignette(0f);
    }

    private void Update()
    {
        if (gigaBlast == null)
        {
            ResolveReferences();
            if (gigaBlast == null)
            {
                WarnMissingGigablast();
                PublishVignette(0f);
                return;
            }
        }

        float normalizedChargeProgress = gigaBlast.NormalizedChargeProgress;
        float targetCharge = gigaBlast.IsCharging ? EvaluateCharge(normalizedChargeProgress) : 0f;
        float speed = gigaBlast.IsCharging ? fadeInSpeed : fadeOutSpeed;
        _currentCharge = Mathf.MoveTowards(_currentCharge, targetCharge, speed * Time.deltaTime);

        Color targetColor = ResolveChargeColor(
            gigaBlast,
            gigaBlast.IsCharging ? normalizedChargeProgress : 0f,
            gigaBlast.IsCharging ? gigaBlast.CurrentChargeTier : 0);
        _currentEdgeColor = Color.Lerp(_currentEdgeColor, targetColor, 1f - Mathf.Exp(-speed * Time.deltaTime));

        PublishVignette(_currentCharge);
    }

    private void OnDisable()
    {
        PublishVignette(0f);
        IsEffectVisible = false;
    }

    private void ResolveReferences()
    {
        player ??= GetComponent<Player3D>();

        if (gigaBlast == null && player != null)
        {
            Weapon3D[] weapons = player.Weapons;
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] is GigaBlastWeapon3D gigaBlastWeapon)
                {
                    gigaBlast = gigaBlastWeapon;
                    break;
                }
            }
        }

        gigaBlast ??= GetComponent<GigaBlastWeapon3D>();
    }

    private float EvaluateCharge(float normalizedCharge)
    {
        normalizedCharge = Mathf.Clamp01(normalizedCharge);
        if (chargeRemap == null || chargeRemap.length == 0)
        {
            return normalizedCharge;
        }

        return Mathf.Clamp01(chargeRemap.Evaluate(normalizedCharge));
    }

    private void PublishVignette(float charge)
    {
        charge = Mathf.Clamp01(charge);
        IsEffectVisible = charge > 0.0001f;

        float alpha = charge * Mathf.Clamp01(maxAlpha);

        if (pulseWhileCharging && charge > 0f)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude * charge;
            alpha = Mathf.Clamp01(alpha + pulse * Mathf.Clamp01(maxAlpha));
        }

        player?.PublishHUDVignetteMessage(new PlayerHUDVignetteMessage3D(
            PlayerHUDVignetteChannel3D.Gigablast,
            alpha,
            _currentEdgeColor));
    }

    private Color ResolveTierColor(int tier)
    {
        if (!useTierColors)
        {
            return edgeColor;
        }

        return tier switch
        {
            1 => tierColors.tier1Color,
            2 => tierColors.tier2Color,
            3 => tierColors.tier3Color,
            4 => tierColors.tier4Color,
            _ => edgeColor
        };
    }

    private Color ResolveChargeColor(GigaBlastWeapon3D weapon, float normalizedCharge, int tier)
    {
        if (!useTierColors)
        {
            return edgeColor;
        }

        if (weapon == null || normalizedCharge <= 0f)
        {
            return ResolveTierColor(tier);
        }

        float maxChargeTime = Mathf.Max(0.0001f, weapon.MaxChargeTime);
        float t1 = Mathf.Clamp01(weapon.Tier1Time / maxChargeTime);
        float t2 = Mathf.Clamp01(weapon.Tier2Time / maxChargeTime);
        float t3 = Mathf.Clamp01(weapon.Tier3Time / maxChargeTime);
        float t4 = Mathf.Clamp01(weapon.Tier4Time / maxChargeTime);

        normalizedCharge = Mathf.Clamp01(normalizedCharge);

        if (normalizedCharge <= t1)
        {
            return LerpSafe(edgeColor, tierColors.tier1Color, 0f, t1, normalizedCharge);
        }

        if (normalizedCharge <= t2)
        {
            return LerpSafe(tierColors.tier1Color, tierColors.tier2Color, t1, t2, normalizedCharge);
        }

        if (normalizedCharge <= t3)
        {
            return LerpSafe(tierColors.tier2Color, tierColors.tier3Color, t2, t3, normalizedCharge);
        }

        return LerpSafe(tierColors.tier3Color, tierColors.tier4Color, t3, t4, normalizedCharge);
    }

    private static Color LerpSafe(Color from, Color to, float start, float end, float value)
    {
        if (end <= start)
        {
            return to;
        }

        float t = Mathf.InverseLerp(start, end, value);
        return Color.Lerp(from, to, t);
    }

    private void WarnMissingGigablast()
    {
        if (_warnedMissingGigablast)
        {
            return;
        }

        _warnedMissingGigablast = true;
        Debug.LogWarning(
            "GigablastChargeEdgeGlow3D could not find a GigaBlastWeapon3D source. Assign the weapon directly or keep the component on the same player object as Player3D.",
            this);
    }
}
