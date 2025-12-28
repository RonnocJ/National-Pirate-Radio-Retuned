using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
public enum SongName
{
    BRD = 0,
    EVG = 1,
    FUG = 2,
    LIH = 3,
    NPR = 4,
    SOW = 5,
    WLZ = 6,
}
public class CDWheel : MonoBehaviour
{
    public bool Found;
    public Disc OutDisc;
    public Disc InDisc;
    public Disc PendingReturnDisc;
    [SerializeField] private CDButton[] cdButtons;
    [SerializeField] private Transform cdList;
    [SerializeField] private Animator insertAnim;
    [SerializeField] private float findDuration = 3f;
    [SerializeField] private float totalDegrees = 720f;
    [Header("Button Alignment")]
    [SerializeField] private float buttonAlignDuration = 1f;
    [SerializeField] private float shootForce;
    [SerializeField] private float shootVariance;
    private float _btn1StartZ;
    private float _btn2StartZ;
    private float _findTimer;
    private float _findValue => PInputManager.root.actions[PlayerActionType.Find].fValue;
    private float _zAngle;

    void Awake()
    {
        _zAngle = transform.localEulerAngles.x;
        if (cdButtons != null && cdButtons.Length > 2)
        {
            _btn1StartZ = cdButtons[1].transform.GetChild(0).localEulerAngles.z;
            _btn2StartZ = cdButtons[2].transform.GetChild(0).localEulerAngles.z;
        }
    }
    void Update()
    {
        if (PlayerStats.root.NewGame && Tutorial.root.Iteration < 4) return;
        
        if (_findValue == 0 || Found || GameManager.root.CurrentPState != PlayerState.Utility)
        {
            if (_findTimer > 0f && !Found)
            {
                _findTimer -= Time.deltaTime;
                if (_findTimer < 0f) _findTimer = 0f;
                _zAngle = CalcAngle(_findTimer);
                transform.localRotation = Quaternion.Euler(20, 0, _zAngle);
                if (_findTimer > _findTimer - buttonAlignDuration) UpdateButtons(_findTimer);
            }
            else
            {
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
            if (_findTimer > _findTimer - buttonAlignDuration) UpdateButtons(_findTimer);
        }
        else
        {
            Found = true;
            _findTimer = findDuration;
            UpdateButtons(_findTimer);
            _zAngle = CalcAngle(_findTimer);
            transform.localRotation = Quaternion.Euler(20, -0, _zAngle);
            StartCoroutine(PopulateDiscs());
            return;
        }
    }

    private float CalcAngle(float t)
    {
        t = Mathf.Clamp(t, 0f, findDuration);
        float tn = t * (3f / findDuration);
        float sCore;
        if (tn <= 1.5f)
        {
            sCore = 0.5f * tn * tn;
        }
        else
        {
            sCore = 3f * tn - 2.25f - 0.5f * tn * tn;
        }

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
    private IEnumerator PopulateDiscs()
    {
        List<SongName> s = new();
        int cds = 0;

        while (cds < 3)
        {
            SongName randomSong = (SongName)Random.Range(0, Enum.GetValues(typeof(SongName)).Length);

            if (!s.Contains(randomSong) && (InDisc == null || InDisc.LoadedSong != randomSong))
            {
                s.Add(randomSong);
                cds++;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            cdButtons[i].Song = s[i];
            cdButtons[i].Enabled = true;
            cdButtons[i].GetComponentInChildren<GlyphTextRenderer>().SetText(s[i].ToString(), 0.5f, true);
            cdButtons[i].anim.SetTrigger("on");

            yield return new WaitForSeconds(1.75f);
        }
    }

    public void SelectedSong(SongName song)
    {
        var cds = cdList.GetComponentsInChildren<Disc>();

        foreach (var cd in cds)
        {
            if (cd.LoadedSong == song)
            {
                OutDisc = cd;
                break;
            }
        }

        OutDisc.rb.isKinematic = false;
        OutDisc.Active = true;
        OutDisc.Outbound = true;

        OutDisc.rb.AddForce(transform.up * shootForce + transform.right * Random.Range(-shootVariance, shootVariance * 0.5f), ForceMode.Impulse);

        StartCoroutine(ReEnableMeshCol(OutDisc));

        if (Enum.TryParse(OutDisc.LoadedSong.ToString(), out AudioSoundbank bank)) SoundbankManager.root.LoadSoundbank(bank);

        Found = false;

        if (InDisc == null)
        {
            insertAnim.SetTrigger("open");
            AudioManager.root.PlaySound(AudioEvent.playCDPlayerOpen, insertAnim.gameObject);
        }

        foreach (var b in cdButtons)
        {
            b.anim.SetTrigger("off");
            b.GetComponentInChildren<GlyphTextRenderer>().SetText("");
        }
    }
    void OnTriggerEnter(Collider col)
    {
        if (PendingReturnDisc == null) return;

        if(col.TryGetComponent(out Disc d) && d == PendingReturnDisc)
        {
            d.GetComponentInParent<MeshCollider>().enabled = false;
            d.Active = false;
            MouseMover.root.ForceRelease();
            StartCoroutine(SnapDiscBackToWheel(d));

            if (Enum.TryParse(d.LoadedSong.ToString(), out AudioSoundbank bank)) SoundbankManager.root.UnloadSoundbank(bank);

            if(InDisc == null)
            {
                insertAnim.SetTrigger("close");
            }
        }
    }
    public IEnumerator ReEnableMeshCol(Disc d)
    {
        yield return new WaitForSeconds(0.25f);
        d.GetComponentInParent<MeshCollider>().enabled = true;
    }
    private IEnumerator SnapDiscBackToWheel(Disc d)
    {
        yield return new WaitForSeconds(0.5f);

        if (d == null) yield break;

        d.rb.isKinematic = true;
        d.transform.parent.localPosition = new Vector3(0, -0.6f, 0.925f);
        PendingReturnDisc = null;
    }
}
