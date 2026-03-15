using UnityEngine;
using UnityEngine.InputSystem;

public class Invisibility : Ability
{
    // Audio implemented with AI
    [System.Serializable]
    public struct InvisibilityConfig
    {
        [Header("Sound Effects")]
        [Tooltip("Sound played when becoming invisible")]
        public SoundEffect becomeInvisibleSound;
        [Tooltip("Sound played when becoming visible again")]
        public SoundEffect becomeVisibleSound;
        [Tooltip("Fade in/out duration for sounds (seconds)")]
        public float soundFadeDuration;
    }

    [Header("Invisibility Settings")]
    public InvisibilityConfig invisibility;

    private AudioSource _audioSource;
    private Coroutine _soundFadeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
    }

    void FixedUpdate()
    {
        
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);
        
        if (gameObject.CompareTag("Player1"))
        {
            gameObject.layer = LayerMask.NameToLayer("Background1");
        }
        else if (gameObject.CompareTag("Player2"))
        {
            gameObject.layer = LayerMask.NameToLayer("Background2");
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer("Invisible");
        }
        SetAllChildrenLayer(gameObject.layer);
        
        // Play invisibility sound with fade-in
        if (invisibility.becomeInvisibleSound != null && _audioSource != null)
        {
            _audioSource.volume = 0f;
            invisibility.becomeInvisibleSound.Play(_audioSource);
            
            if (_soundFadeCoroutine != null)
            {
                StopCoroutine(_soundFadeCoroutine);
            }
            _soundFadeCoroutine = StartCoroutine(FadeVolume(invisibility.becomeInvisibleSound.volume));
        }
        
        Invoke("BecomeVisible", stats.duration);
    }

    void BecomeVisible()
    {
        gameObject.layer = originalLayer;
        SetAllChildrenLayer(originalLayer);
        
        // Play visibility sound with fade-in
        if (invisibility.becomeVisibleSound != null && _audioSource != null)
        {
            _audioSource.volume = 0f;
            invisibility.becomeVisibleSound.Play(_audioSource);
            
            if (_soundFadeCoroutine != null)
            {
                StopCoroutine(_soundFadeCoroutine);
            }
            _soundFadeCoroutine = StartCoroutine(FadeVolume(invisibility.becomeVisibleSound.volume));
        }
    }

    void SetAllChildrenLayer(int layer)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.layer = layer;
            SetAllChildrenLayerRecursive(child, layer);
        }
    }
    
    void SetAllChildrenLayerRecursive(Transform parent, int layer)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.layer = layer;
            SetAllChildrenLayerRecursive(child, layer);
        }
    }

    // ===== AUDIO =====
    private System.Collections.IEnumerator FadeVolume(float targetVolume)
    {
        if (_audioSource == null) yield break;

        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < invisibility.soundFadeDuration)
        {
            if (_audioSource == null || (!_audioSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / invisibility.soundFadeDuration;
            _audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (_audioSource == null) yield break;

        _audioSource.volume = targetVolume;
    }

    public override void Die()
    {
        // Stop sound
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        if (_soundFadeCoroutine != null)
        {
            StopCoroutine(_soundFadeCoroutine);
        }

        base.Die();
    }
}
