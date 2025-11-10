using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class QuizAwnserFeedback : MonoBehaviour
{
    [Header("Referências Visuais")]
    public Image backgroundToColor;
    public TMP_Text MainText; // texto para correto ou incorreto
    [FormerlySerializedAs("pointsText")]
    public TMP_Text PointsText; // texto para mostrar quantas casas deve se mover (ex.: AVANCE<BR>3<BR>CASAS)
    public CanvasGroup canvasGroup; // deve se ativar e desativar conforme o feedback

    [Header("Imagens de Feedback")]
    [SerializeField]
    private GameObject correctFeedbackImage;
    [SerializeField]
    private GameObject wrongFeedbackImage;

    [Header("Cores")]
    public Color CorrectColor = Color.green;
    public Color WrongColor = Color.red;
    public Color InactiveColor = Color.gray;

    [Header("Configuração")]
    [Min(0f)]
    [Tooltip("Tempo (em segundos) que o feedback permanece visível antes de desaparecer automaticamente.")]
    public float displayDuration = 2.5f;

    public float DisplayDuration => Mathf.Max(0f, displayDuration);

    private void Awake()
    {
        HideImmediate();
    }

    public void ShowFeedback(ARTrackingImageController.QuizFeedback feedback, float elapsedSeconds)
    {
        ApplyVisuals(feedback, elapsedSeconds);
        SetCanvasGroup(true);
    }

    public void HideImmediate()
    {
        SetCanvasGroup(false);

        SetStateImages(false, false);

        if (MainText != null)
        {
            MainText.text = string.Empty;
        }

        if (PointsText != null)
        {
            PointsText.text = string.Empty;
        }
    }

    private void ApplyVisuals(ARTrackingImageController.QuizFeedback feedback, float elapsedSeconds)
    {
        var targetColor = ResolveColor(feedback);

        if (backgroundToColor != null)
        {
            backgroundToColor.color = targetColor;
        }

        if (MainText != null)
        {
            MainText.text = ResolveMainText(feedback, elapsedSeconds);
        }

        if (PointsText != null)
        {
            PointsText.text = ResolvePointsText(feedback);
        }

        UpdateStateImages(feedback);
    }

    private void SetCanvasGroup(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    private Color ResolveColor(ARTrackingImageController.QuizFeedback feedback)
    {
        return feedback switch
        {
            ARTrackingImageController.QuizFeedback.Error => WrongColor,
            ARTrackingImageController.QuizFeedback.Inactivity => InactiveColor,
            _ => CorrectColor,
        };
    }

    private string ResolveMainText(ARTrackingImageController.QuizFeedback feedback, float elapsedSeconds)
    {
        return feedback switch
        {
            ARTrackingImageController.QuizFeedback.Error => "Resposta incorreta!",
            ARTrackingImageController.QuizFeedback.Inactivity => "Tempo esgotado!",
            ARTrackingImageController.QuizFeedback.CorrectFast => $"Acerto em {elapsedSeconds:0.0}s!",
            ARTrackingImageController.QuizFeedback.CorrectMedium => $"Acerto em {elapsedSeconds:0.0}s!",
            ARTrackingImageController.QuizFeedback.CorrectSlow => $"Acerto no limite ({elapsedSeconds:0.0}s)!",
            _ => string.Empty,
        };
    }

    private string ResolvePointsText(ARTrackingImageController.QuizFeedback feedback)
    {
        var houses = (int)feedback;
        if (houses == 0)
        {
            return string.Empty;
        }

        var absHouses = Mathf.Abs(houses);
        var prefix = ResolveMovementPrefix(houses > 0);
        var unitLabel = ResolveUnitLabel(absHouses);
        return $"{prefix}<BR>{absHouses}<BR>{unitLabel}";
    }

    private void UpdateStateImages(ARTrackingImageController.QuizFeedback feedback)
    {
        var showCorrect = IsCorrectFeedback(feedback);
        var showWrong = feedback == ARTrackingImageController.QuizFeedback.Error;
        SetStateImages(showCorrect, showWrong);
    }

    private void SetStateImages(bool correctActive, bool wrongActive)
    {
        if (correctFeedbackImage != null)
        {
            correctFeedbackImage.SetActive(correctActive);
        }

        if (wrongFeedbackImage != null)
        {
            wrongFeedbackImage.SetActive(wrongActive);
        }
    }

    private static bool IsCorrectFeedback(ARTrackingImageController.QuizFeedback feedback)
    {
        return feedback == ARTrackingImageController.QuizFeedback.CorrectFast
               || feedback == ARTrackingImageController.QuizFeedback.CorrectMedium
               || feedback == ARTrackingImageController.QuizFeedback.CorrectSlow;
    }

    private string ResolveMovementPrefix(bool forward)
    {
        if (IsPortugueseLanguage())
        {
            return forward ? "AVANCE" : "VOLTE";
        }

        return forward ? "ADVANCE" : "GO BACK";
    }

    private string ResolveUnitLabel(int amount)
    {
        var plural = amount > 1;
        if (IsPortugueseLanguage())
        {
            return plural ? "CASAS" : "CASA";
        }

        return plural ? "SPACES" : "SPACE";
    }

    private bool IsPortugueseLanguage()
    {
        var fallbackLang = LocalizationManager.instance != null && !string.IsNullOrEmpty(LocalizationManager.instance.defaultLang)
            ? LocalizationManager.instance.defaultLang
            : "pt";

        var currentLang = PlayerPrefs.GetString("lang", fallbackLang);
        return !string.IsNullOrEmpty(currentLang) && currentLang.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    }
}
