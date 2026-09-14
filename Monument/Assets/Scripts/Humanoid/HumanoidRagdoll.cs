using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(Health))]
public class HumanoidRagdoll : MonoBehaviour
{
	[Header("Core References")]
	[SerializeField] private Animator animator;
	[SerializeField] private Transform hipsBone;

	[Header("Death Camera (Player Only)")]
	[Tooltip("If true, detaches camera on death and keeps it facing the falling ragdoll.")]
	[SerializeField] private bool decoupleCameraOnDeath = true;

	private Health health;
	private humanoidMotor motor;
	private playerController playerCtrl;
	private BotController botCtrl;
	private AnimatorHumanoid animHumanoid;
	private AnimatorIKProxy ikProxy;
	private NavMeshAgent navAgent;

	private Rigidbody rootRb;
	private Collider rootCollider;

	private List<Rigidbody> boneRigidbodies = new List<Rigidbody>();
	private List<Collider> boneColliders = new List<Collider>();

	private void Awake()
	{
		health = GetComponent<Health>();
		motor = GetComponent<humanoidMotor>();
		playerCtrl = GetComponent<playerController>();
		botCtrl = GetComponent<BotController>();
		animHumanoid = GetComponent<AnimatorHumanoid>();
		ikProxy = GetComponentInChildren<AnimatorIKProxy>();
		navAgent = GetComponent<NavMeshAgent>();

		rootRb = GetComponent<Rigidbody>();
		if (motor != null && motor.playerCollider != null)
		{
			rootCollider = motor.playerCollider;
		}
		else
		{
			rootCollider = GetComponent<Collider>();
		}

		if (animator == null)
		{
			animator = GetComponentInChildren<Animator>();
		}

		FindRagdollBones();
		SetRagdollState(false);
	}

	private void OnEnable()
	{
		if (health != null) health.OnDeath.AddListener(TriggerRagdoll);
	}

	private void OnDisable()
	{
		if (health != null) health.OnDeath.RemoveListener(TriggerRagdoll);
	}

	private void FindRagdollBones()
	{
		boneRigidbodies.Clear();
		boneColliders.Clear();

		Rigidbody[] allRbs = GetComponentsInChildren<Rigidbody>(true);
		foreach (Rigidbody rb in allRbs)
		{
			// Ignore the root movement Rigidbody
			if (rb != rootRb)
			{
				boneRigidbodies.Add(rb);
			}
		}

		Collider[] allCols = GetComponentsInChildren<Collider>(true);
		foreach (Collider col in allCols)
		{
			// Ignore root capsule collider
			if (col != rootCollider)
			{
				boneColliders.Add(col);
			}
		}
	}

	public void SetRagdollState(bool isRagdoll)
	{
		// 1. Configure all skeletal bones
		for (int i = 0; i < boneRigidbodies.Count; i++)
		{
			if (boneRigidbodies[i] == null) continue;
			boneRigidbodies[i].isKinematic = !isRagdoll;
			boneRigidbodies[i].detectCollisions = isRagdoll;
		}

		for (int i = 0; i < boneColliders.Count; i++)
		{
			if (boneColliders[i] == null) continue;
			boneColliders[i].enabled = isRagdoll;
		}

		// 2. Configure main root components
		if (rootCollider != null) rootCollider.enabled = !isRagdoll;
		if (rootRb != null)
		{
			rootRb.isKinematic = isRagdoll;
			rootRb.detectCollisions = !isRagdoll;
		}

		// 3. Disable Animator so it stops overriding bone transforms every frame
		if (animator != null) animator.enabled = !isRagdoll;
	}

	public void TriggerRagdoll()
	{
		// Capture physical momentum before disabling movement components
		Vector3 inheritedVelocity = rootRb != null ? rootRb.linearVelocity : Vector3.zero;

		// Disarm and drop equipped weapon
		if (playerCtrl != null && playerCtrl.currentWeapon != null)
		{
			playerCtrl.currentWeapon.Drop();
		}

		// Disable input and character controllers
		if (motor != null) motor.enabled = false;
		if (playerCtrl != null) playerCtrl.enabled = false;
		if (botCtrl != null) botCtrl.enabled = false;
		if (animHumanoid != null) animHumanoid.enabled = false;
		if (ikProxy != null) ikProxy.enabled = false;

		if (navAgent != null)
		{
			navAgent.isStopped = true;
			navAgent.enabled = false;
		}

		// Decouple player camera to keep it from snapping or pitching wildly
		if (decoupleCameraOnDeath && playerCtrl != null && playerCtrl.playerCamera != null)
		{
			SetupDeathCamera(playerCtrl.playerCamera.transform);
		}

		// Activate ragdoll physics
		SetRagdollState(true);

		// Impart inherited linear velocity to all skeletal bones
		for (int i = 0; i < boneRigidbodies.Count; i++)
		{
			if (boneRigidbodies[i] != null)
			{
				boneRigidbodies[i].linearVelocity = inheritedVelocity;
			}
		}
	}

	public void ApplyImpulseToBone(Vector3 force, Vector3 position)
	{
		// Applies targeted impact force from bullets or explosions
		Rigidbody closestRb = GetClosestBone(position);
		if (closestRb != null)
		{
			closestRb.AddForceAtPosition(force, position, ForceMode.Impulse);
		}
	}

	private Rigidbody GetClosestBone(Vector3 position)
	{
		Rigidbody closest = null;
		float shortestDist = Mathf.Infinity;

		for (int i = 0; i < boneRigidbodies.Count; i++)
		{
			if (boneRigidbodies[i] == null) continue;
			float dist = Vector3.Distance(boneRigidbodies[i].position, position);
			if (dist < shortestDist)
			{
				shortestDist = dist;
				closest = boneRigidbodies[i];
			}
		}

		return closest;
	}

	private void SetupDeathCamera(Transform camTransform)
	{
		camTransform.SetParent(null);
		DeathCameraTracker tracker = camTransform.gameObject.AddComponent<DeathCameraTracker>();
		tracker.target = hipsBone != null ? hipsBone : transform;
	}
}