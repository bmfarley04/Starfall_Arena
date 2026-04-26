using UnityEngine;

public class Enemy3D : Entity3D
{
    [Header("Enemy-Only 3D Systems")]
    [SerializeField] protected EnemyAIFlightController3D aiFlightController;
    [SerializeField] protected FactionMember3D factionMember;
    [SerializeField] protected NetEnemyMovement3D netEnemyMovement;
    [SerializeField] protected NetEnemyCombat3D netEnemyCombat;

    public EnemyAIFlightController3D AIFlightController => aiFlightController;
    public FactionMember3D FactionMember => factionMember;
    public NetEnemyMovement3D NetEnemyMovement => netEnemyMovement;
    public NetEnemyCombat3D NetEnemyCombat => netEnemyCombat;

    protected override void Awake()
    {
        base.Awake();
        aiFlightController ??= GetComponent<EnemyAIFlightController3D>();
        factionMember ??= GetComponent<FactionMember3D>();
        netEnemyMovement ??= GetComponent<NetEnemyMovement3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();

        if (aiFlightController != null && shipFlight != null)
        {
            shipFlight.SetInputSource(aiFlightController);
        }

        if (factionMember == null)
        {
            Debug.LogWarning($"[{nameof(Enemy3D)}] {name} has no FactionMember3D. It will be inferred as EnemyTeam from type/tag, but Invasion prefabs should author the component explicitly.", this);
        }
    }
}
