using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bullet : MonoBehaviour
{
	[Header("Shooter Exclusion")]
	[SerializeField] private GameObject shooter;

	[Header("Impact & Visual Prefabs")]
	[Tooltip("Prefab spawned on standard solid penetration impacts (bullet hole decal, dust, sparks).")]
	[SerializeField] private GameObject impactEffectPrefab;
	[Tooltip("Prefab spawned at the bounce point, oriented along the reflected bullet path (sparks with trails).")]
	[SerializeField] private GameObject ricochetSparksPrefab;

	[Header("Ricochet Angle & Limits")]
	[Tooltip("Minimum glancing angle with the surface plane to allow a ricochet (0° is parallel to the surface).")]
	[Range(0f, 90f)] public float minRicochetAngle = 0f;
	[Tooltip("Maximum glancing angle with the surface plane to allow a ricochet. Hits steeper than this will embed/impact.")]
	[Range(0f, 90f)] public float maxRicochetAngle = 28f;
	[Tooltip("How many times the bullet can bounce before penetrating/lodging.")]
	public int maxRicochets = 2;
	[Tooltip("Velocity retained after each bounce.")]
	[Range(0.1f, 1f)] public float ricochetSpeedRetention = 0.75f;
	[Tooltip("Damage multiplier retained after each bounce.")]
	[Range(0.1f, 1f)] public float ricochetDamageMultiplier = 0.6f;
	public AudioClip ricochetSound;

	[Header("Impact Settings")]
	[SerializeField] private float defaultDecalLifetime = 20f;

	private float damage;
	private float lifeTime;
	private int ricochetCount = 0;
	private bool hasHit = false;
	private Collider _myCollider;
	private Rigidbody _rb;

	private void Awake()
	{
		_myCollider = GetComponent<Collider>();
		_rb = GetComponent<Rigidbody>();
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

		// Guard: Pass through movement capsule to reach ragdoll bone hitboxes
		if (humanoidMotor.MovementColliders.Contains(collision.collider))
		{
			Physics.IgnoreCollision(_myCollider, collision.collider, true);
			return;
		}

		ContactPoint contact = collision.GetContact(0);

		// Calculate glancing angle with the surface plane (0° = parallel along surface, 90° = direct head-on)
		Vector3 incomingDir = transform.forward;
		float angleWithNormal = Vector3.Angle(-incomingDir, contact.normal);
		float grazingAngle = 90f - angleWithNormal;

		// Ricochet check
		if (ricochetCount < maxRicochets && grazingAngle >= minRicochetAngle && grazingAngle <= maxRicochetAngle)
		{
			PerformRicochet(contact, collision);
			return;
		}

		// Direct Solid Impact
		hasHit = true;
		ApplyImpactDamage(collision, contact);
		SpawnImpactEffect(contact, collision.collider);

		Destroy(gameObject);
	}

	private void PerformRicochet(ContactPoint contact, Collision collision)
	{
		ricochetCount++;

		// 1. Calculate reflected trajectory
		Vector3 incomingDir = transform.forward;
		Vector3 reflectedDir = Vector3.Reflect(incomingDir, contact.normal).normalized;

		// 2. Deal partial damage if grazing a character/entity
		IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
		if (damageable != null)
		{
			damageable.TakeDamage(damage * ricochetDamageMultiplier);
		}
		damage *= ricochetDamageMultiplier;

		// 3. Spawn the spark trail emitter at the ricochet site pointing along the reflected angle
		if (ricochetSparksPrefab != null)
		{
			Vector3 sparkSpawnPos = contact.point + contact.normal * 0.02f;
			GameObject sparks = Instantiate(
				ricochetSparksPrefab,
				sparkSpawnPos,
				Quaternion.LookRotation(reflectedDir)
			);
			Destroy(sparks, 2.5f);
		}

		// 4. Audio
		if (ricochetSound != null)
		{
			AudioSource.PlayClipAtPoint(ricochetSound, contact.point, 0.8f);
		}

		// 5. Reposition bullet and apply reflected velocity
		transform.position = contact.point + contact.normal * 0.05f;
		transform.rotation = Quaternion.LookRotation(reflectedDir);

		float speed = _rb != null ? _rb.linearVelocity.magnitude : 60f;
		speed = Mathf.Max(speed * ricochetSpeedRetention, 20f);

		if (_rb != null)
		{
			_rb.linearVelocity = reflectedDir * speed;
		}
	}

	private void ApplyImpactDamage(Collision collision, ContactPoint contact)
	{
		IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
		if (damageable != null)
		{
			damageable.TakeDamage(damage);

			Vector3 impulse = transform.forward * (damage * 0.5f);

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
	}

	private void SpawnImpactEffect(ContactPoint contact, Collider hitCollider)
	{
		GameObject prefabToSpawn = impactEffectPrefab;

		// Optional fallback if not assigned directly on this bullet
		if (prefabToSpawn == null && GlobalReferences.Instance != null)
		{
			prefabToSpawn = GlobalReferences.Instance.bulletImpactEffectPrefab;
		}

		if (prefabToSpawn == null) return;

		Quaternion rot = Quaternion.LookRotation(contact.normal);
		GameObject impactObj = Instantiate(prefabToSpawn, contact.point, rot);

		ImpactEffect effectComp = impactObj.GetComponent<ImpactEffect>();
		if (effectComp != null)
		{
			effectComp.Initialize(hitCollider.transform, contact.point, contact.normal);
			return;
		}

		// Fallback decal parenting if not using ImpactEffect component
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