using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bullet : MonoBehaviour
{
	[Header("Shooter Exclusion")]
	[SerializeField] private GameObject shooter;

	[Header("Decal Fallback")]
	[SerializeField] private float defaultDecalLifetime = 20f;

	private float damage;
	private float lifeTime;
	private bool hasHit = false;
	private Collider _myCollider;

	private void Awake()
	{
		_myCollider = GetComponent<Collider>();
	}

	private void Start()
	{
		// 1. Ignore root movement capsules so bullets penetrate to ragdoll hitboxes
		if (_myCollider != null)
		{
			foreach (Collider moveCol in humanoidMotor.MovementColliders)
			{
				if (moveCol != null)
				{
					Physics.IgnoreCollision(_myCollider, moveCol, true);
				}
			}
		}

		// 2. Auto-detect shooter if not explicitly assigned upon instantiation
		if (shooter == null)
		{
			AutoDetectShooter();
		}
	}

	public void Initialize(float bulletDamage, float maxLifeTime, GameObject bulletShooter = null)
	{
		damage = bulletDamage;
		lifeTime = maxLifeTime;

		if (bulletShooter != null)
		{
			SetShooter(bulletShooter);
		}

		Destroy(gameObject, lifeTime);
	}

	public void SetShooter(GameObject owner)
	{
		shooter = owner;

		if (_myCollider == null) _myCollider = GetComponent<Collider>();
		if (_myCollider != null && shooter != null)
		{
			Collider[] shooterCols = shooter.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < shooterCols.Length; i++)
			{
				if (shooterCols[i] != null)
				{
					Physics.IgnoreCollision(_myCollider, shooterCols[i], true);
				}
			}
		}
	}

	private void AutoDetectShooter()
	{
		Collider[] nearby = Physics.OverlapSphere(transform.position, 2.5f);
		float closestDist = float.MaxValue;
		humanoidMotor closestMotor = null;

		for (int i = 0; i < nearby.Length; i++)
		{
			humanoidMotor m = nearby[i].GetComponentInParent<humanoidMotor>();
			if (m != null && m.currentWeapon != null)
			{
				float dist = Vector3.Distance(transform.position, m.currentWeapon.transform.position);
				if (dist < closestDist && dist < 2.0f)
				{
					closestDist = dist;
					closestMotor = m;
				}
			}
		}

		if (closestMotor != null)
		{
			SetShooter(closestMotor.gameObject);
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (hasHit) return;

		// Guard: Do not allow self-damage
		if (shooter != null)
		{
			if (collision.transform == shooter.transform || collision.transform.IsChildOf(shooter.transform))
			{
				return;
			}
		}

		// Guard: Pass through movement capsule to reach ragdoll hitboxes
		if (humanoidMotor.MovementColliders.Contains(collision.collider))
		{
			Physics.IgnoreCollision(_myCollider, collision.collider, true);
			return;
		}

		hasHit = true;

		// Apply damage to hit entity
		IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
		if (damageable != null)
		{
			damageable.TakeDamage(damage);

			Vector3 impulse = transform.forward * (damage * 0.5f);
			ContactPoint contact = collision.GetContact(0);

			if (collision.rigidbody != null && !collision.rigidbody.isKinematic)
			{
				collision.rigidbody.AddForceAtPosition(impulse, contact.point, ForceMode.Impulse);
			}
			else
			{
				HumanoidVisualController visual = collision.collider.GetComponentInParent<HumanoidVisualController>();
				if (visual != null)
				{
					visual.ApplyImpulseToBone(impulse, contact.point);
				}
			}
		}

		// Spawn impact visual and parent decal to the hit collider
		if (GlobalReferences.Instance != null && GlobalReferences.Instance.bulletImpactEffectPrefab != null)
		{
			ContactPoint contact = collision.GetContact(0);
			SpawnImpactEffect(contact, collision.collider);
		}

		Destroy(gameObject);
	}

	private void SpawnImpactEffect(ContactPoint contact, Collider hitCollider)
	{
		Quaternion rot = Quaternion.LookRotation(contact.normal);
		GameObject impactObj = Instantiate(
			GlobalReferences.Instance.bulletImpactEffectPrefab,
			contact.point,
			rot
		);

		// Route through ImpactEffect component if present
		ImpactEffect effectComp = impactObj.GetComponent<ImpactEffect>();
		if (effectComp != null)
		{
			effectComp.Initialize(hitCollider.transform, contact.point, contact.normal);
			return;
		}

		// Fallback separation: search for child decal and parent it to the hit collider
		Transform decal = impactObj.transform.Find("Decal");
		if (decal == null)
		{
			foreach (Transform child in impactObj.transform)
			{
				if (child.name.IndexOf("decal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
					child.GetComponent("DecalProjector") != null ||
					child.GetComponent<Projector>() != null)
				{
					decal = child;
					break;
				}
			}
		}

		if (decal != null && hitCollider != null)
		{
			decal.SetParent(hitCollider.transform, true);
			Destroy(decal.gameObject, defaultDecalLifetime);
		}

		Destroy(impactObj, 2f);
	}
}