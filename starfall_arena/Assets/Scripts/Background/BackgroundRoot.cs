using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundRoot : MonoBehaviour
{
    public Camera connectedCamera;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        List<ParallaxLayer> layers = new List<ParallaxLayer>(GetComponentsInChildren<ParallaxLayer>());
        foreach (ParallaxLayer layer in layers)
        {
            layer.SetMainCamera(connectedCamera);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
