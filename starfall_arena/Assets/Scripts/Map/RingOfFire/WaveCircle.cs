using UnityEngine;

[System.Serializable]
public class WaveCircle : WaveShape
{
    public Vector2 centerPoint;
    public float radius;

    public WaveCircle(Vector2 centerPoint, float radius)
    {
        this.centerPoint = centerPoint;
        this.radius = radius;
    }

    public override Vector2 GetRandomPoint()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(0f, radius);
        return centerPoint + new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
    }
}