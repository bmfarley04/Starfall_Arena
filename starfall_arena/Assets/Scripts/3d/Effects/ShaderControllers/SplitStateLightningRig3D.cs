using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SplitStateLightningRig3D : MonoBehaviour
{
    [Tooltip("Bolts controlled by this split-state rig. If empty, child LightningBolt3D components are discovered automatically.")]
    [SerializeField] private List<LightningBolt3D> bolts = new();
    [Tooltip("When enabled, the rig starts active so the effect can be previewed in-editor or in test scenes.")]
    [SerializeField] private bool activateOnStart;
    [Tooltip("Global intensity multiplier pushed to every managed bolt while the split-state effect is active.")]
    [SerializeField] [Min(0f)] private float splitStateIntensity = 1f;

    private bool _splitStateActive;

    private void Awake()
    {
        CacheBoltsIfNeeded();
        ApplyState();
    }

    private void Start()
    {
        SetSplitStateActive(activateOnStart);
    }

    public void SetSplitStateActive(bool isActive)
    {
        _splitStateActive = isActive;
        ApplyState();
    }

    public void SetSplitStateIntensity(float intensity)
    {
        splitStateIntensity = Mathf.Max(0f, intensity);
        ApplyState();
    }

    private void OnValidate()
    {
        splitStateIntensity = Mathf.Max(0f, splitStateIntensity);
    }

    private void CacheBoltsIfNeeded()
    {
        if (bolts.Count > 0)
        {
            return;
        }

        LightningBolt3D[] childBolts = GetComponentsInChildren<LightningBolt3D>(true);
        for (int i = 0; i < childBolts.Length; i++)
        {
            LightningBolt3D bolt = childBolts[i];
            if (bolt != null)
            {
                bolts.Add(bolt);
            }
        }
    }

    private void ApplyState()
    {
        CacheBoltsIfNeeded();

        for (int i = 0; i < bolts.Count; i++)
        {
            LightningBolt3D bolt = bolts[i];
            if (bolt == null)
            {
                continue;
            }

            bolt.SetSplitStateActive(_splitStateActive, splitStateIntensity);
        }
    }
}
