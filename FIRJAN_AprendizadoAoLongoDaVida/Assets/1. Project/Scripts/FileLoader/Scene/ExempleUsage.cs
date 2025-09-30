using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using RealGames;

public class ExempleUsage : MonoBehaviour
{
    public AppConfig appConfig;

    public ImageWrapper image;
    public AudioWrapper audio;
    public VideoWrapper video;

    void Start()
    {
        string jsonFilePath = "Assets/StreamingAssets/appConfig.json";
        appConfig = JsonLoader.LoadGameSettings(jsonFilePath);
        image.LoadAndApplySprite();

        audio.LoadAndPlayAudio(true);

        video.LoadAndPlayVideo(false, () =>
        {
            Debug.Log("Vídeo carregado e pronto para tocar.");
            //video.targetVideoPlayer.Play();
        });
    }
}
