using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;
public enum SongName
{
    NPR = 0,
    ABC = 1,
    DEF = 2,
    GHI = 3
}
public class CDWheel : MonoBehaviour
{
    public bool Found;
    [SerializeField] private string buttonTextFile;
    [SerializeField] private CDButton[] cdButtons;
    [SerializeField] private Animator insertAnim;
    [SerializeField] private float findDuration = 3f;
    [SerializeField] private float totalDegrees = 720f; 
    [Header("Button Alignment")]
    [SerializeField] private float buttonAlignDuration = 1f;
    private float _btn1StartZ;
    private float _btn2StartZ;
    private float _findTimer;
    private TextBlock cdButtonText;

    private float _findValue => PInputManager.root.actions[PlayerActionType.Find].fValue;
    private float _zAngle;

    void Awake()
    {
        _zAngle = transform.localEulerAngles.x;
        cdButtonText = TextLoader.LoadFromResources($"Text/UI/{buttonTextFile}").blocks[0];
        if (cdButtons != null && cdButtons.Length > 2)
        {
            _btn1StartZ = cdButtons[1].transform.GetChild(0).localEulerAngles.z;
            _btn2StartZ = cdButtons[2].transform.GetChild(0).localEulerAngles.z;
        }
    }
    void Update()
    {
        if (_findValue == 0 || Found || GameManager.root.CurrentPState != PlayerState.Utility)
        {
            if (_findTimer > 0f && !Found)
            {
                _findTimer -= Time.deltaTime;
                if (_findTimer < 0f) _findTimer = 0f;
                _zAngle = CalcAngle(_findTimer);
                transform.localRotation = Quaternion.Euler(20, 0, _zAngle);
                if(_findTimer > _findTimer - buttonAlignDuration) UpdateButtons(_findTimer);
            }
            else
            {
                //_findTimer = 0f;
                UpdateButtons(_findTimer);
            }

            return;
        }

        if (_findTimer < findDuration)
        {
            _findTimer += Time.deltaTime;
            if (_findTimer > findDuration) _findTimer = findDuration;
            _zAngle = CalcAngle(_findTimer);
            transform.localRotation = Quaternion.Euler(20, 0, _zAngle);
            if(_findTimer > _findTimer - buttonAlignDuration) UpdateButtons(_findTimer);
        }
        else
        {
            Found = true;
            _findTimer = findDuration;
            UpdateButtons(_findTimer);
            _zAngle = CalcAngle(_findTimer);
            transform.localRotation = Quaternion.Euler(20, -0, _zAngle);
            PopulateDiscs();
            return;
        }
    }

    private float CalcAngle(float t)
    {
        t = Mathf.Clamp(t, 0f, findDuration);
        // Map to a 0..3s profile regardless of configured duration
        float tn = (findDuration > 0f) ? t * (3f / findDuration) : 0f; // normalized to 0..3
        float sCore;
        if (tn <= 1.5f)
        {
            sCore = 0.5f * tn * tn; // 0.5 * t^2
        }
        else
        {
            sCore = 3f * tn - 2.25f - 0.5f * tn * tn; // 3t - 2.25 - 0.5 t^2
        }
        // Total area over 0..3 is 2.25; scale to totalDegrees
        float angle = totalDegrees * (sCore / 2.25f);
        return angle;
    }

    private void UpdateButtons(float t)
    {
        if (cdButtons == null || cdButtons.Length <= 2) return;

        float startT = findDuration - buttonAlignDuration;
        float p = (t <= startT) ? 0f : Mathf.Clamp01((t - startT) / buttonAlignDuration);

        float z1 = Mathf.Lerp(_btn1StartZ, 0f, p);

        float z2 = Mathf.Repeat(Mathf.Lerp(_btn2StartZ, 360f, p), 360f);

        var t1 = cdButtons[1].transform.GetChild(0);
        var t2 = cdButtons[2].transform.GetChild(0);

        var e1 = t1.localEulerAngles; e1.z = z1; t1.localEulerAngles = e1;
        var e2 = t2.localEulerAngles; e2.z = z2; t2.localEulerAngles = e2;
    }
    private void PopulateDiscs()
    {
        List<SongName> s = new();
        int cds = 0;

        while (cds < 3)
        {
            SongName randomSong = (SongName)Random.Range(0, Enum.GetValues(typeof(SongName)).Length);

            if (!s.Contains(randomSong))
            {
                s.Add(randomSong);
                cds++;
            }
        }

        

        for (int i = 0; i < 3; i++)
        {
            cdButtons[i].Song = s[i];
            cdButtons[i].Enabled = true;
            cdButtons[i].GetComponentInChildren<GlyphTextRenderer>().SetText(cdButtonText.clusters.Find(c => c != null && c.id == (int)s[i]).lines[0]);
            cdButtons[i].anim.SetTrigger("on");
        }
    }

    public void SelectedSong()
    {
        insertAnim.SetTrigger("open");
        foreach (var b in cdButtons)
        {
            b.anim.SetTrigger("off");
        }
    }
}
