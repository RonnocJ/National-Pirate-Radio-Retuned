using System;
using UnityEngine;
public enum TConType
{
    Grass,
}

[CreateAssetMenu(fileName = "NewData", menuName = "Objects/World/TileConstruct", order = 1)]
public class TileConstruct : ScriptableObject
{
    public TConType constructName;
    public TConData data;
}

[Serializable]
public class TConData
{
    public GameObject Prefab;
    public Mesh DefaultMeshes;
    public int ReqCon;
}