using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class humanoidMotor : MonoBehaviour
{
    public enum PlayerStance { Standing, Crouching, Proning }

    [Header("Movement Speeds")]
    public float speed = 5f;
    public float sprintMultiplier = 1.5f;
    public float crouchSpeedMultiplier = 0.5f;
    public float proneSpeedMultiplier = 0.25f;
    public float groundAcceleration = 15f;

    [Header("Slide Mechanics")]
    public float slideBoostMultiplier = 1.5f;
    public float slideDuration = 0.75f;

    [Header("Camera & Rotation")]
    public Transform bodyGeometry;
    public Transform cameraHolder;
    public float mouseUpClamp = -90f;
    public float mouseDownClamp = 90f;

    [Header("Jumping & Physics")]
    public float jumpForce = 5f;
    public float groundCheckExtraDistance = 0.1f;
    public LayerMask groundMask;
    public float maxSlopeAngle = 45f;
    public float steepSlopeSlideSpeed = 15f;

    [Header("Mantling")]
    public float mantleRaycastDistance = 1f;
    public float mantleSphereRadius = 0.3f;
    public float mantleDurationMultiplier = 0.25f;
    public float vaultMomentumRetention = 1.2f;

    [Header("Air Control")]
    public float airAcceleration = 10f;

    [Header("Stances, Collider & Camera")]
    public CapsuleCollider playerCollider;
    public float standingHeight = 2f;
    public float crouchingHeight = 1f;
    public float proningHeight = 0.5f;
    public float standingCamHeight = 0.8f;
    public float crouchingCamHeight = 0f;
    public float proningCamHeight = -0.3f;
    public float stanceTransitionSpeed = 10f;

    // --- State Properties ---
    public PlayerStance currentStance { get; private set; } = PlayerStance.Standing;
    public bool isGrounded { get; private set; } = false;
    public bool isSprinting { get; private set; } = false;

    // --- Internal State ---
    private Vector2 moveInput = Vector2.zero;
    private Vector3 targetVelocity = Vector3.zero;
    private Vector2 currentXZVelocity = Vector2.zero;
    private Vector3 activeVelocity = Vector3.zero;
    private Vector3 bodyRotation = Vector3.zero;

    private float cameraPitch = 0f;
    private float currentCameraPitch = 0f;
    private bool isOnSteepSlope = false;
    private bool isMantling = false;
    private bool isHoldingJump = false;
    private bool isSliding = false;
    private float slideTimer = 0f;
    private Vector3 currentSlideDirection = Vector3.zero;

    private Vector3 groundNormal = Vector3.up;
    private Vector3 steepNormal = Vector3.up;
    private float timeSinceJump = 1f;

    private float initialCapsuleBottom;
    private float initialCapsuleRadius;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (playerCollider != null)
        {
            initialCapsuleBottom = playerCollider.center.y - (playerCollider.height / 2f);
            initialCapsuleRadius = playerCollider.radius;
        }
    }

    // --- Input Routers ---
    public void SetMoveInput(Vector2 input) => moveInput = input;
    public void SetJumpHeld(bool isHeld) => isHoldingJump = isHeld;
    public void Rotate(Vector3 _rotation) { if (!isMantling) bodyRotation = _rotation; }
    public void RotateCamera(float _cameraPitch) { if (!isMantling) cameraPitch = _cameraPitch; }

    public void SetSprintInput(bool isHeld)
    {
        isSprinting = isHeld && moveInput.y > 0.1f && currentStance == PlayerStance.Standing;
    }

    // --- Action Methods ---
    public void ToggleCrouch()
    {
        if (isSprinting && !isSliding && CanChangeStance(PlayerStance.Proning))
        {
            currentStance = PlayerStance.Proning;
            isSliding = true;
            slideTimer = slideDuration;

            Vector3 _movH = bodyGeometry != null ? bodyGeometry.right * moveInput.x : transform.right * moveInput.x;
            Vector3 _movV = bodyGeometry != null ? bodyGeometry.forward * moveInput.y : transform.forward * moveInput.y;
            currentSlideDirection = (_movH + _movV).normalized;
        }
        else if (!isSliding)
        {
            PlayerStance desiredStance = (currentStance == PlayerStance.Crouching) ? PlayerStance.Standing : PlayerStance.Crouching;
            if (CanChangeStance(desiredStance)) currentStance = desiredStance;
        }
    }

    public void ToggleProne()
    {
        if (!isSliding)
        {
            PlayerStance desiredStance = (currentStance == PlayerStance.Proning) ? PlayerStance.Standing : PlayerStance.Proning;
            if (CanChangeStance(desiredStance)) currentStance = desiredStance;
        }
    }

    public void Jump()
    {
        if (isMantling) return;

        if (isSliding)
        {
            if (isGrounded)
            {
                timeSinceJump = 0f;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            }
            isSliding = false;
            if (CanChangeStance(PlayerStance.Standing)) currentStance = PlayerStance.Standing;
            return;
        }

        if (currentStance != PlayerStance.Standing)
        {
            if (CanChangeStance(PlayerStance.Standing)) currentStance = PlayerStance.Standing;
            return;
        }

        if (isGrounded)
        {
            timeSinceJump = 0f;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    public void ResetLookRotation()
    {
        bodyRotation = Vector3.zero;
        if (bodyGeometry != null) bodyGeometry.localRotation = Quaternion.identity;
        cameraPitch = 0f;
        currentCameraPitch = 0f;
        if (cameraHolder != null) cameraHolder.localEulerAngles = Vector3.zero;
    }

    public bool CanChangeStance(PlayerStance targetStance)
    {
        float targetHeight = standingHeight;
        if (targetStance == PlayerStance.Crouching) targetHeight = crouchingHeight;
        else if (targetStance == PlayerStance.Proning) targetHeight = proningHeight;

        if (targetHeight <= playerCollider.height) return true;

        Vector3 point0 = transform.position + new Vector3(0f, initialCapsuleBottom + initialCapsuleRadius, 0f);
        Vector3 point1 = transform.position + new Vector3(0f, initialCapsuleBottom + targetHeight - initialCapsuleRadius, 0f);

        return !Physics.CheckCapsule(point0, point1, initialCapsuleRadius * 0.95f, groundMask);
    }

    private void Update()
    {
        if (!isMantling)
        {
            CalculateGroundVelocity();
        }
    }

    private void FixedUpdate()
    {
        timeSinceJump += Time.fixedDeltaTime;

        if (!isMantling)
        {
            if (isHoldingJump && currentStance == PlayerStance.Standing && AttemptMantle()) return;

            CheckGrounded();
            PerformMovement();
        }

        PerformStanceTransition();
    }

    private void LateUpdate()
    {
        if (!isMantling)
        {
            PerformBodyRotation();
            PerformCameraRotation();
        }
    }

    private void CalculateGroundVelocity()
    {
        float targetSpeed = speed;
        Vector3 _movHorizontal = bodyGeometry != null ? bodyGeometry.right * moveInput.x : transform.right * moveInput.x;
        Vector3 _movVertical = bodyGeometry != null ? bodyGeometry.forward * moveInput.y : transform.forward * moveInput.y;
        Vector3 desiredDirection = (_movHorizontal + _movVertical).normalized;

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;

            if (slideTimer <= 0f)
            {
                isSliding = false;
                if (CanChangeStance(PlayerStance.Crouching)) currentStance = PlayerStance.Crouching;
                else currentStance = PlayerStance.Proning;
            }
            else
            {
                float slideStartSpeed = speed * sprintMultiplier * slideBoostMultiplier;
                float slideEndSpeed = speed * proneSpeedMultiplier;
                float slideProgress = 1f - (slideTimer / slideDuration);
                targetSpeed = Mathf.Lerp(slideStartSpeed, slideEndSpeed, slideProgress);

                if (desiredDirection != Vector3.zero)
                {
                    Vector3 currentVel = currentSlideDirection * targetSpeed;
                    Vector3 desiredVel = desiredDirection * targetSpeed;
                    currentVel = Vector3.MoveTowards(currentVel, desiredVel, airAcceleration * Time.deltaTime);
                    currentSlideDirection = currentVel.normalized;
                }
                activeVelocity = currentSlideDirection * targetSpeed;
            }
        }

        if (!isSliding)
        {
            if (currentStance == PlayerStance.Crouching) targetSpeed *= crouchSpeedMultiplier;
            else if (currentStance == PlayerStance.Proning) targetSpeed *= proneSpeedMultiplier;
            else if (isSprinting) targetSpeed *= sprintMultiplier;

            Vector3 desiredVelocity = desiredDirection * targetSpeed;
            activeVelocity = Vector3.Lerp(activeVelocity, desiredVelocity, Time.deltaTime * groundAcceleration);
        }

        targetVelocity = activeVelocity;
    }

    private void CheckGrounded()
    {
        if (playerCollider != null)
        {
            Vector3 origin = transform.position + playerCollider.center;
            float castRadius = playerCollider.radius * 0.95f;
            float castDistance = (playerCollider.height / 2f) - castRadius + groundCheckExtraDistance;

            if (Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit, castDistance, groundMask))
            {
                groundNormal = hit.normal;
                if (Vector3.Angle(Vector3.up, groundNormal) > maxSlopeAngle)
                {
                    isGrounded = false;
                    isOnSteepSlope = true;
                    steepNormal = hit.normal;
                }
                else
                {
                    isGrounded = true;
                    isOnSteepSlope = false;
                }
            }
            else
            {
                isGrounded = false;
                isOnSteepSlope = false;
                groundNormal = Vector3.up;
            }
        }
    }

    private void PerformMovement()
    {
        Vector2 targetXZ = new Vector2(targetVelocity.x, targetVelocity.z);

        if (isGrounded)
        {
            currentXZVelocity = targetXZ;
            Vector3 flatMovement = new Vector3(currentXZVelocity.x, 0f, currentXZVelocity.y);

            if (timeSinceJump <= 0.2f) rb.linearVelocity = new Vector3(flatMovement.x, rb.linearVelocity.y, flatMovement.z);
            else
            {
                Vector3 slopeMovement = Vector3.ProjectOnPlane(flatMovement, groundNormal).normalized * flatMovement.magnitude;
                rb.linearVelocity = slopeMovement;
            }
        }
        else
        {
            if (isOnSteepSlope)
            {
                Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, steepNormal).normalized;
                Vector2 slideXZ = new Vector2(slideDirection.x, slideDirection.z);
                currentXZVelocity = Vector2.MoveTowards(currentXZVelocity, slideXZ * steepSlopeSlideSpeed, airAcceleration * 2f * Time.fixedDeltaTime);
            }
            else
            {
                if (targetXZ != Vector2.zero) currentXZVelocity = Vector2.MoveTowards(currentXZVelocity, targetXZ, airAcceleration * Time.fixedDeltaTime);
            }
            rb.linearVelocity = new Vector3(currentXZVelocity.x, rb.linearVelocity.y, currentXZVelocity.y);
        }
    }

    private void PerformStanceTransition()
    {
        if (playerCollider == null || cameraHolder == null) return;

        float targetHeight = standingHeight;
        float targetCamHeight = standingCamHeight;

        if (currentStance == PlayerStance.Crouching)
        {
            targetHeight = crouchingHeight;
            targetCamHeight = crouchingCamHeight;
        }
        else if (currentStance == PlayerStance.Proning)
        {
            targetHeight = proningHeight;
            targetCamHeight = proningCamHeight;
        }

        playerCollider.height = Mathf.Lerp(playerCollider.height, targetHeight, Time.fixedDeltaTime * stanceTransitionSpeed);
        playerCollider.radius = Mathf.Min(initialCapsuleRadius, playerCollider.height / 2f);
        playerCollider.center = new Vector3(0f, initialCapsuleBottom + (playerCollider.height / 2f), 0f);

        Vector3 camPos = cameraHolder.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCamHeight, Time.fixedDeltaTime * stanceTransitionSpeed);
        cameraHolder.localPosition = camPos;
    }

    private bool AttemptMantle()
    {
        if (bodyGeometry == null || cameraHolder == null) return false;

        Vector3 wallRayOrigin = transform.position + new Vector3(0, playerCollider.height / 2f, 0);
        if (Physics.SphereCast(wallRayOrigin, mantleSphereRadius, bodyGeometry.forward, out RaycastHit wallHit, mantleRaycastDistance, groundMask))
        {
            float ledgeCheckHeight = cameraHolder.position.y + 0.1f;
            Vector3 ledgeRayOrigin = new Vector3(wallHit.point.x, ledgeCheckHeight, wallHit.point.z) - (wallHit.normal * 0.1f);
            float downcastDistance = ledgeCheckHeight - transform.position.y;

            if (Physics.Raycast(ledgeRayOrigin, Vector3.down, out RaycastHit ledgeHit, downcastDistance, groundMask))
            {
                float heightDifference = ledgeHit.point.y - transform.position.y;
                if (ledgeHit.point.y < cameraHolder.position.y && heightDifference > 0.5f)
                {
                    Vector3 finalPosition = ledgeHit.point + Vector3.up * 0.05f;
                    Vector3 p0 = finalPosition + new Vector3(0f, initialCapsuleRadius, 0f);
                    Vector3 p1 = finalPosition + new Vector3(0f, standingHeight - initialCapsuleRadius, 0f);

                    if (!Physics.CheckCapsule(p0, p1, initialCapsuleRadius * 0.95f, groundMask))
                    {
                        StartCoroutine(MantleRoutine(finalPosition, currentXZVelocity));
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private IEnumerator MantleRoutine(Vector3 targetPosition, Vector2 entryMomentum)
    {
        isMantling = true;
        rb.isKinematic = true;

        Vector3 startPosition = transform.position;
        float heightDifference = targetPosition.y - startPosition.y;

        float duration = Mathf.Max(0.15f, heightDifference * mantleDurationMultiplier);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        rb.isKinematic = false;

        float preservationRatio = Mathf.Clamp(vaultMomentumRetention - (heightDifference / standingHeight), 0f, vaultMomentumRetention);
        currentXZVelocity = entryMomentum * preservationRatio;
        rb.linearVelocity = new Vector3(currentXZVelocity.x, 0f, currentXZVelocity.y);

        isMantling = false;
    }

    private void PerformBodyRotation()
    {
        if (bodyRotation != Vector3.zero && bodyGeometry != null)
        {
            bodyGeometry.Rotate(bodyRotation);
            bodyRotation = Vector3.zero;
        }
    }

    private void PerformCameraRotation()
    {
        if (cameraHolder != null)
        {
            currentCameraPitch += cameraPitch;
            currentCameraPitch = Mathf.Clamp(currentCameraPitch, mouseUpClamp, mouseDownClamp);
            cameraHolder.localEulerAngles = new Vector3(currentCameraPitch, 0f, 0f);
            cameraPitch = 0f;
        }
    }
}