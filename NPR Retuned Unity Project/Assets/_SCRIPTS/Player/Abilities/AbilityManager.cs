using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
public enum AbilityPosition
{
    None = -1,
    Left = 0,
    Right = 1,
    Back = 2
}
[Serializable]
public class Ability
{
    public AbilityDefinition Def;
    public float FillAmount
    {
        get => _fillAmount;
        set
        {
            if (value != _fillAmount) SetFillAmount(value);
            _fillAmount = value;
        }
    }
    private float _fillAmount;
    public Transform MaxTr;
    [SerializeField] private Animator anim;
    [SerializeField] private GlyphTextRenderer text;
    [SerializeField] private MeshRenderer meterRenderer;

    private void SetFillAmount(float newFillAmount)
    {
        MaterialPropertyBlock mpBlock = new MaterialPropertyBlock();
        mpBlock.SetFloat("_Fill", Mathf.Clamp(Mathf.Lerp(FillAmount, newFillAmount, Time.deltaTime * 2.5f), 0, 1));
        meterRenderer.SetPropertyBlock(mpBlock);
        if (newFillAmount < FillAmount && newFillAmount > 0f)
        {
            text.SetText(Mathf.FloorToInt((newFillAmount + Def.CooldownRate) / Def.CooldownRate).ToString());
            anim.SetBool("maxOn", false);
        }
        else if (newFillAmount > FillAmount || newFillAmount <= 0f)
        {
            text.SetText(Def.Pos.ToString()[0].ToString());

            if (newFillAmount >= 1) anim.SetTrigger("overloadOn");
            else if (newFillAmount > Def.AbilityMax / Def.AbilityOverload) anim.SetBool("maxOn", true);
            else anim.SetBool("maxOn", false);
        }
    }
}
public class AbilityManager : MonoBehaviour
{
    public Ability[] Abilities;
    private float _inputActiveTime;
    private Vector2 _input => PInputManager.root.actions[PlayerActionType.Ability].v2Value;
    private Coroutine _inputRoutine;
    [SerializeField] private AbilityPosition _heldPos;
    [SerializeField] private AbilityDefinition _heldDef;
    void Start()
    {
        if (!GameManager.root.NewGame) RegsiterAbilityInputs();

        foreach (var a in Abilities)
        {
            if (a.Def.Type == AbilityType.Charge) a.MaxTr.localEulerAngles = Vector3.forward * (360f * (1 - (a.Def.AbilityMax / a.Def.AbilityOverload)));
            else a.MaxTr.localEulerAngles = Vector3.zero;
        }
    }
    public void RegsiterAbilityInputs()
    {
        PInputManager.root.actions[PlayerActionType.Ability].onV2ValueChange += OnInputChange;
    }
    private void OnInputChange(Vector2 newInput)
    {
        if(_inputRoutine == null) _inputRoutine = StartCoroutine(HandleInputs());
    }
    private IEnumerator HandleInputs()
    {
        yield return new WaitForSeconds(0.05f);
        if (_input != Vector2.zero) _inputActiveTime = 0.05f;

        _heldPos = GetInputPos(_input);

        if (_heldPos != AbilityPosition.None)
        {
            _heldDef = Abilities[(int)_heldPos].Def;

            switch (_heldDef.Type)
            {
                case AbilityType.Charge:
                    StartCoroutine(ChargeAbility(_heldPos));
                    break;
                case AbilityType.Single:
                    StartCoroutine(FireSingleAbility(_heldPos));
                    break;
                case AbilityType.Continuous:
                    StartCoroutine(ActivateContinuousAbility(_heldPos));
                    break;
            }
        }

        _inputRoutine = null;
    }
    private IEnumerator ChargeAbility(AbilityPosition inPos)
    {
        var cachedAb = Abilities[(int)inPos];

        if (Abilities[(int)inPos].FillAmount > 0f || !InputEqualsPos(inPos)) yield break;

        while (InputEqualsPos(inPos) && _inputActiveTime < cachedAb.Def.AbilityOverload)
        {
            cachedAb.Def.AbilityHeld(_inputActiveTime);

            Abilities[(int)inPos].FillAmount = _inputActiveTime / cachedAb.Def.AbilityOverload;
            _inputActiveTime += Time.deltaTime;

            yield return null;
        }

        if (!InputEqualsPos(inPos) || _inputActiveTime >= cachedAb.Def.AbilityOverload) cachedAb.Def.AbilityRelease(_inputActiveTime >= cachedAb.Def.AbilityOverload, _inputActiveTime);

        if (_input == Vector2.zero)
        {
            _heldDef = null;
            _inputActiveTime = 0;
        }

        while (cachedAb.FillAmount > 0f)
        {
            cachedAb.FillAmount -= Time.deltaTime * cachedAb.Def.CooldownRate;
            yield return null;
        }
    }
    private IEnumerator FireSingleAbility(AbilityPosition inPos)
    {
        var cachedAb = Abilities[(int)inPos];

        if (!InputEqualsPos(inPos)) yield break;

        while (InputEqualsPos(inPos) && _inputActiveTime > _heldDef.MaxActiveTime)
        {

        }
    }
    private IEnumerator ActivateContinuousAbility(AbilityPosition inPos)
    {
        var cachedAb = Abilities[(int)inPos];

        if (cachedAb.FillAmount > 0f || !InputEqualsPos(inPos)) yield break;

        while (InputEqualsPos(inPos) && _inputActiveTime < cachedAb.Def.MaxActiveTime)
        {
            cachedAb.Def.AbilityHeld(_inputActiveTime);

            cachedAb.FillAmount = _inputActiveTime / cachedAb.Def.MaxActiveTime;
            _inputActiveTime += Time.deltaTime;

            yield return null;
        }

        cachedAb.Def.AbilityRelease(_inputActiveTime >= cachedAb.Def.MaxActiveTime, _inputActiveTime);

        float ogCooldown = cachedAb.Def.CooldownRate;

        if (_inputActiveTime < cachedAb.Def.MaxActiveTime) cachedAb.Def.CooldownRate = cachedAb.Def.MaxActiveTime;

        if (_input == Vector2.zero)
        {
            _heldDef = null;
            _inputActiveTime = 0;
        }

        while (cachedAb.FillAmount > 0f)
        {
            cachedAb.FillAmount -= Time.deltaTime / cachedAb.Def.CooldownRate;
            yield return null;
        }

        cachedAb.Def.CooldownRate = ogCooldown;
    }
    private AbilityPosition GetInputPos(Vector2 newInput)
    {
        if (newInput.y > 0.75f) { return AbilityPosition.Back; }
        else if (newInput.x > 0f) return AbilityPosition.Right;
        else if (newInput.x < 0f) return AbilityPosition.Left;
        else return AbilityPosition.None;
    }
    private bool InputEqualsPos(AbilityPosition pos)
    {
        switch (pos)
        {
            case AbilityPosition.Left:
                return _input.x < 0f;
            case AbilityPosition.Right:
                return _input.x > 0f;
            case AbilityPosition.Back:
                return _input.y > 0.75f;
            case AbilityPosition.None:
                return _input == Vector2.zero;
        }

        return false;
    }
}
