using System;
using UnityEngine;

public enum RConType
{
    Road
}

[CreateAssetMenu(fileName = "NewData", menuName = "Objects/World/RoadConstruct")]
public class RoadConstruct : ScriptableObject
{
    public RConType constructName = RConType.Road;
    public RConData data;
}

[Serializable]
public class RConData
{
    public GameObject SegmentPrefab;
    public float HeightOffset = 0.02f; 
}

