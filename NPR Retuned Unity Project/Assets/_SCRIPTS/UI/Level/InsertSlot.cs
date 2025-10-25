using System;
using UnityEngine;

public class InsertSlot : MonoBehaviour
{
    public static Action<SongName> OnSongInserted;
    [SerializeField] private float shootForce;
    [SerializeField] private float shootVariance;
    [SerializeField] private Transform discList;
    [SerializeField] private Animator anim;
    [SerializeField] private CDWheel wheel;
    [SerializeField] private GlyphTextRenderer nameText;
    public int contacts;
    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Disc") && col.TryGetComponent(out Disc d) && d.Outbound)
        {
            contacts++;
            if (contacts == 4)
            {
                AddDisc();
            }
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Disc"))
        {
            contacts--;
            if (contacts < 0) contacts = 0;
        }
    }

    private void AddDisc()
    {
        if (wheel.OutDisc == null) return;

        contacts = 0;

        wheel.OutDisc.Active = false;
        wheel.OutDisc.Outbound = false;

        wheel.OutDisc.grabPlane.enabled = false;
        wheel.OutDisc.rb.isKinematic = true;
        wheel.OutDisc.GetComponentInParent<MeshCollider>().enabled = false;

        var discParent = wheel.OutDisc.transform.parent;
        discParent.SetParent(transform, true);
        discParent.localPosition = Vector3.zero;

        MouseMover.root.ForceRelease();

        anim.SetTrigger("close");
        AudioManager.root.PlaySound(AudioEvent.playCDPlayerClose, gameObject);
        MusicManager.root.SwitchSong(wheel.OutDisc.LoadedSong);

        wheel.InDisc = wheel.OutDisc;
        wheel.OutDisc = null;
        if (wheel.PendingReturnDisc == wheel.OutDisc) wheel.PendingReturnDisc = null;

        nameText.SetText($"Now Playing: \n{wheel.InDisc.FullName}", 0.01f);

        OnSongInserted?.Invoke(wheel.InDisc.LoadedSong);
    }
    public bool EjectDisc()
    {
        if (wheel.InDisc == null) return false;

        wheel.PendingReturnDisc = wheel.InDisc;

        wheel.InDisc.Active = true;

        wheel.InDisc.grabPlane.enabled = true;
        wheel.InDisc.rb.isKinematic = false;
        StartCoroutine(wheel.ReEnableMeshCol(wheel.InDisc));

        wheel.InDisc.transform.parent.SetParent(discList, true);

        wheel.InDisc.rb.AddForce(transform.up * shootForce + transform.right * UnityEngine.Random.Range(-shootVariance, shootVariance * 0.25f), ForceMode.Impulse);

        wheel.InDisc = null;

        nameText.SetText("Please insert CD", 0.01f);

        return true;
    }
}
