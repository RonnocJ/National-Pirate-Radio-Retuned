using System;
using UnityEngine;

public enum RConType
{
    Road,
    TIntersection,
    XIntersection
}

[CreateAssetMenu(fileName = "NewData", menuName = "Objects/World/RoadConstruct")]
public class RoadConstruct : ScriptableObject
{
    public RConType constructName;
    public RConData data;
}

[Serializable]
public class RConData
{
    public GameObject SegmentPrefab;
    public float HeightOffset = 0.02f; 
}

