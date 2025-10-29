using UnityEngine;

public class AmbienceManager : MonoBehaviour
{
    void Start()
    {
        AudioManager.root.PlaySound(AudioEvent.playWindAmbience, gameObject);
    }

    void Update()
    {
        AudioManager.root.SetRTPC(AudioRTPC.Player_Speed, VanController.root.PlayerRb.linearVelocity.magnitude);
    }
}