using UnityEngine;

/// <summary>
/// Laser sniper enemy that maintains distance and fires beam weapons at the player
/// Only fires when it has clear line-of-sight (no asteroids blocking)
/// </summary>
public class ArtilleryEnemyScript : EnemyScript
{
    [Header("Sniper Behavior Settings")]
    [Tooltip("Minimum distance to keep from player")]
    public float keepAwayDistance = 8f;

    [Tooltip("Optimal sniping range from player")]
    public float optimalRange = 12f;

    [Tooltip("How often to check line-of-sight (seconds) - lower = more responsive but more expensive")]
    public float beamCheckInterval = 0.2f;

    [Tooltip("Distance buffer for range management")]
    public float rangeBuffer = 2f;

    [Tooltip("Distance to spawn beam forward from turret/ship (prevents self-collision)")]
    public float turretOffset = 1f;

    [Tooltip("Rotation speed multiplier when beam is active (0.3 = 70% slower)")]
    public float beamRotationMultiplier = 0.3f;

    [Tooltip("Maximum range at which enemy will engage (should be less than beam max distance for balance)")]
    public float maxEngagementRange = 20f;

    private LaserBeam _activeBeam;
    private float _lastBeamCheckTime;
    private bool _hasLineOfSight;
    private bool _isMovingBackwards = false;
    private System.Collections.Generic.Dictionary<ParticleSystem, Quaternion> _thrusterOriginalRotations = new();
    private System.Collections.Generic.Dictionary<ParticleSystem, Vector3> _thrusterOriginalPositions = new();

    protected override void Start()
    {
        base.Start();
        _lastBeamCheckTime = Time.time;

        // Cache original thruster rotations and positions
        foreach (var thruster in thrusters)
        {
            if (thruster != null)
            {
                _thrusterOriginalRotations[thruster] = thruster.transform.localRotation;
                _thrusterOriginalPositions[thruster] = thruster.transform.localPosition;
            }
        }
    }

    protected override void Update()
    {
        base.Update();

        if (_target == null) return;

        // Rotate thrusters 180 degrees when moving backwards
        UpdateThrusterRotation();

        // Periodically check line-of-sight and manage beam
        if (Time.time >= _lastBeamCheckTime + beamCheckInterval)
        {
            _lastBeamCheckTime = Time.time;
            UpdateBeamFiring();
        }
    }

    private void UpdateThrusterRotation()
    {
        // Flip thruster emission direction and position when moving backwards
        foreach (var thruster in thrusters)
        {
            if (thruster == null || !_thrusterOriginalRotations.ContainsKey(thruster) || !_thrusterOriginalPositions.ContainsKey(thruster))
                continue;

            Quaternion originalRotation = _thrusterOriginalRotations[thruster];
            Vector3 originalPosition = _thrusterOriginalPositions[thruster];

            Quaternion targetRotation;
            Vector3 targetPosition;

            if (_isMovingBackwards)
            {
                // Apply 180 degree rotation around X axis to flip emission direction
                targetRotation = originalRotation * Quaternion.Euler(180f, 0, 0);

                // Adjust Y position: change from -1 to 3.5 (offset of 4.5)
                targetPosition = originalPosition;
                targetPosition.z = originalPosition.z + 6f; // -1 + 5 = 3.5, or -1.5 + 5 = 3.5
            }
            else
            {
                // Use original rotation and position
                targetRotation = originalRotation;
                targetPosition = originalPosition;
            }

            // Smoothly interpolate to target rotation
            thruster.transform.localRotation = Quaternion.RotateTowards(
                thruster.transform.localRotation,
                targetRotation,
                360f * Time.deltaTime
            );

            // Smoothly interpolate to target position
            thruster.transform.localPosition = Vector3.MoveTowards(
                thruster.transform.localPosition,
                targetPosition,
                10f * Time.deltaTime // 10 units per second
            );
        }
    }

    protected override void MovePursuit()
    {
        if (_target == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        Vector2 directionToTarget = (_target.position - transform.position).normalized;

        // Kiting behavior: maintain optimal range
        if (distanceToPlayer < keepAwayDistance)
        {
            // Too close - move away from player
            _rb.AddForce(-directionToTarget * thrustForce);
            _isMovingBackwards = true;
            _isThrusting = true;
        }
        else if (distanceToPlayer > optimalRange + rangeBuffer)
        {
            // Too far - move closer
            _rb.AddForce(directionToTarget * thrustForce);
            _isMovingBackwards = false;
            _isThrusting = true;
        }
        else
        {
            // In optimal range, don't thrust (just drift/maintain position)
            _isMovingBackwards = false;
            _isThrusting = false;
        }
    }

    protected override void RotateTowardTarget()
    {
        Vector2 direction;

        if (_currentState == EnemyState.Patrolling)
        {
            direction = _wanderDirection;
        }
        else if (_currentState == EnemyState.Pursuing)
        {
            direction = _target.position - transform.position;
        }
        else if (_currentState == EnemyState.Searching)
        {
            direction = _lastKnownPlayerPosition - transform.position;
        }
        else
        {
            return;
        }

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;

        // Apply rotation speed penalty when beam is active
        float effectiveRotationSpeed = rotationSpeed;
        if (_activeBeam != null)
        {
            effectiveRotationSpeed *= beamRotationMultiplier;
        }

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, effectiveRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    protected override void TryFire()
    {
        // Laser snipers don't use projectile weapons
        // They only fire beam weapons (handled in UpdateBeamFiring)
    }

    private void UpdateBeamFiring()
    {
        if (_target == null) return;

        // Only fire when pursuing or searching
        if (_currentState != EnemyState.Pursuing && _currentState != EnemyState.Searching)
        {
            StopBeam();
            return;
        }

        // Check distance to player - don't fire if beyond max engagement range
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        if (distanceToPlayer > maxEngagementRange)
        {
            StopBeam();
            return;
        }

        // Check if we have line-of-sight and are aimed at target
        _hasLineOfSight = HasLineOfSight();
        bool isAimed = IsAimedAtTarget();

        if (_hasLineOfSight && isAimed && beamWeapon.prefab != null)
        {
            // Start beam if not already active
            if (_activeBeam == null)
            {
                StartBeam();
            }
        }
        else
        {
            // Stop beam if we lost line-of-sight or lost aim
            StopBeam();
        }
    }

    private bool HasLineOfSight()
    {
        if (_target == null) return false;

        Vector2 startPosition = transform.position;
        Vector2 directionToTarget = (_target.position - transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, _target.position);

        // Raycast to find all objects between enemy and player
        RaycastHit2D[] hits = Physics2D.RaycastAll(startPosition, directionToTarget, distanceToTarget);

        // Check what we hit first
        foreach (RaycastHit2D hit in hits)
        {
            // Skip self
            if (hit.collider.gameObject == gameObject)
                continue;

            // If we hit an asteroid before reaching the player, no line-of-sight
            if (hit.collider.CompareTag("Asteroid"))
            {
                return false;
            }

            // If we hit the player, we have line-of-sight
            if (hit.collider.CompareTag("Player"))
            {
                return true;
            }
        }

        // No obstacles found, but also didn't hit player (might be out of range)
        return false;
    }

    private void StartBeam()
    {
        if (_activeBeam != null) return; // Already firing

        // Spawn beam from turret if available, otherwise from front of ship
        Vector3 spawnPosition;
        if (turrets != null && turrets.Count > 0)
        {
            // Use first turret position with forward offset to prevent self-collision
            spawnPosition = turrets[0].transform.position + transform.up * turretOffset;
        }
        else
        {
            // No turrets - spawn from front of ship with offset
            spawnPosition = transform.position + transform.up * turretOffset;
        }

        GameObject beamObj = Instantiate(beamWeapon.prefab, spawnPosition, transform.rotation, transform);
        _activeBeam = beamObj.GetComponent<LaserBeam>();

        if (_activeBeam != null)
        {
            _activeBeam.Initialize(
                "Player",
                beamWeapon.damagePerSecond,
                beamWeapon.maxDistance,
                beamWeapon.recoilForcePerSecond,
                beamWeapon.impactForce,
                this
            );
            _activeBeam.StartFiring();

            // Start looping beam audio
            StartBeamAudio();
        }
    }

    private void StopBeam()
    {
        if (_activeBeam != null)
        {
            _activeBeam.StopFiring();
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;

            // Stop looping beam audio
            StopBeamAudio();
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        // Apply recoil from active beam
        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            ApplyRecoil(recoilForceThisFrame);
        }
    }

    void OnDestroy()
    {
        // Clean up beam when enemy is destroyed
        StopBeam();
    }

    // Visualize ranges in editor
    void OnDrawGizmosSelected()
    {
        // Keep away distance (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, keepAwayDistance);

        // Optimal range (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalRange);

        // Optimal range + buffer (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, optimalRange + rangeBuffer);

        // Max engagement range (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxEngagementRange);
    }
}
