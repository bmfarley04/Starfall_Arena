using UnityEngine;

public static class HUDCanvasCameraResolver3D
{
    public static void BindCanvasToBestCamera(Canvas canvas, Camera preferredCamera = null)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return;
        }

        Camera resolvedCamera = ResolveBestCamera(canvas, preferredCamera);
        if (resolvedCamera == null || ReferenceEquals(canvas.worldCamera, resolvedCamera))
        {
            return;
        }

        canvas.worldCamera = resolvedCamera;
    }

    public static Camera ResolveBestCamera(Canvas canvas, Camera preferredCamera = null)
    {
        if (preferredCamera != null && preferredCamera.isActiveAndEnabled)
        {
            return preferredCamera;
        }

        if (canvas != null && canvas.worldCamera != null && canvas.worldCamera.isActiveAndEnabled)
        {
            return canvas.worldCamera;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled && camera.name == "UICamera")
            {
                return camera;
            }
        }

        return Camera.main;
    }
}
