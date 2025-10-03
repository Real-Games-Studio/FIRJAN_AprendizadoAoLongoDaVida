using TMPro;
using UnityEngine;

public class ScreenGamePlayCameraTracking : CanvasScreen
{
    [SerializeField] private ARTrackingImageController ARTrackingImageController;
    [SerializeField] private TMP_Text CameraTrackingText;

    [SerializeField] private TMP_Text LastFoundImage;

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
            CameraTrackingText.text = "Posicione-se em qualquer casa";
            return;
        }

        if (!ARTrackingImageController.HasAnsweredAnyQuestion)
        {
            CameraTrackingText.text = "Aguarde. Responda à pergunta antes de seguir.";
            return;
        }

        var nextId = ARTrackingImageController.ExpectedNextImageId;
        if (nextId >= 0)
        {
            var houseIndex = ARTrackingImageController.GetBoardIndexById(nextId);
            if (houseIndex >= 0)
            {
                CameraTrackingText.text = $"Vá para a casa {houseIndex}";
            }
            else
            {
                CameraTrackingText.text = "Procure a próxima casa indicada.";
            }
        }
        else
        {
            CameraTrackingText.text = "Você concluiu o percurso!";
        }
    }

    private void UpdateLastFoundImageLabel()
    {
        UpdateLastFoundImageLabel(ARTrackingImageController != null ? ARTrackingImageController.LastFoundImageName : string.Empty);
    }

    private void UpdateLastFoundImageLabel(string imageName)
    {
        if (LastFoundImage == null)
        {
            return;
        }

        LastFoundImage.text = string.IsNullOrEmpty(imageName) ? string.Empty : imageName;
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
}
