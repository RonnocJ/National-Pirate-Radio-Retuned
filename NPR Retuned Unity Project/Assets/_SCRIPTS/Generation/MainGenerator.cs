using System.Collections;
using UnityEngine;

public class MainGenerator : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.25f);

        while (true)
        {
            yield return TerrainGenerator.root.GenerateTerrain();
            yield return RoadGenerator.root.GenerateRoads();
            yield return FoliageGenerator.root.GenerateFoliage();
        }
    }
}