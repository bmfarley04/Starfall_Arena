using UnityEngine;

[System.Serializable]
public struct IndicatorConfig
{
    [Header("Arrow Settings")]
    [Tooltip("The arrow sprite/object that points toward the enemy")]
    public GameObject arrowObject;

    [Tooltip("Distance from player center where arrow floats")]
    [Range(1f, 10f)]
    public float indicatorRadius;

    [Tooltip("How fast the arrow moves around the player (0 = instant, higher = smoother)")]
    [Range(0f, 20f)]
    public float positionSmoothSpeed;

    [Tooltip("How fast the arrow rotates to point at enemy (0 = instant, higher = smoother)")]
    [Range(0f, 20f)]
    public float rotationSmoothSpeed;

    [Header("Visibility (Local/Split-Screen)")]
    [Tooltip("If true, arrow will fade out when enemy is very close")]
    public bool fadeWhenClose;

    [Tooltip("Distance at which arrow starts fading (local/split-screen)")]
    public float fadeStartDistance;

    [Tooltip("Distance at which arrow is fully transparent (local/split-screen)")]
    public float fadeEndDistance;

    [Header("Visibility (Networked)")]
    [Tooltip("Distance at which arrow starts fading in networked play (full screen view)")]
    public float networkFadeStartDistance;

    [Tooltip("Distance at which arrow is fully transparent in networked play (full screen view)")]
    public float networkFadeEndDistance;

    [Header("Camera Culling (Local/Split-Screen Only)")]
    [Tooltip("Layer for Player1's arrow. Should be 'Background2' (Player1 sees it, Player2 doesn't)")]
    public string player1Layer;

    [Tooltip("Layer for Player2's arrow. Should be 'Background1' (Player2 sees it, Player1 doesn't)")]
    public string player2Layer;
}

/// <summary>
/// Displays an arrow indicator that floats around the player and points toward their enemy.
/// The arrow is only visible when the player is alive and the enemy is not invisible.
/// </summary>
[RequireComponent(typeof(Player))]
public class EnemyDirectionIndicator : MonoBehaviour
{
    [Header("Enemy Direction Indicator")]
    public IndicatorConfig indicator = new IndicatorConfig
    {
        indicatorRadius = 3f,
        positionSmoothSpeed = 10f,
        rotationSmoothSpeed = 12f,
        fadeWhenClose = true,
        fadeStartDistance = 15f,
        fadeEndDistance = 5f,
        networkFadeStartDistance = 30f,
        networkFadeEndDistance = 10f,
        player1Layer = "Background2", // Player1 sees Background2, but not Background1
        player2Layer = "Background1"  // Player2 sees Background1, but not Background2
    };

    private Player _player;
    private GameObject _enemyShip;
    private SpriteRenderer _arrowRenderer;
    private float _targetAlpha = 1f;
    private bool _isNetworked;
    private bool _isLocalPlayer;
    private bool _networkStateResolved;
    private float _smoothedAngle;
    private bool _angleInitialized;

    private void Awake()
    {
        _player = GetComponent<Player>();

        if (indicator.arrowObject != null)
        {
            _arrowRenderer = indicator.arrowObject.GetComponent<SpriteRenderer>();
            if (_arrowRenderer == null)
            {
                Debug.LogWarning($"Arrow object on {gameObject.name} has no SpriteRenderer. Indicator will not be visible.", this);
            }
        }
        else
        {
            Debug.LogWarning($"No arrow object assigned to EnemyDirectionIndicator on {gameObject.name}", this);
        }
    }

    /// <summary>
    /// Resolves networked vs local state once the NetworkObject has been spawned.
    /// Called lazily from the first Update because IsSpawned is false during Awake/Start.
    /// </summary>
    private void ResolveNetworkState()
    {
        _networkStateResolved = true;

        NetMovement netMovement = GetComponent<NetMovement>();
        _isNetworked = NetTickUtil.IsActive && netMovement != null && netMovement.IsSpawned;
        _isLocalPlayer = !_isNetworked || netMovement.IsOwner;

        if (indicator.arrowObject == null) return;

        if (_isNetworked)
        {
            // In networked play there is a single camera with no split-screen culling.
            // Only the local player needs an indicator; hide the remote player's arrow
            // and force the local arrow to the Default layer so the camera can see it
            // (the prefab may already be on a Background layer that the single camera culls).
            if (_isLocalPlayer)
            {
                int defaultLayer = LayerMask.NameToLayer("Default");
                indicator.arrowObject.layer = defaultLayer;
                SetLayerRecursively(indicator.arrowObject.transform, defaultLayer);
            }
            else
            {
                indicator.arrowObject.SetActive(false);
            }
        }
        else
        {
            // Local split-screen: use per-player layer culling so each camera
            // only renders its own player's arrow.
            SetArrowLayer();
        }
    }

    private void SetArrowLayer()
    {
        if (indicator.arrowObject == null) return;

        string targetLayerName = null;

        // Use the same layer system as invisibility:
        // - Player1's arrow → Background2 (Player1 can see it, Player2 cannot)
        // - Player2's arrow → Background1 (Player2 can see it, Player1 cannot)
        // This reuses the existing camera culling mask setup from the invisibility system
        if (gameObject.CompareTag("Player1"))
        {
            targetLayerName = indicator.player1Layer;
        }
        else if (gameObject.CompareTag("Player2"))
        {
            targetLayerName = indicator.player2Layer;
        }

        // Apply the layer
        if (!string.IsNullOrEmpty(targetLayerName))
        {
            int layerIndex = LayerMask.NameToLayer(targetLayerName);
            if (layerIndex != -1)
            {
                indicator.arrowObject.layer = layerIndex;

                // Also set all children to the same layer
                SetLayerRecursively(indicator.arrowObject.transform, layerIndex);
            }
            else
            {
                Debug.LogWarning($"Layer '{targetLayerName}' not found! Arrow visibility may not work correctly. " +
                    $"Create this layer in Project Settings → Tags and Layers, then configure the camera's culling mask.", this);
            }
        }
    }

    private void SetLayerRecursively(Transform obj, int layer)
    {
        foreach (Transform child in obj)
        {
            child.gameObject.layer = layer;
            SetLayerRecursively(child, layer);
        }
    }

    private void Start()
    {
        // Find the enemy ship based on the player's enemy tag
        FindEnemyShip();

        // Hide arrow initially if no enemy found
        if (_enemyShip == null && indicator.arrowObject != null)
        {
            indicator.arrowObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Resolve networked vs local state on first update (IsSpawned is false during Awake/Start)
        if (!_networkStateResolved)
        {
            ResolveNetworkState();
        }

        // Early exit if arrow object is missing
        if (indicator.arrowObject == null) return;

        // Find enemy if we don't have one yet
        if (_enemyShip == null)
        {
            FindEnemyShip();

            if (_enemyShip == null)
            {
                indicator.arrowObject.SetActive(false);
                return;
            }
        }

        // Check if we should show the indicator
        bool shouldShow = ShouldShowIndicator();
        indicator.arrowObject.SetActive(shouldShow);

        if (!shouldShow) return;

        // Update arrow position and rotation
        UpdateArrowPosition();
        UpdateArrowRotation();
        UpdateArrowTransparency();
    }

    private bool ShouldShowIndicator()
    {
        // In networked play only the local player's indicator is shown
        if (_isNetworked && !_isLocalPlayer) return false;

        // Don't show if player is dead
        if (_player.CurrentHealth <= 0) return false;

        // Don't show if enemy is null (destroyed)
        if (_enemyShip == null) return false;

        // Check if enemy is invisible by checking their layer
        // Invisibility ability changes layer to "Background1", "Background2", or "Invisible"
        int enemyLayer = _enemyShip.layer;
        string enemyLayerName = LayerMask.LayerToName(enemyLayer);

        if (enemyLayerName == "Background1" ||
            enemyLayerName == "Background2" ||
            enemyLayerName == "Invisible")
        {
            return false;
        }

        return true;
    }

    private void FindEnemyShip()
    {
        if (string.IsNullOrEmpty(_player.enemyTag)) return;

        if (_isNetworked)
        {
            // In networked play, tags are synced via NetworkVariable but may arrive
            // after spawn. Use EnumeratePlayers to find the other player reliably.
            foreach (NetMovement candidate in NetMovement.EnumeratePlayers())
            {
                if (candidate != null && candidate.gameObject != gameObject)
                {
                    _enemyShip = candidate.gameObject;
                    return;
                }
            }
            return;
        }

        // Local play: find the enemy by tag
        GameObject foundEnemy = GameObject.FindGameObjectWithTag(_player.enemyTag);

        if (foundEnemy != null)
        {
            _enemyShip = foundEnemy;
        }
    }

    private void UpdateArrowPosition()
    {
        if (_enemyShip == null) return;

        // Calculate raw direction angle from player to enemy
        Vector2 delta = (Vector2)_enemyShip.transform.position - (Vector2)transform.position;
        float targetAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // Smooth the direction angle to absorb network jitter
        if (!_angleInitialized)
        {
            _smoothedAngle = targetAngle;
            _angleInitialized = true;
        }
        else if (indicator.positionSmoothSpeed > 0)
        {
            _smoothedAngle = Mathf.LerpAngle(_smoothedAngle, targetAngle, Time.deltaTime * indicator.positionSmoothSpeed);
        }
        else
        {
            _smoothedAngle = targetAngle;
        }

        // Derive position from the smoothed angle
        float rad = _smoothedAngle * Mathf.Deg2Rad;
        Vector2 smoothedDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 targetPosition = (Vector2)transform.position + smoothedDirection * indicator.indicatorRadius;

        indicator.arrowObject.transform.position = (Vector3)targetPosition;
    }

    private void UpdateArrowRotation()
    {
        // Arrow rotation follows the same smoothed angle (sprite points up, so subtract 90°)
        float arrowAngle = _smoothedAngle - 90f;

        if (indicator.rotationSmoothSpeed > 0)
        {
            float currentAngle = indicator.arrowObject.transform.eulerAngles.z;
            float smoothedRotation = Mathf.LerpAngle(currentAngle, arrowAngle, Time.deltaTime * indicator.rotationSmoothSpeed);
            indicator.arrowObject.transform.rotation = Quaternion.Euler(0, 0, smoothedRotation);
        }
        else
        {
            indicator.arrowObject.transform.rotation = Quaternion.Euler(0, 0, arrowAngle);
        }
    }

    private void UpdateArrowTransparency()
    {
        if (_arrowRenderer == null || _enemyShip == null) return;

        if (indicator.fadeWhenClose)
        {
            // Use networked fade distances when in a networked session (full screen view)
            float fadeStart = _isNetworked ? indicator.networkFadeStartDistance : indicator.fadeStartDistance;
            float fadeEnd = _isNetworked ? indicator.networkFadeEndDistance : indicator.fadeEndDistance;

            // Calculate distance to enemy
            float distanceToEnemy = Vector2.Distance(transform.position, _enemyShip.transform.position);

            // Calculate target alpha based on distance
            if (distanceToEnemy >= fadeStart)
            {
                _targetAlpha = 1f;
            }
            else if (distanceToEnemy <= fadeEnd)
            {
                _targetAlpha = 0f;
            }
            else
            {
                // Linear interpolation between fade distances
                float fadeRange = fadeStart - fadeEnd;
                float fadeProgress = (distanceToEnemy - fadeEnd) / fadeRange;
                _targetAlpha = Mathf.Clamp01(fadeProgress);
            }

            // Apply alpha to sprite
            Color currentColor = _arrowRenderer.color;
            currentColor.a = _targetAlpha;
            _arrowRenderer.color = currentColor;
        }
        else
        {
            // Ensure alpha is 1 if not fading
            Color currentColor = _arrowRenderer.color;
            currentColor.a = 1f;
            _arrowRenderer.color = currentColor;
        }
    }

    // Public method to manually refresh enemy reference (useful if enemy respawns)
    public void RefreshEnemyReference()
    {
        FindEnemyShip();
    }
}
