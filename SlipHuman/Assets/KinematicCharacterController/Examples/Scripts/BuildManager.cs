using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildManager : MonoBehaviour
{
    public GameObject WebGLCanvas;
    public GameObject WarningPanel;

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLCanvas.SetActive(true);
#endif
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.H))
        {
            WarningPanel.SetActive(!WarningPanel.activeSelf);
        }
#endif
    }
}
