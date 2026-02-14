using System.Collections.Generic;
using UnityEngine;
using StarfallArena.UI;

[DisallowMultipleComponent]
public class AugmentController : MonoBehaviour
{
    private Player _player;
    private readonly List<IAugmentRuntime> _runtimes = new List<IAugmentRuntime>();
    private int _currentRound;

    public void Initialize(Player player)
    {
        _player = player;
    }

    public void SetCurrentRound(int currentRound)
    {
        _currentRound = currentRound;

        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime.OnRoundSet(currentRound);
        }
    }

    public void AcquireAugment(Augment definition, int roundAcquired, object persistentState = null)
    {
        if (_player == null || definition == null) return;

        IAugmentRuntime runtime = definition.CreateRuntime();
        if (runtime == null)
        {
            Debug.LogWarning($"Augment {definition.name} returned null runtime. Using no-op runtime.");
            runtime = new NoOpAugmentRuntime(definition);
        }

        runtime.Initialize(_player, roundAcquired, persistentState);
        runtime.OnRoundSet(_currentRound);
        _runtimes.Add(runtime);
    }

    public void ImportLoadout(List<AugmentLoadoutEntry> entries, int currentRound)
    {
        if (_player == null) return;

        ClearRuntimesAndModifiers();
        SetCurrentRound(currentRound);

        if (entries == null) return;

        foreach (AugmentLoadoutEntry entry in entries)
        {
            if (entry == null || entry.definition == null) continue;

            AcquireAugment(entry.definition, entry.roundAcquired, entry.persistentState);
        }
    }

    public List<AugmentLoadoutEntry> ExportLoadout()
    {
        List<AugmentLoadoutEntry> entries = new List<AugmentLoadoutEntry>(_runtimes.Count);

        foreach (IAugmentRuntime runtime in _runtimes)
        {
            if (runtime == null || runtime.Definition == null) continue;

            entries.Add(new AugmentLoadoutEntry
            {
                definition = runtime.Definition,
                roundAcquired = runtime.RoundAcquired,
                persistentState = runtime.CapturePersistentState()
            });
        }

        return entries;
    }

    public void ExecuteEffects()
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime?.ExecuteEffects();
        }
    }

    public void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime?.OnTakeDamage(damage, impactForce, hitPoint, source);
        }
    }

    public void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source)
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime?.OnBeforeTakeDamage(ref damage, ref shieldIgnored, ref healthIgnored, source);
        }
    }

    public void OnTakeDirectDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime?.OnTakeDirectDamage(damage, impactForce, hitPoint, source);
        }
    }

    public void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source)
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime?.OnBeforeTakeDirectDamage(ref damage, ref healthIgnored, source);
        }
    }

    public void OnContact(Collision2D collision)
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            runtime?.OnContact(collision);
        }
    }

    private void ClearRuntimesAndModifiers()
    {
        _runtimes.Clear();

        if (_player == null) return;

        _player.damageMultipliers.Clear();
        _player.speedMultipliers.Clear();
        _player.rotationMultipliers.Clear();
        _player.SetAugmentVariables();
    }
}
