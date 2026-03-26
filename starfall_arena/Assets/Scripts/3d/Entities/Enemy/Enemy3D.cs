using UnityEngine;

public class Enemy3D : Entity3D
{
    [Header("Enemy-Only 3D Systems")]
    [SerializeField] protected EnemyAIFlightController3D aiFlightController;

    public EnemyAIFlightController3D AIFlightController => aiFlightController;

    protected override void Awake()
    {
        base.Awake();
        aiFlightController ??= GetComponent<EnemyAIFlightController3D>();

        if (aiFlightController != null && shipFlight != null)
        {
            shipFlight.SetInputSource(aiFlightController);
        }
    }
}
