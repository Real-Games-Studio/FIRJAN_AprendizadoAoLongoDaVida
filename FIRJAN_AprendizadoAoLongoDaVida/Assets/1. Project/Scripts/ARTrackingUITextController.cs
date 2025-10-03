using TMPro;
using UnityEngine;

/// <summary>
/// Atualiza textos de interface (TMP) conforme os eventos do <see cref="ARTrackingImageController"/>.
/// Faça a ligação dos métodos públicos deste componente nos eventos do controlador via Inspector.
/// </summary>
public class ARTrackingUITextController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField]
    private TMP_Text messageText;

    [SerializeField]
    private ARTrackingImageController trackingController;

    [Header("Mensagens")]
    [SerializeField]
    [TextArea]
    private string startMessage = "Encontre uma imagem!";

    [SerializeField]
    [TextArea]
    private string foundMessageFormat = "ID: {0} encontrado. Agora encontre a imagem de ID {1}.";

    [SerializeField]
    [TextArea]
    private string finalMessageFormat = "ID: {0} encontrado. Sequência concluída!";

    [SerializeField]
    [TextArea]
    private string unexpectedMessageFormat = "ID {0} não é o esperado. Procure a imagem de ID {1}.";

    [SerializeField]
    [TextArea]
    private string unexpectedWithoutNextFormat = "ID {0} não é o esperado. Procure uma imagem válida para continuar.";

    private void Awake()
    {
        if (trackingController == null)
        {
#if UNITY_2023_1_OR_NEWER
            trackingController = FindFirstObjectByType<ARTrackingImageController>();
#else
            trackingController = FindObjectOfType<ARTrackingImageController>();
#endif
        }
    }

    private void Start()
    {
        ShowStartMessage();
    }

    /// <summary>
    /// Dispare este método a partir do evento <c>onImageDetected</c> do controlador.
    /// </summary>
    /// <param name="currentId">ID da imagem reconhecida.</param>
    public void HandleImageDetected(int currentId)
    {
        if (messageText == null)
        {
            return;
        }

        var nextId = trackingController != null ? trackingController.ExpectedNextImageId : -1;
        if (nextId >= 0)
        {
            messageText.text = string.Format(foundMessageFormat, currentId, nextId);
        }
        else
        {
            messageText.text = string.Format(finalMessageFormat, currentId);
        }
    }

    /// <summary>
    /// Dispare este método a partir do evento <c>onUnexpectedImageDetected</c> do controlador.
    /// </summary>
    /// <param name="unexpectedId">ID detectado fora da ordem esperada.</param>
    public void HandleUnexpectedImage(int unexpectedId)
    {
        if (messageText == null)
        {
            return;
        }

        var nextId = trackingController != null ? trackingController.ExpectedNextImageId : -1;
        if (nextId >= 0)
        {
            messageText.text = string.Format(unexpectedMessageFormat, unexpectedId, nextId);
        }
        else
        {
            messageText.text = string.Format(unexpectedWithoutNextFormat, unexpectedId);
        }
    }

    /// <summary>
    /// Dispare este método a partir do evento <c>onSequenceReset</c> do controlador.
    /// </summary>
    public void HandleSequenceReset()
    {
        ShowStartMessage();
    }

    private void ShowStartMessage()
    {
        if (messageText == null)
        {
            return;
        }

        messageText.text = startMessage;
    }
}
