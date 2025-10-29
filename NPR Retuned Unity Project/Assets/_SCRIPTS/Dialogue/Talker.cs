using System.IO;
using UnityEditor;
using UnityEngine;

public class Talker : MonoBehaviour
{
    public bool StartedTalking;
    public bool OnRight;
    public bool Obscured;
    public Characters CharName;
    public Animator Anim;
    public void BeginDialogue()
    {
        if (Obscured) GetComponentInChildren<MeshRenderer>().sharedMaterial.color = Color.black;
        else GetComponentInChildren<MeshRenderer>().sharedMaterial.color = Color.white;

        Anim.SetBool("right", OnRight);
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

#if UNITY_EDITOR

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
        if (t.Anim != null) t.SetTalking(toggle);

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }
}

#endif