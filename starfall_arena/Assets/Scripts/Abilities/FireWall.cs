using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class FireWall : Ability
{
    // AI was used heavily in designing this class
    
    // Class to manage a group of fire hazards spawned during a single activation
    private class FireHazardGroup
    {
        public List<GameObject> hazards = new List<GameObject>();
        public AudioSource audioSource;
        public bool isFadingOut = false;
        
        public FireHazardGroup(AudioSource source)
        {
            audioSource = source;
        }
        
        public bool HasActiveHazards()
        {
            hazards.RemoveAll(h => h == null);
            return hazards.Count > 0;
        }
    }
    
    [System.Serializable]
    public struct FireTrailConfig
    {
        [Header("Fire Hazard Settings")]
        [Tooltip("Fire hazard prefab to spawn (should have FireHazard component and collider)")]
        public GameObject firePrefab;
        [Tooltip("Damage per second dealt by each fire hazard")]
        public float damagePerSecond;
        [Tooltip("How long each fire hazard lasts (seconds)")]
        public float fireDuration;
        [Tooltip("Impact force when fire deals damage")]
        public float impactForce;

        [Header("Trail Spawning")]
        [Tooltip("Distance behind ship to spawn fire (negative = behind)")]
        public float spawnOffset;
        [Tooltip("Minimum distance traveled before spawning next fire")]
        public float spawnInterval;
        [Tooltip("Maximum number of active fire hazards at once (0 = unlimited)")]
        public int maxActiveHazards;

        [Header("Capacity System")]
        [Tooltip("Maximum fire trail capacity (100 units)")]
        public float capacity;
        [Tooltip("How fast capacity drains while active (units per second)")]
        public float drainRate;
        [Tooltip("How fast capacity regenerates when not active (units per second)")]
        public float regenRate;

        [Header("Sound Effects")]
        public SoundEffect fireLoopSound;
        [Tooltip("Fade in/out duration for fire sound (seconds)")]
        public SoundEffect fireWallsLoopSound;
        [Tooltip("Fade in/out duration for fire sound (seconds)")]
        public float soundFadeDuration;
    }

    [Header("Fire Trail Ability")]
    public FireTrailConfig fireTrail;

    // ===== PRIVATE STATE =====
    private bool _isActive;
    private float _currentCapacity;
    private Vector3 _lastSpawnPosition;
    private List<GameObject> _activeHazards = new List<GameObject>();
    private List<FireHazardGroup> _hazardGroups = new List<FireHazardGroup>();
    private FireHazardGroup _currentGroup;
    private AudioSource _fireLoopSource;
    private Coroutine _soundFadeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _currentCapacity = 0f;

        _fireLoopSource = gameObject.AddComponent<AudioSource>();
        _fireLoopSource.playOnAwake = false;
        _fireLoopSource.loop = true;
        _fireLoopSource.spatialBlend = 1f;
        _fireLoopSource.rolloffMode = AudioRolloffMode.Linear;
        _fireLoopSource.minDistance = 10f;
        _fireLoopSource.maxDistance = 50f;
        _fireLoopSource.dopplerLevel = 0f;
    }

    protected void Update()
    {
        // Regenerate capacity when not active
        if (!_isActive && _currentCapacity > 0f)
        {
            _currentCapacity = Mathf.Max(_currentCapacity - fireTrail.regenRate * Time.deltaTime, 0f);
        }

        // Clean up destroyed hazards from tracking list
        _activeHazards.RemoveAll(h => h == null);

        // Manage audio for each group
        for (int i = _hazardGroups.Count - 1; i >= 0; i--)
        {
            FireHazardGroup group = _hazardGroups[i];
            bool hasActiveHazards = group.HasActiveHazards();
            
            if (hasActiveHazards && group.audioSource != null && !group.audioSource.isPlaying && !group.isFadingOut)
            {
                // Start sound for this group
                if (fireTrail.fireWallsLoopSound != null)
                {
                    group.audioSource.volume = 0f;
                    fireTrail.fireWallsLoopSound.Play(group.audioSource);
                    StartCoroutine(FadeGroupVolume(group, fireTrail.fireWallsLoopSound.volume));
                }
            }
            else if (!hasActiveHazards && group.audioSource != null)
            {
                // All hazards in this group are gone
                if (group.audioSource.isPlaying && !group.isFadingOut)
                {
                    // Fade out and cleanup
                    StartCoroutine(FadeGroupVolume(group, 0f, stopAfterFade: true, removeGroup: true));
                }
                else if (!group.audioSource.isPlaying)
                {
                    // Audio already stopped, just cleanup
                    Destroy(group.audioSource);
                    _hazardGroups.RemoveAt(i);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (_isActive)
        {
            // Drain capacity while active
            _currentCapacity = Mathf.Min(_currentCapacity + fireTrail.drainRate * Time.fixedDeltaTime, fireTrail.capacity);

            // Check if we've moved enough to spawn a new fire hazard
            float distanceMoved = Vector3.Distance(transform.position, _lastSpawnPosition);
            if (distanceMoved >= fireTrail.spawnInterval)
            {
                SpawnFireHazard();
                _lastSpawnPosition = transform.position;
            }

            // Stop if capacity is full (overheated)
            if (_currentCapacity >= fireTrail.capacity)
            {
                Debug.Log("Fire trail capacity full! Stopping.");
                StopFireTrail();
            }
        }
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);
        Debug.Log($"Fire Trail input received - isPressed: {value.isPressed}");

        if (value.isPressed)
        {
            if (_currentCapacity >= fireTrail.capacity)
            {
                Debug.Log("Cannot activate fire trail: capacity full (overheated)");
                return;
            }

            if (!_isActive && fireTrail.firePrefab != null)
            {
                StartFireTrail();
            }
        }
        else
        {
            if (_isActive)
            {
                StopFireTrail();
            }
        }
    }

    private void StartFireTrail()
    {
        Debug.Log("Starting fire trail");
        _isActive = true;
        _lastSpawnPosition = transform.position;

        // Create a new group for this activation
        AudioSource groupAudioSource = gameObject.AddComponent<AudioSource>();
        groupAudioSource.playOnAwake = false;
        groupAudioSource.loop = true;
        groupAudioSource.spatialBlend = 1f;
        groupAudioSource.rolloffMode = AudioRolloffMode.Linear;
        groupAudioSource.minDistance = 10f;
        groupAudioSource.maxDistance = 50f;
        groupAudioSource.dopplerLevel = 0f;
        
        _currentGroup = new FireHazardGroup(groupAudioSource);
        _hazardGroups.Add(_currentGroup);

        // Spawn initial fire hazard
        SpawnFireHazard();
    }

    private void StopFireTrail()
    {
        Debug.Log("Stopping fire trail");
        _isActive = false;
        _currentGroup = null;
    }

    private void SpawnFireHazard()
    {
        if (fireTrail.firePrefab == null) return;

        // Enforce max hazard limit
        if (fireTrail.maxActiveHazards > 0 && _activeHazards.Count >= fireTrail.maxActiveHazards)
        {
            // Remove the oldest hazard
            if (_activeHazards[0] != null)
            {
                Destroy(_activeHazards[0]);
            }
            _activeHazards.RemoveAt(0);
        }

        // Spawn behind the ship
        Vector3 spawnPosition = transform.position + transform.up * fireTrail.spawnOffset;
        
        GameObject hazard = Instantiate(fireTrail.firePrefab, spawnPosition, Quaternion.identity);
        
        FireHazard fireHazardComponent = hazard.GetComponent<FireHazard>();
        if (fireHazardComponent != null)
        {
            fireHazardComponent.Initialize(
                player.enemyTag,
                fireTrail.damagePerSecond,
                fireTrail.fireDuration,
                fireTrail.impactForce
            );
        }

        _activeHazards.Add(hazard);
        
        // Add to current group if active
        if (_currentGroup != null)
        {
            _currentGroup.hazards.Add(hazard);
        }
    }

    public override bool IsAbilityActive()
    {
        return _isActive;
    }

    public override void Die()
    {
        // Stop sound
        if (_fireLoopSource != null && _fireLoopSource.isPlaying)
        {
            _fireLoopSource.Stop();
        }

        if (_soundFadeCoroutine != null)
        {
            StopCoroutine(_soundFadeCoroutine);
        }

        // Clean up all group audio sources
        foreach (var group in _hazardGroups)
        {
            if (group.audioSource != null)
            {
                if (group.audioSource.isPlaying)
                {
                    group.audioSource.Stop();
                }
                Destroy(group.audioSource);
            }
        }
        _hazardGroups.Clear();

        // Clean up active hazards
        foreach (var hazard in _activeHazards)
        {
            if (hazard != null)
            {
                Destroy(hazard);
            }
        }
        _activeHazards.Clear();

        base.Die();
    }

    // ===== AUDIO =====
    private System.Collections.IEnumerator FadeVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_fireLoopSource == null) yield break;

        float startVolume = _fireLoopSource.volume;
        float elapsed = 0f;

        while (elapsed < fireTrail.soundFadeDuration)
        {
            if (_fireLoopSource == null || (!_fireLoopSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / fireTrail.soundFadeDuration;
            _fireLoopSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (_fireLoopSource == null) yield break;

        _fireLoopSource.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade && _fireLoopSource.isPlaying)
        {
            _fireLoopSource.Stop();
        }
    }

    private System.Collections.IEnumerator FadeGroupVolume(FireHazardGroup group, float targetVolume, bool stopAfterFade = false, bool removeGroup = false)
    {
        if (group.audioSource == null) yield break;

        if (stopAfterFade)
        {
            group.isFadingOut = true;
        }

        float startVolume = group.audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fireTrail.soundFadeDuration)
        {
            if (group.audioSource == null || (!group.audioSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / fireTrail.soundFadeDuration;
            group.audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (group.audioSource == null) yield break;

        group.audioSource.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade && group.audioSource.isPlaying)
        {
            group.audioSource.Stop();
        }

        if (removeGroup)
        {
            Destroy(group.audioSource);
            _hazardGroups.Remove(group);
        }
    }

    /// <summary>
    /// Returns the current capacity ratio (0 to 1) for UI display.
    /// </summary>
    public float GetCapacityRatio()
    {
        return fireTrail.capacity > 0 ? _currentCapacity / fireTrail.capacity : 0f;
    }
}
