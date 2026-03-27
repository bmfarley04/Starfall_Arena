using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TractorBeam3D : Ability3D
{
    private const int MaxOverlapResults = 64;
    private static readonly Collider[] OverlapResults = new Collider[MaxOverlapResults];

    [System.Serializable]
    public struct TractorBeamAbilityConfig3D
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses in seconds.")]
        public float cooldown;
        [Tooltip("How long the tractor beam stays active in seconds.")]
        public float duration;

        [Header("Area of Effect")]
        [Range(5f, 90f)]
        [Tooltip("Half-angle of the tractor cone in degrees.")]
        public float coneHalfAngle;
        [Tooltip("Maximum range of the tractor beam.")]
        public float coneRange;
        [Tooltip("Layer mask used when gathering pull targets.")]
        public LayerMask targetMask;

        [Header("Pull Effect")]
        [Tooltip("Speed applied while pulling targets toward the ship.")]
        public float pullSpeed;
        [Tooltip("If true, targets inside the cone stop using their current velocity before the pull is applied.")]
        public bool freezeTargetMovement;
        [Tooltip("How close a target can get before the beam stops pulling it.")]
        public float stopDistance;

        [Header("Visuals")]
        [Tooltip("Tint applied to the cone mesh.")]
        public Color beamColor;
        [Tooltip("Material used for the cone mesh.")]
        public Material coneMaterial;
        [Range(8, 64)]
        [Tooltip("Number of fan segments used to build the cone mesh.")]
        public int coneSegments;
        [Tooltip("Cone origin offset in local space.")]
        public Vector3 coneOffset;
        [Tooltip("Optional particle system for suction feedback.")]
        public ParticleSystem suctionParticles;

        [Header("Sound Effects")]
        [Tooltip("Looping sound played while the tractor beam is active.")]
        public SoundEffect beamLoopSound;
    }

    [Header("Ability 3 - Tractor Beam 3D")]
    [SerializeField] private TractorBeamAbilityConfig3D tractorBeam = new TractorBeamAbilityConfig3D
    {
        cooldown = 6f,
        duration = 2f,
        coneHalfAngle = 30f,
        coneRange = 15f,
        pullSpeed = 20f,
        stopDistance = 1f,
        beamColor = new Color(0f, 1f, 1f, 0.45f),
        coneSegments = 16,
        targetMask = ~0
    };
    [SerializeField] private AudioSource tractorBeamLoopAudioSource;

    private bool _isActive;
    private Coroutine _tractorBeamCoroutine;
    private GameObject _tractorBeamConeObject;
    private MeshFilter _tractorBeamConeMeshFilter;
    private MeshRenderer _tractorBeamConeMeshRenderer;

    protected override void Awake()
    {
        base.Awake();

        if (tractorBeamLoopAudioSource == null)
        {
            tractorBeamLoopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        tractorBeamLoopAudioSource.playOnAwake = false;
        tractorBeamLoopAudioSource.loop = true;
        tractorBeamLoopAudioSource.spatialBlend = 0f;

        InitializeTractorBeamCone();
    }

    private void Update()
    {
        if (_isActive && _tractorBeamConeObject != null)
        {
            AlignTractorBeamCone();
        }
    }

    private void FixedUpdate()
    {
        if (_isActive)
        {
            ApplyTractorBeamPull();
        }
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed || _isActive)
        {
            return false;
        }

        return base.TryUseAbility(value);
    }

    public override void UseAbility(InputValue value)
    {
        if (_tractorBeamCoroutine != null)
        {
            StopCoroutine(_tractorBeamCoroutine);
        }

        _tractorBeamCoroutine = StartCoroutine(ActivateTractorBeam());
    }

    public override bool IsAbilityActive()
    {
        return _isActive;
    }

    protected override float GetCooldownDuration()
    {
        return tractorBeam.cooldown;
    }

    public override void Die()
    {
        if (_tractorBeamCoroutine != null)
        {
            StopCoroutine(_tractorBeamCoroutine);
            _tractorBeamCoroutine = null;
        }

        DeactivateTractorBeam();

        if (_tractorBeamConeObject != null)
        {
            Destroy(_tractorBeamConeObject);
            _tractorBeamConeObject = null;
        }
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
        _tractorBeamConeObject = new GameObject("TractorBeamCone3D");
        _tractorBeamConeObject.transform.SetParent(transform, false);

        _tractorBeamConeMeshFilter = _tractorBeamConeObject.AddComponent<MeshFilter>();
        _tractorBeamConeMeshRenderer = _tractorBeamConeObject.AddComponent<MeshRenderer>();

        if (tractorBeam.coneMaterial != null)
        {
            _tractorBeamConeMeshRenderer.sharedMaterial = tractorBeam.coneMaterial;
        }
        else
        {
            Material fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
            fallbackMaterial.color = tractorBeam.beamColor;
            _tractorBeamConeMeshRenderer.material = fallbackMaterial;
        }

        _tractorBeamConeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _tractorBeamConeMeshRenderer.receiveShadows = false;
        _tractorBeamConeObject.SetActive(false);
        GenerateTractorBeamConeMesh();
    }

    private void GenerateTractorBeamConeMesh()
    {
        if (_tractorBeamConeMeshFilter != null && _tractorBeamConeMeshFilter.sharedMesh != null)
        {
            Destroy(_tractorBeamConeMeshFilter.sharedMesh);
        }

        int segments = Mathf.Max(8, tractorBeam.coneSegments);
        float halfAngleRadians = Mathf.Max(1f, tractorBeam.coneHalfAngle) * Mathf.Deg2Rad;
        float range = Mathf.Max(0.1f, tractorBeam.coneRange);

        Mesh mesh = new Mesh
        {
            name = "TractorBeamConeMesh3D"
        };

        Vector3[] vertices = new Vector3[segments + 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0f);
        colors[0] = tractorBeam.beamColor;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-halfAngleRadians, halfAngleRadians, t);
            float x = Mathf.Sin(angle) * range;
            float z = Mathf.Cos(angle) * range;
            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(t, 1f);
            colors[i + 1] = new Color(tractorBeam.beamColor.r, tractorBeam.beamColor.g, tractorBeam.beamColor.b, tractorBeam.beamColor.a * 0.25f);
        }

        for (int i = 0; i < segments; i++)
        {
            int triangleStart = i * 3;
            triangles[triangleStart] = 0;
            triangles[triangleStart + 1] = i + 1;
            triangles[triangleStart + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        _tractorBeamConeMeshFilter.sharedMesh = mesh;
    }

    private IEnumerator ActivateTractorBeam()
    {
        _isActive = true;
        GenerateTractorBeamConeMesh();
        AlignTractorBeamCone();
        _tractorBeamConeObject?.SetActive(true);
        tractorBeam.suctionParticles?.Play();
        StartBeamLoopSound();
        yield return new WaitForSeconds(tractorBeam.duration);
        DeactivateTractorBeam();
        _tractorBeamCoroutine = null;
    }

    private void DeactivateTractorBeam()
    {
        _isActive = false;

        if (_tractorBeamConeObject != null)
        {
            _tractorBeamConeObject.SetActive(false);
            _tractorBeamConeObject.transform.SetParent(transform, false);
            _tractorBeamConeObject.transform.localPosition = Vector3.zero;
            _tractorBeamConeObject.transform.localRotation = Quaternion.identity;
        }

        tractorBeam.suctionParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StopBeamLoopSound();
    }

    private void AlignTractorBeamCone()
    {
        if (_tractorBeamConeObject == null)
        {
            return;
        }

        _tractorBeamConeObject.transform.SetParent(null);
        _tractorBeamConeObject.transform.position = transform.TransformPoint(tractorBeam.coneOffset);
        _tractorBeamConeObject.transform.rotation = transform.rotation;
    }

    private void ApplyTractorBeamPull()
    {
        Vector3 origin = transform.TransformPoint(tractorBeam.coneOffset);
        Vector3 forward = GetPlanarForward();
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            Mathf.Max(0f, tractorBeam.coneRange),
            OverlapResults,
            tractorBeam.targetMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = OverlapResults[i];
            if (hitCollider == null)
            {
                continue;
            }

            Entity3D targetEntity = ResolveTargetEntity(hitCollider);
            if (targetEntity == null || targetEntity == entity)
            {
                continue;
            }

            Rigidbody targetBody = hitCollider.attachedRigidbody != null
                ? hitCollider.attachedRigidbody
                : targetEntity.GetComponent<Rigidbody>();

            if (targetBody == null)
            {
                continue;
            }

            Vector3 toTarget = targetBody.worldCenterOfMass - origin;
            Vector3 planarToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            float planarDistance = planarToTarget.magnitude;
            if (planarDistance <= Mathf.Max(0f, tractorBeam.stopDistance))
            {
                continue;
            }

            if (planarToTarget.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            float angle = Vector3.Angle(forward, planarToTarget.normalized);
            if (angle > tractorBeam.coneHalfAngle)
            {
                continue;
            }

            Vector3 currentVelocity = targetBody.linearVelocity;
            currentVelocity.y = 0f;

            Vector3 pullVelocity = -planarToTarget.normalized * tractorBeam.pullSpeed;
            targetBody.linearVelocity = tractorBeam.freezeTargetMovement
                ? pullVelocity
                : currentVelocity + (pullVelocity * Time.fixedDeltaTime);
        }

        for (int i = 0; i < hitCount; i++)
        {
            OverlapResults[i] = null;
        }
    }

    private Entity3D ResolveTargetEntity(Collider hitCollider)
    {
        Entity3D targetEntity = hitCollider.GetComponent<Entity3D>();
        if (targetEntity != null)
        {
            return targetEntity;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            targetEntity = hitCollider.attachedRigidbody.GetComponent<Entity3D>();
            if (targetEntity != null)
            {
                return targetEntity;
            }
        }

        return hitCollider.GetComponentInParent<Entity3D>();
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return planarForward.normalized;
    }

    private void StartBeamLoopSound()
    {
        if (tractorBeam.beamLoopSound == null || tractorBeamLoopAudioSource == null)
        {
            return;
        }

        if (tractorBeamLoopAudioSource.isPlaying && tractorBeamLoopAudioSource.clip == tractorBeam.beamLoopSound.clip)
        {
            return;
        }

        tractorBeam.beamLoopSound.Play(tractorBeamLoopAudioSource);
    }

    private void StopBeamLoopSound()
    {
        if (tractorBeamLoopAudioSource != null && tractorBeamLoopAudioSource.isPlaying)
        {
            tractorBeamLoopAudioSource.Stop();
        }
    }
}
