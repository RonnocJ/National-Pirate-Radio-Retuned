using System;
using System.Collections.Generic;

[Serializable]
public class DialogueScript
{
    public List<DialogueScenario> scenarios;
}

[Serializable]
public class DialogueScenario
{
    public string name;
    public List<DialogueCluster> clusters;
}

[Serializable]
public class DialogueCluster
{
    public int id;
    public List<DialogueLine> lines;
    public float pauseBefore;
}

[Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;
    public string wwiseEvent;
    public float wait;
    public float speed;
}

