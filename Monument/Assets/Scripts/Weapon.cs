using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class Weapon : MonoBehaviour, IInteractable
{
	[Header("Model Prefabs")]
	[Tooltip("The third-person / world model visible on the floor and to other players.")]
	public GameObject worldModelPrefab;
	[Tooltip("The first-person model instantiated in the FP camera / arms rig for the local player.")]
	public GameObject firstPersonModelPrefab;

	[Header("Sockets & Transforms")]
	[Tooltip("Where bullets and muzzle effects spawn. Can be a child or located on the world model.")]
	public Transform barrelEnd;
	[Tooltip("Target socket for the humanoid left hand IK grip.")]
	public Transform leftHandGrip;

	[Header("Ammunition & Reloading")]
	public int magazineSize = 30;
	public int currentAmmo = 30;
	public int reserveBullets = 90;
	public float reloadTime = 2.0f;
	public bool isReloading { get; private set; } = false;

	[Header("Weapon Stats")]
	public float damage = 25f;
	public float bulletSpeed = 60f;
	public float bulletLifeTime = 5f;
	public float fireRate = 0.12f;
	public bool isAutomatic = true;

	[Header("Accuracy & Spread")]
	[Tooltip("Base bullet spread angle in degrees when hipfiring while stationary.")]
	public float baseSpread = 2.5f;
	[Tooltip("Spread multiplier when aiming down sights (0 = perfect accuracy).")]
	[Range(0f, 1f)] public float scopeSpreadMultiplier = 0.1f;
	[Tooltip("Max spread multiplier when moving. Scales dynamically based on player speed.")]
	public float movementSpreadMultiplier = 2.5f;

	[Header("Prefabs")]
	public GameObject bulletPrefab;
	public GameObject muzzleFlashPrefab;

	[Header("Audio")]
	public AudioSource audioSource;
	public AudioClip fireSound;
	public AudioClip reloadSound;
	public AudioClip emptyClickSound;

	private GameObject _spawnedWorldModel;
	private Rigidbody _rb;
	private BoxCollider _triggerCollider;
	private float _nextFireTime = 0f;
	private humanoidMotor _currentOwner;
	private Coroutine _reloadCoroutine;

	public GameObject SpawnedWorldModel => _spawnedWorldModel;
	public humanoidMotor CurrentOwner => _currentOwner;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody>();
		_triggerCollider = GetComponent<BoxCollider>();
		_triggerCollider.isTrigger = true;

		if (audioSource == null) audioSource = GetComponent<AudioSource>();

		if (transform.parent == null)
		{
			EnsureWorldModel();
			ConfigureWorldPhysics(true);
		}
	}

	public GameObject EnsureWorldModel()
	{
		if (_spawnedWorldModel == null && worldModelPrefab != null)
		{
			Transform existing = transform.Find(worldModelPrefab.name);
			if (existing != null)
			{
				_spawnedWorldModel = existing.gameObject;
			}
			else
			{
				_spawnedWorldModel = Instantiate(worldModelPrefab, transform);
				_spawnedWorldModel.name = worldModelPrefab.name;
				_spawnedWorldModel.transform.localPosition = Vector3.zero;
				_spawnedWorldModel.transform.localRotation = Quaternion.identity;
			}

			int defaultLayer = LayerMask.NameToLayer("Default");
			if (defaultLayer != -1)
			{
				_spawnedWorldModel.layer = defaultLayer;
			}

			ResolveSockets(_spawnedWorldModel.transform);
		}

		return _spawnedWorldModel;
	}

	private void ResolveSockets(Transform modelTransform)
	{
		if (barrelEnd == null)
		{
			barrelEnd = modelTransform.Find("BarrelEnd");
			if (barrelEnd == null) barrelEnd = modelTransform.Find("Muzzle");
			if (barrelEnd == null) barrelEnd = transform;
		}

		if (leftHandGrip == null)
		{
			leftHandGrip = modelTransform.Find("LeftHandGrip");
			if (leftHandGrip == null) leftHandGrip = modelTransform.Find("Grip");
		}
	}

	/// <summary>
	/// Configures rigidbody physics, colliders, and layer assignment.
	/// When held (enable = false), colliders are disabled and physics silenced.
	/// When dropped (enable = true), colliders are enabled and the layer is set to "Interactable".
	/// </summary>
	public void ConfigureWorldPhysics(bool enable)
	{
		Rigidbody rb = GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.isKinematic = !enable;
			rb.detectCollisions = enable;
			if (!enable)
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
		}

		// Re-assign to the Interactable layer so playerController can detect it via SphereCast
		int targetLayer = enable ? LayerMask.NameToLayer("Interactable") : LayerMask.NameToLayer("Default");
		if (targetLayer != -1)
		{
			SetLayerRecursively(gameObject, targetLayer);
		}

		// Toggle colliders
		Collider[] colliders = GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < colliders.Length; i++)
		{
			colliders[i].enabled = enable;
		}
	}

	private void SetLayerRecursively(GameObject obj, int layer)
	{
		obj.layer = layer;
		for (int i = 0; i < obj.transform.childCount; i++)
		{
			SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
		}
	}

	// --- INTERACTION & PICKUP ---
	/// <summary>
	/// IInteractable implementation for picking up the weapon from the world.
	/// </summary>
	public void Interact(GameObject interactor)
	{
		if (interactor == null) return;

		humanoidMotor motor = interactor.GetComponent<humanoidMotor>();
		if (motor != null)
		{
			motor.EquipWeapon(this);
		}
	}

	public void SetOwner(humanoidMotor newOwner)
	{
		_currentOwner = newOwner;
	}

	public void Drop()
	{
		_currentOwner = null;

		if (_reloadCoroutine != null)
		{
			StopCoroutine(_reloadCoroutine);
			_reloadCoroutine = null;
			isReloading = false;
		}

		transform.SetParent(null);

		EnsureWorldModel();
		if (_spawnedWorldModel != null)
		{
			_spawnedWorldModel.SetActive(true);
		}

		ConfigureWorldPhysics(true);

		if (_rb != null)
		{
			_rb.linearVelocity = Vector3.zero;
			_rb.angularVelocity = Vector3.zero;
			_rb.AddForce(transform.forward * 2.5f + Vector3.up * 1.5f, ForceMode.VelocityChange);
		}
	}

	// --- FIRING & RELOADING ---

	public void ProcessInput(bool fireHeld, bool fireTrig)
	{
		if (isReloading) return;

		bool wantsToFire = isAutomatic ? fireHeld : fireTrig;

		if (wantsToFire && Time.time >= _nextFireTime)
		{
			if (currentAmmo <= 0)
			{
				if (fireTrig && audioSource != null && emptyClickSound != null)
				{
					audioSource.PlayOneShot(emptyClickSound);
				}

				if (reserveBullets > 0)
				{
					Reload();
				}
				return;
			}

			_nextFireTime = Time.time + fireRate;
			Shoot();
		}
	}

	private void Shoot()
	{
		currentAmmo--;

		Vector3 spawnPoint = barrelEnd != null ? barrelEnd.position : transform.position;
		Quaternion spawnRot = barrelEnd != null ? barrelEnd.rotation : transform.rotation;

		// --- SPREAD CALCULATION ---
		float currentSpread = baseSpread;

		if (_currentOwner != null)
		{
			// Apply scope accuracy bonus
			if (_currentOwner.isAiming)
			{
				currentSpread *= scopeSpreadMultiplier;
			}

			// Apply movement penalty
			Rigidbody ownerRb = _currentOwner.GetComponent<Rigidbody>();
			if (ownerRb != null && _currentOwner.walkSpeed > 0f)
			{
				Vector3 horizVel = new Vector3(ownerRb.linearVelocity.x, 0f, ownerRb.linearVelocity.z);

				// Calculate normalized speed (0 to 1 based on walk speed)
				float speedRatio = Mathf.Clamp01(horizVel.magnitude / _currentOwner.walkSpeed);

				// Lerp from 1.0x to movementSpreadMultiplier based on current speed
				float dynamicMoveMult = Mathf.Lerp(1f, movementSpreadMultiplier, speedRatio);
				currentSpread *= dynamicMoveMult;
			}

			// Trigger visual animation loop
			if (_currentOwner.VisualController != null)
			{
				_currentOwner.VisualController.TriggerFire();
			}
		}

		// Apply randomized angular offset for the final trajectory
		if (currentSpread > 0f)
		{
			float spreadX = Random.Range(-currentSpread, currentSpread);
			float spreadY = Random.Range(-currentSpread, currentSpread);
			spawnRot *= Quaternion.Euler(spreadX, spreadY, 0f);
		}

		// --- PROJECTILE CREATION ---
		if (bulletPrefab != null)
		{
			GameObject bulletObj = Instantiate(bulletPrefab, spawnPoint, spawnRot);
			Bullet bullet = bulletObj.GetComponent<Bullet>();

			if (bullet != null)
			{
				GameObject shooterObj = _currentOwner != null ? _currentOwner.gameObject : gameObject;
				bullet.Initialize(damage, bulletLifeTime, shooterObj);
			}

			Rigidbody bRb = bulletObj.GetComponent<Rigidbody>();
			if (bRb != null)
			{
				bRb.linearVelocity = spawnRot * Vector3.forward * bulletSpeed;
			}
		}

		// --- VISUAL & AUDIO EFFECTS ---
		if (muzzleFlashPrefab != null)
		{
			GameObject flash = Instantiate(muzzleFlashPrefab, spawnPoint, spawnRot, barrelEnd);
			Destroy(flash, 1f);
		}

		if (audioSource != null && fireSound != null)
		{
			audioSource.PlayOneShot(fireSound);
		}
	}

	public void Reload()
	{
		if (isReloading || currentAmmo >= magazineSize || reserveBullets <= 0) return;

		_reloadCoroutine = StartCoroutine(ReloadRoutine());
	}

	private IEnumerator ReloadRoutine()
	{
		isReloading = true;

		if (audioSource != null && reloadSound != null)
		{
			audioSource.PlayOneShot(reloadSound);
		}

		yield return new WaitForSeconds(reloadTime);

		int needed = magazineSize - currentAmmo;
		int toLoad = Mathf.Min(needed, reserveBullets);

		currentAmmo += toLoad;
		reserveBullets -= toLoad;

		isReloading = false;
		_reloadCoroutine = null;
	}
}