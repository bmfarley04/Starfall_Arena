using UnityEngine;

[DisallowMultipleComponent]
public class FactionMember3D : MonoBehaviour
{
    [SerializeField] private Faction3D faction = Faction3D.Neutral;

    public Faction3D Faction
    {
        get => faction;
        set => faction = value;
    }

    public static Faction3D ResolveFaction(Entity3D entity)
    {
        if (entity == null)
        {
            return Faction3D.Neutral;
        }

        if (entity.TryGetComponent(out FactionMember3D member))
        {
            return member.faction;
        }

        if (entity is Player3D || entity.CompareTag("Player1") || entity.CompareTag("Player2"))
        {
            return Faction3D.PlayerTeam;
        }

        if (entity is Enemy3D || entity.CompareTag("Enemy"))
        {
            return Faction3D.EnemyTeam;
        }

        return Faction3D.Neutral;
    }

    public static bool TryGetExplicitFaction(Entity3D entity, out Faction3D resolvedFaction)
    {
        if (entity != null && entity.TryGetComponent(out FactionMember3D member))
        {
            resolvedFaction = member.faction;
            return true;
        }

        resolvedFaction = Faction3D.Neutral;
        return false;
    }

    public static bool AreAllied(Entity3D first, Entity3D second)
    {
        Faction3D firstFaction = ResolveFaction(first);
        Faction3D secondFaction = ResolveFaction(second);
        return firstFaction != Faction3D.Neutral && firstFaction == secondFaction;
    }
}
