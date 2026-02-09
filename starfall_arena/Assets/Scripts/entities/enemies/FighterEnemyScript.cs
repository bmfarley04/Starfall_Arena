using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct FighterDifficultyLevel
{
    public string levelName;

    [Tooltip("Use flanking/positioning AI instead of charging directly at the player")]
    public bool useAdvancedMovement;

    [Tooltip("Predict player movement when aiming projectiles")]
    public bool useLeadShots;

    [Tooltip("Allow beam weapon usage at range")]
    public bool useBeamWeapon;

    [Tooltip("Detect and dodge incoming projectiles and beams")]
    public bool useDodging;
}

public enum FighterState
{
    Flanking,      // Circling to stay out of player's forward arc
    Disengaging,   // Breaking away to reposition from distance
    CoastAndTurn,  // Coasting on momentum, turning to face pursuer
    Attacking      // Good position achieved, focusing on offense
}

/// <summary>
/// Ace pilot enemy that mirrors player tactics: avoids forward arc, performs
/// coast-and-turn maneuvers, uses dual weapons, and dodges incoming fire.
/// </summary>
public class FighterEnemyScript : Enemy
{
    [Header("Fighter Positioning")]
    [Tooltip("Angle in degrees defining player's forward arc to avoid")]
    public float forwardArcAngle = 60f;

    [Tooltip("Distance to maintain while flanking/circling")]
    public float flankingDistance = 5f; // Closer engagement

    [Tooltip("Distance to reach when disengaging")]
    public float disengageDistance = 10f; // Shorter retreat distance

    [Tooltip("Maximum distance before fighter gives up pursuit and re-engages")]
    public float maxReEngageDistance = 30f;

    [Tooltip("How long to stay in attacking state before repositioning")]
    public float maxAttackDuration = 3f; // Stay aggressive longer

    [Header("Coast-and-Turn")]
    [Tooltip("Angle threshold to consider 'facing' target after turn")]
    public float coastTurnAimThreshold = 15f;

    [Tooltip("Minimum time in coast-and-turn state")]
    public float minCoastTurnDuration = 0.5f;

    [Tooltip("Maximum time before forcing exit from coast-and-turn")]
    public float maxCoastTurnDuration = 2f;

    [Header("Weapon Selection")]
    [Tooltip("Range at which projectiles are preferred")]
    public float projectilePreferredRange = 6f;

    [Tooltip("Range at which beam is preferred")]
    public float beamPreferredRange = 12f;

    [Tooltip("Rotation speed multiplier when beam is active")]
    public float beamRotationMultiplier = 0.3f;

    [Tooltip("Offset distance for beam spawn to prevent self-collision")]
    public float beamOffsetDistance = 1f;

    [Tooltip("Bias toward selecting beam at mid-range (0-1)")]
    [Range(0f, 1f)]
    public float beamSelectionBias = 0.4f;

    [Header("Beam Weapon")]
    public BeamWeaponConfig beamWeapon;

    [Header("Burst Fire Settings")]
    [Tooltip("Number of shots to fire in each burst")]
    public int burstShotsPerVolley = 3;

    [Tooltip("Time between shots within a burst (seconds)")]
    public float burstCooldown = 0.15f;

    [Tooltip("Time between volleys (seconds)")]
    public float volleyCooldown = 1f;

    [Header("Evasion")]
    [Tooltip("Radius to scan for incoming threats")]
    public float threatDetectionRadius = 5f;

    [Tooltip("How often to check for threats (seconds)")]
    public float threatCheckInterval = 0.15f;

    [Tooltip("Impulse force applied when dodging")]
    public float dodgeImpulseForce = 8f;

    [Tooltip("Minimum time between dodges")]
    public float dodgeCooldown = 0.5f;

    [Tooltip("Probability of dodging when threat detected (0-1)")]
    [Range(0f, 1f)]
    public float dodgeChance = 0.7f;

    [Header("Pursuit Detection")]
    [Tooltip("Player velocity threshold to consider them 'pursuing' us")]
    public float pursuitVelocityThreshold = 5f;

    [Header("Difficulty")]
    public List<FighterDifficultyLevel> difficultyLevels = new List<FighterDifficultyLevel>();
    public int currentDifficultyLevel = 0;

    private FighterDifficultyLevel CurrentLevel =>
        difficultyLevels != null && currentDifficultyLevel >= 0 && currentDifficultyLevel < difficultyLevels.Count
            ? difficultyLevels[currentDifficultyLevel]
            : new FighterDifficultyLevel { useAdvancedMovement = true, useLeadShots = true, useBeamWeapon = true, useDodging = true };

    // Fighter state
    private FighterState _fighterState = FighterState.Flanking;
    private float _stateEnterTime;
    private int _strafeDirection = 1; // 1 or -1, picked at state entry

    // Weapon state
    private LaserBeam _activeBeam;
    private float _lastProjectileFireTime;
    private int _burstShotsFired;

    // Evasion state
    private float _lastThreatCheckTime;
    private float _lastDodgeTime;

    // Damage tracking for coast-and-turn trigger
    private float _lastDamageTime;
    private float _coastTurnTriggerWindow = 0.5f;

    // Player reference for beam detection
    private Player _Player;

    // Movement tracking (needed for FixedUpdate override)
    private Vector2 _previousVelocity;
    private Vector2 _acceleration;

    protected override void Start()
    {
        base.Start();
        _stateEnterTime = Time.time;
        _strafeDirection = Random.value > 0.5f ? 1 : -1;

        // Apply lead targeting from current difficulty level
        useLeadTargeting = CurrentLevel.useLeadShots;

        // Cache player script for beam detection
        if (_target != null)
        {
            _Player = _target.GetComponent<Player>();
        }
    }

    public void SetDifficultyLevel(int level)
    {
        currentDifficultyLevel = Mathf.Clamp(level, 0, difficultyLevels.Count - 1);
        useLeadTargeting = CurrentLevel.useLeadShots;

        // Stop beam if beam weapon is now disabled
        if (!CurrentLevel.useBeamWeapon)
        {
            StopBeam();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (_target == null) return;

        // Check for threats (projectiles and beams) only if dodging is enabled
        if (CurrentLevel.useDodging)
        {
            CheckForThreats();
        }

        // Check re-engage distance in all states
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        if (distanceToPlayer > maxReEngageDistance)
        {
            // Too far - stop beam and reset to pursue/flank
            StopBeam();
            if (_currentState != EnemyState.Pursuing)
            {
                _currentState = EnemyState.Pursuing;
            }
            if (_fighterState != FighterState.Flanking)
            {
                EnterState(FighterState.Flanking);
            }
        }

        // Update fighter-specific state machine when pursuing (only with advanced movement)
        if (_currentState == EnemyState.Pursuing && CurrentLevel.useAdvancedMovement)
        {
            UpdateFighterState();
        }
    }

    protected override void FixedUpdate()
    {
        // Call ShipBase's FixedUpdate for acceleration tracking, but NOT EnemyScript's
        // This prevents the default enemy movement behavior
        if (_rb == null) return;

        Vector2 currentVelocity = _rb.linearVelocity;
        _acceleration = (currentVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _previousVelocity = currentVelocity;

        if (_target == null) return;

        // Player-style movement: only thrust forward in the direction we're facing
        // Movement states determine WHEN to thrust, not the direction
        bool shouldThrust = DetermineIfShouldThrust();

        if (shouldThrust)
        {
            _isThrusting = true;
            Vector2 thrustDirection = transform.up; // Always thrust forward
            _rb.AddForce(thrustDirection * movement.thrustForce);
            ApplyLateralDamping();
        }
        else
        {
            _isThrusting = false;
        }

        ClampVelocity();

        // Apply continuous recoil while beam is active
        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            ApplyRecoil(recoilForceThisFrame);
        }
    }

    private bool DetermineIfShouldThrust()
    {
        // Determine if we should apply thrust based on current state
        switch (_currentState)
        {
            case EnemyState.Patrolling:
                return true; // Patrol wandering

            case EnemyState.Pursuing:
                return DetermineIfShouldThrustInPursuit();

            case EnemyState.Searching:
                return true; // Move to last known position

            default:
                return false;
        }
    }

    private bool DetermineIfShouldThrustInPursuit()
    {
        // Without advanced movement, always thrust toward player
        if (!CurrentLevel.useAdvancedMovement)
        {
            return true;
        }

        // Fighter sub-states determine thrust
        switch (_fighterState)
        {
            case FighterState.Flanking:
                // Thrust when trying to get into position
                return ShouldThrustWhileFlanking();

            case FighterState.Disengaging:
                return true; // Always thrust when retreating

            case FighterState.CoastAndTurn:
                return false; // Coast on momentum

            case FighterState.Attacking:
                return ShouldThrustWhileAttacking();

            default:
                return false;
        }
    }

    private bool ShouldThrustWhileFlanking()
    {
        // Aggressively thrust to get behind the player
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);

        // Calculate optimal behind position
        Vector2 optimalBehindPosition = (Vector2)_target.position - (Vector2)_target.up * flankingDistance;
        Vector2 toOptimalPosition = optimalBehindPosition - (Vector2)transform.position;

        Vector2 ourForward = transform.up;
        float dotToOptimal = Vector2.Dot(ourForward, toOptimalPosition.normalized);

        // Check if we're behind the player
        Vector2 playerForward = _target.up;
        Vector2 playerToUs = ((Vector2)transform.position - (Vector2)_target.position).normalized;
        float angleFromPlayerForward = Vector2.Angle(playerForward, playerToUs);

        // Thrust aggressively when:
        // 1. Too far from flanking distance
        if (distanceToPlayer > flankingDistance * 1.2f && dotToOptimal > 0.3f) return true;

        // 2. Too close to flanking distance
        if (distanceToPlayer < flankingDistance * 0.8f && dotToOptimal < -0.3f) return true;

        // 3. NOT behind the player (less than 120 degrees) and facing roughly toward optimal position
        if (angleFromPlayerForward < 120f && dotToOptimal > 0.4f) return true;

        // 4. Well behind the player and facing toward them to shoot
        if (angleFromPlayerForward >= 120f && dotToOptimal > 0.5f) return true;

        return false;
    }

    private bool ShouldThrustWhileAttacking()
    {
        // Manage range - thrust if too far or too close
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        float optimalRange = (projectilePreferredRange + beamPreferredRange) / 2f;

        Vector2 toPlayer = ((Vector2)_target.position - (Vector2)transform.position).normalized;
        Vector2 ourForward = transform.up;
        float dotProduct = Vector2.Dot(ourForward, toPlayer);

        // Thrust if too far and facing toward player
        if (distanceToPlayer > optimalRange * 1.3f && dotProduct > 0.3f)
            return true;

        // Thrust if too close and facing away from player
        if (distanceToPlayer < projectilePreferredRange * 0.7f && dotProduct < -0.3f)
            return true;

        return false;
    }

    #region State Machine

    private void UpdateFighterState()
    {
        float timeInState = Time.time - _stateEnterTime;
        bool recentlyDamaged = Time.time - _lastDamageTime < _coastTurnTriggerWindow;
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);

        // Check if too far from player - force re-engagement
        if (distanceToPlayer > maxReEngageDistance)
        {
            // Reset to Flanking to re-engage
            if (_fighterState != FighterState.Flanking)
            {
                EnterState(FighterState.Flanking);
            }
            return;
        }

        switch (_fighterState)
        {
            case FighterState.Flanking:
                // Transition to CoastAndTurn if damaged or player pursuing
                if (recentlyDamaged || IsPlayerPursuing())
                {
                    EnterState(FighterState.CoastAndTurn);
                }
                // Transition to Disengaging if stuck in player's forward arc for too long
                else if (IsInPlayerForwardArc() && timeInState > 2f && Random.value < 0.02f) // Only after 2s
                {
                    EnterState(FighterState.Disengaging);
                }
                // Transition to Attacking only if we're WELL behind the player
                else if (!IsInPlayerForwardArc() && IsBehindPlayer())
                {
                    EnterState(FighterState.Attacking);
                }
                break;

            case FighterState.Disengaging:
                // Transition to CoastAndTurn if damaged
                if (recentlyDamaged)
                {
                    EnterState(FighterState.CoastAndTurn);
                }
                // Transition to Flanking once safe distance reached
                else if (distanceToPlayer >= disengageDistance)
                {
                    EnterState(FighterState.Flanking);
                }
                break;

            case FighterState.CoastAndTurn:
                // Check if facing target
                bool facingTarget = IsAimedAtTarget() ||
                    Mathf.Abs(GetAngleToTarget()) < coastTurnAimThreshold;

                // Exit conditions
                if (timeInState >= maxCoastTurnDuration)
                {
                    EnterState(FighterState.Flanking);
                }
                else if (facingTarget && timeInState >= minCoastTurnDuration)
                {
                    // Brief attack window after completing turn
                    EnterState(FighterState.Attacking);
                }
                break;

            case FighterState.Attacking:
                // Transition to CoastAndTurn if damaged
                if (recentlyDamaged)
                {
                    EnterState(FighterState.CoastAndTurn);
                }
                // Transition to Flanking if back in danger zone, lost behind position, or time expired
                else if (IsInPlayerForwardArc() || !IsBehindPlayer() || timeInState >= maxAttackDuration)
                {
                    EnterState(FighterState.Flanking);
                }
                break;
        }
    }

    private void EnterState(FighterState newState)
    {
        _fighterState = newState;
        _stateEnterTime = Time.time;

        // State entry logic
        switch (newState)
        {
            case FighterState.Flanking:
                // Pick new strafe direction
                _strafeDirection = Random.value > 0.5f ? 1 : -1;
                break;

            case FighterState.Disengaging:
                // Pick side to disengage toward
                _strafeDirection = Random.value > 0.5f ? 1 : -1;
                break;

            case FighterState.CoastAndTurn:
                // Stop beam if active when entering coast-and-turn
                StopBeam();
                break;

            case FighterState.Attacking:
                // Ready to attack
                break;
        }
    }

    protected override void UpdateEnemyState()
    {
        // Use base state machine for Patrolling/Pursuing/Searching transitions
        EnemyState previousState = _currentState;
        base.UpdateEnemyState();

        // Reset fighter state when entering Pursuing
        if (_currentState == EnemyState.Pursuing && previousState != EnemyState.Pursuing)
        {
            EnterState(FighterState.Flanking);
        }

        // Stop beam when leaving Pursuing
        if (_currentState != EnemyState.Pursuing && previousState == EnemyState.Pursuing)
        {
            StopBeam();
        }
    }

    #endregion


    #region Rotation

    protected override void RotateTowardTarget()
    {
        if (_target == null) return;

        Vector2 targetDirection;

        // Determine rotation target based on state
        switch (_currentState)
        {
            case EnemyState.Patrolling:
                targetDirection = _wanderDirection;
                break;

            case EnemyState.Pursuing:
                targetDirection = GetDesiredFacingDirection();
                break;

            case EnemyState.Searching:
                targetDirection = _lastKnownPlayerPosition - transform.position;
                break;

            default:
                return;
        }

        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;

        // Apply rotation speed penalty when beam is active
        float effectiveRotationSpeed = movement.rotationSpeed;
        if (_activeBeam != null)
        {
            effectiveRotationSpeed *= beamRotationMultiplier;
        }

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, effectiveRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    private Vector2 GetDesiredFacingDirection()
    {
        // Determine where the fighter should be facing based on sub-state
        Vector2 toPlayer = (Vector2)_target.position - (Vector2)transform.position;

        // Without advanced movement, always face the player directly
        if (!CurrentLevel.useAdvancedMovement)
        {
            return toPlayer.normalized;
        }

        switch (_fighterState)
        {
            case FighterState.Flanking:
                return GetFlankingFacingDirection(toPlayer);

            case FighterState.Disengaging:
                // Face away from player when retreating
                return -toPlayer.normalized;

            case FighterState.CoastAndTurn:
                // Always face toward player during coast-and-turn
                return toPlayer.normalized;

            case FighterState.Attacking:
                // Face toward player to shoot
                return toPlayer.normalized;

            default:
                return toPlayer.normalized;
        }
    }

    private Vector2 GetFlankingFacingDirection(Vector2 toPlayer)
    {
        // When flanking, aggressively try to get behind the player
        Vector2 playerForward = _target.up;
        Vector2 playerToUs = -toPlayer.normalized;
        float angleFromPlayerForward = Vector2.Angle(playerForward, playerToUs);

        // Calculate the optimal position behind the player
        Vector2 optimalBehindPosition = (Vector2)_target.position - (Vector2)_target.up * flankingDistance;
        Vector2 toOptimalPosition = optimalBehindPosition - (Vector2)transform.position;

        if (angleFromPlayerForward < 120f) // Increased from 90f to be more aggressive
        {
            // We're NOT well behind the player - aggressively move toward behind position
            // Use optimal position instead of just circling
            if (angleFromPlayerForward < 45f)
            {
                // Very far from behind - move aggressively toward optimal position
                Vector2 perpendicular = Vector2.Perpendicular(toPlayer.normalized) * _strafeDirection;
                return (toOptimalPosition.normalized * 0.7f + perpendicular * 0.3f).normalized;
            }
            else
            {
                // Getting closer to behind - blend circling with optimal positioning
                Vector2 perpendicular = Vector2.Perpendicular(toPlayer.normalized) * _strafeDirection;
                return (toOptimalPosition.normalized * 0.5f + perpendicular * 0.5f).normalized;
            }
        }
        else
        {
            // We're well behind player - face toward them to maintain position and shoot
            return toPlayer.normalized;
        }
    }

    private float GetAngleToTarget()
    {
        if (_target == null) return 180f;

        Vector2 direction = _target.position - transform.position;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;

        return Mathf.DeltaAngle(currentAngle, targetAngle + ROTATION_OFFSET);
    }

    #endregion

    #region Combat

    protected override void TryFire()
    {
        if (_target == null) return;

        // With advanced movement: skip firing when actively disengaging
        // Without advanced movement: no disengaging state, so always allow firing
        if (CurrentLevel.useAdvancedMovement && _fighterState == FighterState.Disengaging)
        {
            return;
        }

        // With advanced movement: relaxed aim (spray and pray while dodging)
        // Without advanced movement: normal aim tolerance (charging directly at player)
        float angleToTarget = Mathf.Abs(GetAngleToTarget());
        float effectiveAimTolerance = CurrentLevel.useAdvancedMovement ? aimTolerance * 2f : aimTolerance;
        if (angleToTarget > effectiveAimTolerance) return;

        // Weapon selection based on range (beam gated by difficulty)
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        bool useBeam = SelectWeapon(distanceToPlayer);

        if (useBeam && beamWeapon.prefab != null)
        {
            // Stop beam if too far away (beyond max beam range)
            if (_activeBeam != null && distanceToPlayer > beamWeapon.maxDistance * 1.1f)
            {
                StopBeam();
            }
            // Check line of sight for beam
            else if (HasLineOfSight())
            {
                FireBeam();
            }
        }
        else
        {
            // If switching to projectiles, stop beam
            if (_activeBeam != null)
            {
                StopBeam();
            }

            if (projectileWeapon.prefab != null)
            {
                FireProjectile();
            }
        }
    }

    private bool SelectWeapon(float distance)
    {
        // Beam weapon disabled at this difficulty level
        if (!CurrentLevel.useBeamWeapon) return false;

        // If beam is already active, keep using it
        if (_activeBeam != null) return true;

        // Distance-based preference with randomness
        if (distance > beamPreferredRange)
        {
            return true; // Long range = beam
        }
        else if (distance < projectilePreferredRange)
        {
            return false; // Close range = projectiles
        }
        else
        {
            // Mid-range: semi-random with bias
            return Random.value < beamSelectionBias;
        }
    }

    private void FireProjectile()
    {
        // Configurable burst firing system
        // burstShotsPerVolley: number of shots per burst
        // burstCooldown: time between shots within a burst
        // volleyCooldown: time between volleys

        // Check if we're in a burst or need to start a new volley
        if (_burstShotsFired >= burstShotsPerVolley)
        {
            // Volley complete, wait for next volley
            if (Time.time < _lastProjectileFireTime + volleyCooldown) return;
            _burstShotsFired = 0; // Reset burst counter
        }
        else
        {
            // Within burst, use short cooldown
            if (Time.time < _lastProjectileFireTime + burstCooldown) return;
        }

        _lastProjectileFireTime = Time.time;
        _burstShotsFired++;

        Vector3 targetPosition = CalculateTargetPosition();
        Vector2 fireDirection = (targetPosition - transform.position).normalized;

        // Use first 4 turrets for projectiles (indices 0-3)
        if (turrets != null && turrets.Length > 0)
        {
            int projectileTurretCount = Mathf.Min(4, turrets.Length);
            for (int i = 0; i < projectileTurretCount; i++)
            {
                GameObject projectile = Instantiate(projectileWeapon.prefab, turrets[i].transform.position, transform.rotation);

                if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
                {
                    projectileScript.targetTag = "Player";
                    projectileScript.Initialize(
                        fireDirection,
                        Vector2.zero,
                        projectileWeapon.speed,
                        projectileWeapon.damage,
                        projectileWeapon.lifetime,
                        projectileWeapon.impactForce,
                        this
                    );
                }
            }
        }
        else
        {
            // Fire from front of ship
            Vector3 spawnPosition = transform.position + transform.up * 0.5f;
            GameObject projectile = Instantiate(projectileWeapon.prefab, spawnPosition, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = "Player";
                projectileScript.Initialize(
                    fireDirection,
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

        // Play projectile fire sound
        PlayOneShotSound(projectileFireSound, 1f, AudioClipType.ProjectileFire);
    }

    private void FireBeam()
    {
        if (_activeBeam != null) return; // Already firing

        // Spawn beam from turret 5 (index 4) or front of ship
        Vector3 spawnPosition;
        if (turrets != null && turrets.Length >= 5)
        {
            spawnPosition = turrets[4].transform.position + transform.up * beamOffsetDistance;
        }
        else
        {
            spawnPosition = transform.position + transform.up * beamOffsetDistance;
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

    private bool HasLineOfSight()
    {
        if (_target == null) return false;

        Vector2 startPosition = transform.position;
        Vector2 directionToTarget = (_target.position - transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, _target.position);

        RaycastHit2D[] hits = Physics2D.RaycastAll(startPosition, directionToTarget, distanceToTarget);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;

            if (hit.collider.CompareTag("Asteroid"))
            {
                return false; // Blocked by asteroid
            }

            if (hit.collider.CompareTag("Player"))
            {
                return true; // Clear line to player
            }
        }

        return false;
    }

    #endregion

    #region Damage Response

    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        _lastDamageTime = Time.time;
        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    #endregion

    #region Evasion

    private void CheckForThreats()
    {
        if (!CurrentLevel.useDodging) return;
        if (Time.time < _lastThreatCheckTime + threatCheckInterval) return;
        _lastThreatCheckTime = Time.time;

        // Check for incoming projectiles
        Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, threatDetectionRadius);

        foreach (var collider in nearbyObjects)
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == "Enemy")
            {
                Rigidbody2D projectileRb = collider.GetComponent<Rigidbody2D>();
                if (projectileRb != null)
                {
                    Vector2 projectileVelocity = projectileRb.linearVelocity;
                    Vector2 toUs = (Vector2)transform.position - (Vector2)projectile.transform.position;

                    // Check if projectile is heading toward us
                    if (Vector2.Dot(projectileVelocity.normalized, toUs.normalized) > 0.5f)
                    {
                        TryDodge(projectileVelocity);
                        return; // One dodge per check
                    }
                }
            }
        }

        // Check for player beam
        if (IsInBeamPath())
        {
            Vector2 beamDirection = _target.up;
            TryDodge(beamDirection);
        }
    }

    private bool IsInBeamPath()
    {
        if (_target == null) return false;

        // Check if player has active beam (approximate by checking forward line)
        Vector2 playerForward = _target.up;
        Vector2 playerToUs = (Vector2)transform.position - (Vector2)_target.position;

        // Must be in front of player
        float projection = Vector2.Dot(playerToUs, playerForward);
        if (projection < 0) return false;

        // Check distance from player's forward line
        Vector2 closestPointOnLine = (Vector2)_target.position + playerForward * projection;
        float distanceToLine = Vector2.Distance(transform.position, closestPointOnLine);

        // Within approximate beam width and within beam range
        return distanceToLine < 1.5f && projection < beamWeapon.maxDistance;
    }

    private void TryDodge(Vector2 threatDirection)
    {
        if (Time.time < _lastDodgeTime + dodgeCooldown) return;

        // Risk vs reward: don't always dodge
        if (Random.value > dodgeChance) return;

        _lastDodgeTime = Time.time;

        // Strafe perpendicular to incoming threat
        Vector2 dodgeDirection = Vector2.Perpendicular(threatDirection.normalized);

        // Random left/right
        if (Random.value > 0.5f) dodgeDirection = -dodgeDirection;

        // Apply impulse
        _rb.AddForce(dodgeDirection * dodgeImpulseForce, ForceMode2D.Impulse);
    }

    #endregion

    #region Positioning Helpers

    private bool IsInPlayerForwardArc()
    {
        if (_target == null) return false;

        Vector2 playerForward = _target.up;
        Vector2 playerToUs = ((Vector2)transform.position - (Vector2)_target.position).normalized;

        float angle = Vector2.Angle(playerForward, playerToUs);
        return angle < forwardArcAngle / 2f;
    }

    private bool IsPlayerPursuing()
    {
        if (_target == null) return false;

        Rigidbody2D playerRb = _target.GetComponent<Rigidbody2D>();
        if (playerRb == null) return false;

        Vector2 playerVelocity = playerRb.linearVelocity;
        Vector2 playerToUs = ((Vector2)transform.position - (Vector2)_target.position).normalized;

        // Check if player is moving fast toward us
        float approachSpeed = Vector2.Dot(playerVelocity, playerToUs);
        return approachSpeed > pursuitVelocityThreshold;
    }

    private bool IsBehindPlayer()
    {
        if (_target == null) return false;

        Vector2 playerForward = _target.up;
        Vector2 playerToUs = ((Vector2)transform.position - (Vector2)_target.position).normalized;

        // We're behind if the angle from player's forward to us is > 120 degrees
        float angle = Vector2.Angle(playerForward, playerToUs);
        return angle >= 120f;
    }

    #endregion

    #region Cleanup

    void OnDestroy()
    {
        StopBeam();
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmosSelected()
    {
        // Flanking distance (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, flankingDistance);

        // Disengage distance (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, disengageDistance);

        // Threat detection radius (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, threatDetectionRadius);

        // Weapon ranges
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, projectilePreferredRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, beamPreferredRange);

        // Forward arc visualization (if target exists)
        if (_target != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange
            Vector3 playerPos = _target.position;
            Vector3 playerForward = _target.up;

            float arcHalfAngle = forwardArcAngle / 2f;
            Vector3 leftEdge = Quaternion.Euler(0, 0, arcHalfAngle) * playerForward * 10f;
            Vector3 rightEdge = Quaternion.Euler(0, 0, -arcHalfAngle) * playerForward * 10f;

            Gizmos.DrawLine(playerPos, playerPos + leftEdge);
            Gizmos.DrawLine(playerPos, playerPos + rightEdge);
        }
    }

    #endregion
}
