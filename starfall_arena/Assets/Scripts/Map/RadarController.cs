using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RadarController : MonoBehaviour
{
    [System.Serializable]
    public struct RadarCategory
    {
        public string name;
        public LayerMask targetLayer;
        public GameObject blipPrefab;
    }

    [Header("References")]
    public Transform playerTransform;
    public RectTransform blipContainer;
    public RectTransform mapBoundaryRect;
    public SceneManagerScript sceneManager;

    [Header("Radar Settings")]
    public float detectionRadius = 50f;
    public float radarUiRadius = 100f; // Ensure this is half the width of your blue circle
    public bool clampToEdge = true;

    [Header("Calibration")]
    [Tooltip("Adjust this if the Red Box is the wrong size, but DO NOT touch the code scale.")]
    public float boundarySizeCorrection = 1.0f;

    [Header("Tracking Categories")]
    public List<RadarCategory> trackableObjects;

    private Dictionary<Collider2D, RectTransform> activeBlips = new Dictionary<Collider2D, RectTransform>();
    private List<Collider2D> targetsToRemove = new List<Collider2D>();

    // 1. Calculate Scale ONCE per frame so everyone uses the same number
    private float currentRadarScale;

    void Start()
    {
        if (sceneManager == null) sceneManager = FindAnyObjectByType<SceneManagerScript>();
    }

    void Update()
    {
        if (playerTransform == null)
        {
            var p = FindAnyObjectByType<PlayerScript>();
            if (p != null) playerTransform = p.transform;
            else return;
        }

        // CALCULATE SCALE HERE
        // This ensures Blips and Boundary move at the exact same speed
        currentRadarScale = radarUiRadius / detectionRadius;

        UpdateBlips();
        UpdateMapBoundary();
        CleanUpBlips();
    }

    void UpdateMapBoundary()
    {
        if (mapBoundaryRect == null || sceneManager == null) return;

        // --- POSITION (Must use currentRadarScale to prevent parallax) ---
        Vector2 worldCenter = sceneManager.GetBoxCenter();
        Vector2 offsetFromPlayer = worldCenter - (Vector2)playerTransform.position;
        mapBoundaryRect.anchoredPosition = offsetFromPlayer * currentRadarScale;

        // --- SIZE (Can be adjusted independently) ---
        Vector2 worldSize = sceneManager.GetWarningBoxSize();
        // We apply the 'boundarySizeCorrection' here to fix the visual size issue
        mapBoundaryRect.sizeDelta = worldSize * currentRadarScale * boundarySizeCorrection;
    }

    void UpdateBlips()
    {
        foreach (var category in trackableObjects)
        {
            Collider2D[] targets = Physics2D.OverlapCircleAll(playerTransform.position, detectionRadius, category.targetLayer);

            foreach (var target in targets)
            {
                if (target.transform == playerTransform) continue;

                if (!activeBlips.ContainsKey(target))
                {
                    CreateBlip(target, category.blipPrefab);
                }
                UpdateBlipPosition(target, activeBlips[target]);
            }
        }
    }

    void CreateBlip(Collider2D target, GameObject prefabToUse)
    {
        GameObject newBlip = Instantiate(prefabToUse, blipContainer);
        activeBlips.Add(target, newBlip.GetComponent<RectTransform>());
    }

    void UpdateBlipPosition(Collider2D target, RectTransform blipRect)
    {
        if (target == null) return;

        // Use the attached Rigidbody2D position if available, as it represents
        // the true physics center. Otherwise fall back to transform position.
        // This avoids offset issues from colliders with local offsets that
        // get scaled/rotated with the parent object.
        Vector3 targetPosition;
        if (target.attachedRigidbody != null)
        {
            targetPosition = target.attachedRigidbody.position;
        }
        else
        {
            targetPosition = target.transform.position;
        }
        Vector3 offset = targetPosition - playerTransform.position;
        float distanceToTarget = offset.magnitude;
        Vector2 direction = new Vector2(offset.x, offset.y).normalized;

        // This is the standard normalized distance (0 to 1)
        float normalizedDistance = distanceToTarget / detectionRadius;

        // --- PARALLAX FIX ---
        // Originally you calculated position as: direction * normalizedDistance * radarUiRadius
        // That is mathematically the same as: direction * distance * currentRadarScale
        // We use that logic here to ensure it matches the map boundary perfectly.

        if (clampToEdge)
        {
            // If clamped, we stick to the radius
            if (normalizedDistance > 1.0f)
            {
                blipRect.anchoredPosition = direction * radarUiRadius;
            }
            else
            {
                // If inside, we move using the shared scale
                blipRect.anchoredPosition = direction * distanceToTarget * currentRadarScale;
            }
        }
        else
        {
            // Non-clamped logic
            if (normalizedDistance > 1.0f)
            {
                blipRect.gameObject.SetActive(false);
                return;
            }
            blipRect.gameObject.SetActive(true);
            blipRect.anchoredPosition = direction * distanceToTarget * currentRadarScale;
        }
    }

    void CleanUpBlips()
    {
        targetsToRemove.Clear();
        foreach (var pair in activeBlips)
        {
            if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || IsOutOfRange(pair.Key))
            {
                targetsToRemove.Add(pair.Key);
            }
        }

        foreach (var target in targetsToRemove)
        {
            if (activeBlips[target] != null) Destroy(activeBlips[target].gameObject);
            activeBlips.Remove(target);
        }
    }

    bool IsOutOfRange(Collider2D target)
    {
        return Vector3.Distance(target.transform.position, playerTransform.position) > detectionRadius * 1.2f;
    }
}