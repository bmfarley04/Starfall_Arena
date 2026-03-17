using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
    private NetMovement _netMovement;
    private Coroutine _visibilityCoroutine;
    private bool _isInvisible;
    private readonly List<Renderer> _cachedRenderers = new List<Renderer>();
    private readonly List<bool> _cachedRendererStates = new List<bool>();

    protected override void Awake()
    {
        base.Awake();

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _netMovement = GetComponent<NetMovement>();
        CacheRenderers();
    }

    void FixedUpdate()
    {
        
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        if (!value.isPressed)
        {
            return;
        }

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkInvisibilityState(true, authoritative: false);
            }

            _netMovement.RequestInvisibilityState(true);
            return;
        }

        ApplyNetworkInvisibilityState(true, authoritative: true);
    }

    void BecomeVisible()
    {
        _isInvisible = false;
        gameObject.layer = originalLayer;
        SetAllChildrenLayer(originalLayer);
        RestoreRendererVisibility();
        
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

    public void ApplyNetworkInvisibilityState(bool isActive, bool authoritative)
    {
        if (isActive)
        {
            if (_isInvisible)
            {
                return;
            }

            _isInvisible = true;
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
            if (ShouldHideRenderersForLocalView())
            {
                HideRenderers();
            }
            else
            {
                RestoreRendererVisibility();
            }

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

            if (_visibilityCoroutine != null)
            {
                StopCoroutine(_visibilityCoroutine);
            }

            if (!NetTickUtil.IsActive)
            {
                _visibilityCoroutine = StartCoroutine(ExpireInvisibilityLocal());
            }
            else if (authoritative && _netMovement != null && _netMovement.IsServer)
            {
                _visibilityCoroutine = StartCoroutine(ExpireInvisibilityAuthoritative());
            }
            return;
        }

        if (!_isInvisible && _visibilityCoroutine == null)
        {
            BecomeVisible();
            return;
        }

        _isInvisible = false;
        if (_visibilityCoroutine != null)
        {
            StopCoroutine(_visibilityCoroutine);
            _visibilityCoroutine = null;
        }

        BecomeVisible();
    }

    public void BreakInvisibilityFromAction()
    {
        if (!_isInvisible)
        {
            return;
        }

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkInvisibilityState(false, authoritative: false);
            }

            _netMovement.RequestInvisibilityState(false);
            return;
        }

        ApplyNetworkInvisibilityState(false, authoritative: true);
    }

    public override bool IsAbilityActive()
    {
        return _isInvisible;
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
        ApplyNetworkInvisibilityState(false, authoritative: NetTickUtil.IsActive && _netMovement != null && _netMovement.IsServer);

        // Stop sound
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        if (_soundFadeCoroutine != null)
        {
            StopCoroutine(_soundFadeCoroutine);
        }

        if (_visibilityCoroutine != null)
        {
            StopCoroutine(_visibilityCoroutine);
        }

        base.Die();
    }

    private void CacheRenderers()
    {
        _cachedRenderers.Clear();
        _cachedRendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            _cachedRenderers.Add(renderer);
            _cachedRendererStates.Add(renderer.enabled);
        }
    }

    private void HideRenderers()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            Renderer renderer = _cachedRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (i >= _cachedRendererStates.Count)
            {
                _cachedRendererStates.Add(renderer.enabled);
            }
            else
            {
                _cachedRendererStates[i] = renderer.enabled;
            }

            renderer.enabled = false;
        }
    }

    private bool ShouldHideRenderersForLocalView()
    {
        if (!NetTickUtil.IsActive || _netMovement == null || !_netMovement.IsSpawned)
        {
            return false;
        }

        return !_netMovement.IsOwner;
    }

    private void RestoreRendererVisibility()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            Renderer renderer = _cachedRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool shouldBeVisible = i < _cachedRendererStates.Count && _cachedRendererStates[i];
            renderer.enabled = shouldBeVisible;
        }
    }

    private System.Collections.IEnumerator ExpireInvisibilityLocal()
    {
        yield return new WaitForSeconds(stats.duration);
        ApplyNetworkInvisibilityState(false, authoritative: true);
    }

    private System.Collections.IEnumerator ExpireInvisibilityAuthoritative()
    {
        yield return new WaitForSeconds(stats.duration);

        if (_netMovement != null)
        {
            _netMovement.SetInvisibilityStateAuthoritative(false);
        }
        else
        {
            ApplyNetworkInvisibilityState(false, authoritative: true);
        }
    }
}
