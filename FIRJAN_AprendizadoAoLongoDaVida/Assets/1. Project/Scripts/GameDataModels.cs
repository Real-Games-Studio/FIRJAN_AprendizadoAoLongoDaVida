using System;
using System.Collections.Generic;

[Serializable]
public class GameData
{
    public List<Question> questions;
}

[Serializable]
public class Question
{
    public string id;
    public string title;
    public string description;
    public List<Answer> answers;
}

[Serializable]
public class Answer
{
    public string text;
    public string feedback;
    // New preferred field in gamedata.json
    public int casas;
    // 'points' removed: use 'casas' exclusively
}

[Serializable]
public class GameConfig
{
    public string applicationName;
    public int maxInactiveTime;
    public string gamedataVersion;
    public string serverIP;
    public int serverPort;
}
