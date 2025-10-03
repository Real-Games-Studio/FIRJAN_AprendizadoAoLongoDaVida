using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

		[Tooltip("ID da próxima imagem obrigatória para avançar na sequência. Use valores negativos para sinalizar o fim da sequência.")]
		public int NextTargetID;
	}

	[Header("Referências")]
	[SerializeField]
	private ARTrackedImageManager trackedImageManager;

	[Header("Configuração de IDs")]
	[Tooltip("Último ID aceito pelo controlador. -1 indica que nenhuma imagem foi processada.")]
	public int currentID = -1;
	[SerializeField]
	private List<ImageIdMapping> imageIdMappings = new();

	[Tooltip("Define se, ao alcançar um mapeamento cujo NextTargetID seja negativo, o controlador volta automaticamente a aceitar qualquer imagem.")]
	[SerializeField]
	private bool autoResetOnSequenceEnd = true;

	[Header("Eventos")]
	[SerializeField]
	private UnityEvent<int> onImageDetected;

	[SerializeField]
	private UnityEvent<int> onUnexpectedImageDetected;

	[SerializeField]
	private UnityEvent onSequenceReset;

	private readonly Dictionary<string, ImageIdMapping> nameToMapping = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, ImageIdMapping> idToMapping = new();
	private readonly HashSet<string> notifiedImages = new(StringComparer.OrdinalIgnoreCase);

	[SerializeField]
	[Tooltip("Próximo ID esperado para liberar a leitura da imagem.")]
	private int expectedNextImageId = -1;

	private bool gameStarted;

	public int ExpectedNextImageId => expectedNextImageId;
	public bool GameStarted => gameStarted;

	private void Awake()
	{
		if (trackedImageManager == null)
		{
			trackedImageManager = GetComponent<ARTrackedImageManager>();
		}

		BuildLookup();
		ResetSequenceInternal(false, true, false);
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

		foreach (var mapping in imageIdMappings)
		{
			if (string.IsNullOrWhiteSpace(mapping.referenceImageName))
			{
				continue;
			}

			var trimmedName = mapping.referenceImageName.Trim();
			nameToMapping[trimmedName] = mapping;
			idToMapping[mapping.imageId] = mapping;
		}
	}

	private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
	{
		HandleTrackedCollection(args.added);
		HandleTrackedCollection(args.updated);
	}

	private void HandleTrackedCollection(IEnumerable<ARTrackedImage> trackedImages)
	{
		foreach (var trackedImage in trackedImages)
		{
			if (trackedImage == null)
			{
				continue;
			}

			if (trackedImage.trackingState != TrackingState.Tracking)
			{
				notifiedImages.Remove(trackedImage.referenceImage.name);
				continue;
			}

			var referenceName = trackedImage.referenceImage.name;

			if (!nameToMapping.TryGetValue(referenceName, out var mapping))
			{
				Debug.LogWarning($"Nenhum ID configurado para a imagem '{referenceName}'.");
				continue;
			}

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

	private void AcceptImage(ImageIdMapping mapping, string referenceName)
	{
		notifiedImages.Add(referenceName);
		gameStarted = true;
		currentID = mapping.imageId;
		expectedNextImageId = mapping.NextTargetID;

		if (expectedNextImageId >= 0 && !idToMapping.ContainsKey(expectedNextImageId))
		{
			Debug.LogWarning($"O NextTargetID '{expectedNextImageId}' informado para a imagem '{mapping.referenceImageName}' não possui correspondência na lista de mapeamentos.");
		}
		onImageDetected?.Invoke(mapping.imageId);

		if (expectedNextImageId < 0)
		{
			if (autoResetOnSequenceEnd)
			{
				ResetSequenceInternal(true, false, true);
			}
		}
	}

	public void ResetSequence()
	{
		ResetSequenceInternal(true, true, false);
	}

	private void ResetSequenceInternal(bool invokeEvent, bool clearTrackedCache, bool preserveCurrentId)
	{
		gameStarted = false;

		if (!preserveCurrentId)
		{
			currentID = -1;
		}
		expectedNextImageId = -1;

		if (clearTrackedCache)
		{
			notifiedImages.Clear();
		}

		if (invokeEvent)
		{
			onSequenceReset?.Invoke();
		}
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
	}
#endif
}
