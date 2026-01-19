using UnityEngine;

public class BackgroundCameraScript : MonoBehaviour
{
    public float moveSpeed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //move camera to the right constantly
        transform.position += new Vector3(moveSpeed * Time.deltaTime, 0, 0);
    }
}
