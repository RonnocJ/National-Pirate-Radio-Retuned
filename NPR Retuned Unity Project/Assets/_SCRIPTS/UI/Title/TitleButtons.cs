using UnityEngine;
public class TitleButtons : Interactable
{
    public enum TButtonType
    {
        Play,
        Settings,
        Quit,
        Back
    }

    [SerializeField] private TButtonType type;
    private Vector3 originalScale;
    void Start()
    {
        originalScale = transform.localScale;
    }
    public override void OnHover()
    {
        base.OnHover();
        transform.localScale = originalScale * 1.1f;
    }
    public override void OnEndHover()
    {
        base.OnEndHover();
        transform.localScale = originalScale;
    }
    public override void OnClick()
    {
        base.OnClick();

        switch (type)
        {
            case TButtonType.Play:
                if (PlayerStats.root.NewGame && GameManager.root.CurrentGState == GameState.Title)
                {
                    StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Level));
                }
                else if (GameManager.root.CurrentGState == GameState.Shop)
                {
                    StartCoroutine(NonDgUI.root.ToTalkTransition());
                }
                else StartCoroutine(NonDgUI.root.FadeToBlack(true, GameState.Shop));
                break;
            case TButtonType.Quit:
                Application.Quit();
                break;
        }
    }
}