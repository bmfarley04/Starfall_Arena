using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    // Optional: Offset if your sprite isn't centered
    public Vector2 offset = Vector2.zero;

    void Start()
    {
        // Hide the system mouse pointer
        Cursor.visible = false; 
    }

    void LateUpdate()
    {
        // Move this UI element to the mouse position
        transform.position = (Vector2)Input.mousePosition + offset;
    }
}