using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;
public enum Characters
{
    FreeQuency,
    JoeTools,
    Auxy
}
public class DialoguePlayer : Singleton<DialoguePlayer>
{
    public Talker TalkerL;
    public Talker TalkerR;
    public Talker[] AllCharacters;
    public Dictionary<Characters, Talker> TalkDict = new();
    public Characters CurrentSpeaker;
    public GlyphTextRenderer textBody;
    public GlyphTextRenderer namePlate;
    public Animator speechBubbleAnim;
    public void PlayDialogue(LevelIntro dialogue, Opinion opinion = Opinion.neutral)
    {
        PlayFromResources($"LevelIntro/{dialogue}", opinion.ToString(), -1, ToLevel);
    }
    public void PlayDialogue(AfterLevel dialogue)
    {
        PlayFromResources($"AfterLevel/{dialogue}", "mono", 0, ToShop);
    }
    public void PlayFromResources(string filePath, string blockName, int clusterId, Action OnComplete)
    {
        if (TalkDict.Count == 0)
        {
            foreach (var c in AllCharacters)
            {
                TalkDict[c.CharName] = c;
            }
        }
        string resourcePath = $"Scripts/{filePath}";

        var script = TextLoader.LoadFromResources(resourcePath);
        if (script == null || script.blocks == null)
        {
            Debug.LogError($"No script with blocks found at {filePath}");
            return;
        }

        var scenario = script.blocks.Find(s => s != null && s.name == blockName);
        if (scenario == null || scenario.clusters == null)
        {
            Debug.LogWarning($"Block '{blockName}' not found.");
            return;
        }

        if (clusterId < 0)
        {
            clusterId = Random.Range(0, scenario.clusters.Count);
        }

        var cluster = scenario.clusters.Find(c => c != null && c.id == clusterId);
        if (cluster == null)
        {
            Debug.LogWarning($"Cluster '{clusterId}' not found in scenario '{blockName}'.");
            return;
        }

        foreach (var l in cluster.lines)
        {
            if (!Enum.TryParse(Regex.Replace(l.speaker, @"\s+", ""), out Characters c)) continue;

            if (TalkerR == null && c != Characters.FreeQuency) TalkerR = TalkDict[c];
            else if (TalkDict[c] != TalkerR) TalkerL = TalkDict[c];

        }

        TalkerR.OnRight = true;
        if (TalkerL != null) TalkerL.OnRight = false;

        StopAllCoroutines();
        StartCoroutine(PlayClusterRoutine(cluster, OnComplete));
    }

    private IEnumerator PlayClusterRoutine(TextCluster cluster, Action OnComplete)
    {
        if (cluster.pauseBefore > 0f)
        {
            yield return new WaitForSeconds(cluster.pauseBefore - 0.1f);
        }

        if (cluster.lines != null)
        {
            foreach (var line in cluster.lines)
            {
                yield return PlayLineRoutine(line);
            }
        }

        speechBubbleAnim.SetBool("opened", false);
        OnComplete?.Invoke();
    }

    private IEnumerator PlayLineRoutine(TextLine line)
    {
        if (!Enum.TryParse(Regex.Replace(line.speaker, @"\s+", ""), out Characters c))
        {
            Debug.LogError("Add speaker to enum!");
            yield break;
        }
        var talker = TalkDict[c];

        if (!talker.StartedTalking)
        {
            talker.BeginDialogue();
            talker.StartedTalking = true;
            yield return new WaitForSeconds(0.75f);

            speechBubbleAnim.SetBool("opened", true);
            yield return new WaitForSeconds(0.25f);
        }

        if (c != CurrentSpeaker)
        {
            CurrentSpeaker = c;
            namePlate.SetText(talker.Obscured ? "???" : line.speaker);
            speechBubbleAnim.SetBool("right", talker.OnRight);
        }

        yield return new WaitForSeconds(0.1f);

        talker.SetTalking(true);

        textBody.SetText(line);

        yield return new WaitForSeconds(line.speed * line.text.Length);

        talker.SetTalking(false);

        yield return new WaitForSeconds(line.wait);
    }
    public void ToTitle()
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
    void ToShop()
    {
        TalkerL.EndDialogue();
        TalkerR.EndDialogue();

        GameSceneManager.root.Invoke("LoadShop", 2.5f);
    }
}