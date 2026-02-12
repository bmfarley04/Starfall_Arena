using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallArena.UI
{
    public class SegmentedBar : MonoBehaviour
    {
        [Header("Segments")]
        [Tooltip("Ordered list of segment images (index 0 = leftmost/first segment)")]
        public Image[] segments;

        [Header("Alpha Settings")]
        [Tooltip("Alpha for fully depleted segments (0-1)")]
        [Range(0f, 1f)]
        public float depletedAlpha = 0.059f;

        [Header("Flash Settings")]
        [Tooltip("Material swapped onto segments briefly when depleted (e.g. a white/bright material)")]
        public Material flashMaterial;
        [Tooltip("Duration of the flash before reverting to original material (seconds)")]
        public float flashDuration = 0.15f;

        private float _previousValue = -1f;
        private float _previousMaxValue;
        private Material[] _originalMaterials;
        private Coroutine[] _flashCoroutines;

        void Awake()
        {
            CacheOriginals();
        }

        private void CacheOriginals()
        {
            if (segments == null) return;

            _originalMaterials = new Material[segments.Length];
            _flashCoroutines = new Coroutine[segments.Length];

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                    _originalMaterials[i] = segments[i].material;
            }
        }

        public void InitializeBar(float currentValue, float maxValue)
        {
            if (_originalMaterials == null) CacheOriginals();

            _previousValue = currentValue;
            _previousMaxValue = maxValue;
            ApplySegmentAlphas(currentValue, maxValue, flash: false);
        }

        public void UpdateBar(float currentValue, float maxValue)
        {
            if (segments == null || segments.Length == 0 || maxValue <= 0f) return;
            if (_originalMaterials == null) CacheOriginals();

            bool takingDamage = _previousValue >= 0f && currentValue < _previousValue;
            ApplySegmentAlphas(currentValue, maxValue, flash: takingDamage);

            _previousValue = currentValue;
            _previousMaxValue = maxValue;
        }

        private void ApplySegmentAlphas(float currentValue, float maxValue, bool flash)
        {
            float hpPerSegment = maxValue / segments.Length;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;

                float segmentStart = i * hpPerSegment;
                float segmentEnd = (i + 1) * hpPerSegment;

                float targetAlpha;
                if (currentValue >= segmentEnd)
                {
                    targetAlpha = 1f;
                }
                else if (currentValue <= segmentStart)
                {
                    targetAlpha = depletedAlpha;
                }
                else
                {
                    float fraction = (currentValue - segmentStart) / hpPerSegment;
                    targetAlpha = Mathf.Max(fraction, depletedAlpha);
                }

                if (flash)
                {
                    float previousAlpha = GetSegmentAlpha(i, _previousValue, _previousMaxValue);
                    bool justDepleted = targetAlpha < previousAlpha - 0.001f;

                    if (justDepleted)
                    {
                        if (_flashCoroutines[i] != null)
                            StopCoroutine(_flashCoroutines[i]);
                        _flashCoroutines[i] = StartCoroutine(FlashSegment(i, targetAlpha));
                        continue;
                    }
                }

                SetSegmentAlpha(i, targetAlpha);
            }
        }

        private void SetSegmentAlpha(int index, float alpha)
        {
            Color c = segments[index].color;
            c.a = alpha;
            segments[index].color = c;
        }

        private float GetSegmentAlpha(int index, float value, float maxValue)
        {
            if (maxValue <= 0f) return depletedAlpha;

            float hpPerSegment = maxValue / segments.Length;
            float segmentStart = index * hpPerSegment;
            float segmentEnd = (index + 1) * hpPerSegment;

            if (value >= segmentEnd) return 1f;
            if (value <= segmentStart) return depletedAlpha;
            return Mathf.Max((value - segmentStart) / hpPerSegment, depletedAlpha);
        }

        private IEnumerator FlashSegment(int index, float targetAlpha)
        {
            // Swap to flash material at full alpha
            if (flashMaterial != null)
                segments[index].material = flashMaterial;
            SetSegmentAlpha(index, 1f);

            // Hold flash
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Revert to original material and set depleted alpha
            if (_originalMaterials[index] != null)
                segments[index].material = _originalMaterials[index];
            SetSegmentAlpha(index, targetAlpha);

            _flashCoroutines[index] = null;
        }
    }
}
