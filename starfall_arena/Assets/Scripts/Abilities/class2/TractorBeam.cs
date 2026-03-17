using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TractorBeam : Ability
{
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

    [Header("Ability 3 - Tractor Beam")]
    public TractorBeamAbilityConfig tractorBeam;

    private float _lastTractorBeamTime = -999f;
    private bool _isTractorBeamActive = false;
    private Coroutine _tractorBeamCoroutine;
    private AudioSource _tractorBeamSource;
    private GameObject _tractorBeamConeObject;
    private MeshFilter _tractorBeamConeMeshFilter;
    private MeshRenderer _tractorBeamConeMeshRenderer;
    private readonly List<Entity> _tractorBeamTargets = new List<Entity>();
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();

        _tractorBeamSource = gameObject.AddComponent<AudioSource>();
        _tractorBeamSource.playOnAwake = false;
        _tractorBeamSource.loop = true;
        _tractorBeamSource.spatialBlend = 0f;

        InitializeTractorBeamCone();
    }

    private void Update()
    {
        if (_isTractorBeamActive && _tractorBeamConeObject != null)
        {
            Vector3 worldOffset = transform.TransformDirection(tractorBeam.coneOffset);
            _tractorBeamConeObject.transform.position = transform.position + worldOffset;
            _tractorBeamConeObject.transform.rotation = transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (NetTickUtil.IsActive && (_netMovement == null || !_netMovement.IsServer))
        {
            return;
        }

        if (_isTractorBeamActive)
        {
            ApplyTractorBeamPull();
        }
    }

    public override bool TryUseAbility(InputValue value)
    {
        stats.cooldown = tractorBeam.cooldown;
        stats.duration = tractorBeam.duration;

        if (!CanUseAbility())
        {
            return false;
        }

        if (_isTractorBeamActive)
        {
            return false;
        }

        _lastTractorBeamTime = Time.time;
        lastUsedAbility = Time.time;
        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkTractorBeamState(true, authoritative: false);
            }

            _netMovement.RequestTractorBeamState(true);
            return true;
        }

        ApplyNetworkTractorBeamState(true, authoritative: true);
        return true;
    }

    public override bool IsAbilityActive()
    {
        return _isTractorBeamActive;
    }

    public override float GetHUDFillRatio()
    {
        if (tractorBeam.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastTractorBeamTime;
        if (elapsed >= tractorBeam.cooldown) return 0f;
        return 1f - (elapsed / tractorBeam.cooldown);
    }

    public override bool IsOnCooldown()
    {
        return Time.time < _lastTractorBeamTime + tractorBeam.cooldown;
    }

    public override void Die()
    {
        if (_tractorBeamSource != null && _tractorBeamSource.isPlaying)
        {
            _tractorBeamSource.Stop();
        }

        if (_isTractorBeamActive)
        {
            DeactivateTractorBeam();
        }

        if (_tractorBeamConeObject != null)
        {
            Destroy(_tractorBeamConeObject);
        }

        base.Die();
    }

    private void OnDestroy()
    {
        if (_tractorBeamConeObject != null)
        {
            Destroy(_tractorBeamConeObject);
        }
    }

    private void InitializeTractorBeamCone()
    {
        _tractorBeamConeObject = new GameObject("TractorBeamCone");
        _tractorBeamConeObject.transform.SetParent(transform);
        _tractorBeamConeObject.transform.localPosition = Vector3.zero;
        _tractorBeamConeObject.transform.localRotation = Quaternion.identity;

        _tractorBeamConeMeshFilter = _tractorBeamConeObject.AddComponent<MeshFilter>();
        _tractorBeamConeMeshRenderer = _tractorBeamConeObject.AddComponent<MeshRenderer>();

        if (tractorBeam.coneMaterial != null)
        {
            _tractorBeamConeMeshRenderer.material = tractorBeam.coneMaterial;
        }
        else
        {
            Material defaultMat = new Material(Shader.Find("Sprites/Default"));
            defaultMat.color = new Color(0f, 1f, 1f, 0.5f);
            _tractorBeamConeMeshRenderer.material = defaultMat;
        }

        _tractorBeamConeMeshRenderer.sortingOrder = 100;
        GenerateTractorBeamConeMesh();
        _tractorBeamConeObject.SetActive(false);
    }

    private void GenerateTractorBeamConeMesh()
    {
        int segments = Mathf.Max(8, tractorBeam.coneSegments);
        float halfAngle = tractorBeam.coneHalfAngle * Mathf.Deg2Rad;
        float range = tractorBeam.coneRange;

        if (range <= 0f) range = 10f;
        if (halfAngle <= 0f) halfAngle = 30f * Mathf.Deg2Rad;

        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = new Vector3(0, 0, -1f);

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            float x = Mathf.Sin(angle) * range;
            float y = Mathf.Cos(angle) * range;
            vertices[i + 1] = new Vector3(x, y, -1f);
        }

        int[] triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 6] = 0;
            triangles[i * 6 + 1] = i + 2;
            triangles[i * 6 + 2] = i + 1;
            triangles[i * 6 + 3] = 0;
            triangles[i * 6 + 4] = i + 1;
            triangles[i * 6 + 5] = i + 2;
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = new Vector2(0.5f, 0);
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            uvs[i + 1] = new Vector2(t, 1);
        }

        Color[] colors = new Color[vertices.Length];
        Color beamColor = tractorBeam.beamColor;
        if (beamColor.a <= 0.01f)
        {
            beamColor = new Color(0f, 1f, 1f, 0.6f);
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

        GenerateTractorBeamConeMesh();

        if (_tractorBeamConeObject != null)
        {
            _tractorBeamConeObject.transform.SetParent(null);
            Vector3 worldOffset = transform.TransformDirection(tractorBeam.coneOffset);
            _tractorBeamConeObject.transform.position = transform.position + worldOffset;
            _tractorBeamConeObject.transform.rotation = transform.rotation;
            _tractorBeamConeObject.SetActive(true);
        }

        if (tractorBeam.beamLoopSound != null && _tractorBeamSource != null)
        {
            tractorBeam.beamLoopSound.Play(_tractorBeamSource);
        }

        if (tractorBeam.suctionParticles != null)
        {
            tractorBeam.suctionParticles.Play();
        }

        yield return new WaitForSeconds(tractorBeam.duration);

        DeactivateTractorBeam();

        if (NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner)
        {
            _netMovement.RequestTractorBeamState(false);
        }
    }

    private void DeactivateTractorBeam()
    {
        _isTractorBeamActive = false;
        _tractorBeamTargets.Clear();

        if (_tractorBeamConeObject != null)
        {
            _tractorBeamConeObject.SetActive(false);
            _tractorBeamConeObject.transform.SetParent(transform);
            _tractorBeamConeObject.transform.localPosition = Vector3.zero;
            _tractorBeamConeObject.transform.localRotation = Quaternion.identity;
        }

        if (_tractorBeamSource != null && _tractorBeamSource.isPlaying)
        {
            _tractorBeamSource.Stop();
        }

        if (tractorBeam.suctionParticles != null)
        {
            tractorBeam.suctionParticles.Stop();
        }
    }

    public void ApplyNetworkTractorBeamState(bool isActive, bool authoritative)
    {
        if (isActive)
        {
            if (_tractorBeamCoroutine != null)
            {
                StopCoroutine(_tractorBeamCoroutine);
            }

            _tractorBeamCoroutine = StartCoroutine(ActivateTractorBeam());
            return;
        }

        DeactivateTractorBeam();
    }

    private void ApplyTractorBeamPull()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, tractorBeam.coneRange);

        _tractorBeamTargets.Clear();

        foreach (Collider2D col in colliders)
        {
            if (col.gameObject == gameObject)
                continue;

            Entity entity = col.GetComponent<Entity>();
            if (entity == null)
                continue;

            Vector2 directionToTarget = (col.transform.position - transform.position).normalized;
            Vector2 forwardDirection = transform.up;
            float angleToTarget = Vector2.Angle(forwardDirection, directionToTarget);

            if (angleToTarget <= tractorBeam.coneHalfAngle)
            {
                _tractorBeamTargets.Add(entity);

                Rigidbody2D targetRb = col.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    if (tractorBeam.freezeTargetMovement)
                    {
                        targetRb.linearVelocity = Vector2.zero;
                    }

                    Vector2 pullDirection = ((Vector2)transform.position - (Vector2)col.transform.position).normalized;
                    float distanceToTarget = Vector2.Distance(transform.position, col.transform.position);

                    if (distanceToTarget > 1f)
                    {
                        Vector2 pullVelocity = pullDirection * tractorBeam.pullSpeed;

                        if (tractorBeam.freezeTargetMovement)
                        {
                            targetRb.linearVelocity = pullVelocity;
                        }
                        else
                        {
                            targetRb.linearVelocity += pullVelocity * Time.fixedDeltaTime;
                        }
                    }
                }
            }
        }
    }
}
