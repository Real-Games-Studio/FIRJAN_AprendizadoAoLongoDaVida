using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

public static class ScreenManager
{
    public static Action OnReset;
    public static Action<string> CallScreen;
    public static Func<string, bool> ScreenChangeGuard;
    public static string currentScreenName;

    public static void SetCallScreen(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (ScreenChangeGuard != null && !ScreenChangeGuard.Invoke(name))
        {
            return;
        }

        CallScreen?.Invoke(name);
        currentScreenName = name;
    }

    public static void TurnOnCanvasGroup(CanvasGroup c)
    {
        c.alpha = 1;
        c.interactable = true;
        c.blocksRaycasts = true;
    }

    public static void TurnOffCanvasGroup(CanvasGroup c)
    {
        c.alpha = 0;
        c.interactable = false;
        c.blocksRaycasts = false;
    }
}
