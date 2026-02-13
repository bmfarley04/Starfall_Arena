using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplitScreenManager : MonoBehaviour
{
    public Boolean verticalSplit = true;
    public CinemachineCamera player1Cinemachine;
    public Camera player1Camera;
    public GameObject player1;
    public CinemachineCamera player2Cinemachine;
    public Camera player2Camera;
    public GameObject player2;

    private PlayerInput player1Input;
    private PlayerInput player2Input;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player1Input = player1.GetComponent<PlayerInput>();
        player2Input = player2.GetComponent<PlayerInput>();
        player1Input.camera = player1Camera;
        player2Input.camera = player2Camera;
        player1Cinemachine.Follow = player1.transform;
        player1Cinemachine.LookAt = player1.transform;
        player2Cinemachine.Follow = player2.transform;
        player2Cinemachine.LookAt = player2.transform;


    }

    // Update is called once per frame
    void Update()
    {
        if (verticalSplit)
        {
            player1Camera.rect = new Rect(0, 0, 0.5f, 1);
            player2Camera.rect = new Rect(0.5f, 0, 0.5f, 1);
        }
        else
        {
            player1Camera.rect = new Rect(0, 0.5f, 1, 0.5f);
            player2Camera.rect = new Rect(0, 0, 1, 0.5f);
        }
    }
}
