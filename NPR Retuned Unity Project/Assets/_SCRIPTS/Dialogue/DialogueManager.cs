
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class DialogueManager : Singleton<DialogueManager>
{
    public Talker levelIntroTalker;
    public GameObject[] levelCards;
    void Start()
    {
        levelIntroTalker.BeginDialogue();
        DialoguePlayer.root.PlayFromResources($"{levelIntroTalker.gameObject.name}/levelIntroDialogue", levelIntroTalker.Opinion.ToString(), -1, ToLevel);
    }

    void ToLevel()
    {
        levelIntroTalker.EndDialogueToLevel();
        StartCoroutine(ToLevelTransition());
    }

    IEnumerator ToLevelTransition()
    {
        yield return new WaitForSeconds(1.25f);

        levelCards[0].SetActive(true);
        yield return new WaitForSeconds(0.75f);

        levelCards[1].SetActive(true);
        yield return new WaitForSeconds(0.75f);

        levelCards[2].SetActive(true);
        yield return new WaitForSeconds(0.75f);

        GameSceneManager.root.LoadLevel();
    }
}