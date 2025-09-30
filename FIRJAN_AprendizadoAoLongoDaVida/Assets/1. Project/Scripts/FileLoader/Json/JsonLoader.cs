using UnityEngine;
using System.IO;

namespace RealGames
{
    public static class JsonLoader
    {
        public static AppConfig LoadGameSettings(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                AppConfig settings = JsonUtility.FromJson<AppConfig>(json);
                return settings;
            }
            else
            {
                Debug.LogError("JSON file not found at: " + jsonFilePath);
                return null;
            }
        }
    }

}
