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

    // Returns the two corners forming the Left edge
    public (Vector2 start, Vector2 end) GetLeftEdge()
    {
        float x = centerPoint.x - width / 2;
        Vector2 topLeft = new Vector2(x, centerPoint.y + length / 2);
        Vector2 bottomLeft = new Vector2(x, centerPoint.y - length / 2);
        return (bottomLeft, topLeft);
    }

    // Returns the two corners forming the Right edge
    public (Vector2 start, Vector2 end) GetRightEdge()
    {
        float x = centerPoint.x + width / 2;
        Vector2 topRight = new Vector2(x, centerPoint.y + length / 2);
        Vector2 bottomRight = new Vector2(x, centerPoint.y - length / 2);
        return (bottomRight, topRight);
    }

    // Returns the two corners forming the Top edge
    public (Vector2 start, Vector2 end) GetTopEdge()
    {
        float y = centerPoint.y + length / 2;
        Vector2 topLeft = new Vector2(centerPoint.x - width / 2, y);
        Vector2 topRight = new Vector2(centerPoint.x + width / 2, y);
        return (topLeft, topRight);
    }

    // Returns the two corners forming the Bottom edge
    public (Vector2 start, Vector2 end) GetBottomEdge()
    {
        float y = centerPoint.y - length / 2;
        Vector2 bottomLeft = new Vector2(centerPoint.x - width / 2, y);
        Vector2 bottomRight = new Vector2(centerPoint.x + width / 2, y);
        return (bottomLeft, bottomRight);
    }
}