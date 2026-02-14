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

    [Header("Visibility")]
    [Tooltip("If true, arrow will fade out when enemy is very close")]
    public bool fadeWhenClose;

    [Tooltip("Distance at which arrow starts fading")]
    public float fadeStartDistance;

    [Tooltip("Distance at which arrow is fully transparent")]
    public float fadeEndDistance;

    [Header("Camera Culling")]
    [Tooltip("Layer name for Player1's UI (arrow will be set to this layer if this is Player1)")]
    public string player1Layer;

    [Tooltip("Layer name for Player2's UI (arrow will be set to this layer if this is Player2)")]
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
        player1Layer = "Player1UI",
        player2Layer = "Player2UI"
    };

    private Player _player;
    private GameObject _enemyShip;
    private SpriteRenderer _arrowRenderer;
    private float _targetAlpha = 1f;

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

            // Set arrow to player-specific layer so only this player's camera sees it
            SetArrowLayer();
        }
        else
        {
            Debug.LogWarning($"No arrow object assigned to EnemyDirectionIndicator on {gameObject.name}", this);
        }
    }

    private void SetArrowLayer()
    {
        if (indicator.arrowObject == null) return;

        string targetLayerName = null;

        // Determine which layer to use based on player tag
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

        // Find the enemy by tag
        GameObject foundEnemy = GameObject.FindGameObjectWithTag(_player.enemyTag);

        if (foundEnemy != null)
        {
            _enemyShip = foundEnemy;
        }
    }

    private void UpdateArrowPosition()
    {
        if (_enemyShip == null) return;

        // Calculate direction from player to enemy
        Vector2 directionToEnemy = ((Vector2)_enemyShip.transform.position - (Vector2)transform.position).normalized;

        // Calculate target position at the radius
        Vector2 targetPosition = (Vector2)transform.position + directionToEnemy * indicator.indicatorRadius;

        // Smoothly move arrow to target position
        if (indicator.positionSmoothSpeed > 0)
        {
            indicator.arrowObject.transform.position = Vector2.Lerp(
                indicator.arrowObject.transform.position,
                targetPosition,
                Time.deltaTime * indicator.positionSmoothSpeed
            );
        }
        else
        {
            indicator.arrowObject.transform.position = targetPosition;
        }
    }

    private void UpdateArrowRotation()
    {
        if (_enemyShip == null) return;

        // Calculate direction from arrow to enemy
        Vector2 directionToEnemy = ((Vector2)_enemyShip.transform.position - (Vector2)indicator.arrowObject.transform.position).normalized;

        // Calculate target rotation angle (assuming arrow sprite points up by default)
        float targetAngle = Mathf.Atan2(directionToEnemy.y, directionToEnemy.x) * Mathf.Rad2Deg - 90f;

        // Smoothly rotate arrow to point at enemy
        if (indicator.rotationSmoothSpeed > 0)
        {
            float currentAngle = indicator.arrowObject.transform.eulerAngles.z;
            float smoothedAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * indicator.rotationSmoothSpeed);
            indicator.arrowObject.transform.rotation = Quaternion.Euler(0, 0, smoothedAngle);
        }
        else
        {
            indicator.arrowObject.transform.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
    }

    private void UpdateArrowTransparency()
    {
        if (_arrowRenderer == null || _enemyShip == null) return;

        if (indicator.fadeWhenClose)
        {
            // Calculate distance to enemy
            float distanceToEnemy = Vector2.Distance(transform.position, _enemyShip.transform.position);

            // Calculate target alpha based on distance
            if (distanceToEnemy >= indicator.fadeStartDistance)
            {
                _targetAlpha = 1f;
            }
            else if (distanceToEnemy <= indicator.fadeEndDistance)
            {
                _targetAlpha = 0f;
            }
            else
            {
                // Linear interpolation between fade distances
                float fadeRange = indicator.fadeStartDistance - indicator.fadeEndDistance;
                float fadeProgress = (distanceToEnemy - indicator.fadeEndDistance) / fadeRange;
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
