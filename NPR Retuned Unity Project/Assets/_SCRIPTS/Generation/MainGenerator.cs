using System.Collections;
using UnityEngine;

public class MainGenerator : Singleton<MainGenerator>
{
    public Vector2Int CurrentChunk;
    [SerializeField] private Transform worldTr;
    [SerializeField] private TerrainGenerator terrain;
    [SerializeField] private FoliageGenerator foliage;
    [SerializeField] private RoadGenerator road;
    [SerializeField] private BuildingGenerator building;
    [SerializeField] private GrassGenerator grass;
    [SerializeField] private Animator doorAnim;
    public bool firstGen = true;
    private Vector3 _playerPos => VanController.root.transform.position;
    private GeneratorSettings g => GeneratorSettings.root;
    IEnumerator Start()
    {
        while (true)
        {
            //Loops through terrain, foliage, road, and buidling generation coroutines

            yield return terrain.GenerateTerrain();
            yield return foliage.GenerateFoliage();

            yield return road.GenerateRoads();
            yield return building.GenerateBuildings();

            //Checks player position to offset chunk

            yield return CheckChunk();

            //Various conditions set after first generation pass it complete

            if (firstGen)
            {
                firstGen = false;
                doorAnim.SetTrigger("openDoors");
                StartCoroutine(NonDgUI.root.RemoveLevelCard());
                StartCoroutine(GrassGen());

                if (!PlayerStats.root.NewGame) GameManager.root.CurrentPState = PlayerState.Utility;
            }
        }
    }
    private IEnumerator CheckChunk()
    {
        Vector2Int playerChunk = new Vector2Int(Mathf.RoundToInt(PosUtil.GetWorldPos(_playerPos).x / g.ChunkSize), Mathf.RoundToInt(PosUtil.GetWorldPos(_playerPos).z / g.ChunkSize));

        if (CurrentChunk != playerChunk)
        {
            CurrentChunk = playerChunk;
            Vector3 previous = worldTr.transform.position;
            Vector3 newPos = new Vector3(-playerChunk.x * g.ChunkSize, 0, -playerChunk.y * g.ChunkSize);
            Vector3 delta = newPos - previous;
            worldTr.transform.position = newPos;

            if (delta != Vector3.zero && grass != null)
            {
                grass.ApplyWorldOffset(delta);
            }
        }
        yield return null;
    }
    private IEnumerator GrassGen()
    {
        //Generates grass independently of other generators after first pass is complete
        
        while(true)
        {
            yield return grass.GenerateGrass();
        }
    }
}
