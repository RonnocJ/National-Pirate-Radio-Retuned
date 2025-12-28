using System.Collections;
using UnityEngine;

public class Tutorial : Singleton<Tutorial>
{
    public int Iteration;
    [SerializeField, TextArea(5, 10)] private string[] texts;
    [SerializeField] private Animator anim;
    [SerializeField] private GlyphTextRenderer frontText;
    [SerializeField] private GlyphTextRenderer backText;
    [SerializeField] private Transform frontFillBar;
    [SerializeField] private Transform backFillBar;
    [SerializeField] private CameraManager cam;
    [SerializeField] private AbilityManager ab;
    private Coroutine tutorialRoutine;
    private void Start()
    {
        GameManager.root.OnPStateSwitch += state =>
        {
            if (PlayerStats.root.NewGame && state == PlayerState.Utility && tutorialRoutine == null) tutorialRoutine = StartCoroutine(PlayTutorial());
        };
    }
    private IEnumerator PlayTutorial()
    {
        //Learn Move input

        anim.gameObject.SetActive(true);
        anim.SetTrigger("enter");

        frontText.SetText(texts[Iteration], 0);

        while (frontFillBar.localScale.x <= 0.325f)
        {
            frontFillBar.localScale += Vector3.right * (Mathf.Abs(PInputManager.root.actions[PlayerActionType.Drive].v2Value.x) * Time.deltaTime * 0.1f);
            frontFillBar.localScale += Vector3.right * (Mathf.Abs(PInputManager.root.actions[PlayerActionType.Drive].v2Value.y) * Time.deltaTime * 0.1f);

            yield return null;
        }

        yield return new WaitForSeconds(1f);

        //Learn Switch input

        Iteration = 1;
        anim.SetTrigger("switchBack");
        backText.SetText(texts[Iteration], 0);

        cam.RegisterSwitchInputs();
        PInputManager.root.actions[PlayerActionType.Switch].bAction += AddSwitchFill;

        while (backFillBar.localScale.x <= 0.325f)
        {
            yield return null;
        }

        PInputManager.root.actions[PlayerActionType.Switch].bAction -= AddSwitchFill;

        yield return new WaitForSeconds(1f);
        
        //Learn Weapon input

        Iteration = 2;
        anim.SetTrigger("switchFront");
        frontText.SetText(texts[Iteration], 0);
        frontFillBar.localScale -= Vector3.right * frontFillBar.localScale.x;

        while (frontFillBar.localScale.x <= 0.325f)
        {
            frontFillBar.localScale += Vector3.right * (Mathf.Abs(PInputManager.root.actions[PlayerActionType.Look].v2Value.x) * Time.deltaTime * 0.025f);
            frontFillBar.localScale += Vector3.right * (Mathf.Abs(PInputManager.root.actions[PlayerActionType.Look].v2Value.y) * Time.deltaTime * 0.025f);
            frontFillBar.localScale += Vector3.right * PInputManager.root.actions[PlayerActionType.Action].fValue * Time.deltaTime * 0.1f;

            yield return null;
        }

        yield return new WaitForSeconds(1f);

        //Learn Autopilot input
        
        Iteration = 3;
        anim.SetTrigger("switchBack");
        backText.SetText(texts[Iteration], 0);
        backFillBar.localScale -= Vector3.right * backFillBar.localScale.x;

        VanController.root.RegisterAutopilotActions();
        PInputManager.root.actions[PlayerActionType.Find].bAction += AddFindFill;

        while (backFillBar.localScale.x <= 0.325f)
        {
            yield return null;
        }

        PInputManager.root.actions[PlayerActionType.Find].bAction -= AddFindFill;

        yield return new WaitForSeconds(1f);
        
        //Learn Disc Find input

        Iteration = 4;
        anim.SetTrigger("switchFront");
        frontText.SetText(texts[Iteration], 0);
        frontFillBar.localScale -= Vector3.right * frontFillBar.localScale.x;

        while (frontFillBar.localScale.x <= 0.325f)
        {
            if(GameManager.root.CurrentPState == PlayerState.Utility) frontFillBar.localScale += Vector3.right * PInputManager.root.actions[PlayerActionType.Find].fValue * Time.deltaTime * 0.109f;

            yield return null;
        }

        yield return new WaitForSeconds(1f);

        //Learn Disc Insert input
        
        Iteration = 5;
        anim.SetTrigger("switchBack");
        backText.SetText(texts[Iteration], 0);
        backFillBar.localScale -= Vector3.right * backFillBar.localScale.x;

        InsertSlot.OnSongInserted += _ => { if(Iteration < 6) AddDiscFill(); };

        while (backFillBar.localScale.x <= 0.325f)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);
        
        //Learn Ability input

        Iteration = 6;
        anim.SetTrigger("switchFront");
        frontText.SetText(texts[Iteration], 0);
        frontFillBar.localScale -= Vector3.right * frontFillBar.localScale.x;

        ab.RegsiterAbilityInputs();

        while (frontFillBar.localScale.x <= 0.325f)
        {
            frontFillBar.localScale += Vector3.right * Mathf.Abs(PInputManager.root.actions[PlayerActionType.Ability].v2Value.x) * Time.deltaTime * 0.1f;
            frontFillBar.localScale += Vector3.right * Mathf.Abs(PInputManager.root.actions[PlayerActionType.Ability].v2Value.y) * Time.deltaTime * 0.1f;

            yield return null;
        }

        anim.SetTrigger("exit");
        PlayerStats.root.NewGame = false;
    }
    private void AddSwitchFill()
    {
        backFillBar.localScale += Vector3.right * 0.109f;
    }

    private void AddFindFill()
    {
        backFillBar.localScale += Vector3.right * 0.1626f;
    }

    public void AddDiscFill()
    {
        backFillBar.localScale += Vector3.right * 0.1626f;
    }
}