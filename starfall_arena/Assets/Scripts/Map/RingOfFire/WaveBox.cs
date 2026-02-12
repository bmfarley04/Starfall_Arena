using UnityEngine;

[System.Serializable]
public class WaveBox
{
    public Vector2 centerPoint;
    public float width;
    public float length;

    public WaveBox(Vector2 centerPoint, float width, float length)
    {
        this.centerPoint = centerPoint;
        this.width = width;
        this.length = length;
    }

    public Vector2 GetRandomPoint()
    {
        float x = Random.Range(centerPoint.x - width / 2, centerPoint.x + width / 2);
        float y = Random.Range(centerPoint.y - length / 2, centerPoint.y + length / 2);
        return new Vector2(x,y);
    }

    public Vector2 GetLeftEdge()
    {
        float x = centerPoint.x - width / 2;
        float y = centerPoint.y;
        return new Vector2(x, y);
    }
    public Vector2 GetRightEdge()
    {
        float x = centerPoint.x + width / 2;
        float y = centerPoint.y;
        return new Vector2(x, y);
    }
    public Vector2 GetTopEdge()
    {
        float x = centerPoint.x;
        float y = centerPoint.y + length / 2;
        return new Vector2(x, y);
    }
    public Vector2 GetBottomEdge()
    {
        float x = centerPoint.x;
        float y = centerPoint.y - length / 2;
        return new Vector2(x, y);
    }
}