using System;
using UnityEngine;

public enum FConType
{
    Bush,
    LargeTree,
    SmallTree,
    Stump
}

[CreateAssetMenu(fileName = "NewData", menuName = "Objects/World/FoliageConstruct")]
public class FoliageConstruct : ScriptableObject
{
    public FConType constructName;
    public FConData data;
}

[Serializable]
public class FConData
{
    public GameObject[] Prefabs;
    public Vector3 MinScale;
    public Vector3 MaxScale;
    public int MaxPerTile;
}