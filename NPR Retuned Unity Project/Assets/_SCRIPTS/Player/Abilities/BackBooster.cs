using UnityEngine;

public class BackBooster : AbilityDefinition
{
    public override AbilityType Type => AbilityType.Continuous;
    [SerializeField] private float boostForce;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private ParticleSystem overloadParticles;
    public override void AbilityHeld(float currentTime)
    {
        base.AbilityHeld(currentTime);

        if (!boostParticles.isPlaying) boostParticles.Play();

        VanController.root.PlayerRb.AddForceAtPosition(transform.forward * boostForce, transform.position, ForceMode.Force);
    }
    public override void AbilityRelease(bool overloaded, float currentTime)
    {
        if(currentTime > 0) base.AbilityRelease(overloaded, currentTime);

        boostParticles.Stop();
    }
}