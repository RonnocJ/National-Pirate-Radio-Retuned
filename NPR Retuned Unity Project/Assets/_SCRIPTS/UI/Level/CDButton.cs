using UnityEngine;

public class CDButton : Interactable
{
    [SerializeField] private CDWheel cdWheel;
    
    public SongName Song;
    public Animator anim;
    public override void OnClick()
    {
        base.OnClick();

        if (PlayerStats.root.NewGame) Tutorial.root.AddDiscFill();

        cdWheel.SelectedSong(Song);
    }
}