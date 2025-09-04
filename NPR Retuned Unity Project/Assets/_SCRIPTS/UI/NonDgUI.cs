using UnityEngine;

public class NonDgUI : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(this);
    }
}