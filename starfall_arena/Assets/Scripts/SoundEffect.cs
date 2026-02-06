using UnityEngine;

[CreateAssetMenu(menuName = "Audio/SoundEffect")]
public class SoundEffect : ScriptableObject 
{
    public AudioClip clip;
    
    [Range(0f, 1f)] public float volume = 1f;
    
    [Header("Randomization")]
    [Range(0.1f, 2f)] public float minPitch = 0.95f;
    [Range(0.1f, 2f)] public float maxPitch = 1.05f;

    public void Play(AudioSource source)
    {
        if (clip == null) return;

        source.clip = clip;
        source.volume = volume;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
    }

    /// <summary>
    /// Plays the sound at a world position (creates a temporary AudioSource that auto-destroys).
    /// Use this when the original object is being destroyed.
    /// </summary>
    public void PlayAtPoint(Vector3 position)
    {
        if (clip == null) return;

        // Create temp GameObject with AudioSource
        GameObject tempAudio = new GameObject("TempAudio_" + clip.name);
        tempAudio.transform.position = position;

        AudioSource source = tempAudio.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.spatialBlend = 0f; // 2D sound
        source.Play();

        // Destroy after clip finishes
        Object.Destroy(tempAudio, clip.length / source.pitch);
    }
}