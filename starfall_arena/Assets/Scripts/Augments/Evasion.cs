using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Evasion", menuName = "Starfall Arena/Augments/Evasion", order = 2)]
public class Evasion : Augment
{
    [Header("Presentation")]
    [Tooltip("Sound played when Evasion successfully ignores incoming damage")]
    public SoundEffect successfulEvadeSound;

    [Tooltip("One-shot prefab spawned when Evasion successfully ignores incoming damage")]
    public GameObject successfulEvadePrefab;

    [Header("Gameplay")]
    [Tooltip("Chance (0-1) to ignore incoming shield damage portion")]
    [Range(0f, 1f)]
    public float shieldIgnoreChance = 0.05f;

    [Tooltip("Chance (0-1) to ignore incoming health damage portion")]
    [Range(0f, 1f)]
    public float healthIgnoreChance = 0.10f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new EvasionRuntime(this);
    }
}
