using System.Collections.Generic;
using UnityEngine;

public class DamageParticle : MonoBehaviour
{
    [SerializeField] private bool useVelocity;
    [SerializeField] private float damage;
    [SerializeField] private float velocityScaling;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private List<ParticleCollisionEvent> collisions = new();
    void OnParticleCollision(GameObject obj)
    {
        int numColEvs = particles.GetCollisionEvents(obj, collisions);

        for (int i = 0; i < numColEvs; i++)
        {
            if (!collisions[i].colliderComponent.TryGetComponent(out VanDamage v)) continue;

            v.DealDamage(damage * (useVelocity? collisions[i].velocity.magnitude * velocityScaling : 1));
        }
    }
}