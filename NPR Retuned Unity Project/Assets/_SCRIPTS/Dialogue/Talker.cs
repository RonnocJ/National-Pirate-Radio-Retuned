using System.IO;
using UnityEditor;
using UnityEngine;
public enum CharOpinion
{
    Positive,
    Neutral,
    Negative
}
public class Talker : MonoBehaviour
{
    public CharOpinion Opinion;
    public Animator Anim;
    public void BeginDialogue()
    {
        Anim.SetTrigger("enter");
    }
    public void SetTalking(bool toggle)
    {
        Anim.SetBool("talking", toggle);
    }
    public void EndDialogue()
    {
        Anim.SetTrigger("exit");
    }
    public void EndDialogueToLevel()
    {
        Anim.SetTrigger("exitLevel");
    }
}

[CustomEditor(typeof(Talker))]
public class TalkerEditor : Editor
{
    static private bool toggle;
    public override void OnInspectorGUI()
    {
        var t = target as Talker;

        GUILayout.BeginVertical();
        base.OnInspectorGUI();
        EditorGUILayout.Space(20);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Play Enter Animation"))
        {
            t.BeginDialogue();
        }

        if (GUILayout.Button("Play Exit Animation"))
        {
            t.EndDialogue();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.Space(20);

        toggle = GUILayout.Toggle(toggle, "Toggle Talking");
        if(t.Anim != null) t.SetTalking(toggle);

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }
}
