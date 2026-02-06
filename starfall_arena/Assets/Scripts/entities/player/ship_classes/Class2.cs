using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== ABILITY CONFIGURATION STRUCTS =====

[System.Serializable]
public struct EmpoweredShotAbilityConfig
{
    [Header("Cooldown")]
    [Tooltip("Cooldown between empowered shots (seconds)")]
    public float cooldown;

    [Header("Projectile")]
    [Tooltip("Empowered projectile prefab (different visual from normal shots)")]
    public GameObject projectilePrefab;
    [Tooltip("Damage multiplier compared to normal shot")]
    public float damageMultiplier;
    [Tooltip("Speed multiplier compared to normal shot")]
    public float speedMultiplier;
    [Tooltip("Impact force multiplier compared to normal shot")]
    public float impactMultiplier;
    [Tooltip("Recoil multiplier compared to normal shot")]
    public float recoilMultiplier;
    [Tooltip("Projectile lifetime (seconds)")]
    public float lifetime;

    [Header("Slow Effect")]
    [Tooltip("Slow multiplier applied to target (0.5 = 50% speed)")]
    [Range(0f, 1f)]
    public float slowMultiplier;
    [Tooltip("Duration of slow effect (seconds)")]
    public float slowDuration;

    [Header("Sound Effects")]
    [Tooltip("Sound played when firing empowered shot")]
    public SoundEffect fireSound;
}

[System.Serializable]
public struct ShieldAbilityConfig
{
    [Header("Timing")]
    [Tooltip("Cooldown between uses (seconds)")]
    public float cooldown;
    [Tooltip("Shield active duration (seconds)")]
    public float activeDuration;

    [Header("Shield")]
    [Tooltip("ReflectShield component (drag from Hierarchy)")]
    public ReflectShield shield;
    [Tooltip("Color of the shield when active")]
    public Color shieldColor;

    [Header("Sound Effects")]
    [Tooltip("Shield duration sound (loops while active)")]
    public SoundEffect shieldLoopSound;
    [Tooltip("Sound when projectile hits the shield")]
    public SoundEffect shieldHitSound;
}

[System.Serializable]
public struct TractorBeamAbilityConfig
{
    [Header("Timing")]
    [Tooltip("Cooldown between uses (seconds)")]
    public float cooldown;
    [Tooltip("Duration the beam stays active (seconds)")]
    public float duration;

    [Header("Area of Effect")]
    [Tooltip("Half-angle of the cone in degrees (e.g., 30 = 60 degree cone total)")]
    [Range(5f, 90f)]
    public float coneHalfAngle;
    [Tooltip("Maximum range of the tractor beam")]
    public float coneRange;

    [Header("Pull Effect")]
    [Tooltip("Speed at which targets are pulled toward the ship (units/second)")]
    public float pullSpeed;
    [Tooltip("If true, completely stops target movement while in beam")]
    public bool freezeTargetMovement;

    [Header("Visuals")]
    [Tooltip("Color of the tractor beam cone")]
    public Color beamColor;
    [Tooltip("Material for the cone effect (should support transparency)")]
    public Material coneMaterial;
    [Tooltip("Number of segments for cone mesh (higher = smoother)")]
    [Range(8, 64)]
    public int coneSegments;
    [Tooltip("Offset of cone visual relative to ship (local space)")]
    public Vector3 coneOffset;
    [Tooltip("Particle system for suction effect (should be a child of ship)")]
    public ParticleSystem suctionParticles;

    [Header("Sound Effects")]
    [Tooltip("Sound that loops while tractor beam is active")]
    public SoundEffect beamLoopSound;
}

[System.Serializable]
public struct Class2AbilitiesConfig
{
    [Header("Ability 1 - Empowered Shot")]
    public EmpoweredShotAbilityConfig empoweredShot;

    [Header("Ability 2 - Shield")]
    public ShieldAbilityConfig shield;

    [Header("Ability 3 - Tractor Beam")]
    public TractorBeamAbilityConfig tractorBeam;
}

public class Class2 : Player
{
    // ===== ABILITIES =====
    [Header("Abilities")]
    public Class2AbilitiesConfig abilities;

    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    [SerializeField] private float _fireCooldown = 0.5f;

    [Header("Convergence Settings")]
    [Tooltip("Distance ahead of ship where all projectiles converge (all shots meet at this point)")]
    [SerializeField] private float convergenceDistance = 20f;

    // ===== PRIVATE STATE =====
    private float _lastEmpoweredShotTime = -999f;
    private float _lastShieldTime = -999f;
    private Coroutine _shieldCoroutine;
    private AudioSource _shieldSource;

    // Tractor Beam State
    private float _lastTractorBeamTime = -999f;
    private bool _isTractorBeamActive = false;
    private Coroutine _tractorBeamCoroutine;
    private AudioSource _tractorBeamSource;
    private GameObject _tractorBeamConeObject;
    private MeshFilter _tractorBeamConeMeshFilter;
    private MeshRenderer _tractorBeamConeMeshRenderer;
    private System.Collections.Generic.List<Entity> _tractorBeamTargets = new System.Collections.Generic.List<Entity>();

    protected override void Awake()
    {
        base.Awake();
        // Sync Inspector value to parent's protected field
        fireCooldown = _fireCooldown;

        _shieldSource = gameObject.AddComponent<AudioSource>();
        _shieldSource.playOnAwake = false;
        _shieldSource.loop = true;
        _shieldSource.spatialBlend = 1f;
        _shieldSource.rolloffMode = AudioRolloffMode.Linear;
        _shieldSource.minDistance = 10f;
        _shieldSource.maxDistance = 50f;
        _shieldSource.dopplerLevel = 0f;

        // Initialize tractor beam audio source
        _tractorBeamSource = gameObject.AddComponent<AudioSource>();
        _tractorBeamSource.playOnAwake = false;
        _tractorBeamSource.loop = true;
        _tractorBeamSource.spatialBlend = 1f;
        _tractorBeamSource.rolloffMode = AudioRolloffMode.Linear;
        _tractorBeamSource.minDistance = 10f;
        _tractorBeamSource.maxDistance = 50f;
        _tractorBeamSource.dopplerLevel = 0f;

        // Initialize tractor beam cone visual
        InitializeTractorBeamCone();
    }

    protected override void Update()
    {
        base.Update();

        // Update tractor beam cone position and rotation to follow ship
        if (_isTractorBeamActive && _tractorBeamConeObject != null)
        {
            // Apply offset in local space (rotates with ship)
            Vector3 worldOffset = transform.TransformDirection(abilities.tractorBeam.coneOffset);
            _tractorBeamConeObject.transform.position = transform.position + worldOffset;
            _tractorBeamConeObject.transform.rotation = transform.rotation;
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        // Apply tractor beam pull effect to targets
        if (_isTractorBeamActive)
        {
            ApplyTractorBeamPull();
        }
    }

    protected override void TryFireProjectile()
    {
        if (projectileWeapon.prefab == null)
            return;

        if (Time.time < _lastFireTime + fireCooldown)
            return;

        // Calculate convergence point ahead of ship
        Vector3 convergencePoint = transform.position + transform.up * convergenceDistance;

        for (int i = 0; i < turrets.Length; i++)
        {
            Transform turret = turrets[i];

            // Calculate direction from turret to convergence point
            Vector3 directionToConvergence = (convergencePoint - turret.position).normalized;

            GameObject projectile = Instantiate(projectileWeapon.prefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = enemyTag;
                projectileScript.Initialize(
                    directionToConvergence,
                    Vector2.zero,
                    projectileWeapon.speed,
                    projectileWeapon.damage,
                    projectileWeapon.lifetime,
                    projectileWeapon.impactForce,
                    this
                );
            }
        }

        ApplyRecoil(projectileWeapon.recoilForce);

        if (projectileFireSound != null)
        {
            projectileFireSound.Play(GetAvailableAudioSource());
        }

        _lastFireTime = Time.time;
    }

    // ===== ABILITY INPUT CALLBACKS =====
    void OnAbility3()
    {
        if (Time.time < _lastEmpoweredShotTime + abilities.empoweredShot.cooldown)
        {
            Debug.Log($"Empowered Shot on cooldown: {(_lastEmpoweredShotTime + abilities.empoweredShot.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (abilities.empoweredShot.projectilePrefab == null)
        {
            Debug.LogWarning("Empowered Shot projectile prefab not assigned!");
            return;
        }

        FireEmpoweredShot();
        _lastEmpoweredShotTime = Time.time;
    }

    // ===== EMPOWERED SHOT ABILITY =====
    private void FireEmpoweredShot()
    {
        // Calculate base stats from primary weapon
        float damage = projectileWeapon.damage * abilities.empoweredShot.damageMultiplier;
        float speed = projectileWeapon.speed * abilities.empoweredShot.speedMultiplier;
        float impactForce = projectileWeapon.impactForce * abilities.empoweredShot.impactMultiplier;
        float recoil = projectileWeapon.recoilForce * abilities.empoweredShot.recoilMultiplier;

        // Calculate convergence point ahead of ship
        Vector3 convergencePoint = transform.position + transform.up * convergenceDistance;

        for (int i = 0; i < turrets.Length; i++)
        {
            Transform turret = turrets[i];

            // Calculate direction from turret to convergence point
            Vector3 directionToConvergence = (convergencePoint - turret.position).normalized;

            GameObject projectile = Instantiate(abilities.empoweredShot.projectilePrefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = enemyTag;
                projectileScript.Initialize(
                    directionToConvergence,
                    Vector2.zero,
                    speed,
                    damage,
                    abilities.empoweredShot.lifetime,
                    impactForce,
                    this
                );

                // Enable slow effect
                projectileScript.EnableSlow(abilities.empoweredShot.slowMultiplier, abilities.empoweredShot.slowDuration);
            }
        }

        ApplyRecoil(recoil);

        if (abilities.empoweredShot.fireSound != null)
        {
            abilities.empoweredShot.fireSound.Play(GetAvailableAudioSource());
        }

        Debug.Log($"Empowered Shot fired! Damage: {damage:F1}, Speed: {speed:F1}, Slow: {abilities.empoweredShot.slowMultiplier * 100}% for {abilities.empoweredShot.slowDuration}s");
    }

    // ===== SHIELD ABILITY =====
    void OnAbility4()
    {
        if (Time.time < _lastShieldTime + abilities.shield.cooldown)
        {
            Debug.Log($"Shield on cooldown: {(_lastShieldTime + abilities.shield.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (abilities.shield.shield == null)
        {
            Debug.LogWarning("Shield not assigned!");
            return;
        }

        _lastShieldTime = Time.time;

        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
        }
        _shieldCoroutine = StartCoroutine(ActivateShield());
    }

    private System.Collections.IEnumerator ActivateShield()
    {
        abilities.shield.shield.Activate(abilities.shield.shieldColor);

        if (abilities.shield.shieldLoopSound != null && _shieldSource != null)
        {
            abilities.shield.shieldLoopSound.Play(_shieldSource);
        }

        yield return new WaitForSeconds(abilities.shield.activeDuration);

        abilities.shield.shield.Deactivate();

        if (_shieldSource != null && _shieldSource.isPlaying)
        {
            _shieldSource.Stop();
        }
    }

    // ===== TRACTOR BEAM ABILITY =====
    void OnAbility2()
    {
        if (Time.time < _lastTractorBeamTime + abilities.tractorBeam.cooldown)
        {
            Debug.Log($"Tractor Beam on cooldown: {(_lastTractorBeamTime + abilities.tractorBeam.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (_isTractorBeamActive)
        {
            return;
        }

        _lastTractorBeamTime = Time.time;

        if (_tractorBeamCoroutine != null)
        {
            StopCoroutine(_tractorBeamCoroutine);
        }
        _tractorBeamCoroutine = StartCoroutine(ActivateTractorBeam());
    }

    private void InitializeTractorBeamCone()
    {
        // Create a child object for the cone visual
        _tractorBeamConeObject = new GameObject("TractorBeamCone");
        _tractorBeamConeObject.transform.SetParent(transform);
        _tractorBeamConeObject.transform.localPosition = Vector3.zero;
        _tractorBeamConeObject.transform.localRotation = Quaternion.identity;

        _tractorBeamConeMeshFilter = _tractorBeamConeObject.AddComponent<MeshFilter>();
        _tractorBeamConeMeshRenderer = _tractorBeamConeObject.AddComponent<MeshRenderer>();

        // Set up material
        if (abilities.tractorBeam.coneMaterial != null)
        {
            _tractorBeamConeMeshRenderer.material = abilities.tractorBeam.coneMaterial;
        }
        else
        {
            // Create a default transparent material
            Material defaultMat = new Material(Shader.Find("Sprites/Default"));
            defaultMat.color = new Color(0f, 1f, 1f, 0.5f); // Cyan
            _tractorBeamConeMeshRenderer.material = defaultMat;
        }

        // Ensure it renders on top of other sprites
        _tractorBeamConeMeshRenderer.sortingOrder = 100;

        // Generate the cone mesh
        GenerateTractorBeamConeMesh();

        // Start hidden
        _tractorBeamConeObject.SetActive(false);
    }

    private void GenerateTractorBeamConeMesh()
    {
        int segments = Mathf.Max(8, abilities.tractorBeam.coneSegments);
        float halfAngle = abilities.tractorBeam.coneHalfAngle * Mathf.Deg2Rad;
        float range = abilities.tractorBeam.coneRange;

        // Safety check for uninitialized values
        if (range <= 0f) range = 10f;
        if (halfAngle <= 0f) halfAngle = 30f * Mathf.Deg2Rad;

        Mesh mesh = new Mesh();

        // Vertices: origin point + outer ring
        // Z = -1 to render in front of ships in 2.5D
        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = new Vector3(0, 0, -1f); // Origin at ship position

        // Generate outer ring vertices
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);

            // In 2D space, the cone extends in the Y+ direction (transform.up)
            // X is left/right spread
            float x = Mathf.Sin(angle) * range;
            float y = Mathf.Cos(angle) * range;

            vertices[i + 1] = new Vector3(x, y, -1f);
        }

        // Triangles: double-sided (both front and back faces)
        int[] triangles = new int[segments * 6]; // Double for both sides
        for (int i = 0; i < segments; i++)
        {
            // Front face (counter-clockwise when viewed from -Z)
            triangles[i * 6] = 0;
            triangles[i * 6 + 1] = i + 2;
            triangles[i * 6 + 2] = i + 1;

            // Back face (clockwise when viewed from -Z)
            triangles[i * 6 + 3] = 0;
            triangles[i * 6 + 4] = i + 1;
            triangles[i * 6 + 5] = i + 2;
        }

        // UVs for potential texture mapping
        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = new Vector2(0.5f, 0);
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            uvs[i + 1] = new Vector2(t, 1);
        }

        // Colors for vertex coloring (fade from center to edge)
        Color[] colors = new Color[vertices.Length];
        Color beamColor = abilities.tractorBeam.beamColor;

        // Default to cyan if color not set
        if (beamColor.a <= 0.01f)
        {
            beamColor = new Color(0f, 1f, 1f, 0.6f); // Cyan with 60% alpha
        }

        colors[0] = new Color(beamColor.r, beamColor.g, beamColor.b, beamColor.a);
        for (int i = 1; i < vertices.Length; i++)
        {
            colors[i] = new Color(beamColor.r, beamColor.g, beamColor.b, beamColor.a * 0.3f);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.RecalculateNormals();

        _tractorBeamConeMeshFilter.mesh = mesh;
    }

    private System.Collections.IEnumerator ActivateTractorBeam()
    {
        _isTractorBeamActive = true;

        // Regenerate mesh in case settings changed
        GenerateTractorBeamConeMesh();

        // Show cone visual
        if (_tractorBeamConeObject != null)
        {
            // Detach from parent so it can follow position but not inherit scale issues
            _tractorBeamConeObject.transform.SetParent(null);
            Vector3 worldOffset = transform.TransformDirection(abilities.tractorBeam.coneOffset);
            _tractorBeamConeObject.transform.position = transform.position + worldOffset;
            _tractorBeamConeObject.transform.rotation = transform.rotation;
            _tractorBeamConeObject.SetActive(true);
            Debug.Log($"Tractor beam cone activated at {transform.position}, range: {abilities.tractorBeam.coneRange}, angle: {abilities.tractorBeam.coneHalfAngle}");
        }

        // Start loop sound
        if (abilities.tractorBeam.beamLoopSound != null && _tractorBeamSource != null)
        {
            abilities.tractorBeam.beamLoopSound.Play(_tractorBeamSource);
        }

        // Start suction particles
        if (abilities.tractorBeam.suctionParticles != null)
        {
            abilities.tractorBeam.suctionParticles.Play();
        }

        Debug.Log($"Tractor Beam activated! Duration: {abilities.tractorBeam.duration}s, Range: {abilities.tractorBeam.coneRange}, Angle: {abilities.tractorBeam.coneHalfAngle * 2}°");

        yield return new WaitForSeconds(abilities.tractorBeam.duration);

        DeactivateTractorBeam();
    }

    private void DeactivateTractorBeam()
    {
        _isTractorBeamActive = false;
        _tractorBeamTargets.Clear();

        // Hide cone visual
        if (_tractorBeamConeObject != null)
        {
            _tractorBeamConeObject.SetActive(false);
            _tractorBeamConeObject.transform.SetParent(transform);
            _tractorBeamConeObject.transform.localPosition = Vector3.zero;
            _tractorBeamConeObject.transform.localRotation = Quaternion.identity;
        }

        // Stop loop sound
        if (_tractorBeamSource != null && _tractorBeamSource.isPlaying)
        {
            _tractorBeamSource.Stop();
        }

        // Stop suction particles
        if (abilities.tractorBeam.suctionParticles != null)
        {
            abilities.tractorBeam.suctionParticles.Stop();
        }

        Debug.Log("Tractor Beam deactivated");
    }

    private void ApplyTractorBeamPull()
    {
        // Find all potential targets in range
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, abilities.tractorBeam.coneRange);

        _tractorBeamTargets.Clear();

        foreach (Collider2D col in colliders)
        {
            // Skip self
            if (col.gameObject == gameObject)
                continue;

            // Check if it's an entity (player or enemy)
            Entity entity = col.GetComponent<Entity>();
            if (entity == null)
                continue;

            // Check if target is within cone angle
            Vector2 directionToTarget = (col.transform.position - transform.position).normalized;
            Vector2 forwardDirection = transform.up;

            float angleToTarget = Vector2.Angle(forwardDirection, directionToTarget);

            if (angleToTarget <= abilities.tractorBeam.coneHalfAngle)
            {
                _tractorBeamTargets.Add(entity);

                // Get target's rigidbody
                Rigidbody2D targetRb = col.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    // Optionally freeze target movement
                    if (abilities.tractorBeam.freezeTargetMovement)
                    {
                        targetRb.linearVelocity = Vector2.zero;
                    }

                    // Pull target toward this ship
                    Vector2 pullDirection = ((Vector2)transform.position - (Vector2)col.transform.position).normalized;
                    float distanceToTarget = Vector2.Distance(transform.position, col.transform.position);

                    // Don't pull if already very close
                    if (distanceToTarget > 1f)
                    {
                        Vector2 pullVelocity = pullDirection * abilities.tractorBeam.pullSpeed;

                        if (abilities.tractorBeam.freezeTargetMovement)
                        {
                            // Set velocity directly for frozen targets
                            targetRb.linearVelocity = pullVelocity;
                        }
                        else
                        {
                            // Add force for non-frozen targets
                            targetRb.AddForce(pullVelocity * targetRb.mass, ForceMode2D.Force);
                        }
                    }
                }
            }
        }
    }

    // ===== DAMAGE OVERRIDE =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        if (abilities.shield.shield != null && abilities.shield.shield.IsActive())
        {
            return;
        }

        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
        if (_shieldSource != null && _shieldSource.isPlaying)
        {
            _shieldSource.Stop();
        }

        if (_tractorBeamSource != null && _tractorBeamSource.isPlaying)
        {
            _tractorBeamSource.Stop();
        }

        if (_isTractorBeamActive)
        {
            DeactivateTractorBeam();
        }

        // Clean up the cone object
        if (_tractorBeamConeObject != null)
        {
            Destroy(_tractorBeamConeObject);
        }

        base.Die();
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (abilities.shield.shield != null && abilities.shield.shield.IsActive())
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == thisPlayerTag)
            {
                Vector3 hitPoint = collider.ClosestPoint(transform.position);
                abilities.shield.shield.OnReflectHit(hitPoint);

                if (abilities.shield.shieldHitSound != null)
                {
                    abilities.shield.shieldHitSound.Play(GetAvailableAudioSource());
                }

                Destroy(projectile.gameObject);
            }
        }
    }
}
