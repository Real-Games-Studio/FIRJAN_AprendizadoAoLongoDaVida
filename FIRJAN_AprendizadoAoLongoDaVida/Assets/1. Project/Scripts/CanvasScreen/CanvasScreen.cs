using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Unity.Mathematics;
using UnityEngine;


[RequireComponent(typeof(CanvasGroup))]
public class CanvasScreen: MonoBehaviour
{
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
    public ScreenData screenData;
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

            foreach (var screen in FindObjectsOfType<CanvasScreen>())
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
    }
    public virtual void OnDisable()
    {
        // Remove o m騁odo CallScreenListner como ouvinte do evento CallScreen
        ScreenManager.CallScreen -= CallScreenListner;
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