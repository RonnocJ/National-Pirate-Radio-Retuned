using System;
using System.Collections.Generic;

public interface ISaveData
{
    public void ReadSaveData(Dictionary<string, object> dataDict);
    public Dictionary<string, object> AddSaveData();
}