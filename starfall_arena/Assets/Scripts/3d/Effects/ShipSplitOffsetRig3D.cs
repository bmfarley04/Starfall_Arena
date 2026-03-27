using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShipSplitOffsetRig3D : MonoBehaviour
{
    [System.Serializable]
    private class SplitOffsetGroup3D
    {
        [Tooltip("Local-space offset added to every piece in this group while the split state is active.")]
        public Vector3 offset;
        [Tooltip("Pieces that should all receive this same additive local-space offset.")]
        public List<Transform> pieces = new();
    }

    private struct PieceLocalPositionState
    {
        public Transform piece;
        public Vector3 baseLocalPosition;
    }

    [Tooltip("Grouped additive offsets applied while the split state is active.")]
    [SerializeField] private List<SplitOffsetGroup3D> offsetGroups = new();
    [Tooltip("When enabled, the offset state starts active so the authored split can be previewed in-editor or test scenes.")]
    [SerializeField] private bool activateOnStart;

    private readonly List<List<PieceLocalPositionState>> _cachedPieceStates = new();
    private bool _splitStateActive;

    private void Awake()
    {
        CachePieceStates();
        ApplyState();
    }

    private void Start()
    {
        SetSplitStateActive(activateOnStart);
    }

    private void OnDisable()
    {
        if (_cachedPieceStates.Count == 0)
        {
            return;
        }

        bool previousState = _splitStateActive;
        _splitStateActive = false;
        ApplyState();
        _splitStateActive = previousState;
    }

    public void SetSplitStateActive(bool isActive)
    {
        _splitStateActive = isActive;
        ApplyState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CachePieceStates();
            ApplyState();
        }
    }

    private void CachePieceStates()
    {
        _cachedPieceStates.Clear();

        for (int groupIndex = 0; groupIndex < offsetGroups.Count; groupIndex++)
        {
            SplitOffsetGroup3D group = offsetGroups[groupIndex];
            List<PieceLocalPositionState> cachedGroup = new();

            if (group == null || group.pieces == null)
            {
                _cachedPieceStates.Add(cachedGroup);
                continue;
            }

            for (int pieceIndex = 0; pieceIndex < group.pieces.Count; pieceIndex++)
            {
                Transform piece = group.pieces[pieceIndex];
                if (piece == null)
                {
                    continue;
                }

                cachedGroup.Add(new PieceLocalPositionState
                {
                    piece = piece,
                    baseLocalPosition = piece.localPosition
                });
            }

            _cachedPieceStates.Add(cachedGroup);
        }
    }

    private void ApplyState()
    {
        int groupCount = Mathf.Min(offsetGroups.Count, _cachedPieceStates.Count);
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            SplitOffsetGroup3D group = offsetGroups[groupIndex];
            List<PieceLocalPositionState> cachedGroup = _cachedPieceStates[groupIndex];
            Vector3 activeOffset = _splitStateActive && group != null ? group.offset : Vector3.zero;

            for (int pieceIndex = 0; pieceIndex < cachedGroup.Count; pieceIndex++)
            {
                PieceLocalPositionState pieceState = cachedGroup[pieceIndex];
                if (pieceState.piece == null)
                {
                    continue;
                }

                pieceState.piece.localPosition = pieceState.baseLocalPosition + activeOffset;
            }
        }
    }
}
