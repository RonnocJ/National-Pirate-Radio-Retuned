using System;
using UnityEngine;

public enum BConType
{
    HouseSmall = 0,
    HouseLarge = 1,
    AntennaTower = 2
}

[CreateAssetMenu(fileName = "NewData", menuName = "Objects/World/BuildingConstruct")]
public class BuildingConstruct : ScriptableObject
{
    public BConType constructName;
    public BConData data;
}

[Serializable]
public class BConData
{
    public Bounds Area;
    public GameObject[] Prefabs;
    public bool GeneratesOnRoads;
    public float MinSpacing;
    [Range(0, 1f)] public float SpawnChance;
}