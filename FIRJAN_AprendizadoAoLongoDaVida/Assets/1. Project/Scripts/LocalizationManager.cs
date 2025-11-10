using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager instance;

    public string defaultLang = "pt";
    public string filePrefix = "strings"; // strings.{lang}.json

    private Dictionary<string, string> strings = new Dictionary<string, string>();

    public event Action OnLanguageChanged;

    public bool editor_updateToEN = false;
    public bool editor_updateToPT = false;

    void OnValidate()
    {
        if (editor_updateToEN)
        {
            editor_updateToEN = false;
            SetLanguage("en");
        }
        if (editor_updateToPT)
        {
            editor_updateToPT = false;
            SetLanguage("pt");
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Always start sessions in Portuguese, regardless of previous PlayerPrefs.
        PlayerPrefs.SetString("lang", defaultLang);
        LoadLanguage(defaultLang);
    }

    public void SetLanguage(string lang)
    {
        Debug.Log("Setting language to: " + lang);
        PlayerPrefs.SetString("lang", lang);
        LoadLanguage(lang);
        OnLanguageChanged?.Invoke();
    }

    public string Get(string key)
    {
        if (strings.TryGetValue(key, out var val)) return val;
        return key;
    }

    private void LoadLanguage(string lang)
    {
        strings.Clear();
        string fileName = filePrefix + "." + lang + ".json";
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var www = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            var op = www.SendWebRequest();
            while (!op.isDone) { }
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load localization file: " + www.error);
                return;
            }
            ParseJson(www.downloadHandler.text);
        }
#else
        if (!File.Exists(path))
        {
            Debug.LogError("Localization file not found: " + path);
            return;
        }
        string json = File.ReadAllText(path);
        ParseJson(json);
#endif
    }

    private void ParseJson(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<LocalizationWrapper>(json);
            if (wrapper?.items != null)
            {
                foreach (var it in wrapper.items)
                {
                    strings[it.key] = it.value;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to parse localization JSON: " + ex.Message);
        }
    }

    [Serializable]
    private class LocalizationWrapper
    {
        public LocalizationItem[] items;
    }

    [Serializable]
    private class LocalizationItem
    {
        public string key;
        public string value;
    }
}
