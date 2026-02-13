using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplitScreenManager : MonoBehaviour
{
    public Boolean verticalSplit = true;
    public CinemachineCamera player1Cinemachine;
    public Camera player1Camera;
    public GameObject player1;
    public CinemachineCamera player2Cinemachine;
    public Camera player2Camera;
    public GameObject player2;

    [Header("Full-Screen Cameras")]
    [Tooltip("Single camera used for full-screen UI moments (VS screen, augment select, game end)")]
    public Camera wholeScreenCamera;

    [Tooltip("Overlay UI camera that renders UI canvases on top of the whole-screen camera")]
    public Camera uiOverlayCamera;

    private PlayerInput player1Input;
    private PlayerInput player2Input;

    void Start()
    {
        // Don't run initial player wiring in Start — GameSceneManager handles it via AssignPlayers
    }

    public void AssignPlayers(GameObject p1, GameObject p2)
    {
        player1 = p1;
        player2 = p2;

        player1Input = player1.GetComponent<PlayerInput>();
        player2Input = player2.GetComponent<PlayerInput>();
        player1Input.camera = player1Camera;
        player2Input.camera = player2Camera;
        player1Cinemachine.Follow = player1.transform;
        player1Cinemachine.LookAt = player1.transform;
        player2Cinemachine.Follow = player2.transform;
        player2Cinemachine.LookAt = player2.transform;
    }

    /// <summary>
    /// Enables the split-screen cameras and disables the whole-screen + UI overlay cameras.
    /// </summary>
    public void ActivateSplitScreen()
    {
        if (player1Camera != null) player1Camera.enabled = true;
        if (player2Camera != null) player2Camera.enabled = true;
        if (wholeScreenCamera != null) wholeScreenCamera.enabled = false;
        if (uiOverlayCamera != null) uiOverlayCamera.enabled = false;
    }

    /// <summary>
    /// Enables the whole-screen + UI overlay cameras and disables the split-screen cameras.
    /// </summary>
    public void ActivateWholeScreen()
    {
        if (player1Camera != null) player1Camera.enabled = false;
        if (player2Camera != null) player2Camera.enabled = false;
        if (wholeScreenCamera != null) wholeScreenCamera.enabled = true;
        if (uiOverlayCamera != null) uiOverlayCamera.enabled = true;
    }

    /// <summary>
    /// Detaches both Cinemachine cameras from their follow targets and smoothly lerps
    /// them back to the given positions. Both cameras arrive simultaneously — the one
    /// that is farther away moves faster.
    /// Call this BEFORE swapping to whole-screen mode.
    /// </summary>
    public IEnumerator LerpCamerasToPositions(Vector3 p1TargetPos, Vector3 p2TargetPos, float duration)
    {
        // Detach Cinemachine so we can control camera positions directly
        if (player1Cinemachine != null)
        {
            player1Cinemachine.Follow = null;
            player1Cinemachine.LookAt = null;
        }
        if (player2Cinemachine != null)
        {
            player2Cinemachine.Follow = null;
            player2Cinemachine.LookAt = null;
        }

        // Use the Cinemachine camera transforms for lerp (the actual camera position)
        Transform cam1Transform = player1Cinemachine != null ? player1Cinemachine.transform : null;
        Transform cam2Transform = player2Cinemachine != null ? player2Cinemachine.transform : null;

        Vector3 start1 = cam1Transform != null ? cam1Transform.position : p1TargetPos;
        Vector3 start2 = cam2Transform != null ? cam2Transform.position : p2TargetPos;

        // Preserve camera Z positions (Cinemachine manages these)
        float cam1Z = start1.z;
        float cam2Z = start2.z;

        // Compute 2D distances so both cameras can arrive at the same time
        Vector3 target1 = new Vector3(p1TargetPos.x, p1TargetPos.y, cam1Z);
        Vector3 target2 = new Vector3(p2TargetPos.x, p2TargetPos.y, cam2Z);

        float dist1 = Vector3.Distance(start1, target1);
        float dist2 = Vector3.Distance(start2, target2);
        float maxDist = Mathf.Max(dist1, dist2, 0.001f);

        // Each camera gets a normalized t-multiplier: the closer one moves slower
        // so both finish at the same wall-clock time
        float speed1 = dist1 / maxDist; // 0–1 fraction of full speed
        float speed2 = dist2 / maxDist;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);

            if (cam1Transform != null)
            {
                float t1 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rawT * (1f / Mathf.Max(speed1, 0.001f))));
                t1 = Mathf.Min(t1, 1f);
                cam1Transform.position = Vector3.Lerp(start1, target1, t1);
            }
            if (cam2Transform != null)
            {
                float t2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rawT * (1f / Mathf.Max(speed2, 0.001f))));
                t2 = Mathf.Min(t2, 1f);
                cam2Transform.position = Vector3.Lerp(start2, target2, t2);
            }

            yield return null;
        }

        // Snap to exact targets
        if (cam1Transform != null) cam1Transform.position = target1;
        if (cam2Transform != null) cam2Transform.position = target2;
    }

    void Update()
    {
        // Only set rects when split-screen cameras are active
        if (player1Camera != null && player1Camera.enabled && player2Camera != null && player2Camera.enabled)
        {
            if (verticalSplit)
            {
                player1Camera.rect = new Rect(0, 0, 0.5f, 1);
                player2Camera.rect = new Rect(0.5f, 0, 0.5f, 1);
            }
            else
            {
                player1Camera.rect = new Rect(0, 0.5f, 1, 0.5f);
                player2Camera.rect = new Rect(0, 0, 1, 0.5f);
            }
        }
    }
}
