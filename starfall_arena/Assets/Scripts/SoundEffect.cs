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
}