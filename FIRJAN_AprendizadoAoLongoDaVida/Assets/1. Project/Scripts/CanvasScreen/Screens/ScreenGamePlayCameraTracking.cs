using System;
using TMPro;
using UnityEngine;

public class ScreenGamePlayCameraTracking : CanvasScreen
{
    [SerializeField] private ARTrackingImageController ARTrackingImageController;
    [SerializeField] private TMP_Text CameraTrackingText;

    [SerializeField] private TMP_Text LastFoundImage;
    [SerializeField] private TMP_Text currentFoundImage;

    [SerializeField] private AudioSource findSound;

    private void Awake()
    {
        EnsureController();
    }

    public override void OnValidate()
    {
        base.OnValidate();
        EnsureController();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        EnsureController();
        Subscribe();
    }

    public override void OnDisable()
    {
        Unsubscribe();
        base.OnDisable();
    }

    public override void TurnOn()
    {
        base.TurnOn();
        UpdateLastFoundImageLabel();
        UpdateCameraTrackingText();
    }

    private void Subscribe()
    {
        if (ARTrackingImageController == null)
        {
            return;
        }

        ARTrackingImageController.NextTargetChanged += HandleNextTargetChanged;
        ARTrackingImageController.SequenceReset += HandleSequenceReset;
        ARTrackingImageController.QuestionChanged += HandleQuestionChanged;
        ARTrackingImageController.LastImageNameChanged += HandleLastImageNameChanged;
        ARTrackingImageController.ImageDetected += HandleImageDetected;
    }

    private void Unsubscribe()
    {
        if (ARTrackingImageController == null)
        {
            return;
        }

        ARTrackingImageController.NextTargetChanged -= HandleNextTargetChanged;
        ARTrackingImageController.SequenceReset -= HandleSequenceReset;
        ARTrackingImageController.QuestionChanged -= HandleQuestionChanged;
        ARTrackingImageController.LastImageNameChanged -= HandleLastImageNameChanged;
        ARTrackingImageController.ImageDetected -= HandleImageDetected;
    }

    private void HandleNextTargetChanged(int _)
    {
        UpdateLastFoundImageLabel();
        UpdateCameraTrackingText();
    }

    private void HandleSequenceReset()
    {
        UpdateLastFoundImageLabel();
        UpdateCameraTrackingText();
    }

    private void HandleQuestionChanged(ARTrackingImageController.QuestionEntry question)
    {
        if (question == null)
        {
            return;
        }

        if (!IsOn())
        {
            return;
        }

        // Switch to the quiz screen as soon as a valid question is ready.
        CallNextScreen();
    }

    private void HandleLastImageNameChanged(string imageName)
    {
        UpdateLastFoundImageLabel(imageName);
    }

    private void HandleImageDetected(int _)
    {
        if (findSound == null)
        {
            return;
        }

        findSound.Play();
    }

    private void UpdateCameraTrackingText()
    {
        if (CameraTrackingText == null)
        {
            return;
        }

        if (ARTrackingImageController == null)
        {
            CameraTrackingText.text = string.Empty;
            return;
        }

        if (!ARTrackingImageController.GameStarted)
        {
            CameraTrackingText.text = ResolveMessage("Posicione-se em qualquer casa", "Stand on any tile");
            return;
        }

        if (!ARTrackingImageController.HasAnsweredAnyQuestion)
        {
            CameraTrackingText.text = ResolveMessage("Aguarde. Responda \u00E0 pergunta antes de seguir.", "Hold on. Answer the question before moving forward.");
            return;
        }

        var nextId = ARTrackingImageController.ExpectedNextImageId;
        if (nextId >= 0)
        {
            var houseIndex = ARTrackingImageController.GetBoardIndexById(nextId);
            if (houseIndex >= 0)
            {
                var displayIndex = houseIndex + 1;
                var template = ResolveMessage("V\u00E1 para a casa {0}", "Go to tile {0}");
                CameraTrackingText.text = string.Format(template, displayIndex);
            }
            else
            {
                CameraTrackingText.text = ResolveMessage("Procure a pr\u00F3xima casa indicada.", "Look for the indicated next tile.");
            }
        }
        else
        {
            CameraTrackingText.text = ResolveMessage("Voc\u00EA concluiu o percurso!", "You have finished the route!");
        }
    }

    private void UpdateLastFoundImageLabel()
    {
        var imageName = ARTrackingImageController != null ? ARTrackingImageController.LastFoundImageName : string.Empty;
        UpdateLastFoundImageLabel(imageName);
    }

    private void UpdateLastFoundImageLabel(string imageName)
    {
        if (LastFoundImage != null)
        {
            LastFoundImage.text = string.IsNullOrEmpty(imageName) ? string.Empty : imageName;
        }

        UpdateCurrentFoundImageLabel(imageName);
    }

    private void UpdateCurrentFoundImageLabel(string imageName)
    {
        if (currentFoundImage == null)
        {
            return;
        }

        currentFoundImage.text = string.IsNullOrEmpty(imageName) ? string.Empty : imageName;
    }

    private void EnsureController()
    {
        if (ARTrackingImageController != null)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        ARTrackingImageController = FindFirstObjectByType<ARTrackingImageController>();
#else
        ARTrackingImageController = FindObjectOfType<ARTrackingImageController>();
#endif
    }

    private bool IsPortugueseLanguage()
    {
        var fallback = LocalizationManager.instance != null ? LocalizationManager.instance.defaultLang : "pt";
        var lang = PlayerPrefs.GetString("lang", fallback);
        return !string.IsNullOrEmpty(lang) && lang.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveMessage(string portuguese, string english)
    {
        return IsPortugueseLanguage() ? portuguese : english;
    }
}
