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
		public string correctAnswer;

		public string GetCorrectAnswerId()
		{
			if (!string.IsNullOrEmpty(respostaCorreta))
			{
				return respostaCorreta;
			}

			return correctAnswer;
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
	private readonly Dictionary<int, Sprite> referenceSpriteCache = new();

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

	[SerializeField]
	[Tooltip("Nome base dos arquivos de perguntas, sem sufixos de idioma (ex.: gamedata).")]
	private string gameDataBaseName = "gamedata";

	[SerializeField]
	[Tooltip("Idioma utilizado como fallback quando nenhuma prefer��ncia estiver definida.")]
	private string fallbackLanguage = "pt";

	private Coroutine gameDataLoadRoutine;
	private string lastLoadedGameDataFile;

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
		yield return LoadGameDataAndNotify(GetCurrentLanguage());
		SubscribeToLocalizationChanges();
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

	private void OnDestroy()
	{
		if (LocalizationManager.instance != null)
		{
			LocalizationManager.instance.OnLanguageChanged -= HandleLanguageChanged;
		}
	}

	private void SubscribeToLocalizationChanges()
	{
		if (LocalizationManager.instance != null)
		{
			LocalizationManager.instance.OnLanguageChanged += HandleLanguageChanged;
			return;
		}

		StartCoroutine(WaitForLocalizationAndSubscribe());
	}

	private IEnumerator WaitForLocalizationAndSubscribe()
	{
		while (LocalizationManager.instance == null)
		{
			yield return null;
		}

		LocalizationManager.instance.OnLanguageChanged += HandleLanguageChanged;
	}

	private void HandleLanguageChanged()
	{
		StartGameDataReload();
	}

	private void StartGameDataReload()
	{
		if (gameDataLoadRoutine != null)
		{
			StopCoroutine(gameDataLoadRoutine);
		}

		gameDataLoadRoutine = StartCoroutine(LoadGameDataAndNotify(GetCurrentLanguage()));
	}

	private IEnumerator LoadGameDataAndNotify(string language)
	{
		yield return LoadGameDataAsync(language);
		gameDataLoadRoutine = null;
		RefreshCurrentQuestionAfterReload();
	}

	private void RefreshCurrentQuestionAfterReload()
	{
		if (!dataLoaded)
		{
			return;
		}

		if (currentID >= 0 && questionById.TryGetValue(currentID, out var question))
		{
			currentQuestion = question;
		}
		else if (currentID < 0)
		{
			currentQuestion = null;
		}

		QuestionChanged?.Invoke(currentQuestion);
	}

	private string GetCurrentLanguage()
	{
		var defaultLang = !string.IsNullOrEmpty(fallbackLanguage) ? fallbackLanguage : "pt";
		if (LocalizationManager.instance != null && !string.IsNullOrEmpty(LocalizationManager.instance.defaultLang))
		{
			defaultLang = LocalizationManager.instance.defaultLang;
		}

		return PlayerPrefs.GetString("lang", defaultLang);
	}

	private void BuildLookup()
	{
		nameToMapping.Clear();
		idToMapping.Clear();
		imageIdToIndex.Clear();
		orderedImageIds.Clear();
		referenceSpriteCache.Clear();

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

	public bool TryGetReferenceImageSprite(int imageId, out Sprite sprite)
	{
		if (referenceSpriteCache.TryGetValue(imageId, out sprite) && sprite != null)
		{
			return true;
		}

		sprite = null;

		if (!idToMapping.TryGetValue(imageId, out var mapping))
		{
			return false;
		}

		if (trackedImageManager == null || trackedImageManager.referenceLibrary == null)
		{
			return false;
		}

		var library = trackedImageManager.referenceLibrary;
		var count = library.count;
		for (var i = 0; i < count; i++)
		{
			var referenceImage = library[i];
			if (!string.Equals(referenceImage.name, mapping.referenceImageName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (!referenceImage.texture)
			{
				return false;
			}

			var texture = referenceImage.texture;
			sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
			referenceSpriteCache[imageId] = sprite;
			return true;
		}

		return false;
	}

	private IEnumerator LoadGameDataAsync(string language)
	{
		var candidates = BuildGameDataCandidateList(language);
		string json = null;
		string loadedFile = null;

		foreach (var candidate in candidates)
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				continue;
			}

			var path = Path.Combine(Application.streamingAssetsPath, candidate);

#if UNITY_ANDROID && !UNITY_EDITOR
			using (var request = UnityWebRequest.Get(path))
			{
				yield return request.SendWebRequest();

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning($"Falha ao carregar {candidate}: {request.error}");
					continue;
				}

				json = request.downloadHandler.text;
				loadedFile = candidate;
				break;
			}
#else
			if (!File.Exists(path))
			{
				continue;
			}

			try
			{
				json = File.ReadAllText(path);
				loadedFile = candidate;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"Falha ao carregar {candidate}: {e.Message}");
				continue;
			}

			yield return null;
			if (!string.IsNullOrWhiteSpace(json))
			{
				break;
			}
#endif
		}

		if (string.IsNullOrWhiteSpace(json))
		{
			Debug.LogError($"N\u00E3o foi poss\u00EDvel carregar arquivos de perguntas para o idioma '{language}'.");
			yield break;
		}

		lastLoadedGameDataFile = loadedFile;
		ApplyGameData(json);
	}

	private List<string> BuildGameDataCandidateList(string language)
	{
		var candidates = new List<string>();
		var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var baseName = string.IsNullOrWhiteSpace(gameDataBaseName)
			? "gamedata"
			: Path.GetFileNameWithoutExtension(gameDataBaseName);

		var ext = Path.GetExtension(gameDataBaseName);
		if (string.IsNullOrEmpty(ext))
		{
			ext = ".json";
		}

		string Format(string langCode)
		{
			return string.IsNullOrEmpty(langCode)
				? $"{baseName}{ext}"
				: $"{baseName}.{langCode}{ext}";
		}

		void Add(string candidate)
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				return;
			}

			if (unique.Add(candidate))
			{
				candidates.Add(candidate);
			}
		}

		if (!string.IsNullOrWhiteSpace(language))
		{
			var normalizedLanguage = language.Trim().ToLowerInvariant();
			Add(Format(normalizedLanguage));

			var tokens = normalizedLanguage.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var token in tokens)
			{
				Add(Format(token));
			}
		}

		if (!string.IsNullOrWhiteSpace(fallbackLanguage))
		{
			Add(Format(fallbackLanguage.Trim().ToLowerInvariant()));
		}

		Add($"{baseName}{ext}");
		Add("gamedata.json");
		Add("gamedata.pt.json");
		Add("gamedata.en.json");

		return candidates;
	}

	private string GetActiveGameDataFileName()
	{
		if (!string.IsNullOrEmpty(lastLoadedGameDataFile))
		{
			return lastLoadedGameDataFile;
		}

		var sanitized = string.IsNullOrWhiteSpace(gameDataBaseName) ? "gamedata" : Path.GetFileName(gameDataBaseName);
		if (string.IsNullOrEmpty(Path.GetExtension(sanitized)))
		{
			sanitized += ".json";
		}

		return sanitized;
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
			Debug.LogError($"Erro ao interpretar {GetActiveGameDataFileName()}: {e.Message}");
		}
	}

	private void NormalizeQuestion(QuestionEntry question)
	{
		if (string.IsNullOrEmpty(question.respostaCorreta) && !string.IsNullOrEmpty(question.correctAnswer))
		{
			question.respostaCorreta = question.correctAnswer;
		}

		if (question.respostas == null)
		{
			return;
		}

		foreach (var answer in question.respostas)
		{
			if (answer == null || string.IsNullOrEmpty(answer.texto))
			{
				continue;
			}

			answer.texto = answer.texto.Replace("\\n", "\n");
		}
	}

	private void ValidateImageQuestionLinks()
	{
		foreach (var mapping in imageIdMappings)
		{
			if (!questionById.ContainsKey(mapping.imageId))
			{
				Debug.LogWarning($"Nenhuma pergunta encontrada para o ID de imagem {mapping.imageId}. Verifique o arquivo {GetActiveGameDataFileName()}.");
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
			Debug.LogWarning($"Imagem {mapping.referenceImageName} (ID {mapping.imageId}) não possui pergunta correspondente no {GetActiveGameDataFileName()}.");
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
		var editorLang = GetCurrentLanguage();
		foreach (var candidate in BuildGameDataCandidateList(editorLang))
		{
			var path = Path.Combine(Application.streamingAssetsPath, candidate);
			if (!File.Exists(path))
			{
				continue;
			}

			var json = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(json))
			{
				continue;
			}

			lastLoadedGameDataFile = candidate;
			ApplyGameData(json);
			break;
		}
#endif
	}
#endif
}
