using UnityEngine;

public class Class2 : Player
{
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    [SerializeField] private float _fireCooldown = 0.5f;

    [Header("Convergence Settings")]
    [Tooltip("Distance ahead of ship where all projectiles converge (all shots meet at this point)")]
    [SerializeField] private float convergenceDistance = 20f;

    protected override void Awake()
    {
        base.Awake();
        fireCooldown = _fireCooldown;
    }

    protected override Vector3 GetFireDirection(Transform turret)
    {
        Vector3 convergencePoint = transform.position + transform.up * convergenceDistance;
        return (convergencePoint - turret.position).normalized;
    }

    public Vector3 GetConvergingFireDirection(Transform turret)
    {
        return GetFireDirection(turret);
    }

    protected override bool IsAnyAbilityActiveForPrimaryFireLock()
    {
        return false;
    }

    public override void LockAbility4()
    {
        base.LockAbility4();
    }

    public override void UnlockAbility4()
    {
        base.UnlockAbility4();
    }
}
