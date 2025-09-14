using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class AudioCallback {
    public Action method;
    public AkCallbackType type;
    public AudioCallback(Action newMethod, AkCallbackType newType) {
        method = newMethod;
        type = newType;
    }
}
public class AudioManager : Singleton<AudioManager> {
    [SerializeField] private AkAudioListener _akListener;
    public GameObject ghostSoundPrefab;
    public float ghostPoolSize;
    void Start() {
        for (int i = 0; i < ghostPoolSize; i++) {
            var gs = Instantiate(ghostSoundPrefab, transform);
            gs.SetActive(false);
        }
    }
    private Dictionary<(AudioEvent, GameObject, float), Queue<uint>> postedSoundEvents = new();
    public bool PlaySound(AudioEvent soundType, GameObject soundSource, float instanceNumber, AudioCallback customCB, out uint eventId) {
        eventId = 0;
        if (soundType == AudioEvent.None)
            return false;

        GameObject sourceObj = soundSource != null ? soundSource : gameObject;

        AkCallbackManager.EventCallback callback = (object inCookie, AkCallbackType type, AkCallbackInfo info) => {
            if (sourceObj != null && type == AkCallbackType.AK_EndOfEvent) {
                var key = (soundType, sourceObj, instanceNumber);
                if (postedSoundEvents.ContainsKey(key)) {
                    postedSoundEvents[key].Dequeue();
                    if (postedSoundEvents[key].Count == 0)
                        postedSoundEvents.Remove(key);
                }
            }

            if (sourceObj != null && customCB != null && type == customCB.type) {
                customCB.method.Invoke();
            }
        };

        if (postedSoundEvents.ContainsKey((soundType, sourceObj, instanceNumber)) && instanceNumber > 0) {
            return false;
        }

        var callbackTypes = AkCallbackType.AK_EndOfEvent;
        if (customCB != null) callbackTypes |= customCB.type;

        eventId = AkUnitySoundEngine.PostEvent(soundType.ToString(), sourceObj,
            (uint)callbackTypes, callback, null);

        var key = (soundType, sourceObj, instanceNumber);
        if (!postedSoundEvents.ContainsKey(key))
            postedSoundEvents[key] = new Queue<uint>();

        postedSoundEvents[key].Enqueue(eventId);

        return true;
    }
    public bool PlaySound(AudioEvent soundType, GameObject soundSource = null, float instanceNumber = 0, AudioCallback customCB = null)
    => PlaySound(soundType, soundSource, instanceNumber, customCB, out _);
    public void PlayGhostSound(AudioEvent soundType, Transform tr) {
        if (transform.childCount == 0) {
            var gs = Instantiate(ghostSoundPrefab, transform);
            gs.SetActive(false);
        }
        var ghostTr = transform.GetChild(0);

        var cb = new AudioCallback(() => {
            ghostTr.position = Vector3.zero;
            ghostTr.rotation = Quaternion.identity;
            ghostTr.SetParent(transform);
            ghostTr.gameObject.SetActive(false);
        }, AkCallbackType.AK_EndOfEvent);

        ghostTr.position = tr.position;
        ghostTr.rotation = tr.rotation;
        ghostTr.parent = null;
        ghostTr.gameObject.SetActive(true);

        PlaySound(soundType, ghostTr.gameObject, 1, cb);
    }
    public bool StopSound(AudioEvent soundType, GameObject soundSource = null, float instanceNumber = 0,
    float fadeTime = 0, AkCurveInterpolation curveType = AkCurveInterpolation.AkCurveInterpolation_Linear) {
        //Check if sound type isn't null
        if (soundType != AudioEvent.None) {
            //Set gameObject to passed source or self
            GameObject sourceObj = soundSource != null ? soundSource : gameObject;
            //Check dictionary against passed sound type and source, and optionally instance number
            if (postedSoundEvents.TryGetValue((soundType, sourceObj, instanceNumber), out var eventIdQueue)) {
                foreach (uint eventId in eventIdQueue) {
                    //Stop each event ID in the queue with fade time (ms -> s) and curve type, if parameters are passed
                    AkUnitySoundEngine.StopPlayingID(eventId, (int)(fadeTime * 1000), curveType);
                }
                //Clear queue and remove from dictionary
                eventIdQueue.Clear();
                postedSoundEvents.Remove((soundType, soundSource, instanceNumber));

                return true;
            }
        }

        return false;
    }
    public bool BreakSound(AudioEvent soundType, GameObject soundSource = null, float instanceNumber = 0) {
        if (soundType != AudioEvent.None) {
            GameObject sourceObj = soundSource != null ? soundSource : gameObject;

            if (postedSoundEvents.TryGetValue((soundType, sourceObj, instanceNumber), out var eventIdQueue)) {
                foreach (uint eventId in eventIdQueue) {
                    AkUnitySoundEngine.ExecuteActionOnPlayingID(AkActionOnEventType.AkActionOnEventType_Break, eventId);
                }
                eventIdQueue.Clear();
                postedSoundEvents.Remove((soundType, soundSource, instanceNumber));

                return true;
            }
        }

        return false;
    }
    public void SilenceSound(AudioEvent soundType) {
        if (soundType != AudioEvent.None) {
            var eventsToStop = new List<(AudioEvent, GameObject, float)>();

            foreach (var kvp in postedSoundEvents) {
                if (kvp.Key.Item1 == soundType) {
                    foreach (uint eventId in kvp.Value) {
                        AkUnitySoundEngine.StopPlayingID(eventId);
                    }

                    eventsToStop.Add(kvp.Key);
                }
            }
            foreach (var key in eventsToStop) {
                postedSoundEvents.Remove(key);
            }
        }
    }
    public void SilenceGameObject(GameObject soundSource) {
        var eventsToStop = new List<(AudioEvent, GameObject, float)>();

        foreach (var kvp in postedSoundEvents) {
            if (kvp.Key.Item2 == soundSource) {
                foreach (uint eventId in kvp.Value) {
                    AkUnitySoundEngine.StopPlayingID(eventId);
                }

                eventsToStop.Add(kvp.Key);
            }
        }
        foreach (var key in eventsToStop) {
            postedSoundEvents.Remove(key);
        }
    }
    public bool IsPlaying(AudioEvent soundType, GameObject soundSource = null, float instanceNumber = 0) {
        if (postedSoundEvents.ContainsKey((soundType, soundSource != null ? soundSource : gameObject, instanceNumber))) {
            return true;
        }
        return false;
    }

    public void SetSwitch(AudioSwitch switchType, GameObject sourceObject = null) {
        if (switchType != AudioSwitch.None) {
            var separatedSwitch = switchType.ToString().Split("_BREAK_");
            AkUnitySoundEngine.SetSwitch(separatedSwitch[0], separatedSwitch[1], sourceObject != null ? sourceObject : gameObject);
        }
    }
    public void SetState(AudioState stateType) {
        if (stateType != AudioState.None) {
            var separatedState = stateType.ToString().Split("_BREAK_");
            AkUnitySoundEngine.SetState(separatedState[0], separatedState[1]);
        }
    }

    public void SetRTPC(AudioRTPC rtpcType, float value, bool isGlobal = true, AudioEvent localEvent = AudioEvent.None, GameObject sourceObject = null, float instanceNumber = 1) {
        if (rtpcType != AudioRTPC.None) {
            GameObject sourceObj = sourceObject != null ? sourceObject : gameObject;

            if (isGlobal) {
                AkUnitySoundEngine.SetRTPCValue(rtpcType.ToString(), value);
            } else {
                if (postedSoundEvents.TryGetValue((localEvent, sourceObj, instanceNumber), out var eventIdQueue)) {
                    foreach (var eventId in eventIdQueue) {
                        AkUnitySoundEngine.SetRTPCValueByPlayingID(rtpcType.ToString(), value, eventId);
                    }
                }
            }
        }
    }
    public float GetRTPC(AudioRTPC rtpcType, GameObject sourceObj = null) {
        int type = 1;

        AkUnitySoundEngine.GetRTPCValue(rtpcType.ToString(), (sourceObj != null) ? sourceObj : gameObject, 0, out var returnValue, ref type);
        return returnValue;
    }

    public void SetTrigger(AudioTrigger triggerEnum, GameObject sourceObj = null) {
        AkUnitySoundEngine.PostTrigger(triggerEnum.ToString(), sourceObj != null ? sourceObj : gameObject);
    }


#if UNITY_EDITOR
    private void Update() {
        _akListener.enabled = !EditorUtility.audioMasterMute;
    }
#endif
}