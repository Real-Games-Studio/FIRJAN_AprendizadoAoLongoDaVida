using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenGamePlayQuiz : CanvasScreen
{
    [SerializeField] private Image QuestionImage;
    [SerializeField] private Image TimerFeedbackImage;
    [SerializeField] private TMP_Text timerFeedbackText; // 30 segundos. mas essa informacao poderia vir no do appconfig.json
    [SerializeField] private ARTrackingImageController ARTrackingImageController;
    [SerializeField] private TMP_Text QuestionText;
    [SerializeField] private TMP_Text Answer1Text;
    [SerializeField] private TMP_Text Answer2Text;

    [SerializeField] private Button Answer1Button;
    [SerializeField] private Button Answer2Button;

    [SerializeField] [Tooltip("Tempo máximo em segundos para responder a uma pergunta antes de marcar inatividade.")]
    private float maxQuestionTime = 30f;

    [SerializeField] [Tooltip("Define se, ao finalizar uma resposta, a próxima tela configurada deve ser chamada automaticamente.")]
    private bool autoAdvanceAfterAnswer = true;

    private ARTrackingImageController.QuestionEntry activeQuestion;
    private float questionStartTime;
    private bool awaitingAnswer;
    private Coroutine feedbackRoutine;

    public QuizAwnserFeedback QuizAwnserFeedback;

    private void Awake()
    {
        EnsureController();
        ConfigureButtons();
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
        RefreshFromCurrentQuestion();
    }

    public override void OnDisable()
    {
        CancelFeedbackRoutine();
        Unsubscribe();
        awaitingAnswer = false;
        base.OnDisable();
    }

    private void Update()
    {
        if (!awaitingAnswer)
        {
            return;
        }

        var elapsed = Time.time - questionStartTime;
        var remaining = Mathf.Max(0f, maxQuestionTime - elapsed);
        UpdateTimerUI(remaining);

        if (elapsed >= maxQuestionTime)
        {
            ResolveQuiz(ARTrackingImageController.QuizFeedback.Inactivity, elapsed);
        }
    }

    public override void TurnOn()
    {
        base.TurnOn();
        RefreshFromCurrentQuestion();
    }

    private void RefreshFromCurrentQuestion()
    {
        if (ARTrackingImageController == null)
        {
            SetQuestionUI(null);
            return;
        }

        var question = ARTrackingImageController.CurrentQuestion ?? ARTrackingImageController.GetQuestionById(ARTrackingImageController.currentID);
        SetQuestionUI(question);
    }

    private void Subscribe()
    {
        if (ARTrackingImageController == null)
        {
            return;
        }

        ARTrackingImageController.QuestionChanged += HandleQuestionChanged;
        ARTrackingImageController.SequenceReset += HandleSequenceReset;
        ARTrackingImageController.LastImageSpriteChanged += HandleLastImageSpriteChanged;
    }

    private void Unsubscribe()
    {
        if (ARTrackingImageController == null)
        {
            return;
        }

        ARTrackingImageController.QuestionChanged -= HandleQuestionChanged;
        ARTrackingImageController.SequenceReset -= HandleSequenceReset;
        ARTrackingImageController.LastImageSpriteChanged -= HandleLastImageSpriteChanged;
    }

    private void HandleQuestionChanged(ARTrackingImageController.QuestionEntry question)
    {
        SetQuestionUI(question);
    }

    private void HandleSequenceReset()
    {
        SetQuestionUI(null);
    }

    private void HandleLastImageSpriteChanged(Sprite sprite)
    {
        if (QuestionImage == null)
        {
            return;
        }

        QuestionImage.enabled = sprite != null;
        QuestionImage.sprite = sprite;
    }

    private void SetQuestionUI(ARTrackingImageController.QuestionEntry question)
    {
        CancelFeedbackRoutine();
        activeQuestion = question;
        awaitingAnswer = question != null;
        questionStartTime = Time.time;
        UpdateTimerUI(question != null ? maxQuestionTime : 0f);
        SetButtonsInteractable(awaitingAnswer);
        HandleLastImageSpriteChanged(ARTrackingImageController != null ? ARTrackingImageController.LastFoundImageSprite : null);

        if (QuestionText != null)
        {
            QuestionText.text = question?.pergunta ?? string.Empty;
        }

        if (Answer1Text != null)
        {
            Answer1Text.text = question != null && question.respostas.Length > 0 ? question.respostas[0].texto : string.Empty;
        }

        if (Answer2Text != null)
        {
            Answer2Text.text = question != null && question.respostas.Length > 1 ? question.respostas[1].texto : string.Empty;
        }
    }

    private void ConfigureButtons()
    {
        if (Answer1Button != null)
        {
            Answer1Button.onClick.AddListener(() => OnAnswerSelected(0));
        }

        if (Answer2Button != null)
        {
            Answer2Button.onClick.AddListener(() => OnAnswerSelected(1));
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (Answer1Button != null)
        {
            var hasAnswer = activeQuestion != null && activeQuestion.respostas != null && activeQuestion.respostas.Length > 0;
            Answer1Button.interactable = interactable && hasAnswer;
        }

        if (Answer2Button != null)
        {
            var hasAnswer = activeQuestion != null && activeQuestion.respostas != null && activeQuestion.respostas.Length > 1;
            Answer2Button.interactable = interactable && hasAnswer;
        }
    }

    private void OnAnswerSelected(int answerIndex)
    {
        if (!awaitingAnswer || activeQuestion == null || answerIndex < 0 || answerIndex >= activeQuestion.respostas.Length)
        {
            return;
        }

        var selectedAnswer = activeQuestion.respostas[answerIndex];
        var correctAnswerId = activeQuestion.GetCorrectAnswerId();
        var isCorrect = !string.IsNullOrEmpty(selectedAnswer?.id) && string.Equals(selectedAnswer.id, correctAnswerId, StringComparison.OrdinalIgnoreCase);
        var elapsed = Time.time - questionStartTime;
        var feedback = DetermineFeedback(isCorrect, elapsed);

        Debug.Log($"Resposta selecionada: {selectedAnswer?.texto} (ID {selectedAnswer?.id}). Correta: {isCorrect}. Tempo: {elapsed:F1}s. Feedback: {feedback}.");
        ResolveQuiz(feedback, elapsed);
    }

    private ARTrackingImageController.QuizFeedback DetermineFeedback(bool isCorrect, float elapsed)
    {
        if (!isCorrect)
        {
            return ARTrackingImageController.QuizFeedback.Error;
        }

        if (elapsed >= 20f)
        {
            return ARTrackingImageController.QuizFeedback.CorrectSlow;
        }

        if (elapsed >= 10f)
        {
            return ARTrackingImageController.QuizFeedback.CorrectMedium;
        }

        return ARTrackingImageController.QuizFeedback.CorrectFast;
    }

    private void ResolveQuiz(ARTrackingImageController.QuizFeedback feedback, float elapsedSeconds)
    {
        if (ARTrackingImageController == null)
        {
            return;
        }

        awaitingAnswer = false;
        SetButtonsInteractable(false);
        UpdateTimerUI(0f);
        ARTrackingImageController.ApplyQuizFeedback(feedback);
        HandlePostFeedback(feedback, elapsedSeconds);
    }

    private void HandlePostFeedback(ARTrackingImageController.QuizFeedback feedback, float elapsedSeconds)
    {
        if (QuizAwnserFeedback == null)
        {
            if (autoAdvanceAfterAnswer)
            {
                CallPreviusScreen();
            }
            return;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(ShowFeedbackAndMaybeAdvance(feedback, elapsedSeconds));
    }

    private IEnumerator ShowFeedbackAndMaybeAdvance(ARTrackingImageController.QuizFeedback feedback, float elapsedSeconds)
    {
        QuizAwnserFeedback.ShowFeedback(feedback, elapsedSeconds);

        var delay = QuizAwnserFeedback.DisplayDuration;
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        QuizAwnserFeedback.HideImmediate();
        feedbackRoutine = null;

        if (autoAdvanceAfterAnswer)
        {
            CallPreviusScreen();
        }
    }

    private void CancelFeedbackRoutine()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        QuizAwnserFeedback?.HideImmediate();
    }

    private void UpdateTimerUI(float secondsRemaining)
    {
        if (TimerFeedbackImage != null)
        {
            TimerFeedbackImage.fillAmount = !awaitingAnswer || maxQuestionTime <= 0f ? 0f : Mathf.Clamp01(secondsRemaining / maxQuestionTime);
        }

        if (timerFeedbackText != null)
        {
            timerFeedbackText.text = awaitingAnswer ? Mathf.CeilToInt(secondsRemaining).ToString() : string.Empty;
        }
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

    // de acordo com a resposta temos os seguintes feedbacks:
    //ERRO = -2 / volta duas casas
    //INATIVIDADE = -1 / voltar 1 casa
    //ACERTO em 20s+ = +1 / avança 1 casa
    //ACERTO em 10 a 20s  = +2 / avança 2 casas
    //ACERTO menos de 10s+ = +3 / avança 3 casas
}