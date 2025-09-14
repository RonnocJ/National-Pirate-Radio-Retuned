using UnityEngine;

public class InsertSlot : MonoBehaviour
{
    [SerializeField] private Animator anim;
    public int contacts;
    private Disc _d;
    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Disc"))
        {
            contacts++;
            if (contacts == 4)
            {
                _d = col.GetComponent<Disc>();
                AddDisc();
            }
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Disc")) contacts--;
    }

    private void AddDisc()
    {
        contacts = 0;

        _d.Active = false;

        _d.grabPlane.enabled = false;
        _d.rb.isKinematic = true;
        _d.GetComponentInParent<MeshCollider>().enabled = false;

        _d.transform.parent.SetParent(transform, true);
        _d.transform.parent.localPosition = Vector3.zero;

        MouseMover.root.ForceRelease();

        anim.SetTrigger("close");
        AudioManager.root.PlaySound(AudioEvent.playCDPlayerClose, gameObject);
        MusicManager.root.SwitchSong(_d.LoadedSong);
    }
}