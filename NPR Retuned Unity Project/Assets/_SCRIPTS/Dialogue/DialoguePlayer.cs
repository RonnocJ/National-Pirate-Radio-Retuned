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
    private static readonly Regex WordChunkRegex = new Regex(@"\S+", RegexOptions.Compiled);
    private static readonly Regex NamePlateSpacingRegex = new Regex("(?<!^)([A-Z])", RegexOptions.Compiled);
    private MatchCollection _activeWordMatches;
    private int _activeWordIndex;
    private string _activeFullLineText;
    private int _activeVisibleCharacters;
    private bool _waitingOnMarkers;
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
            if (TalkerR == null && l.speaker != Characters.FreeQuency) TalkerR = TalkDict[l.speaker];
            else if (TalkDict[l.speaker] != TalkerR) TalkerL = TalkDict[l.speaker];

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
                yield return PlayLineRoutine(line, cluster.wwiseEvent);
            }
        }

        speechBubbleAnim.SetBool("opened", false);
        OnComplete?.Invoke();
    }

    private IEnumerator PlayLineRoutine(TextLine line, AudioEvent dialogueAudio)
    {
        var talker = TalkDict[line.speaker];

        if (!talker.StartedTalking)
        {
            talker.BeginDialogue();
            talker.StartedTalking = true;
            yield return new WaitForSeconds(0.75f);

            speechBubbleAnim.SetBool("opened", true);
            yield return new WaitForSeconds(0.25f);
        }

        if (line.speaker != CurrentSpeaker)
        {
            CurrentSpeaker = line.speaker;
            string displayName = talker.Obscured ? "???" : NamePlateSpacingRegex.Replace(line.speaker.ToString(), " $1");
            namePlate.SetText(displayName);
            speechBubbleAnim.SetBool("right", talker.OnRight);
        }

        yield return new WaitForSeconds(0.1f);

        ResetWordSyncState();

        var matches = WordChunkRegex.Matches(line.text);

        _activeWordMatches = matches;
        _activeWordIndex = 0;
        _activeFullLineText = line.text;
        _activeVisibleCharacters = 0;
        _waitingOnMarkers = true;
        textBody.SetText(string.Empty, 0f);

        yield return new WaitForSeconds((float)line.wait);

        AudioManager.root.PlaySound(dialogueAudio, talker.gameObject, 0, new AudioCallback(WordMarkerCallback, AkCallbackType.AK_Marker | AkCallbackType.AK_EndOfEvent));

        talker.SetTalking(true);

        yield return new WaitUntil(() => !_waitingOnMarkers);

        talker.SetTalking(false);

        ResetWordSyncState();
    }
    private void WordMarkerCallback(AkCallbackType type, AkCallbackInfo info)
    {
        if (!_waitingOnMarkers) return;

        if (type == AkCallbackType.AK_EndOfEvent)
        {
            if (_activeVisibleCharacters < _activeFullLineText.Length)
            {
                textBody.SetText(_activeFullLineText, 0f, false, _activeVisibleCharacters);
                _activeVisibleCharacters = _activeFullLineText.Length;
            }

            _waitingOnMarkers = false;

            return;
        }

        if (info is not AkMarkerCallbackInfo markerInfo) return;

        float.TryParse(markerInfo.strLabel, out float durationSeconds);

        if (_activeWordMatches == null || _activeWordIndex >= _activeWordMatches.Count) return;
        if (string.IsNullOrEmpty(_activeFullLineText)) return;

        var match = _activeWordMatches[_activeWordIndex++];
        int matchStart = match.Index;

        if (_activeVisibleCharacters < matchStart)
        {
            int whitespaceEnd = Mathf.Clamp(matchStart, 0, _activeFullLineText.Length);
            string uptoWhitespace = _activeFullLineText.Substring(0, whitespaceEnd);
            textBody.SetText(uptoWhitespace, 0f, false, _activeVisibleCharacters);
            _activeVisibleCharacters = whitespaceEnd;
        }

        int wordEnd = Mathf.Clamp(match.Index + match.Length, 0, _activeFullLineText.Length);
        int charsToAdd = Mathf.Max(0, wordEnd - _activeVisibleCharacters);
        if (charsToAdd == 0) return;

        float charDelay = 0f;
        if (durationSeconds > 0f && charsToAdd > 0)
        {
            charDelay = durationSeconds / charsToAdd;
        }

        string nextSlice = _activeFullLineText.Substring(0, wordEnd);
        textBody.SetText(nextSlice, charDelay, false, _activeVisibleCharacters);
        _activeVisibleCharacters = wordEnd;
    }
    private void ResetWordSyncState()
    {
        _activeWordMatches = null;
        _activeWordIndex = 0;
        _activeFullLineText = null;
        _activeVisibleCharacters = 0;
        _waitingOnMarkers = false;
    }
    public void ToTitle()
    {
        TalkerL.EndDialogue();
        TalkerR.EndDialogue();

        AudioManager.root.PlaySound(AudioEvent.setTitleOnDelay);

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
