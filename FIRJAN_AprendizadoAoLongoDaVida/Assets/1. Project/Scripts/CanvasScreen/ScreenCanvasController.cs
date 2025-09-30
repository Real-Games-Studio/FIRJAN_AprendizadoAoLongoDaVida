using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;
using RealGames;
public class ScreenCanvasController : MonoBehaviour
{
    public static ScreenCanvasController instance;
    public AppConfig appConfig;

    public string previusScreen;
    public string currentScreen;
    public string inicialScreen;
    public float inactiveTimer = 0;

    public CanvasGroup DEBUG_CANVAS;
    public TMP_Text timeOut;

    private void OnEnable()
    {
        // Registra o m�todo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen += OnScreenCall;

    }
    private void OnDisable()
    {
        // Remove o m�todo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen -= OnScreenCall;

    }
    // Start is called before the first frame update
    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        instance = this;
        ScreenManager.SetCallScreen(inicialScreen);
    }
    // Update is called once per frame
    void Update()
    {
        if (currentScreen != inicialScreen)
        {
            inactiveTimer += Time.deltaTime * 1;

            if (inactiveTimer >= appConfig.maxInactiveTime)
            {
                ResetGame();
            }
        }
        else
        {
            inactiveTimer = 0;
        }
    }
    public void ResetGame()
    {
        Debug.Log("Tempo de inatividade extrapolado!");
        inactiveTimer = 0;
        ScreenManager.CallScreen(inicialScreen);
    }
    public void OnScreenCall(string name)
    {
        inactiveTimer = 0;
        previusScreen = currentScreen;
        currentScreen = name;
    }
    public void NFCInputHandler(string obj)
    {
        inactiveTimer = 0;
    }

    public void CallAnyScreenByName(string name)
    {
        ScreenManager.CallScreen(name);
    }
}
