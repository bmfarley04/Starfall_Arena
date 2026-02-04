using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundRoot : MonoBehaviour
{
    public Camera connectedCamera;
    void Awake()
    {
        List<ParallaxLayer> layers = new List<ParallaxLayer>(GetComponentsInChildren<ParallaxLayer>());
        foreach (ParallaxLayer layer in layers)
        {
            if (connectedCamera != null)
            {
                layer.SetMainCamera(connectedCamera);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
