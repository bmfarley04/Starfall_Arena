using UnityEngine;

[DisallowMultipleComponent]
public class DeathEffects3D : MonoBehaviour
{
    [System.Serializable]
    private struct ExplosionConfig3D
    {
        [Tooltip("Explosion prefab spawned when this ship dies.")]
        public GameObject prefab;
        [Tooltip("Uniform multiplier applied to the spawned explosion scale.")]
        public float scale;
        [Tooltip("Death sound played when the ship is destroyed.")]
        public SoundEffect sound;
    }

    [Header("Explosion")]
    [SerializeField] private ExplosionConfig3D explosion = new ExplosionConfig3D
    {
        scale = 1f
    };
    [SerializeField] [Range(0f, 1f)] private float deathSoundSpatialBlend = 1f;

    [Header("Part Scatter")]
    [Tooltip("Root that contains the ship parts with ShipPartScatter3D components. Defaults to this transform.")]
    [SerializeField] private Transform scatterRoot;
    [Tooltip("Chance for each configured part to scatter when the ship dies.")]
    [SerializeField] [Range(0f, 1f)] private float scatterChance = 0.75f;

    private Entity3D _entity;
    private bool _hasPlayed;

    private void Awake()
    {
        _entity = GetComponent<Entity3D>();

        if (scatterRoot == null)
        {
            scatterRoot = transform;
        }
    }

    public void PlayDeathEffects(Vector3 damageDirection)
    {
        if (_hasPlayed)
        {
            return;
        }

        _hasPlayed = true;

        PlayExplosion();
        ScatterShipParts(ResolveScatterDirection(damageDirection));
    }

    private void PlayExplosion()
    {
        if (explosion.prefab != null)
        {
            GameObject spawnedExplosion = Instantiate(explosion.prefab, transform.position, transform.rotation);
            spawnedExplosion.transform.localScale = Vector3.one * Mathf.Max(0.01f, explosion.scale);
        }

        PlayDeathSound();
    }

    private void ScatterShipParts(Vector3 scatterDirection)
    {
        if (scatterRoot == null)
        {
            return;
        }

        ShipPartScatter3D[] parts = scatterRoot.GetComponentsInChildren<ShipPartScatter3D>(true);
        if (parts == null || parts.Length == 0)
        {
            return;
        }

        Vector3 inheritedVelocity = Vector3.zero;
        if (_entity != null && _entity.Flight != null && _entity.Flight.Rigidbody != null)
        {
            inheritedVelocity = _entity.Flight.Rigidbody.linearVelocity;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            ShipPartScatter3D part = parts[i];
            if (part == null || Random.value > scatterChance)
            {
                continue;
            }

            part.Scatter(scatterDirection, inheritedVelocity);
        }
    }

    private Vector3 ResolveScatterDirection(Vector3 damageDirection)
    {
        if (damageDirection.sqrMagnitude > 0.0001f)
        {
            return damageDirection.normalized;
        }

        Vector3 fallbackDirection = transform.forward;
        if (_entity != null && _entity.Flight != null)
        {
            Vector3 flightVelocity = _entity.Flight.LinearVelocity;
            if (flightVelocity.sqrMagnitude > 0.0001f)
            {
                fallbackDirection = flightVelocity.normalized;
            }
        }

        if (fallbackDirection.sqrMagnitude <= 0.0001f)
        {
            fallbackDirection = Random.onUnitSphere;
        }

        return fallbackDirection.normalized;
    }

    private void PlayDeathSound()
    {
        if (explosion.sound == null || explosion.sound.clip == null)
        {
            return;
        }

        GameObject tempAudio = new GameObject("TempDeathAudio3D_" + explosion.sound.clip.name);
        tempAudio.transform.position = transform.position;

        AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
        tempSource.clip = explosion.sound.clip;
        tempSource.volume = explosion.sound.volume;
        tempSource.pitch = Random.Range(explosion.sound.minPitch, explosion.sound.maxPitch);
        tempSource.spatialBlend = deathSoundSpatialBlend;
        tempSource.Play();

        Destroy(tempAudio, explosion.sound.clip.length / Mathf.Max(0.01f, tempSource.pitch));
    }
}
