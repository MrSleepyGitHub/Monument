using UnityEngine;

[DisallowMultipleComponent]
public class ImpactEffect : MonoBehaviour
{
	[Header("Child References")]
	[Tooltip("Child transform containing the Decal Projector or Mesh. If unassigned, automatically detects any child with 'decal' in its name or a projector component.")]
	[SerializeField] private Transform decalTransform;
	[SerializeField] private ParticleSystem[] particleSystems;

	[Header("Decal Settings")]
	[Tooltip("How long the bullet hole stays on the surface before being destroyed.")]
	[SerializeField] private float decalLifetime = 20f;

	private void Awake()
	{
		if (particleSystems == null || particleSystems.Length == 0)
		{
			particleSystems = GetComponentsInChildren<ParticleSystem>(true);
		}

		if (decalTransform == null)
		{
			AutoDetectDecal();
		}
	}

	private void AutoDetectDecal()
	{
		Transform d = transform.Find("Decal");
		if (d != null)
		{
			decalTransform = d;
			return;
		}

		foreach (Transform child in transform)
		{
			if (child.name.IndexOf("decal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
				child.GetComponent("DecalProjector") != null ||
				child.GetComponent<Projector>() != null)
			{
				decalTransform = child;
				return;
			}
		}
	}

	/// <summary>
	/// Parents the decal to the hit collider and lets world particles play independently.
	/// </summary>
	public void Initialize(Transform hitTarget, Vector3 hitPoint, Vector3 hitNormal)
	{
		if (decalTransform == null) AutoDetectDecal();

		// 1. Detach decal from particle hierarchy and parent to the struck collider/bone
		if (decalTransform != null && hitTarget != null)
		{
			decalTransform.SetParent(hitTarget, true);
			Destroy(decalTransform.gameObject, decalLifetime);
		}

		// 2. Play particle systems in world space
		float maxParticleDuration = 1.5f;
		if (particleSystems != null && particleSystems.Length > 0)
		{
			for (int i = 0; i < particleSystems.Length; i++)
			{
				if (particleSystems[i] != null)
				{
					particleSystems[i].Play();
					float totalDuration = particleSystems[i].main.duration + particleSystems[i].main.startLifetime.constantMax;
					if (totalDuration > maxParticleDuration)
					{
						maxParticleDuration = totalDuration;
					}
				}
			}
		}

		// 3. Destroy particle root once emissions conclude
		Destroy(gameObject, maxParticleDuration);
	}
}