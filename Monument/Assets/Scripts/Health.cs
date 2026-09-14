using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour, IDamageable
{
	[Header("Health Settings")]
	public float maxHealth = 100f; // Changed from private to public
	public float currentHealth { get; private set; }

	[Header("Corpse / Cleanup")]
	[Tooltip("Set to 0 or negative to keep the ragdoll body forever.")]
	public float corpseDespawnTime = 0f;

	[Header("Events")]
	public UnityEvent<float, float> OnHealthChanged;
	public UnityEvent OnDeath;

	private bool isDead = false;

	private void Awake()
	{
		currentHealth = maxHealth;
	}

	public void TakeDamage(float damage)
	{
		if (isDead) return;

		currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
		OnHealthChanged?.Invoke(currentHealth, maxHealth);

		if (currentHealth <= 0f)
		{
			Die();
		}
	}

	public void Heal(float amount)
	{
		if (isDead) return;

		currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
		OnHealthChanged?.Invoke(currentHealth, maxHealth);
	}

	private void Die()
	{
		isDead = true;
		OnDeath?.Invoke();

		// 1. If this entity has a ragdoll, do not delete it immediately
		if (GetComponent<HumanoidRagdoll>() != null)
		{
			if (corpseDespawnTime > 0f)
			{
				Destroy(gameObject, corpseDespawnTime);
			}
			return;
		}

		// 2. Fallback for non-player breakable props
		if (GetComponent<playerController>() == null)
		{
			Destroy(gameObject);
		}
	}
}