using UnityEngine;
using UnityEngine.Animations.Rigging;

public class StateManager : MonoBehaviour
{
	[Header("Core References")]
	public playerController player;
	public humanoidMotor motor;
	public Rigidbody playerRigidbody;
	public Transform bodyGeometry;

	[Header("Aiming IK (Animation Rigging)")]
	public Transform cameraHolder;
	public Transform playerAim;
	public float aimDistance = 100f;
	public LayerMask aimMask;
	public float aimLerpSpeed = 15f;
	[HideInInspector] public float currentAimDistance = 100f;

	[Header("Procedural Hand IK")]
	public TwoBoneIKConstraint leftArmIK;
	public TwoBoneIKConstraint rightArmIK;
	public Transform leftHandGrip;
	public Transform rightHandGrip;

	[Header("Animator & Blending")]
	public Animator animator;
	public float animationDampTime = 0.1f;
	private readonly int isSeatedHash = Animator.StringToHash("IsSeated");
	private readonly int hasInputHash = Animator.StringToHash("HasInput");
	private readonly int velocityXHash = Animator.StringToHash("VelocityX");
	private readonly int velocityZHash = Animator.StringToHash("VelocityZ");
	private readonly int stanceHash = Animator.StringToHash("Stance");
	private readonly int isGroundedHash = Animator.StringToHash("IsGrounded"); // NEW

	private BaseState currentState;

	private void Start()
	{
		player = GetComponent<playerController>();
		motor = GetComponent<humanoidMotor>();
		playerRigidbody = GetComponent<Rigidbody>();

		currentAimDistance = aimDistance;
		currentState = new LocomotionState(this);
		currentState.EnterState();
	}

	private void Update()
	{
		if (currentState == null) return;

		UpdateAnimatorParameters();

		currentState.UpdateState();

		BaseState nextState = currentState.GetNextState();

		if (nextState != null && nextState != currentState)
		{
			currentState.ExitState();
			currentState = nextState;
			currentState.EnterState();
		}
	}

    private void UpdateAnimatorParameters()
    {
        if (animator == null || playerRigidbody == null || bodyGeometry == null || motor == null) return;

        Vector3 worldVelocity = new Vector3(playerRigidbody.linearVelocity.x, 0f, playerRigidbody.linearVelocity.z);
        Vector3 localVelocity = bodyGeometry.InverseTransformDirection(worldVelocity);

        animator.SetFloat(velocityXHash, localVelocity.x, animationDampTime, Time.deltaTime);
        animator.SetFloat(velocityZHash, localVelocity.z, animationDampTime, Time.deltaTime);

        float targetStance = (float)motor.currentStance;

        // Shift the stance parameter into the negatives to access the sprint tree safely[cite: 8]
        // FIXED: Now correctly references the motor's boolean instead of the controller's
        if (targetStance == 0f && motor.isSprinting)
        {
            targetStance = -1f;
        }

        animator.SetFloat(stanceHash, targetStance, animationDampTime, Time.deltaTime);
    }

    private void LateUpdate()
	{
		currentState?.LateUpdateState();
	}

	private void OnTriggerEnter(Collider other) => currentState?.OnTriggerEnter(other);
	private void OnTriggerStay(Collider other) => currentState?.OnTriggerStay(other);
	private void OnTriggerExit(Collider other) => currentState?.OnTriggerExit(other);

	private void OnAnimatorIK(int layerIndex)
	{
		currentState?.UpdateIK();
	}
}