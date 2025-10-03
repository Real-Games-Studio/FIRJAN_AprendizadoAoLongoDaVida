using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;
using RealGames;
public class ScreenCanvasController : MonoBehaviour
{
    public UnityEngine.UI.Image inactiveFeedback;
    public static ScreenCanvasController instance;
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
        if (inactiveFeedback != null) inactiveFeedback.fillAmount = 0f;
        ScreenManager.SetCallScreen(inicialScreen);
    }
    // Update is called once per frame
    void Update()
    {
        // If any click or touch, reset inactivity
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            ResetInactivity();
        }

        if (currentScreen != inicialScreen)
        {
            inactiveTimer += Time.deltaTime * 1;

            if (inactiveTimer >= GameDataLoader.instance.loadedConfig.maxInactiveTime)
            {
                ResetGame();
            }
            // update the visual feedback (fill from 0 to 1)
            if (inactiveFeedback != null && GameDataLoader.instance != null && GameDataLoader.instance.loadedConfig != null)
            {
                float max = GameDataLoader.instance.loadedConfig.maxInactiveTime;
                if (max > 0f)
                    inactiveFeedback.fillAmount = Mathf.Clamp01(inactiveTimer / max);
                else
                    inactiveFeedback.fillAmount = 0f;
            }
        }
        else
        {
            inactiveTimer = 0;
            if (inactiveFeedback != null) inactiveFeedback.fillAmount = 0f;
        }
    }

    // Helper to reset the inactivity timer and UI
    private void ResetInactivity()
    {
        inactiveTimer = 0f;
        if (inactiveFeedback != null) inactiveFeedback.fillAmount = 0f;
    }
    public void ResetGame()
    {
        Debug.Log("Tempo de inatividade extrapolado!");
        inactiveTimer = 0;
        if (inactiveFeedback != null) inactiveFeedback.fillAmount = 0f;
        ScreenManager.CallScreen(inicialScreen);
    }
    public void OnScreenCall(string name)
    {
        inactiveTimer = 0;
        previusScreen = currentScreen;
        currentScreen = name;
        if (inactiveFeedback != null) inactiveFeedback.fillAmount = 0f;
    }
    public void NFCInputHandler(string obj)
    {
        inactiveTimer = 0;
        if (inactiveFeedback != null) inactiveFeedback.fillAmount = 0f;
    }

    public void CallAnyScreenByName(string name)
    {
        ScreenManager.CallScreen(name);
    }
}
