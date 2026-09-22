using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class humanoidMotor : MonoBehaviour, IDamageable
{
	public enum PlayerStance { Standing = 0, Crouching = 1, Proning = 2 }

	// Static registry of all root locomotion colliders
	public static readonly HashSet<Collider> MovementColliders = new HashSet<Collider>();

	[Header("Status & Team")]
	[Tooltip("Entities with an identical team ID are considered friendly allies.")]
	public int teamId = 0;
	public float maxHealth = 100f;
	public float currentHealth { get; private set; }
	[Tooltip("Seconds before corpse is destroyed. 0 or negative keeps the ragdoll permanently.")]
	public float corpseDespawnTime = 0f;
	public bool isDead { get; private set; } = false;

	[Header("Status Events")]
	public UnityEvent<float, float> OnHealthChanged;
	public UnityEvent OnDeath;

	[Header("Controller Authority")]
	[SerializeField] private MonoBehaviour currentController;
	[SerializeField] private DummyController fallbackDummy;

	[Header("Hierarchy Transforms")]
	public Transform bodyGeometry;
	public Transform groundCheck;
	public Transform camHolder;
	public Transform camGimbal;
	public Transform aimTarget;
	public Camera playerCamera;
	public AudioListener cameraListener;
	public Transform weaponHolder;

	[Header("Locomotion Speeds")]
	public float walkSpeed = 4.5f;
	public float sprintSpeed = 7.5f;
	public float crouchSpeed = 2.5f;
	public float proneSpeed = 1.2f;
	public float acceleration = 25f;
	public float airControlMultiplier = 0.3f;
	public float jumpForce = 6.5f;

	[Header("Ground & Slope Detection")]
	public LayerMask groundMask;
	public float groundCheckRadius = 0.3f;
	public float maxSlopeAngle = 45f;

	[Header("Stance Heights")]
	public float standingHeight = 2.0f;
	public float crouchingHeight = 1.3f;
	public float proningHeight = 0.6f;
	public Vector3 standingCamLocalPos = new Vector3(0f, 1.7f, 0f);
	public Vector3 crouchingCamLocalPos = new Vector3(0f, 1.0f, 0f);
	public Vector3 proningCamLocalPos = new Vector3(0f, 0.35f, 0f);
	public float stanceTransitionSpeed = 8f;

	[Header("Aim Settings")]
	public float maxAimDistance = 100f;
	public LayerMask aimHitMask;
	public float minPitch = -85f;
	public float maxPitch = 85f;

	private CapsuleCollider _playerCollider;
	public CapsuleCollider playerCollider
	{
		get
		{
			if (_playerCollider == null) _playerCollider = GetComponent<CapsuleCollider>();
			return _playerCollider;
		}
		private set => _playerCollider = value;
	}

	public PlayerStance currentStance { get; private set; } = PlayerStance.Standing;
	public bool isGrounded { get; private set; }
	public bool isSprinting { get; private set; }
	public bool inVehicle { get; set; } = false;

	public Weapon currentWeapon { get; private set; }
	private Weapon _equippedWeaponPrefab;

	public MonoBehaviour CurrentController => currentController;
	public HumanoidVisualController VisualController => visualController;
	public float CurrentPitch => currentPitch;

	private Rigidbody rb;
	private HumanoidVisualController visualController;
	private Vector2 moveInput;
	private bool jumpHeld;
	private float currentPitch = 0f;
	private float targetColHeight;
	private Vector3 targetColCenter;
	private Vector3 targetCamPos;
	private RaycastHit slopeHit;

	private void OnEnable()
	{
		if (playerCollider != null) MovementColliders.Add(playerCollider);
	}

	private void OnDisable()
	{
		if (playerCollider != null) MovementColliders.Remove(playerCollider);
	}

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		playerCollider = GetComponent<CapsuleCollider>();
		visualController = GetComponentInChildren<HumanoidVisualController>();

		currentHealth = maxHealth;

		rb.freezeRotation = true;
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

		if (playerCamera == null)
		{
			if (camGimbal != null) playerCamera = camGimbal.GetComponentInChildren<Camera>(true);
			if (playerCamera == null && camHolder != null) playerCamera = camHolder.GetComponentInChildren<Camera>(true);
			if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
		}

		if (cameraListener == null && playerCamera != null)
		{
			cameraListener = playerCamera.GetComponent<AudioListener>();
			if (cameraListener == null) cameraListener = GetComponentInChildren<AudioListener>(true);
		}

		if (fallbackDummy == null)
		{
			fallbackDummy = GetComponent<DummyController>();
			if (fallbackDummy == null) fallbackDummy = gameObject.AddComponent<DummyController>();
		}

		targetColHeight = standingHeight;
		targetColCenter = new Vector3(0f, standingHeight * 0.5f, 0f);
		targetCamPos = standingCamLocalPos;

		if (currentController == null)
		{
			Possess(fallbackDummy);
		}
	}

	private void Start()
	{
		OnHealthChanged?.Invoke(currentHealth, maxHealth);
	}

	private void Update()
	{
		if (isDead || inVehicle) return;

		CheckGrounded();
		SmoothStanceTransition();
		UpdateAimTarget();
	}

	private void LateUpdate()
	{
		if (isDead || inVehicle) return;

		AlignWeaponAim();
	}

	private void FixedUpdate()
	{
		if (isDead || inVehicle) return;
		ApplyMovementPhysics();
	}

	// --- HEALTH & COMBAT ---

	public bool IsAllied(int otherTeamId) => teamId == otherTeamId;
	public bool IsAllied(humanoidMotor other) => other != null && other.teamId == teamId;

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

	public void Die()
	{
		if (isDead) return;
		isDead = true;

		OnDeath?.Invoke();

		if (playerCollider != null) playerCollider.enabled = false;
		Vector3 deathVelocity = rb.linearVelocity;
		rb.isKinematic = true;

		if (currentController != null)
		{
			currentController.enabled = false;
		}

		if (visualController != null)
		{
			visualController.EnableRagdoll(deathVelocity);
		}

		if (currentWeapon != null)
		{
			DropWeapon();
		}

		if (corpseDespawnTime > 0f)
		{
			Destroy(gameObject, corpseDespawnTime);
		}
	}

	/// <summary>
	/// Safely deactivates motor logic and movement collision on death.
	/// Leaves all GameObjects, renderers, animators, and rigging intact without destroying components,
	/// keeping the corpse ready for a future revive mechanic.
	/// </summary>
	public void DisableMotorOnDeath()
	{
		Unpossess();

		if (currentWeapon != null)
		{
			DropWeapon();
		}

		if (playerCollider != null)
		{
			playerCollider.enabled = false;
		}

		Rigidbody rootRb = GetComponent<Rigidbody>();
		if (rootRb != null)
		{
			rootRb.linearVelocity = Vector3.zero;
			rootRb.angularVelocity = Vector3.zero;
			rootRb.isKinematic = true;
			rootRb.detectCollisions = false;
		}

		enabled = false;
	}

	/// <summary>
	/// Safely transitions a dead humanoid into a persistent ragdoll corpse.
	/// Disables motor and rigging logic without destroying components, preventing Burst job crashes.
	/// </summary>
	public void RetireAsCorpse()
	{
		gameObject.name = $"[Corpse] {gameObject.name}";
		gameObject.tag = "Untagged";

		// 1. Unpossess any remaining controller
		Unpossess();

		if (currentWeapon != null)
		{
			DropWeapon();
		}

		// 2. Disable the upright movement capsule collider so players can walk over it
		if (playerCollider != null)
		{
			playerCollider.enabled = false;
		}

		// 3. Deactivate root locomotion Rigidbody so only ragdoll bones simulate
		Rigidbody rootRb = GetComponent<Rigidbody>();
		if (rootRb != null)
		{
			rootRb.isKinematic = true;
			rootRb.detectCollisions = false;
		}

		// 4. Safely disable animation rigging without deleting components
		if (VisualController != null)
		{
			VisualController.DisableAndClearRig();
			VisualController.enabled = false;
		}

		// 5. Disable animator so physics has complete ragdoll bone authority
		Animator anim = GetComponentInChildren<Animator>();
		if (anim != null)
		{
			anim.enabled = false;
		}

		// 6. Disable cameras and audio listeners on the corpse
		Camera[] corpseCams = GetComponentsInChildren<Camera>(true);
		for (int i = 0; i < corpseCams.Length; i++) corpseCams[i].enabled = false;

		AudioListener[] corpseListeners = GetComponentsInChildren<AudioListener>(true);
		for (int i = 0; i < corpseListeners.Length; i++) corpseListeners[i].enabled = false;

		// 7. Disable this motor component
		enabled = false;
	}

	// --- POSSESSION & AUTHORITY ---

	// --- CONTROLLER POSSESSION OVERLOADS ---

	[Header("Locally Controlled State")]
	public bool isLocallyControlled { get; private set; } = false;

	public void Possess(playerController controller)
	{
		InitializePossession(true);
	}

	public void Possess(BotController controller)
	{
		InitializePossession(false);
	}

	public void Possess(DummyController controller)
	{
		InitializePossession(false);
	}

	public void Possess(MonoBehaviour controller)
	{
		InitializePossession(controller is playerController);
	}

	private void InitializePossession(bool isLocalPlayer)
	{
		isLocallyControlled = isLocalPlayer;
		isDead = false;
		if (currentHealth <= 0f) currentHealth = maxHealth;

		if (playerCollider != null) playerCollider.enabled = true;

		Rigidbody rootRb = GetComponent<Rigidbody>();
		if (rootRb != null)
		{
			rootRb.isKinematic = false;
			rootRb.detectCollisions = true;
		}

		enabled = true;

		if (playerCamera != null)
		{
			playerCamera.gameObject.SetActive(isLocalPlayer);
			playerCamera.enabled = isLocalPlayer;
		}

		if (VisualController != null)
		{
			VisualController.ConfigureVisuals(isLocalPlayer);

			if (currentWeapon != null)
			{
				VisualController.SetupWeaponForPerspective(currentWeapon, isLocallyControlled);
			}
		}
	}

	public void Unpossess()
	{
		currentController = null;
		SetMoveInput(Vector2.zero);
		SetSprintInput(false);
		SetJumpHeld(false);
		SetCameraActive(false);

		if (visualController != null)
		{
			visualController.ConfigureVisuals(false);
		}

		if (fallbackDummy != null)
		{
			Possess(fallbackDummy);
		}
	}

	public void SetCameraActive(bool active)
	{
		if (playerCamera != null)
		{
			playerCamera.gameObject.SetActive(active);
			playerCamera.enabled = active;
		}

		if (cameraListener != null)
		{
			cameraListener.enabled = active;
		}
	}

	public void ResetLookRotation()
	{
		currentPitch = 0f;
		if (camGimbal != null) camGimbal.localEulerAngles = Vector3.zero;
		if (bodyGeometry != null) bodyGeometry.localEulerAngles = Vector3.zero;
	}

	// --- INPUT RECEPTORS ---

	public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);
	public void SetSprintInput(bool sprint)
	{
		isSprinting = sprint && currentStance == PlayerStance.Standing && moveInput.y > 0.1f;
		if (isSprinting && isAiming)
		{
			SetAimInput(false);
		}
	}
	public void SetJumpHeld(bool held) => jumpHeld = held;

	public void Rotate(Vector3 deltaEuler)
	{
		if (bodyGeometry != null)
			bodyGeometry.Rotate(Vector3.up, deltaEuler.y, Space.World);
		else
			transform.Rotate(Vector3.up, deltaEuler.y, Space.World);
	}

	public void RotateCamera(float deltaPitch)
	{
		if (camGimbal == null) return;
		currentPitch = Mathf.Clamp(currentPitch + deltaPitch, minPitch, maxPitch);
		camGimbal.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
	}

	public void SetCameraPitch(float targetPitch)
	{
		if (camGimbal == null) return;
		currentPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
		camGimbal.localEulerAngles = new Vector3(currentPitch, 0f, 0f);
	}

	public void Jump()
	{
		if (!isGrounded || inVehicle || isDead) return;

		if (currentStance != PlayerStance.Standing)
		{
			SetStance(PlayerStance.Standing);
			return;
		}

		Vector3 currentVel = rb.linearVelocity;
		currentVel.y = jumpForce;
		rb.linearVelocity = currentVel;
	}

	public void ToggleCrouch()
	{
		if (inVehicle || isDead) return;
		SetStance(currentStance == PlayerStance.Crouching ? PlayerStance.Standing : PlayerStance.Crouching);
	}

	public void ToggleProne()
	{
		if (inVehicle || isDead) return;
		SetStance(currentStance == PlayerStance.Proning ? PlayerStance.Standing : PlayerStance.Proning);
	}

	public bool isAiming { get; private set; }

	public void SetAimInput(bool aim)
	{
		// Sprinting cancels aim
		isAiming = aim && !isSprinting;

		if (visualController != null)
		{
			visualController.SetAim(isAiming);
		}
	}

	// --- WEAPON SYSTEM ---
	/// <summary>
	/// Equips a weapon. If the weapon is already an instantiated scene object, it takes direct
	/// ownership and parents it. If it is an uninstantiated project prefab, it instantiates it.
	/// </summary>
	public void EquipWeapon(Weapon weaponPrefabOrInstance)
	{
		if (weaponPrefabOrInstance == null) return;

		if (currentWeapon != null)
		{
			DropWeapon();
		}

		// Detect if this is an existing world instance or an uninstantiated project prefab
		if (weaponPrefabOrInstance.gameObject.scene.IsValid())
		{
			currentWeapon = weaponPrefabOrInstance;
		}
		else
		{
			currentWeapon = Instantiate(weaponPrefabOrInstance);
		}

		if (VisualController != null)
		{
			VisualController.SetupWeaponForPerspective(currentWeapon, isLocallyControlled);
		}
	}

	/// <summary>
	/// Explicit alias for picking up an existing world weapon instance.
	/// </summary>
	public void PickupWeapon(Weapon worldWeapon)
	{
		EquipWeapon(worldWeapon);
	}

	/// <summary>
	/// Drops the current weapon into the world, restoring its world physics, colliders, and throw velocity.
	/// </summary>
	public void DropWeapon()
	{
		if (currentWeapon == null) return;

		Weapon dropped = currentWeapon;
		currentWeapon = null;

		if (VisualController != null)
		{
			VisualController.RestoreWeaponForWorld(dropped);
			VisualController.ClearAllWeapons();
		}

		// Detach from player hierarchy back into the scene root
		dropped.transform.SetParent(null);

		Vector3 dropPos = transform.position + Vector3.up * 1.0f;
		Vector3 throwDir = transform.forward;

		if (playerCamera != null && playerCamera.gameObject.activeInHierarchy)
		{
			dropPos = playerCamera.transform.position + playerCamera.transform.forward * 0.75f;
			throwDir = playerCamera.transform.forward;
		}

		dropped.transform.position = dropPos;
		dropped.transform.rotation = transform.rotation;
		dropped.SetOwner(null);
		dropped.ConfigureWorldPhysics(true);

		Rigidbody rb = dropped.GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.linearVelocity = (throwDir * 4.5f) + Vector3.up * 1.5f;
			rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
		}
	}

	private void AlignWeaponAim()
	{
		if (currentWeapon == null || aimTarget == null) return;

		Vector3 targetDir = (aimTarget.position - currentWeapon.transform.position).normalized;
		if (targetDir.sqrMagnitude > 0.001f)
		{
			currentWeapon.transform.rotation = Quaternion.LookRotation(targetDir, camGimbal != null ? camGimbal.up : transform.up);
		}
	}

	// --- INTERNAL PHYSICS ---

	private void CheckGrounded()
	{
		Vector3 origin = groundCheck != null ? groundCheck.position : transform.position;
		isGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
	}

	private void ApplyMovementPhysics()
	{
		Transform orientation = bodyGeometry != null ? bodyGeometry : transform;
		Vector3 targetDirection = (orientation.forward * moveInput.y + orientation.right * moveInput.x).normalized;

		float targetSpeed = currentStance switch
		{
			PlayerStance.Proning => proneSpeed,
			PlayerStance.Crouching => crouchSpeed,
			_ => isSprinting ? sprintSpeed : walkSpeed
		};

		Vector3 desiredVelocity = targetDirection * targetSpeed;
		Vector3 currentHorizVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

		if (isGrounded)
		{
			if (OnSlope())
			{
				desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, slopeHit.normal);
			}

			Vector3 velocityDelta = desiredVelocity - currentHorizVelocity;
			rb.AddForce(velocityDelta * acceleration, ForceMode.Acceleration);
		}
		else
		{
			Vector3 airVelocityDelta = (desiredVelocity - currentHorizVelocity) * airControlMultiplier;
			rb.AddForce(airVelocityDelta * acceleration, ForceMode.Acceleration);
		}
	}

	private bool OnSlope()
	{
		Vector3 origin = groundCheck != null ? groundCheck.position : transform.position;
		if (Physics.Raycast(origin + Vector3.up * 0.1f, Vector3.down, out slopeHit, 0.4f, groundMask))
		{
			float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
			return angle > 0.05f && angle <= maxSlopeAngle;
		}
		return false;
	}

	private void SetStance(PlayerStance newStance)
	{
		currentStance = newStance;
		switch (newStance)
		{
			case PlayerStance.Standing:
				targetColHeight = standingHeight;
				targetCamPos = standingCamLocalPos;
				break;
			case PlayerStance.Crouching:
				targetColHeight = crouchingHeight;
				targetCamPos = crouchingCamLocalPos;
				break;
			case PlayerStance.Proning:
				targetColHeight = proningHeight;
				targetCamPos = proningCamLocalPos;
				break;
		}
		targetColCenter = new Vector3(0f, targetColHeight * 0.5f, 0f);
	}

	private void SmoothStanceTransition()
	{
		playerCollider.height = Mathf.Lerp(playerCollider.height, targetColHeight, Time.deltaTime * stanceTransitionSpeed);
		playerCollider.center = Vector3.Lerp(playerCollider.center, targetColCenter, Time.deltaTime * stanceTransitionSpeed);

		if (camHolder != null)
		{
			camHolder.localPosition = Vector3.Lerp(camHolder.localPosition, targetCamPos, Time.deltaTime * stanceTransitionSpeed);
		}
	}

	private void UpdateAimTarget()
	{
		if (aimTarget == null || camGimbal == null) return;

		// Perform raycast query ignoring character's own capsule, bones, and equipped weapons
		RaycastHit[] hits = Physics.RaycastAll(camGimbal.position, camGimbal.forward, maxAimDistance, aimHitMask, QueryTriggerInteraction.Ignore);
		float closestHit = maxAimDistance;
		Vector3 targetPoint = camGimbal.position + camGimbal.forward * maxAimDistance;

		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform))
				continue;

			if (hits[i].distance < closestHit)
			{
				closestHit = hits[i].distance;
				targetPoint = hits[i].point;
			}
		}

		aimTarget.position = targetPoint;
	}
}