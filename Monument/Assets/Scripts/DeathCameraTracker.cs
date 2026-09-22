using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class DeathCameraTracker : MonoBehaviour
{
	[Header("Target & References")]
	[SerializeField] private humanoidMotor motor;
	[Tooltip("Target bone to follow upon death. If unassigned, automatically detects Hips or Spine on the humanoid animator.")]
	[SerializeField] private Transform trackTarget;

	[Header("Orbit & Look Settings")]
	[SerializeField] private float cameraDistance = 3.5f;
	[SerializeField] private float minDistance = 1.0f;
	[SerializeField] private float maxDistance = 6.0f;
	[SerializeField] private float zoomStep = 0.5f;
	[SerializeField] private float heightOffset = 0.5f;
	[SerializeField] private float orbitSensitivity = 1.5f;
	[SerializeField] private float followDamping = 6f;
	[SerializeField] private float rotationDamping = 10f;
	[SerializeField] private Vector2 pitchClamp = new Vector2(-20f, 75f);

	[Header("Collision Prevention")]
	[SerializeField] private LayerMask collisionMask;
	[SerializeField] private float minCameraDistance = 0.5f;
	[SerializeField] private float cameraCollisionRadius = 0.25f;

	private static readonly List<DeathCameraTracker> activeTrackers = new List<DeathCameraTracker>();

	private bool _isTrackingDeath = false;
	private Vector3 _currentVelocity;
	private float _orbitYaw = 0f;
	private float _orbitPitch = 20f;
	private float _currentDistance;

	private void Awake()
	{
		if (motor == null) motor = GetComponentInParent<humanoidMotor>();
		if (collisionMask == 0) collisionMask = LayerMask.GetMask("Default", "Ground", "Terrain");
		_currentDistance = cameraDistance;
	}

	private void OnEnable()
	{
		if (!activeTrackers.Contains(this)) activeTrackers.Add(this);
		if (motor != null) motor.OnDeath.AddListener(HandleDeath);
	}

	private void OnDisable()
	{
		activeTrackers.Remove(this);
		if (motor != null) motor.OnDeath.RemoveListener(HandleDeath);
	}

	private void OnDestroy()
	{
		activeTrackers.Remove(this);
		if (motor != null) motor.OnDeath.RemoveListener(HandleDeath);
	}

	/// <summary>
	/// Cleans up and destroys all detached death cameras currently floating in the scene.
	/// </summary>
	public static void CleanupAllTrackers()
	{
		for (int i = activeTrackers.Count - 1; i >= 0; i--)
		{
			if (activeTrackers[i] != null)
			{
				activeTrackers[i].Dismiss();
			}
		}
		activeTrackers.Clear();
	}

	public void Dismiss()
	{
		_isTrackingDeath = false;
		Destroy(gameObject);
	}

	private void HandleDeath()
	{
		// 1. Resolve tracking bone on the ragdoll if unassigned
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

		// 2. Initialize orbit yaw and pitch from current camera view to avoid snapping
		Vector3 currentAngles = transform.eulerAngles;
		_orbitYaw = currentAngles.y;
		float rawPitch = currentAngles.x > 180f ? currentAngles.x - 360f : currentAngles.x;
		_orbitPitch = Mathf.Clamp(rawPitch, pitchClamp.x, pitchClamp.y);

		// 3. Detach camera from the player hierarchy into the scene root
		transform.SetParent(null, true);
		_isTrackingDeath = true;
	}

	private void Update()
	{
		if (!_isTrackingDeath) return;

		// Read mouse delta for orbiting around the dead body
		Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
		float sens = PlayerMaster.Instance != null ? PlayerMaster.Instance.MouseSensitivity : 10f;

		_orbitYaw += mouseDelta.x * (orbitSensitivity * sens * 0.02f);
		_orbitPitch = Mathf.Clamp(_orbitPitch - mouseDelta.y * (orbitSensitivity * sens * 0.02f), pitchClamp.x, pitchClamp.y);

		// Zoom in and out with scroll wheel
		if (Mouse.current != null)
		{
			float scroll = Mouse.current.scroll.ReadValue().y;
			if (Mathf.Abs(scroll) > 0.01f)
			{
				_currentDistance = Mathf.Clamp(_currentDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
			}
		}
	}

	private void LateUpdate()
	{
		if (!_isTrackingDeath || trackTarget == null) return;

		Vector3 targetFocusPos = trackTarget.position + Vector3.up * heightOffset;

		Quaternion orbitRot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
		Vector3 desiredDir = orbitRot * -Vector3.forward;
		Vector3 desiredPosition = targetFocusPos + desiredDir * _currentDistance;

		// Prevent camera from clipping through geometry or ground
		if (Physics.SphereCast(targetFocusPos, cameraCollisionRadius, desiredDir, out RaycastHit hit, _currentDistance, collisionMask, QueryTriggerInteraction.Ignore))
		{
			float clampedDist = Mathf.Max(hit.distance, minCameraDistance);
			desiredPosition = targetFocusPos + desiredDir * clampedDist;
		}

		transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, 1f / followDamping);

		Quaternion targetRotation = Quaternion.LookRotation((targetFocusPos - transform.position).normalized, Vector3.up);
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping);
	}
}