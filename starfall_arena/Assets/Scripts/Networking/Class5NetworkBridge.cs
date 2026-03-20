using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network bridge for Class5-specific charge replication and audio playback.
/// Keeps NetMovement lean by hosting Class5 RPCs and proxy audio helpers.
/// </summary>
public class Class5NetworkBridge : NetworkBehaviour
{
    private Class5 _class5;
    private Coroutine _chargeComboCoroutine;
    private Coroutine _fireBurstCoroutine;

    private void Awake()
    {
        _class5 = GetComponent<Class5>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer && _class5 != null)
        {
            // Seed initial state for late joiners.
            _class5.ApplyNetworkChargeState(_class5.CurrentCharges, 0, playAudio: false);
            BroadcastChargeState(_class5.CurrentCharges, 0, playAudio: false);
        }
    }

    public void TickServerCharges()
    {
        if (!IsServer || _class5 == null)
        {
            return;
        }

        _class5.ServerTickCharges();
    }

    public void BroadcastChargeState(int currentCharges, int delta, bool playAudio, bool forceBroadcast = false)
    {
        if (!IsServer)
        {
            return;
        }

        if (delta == 0 && !forceBroadcast)
        {
            return;
        }

        BroadcastChargeStateClientRpc(currentCharges, delta, playAudio || forceBroadcast);
    }

    [ClientRpc]
    private void BroadcastChargeStateClientRpc(int currentCharges, int delta, bool playAudio)
    {
        // Server already applied locally.
        if (IsServer)
        {
            return;
        }

        if (_class5 == null)
        {
            _class5 = GetComponent<Class5>();
        }

        bool shouldPlayAudio = playAudio && !(IsOwner && delta < 0);
        _class5?.ApplyNetworkChargeState(currentCharges, delta, shouldPlayAudio);
    }

    public void PlayChargeAudioForProxy(
        int currentCharges,
        SoundEffect gain1,
        SoundEffect gain2,
        SoundEffect gain3,
        SoundEffect gain4,
        float comboSpacing,
        bool isSpend,
        SoundEffect spendSound = null)
    {
        if (_class5 == null)
        {
            _class5 = GetComponent<Class5>();
        }

        if (_class5 == null)
        {
            return;
        }

        if (isSpend)
        {
            spendSound?.Play(_class5.GetAvailableAudioSource());
            return;
        }

        if (_chargeComboCoroutine != null)
        {
            StopCoroutine(_chargeComboCoroutine);
        }

        if (currentCharges >= Class5.ProjectileBurstCount)
        {
            SoundEffect[] sounds =
            {
                gain1,
                gain2,
                gain3,
                gain4 != null ? gain4 : gain1
            };

            _chargeComboCoroutine = StartCoroutine(PlayChargeCombo(sounds, comboSpacing));
            return;
        }

        SoundEffect oneShot = currentCharges switch
        {
            1 => gain1,
            2 => gain2,
            3 => gain3,
            _ => gain4 != null ? gain4 : gain1
        };

        oneShot?.Play(_class5.GetAvailableAudioSource());
    }

    private IEnumerator PlayChargeCombo(SoundEffect[] sounds, float comboSpacing)
    {
        float spacing = comboSpacing > 0f ? comboSpacing : 0.05f;
        float pitch = 1f;
        foreach (SoundEffect effect in sounds)
        {
            if (effect != null)
            {
                pitch = Random.Range(effect.minPitch, effect.maxPitch);
                break;
            }
        }

        for (int i = 0; i < sounds.Length; i++)
        {
            SoundEffect effect = sounds[i];
            if (effect != null)
            {
                AudioSource source = _class5.GetAvailableAudioSource();
                if (source != null)
                {
                    source.clip = effect.clip;
                    source.volume = effect.volume;
                    source.pitch = pitch;
                    source.Play();
                }
            }

            if (i < sounds.Length - 1)
            {
                yield return new WaitForSeconds(spacing);
            }
        }

        _chargeComboCoroutine = null;
    }

    public void PlayFireBurst(SoundEffect fireSound)
    {
        if (_class5 == null)
        {
            _class5 = GetComponent<Class5>();
        }

        if (fireSound == null || _class5 == null)
        {
            return;
        }

        if (_fireBurstCoroutine != null)
        {
            StopCoroutine(_fireBurstCoroutine);
        }

        _fireBurstCoroutine = StartCoroutine(PlayFireBurstRoutine(fireSound));
    }

    private IEnumerator PlayFireBurstRoutine(SoundEffect fireSound)
    {
        for (int i = 0; i < Class5.ProjectileBurstCount; i++)
        {
            fireSound.Play(_class5.GetAvailableAudioSource());
            if (i < Class5.ProjectileBurstCount - 1)
            {
                yield return new WaitForSeconds(Class5.ProjectileBurstSpacing);
            }
        }

        _fireBurstCoroutine = null;
    }
}