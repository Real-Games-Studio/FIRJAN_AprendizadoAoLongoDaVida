using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


[RequireComponent(typeof(ARTrackedImageManager))]
public class ARTrackingImageController : MonoBehaviour
{
	[Serializable]
	private struct ImageIdMapping
	{
		[Tooltip("Nome da imagem configurada na Image Library do AR Foundation.")]
		public string referenceImageName;

		[Tooltip("Identificador inteiro que será retornado quando a imagem for detectada.")]
		public int imageId;
	}

	[Serializable]
	private class GameDataFile
	{
		public QuestionEntry[] questions;
	}

	public enum QuizFeedback
	{
		Error = -2,
		Inactivity = -1,
		CorrectSlow = 1,
		CorrectMedium = 2,
		CorrectFast = 3
	}

	[Serializable]
	public class QuestionEntry
	{
		public int id;
		public int nextId;
		public string pergunta;
		public AnswerEntry[] respostas;
		public string respostaCorreta;

		public string GetCorrectAnswerId()
		{
			return respostaCorreta;
		}
	}

	[Serializable]
	public class AnswerEntry
	{
		public string id;
		public string texto;
	}

	[Header("Referências")]
	[SerializeField]
	private ARTrackedImageManager trackedImageManager;

	[SerializeField]
	private TMP_Text currentDistanceAndAngleText;

	[Header("Configuração de IDs")]
	[Tooltip("Último ID aceito pelo controlador. -1 indica que nenhuma imagem foi processada.")]
	public int currentID = -1;
	[SerializeField]
	private List<ImageIdMapping> imageIdMappings = new();

	[Tooltip("Define se, ao alcançar um mapeamento cujo NextTargetID seja negativo, o controlador volta automaticamente a aceitar qualquer imagem.")]
	[SerializeField]
	private bool autoResetOnSequenceEnd = true;

	[Header("Filtros de Detecao")]
	[Tooltip("Distancia maxima (em metros) para validar a leitura da imagem.")]
	[SerializeField]
	[Min(0f)]
	private float maxDistanceMeters = 0.5f;

	[Tooltip("Desvio maximo (0-90) em relacao a perpendicular da imagem para validar a leitura.")]
	[SerializeField]
	[Range(0f, 90f)]
	private float maxAngleDeviationDegrees = 10f;

	[Header("Eventos")]
	[SerializeField]
	private UnityEvent<int> onImageDetected;

	[SerializeField]
	private UnityEvent<int> onUnexpectedImageDetected;

	[SerializeField]
	private UnityEvent onSequenceReset;

	[SerializeField]
	private UnityEvent onGameOver;

	public event Action<int> ImageDetected;
	public event Action<int> UnexpectedImageDetected;
	public event Action SequenceReset;
	public event Action<QuestionEntry> QuestionChanged;
	public event Action<int> NextTargetChanged;
	public event Action<int> LapCompleted;
	public event Action<string> LastImageNameChanged;
	public event Action<Sprite> LastImageSpriteChanged;
	public event Action GameOver;

	private readonly Dictionary<string, ImageIdMapping> nameToMapping = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, ImageIdMapping> idToMapping = new();
	private readonly HashSet<string> notifiedImages = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, QuestionEntry> questionById = new();
	private readonly Dictionary<int, int> imageIdToIndex = new();
	private readonly List<int> orderedImageIds = new();

	[SerializeField]
	[Tooltip("Próximo ID esperado para liberar a leitura da imagem.")]
	private int expectedNextImageId = -1;

	private bool gameStarted;
	private bool dataLoaded;
	private bool trackingSuspended;
	private bool awaitingQuizFeedback;
	private bool hasAnsweredQuestion;
	private int lapsCompleted;
	private int nextTargetHouseIndex = -1;
	private int startingImageId = -1;
	private int unwrappedStepProgress;
	private string lastFoundImageName = string.Empty;
	private Sprite lastFoundImageSprite;
	private bool gameOverTriggered;

	[SerializeField]
	private QuestionEntry currentQuestion;

	private const string GameDataFileName = "gamedata.pt.json";

	public int ExpectedNextImageId => expectedNextImageId;
	public bool GameStarted => gameStarted;
	public bool TrackingSuspended => trackingSuspended;
	public bool HasAnsweredAnyQuestion => hasAnsweredQuestion;
	public int LapsCompleted => lapsCompleted;
	public int NextTargetHouseIndex => nextTargetHouseIndex;
	public int StartingImageId => startingImageId;
	public string LastFoundImageName => lastFoundImageName;
	public Sprite LastFoundImageSprite => lastFoundImageSprite;
	public QuestionEntry CurrentQuestion => currentQuestion;

	public bool TryGetQuestion(int id, out QuestionEntry question) => questionById.TryGetValue(id, out question);

	public QuestionEntry GetQuestionById(int id)
	{
		TryGetQuestion(id, out var question);
		return question;
	}

	public int GetBoardIndexById(int imageId)
	{
		return imageIdToIndex.TryGetValue(imageId, out var index) ? index : -1;
	}

	private void Awake()
	{
		if (trackedImageManager == null)
		{
			trackedImageManager = GetComponent<ARTrackedImageManager>();
		}

		BuildLookup();
		ResetSequenceInternal(false, true, false);
	}

	private IEnumerator Start()
	{
		yield return LoadGameDataAsync();
	}

	private void OnEnable()
	{
		if (trackedImageManager != null)
		{
			trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
		}
	}

	private void OnDisable()
	{
		if (trackedImageManager != null)
		{
			trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
		}

		ResetSequenceInternal(false, true, false);
	}

	private void BuildLookup()
	{
		nameToMapping.Clear();
		idToMapping.Clear();
		imageIdToIndex.Clear();
		orderedImageIds.Clear();

		for (var i = 0; i < imageIdMappings.Count; i++)
		{
			var mapping = imageIdMappings[i];
			if (string.IsNullOrWhiteSpace(mapping.referenceImageName))
			{
				continue;
			}

			var trimmedName = mapping.referenceImageName.Trim();
			nameToMapping[trimmedName] = mapping;
			idToMapping[mapping.imageId] = mapping;
			imageIdToIndex[mapping.imageId] = orderedImageIds.Count;
			orderedImageIds.Add(mapping.imageId);
		}
	}

	private IEnumerator LoadGameDataAsync()
	{
		var path = Path.Combine(Application.streamingAssetsPath, GameDataFileName);
		string json = null;

#if UNITY_ANDROID && !UNITY_EDITOR
		using (var request = UnityWebRequest.Get(path))
		{
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"Falha ao carregar {GameDataFileName}: {request.error}");
				yield break;
			}

			json = request.downloadHandler.text;
		}
#else
		try
		{
			json = File.ReadAllText(path);
		}
		catch (Exception e)
		{
			Debug.LogError($"Falha ao carregar {GameDataFileName}: {e.Message}");
		}

		yield return null;
#endif

		if (string.IsNullOrWhiteSpace(json))
		{
			yield break;
		}

		ApplyGameData(json);
	}

	private void ApplyGameData(string json)
	{
		try
		{
			var data = JsonUtility.FromJson<GameDataFile>(json);
			if (data?.questions == null || data.questions.Length == 0)
			{
				Debug.LogWarning("Nenhuma pergunta encontrada em gamedata.");
				return;
			}

			questionById.Clear();
			foreach (var question in data.questions)
			{
				if (question == null)
				{
					continue;
				}

				NormalizeQuestion(question);

				questionById[question.id] = question;
			}

			ValidateImageQuestionLinks();
			dataLoaded = true;
		}
		catch (Exception e)
		{
			Debug.LogError($"Erro ao interpretar {GameDataFileName}: {e.Message}");
		}
	}

	private void NormalizeQuestion(QuestionEntry question)
	{
		if (!string.IsNullOrEmpty(question.respostaCorreta))
		{
			return;
		}

		const string marker = "\"correctAnswer\":";
		var rawJson = JsonUtility.ToJson(question);
		var index = rawJson.IndexOf(marker, StringComparison.Ordinal);
		if (index < 0)
		{
			return;
		}

		var start = index + marker.Length;
		var end = rawJson.IndexOf('"', start + 1);
		if (end <= start)
		{
			return;
		}

		question.respostaCorreta = rawJson.Substring(start + 1, end - start - 1);
	}

	private void ValidateImageQuestionLinks()
	{
		foreach (var mapping in imageIdMappings)
		{
			if (!questionById.ContainsKey(mapping.imageId))
			{
				Debug.LogWarning($"Nenhuma pergunta encontrada para o ID de imagem {mapping.imageId}. Verifique o arquivo {GameDataFileName}.");
			}
		}
	}

	private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
	{
		HandleTrackedCollection(args.added);
		HandleTrackedCollection(args.updated);
	}

	private void HandleTrackedCollection(IEnumerable<ARTrackedImage> trackedImages)
	{
		if (!dataLoaded || trackingSuspended)
		{
			return;
		}

		foreach (var trackedImage in trackedImages)
		{
			if (trackedImage == null)
			{
				continue;
			}

			if (trackedImage.trackingState != TrackingState.Tracking)
			{
				notifiedImages.Remove(trackedImage.referenceImage.name);
				UpdatePoseFeedback(float.NaN, float.NaN, float.NaN, false);
				continue;
			}

			var withinConstraints = IsWithinPoseConstraints(trackedImage, out var distanceToCamera, out var angleToNormal, out var angleDeviation);
			UpdatePoseFeedback(distanceToCamera, angleToNormal, angleDeviation, withinConstraints);
			if (!withinConstraints)
			{
				continue;
			}

			if (awaitingQuizFeedback)
			{
				continue;
			}

			var referenceName = trackedImage.referenceImage.name;

			if (!nameToMapping.TryGetValue(referenceName, out var mapping))
			{
				Debug.LogWarning($"Nenhum ID configurado para a imagem '{referenceName}'.");
				continue;
			}

			lastFoundImageName = mapping.referenceImageName;
			LastImageNameChanged?.Invoke(lastFoundImageName);
			UpdateLastFoundSprite(trackedImage.referenceImage);

			var imageId = mapping.imageId;

			if (notifiedImages.Contains(referenceName))
			{
				continue;
			}

			if (!gameStarted)
			{
				AcceptImage(mapping, referenceName);
				continue;
			}

			if (expectedNextImageId >= 0 && imageId != expectedNextImageId)
			{
				onUnexpectedImageDetected?.Invoke(imageId);
				UnexpectedImageDetected?.Invoke(imageId);
				continue;
			}

			if (expectedNextImageId < 0 && !autoResetOnSequenceEnd)
			{
				// Sequência encerrada: é necessário reset manual.
				continue;
			}

			AcceptImage(mapping, referenceName);
		}
	}

	private bool IsWithinPoseConstraints(ARTrackedImage trackedImage, out float distanceMeters, out float angleToNormalDegrees, out float angleDeviationDegrees)
	{
		distanceMeters = float.NaN;
		angleToNormalDegrees = float.NaN;
		angleDeviationDegrees = float.NaN;

		if (trackedImage == null)
		{
			return false;
		}

		var mainCamera = Camera.main;
		if (mainCamera == null)
		{
			return false;
		}

		var cameraTransform = mainCamera.transform;
		var toCamera = cameraTransform.position - trackedImage.transform.position;
		var sqrDistance = toCamera.sqrMagnitude;

		if (sqrDistance <= float.Epsilon)
		{
			distanceMeters = 0f;
			angleToNormalDegrees = 0f;
			angleDeviationDegrees = 0f;
			return maxAngleDeviationDegrees <= 0f;
		}

		distanceMeters = Mathf.Sqrt(sqrDistance);
		var directionToCamera = toCamera / distanceMeters;
		angleToNormalDegrees = Vector3.Angle(trackedImage.transform.forward, directionToCamera);
		angleDeviationDegrees = Mathf.Abs(90f - angleToNormalDegrees);

		if (maxDistanceMeters > 0f && distanceMeters > maxDistanceMeters)
		{
			return false;
		}

		return maxAngleDeviationDegrees <= 0f || angleDeviationDegrees <= maxAngleDeviationDegrees;
	}

	private void UpdatePoseFeedback(float distanceMeters, float angleToNormalDegrees, float angleDeviationDegrees, bool withinConstraints)
	{
		if (currentDistanceAndAngleText == null)
		{
			return;
		}

		if (float.IsNaN(distanceMeters) || float.IsNaN(angleToNormalDegrees) || float.IsNaN(angleDeviationDegrees))
		{
			currentDistanceAndAngleText.text = "Distancia: -- | Angulo: --";
			return;
		}

		var status = withinConstraints ? "OK" : "fora";
		currentDistanceAndAngleText.text = $"Distancia: {distanceMeters:0.00} m | Angulo Normal: {angleToNormalDegrees:0.0} deg | Desvio: {angleDeviationDegrees:0.0} deg ({status})";
	}

	private void AcceptImage(ImageIdMapping mapping, string referenceName)
	{
		notifiedImages.Add(referenceName);
		gameStarted = true;
		currentID = mapping.imageId;
		if (startingImageId < 0)
		{
			startingImageId = mapping.imageId;
			unwrappedStepProgress = 0;
		}

		awaitingQuizFeedback = true;
		expectedNextImageId = -1;
		nextTargetHouseIndex = -1;

		if (questionById.TryGetValue(mapping.imageId, out var question))
		{
			currentQuestion = question;
		}
		else
		{
			currentQuestion = null;
			Debug.LogWarning($"Imagem {mapping.referenceImageName} (ID {mapping.imageId}) não possui pergunta correspondente no {GameDataFileName}.");
			awaitingQuizFeedback = false;
		}

		onImageDetected?.Invoke(mapping.imageId);
		ImageDetected?.Invoke(mapping.imageId);
		if (currentQuestion != null)
		{
			QuestionChanged?.Invoke(currentQuestion);
		}
		NextTargetChanged?.Invoke(expectedNextImageId);

		if (expectedNextImageId < 0 && autoResetOnSequenceEnd && !awaitingQuizFeedback)
		{
			ResetSequenceInternal(true, false, true);
		}
	}

	public void ResetSequence()
	{
		ResetSequenceInternal(true, true, false);
	}

	private void ResetSequenceInternal(bool invokeEvent, bool clearTrackedCache, bool preserveCurrentId)
	{
		gameStarted = false;
		awaitingQuizFeedback = false;
		hasAnsweredQuestion = false;
		lapsCompleted = 0;
		nextTargetHouseIndex = -1;
		startingImageId = -1;
		unwrappedStepProgress = 0;
		gameOverTriggered = false;
		lastFoundImageName = string.Empty;
		LastImageNameChanged?.Invoke(lastFoundImageName);
		lastFoundImageSprite = null;
		LastImageSpriteChanged?.Invoke(lastFoundImageSprite);
		UpdatePoseFeedback(float.NaN, float.NaN, float.NaN, false);

		if (!preserveCurrentId)
		{
			currentID = -1;
		}
		expectedNextImageId = -1;

		if (clearTrackedCache)
		{
			notifiedImages.Clear();
		}

		currentQuestion = null;
		NextTargetChanged?.Invoke(expectedNextImageId);

		if (invokeEvent)
		{
			onSequenceReset?.Invoke();
			SequenceReset?.Invoke();
		}
	}

	public void SetTrackingSuspended(bool suspended)
	{
		if (trackingSuspended == suspended)
		{
			return;
		}

		trackingSuspended = suspended;

		if (trackedImageManager != null)
		{
			trackedImageManager.enabled = !suspended;
		}

		if (!suspended)
		{
			notifiedImages.Clear();
		}
	}

	public void ApplyQuizFeedback(QuizFeedback feedback)
	{
		if (!gameStarted || currentID < 0)
		{
			Debug.LogWarning("Não é possível aplicar feedback de quiz antes de iniciar o jogo ou detectar uma imagem.", this);
			return;
		}

		if (awaitingQuizFeedback == false)
		{
			Debug.LogWarning("Feedback de quiz ignorado: nenhum quiz aguardando resposta.", this);
			return;
		}

		if (!imageIdToIndex.TryGetValue(currentID, out var baseIndex))
		{
			Debug.LogWarning($"ID atual {currentID} não está presente na ordem de casas configurada.", this);
			return;
		}

		var boardCount = orderedImageIds.Count;
		if (boardCount == 0)
		{
			Debug.LogWarning("Lista de casas vazia ao aplicar feedback do quiz.", this);
			return;
		}

		var steps = (int)feedback;
		var rawTarget = baseIndex + steps;
		var wrappedIndex = PositiveMod(rawTarget, boardCount);
		var completedLaps = UpdateProgressAndCountLaps(steps, boardCount);
		if (completedLaps > 0)
		{
			lapsCompleted += completedLaps;
			Debug.Log($"O jogador completou {lapsCompleted} volta(s) ao retornar \u00E0 casa inicial.", this);

			ScreenCanvasController.instance.CallAnyScreenByName("gameover");

			LapCompleted?.Invoke(lapsCompleted);
			if (!gameOverTriggered)
			{
				gameOverTriggered = true;
				onGameOver?.Invoke();
				GameOver?.Invoke();
			}
		}

		nextTargetHouseIndex = wrappedIndex;
		expectedNextImageId = orderedImageIds[wrappedIndex];
		awaitingQuizFeedback = false;
		hasAnsweredQuestion = true;
		NextTargetChanged?.Invoke(expectedNextImageId);
	}

	private static int PositiveMod(int value, int length)
	{
		if (length <= 0)
		{
			return 0;
		}

		var result = value % length;
		if (result < 0)
		{
			result += length;
		}
		return result;
	}

	private int UpdateProgressAndCountLaps(int steps, int boardCount)
	{
		var previousProgress = unwrappedStepProgress;
		unwrappedStepProgress += steps;

		if (boardCount <= 0 || startingImageId < 0 || steps <= 0)
		{
			return 0;
		}

		var nextThreshold = boardCount;
		if (previousProgress >= 0)
		{
			var previousLoops = previousProgress / boardCount;
			nextThreshold = (previousLoops + 1) * boardCount;
		}

		var completed = 0;
		while (nextThreshold > previousProgress && unwrappedStepProgress >= nextThreshold)
		{
			completed++;
			nextThreshold += boardCount;
		}

		return completed;
	}

	private void UpdateLastFoundSprite(XRReferenceImage referenceImage)
	{
		if (!referenceImage.texture)
		{
			lastFoundImageSprite = null;
			LastImageSpriteChanged?.Invoke(lastFoundImageSprite);
			return;
		}

		var texture = referenceImage.texture;
		lastFoundImageSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
		LastImageSpriteChanged?.Invoke(lastFoundImageSprite);
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		if (trackedImageManager == null)
		{
			trackedImageManager = GetComponent<ARTrackedImageManager>();
		}

		BuildLookup();
		expectedNextImageId = -1;
		currentQuestion = null;
		dataLoaded = false;

#if UNITY_EDITOR
		var path = Path.Combine(Application.streamingAssetsPath, GameDataFileName);
		if (File.Exists(path))
		{
			var json = File.ReadAllText(path);
			if (!string.IsNullOrWhiteSpace(json))
			{
				ApplyGameData(json);
			}
		}
#endif
	}
#endif
}
