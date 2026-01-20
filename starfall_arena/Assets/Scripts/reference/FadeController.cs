using UnityEngine;
using System.Collections; // Needed for Coroutines

public class FadeController : MonoBehaviour
{
    private static FadeController instance;
    private Material fadeMaterial;
    private float currentAlpha = 1f;

    public float fadeDuration = 3f; // Duration of the fade in seconds

    void Awake()
    {
        // Create a simple material for screen-wide fade
        Shader fadeShader = Shader.Find("UI/Default");
        fadeMaterial = new Material(fadeShader);
        fadeMaterial.color = new Color(0, 0, 0, 1);

        // Start the fade-in immediately when the scene loads
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float startTime = Time.time;

        while (Time.time < startTime + fadeDuration)
        {
            // Calculate the current alpha (goes from 1.0 to 0.0)
            float t = (Time.time - startTime) / fadeDuration;
            currentAlpha = Mathf.Lerp(1f, 0f, t);

            yield return null; // Wait for the next frame
        }

        // Ensure completely transparent at the end
        currentAlpha = 0f;
        enabled = false; // Disable to stop rendering
    }

    void OnGUI()
    {
        if (currentAlpha > 0)
        {
            // Draw a black rectangle covering the entire screen
            GUI.color = new Color(0, 0, 0, currentAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white; // Reset GUI color
        }
    }
}