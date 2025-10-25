using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class DialoguePlayer : Singleton<DialoguePlayer>
{
    public Characters CurrentSpeaker;
    public GlyphTextRenderer textBody;
    public GlyphTextRenderer namePlate;
    public Animator speechBubbleAnim;

    public void PlayFromResources(string filePath, string blockName, int clusterId, Action OnComplete)
    {
        string resourcePath = $"Scripts/{filePath}";

        var script = TextLoader.LoadFromResources(resourcePath);
        if (script == null || script.blocks == null)
            return;

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
        var talker = DialogueManager.root.TalkDict[c];

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
            namePlate.SetText(line.speaker);
            speechBubbleAnim.SetBool("right", talker.OnRight);
        }

        talker.SetTalking(true);

        textBody.SetText(line);

        yield return new WaitForSeconds(line.speed * line.text.Length);

        talker.SetTalking(false);

        yield return new WaitForSeconds(line.wait);
    }
}
