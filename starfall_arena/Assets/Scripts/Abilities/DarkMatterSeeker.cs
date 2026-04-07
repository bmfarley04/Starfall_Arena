using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class DarkMatterSeeker : MonoBehaviour
{
    [Tooltip("Tag of targets to home toward")]
    public string targetTag;

    [Tooltip("Movement speed while seeking")]
    public float moveSpeed = 5f;

    [Tooltip("Seconds between retarget checks")]
    public float retargetInterval = 0.15f;

    private Rigidbody2D _rb;
    private Transform _currentTarget;
    private float _nextSearchTime;
    private Vector2 _lastDirection = Vector2.up;
    private const float ROTATION_OFFSET = -90f;

    private float _initialSpeed;
    private float _lifetime = 1f;
    private float _spawnTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (moveSpeed <= 0f && _rb != null)
        {
            moveSpeed = _rb.linearVelocity.magnitude;
        }
        _initialSpeed = moveSpeed;
        _spawnTime = Time.time;
    }

    public void Initialize(string targetTag, float speed, float lifetime, float retargetInterval = 0.15f)
    {
        this.targetTag = targetTag;
        if (speed > 0f)
        {
            moveSpeed = speed;
        }
        _initialSpeed = moveSpeed;
        _lifetime = Mathf.Max(0.01f, lifetime);
        this.retargetInterval = Mathf.Max(0.02f, retargetInterval);
        _spawnTime = Time.time;
        FindNewTarget();
    }

    private void Update()
    {
        if (Time.time >= _nextSearchTime)
        {
            FindNewTarget();
        }
    }

    private void FixedUpdate()
    {
        Vector2 direction;

        if (_currentTarget != null && _currentTarget.gameObject.activeInHierarchy)
        {
            direction = ((Vector2)_currentTarget.position - _rb.position);
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                _lastDirection = direction;
            }
        }
        else
        {
            direction = _lastDirection;
        }

        float t = Mathf.Clamp01((Time.time - _spawnTime) / _lifetime);
        float currentSpeed = Mathf.Lerp(_initialSpeed, 0f, t);

        if (direction.sqrMagnitude > 0.0001f)
        {
            _rb.linearVelocity = direction * currentSpeed;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + ROTATION_OFFSET;
            _rb.MoveRotation(angle);
        }
    }

    private void FindNewTarget()
    {
        _nextSearchTime = Time.time + retargetInterval;
        if (string.IsNullOrEmpty(targetTag))
        {
            _currentTarget = null;
            return;
        }

        GameObject[] objs;
        try
        {
            objs = GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            _currentTarget = null;
            return;
        }

        float nearestSqr = float.MaxValue;
        Transform nearest = null;
        Vector3 origin = transform.position;

        foreach (GameObject obj in objs)
        {
            if (obj == null || !obj.activeInHierarchy) continue;
            float sqr = (obj.transform.position - origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = obj.transform;
            }
        }

        _currentTarget = nearest;
    }
}
