using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NonDgUI : Singleton<NonDgUI>
{
    public RectTransform toTalkPanel;
    [SerializeField] private RectTransform[] levelCards;
    [SerializeField] private TextMeshProUGUI[] deepQuotes;

    public IEnumerator FadeToBlack(bool direction, GameState sceneToLoad = GameState.None)
    {        
        toTalkPanel.anchoredPosition = Vector2.zero;
        float timer = direction ? 0 : 1;
        toTalkPanel.GetComponent<Image>().color = new Vector4(0, 0, 0, timer);

        yield return new WaitForSeconds(0.5f);
        
        while (direction ? timer <= 1.1f : timer >= -0.1f)
        {
            toTalkPanel.GetComponent<Image>().color = new Vector4(0, 0, 0, timer);
            timer = direction ? timer + Time.deltaTime : timer - Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        switch (sceneToLoad)
        {
            case GameState.Level:
                GameSceneManager.root.LoadLevel();
                break;
            case GameState.Shop:
                GameSceneManager.root.LoadShop();
                break;
            case GameState.Talking:
                GameSceneManager.root.LoadTalk();
                break;
            case GameState.Debt:
                GameSceneManager.root.LoadDebt();
                break;
        }
    }
    public IEnumerator ShowIntroQuotes()
    {
        toTalkPanel.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(0.5f);

        float timer = 0;

        while (timer <= 3f)
        {
            deepQuotes[0].color = Vector4.one * timer;
            deepQuotes[1].color = Vector4.one * (timer - 2);

            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(3f);

        timer = 0;

        while (timer <= 2f)
        {
            toTalkPanel.GetComponent<Image>().color = new Vector4(0, 0, 0, 1.5f - timer);
            deepQuotes[0].color = Vector4.one * (1.5f - timer);
            deepQuotes[1].color = Vector4.one * (1.5f - timer);

            timer += Time.deltaTime;
            yield return null;
        }

        GameManager.root.CurrentPState = PlayerState.Utility;
    }
    public IEnumerator ToTalkTransition()
    {
        toTalkPanel.GetComponent<Image>().color = Color.black;
        float timer = 0;
        while (timer <= 1f)
        {
            toTalkPanel.anchoredPosition = Vector2.right * ((timer * 2560f / 1f) - 2560);
            timer += Time.deltaTime;
            yield return null;
        }

        toTalkPanel.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(0.25f);

        GameSceneManager.root.LoadTalk();
    }
    public IEnumerator ToLevelTransition()
    {
        for(int i = -1; i <= 1; i++)
        {
            levelCards[i + 1].anchoredPosition = Vector2.right * i * 770;
            levelCards[i + 1].gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(1.25f);

        levelCards[0].gameObject.SetActive(true);
        yield return new WaitForSeconds(0.75f);

        levelCards[1].gameObject.SetActive(true);
        yield return new WaitForSeconds(0.75f);

        levelCards[2].gameObject.SetActive(true);
        yield return new WaitForSeconds(0.75f);

        GameSceneManager.root.LoadLevel();
    }

    public IEnumerator RemoveLevelCard()
    {
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                levelCards[j].position += Quaternion.AngleAxis((j - 1) * -45f, Vector3.forward) * Vector2.up * i / 2f;
                yield return null;
            }
        }
    }
}