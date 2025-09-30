using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;

namespace RealGames
{
    public static class FileLoaderExtensions
    {
        public static void LoadAndApplySprite(this Image imageComponent, FileLoader fileLoader)
        {
            if (fileLoader != null && imageComponent != null)
            {
                fileLoader.LoadSprite(sprite =>
                {
                    if (sprite != null)
                    {
                        imageComponent.sprite = sprite;
                    }
                });
            }
        }

        public static void LoadAndApplyTexture(this RawImage rawImageComponent, FileLoader fileLoader)
        {
            if (fileLoader != null && rawImageComponent != null)
            {
                fileLoader.LoadTexture2D(texture =>
                {
                    if (texture != null)
                    {
                        rawImageComponent.texture = texture;
                    }
                });
            }
        }

        public static void LoadAndPlayAudio(this AudioSource audioSourceComponent, FileLoader fileLoader)
        {
            if (fileLoader != null && audioSourceComponent != null)
            {
                fileLoader.LoadAudioClip(audioClip =>
                {
                    if (audioClip != null)
                    {
                        audioSourceComponent.clip = audioClip;
                        audioSourceComponent.Play();
                    }
                });
            }
        }

        public static void LoadAndPlayVideo(this VideoPlayer videoPlayerComponent, FileLoader fileLoader)
        {
            if (fileLoader != null && videoPlayerComponent != null)
            {
                fileLoader.LoadVideoClip(videoPlayerComponent, () =>
                {
                    Debug.Log("Video loaded successfully.");
                    videoPlayerComponent.Play();
                });
            }
        }
    }
}

namespace RealGames
{
    [System.Serializable]
    public struct ImageWrapper
    {
        public UnityEngine.UI.Image targetImage;
        public UnityEngine.Sprite sprite; // Sprite to be applied directly
        public FileLoader imageFile;

        public void LoadSprite()
        {
            var localImageFile = imageFile;
            var localThis = this; // Copy 'this' to a local variable
            if (localImageFile != null)
            {
                localImageFile.LoadSprite(loadedSprite =>
                {
                    if (loadedSprite != null)
                    {
                        localThis.sprite = loadedSprite; // Use the local copy of 'this'
                    }
                });
            }
        }

        public void LoadAndApplySprite()
        {
            var localTargetImage = targetImage;
            var localImageFile = imageFile;

            if (localImageFile != null && localTargetImage != null)
            {
                localImageFile.LoadSprite(sprite =>
                {
                    if (sprite != null)
                    {
                        localTargetImage.sprite = sprite;
                    }
                });
            }
        }
    }
}

namespace RealGames
{
    [System.Serializable]
    public struct VideoWrapper
    {
        public UnityEngine.Video.VideoPlayer targetVideoPlayer;
        public FileLoader videoFile;

        public VideoWrapper(UnityEngine.Video.VideoPlayer targetVideoPlayer, FileLoader videoFile)
        {
            this.targetVideoPlayer = targetVideoPlayer;
            this.videoFile = videoFile;
        }

        public void LoadAndPlayVideo(bool playOnLoad = false, System.Action onComplete = null)
        {
            var localTargetVideoPlayer = targetVideoPlayer;
            var localVideoFile = videoFile;

            if (localVideoFile != null && localTargetVideoPlayer != null)
            {
                localVideoFile.LoadVideoClip(localTargetVideoPlayer, () =>
                {
                    Debug.Log("Video loaded successfully.");
                    if (playOnLoad)
                    {
                        localTargetVideoPlayer.Play();
                    }
                    onComplete?.Invoke(); // Chama o callback após o vídeo ser carregado (ou começar a tocar, se playOnLoad for true)
                });
            }
        }
    }
}


namespace RealGames
{
    [System.Serializable]
    public struct AudioWrapper
    {
        public UnityEngine.AudioSource targetAudioSource;
        public FileLoader audioFile;

        public AudioWrapper(UnityEngine.AudioSource targetAudioSource, FileLoader audioFile)
        {
            this.targetAudioSource = targetAudioSource;
            this.audioFile = audioFile;
        }

        public void LoadAndPlayAudio(bool playOnLoad = false, System.Action onComplete = null)
        {
            var localTargetAudioSource = targetAudioSource;
            var localAudioFile = audioFile;

            if (localAudioFile != null && localTargetAudioSource != null)
            {
                localAudioFile.LoadAudioClip(audioClip =>
                {
                    if (audioClip != null)
                    {
                        localTargetAudioSource.clip = audioClip;
                        if (playOnLoad)
                        {
                            localTargetAudioSource.Play();
                        }
                        onComplete?.Invoke(); // Chama o callback após o áudio começar a tocar
                    }
                });
            }
        }
    }
}
