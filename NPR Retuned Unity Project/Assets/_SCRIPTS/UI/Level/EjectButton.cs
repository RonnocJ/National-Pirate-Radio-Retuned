using UnityEngine;

public class EjectButton : Interactable
{
    [SerializeField] private Animator anim;
    [SerializeField] private Animator insertAnim;
    [SerializeField] private InsertSlot insert;
    public override void OnClick()
    {
        if (!insert.EjectDisc()) return;

        base.OnClick();

        anim.SetTrigger("open");
        insertAnim.SetTrigger("open");

        MusicManager.root.SetStatic(); 
    }
}