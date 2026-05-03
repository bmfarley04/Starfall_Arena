using System;
using UnityEngine;

[DisallowMultipleComponent]
public class InvasionTrackedEnemyLifecycle3D : MonoBehaviour
{
    private Enemy3D _enemy;
    private bool _hasNotifiedEnded;

    public event Action<Enemy3D> TrackingEnded;

    public Enemy3D Enemy
    {
        get
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy3D>();
            }

            return _enemy;
        }
    }

    public void ResetTrackingState()
    {
        _hasNotifiedEnded = false;
        _enemy ??= GetComponent<Enemy3D>();
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy3D>();
    }

    private void OnDestroy()
    {
        if (_hasNotifiedEnded)
        {
            return;
        }

        _hasNotifiedEnded = true;
        TrackingEnded?.Invoke(Enemy);
    }
}
