using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;
using RealGames;
using UnityEngine.SceneManagement;
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
    private bool isScreenLocked;
    private string lockedScreenName;

    private void OnEnable()
    {
        // Registra o m�todo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen += OnScreenCall;
        ScreenManager.ScreenChangeGuard = ShouldAllowScreenChange;
    }
    private void OnDisable()
    {
        // Remove o m�todo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen -= OnScreenCall;
        if (ScreenManager.ScreenChangeGuard == ShouldAllowScreenChange)
        {
            ScreenManager.ScreenChangeGuard = null;
        }
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
        if (isScreenLocked)
        {
            ResetInactivity();
            return;
        }

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
        if (isScreenLocked)
        {
            Debug.Log($"ResetGame ignorado: tela bloqueada em {lockedScreenName}.");
            return;
        }
        Debug.Log("Tempo de inatividade extrapolado!");
        ResetInactivity();
        ScreenManager.SetCallScreen(inicialScreen);
    }
    public void ReloadGame()
    {
        Debug.Log("Tempo de inatividade extrapolado!");
        UnlockScreen();
        SceneManager.LoadScene(0);
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
        ScreenManager.SetCallScreen(name);
    }

    public void LockScreen(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        lockedScreenName = name;
        isScreenLocked = true;
        ResetInactivity();

        if (!string.Equals(currentScreen, name, StringComparison.OrdinalIgnoreCase))
        {
            ScreenManager.SetCallScreen(name);
        }
    }

    public void UnlockScreen()
    {
        if (!isScreenLocked)
        {
            return;
        }

        isScreenLocked = false;
        lockedScreenName = string.Empty;
        ResetInactivity();
    }

    private bool ShouldAllowScreenChange(string targetScreen)
    {
        if (!isScreenLocked)
        {
            return true;
        }

        var sameScreen = string.Equals(targetScreen, lockedScreenName, StringComparison.OrdinalIgnoreCase);
        if (!sameScreen)
        {
            Debug.Log($"Troca de tela para \"{targetScreen}\" ignorada. Tela bloqueada em \"{lockedScreenName}\".");
        }
        return sameScreen;
    }
}
