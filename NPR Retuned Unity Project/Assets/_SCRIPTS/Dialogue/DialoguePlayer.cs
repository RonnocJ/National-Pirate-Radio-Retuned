using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class DialoguePlayer : Singleton<DialoguePlayer>
{
    [System.Serializable]
    public class SpeakerBinding
    {
        public string name;
        public Talker talker;
    }

    public List<SpeakerBinding> speakers = new List<SpeakerBinding>();
    public GlyphTextRenderer textBody;
    public GlyphTextRenderer namePlate;
    public Animator speechBubbleAnim;

    private readonly Dictionary<string, Talker> _speakerMap = new Dictionary<string, Talker>();

    protected override void Awake()
    {
        base.Awake();

        _speakerMap.Clear();
        foreach (var b in speakers)
        {
            if (!string.IsNullOrEmpty(b.name) && b.talker)
            {
                _speakerMap[b.name] = b.talker;
            }
        }
    }

    public void PlayFromResources(string filePath, string scenarioName, int clusterId, Action OnComplete)
    {
        string resourcePath = $"Scripts/{filePath}";

        var script = DialogueLoader.LoadFromResources(resourcePath);
        if (script == null || script.scenarios == null)
            return;

        var scenario = script.scenarios.Find(s => s != null && s.name == scenarioName);
        if (scenario == null || scenario.clusters == null)
        {
            Debug.LogWarning($"Scenario '{scenarioName}' not found.");
            return;
        }

        if (clusterId < 0)
        {
            clusterId = Random.Range(0, scenario.clusters.Count);
        }

        var cluster = scenario.clusters.Find(c => c != null && c.id == clusterId);
        if (cluster == null)
        {
            Debug.LogWarning($"Cluster '{clusterId}' not found in scenario '{scenarioName}'.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(PlayClusterRoutine(cluster, OnComplete));
    }

    private IEnumerator PlayClusterRoutine(DialogueCluster cluster, Action OnComplete)
    {
        if (cluster.pauseBefore > 0f)
        {
            yield return new WaitForSeconds(cluster.pauseBefore - 0.1f);
        }

        speechBubbleAnim.SetBool("opened", true);
        yield return new WaitForSeconds(0.1f);

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

    private IEnumerator PlayLineRoutine(DialogueLine line)
    {
        _speakerMap.TryGetValue(line.speaker, out var talker);

        if (!string.IsNullOrEmpty(line.speaker))
        {
            var speakerLine = new DialogueLine { text = line.speaker, speed = 0f };
            namePlate.SetText(speakerLine);
        }

        if (talker)
        {
            talker.SetTalking(true);
        }

        if (!string.IsNullOrEmpty(line.text))
        {
            textBody.SetText(line);
        }

        yield return new WaitForSeconds(line.speed * line.text.Length);

        if (talker)
        {
            talker.SetTalking(false);
        }

        yield return new WaitForSeconds(line.wait);
    }
}
