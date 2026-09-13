using UnityEngine;

public class AnimatorHumanoid : MonoBehaviour
{
    [Header("Core References")]
    public humanoidMotor motor;

    [Header("Animator & Blending")]
    public Animator animator;
    public float moveX, moveY;
    public float groundedStance, idleStance;

    [Header("Aiming (Script Based)")]
    public Transform cameraHolder;
    public float aimDistance = 100f;
    public LayerMask aimMask;
    public float aimAdjustSpeed = 50f;

    [Range(0f, 1f)] public float headLookWeight = 0.9f;
    [Tooltip("Aligns the gun barrel vertically with the crosshair.")]
    public float chestPitchOffset = 0f;
    [Tooltip("Aligns the gun barrel horizontally by tweaking the right arm.")]
    public float rightArmYawOffset = 0f;
    [Tooltip("Moves the camera in an arc when looking up/down to match the spine bending. Prevents the camera from drifting out of the head model.")]
    public float cameraArcDepth = 0.4f;

    [HideInInspector] public float currentAimDistance = 100f;
    [HideInInspector] public Transform leftHandGrip;

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

    void Start()
    {
        currentAimDistance = aimDistance;

        // Automatically finds the camera child and caches its default local position
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

    void Update()
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

        if (Physics.Raycast(cameraHolder.position, cameraHolder.forward, out RaycastHit hit, aimDistance, aimMask))
        {
            targetDistance = hit.distance;
        }

        currentAimDistance = Mathf.MoveTowards(currentAimDistance, targetDistance, Time.deltaTime * aimAdjustSpeed);
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

        Vector3 virtualAimPoint = cameraHolder.position + cameraHolder.forward * currentAimDistance;

        animator.SetLookAtWeight(1f, 0f, headLookWeight, 1f, 0.5f);
        animator.SetLookAtPosition(virtualAimPoint);

        if (leftHandGrip != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandGrip.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandGrip.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        }
    }

    private void LateUpdate()
    {
        if (animator == null || cameraHolder == null || motor == null || motor.bodyGeometry == null) return;

        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        float truePitch = 0f;

        if (chest != null && head != null)
        {
            Quaternion headIKRotation = head.rotation;

            Vector3 localCamDir = motor.bodyGeometry.InverseTransformDirection(cameraHolder.forward);
            truePitch = -Mathf.Atan2(localCamDir.y, localCamDir.z) * Mathf.Rad2Deg;

            chest.RotateAround(chest.position, cameraHolder.right, truePitch + chestPitchOffset);

            head.rotation = headIKRotation;
        }

        if (rightArm != null)
        {
            Vector3 targetPoint = cameraHolder.position + cameraHolder.forward * currentAimDistance;
            Vector3 armToTarget = targetPoint - rightArm.position;

            Vector3 flatTargetDir = Vector3.ProjectOnPlane(armToTarget, cameraHolder.up).normalized;
            float yawConvergence = Vector3.SignedAngle(cameraHolder.forward, flatTargetDir, cameraHolder.up);

            rightArm.RotateAround(rightArm.position, cameraHolder.up, yawConvergence + rightArmYawOffset);
        }

        // --- NEW: Camera Arc Math ---
        if (mainCamera != null)
        {
            // 1. Establish the static radius vector from our virtual pivot to the camera
            Vector3 pivotToCam = motor.bodyGeometry.up * cameraArcDepth;

            // 2. Rotate that vector by our Gimbal-free pitch
            Quaternion pitchRotation = Quaternion.AngleAxis(truePitch, cameraHolder.right);
            Vector3 rotatedPivotToCam = pitchRotation * pivotToCam;

            // 3. Calculate the resulting position change and convert it to local space
            Vector3 worldOffset = rotatedPivotToCam - pivotToCam;
            mainCamera.localPosition = initialCameraLocalPos + cameraHolder.InverseTransformVector(worldOffset);
        }
    }
}