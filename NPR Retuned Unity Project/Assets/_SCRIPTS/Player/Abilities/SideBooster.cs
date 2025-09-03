using UnityEngine;

public class SideBooster : Ability
{
    protected override AbilityType Type => AbilityType.Charge;
    [SerializeField] private float boostForce;
    [SerializeField] private ParticleSystem chargeParticles;
    [SerializeField] private ParticleSystem boostParticles;
    [SerializeField] private ParticleSystem overloadParticles;
    protected override void AbilityHeld()
    {
        base.AbilityHeld();

        if (Position != _chargingPosition)
        {
            if (chargeParticles.isPlaying) chargeParticles.Stop();
            return;
        }
        if (Position == _chargingPosition)
        {
            if (!chargeParticles.isPlaying)
            {
                var main = chargeParticles.main;
                main.duration = Mathf.Max(0.025f, Mathf.Pow(AbilityMax - _abilityTimeActive, -3f));
                chargeParticles.Play();
            }
        }
    }
    protected override void ReleaseLeft(bool overloaded)
    {
        base.ReleaseLeft(overloaded);
        if (Position != AbilityPosition.Left) return;

        chargeParticles.Stop();
        /*if (overloaded) overloadParticles.Play();
        else */boostParticles.Play();

        if (!overloaded) VanController.root.PlayerRb.AddForceAtPosition(-transform.forward * boostForce * _abilityTimeActive, transform.position, ForceMode.Impulse);
    }
    protected override void ReleaseRight(bool overloaded)
    {
        base.ReleaseRight(overloaded);
        if (Position != AbilityPosition.Right) return;

        chargeParticles.Stop();
        /*if (overloaded) overloadParticles.Play();
        else*/ boostParticles.Play();

        if (!overloaded) VanController.root.PlayerRb.AddForceAtPosition(-transform.forward * boostForce * _abilityTimeActive, transform.position, ForceMode.Impulse);
    }
}
