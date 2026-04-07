using System.Collections.Generic;
using UnityEngine;
using StarfallArena.UI;

[DisallowMultipleComponent]
public class AugmentController : MonoBehaviour
{
    private const byte ArtificialFairyTriggeredFlag = 1 << 0;

    private Player _player;
    private readonly List<IAugmentRuntime> _runtimes = new List<IAugmentRuntime>();
    private int _currentRound;
    private bool _artificialFairyTriggeredSinceLastConsume;

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

    public List<NetworkAugmentLoadoutEntry> ExportNetworkLoadout()
    {
        List<NetworkAugmentLoadoutEntry> entries = new List<NetworkAugmentLoadoutEntry>(_runtimes.Count);

        foreach (IAugmentRuntime runtime in _runtimes)
        {
            if (runtime == null || runtime.Definition == null || string.IsNullOrWhiteSpace(runtime.Definition.augmentID))
            {
                continue;
            }

            entries.Add(new NetworkAugmentLoadoutEntry
            {
                augmentId = runtime.Definition.augmentID,
                roundAcquired = runtime.RoundAcquired,
                stateFlags = CreateStateFlags(runtime)
            });
        }

        return entries;
    }

    public void ImportNetworkLoadout(List<NetworkAugmentLoadoutEntry> entries, int currentRound)
    {
        if (_player == null)
        {
            return;
        }

        ClearRuntimesAndModifiers();
        SetCurrentRound(currentRound);

        if (entries == null || GameDataManager.Instance == null)
        {
            return;
        }

        foreach (NetworkAugmentLoadoutEntry entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            Augment definition = GameDataManager.Instance.GetAugmentById(entry.augmentId);
            if (definition == null)
            {
                continue;
            }

            AcquireAugment(definition, entry.roundAcquired, CreatePersistentState(definition, entry));
        }
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

            if (runtime is ArtificialFairyRuntime fairyRuntime && fairyRuntime.ConsumeTriggeredThisDamageEvent())
            {
                _artificialFairyTriggeredSinceLastConsume = true;
            }
        }
    }

    public bool ConsumeArtificialFairyTriggeredFlag()
    {
        bool triggered = _artificialFairyTriggeredSinceLastConsume;
        _artificialFairyTriggeredSinceLastConsume = false;
        return triggered;
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

    public void NotifyEvasionTriggered()
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            if (runtime is EvasionRuntime evasionRuntime)
            {
                evasionRuntime.NotifySuccessfulEvasion();
            }
        }
    }

    public void NotifyArtificialFairyTriggered()
    {
        foreach (IAugmentRuntime runtime in _runtimes)
        {
            if (runtime is ArtificialFairyRuntime fairyRuntime)
            {
                fairyRuntime.NotifyTriggeredFromNetwork();
            }
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
        _artificialFairyTriggeredSinceLastConsume = false;

        if (_player == null) return;

        _player.damageMultipliers.Clear();
        _player.speedMultipliers.Clear();
        _player.rotationMultipliers.Clear();
        _player.SetAugmentVariables();
    }

    private static byte CreateStateFlags(IAugmentRuntime runtime)
    {
        if (runtime == null)
        {
            return 0;
        }

        object persistentState = runtime.CapturePersistentState();
        if (persistentState is ArtificialFairyPersistentState fairyState && fairyState.triggered)
        {
            return ArtificialFairyTriggeredFlag;
        }

        return 0;
    }

    private static object CreatePersistentState(Augment definition, NetworkAugmentLoadoutEntry entry)
    {
        if (definition is ArtificialFairy)
        {
            return new ArtificialFairyPersistentState
            {
                triggered = (entry.stateFlags & ArtificialFairyTriggeredFlag) != 0
            };
        }

        return null;
    }
}
