using UnityEngine;

public class AnimatorHumanoid : MonoBehaviour
{
	public enum BoneAxis { PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ }

	[Header("Core References")]
	public humanoidMotor motor;

	[Header("Animator & Blending")]
	public Animator animator;
	public float moveX, moveY;
	public float groundedStance, idleStance;

	[Header("Aiming (Script Based)")]
	public Transform cameraHolder;
	public Transform playerAim;
	public float aimDistance = 100f;
	public LayerMask aimMask;
	public float aimAdjustSpeed = 50f;

	[Range(0f, 1f)] public float headLookWeight = 0.9f;
	[Tooltip("Aligns the gun barrel vertically with the crosshair.")]
	public float chestPitchOffset = 0f;
	[Tooltip("Aligns the gun barrel horizontally by tweaking the right arm.")]
	public float rightArmYawOffset = 0f;
	[Tooltip("Moves the camera in an arc when looking up/down to match the spine bending.")]
	public float cameraArcDepth = 0.4f;

	[Header("Right Hand Aim Alignment")]
	[Tooltip("Which local axis of the hand bone points down the fingers/gun when weaponHolder is not used.")]
	public BoneAxis handAimAxis = BoneAxis.PositiveX;
	[Tooltip("Optional: If assigned, uses the weapon holder's local orientation to perfectly align the gun barrel.")]
	public Transform weaponHolder;
	[Tooltip("Fine-tuning offset for wrist rotation (Pitch X, Yaw Y, Roll Z).")]
	public Vector3 rightHandRotationOffset = Vector3.zero;
	[Tooltip("Legacy fine-tuning offset for wrist yaw.")]
	public float rightHandYawOffset = 0f;

	[Header("Left Hand Grip IK")]
	[Tooltip("Target grip on the weapon. Updated dynamically upon equipping.")]
	public Transform leftHandGrip;
	[Tooltip("Fine-tuning rotation offset for the left hand.")]
	public Vector3 leftHandRotationOffset = Vector3.zero;

	[HideInInspector] public float currentAimDistance = 100f;

	[Header("Current Motion")]
	[SerializeField] private float velocityX;
	[SerializeField] private float velocityY;

	[Header("Animation Settings")]
	public float maxSpeed = 7.5f;
	public float animationDampTime = 0.1f;

	private readonly int moveXHash = Animator.StringToHash("MoveX");
	private readonly int moveYHash = Animator.StringToHash("MoveY");
	private readonly int groundedStanceHash = Animator.StringToHash("GroundedStance");
	private readonly int idleStanceHash = Animator.StringToHash("IdleStance");
	private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");

	private Transform mainCamera;
	private Vector3 initialCameraLocalPos;

	private void Start()
	{
		currentAimDistance = aimDistance;

		if (playerAim == null && cameraHolder != null)
		{
			playerAim = cameraHolder.Find("PlayerAim");
		}

		if (cameraHolder != null)
		{
			Camera cam = cameraHolder.GetComponentInChildren<Camera>();
			if (cam != null)
			{
				mainCamera = cam.transform;
				initialCameraLocalPos = mainCamera.localPosition;
			}
		}
	}

	private void Update()
	{
		CalculateState();
		CalculateSpeed();
		CalculateAimDistance();
		UpdateAnimation();
	}

	private void CalculateState()
	{
		if (motor == null) return;

		groundedStance = (float)motor.currentStance;

		if (groundedStance == 0f && motor.isSprinting)
		{
			groundedStance = -1f;
		}
	}

	private void CalculateSpeed()
	{
		if (motor == null) return;

		Rigidbody rb = motor.GetComponent<Rigidbody>();
		Transform referenceTransform = motor.bodyGeometry != null ? motor.bodyGeometry : motor.transform;
		Vector3 localVelocity = referenceTransform.InverseTransformDirection(rb.linearVelocity);

		velocityX = localVelocity.x;
		velocityY = localVelocity.z;

		moveX = Mathf.Clamp(velocityX / maxSpeed, -1f, 1f);
		moveY = Mathf.Clamp(velocityY / maxSpeed, -1f, 1f);

		float currentSpeed = new Vector2(velocityX, velocityY).magnitude;
		float targetIdle = currentSpeed < 0.05f ? 1f : 0f;

		idleStance = Mathf.MoveTowards(idleStance, targetIdle, Time.deltaTime * 5f);
	}

	private void CalculateAimDistance()
	{
		if (cameraHolder == null) return;

		float targetDistance = aimDistance;

		RaycastHit[] hits = Physics.RaycastAll(cameraHolder.position, cameraHolder.forward, aimDistance, aimMask, QueryTriggerInteraction.Ignore);
		float closestHit = aimDistance;

		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform)) continue;

			if (hits[i].distance < closestHit)
			{
				closestHit = hits[i].distance;
			}
		}

		targetDistance = closestHit;
		currentAimDistance = Mathf.MoveTowards(currentAimDistance, targetDistance, Time.deltaTime * aimAdjustSpeed);

		if (playerAim != null)
		{
			playerAim.position = cameraHolder.position + cameraHolder.forward * currentAimDistance;
			playerAim.rotation = cameraHolder.rotation;
		}
	}

	private void UpdateAnimation()
	{
		if (animator != null)
		{
			animator.SetFloat(moveXHash, moveX, animationDampTime, Time.deltaTime);
			animator.SetFloat(moveYHash, moveY, animationDampTime, Time.deltaTime);
			animator.SetFloat(groundedStanceHash, groundedStance, animationDampTime, Time.deltaTime);
			animator.SetFloat(idleStanceHash, idleStance, animationDampTime, Time.deltaTime);

			if (motor != null)
			{
				animator.SetBool(isGroundedHash, motor.isGrounded);
			}
		}
	}

	public void ExecuteProceduralIK()
	{
		if (animator == null || cameraHolder == null) return;

		// Head tracking executes cleanly inside OnAnimatorIK
		Vector3 virtualAimPoint = playerAim != null ? playerAim.position : (cameraHolder.position + cameraHolder.forward * currentAimDistance);
		animator.SetLookAtWeight(1f, 0f, headLookWeight, 1f, 0.5f);
		animator.SetLookAtPosition(virtualAimPoint);
	}

	private void LateUpdate()
	{
		if (animator == null || cameraHolder == null || motor == null || motor.bodyGeometry == null) return;

		Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
		Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
		Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
		Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

		float truePitch = 0f;

		// 1. Spine / Chest Pitch
		if (chest != null && head != null)
		{
			Quaternion headIKRotation = head.rotation;

			Vector3 localCamDir = motor.bodyGeometry.InverseTransformDirection(cameraHolder.forward);
			truePitch = -Mathf.Atan2(localCamDir.y, localCamDir.z) * Mathf.Rad2Deg;

			chest.RotateAround(chest.position, cameraHolder.right, truePitch + chestPitchOffset);

			head.rotation = headIKRotation;
		}

		Vector3 targetPoint = playerAim != null ? playerAim.position : (cameraHolder.position + cameraHolder.forward * currentAimDistance);

		// 2. Upper Arm Horizontal Convergence
		if (rightArm != null)
		{
			Vector3 armToTarget = targetPoint - rightArm.position;
			Vector3 flatArmTargetDir = Vector3.ProjectOnPlane(armToTarget, cameraHolder.up).normalized;
			float armYawConvergence = Vector3.SignedAngle(cameraHolder.forward, flatArmTargetDir, cameraHolder.up);

			rightArm.RotateAround(rightArm.position, cameraHolder.up, armYawConvergence + rightArmYawOffset);
		}

		// 3. Absolute Right Hand Aim Orientation
		if (rightHand != null)
		{
			Vector3 aimOrigin = (weaponHolder != null) ? weaponHolder.position : rightHand.position;
			Vector3 aimDir = (targetPoint - aimOrigin).normalized;
			if (aimDir.sqrMagnitude < 0.001f) aimDir = cameraHolder.forward;

			Quaternion desiredAimRot = Quaternion.LookRotation(aimDir, cameraHolder.up);
			Quaternion fineTuneOffset = Quaternion.Euler(rightHandRotationOffset.x, rightHandRotationOffset.y + rightHandYawOffset, rightHandRotationOffset.z);
			Quaternion finalAimRot = desiredAimRot * fineTuneOffset;

			if (weaponHolder != null && weaponHolder.IsChildOf(rightHand))
			{
				rightHand.rotation = finalAimRot * Quaternion.Inverse(weaponHolder.localRotation);
			}
			else
			{
				Vector3 localBoneAxis = GetLocalBoneAxis(handAimAxis);
				Quaternion localToForward = Quaternion.FromToRotation(localBoneAxis, Vector3.forward);
				rightHand.rotation = finalAimRot * localToForward;
			}
		}

		// 4. Procedural Left Arm Two-Bone IK (Runs AFTER the weapon has reached its final aim position)
		if (leftHandGrip != null)
		{
			Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
			Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

			if (leftUpperArm != null && leftLowerArm != null && leftHand != null)
			{
				Quaternion gripRotation = leftHandGrip.rotation * Quaternion.Euler(leftHandRotationOffset);
				SolveTwoBoneIK(leftUpperArm, leftLowerArm, leftHand, leftHandGrip.position, gripRotation);
			}
		}

		// 5. Camera Arc Translation
		if (mainCamera != null)
		{
			Vector3 pivotToCam = motor.bodyGeometry.up * cameraArcDepth;
			Quaternion pitchRotation = Quaternion.AngleAxis(truePitch, cameraHolder.right);
			Vector3 rotatedPivotToCam = pitchRotation * pivotToCam;
			Vector3 worldOffset = rotatedPivotToCam - pivotToCam;
			mainCamera.localPosition = initialCameraLocalPos + cameraHolder.InverseTransformVector(worldOffset);
		}
	}

	private void SolveTwoBoneIK(Transform upper, Transform lower, Transform tip, Vector3 targetPos, Quaternion targetRot)
	{
		Vector3 a = upper.position;
		Vector3 t = targetPos;
		Vector3 at = t - a;
		float distAT = at.magnitude;

		if (distAT < 0.001f) return;
		Vector3 dirAT = at / distAT;

		float lenAB = Vector3.Distance(upper.position, lower.position);
		float lenBC = Vector3.Distance(lower.position, tip.position);

		if (lenAB < 0.001f || lenBC < 0.001f) return;

		// 1. Clamp distance within solvable reach
		float maxReach = (lenAB + lenBC) * 0.999f;
		float minReach = Mathf.Max(0.01f, Mathf.Abs(lenAB - lenBC) * 1.001f);
		float dist = Mathf.Clamp(distAT, minReach, maxReach);

		// 2. Law of Cosines for shoulder angle
		float cosAlpha = (lenAB * lenAB + dist * dist - lenBC * lenBC) / (2f * lenAB * dist);
		cosAlpha = Mathf.Clamp(cosAlpha, -1f, 1f);
		float sinAlpha = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAlpha * cosAlpha));

		// 3. Establish natural bend plane from current animation pose
		Vector3 currentElbowDir = (lower.position - upper.position).normalized;
		Vector3 planeNormal = Vector3.Cross(dirAT, currentElbowDir);

		if (planeNormal.sqrMagnitude < 0.001f)
		{
			Vector3 fallbackDir = motor != null && motor.bodyGeometry != null
				? (-motor.bodyGeometry.up - motor.bodyGeometry.right * 0.5f)
				: (-transform.up - transform.right * 0.5f);
			planeNormal = Vector3.Cross(dirAT, fallbackDir);
		}
		planeNormal.Normalize();

		Vector3 bendDir = Vector3.Cross(planeNormal, dirAT).normalized;
		if (Vector3.Dot(bendDir, currentElbowDir) < 0f)
		{
			bendDir = -bendDir;
		}

		// 4. Calculate exact target elbow point in 3D space
		Vector3 targetElbowPos = a + dirAT * (lenAB * cosAlpha) + bendDir * (lenAB * sinAlpha);

		// 5. Orient shoulder toward calculated elbow point
		upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, targetElbowPos - upper.position) * upper.rotation;

		// 6. Orient forearm from new elbow position toward target grip
		lower.rotation = Quaternion.FromToRotation(tip.position - lower.position, targetPos - lower.position) * lower.rotation;

		// 7. Hard Snap: Lock world position and rotation directly to grip transform
		tip.position = targetPos;
		tip.rotation = targetRot;
	}

	private Vector3 GetLocalBoneAxis(BoneAxis axis)
	{
		return axis switch
		{
			BoneAxis.PositiveX => Vector3.right,
			BoneAxis.NegativeX => Vector3.left,
			BoneAxis.PositiveY => Vector3.up,
			BoneAxis.NegativeY => Vector3.down,
			BoneAxis.PositiveZ => Vector3.forward,
			BoneAxis.NegativeZ => Vector3.back,
			_ => Vector3.forward
		};
	}
}