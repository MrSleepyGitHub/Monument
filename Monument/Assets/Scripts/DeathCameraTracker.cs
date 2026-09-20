using UnityEngine;

[DisallowMultipleComponent]
public class DeathCameraTracker : MonoBehaviour
{
	[Header("Target & References")]
	[SerializeField] private humanoidMotor motor;
	[Tooltip("Target bone to follow upon death. If unassigned, automatically detects Hips or Spine on the humanoid animator.")]
	[SerializeField] private Transform trackTarget;

	[Header("Orbit Settings")]
	[SerializeField] private Vector3 followOffset = new Vector3(0f, 1.8f, -3.5f);
	[SerializeField] private float followDamping = 4f;
	[SerializeField] private float rotationDamping = 6f;

	[Header("Collision Prevention")]
	[SerializeField] private LayerMask collisionMask;
	[SerializeField] private float minCameraDistance = 0.6f;
	[SerializeField] private float cameraCollisionRadius = 0.25f;

	private bool _isTrackingDeath = false;
	private Vector3 _currentVelocity;

	private void Awake()
	{
		if (motor == null) motor = GetComponentInParent<humanoidMotor>();
		if (collisionMask == 0) collisionMask = LayerMask.GetMask("Default", "Ground", "Terrain");
	}

	private void OnEnable()
	{
		if (motor != null) motor.OnDeath.AddListener(HandleDeath);
	}

	private void OnDisable()
	{
		if (motor != null) motor.OnDeath.RemoveListener(HandleDeath);
	}

	private void HandleDeath()
	{
		// 1. Resolve tracking bone if unassigned
		if (trackTarget == null && motor != null)
		{
			Animator anim = motor.GetComponentInChildren<Animator>();
			if (anim != null && anim.isHuman)
			{
				trackTarget = anim.GetBoneTransform(HumanBodyBones.Hips);
				if (trackTarget == null) trackTarget = anim.GetBoneTransform(HumanBodyBones.Chest);
			}

			if (trackTarget == null && motor.bodyGeometry != null)
			{
				trackTarget = motor.bodyGeometry;
			}
		}

		// 2. Detach camera from the player hierarchy so it isn't deleted or spun around by ragdoll joints
		transform.SetParent(null, true);
		_isTrackingDeath = true;
	}

	private void LateUpdate()
	{
		if (!_isTrackingDeath || trackTarget == null) return;

		Vector3 targetFocusPos = trackTarget.position + Vector3.up * 0.4f;
		Vector3 desiredPosition = targetFocusPos + followOffset;

		// SphereCast to prevent camera clipping into geometry or floors
		Vector3 dirToCam = desiredPosition - targetFocusPos;
		float targetDistance = dirToCam.magnitude;

		if (Physics.SphereCast(targetFocusPos, cameraCollisionRadius, dirToCam.normalized, out RaycastHit hit, targetDistance, collisionMask, QueryTriggerInteraction.Ignore))
		{
			float clampedDist = Mathf.Max(hit.distance, minCameraDistance);
			desiredPosition = targetFocusPos + dirToCam.normalized * clampedDist;
		}

		// Smooth position tracking
		transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, 1f / followDamping);

		// Smooth look-at tracking towards target bone
		Quaternion targetRotation = Quaternion.LookRotation((targetFocusPos - transform.position).normalized, Vector3.up);
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping);
	}
}