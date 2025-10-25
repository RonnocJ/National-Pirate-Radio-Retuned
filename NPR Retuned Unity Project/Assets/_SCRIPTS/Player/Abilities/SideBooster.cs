using UnityEngine;

public class SideBooster : AbilityDefinition
{
    public override AbilityType Type => AbilityType.Charge;
    [SerializeField] private float boostForce;
    [SerializeField] private ParticleSystem chargeParticles;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private ParticleSystem overloadParticles;
    public override void AbilityHeld(float currentTime)
    {
        base.AbilityHeld(currentTime);

        if (!chargeParticles.isPlaying) chargeParticles.Play();

        var emission = chargeParticles.emission;
        emission.rateOverTime = currentTime * 3;

        var childEmission = chargeParticles.transform.GetChild(0).GetComponent<ParticleSystem>().emission;
        childEmission.rateOverTime = 10 + (currentTime * 30);
    }
    public override void AbilityRelease(bool overloaded, float currentTime)
    {
        Debug.Log(currentTime);
        
        if (currentTime > 0)
        {
            base.AbilityRelease(overloaded, currentTime);
            boostParticles.Play();

            if (!overloaded) VanController.root.PlayerRb.AddForceAtPosition(transform.forward * boostForce * currentTime, transform.position, ForceMode.Impulse);
        }

        chargeParticles.Stop();
    }
}
