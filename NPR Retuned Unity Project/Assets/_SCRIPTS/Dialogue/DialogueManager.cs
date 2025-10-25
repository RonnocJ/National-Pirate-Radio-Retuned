using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public enum Characters
{
    FreeQuency,
    JoeTools
}
public class DialogueManager : Singleton<DialogueManager>
{
    public Talker TalkerL;
    public Talker TalkerR;
    public GameObject[] levelCards;
    public Talker[] AllCharacters;
    public Dictionary<Characters, Talker> TalkDict = new();
    void Start()
    {
        foreach (var c in AllCharacters)
        {
            TalkDict[c.CharName] = c;
        }

        if (TalkerL == null) TalkerL = TalkDict[Characters.FreeQuency];
        if (TalkerR == null) TalkerR = TalkDict[Characters.JoeTools];

        TalkerL.OnRight = false;
        TalkerR.OnRight = true;

        if (GameManager.root.NewGame) DialoguePlayer.root.PlayFromResources("GameIntro/newGame", "mono", -1, ToTitle);
        else
        {
            DialoguePlayer.root.PlayFromResources($"LevelIntro/introStage{GameManager.root.CurrentStage}", "neutral", -1, ToLevel);
        }
    }
    void ToTitle()
    {
        TalkerL.EndDialogue();
        TalkerR.EndDialogue();

        GameSceneManager.root.Invoke("LoadTitle", 2.5f);
    }
    void ToLevel()
    {
        TalkDict[Characters.JoeTools].EndDialogueToLevel();
        StartCoroutine(NonDgUI.root.ToLevelTransition());
    }
}