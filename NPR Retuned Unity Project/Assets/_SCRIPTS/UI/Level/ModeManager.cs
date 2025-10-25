using System.Collections;
using UnityEngine;

public class ModeManager : MonoBehaviour
{
    [SerializeField] private float flareIntensity;
    [SerializeField] private GlyphTextRenderer text;
    [SerializeField] private Material levelDisplayMat;
    private bool firstSwitch = true;
    void Start()
    {
        GameManager.root.OnPStateSwitch += SwitchText;
    }

    void SwitchText(PlayerState newState)
    {
        if (!firstSwitch && newState == PlayerState.Utility)
        {
            text.SetText("Cam 1:\nUtility Mode", 0);
            StopAllCoroutines();
            StartCoroutine(SetStaticFlare());
        }
        else if (newState == PlayerState.Weapon)
        {
            text.SetText("Cam 2:\nWeapons Mode", 0);
            StopAllCoroutines();
            StartCoroutine(SetStaticFlare());
        }
        else
        {
            firstSwitch = false;
        }
    }
    
    IEnumerator SetStaticFlare()
    {
        float staticIntensity = levelDisplayMat.GetFloat("_NoiseIntensity");
        for (int i = 0; i <= 12; i++)
        {
            levelDisplayMat.SetFloat("_NoiseIntensity", staticIntensity + ((-Mathf.Abs(i - 6) + 6) * flareIntensity));
            yield return null;
        }
    }
}