using System.Collections.Generic;
using UnityEngine;

public class BoundaryGuardianScript : EnemyScript
{
    public enum GuardianSide { Left, Top, Right, Bottom }
    [Header("Boundary Guardian Settings")]

    [Tooltip("Which side of the arena this guardian patrols")]
    public GuardianSide guardianSide = GuardianSide.Top;

    [Tooltip("Barrier gameobject for position reference")]
    public GameObject barrier;

    [Tooltip("Camera to calculate viewport bounds")]
    public Camera gameCamera;

    [Tooltip("Distance to maintain from barrier (units outside camera view)")]
    public float preferredDistanceFromBarrier = 2f;

    [Tooltip("Distance to spawn beam forward from ship center")]
    public float beamOffset = 1f;

    [Header("Debug")]
    [Tooltip("DEBUG: Toggle to test firing behavior in editor")]
    public bool debugIsFiring = false;

    [Tooltip("DEBUG: Beam rotation offset - adjust if beam doesn't point at player (try 0, -90, 90, 180)")]
    public float beamAngleOffset = -90f;

    private bool _isFiring = false;
    public bool IsFiring
    {
        get => _isFiring || debugIsFiring;
        set => _isFiring = value;
    }

    private LaserBeam _activeBeam;

    protected override void Update()
    {
        base.Update();
        if (_target == null) return;

        UpdateBeamFiring();
        UpdateBeamTracking();
    }

    private void UpdateBeamTracking()
    {
        if (_activeBeam == null || _target == null) return;

        // Continuously update beam rotation to track player
        Vector2 directionToPlayer = (_target.position - _activeBeam.transform.position).normalized;
        float angle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;

        // Set beam rotation to face player (adjust beamAngleOffset in Inspector if needed)
        _activeBeam.transform.rotation = Quaternion.Euler(0, 0, angle + beamAngleOffset);
    }

    private void StartBeam()
    {
        if (_activeBeam != null) return; // Already firing

        // Spawn beam at ship center (will be rotated by UpdateBeamTracking)
        Vector3 spawnPosition = transform.position;

        // Initial rotation (will be updated every frame by UpdateBeamTracking)
        Quaternion beamRotation = Quaternion.identity;

        GameObject beamObj = Instantiate(beamWeapon.prefab, spawnPosition, beamRotation, transform);
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

            // Play beam fire sound
            PlayOneShotSound(beamFireSound, 1f, AudioClipType.BeamFire);
        }
    }

    // --- Standard EnemyScript Logic Below (Movement, State, Cleanup) ---

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

    protected override void MovePursuit()
    {
        if (_target == null) return;
        Vector3 targetPosition = CalculateTargetPosition();
        Vector2 directionToTarget = (targetPosition - transform.position);
        float distanceToTarget = directionToTarget.magnitude;

        if (distanceToTarget > 0.1f)
        {
            directionToTarget.Normalize();

            // Proportional thrust based on distance
            float thrustMultiplier = Mathf.Clamp01(distanceToTarget / 5f);
            thrustMultiplier = Mathf.Max(thrustMultiplier, 0.2f);

            // Add velocity damping when close to target to prevent oscillation
            // Check if we're moving away from target (velocity dot product with direction)
            float velocityTowardsTarget = Vector2.Dot(_rb.linearVelocity, directionToTarget);

            // If we're close and moving fast towards target, apply counter-force (damping)
            if (distanceToTarget < 2f && velocityTowardsTarget > 0)
            {
                // Reduce velocity as we get closer
                float dampingFactor = 1f - (distanceToTarget / 2f); // 0 to 1 as we get closer
                _rb.linearVelocity *= (1f - dampingFactor * 0.5f * Time.fixedDeltaTime * 60f); // Dampen velocity
            }

            _rb.AddForce(directionToTarget * thrustForce * thrustMultiplier);
            _isThrusting = true;
        }
        else
        {
            // At target - apply strong damping to kill velocity
            _rb.linearVelocity *= 0.9f;
            _isThrusting = false;
        }
    }

    protected override void UpdateEnemyState()
    {
        _currentState = EnemyState.Pursuing;
    }

    private Vector3 CalculateTargetPosition()
    {
        if (gameCamera == null || barrier == null) return transform.position;

        float cameraHeight = gameCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * gameCamera.aspect;

        Vector3 targetPosition = Vector3.zero;
        targetPosition.z = barrier.transform.position.z; 

        if (IsFiring && _target != null)
        {
            targetPosition.x = _target.position.x;
            targetPosition.y = _target.position.y;
        }
        else
        {
            switch (guardianSide)
            {
                case GuardianSide.Left:
                    targetPosition.x = barrier.transform.position.x - (cameraWidth / 2f + preferredDistanceFromBarrier);
                    targetPosition.y = _target != null ? _target.position.y : barrier.transform.position.y;
                    break;
                case GuardianSide.Top:
                    targetPosition.x = _target != null ? _target.position.x : barrier.transform.position.x;
                    targetPosition.y = barrier.transform.position.y + (cameraHeight / 2f + preferredDistanceFromBarrier);
                    break;
                case GuardianSide.Right:
                    targetPosition.x = barrier.transform.position.x + (cameraWidth / 2f + preferredDistanceFromBarrier);
                    targetPosition.y = _target != null ? _target.position.y : barrier.transform.position.y;
                    break;
                case GuardianSide.Bottom:
                    targetPosition.x = _target != null ? _target.position.x : barrier.transform.position.x;
                    targetPosition.y = barrier.transform.position.y - (cameraHeight / 2f + preferredDistanceFromBarrier);
                    break;
            }
        }
        return targetPosition;
    }

    private void UpdateBeamFiring()
    {
        if (_target == null) return;

        if (!IsFiring)
        {
            StopBeam();
            return;
        }

        if (_activeBeam == null && beamWeapon.prefab != null)
        {
            StartBeam();
        }
    }

    private void StopBeam()
    {
        if (_activeBeam != null)
        {
            _activeBeam.StopFiring();
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;
        }
    }

    void OnDestroy()
    {
        StopBeam();
    }

    void OnDrawGizmosSelected()
    {
        if (barrier != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, barrier.transform.position);
        }
    }
}