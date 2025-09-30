using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Networking;


namespace RealGames
{
    [System.Serializable]
    public class FileLoader
    {
        // Caminho base padrão definido como Application.streamingAssetsPath para compatibilidade em tempo de execução
        public string path = Path.Combine(Application.streamingAssetsPath, "Files");
        public string name; // Nome do arquivo com extensão

        public FileLoader() { }

        public FileLoader(string name)
        {
            this.name = name;
            this.path = Path.Combine(Application.streamingAssetsPath, "Files");
            Debug.Log($"[FileLoader] streamingAssetsPath: {Application.streamingAssetsPath}");
            Debug.Log($"[FileLoader] Resource path: {this.path}");
        }

        // ...existing code...
        // Helper to log and normalize file paths
        private string GetNormalizedFilePath(string subfolder = null)
        {
            // Use Application.streamingAssetsPath for runtime builds
            string basePath = Application.streamingAssetsPath;
            string filePath = subfolder != null
                ? Path.Combine(basePath, "Files", subfolder, name)
                : Path.Combine(basePath, "Files", name);

            // Normalize to forward slashes for UnityWebRequest and logging
            string normalized = filePath.Replace("\\", "/");
            Debug.Log($"[FileLoader] Resolved file path: {normalized}");
            return normalized;
        }

        public void LoadSprite(Action<Sprite> onComplete)
        {
            string filePath = GetNormalizedFilePath();
            Debug.Log($"[FileLoader] Checking sprite file: {filePath}");

#if UNITY_ANDROID && !UNITY_EDITOR
                    // On Android, StreamingAssets is inside APK, use UnityWebRequest
                    string url = filePath;
                    UnityWebRequest www = UnityWebRequest.Get(url);
                    www.SendWebRequest().completed += _ =>
                    {
                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            byte[] fileData = www.downloadHandler.data;
                            Texture2D texture = new Texture2D(2, 2);
                            if (texture.LoadImage(fileData))
                            {
                                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                                onComplete?.Invoke(sprite);
                            }
                            else
                            {
                                Debug.LogError($"Failed to load sprite: {name}");
                                onComplete?.Invoke(null);
                            }
                        }
                        else
                        {
                            Debug.LogError($"Sprite file not found (Android): {filePath}");
                            onComplete?.Invoke(null);
                        }
                    };
#else
            if (System.IO.File.Exists(filePath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                    onComplete?.Invoke(sprite);
                }
                else
                {
                    Debug.LogError($"Failed to load sprite: {name}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"Sprite file not found: {filePath}");
                onComplete?.Invoke(null);
            }
#endif
        }

        public void LoadTexture2D(Action<Texture2D> onComplete)
        {
            string filePath = GetNormalizedFilePath();
            Debug.Log($"[FileLoader] Checking texture file: {filePath}");

#if UNITY_ANDROID && !UNITY_EDITOR
                    string url = filePath;
                    UnityWebRequest www = UnityWebRequest.Get(url);
                    www.SendWebRequest().completed += _ =>
                    {
                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            byte[] fileData = www.downloadHandler.data;
                            Texture2D texture = new Texture2D(2, 2);
                            if (texture.LoadImage(fileData))
                            {
                                onComplete?.Invoke(texture);
                            }
                            else
                            {
                                Debug.LogError($"Failed to load texture: {name}");
                                onComplete?.Invoke(null);
                            }
                        }
                        else
                        {
                            Debug.LogError($"Texture file not found (Android): {filePath}");
                            onComplete?.Invoke(null);
                        }
                    };
#else
            if (System.IO.File.Exists(filePath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    onComplete?.Invoke(texture);
                }
                else
                {
                    Debug.LogError($"Failed to load texture: {name}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"Texture file not found: {filePath}");
                onComplete?.Invoke(null);
            }
#endif
        }

        public void LoadAudioClip(Action<AudioClip> onComplete)
        {
            string filePath = GetNormalizedFilePath("Audio");
            Debug.Log($"[FileLoader] Checking audio file: {filePath}");

#if UNITY_ANDROID && !UNITY_EDITOR
                    string url = filePath;
#else
            string url = "file:///" + filePath;
#endif
            // Use UnityWebRequest for all platforms for consistency
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN);
            www.SendWebRequest().completed += _ =>
            {
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    onComplete?.Invoke(clip);
                }
                else
                {
                    Debug.LogError($"Failed to load audio: {name}, Error: {www.error}");
                    onComplete?.Invoke(null);
                }
            };
        }

        public void LoadVideoClip(VideoPlayer videoPlayer, Action onComplete)
        {
            string filePath = GetNormalizedFilePath();
            Debug.Log($"[FileLoader] Checking video file: {filePath}");

#if UNITY_ANDROID && !UNITY_EDITOR
                    videoPlayer.url = filePath;
#else
            videoPlayer.url = "file:///" + filePath;
#endif
            videoPlayer.Prepare();
            videoPlayer.prepareCompleted += _ => onComplete?.Invoke();
        }
    }
}
