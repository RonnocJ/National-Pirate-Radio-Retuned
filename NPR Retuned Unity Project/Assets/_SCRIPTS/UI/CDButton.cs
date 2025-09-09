using UnityEngine;

public class CDButton : Interactable
{
    [SerializeField] private CDWheel cdWheel;
    public SongName Song;
    public Animator anim;
    public override void OnClick()
    {
        base.OnClick();
        anim.SetTrigger("clicked");

        cdWheel.SelectedSong();
    }
}