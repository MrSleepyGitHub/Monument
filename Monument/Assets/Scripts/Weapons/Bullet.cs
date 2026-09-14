using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bullet : MonoBehaviour
{
	private float damage;
	private float lifeTime;
	private bool hasHit = false;

	public void Initialize(float bulletDamage, float maxLifeTime)
	{
		damage = bulletDamage;
		lifeTime = maxLifeTime;
		Destroy(gameObject, lifeTime);
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (hasHit) return;
		hasHit = true;

		// Apply damage if the target implements IDamageable
		IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
		if (damageable != null)
		{
			damageable.TakeDamage(damage);

			// If target has a ragdoll system, apply the bullet's kinetic impulse
			HumanoidRagdoll ragdoll = collision.collider.GetComponentInParent<HumanoidRagdoll>();
			if (ragdoll != null)
			{
				Vector3 force = transform.forward * (damage * 0.5f);
				ragdoll.ApplyImpulseToBone(force, collision.contacts[0].point);
			}
		}

		// Spawn impact visual at the point of contact
		if (GlobalReferences.Instance != null && GlobalReferences.Instance.bulletImpactEffectPrefab != null)
		{
			ContactPoint contact = collision.GetContact(0);
			Instantiate(
				GlobalReferences.Instance.bulletImpactEffectPrefab,
				contact.point,
				Quaternion.LookRotation(contact.normal)
			);
		}

		Destroy(gameObject);
	}
}