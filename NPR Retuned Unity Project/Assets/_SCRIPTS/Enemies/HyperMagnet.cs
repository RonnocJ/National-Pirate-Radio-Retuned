using UnityEngine;

public class HyperMagnet : Enemy
{
    [SerializeField] private float magnetRange;
    [SerializeField] private float magnetPull;
    [SerializeField] private LayerMask pullMask;
    public override void Spawn()
    {
        base.Spawn();
        AudioManager.root.PlaySound(AudioEvent.playMagnetFloat, gameObject);
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        
        if (_destroyed) return;

        var cols = Physics.OverlapSphere(transform.position, magnetRange, pullMask);

        foreach (var c in cols)
        {
            if (c.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce((transform.position - rb.position).normalized * magnetPull, ForceMode.Acceleration);
            }
        }
    }

    public override void DestroyEnemy(bool killedByPlayer)
    {
        AudioManager.root.StopSound(AudioEvent.playMagnetFloat, gameObject);
        GetComponentInChildren<ParticleSystem>().Stop();
        base.DestroyEnemy(killedByPlayer);
    }
}