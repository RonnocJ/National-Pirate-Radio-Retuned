using UnityEngine;

public static class PosUtil
{
    public static Vector2Int V3RoundToInt(Vector3 inPos)
    {
        return new Vector2Int(Mathf.RoundToInt(inPos.x), Mathf.RoundToInt(inPos.z));
    }
    public static Vector2Int V3FloorToInt(Vector3 inPos)
    {
        return new Vector2Int(Mathf.FloorToInt(inPos.x), Mathf.FloorToInt(inPos.z));
    }
    public static Vector3 GetWorldPos(Vector3 inPos)
    {
        return new Vector3(
            inPos.x + (MainGenerator.root.CurrentChunk.x * GeneratorSettings.root.ChunkSize),
            inPos.y,
            inPos.z + (MainGenerator.root.CurrentChunk.y * GeneratorSettings.root.ChunkSize)
        );
    }
    public static Vector2Int GetWorldPos(Vector2Int inPos)
    {
        int cellsPerChunk = Mathf.RoundToInt((float)GeneratorSettings.root.ChunkSize / Mathf.Max(1, GeneratorSettings.root.CellSize));
        return new Vector2Int(
            inPos.x + MainGenerator.root.CurrentChunk.x * cellsPerChunk,
            inPos.y + MainGenerator.root.CurrentChunk.y * cellsPerChunk
        );
    }
    public static Vector3 GetLocalPos(Vector3 inPos)
    {
        return new Vector3(
            inPos.x - (MainGenerator.root.CurrentChunk.x * GeneratorSettings.root.ChunkSize),
            inPos.y,
            inPos.z - (MainGenerator.root.CurrentChunk.y * GeneratorSettings.root.ChunkSize)
        );
    }
    public static Vector2Int GetLocalPos(Vector2Int inPos)
    {
        return new Vector2Int(
            inPos.x - (MainGenerator.root.CurrentChunk.x * GeneratorSettings.root.ChunkSize),
            inPos.y - (MainGenerator.root.CurrentChunk.y * GeneratorSettings.root.ChunkSize)
        );
    }
}
