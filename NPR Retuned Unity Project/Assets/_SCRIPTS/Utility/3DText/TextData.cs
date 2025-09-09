using System;
using System.Collections.Generic;

[Serializable]
public class TextFile
{
    public List<TextBlock> blocks;
}

[Serializable]
public class TextBlock
{
    public string name;
    public List<TextCluster> clusters;
}

[Serializable]
public class TextCluster
{
    public int id;
    public List<TextLine> lines;
    public float pauseBefore;
}

[Serializable]
public class TextLine
{
    public string speaker;
    public string text;
    public string wwiseEvent;
    public float wait;
    public float speed;
}

