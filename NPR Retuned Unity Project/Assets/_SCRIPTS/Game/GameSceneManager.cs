using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : Singleton<GameSceneManager>
{
    [SerializeField] private RectTransform[] levelCards;
    void Start()
    {
        //DontDestroyOnLoad(this);
    }

    public void LoadLevel()
    {
        SceneManager.LoadSceneAsync("Level", LoadSceneMode.Single);

        SceneManager.sceneLoaded += (sc, _) => StartCoroutine(RemoveLevelCard());
    }

    IEnumerator RemoveLevelCard()
    {
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < 120; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                levelCards[j].position += Quaternion.AngleAxis((j - 1) * -45f, Vector3.forward) * Vector2.up * i / 2f;
                yield return null;
            }
        }
    }
}