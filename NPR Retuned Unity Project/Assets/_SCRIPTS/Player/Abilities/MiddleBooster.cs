using UnityEngine;

public class MiddleBooster : Ability
{
    protected override AbilityType Type => AbilityType.Continuous;
    [SerializeField] private float boostForce;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private ParticleSystem overloadParticles;
    protected override void AbilityHeld()
    {  
        base.AbilityHeld();

        if (Position != _chargingPosition)
        {
            //if (boostParticles.isPlaying) boostParticles.Stop();
            return;
        }

        //if (!boostParticles.isPlaying) boostParticles.Play();

        VanController.root.PlayerRb.AddForceAtPosition(transform.forward * boostForce, transform.position, ForceMode.Force);
    }
    protected override void ReleaseMiddle(bool overloaded)
    {
        base.ReleaseMiddle(overloaded);
        if (Position != AbilityPosition.Middle) return;

        //if (overloaded) overloadParticles.Play();
    }
}