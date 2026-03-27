using UnityEngine;

public class EnemyAIFlightController3D : MonoBehaviour, IShipFlightInputSource
{
    [Header("3D Enemy AI")]
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ProjectileWeapon3D primaryWeapon;
    [SerializeField] private Entity3D entity;
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private float detectionRange = 150f;
    [SerializeField] private float preferredDistance = 60f;
    [SerializeField] private float aimTolerance = 8f;
    [SerializeField] private float repathInterval = 0.2f;

    private Vector2 _lookInput;
    private float _thrustInput;
    private float _nextThinkTime;

    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;

    private void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();
        entity ??= GetComponent<Entity3D>();

        if (shipFlight != null)
        {
            shipFlight.SetInputSource(this);
        }
    }

    private void Update()
    {
        if (Time.time < _nextThinkTime)
        {
            return;
        }

        _nextThinkTime = Time.time + repathInterval;
        ResolveTarget();
        Think();
    }

    public bool ConsumeToggleFrictionPressed()
    {
        return false;
    }

    private void ResolveTarget()
    {
        if (target != null)
        {
            return;
        }

        GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
        if (targetObject != null)
        {
            target = targetObject.transform;
        }
    }

    private void Think()
    {
        if (target == null)
        {
            _lookInput = Vector2.zero;
            _thrustInput = 0f;
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;

        if (distance > detectionRange)
        {
            _lookInput = Vector2.zero;
            _thrustInput = 0f;
            return;
        }

        Vector3 localTarget = transform.InverseTransformDirection(toTarget.normalized);
        _lookInput = new Vector2(
            Mathf.Clamp(localTarget.x, -1f, 1f),
            Mathf.Clamp(localTarget.y, -1f, 1f)
        );

        _thrustInput = distance > preferredDistance ? 1f : 0f;

        if (primaryWeapon != null && (entity == null || !entity.IsPrimaryFireDisabledByAbility()) && IsAimedAtTarget(toTarget))
        {
            primaryWeapon.TryFire();
        }
    }

    private bool IsAimedAtTarget(Vector3 toTarget)
    {
        float angle = Vector3.Angle(transform.forward, toTarget);
        return angle <= aimTolerance;
    }
}
