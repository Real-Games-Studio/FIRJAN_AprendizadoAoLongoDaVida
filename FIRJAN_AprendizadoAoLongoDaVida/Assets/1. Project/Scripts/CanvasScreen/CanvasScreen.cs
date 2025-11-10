using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using Unity.Mathematics;
using UnityEngine;


[RequireComponent(typeof(CanvasGroup))]
public class CanvasScreen: MonoBehaviour
{
    [System.Serializable]
    public class LocalizationText
    {
        public string key;
        public TMP_Text textField;
    }

    [System.Serializable]
    public class ScreenData
    {
        [Tooltip("Toda tela deve ter um nome que possa ser chamada")]
        public string screenName;
        public string previusScreenName;
        public string nextScreenName;
        [Header("- editor -")]
        public bool editor_turnOn = false;
        public bool editor_turnOff = false;
    }
    [Tooltip("Toda tela deve ter uma base de canvas group")]
    public CanvasGroup canvasgroup;
    [SerializeField] protected ScreenData data;
    public List<LocalizationText> localizationTexts = new List<LocalizationText>();
    private Coroutine localizationRoutine;
    private bool localizationSubscribed;
    public virtual void OnValidate()
    {
        if (canvasgroup == null)
        {
            canvasgroup = GetComponent<CanvasGroup>();
        }

        if (data.editor_turnOff)
        {
            data.editor_turnOff = false;

            if (canvasgroup != null)
            {
                TurnOff();
            }
            else
            {
                Debug.LogError("CanvasGroup está nulo ao tentar desativar no OnValidate.", this);
            }
        }

        if (data.editor_turnOn)
        {
            data.editor_turnOn = false;

            foreach (var screen in FindObjectsByType<CanvasScreen>(FindObjectsSortMode.None))
            {
                if (screen != this && screen.canvasgroup != null)
                {
                    screen.TurnOff();
                }
            }

            if (canvasgroup != null)
            {
                TurnOn();
            }
            else
            {
                Debug.LogError("CanvasGroup está nulo ao tentar ativar no OnValidate.", this);
            }
        }
    }

    public virtual void OnEnable()
    {
        if (canvasgroup == null)
        {
            canvasgroup = GetComponent<CanvasGroup>();
        }
        // Registra o m騁odo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen += CallScreenListner;
        StartLocalizationListener();
    }
    public virtual void OnDisable()
    {
        // Remove o m騁odo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen -= CallScreenListner;
        StopLocalizationListener();
    }

    private void StartLocalizationListener()
    {
        if (localizationRoutine != null)
        {
            return;
        }

        localizationRoutine = StartCoroutine(WaitForLocalizationAndApply());
    }

    private void StopLocalizationListener()
    {
        if (localizationRoutine != null)
        {
            StopCoroutine(localizationRoutine);
            localizationRoutine = null;
        }

        UnsubscribeLocalization();
    }

    private void SubscribeLocalization()
    {
        if (LocalizationManager.instance == null || localizationSubscribed)
        {
            return;
        }

        LocalizationManager.instance.OnLanguageChanged += HandleLocalizationChanged;
        localizationSubscribed = true;
    }

    private void UnsubscribeLocalization()
    {
        if (!localizationSubscribed)
        {
            return;
        }

        if (LocalizationManager.instance != null)
        {
            LocalizationManager.instance.OnLanguageChanged -= HandleLocalizationChanged;
        }

        localizationSubscribed = false;
    }

    private void HandleLocalizationChanged()
    {
        ApplyLocalizationChain();
    }

    private void ApplyLocalizationChain()
    {
        if (LocalizationManager.instance == null)
        {
            return;
        }

        ApplyLocalizationTexts();
        OnLocalizationApplied();
    }

    private System.Collections.IEnumerator WaitForLocalizationAndApply()
    {
        while (LocalizationManager.instance == null)
        {
            yield return null;
        }

        localizationRoutine = null;
        ApplyLocalizationChain();
        SubscribeLocalization();
    }

    protected virtual void ApplyLocalizationTexts()
    {
        if (localizationTexts == null) return;
        if (LocalizationManager.instance == null) return;

        foreach (var lt in localizationTexts)
        {
            if (lt == null) continue;
            if (lt.textField == null) continue;
            if (string.IsNullOrEmpty(lt.key)) continue;

            try
            {
                lt.textField.text = LocalizationManager.instance.Get(lt.key);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to apply localization for key: " + lt.key + " - " + ex.Message, this);
            }
        }
    }

    protected virtual void OnLocalizationApplied()
    {
    }

    protected void SetLocalizedText(TMP_Text textField, string key)
    {
        if (textField == null) return;
        if (LocalizationManager.instance == null) return;
        if (string.IsNullOrEmpty(key)) return;

        try
        {
            textField.text = LocalizationManager.instance.Get(key);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to set localized text for key: " + key + " - " + ex.Message, this);
        }
    }

    public virtual void CallScreenListner(string screenName)
    {
        if (screenName == this.data.screenName)
        {
            TurnOn();
        }
        else
        {
            TurnOff();
        }
    }
    public virtual void TurnOn()
    {
        canvasgroup.alpha = 1;
        canvasgroup.interactable = true;
        canvasgroup.blocksRaycasts = true;
    }
    public virtual void TurnOff()
    {
        canvasgroup.alpha = 0;
        canvasgroup.interactable = false;
        canvasgroup.blocksRaycasts = false;
    }
    public bool IsOn()
    {
        return canvasgroup.blocksRaycasts;
    }

    public virtual void CallNextScreen()
    {
        ScreenManager.CallScreen(data.nextScreenName);
    }
    public virtual void CallPreviusScreen()
    {
        ScreenManager.CallScreen(data.previusScreenName);
    }

    public virtual void CallScreenByName(string _name)
    {
        ScreenManager.CallScreen(_name);
    }
}
