using UnityEngine;
using UnityEngine.InputSystem;

public class Dragonfly : Aircraft
{
	[Header("Lift Origins")]
	public Transform leftThrusterOrigin;
	public Transform rightThrusterOrigin;
	public Transform leftWingOrigin;
	public Transform rightWingOrigin;

	[Header("Stage Separation")]
	public GameObject leftThrusterGeometry;
	public GameObject rightThrusterGeometry;
	public GameObject leftThrusterDebrisPrefab;
	public GameObject rightThrusterDebrisPrefab;
	public GameObject explosiveBoltPrefab;

	[Header("Wing Animation")]
	public Animator wingAnimator;
	public float wingFlapDampTime = 0.15f;
	private readonly int flapThrustHash = Animator.StringToHash("FlapThrust");
	private float currentFlapVelocity;
	private float currentFlapValue;

	[Header("Cargo System")]
	public Transform cargoPoint;
	public float cargoGrabRadius = 5f;
	public LayerMask cargoMask;
	public float cargoReelSpeed = 2f;
	public float maxCargoSwayAngle = 15f;
	[Range(0f, 1f)] public float cargoZWeightCompensation = 1f;
	public float cargoAlignSpeed = 50f;   // The rotational torque applied to spin the cargo
	public float cargoAlignDamper = 10f;  // The braking force applied to stop the spin from overshooting

	private bool carryingCargo = false;
	private GameObject currentCargo;
	private Rigidbody currentCargoRb;
	private Transform activeCarrierPoint;
	private ConfigurableJoint cargoJoint;
	private Vector3 currentReelPosition;

	[Header("Space Physics (6DOF)")]
	public float spaceStrafeForce = 40f;
	public float spacePitchForce = 15f;
	public float spaceRollForce = 15f;
	public float spaceYawForce = 10f;
	public float spaceMaxSpeed = 80f;

	[Header("Aero Physics (Arcade Jet)")]
	public float aeroThrustForce = 80f;
	public float aeroPitchForce = 15f;
	public float aeroRollForce = 20f;
	public float aeroYawForce = 5f;
	public float aeroMinSpeed = 20f;
	public float aeroMaxSpeed = 120f;

	[Header("Aero Directional Drag")]
	public float aeroForwardDrag = 0.1f;
	public float aeroLateralDrag = 3f;
	public float aeroVerticalDrag = 8f;
	public float aeroBrakeDrag = 15f;

	[Header("Helicopter Physics")]
	public float heliLiftForce = 15f;
	public float heliPitchForce = 1f;
	public float heliRollForce = 1f;
	public float heliYawForce = 1f;
	public float heliLevelStrength = 20f;
	public float heliMaxSpeed = 60f;

	protected override void Awake()
	{
		base.Awake();
		currentFlightMode = FlightMode.Space;
	}

	public override void ToggleFlightMode()
	{
		if (inAtmosphere && currentFlightMode != FlightMode.Helicopter)
		{
			currentFlightMode = currentFlightMode == FlightMode.Space ? FlightMode.Aero : FlightMode.Space;
		}
	}

	public override void DetachStage()
	{
		if (inAtmosphere && currentFlightMode != FlightMode.Helicopter)
		{
			PerformStageSeparation();
			currentFlightMode = FlightMode.Helicopter;
		}
	}

	public override void ToggleCargoHook()
	{
		if (cargoPoint == null) return;

		if (!carryingCargo)
		{
			Collider[] hits = Physics.OverlapSphere(cargoPoint.position, cargoGrabRadius, cargoMask);
			Collider closestHit = null;
			float closestDist = Mathf.Infinity;

			foreach (Collider hit in hits)
			{
				float dist = Vector3.Distance(cargoPoint.position, hit.transform.position);
				if (dist < closestDist)
				{
					closestDist = dist;
					closestHit = hit;
				}
			}

			if (closestHit != null)
			{
				currentCargoRb = closestHit.attachedRigidbody;

				if (currentCargoRb != null)
				{
					currentCargo = currentCargoRb.gameObject;
					activeCarrierPoint = closestHit.transform;

					float alignmentDot = Vector3.Dot(currentCargo.transform.forward, cargoPoint.forward);
					Vector3 targetForward = alignmentDot >= 0f ? cargoPoint.forward : -cargoPoint.forward;

					currentCargo.transform.rotation = Quaternion.LookRotation(targetForward, cargoPoint.up);

					cargoJoint = currentCargo.AddComponent<ConfigurableJoint>();
					cargoJoint.connectedBody = rb;
					cargoJoint.autoConfigureConnectedAnchor = false;

					cargoJoint.anchor = currentCargo.transform.InverseTransformPoint(activeCarrierPoint.position);

					currentReelPosition = transform.InverseTransformPoint(activeCarrierPoint.position);
					cargoJoint.connectedAnchor = currentReelPosition;

					cargoJoint.xMotion = ConfigurableJointMotion.Locked;
					cargoJoint.yMotion = ConfigurableJointMotion.Locked;
					cargoJoint.zMotion = ConfigurableJointMotion.Locked;

					cargoJoint.angularXMotion = ConfigurableJointMotion.Limited;
					cargoJoint.angularYMotion = ConfigurableJointMotion.Free;
					cargoJoint.angularZMotion = ConfigurableJointMotion.Limited;

					SoftJointLimit swayLimit = new SoftJointLimit { limit = maxCargoSwayAngle };
					cargoJoint.lowAngularXLimit = new SoftJointLimit { limit = -maxCargoSwayAngle };
					cargoJoint.highAngularXLimit = swayLimit;
					cargoJoint.angularZLimit = swayLimit;

					carryingCargo = true;
				}
			}
		}
		else
		{
			if (cargoJoint != null) Destroy(cargoJoint);
			if (currentCargoRb != null) currentCargoRb.WakeUp();

			currentCargo = null;
			currentCargoRb = null;
			activeCarrierPoint = null;
			carryingCargo = false;
		}
	}

	private void PerformStageSeparation()
	{
		DetachThruster(leftThrusterGeometry, leftThrusterOrigin, leftThrusterDebrisPrefab);
		DetachThruster(rightThrusterGeometry, rightThrusterOrigin, rightThrusterDebrisPrefab);

		if (wingAnimator != null) wingAnimator.SetTrigger("UnfoldWings");
	}

	private void DetachThruster(GameObject thruster, Transform origin, GameObject debrisPrefab)
	{
		if (thruster != null && origin != null)
		{
			if (explosiveBoltPrefab != null) Instantiate(explosiveBoltPrefab, origin.position, origin.rotation);

			if (debrisPrefab != null)
			{
				GameObject debris = Instantiate(debrisPrefab, thruster.transform.position, thruster.transform.rotation);
				Rigidbody debrisRb = debris.GetComponent<Rigidbody>();
				if (debrisRb == null) debrisRb = debris.AddComponent<Rigidbody>();

				debrisRb.mass = 5f;
				Vector3 ejectionForce = (-transform.up + -transform.forward).normalized * 10f;
				debrisRb.AddForce(ejectionForce, ForceMode.Impulse);
				debrisRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
			}
			thruster.SetActive(false);
		}
	}

	private void Update()
	{
		UpdateWingAnimation();

		if (carryingCargo && cargoJoint != null && activeCarrierPoint != null)
		{
			Vector3 targetReelPosition = transform.InverseTransformPoint(cargoPoint.position);
			currentReelPosition = Vector3.Lerp(currentReelPosition, targetReelPosition, Time.deltaTime * cargoReelSpeed);
			cargoJoint.connectedAnchor = currentReelPosition;
		}
	}

	protected override void FixedUpdate()
	{
		base.FixedUpdate();
		if (!isOccupied) return;

		ApplyCargoWeightCompensation();
		ApplyCargoAlignment();

		switch (currentFlightMode)
		{
			case FlightMode.Space:
				ApplySpacePhysics();
				ApplySpeedLimits(spaceMaxSpeed);
				break;
			case FlightMode.Aero:
				ApplyAeroPhysics();
				ApplySpeedLimits(aeroMaxSpeed);
				break;
			case FlightMode.Helicopter:
				ApplyHelicopterPhysics();
				ApplySpeedLimits(heliMaxSpeed);
				break;
		}
	}

	private void ApplyCargoWeightCompensation()
	{
		if (carryingCargo && currentCargoRb != null && cargoZWeightCompensation > 0f)
		{
			float cargoGravityForce = currentCargoRb.mass * Mathf.Abs(Physics.gravity.y);
			Vector3 antiGravityThrust = Vector3.up * (cargoGravityForce * cargoZWeightCompensation);
			rb.AddForceAtPosition(antiGravityThrust, cargoPoint.position, ForceMode.Force);
		}
	}

	private void ApplyCargoAlignment()
	{
		if (carryingCargo && currentCargoRb != null && cargoPoint != null)
		{
			// 1. Determine if the cargo should align forward or backward to take the shortest spin
			float alignmentDot = Vector3.Dot(currentCargo.transform.forward, cargoPoint.forward);
			Vector3 targetForward = alignmentDot >= 0f ? cargoPoint.forward : -cargoPoint.forward;

			// 2. Project both vectors onto the winch's flat plane to strictly isolate the Y-axis twist
			Vector3 projectedForward = Vector3.ProjectOnPlane(currentCargo.transform.forward, cargoPoint.up).normalized;
			Vector3 projectedTarget = Vector3.ProjectOnPlane(targetForward, cargoPoint.up).normalized;

			// 3. Generate the rotational force
			Vector3 alignTorque = Vector3.Cross(projectedForward, projectedTarget);
			currentCargoRb.AddTorque(alignTorque * cargoAlignSpeed, ForceMode.Acceleration);

			// 4. Apply a targeted damper strictly to the twist axis to prevent endless wobbling/overshooting
			Vector3 twistVelocity = Vector3.Project(currentCargoRb.angularVelocity, cargoPoint.up);
			currentCargoRb.AddTorque(-twistVelocity * cargoAlignDamper, ForceMode.Acceleration);
		}
	}

	private void ApplySpacePhysics()
	{
		Vector3 pitch = transform.right * currentPitch * spacePitchForce;
		Vector3 roll = transform.forward * currentRoll * spaceRollForce;
		Vector3 yaw = transform.up * currentYaw * spaceYawForce;
		rb.AddTorque(pitch + roll + yaw, ForceMode.Acceleration);

		Vector3 strafe = (transform.right * currentStrafeX) + (transform.up * currentLift) + (transform.forward * currentStrafeZ);
		rb.AddForce(strafe * spaceStrafeForce, ForceMode.Acceleration);
	}

	private void ApplyAeroPhysics()
	{
		Vector3 pitch = transform.right * currentPitch * aeroPitchForce;
		Vector3 roll = transform.forward * currentRoll * aeroRollForce;
		Vector3 yaw = transform.up * currentYaw * aeroYawForce;
		rb.AddTorque(pitch + roll + yaw, ForceMode.Acceleration);

		float forwardThrust = Mathf.Max(0f, currentLift);
		float brakeInput = Mathf.Max(0f, -currentLift);

		if (forwardThrust != 0f)
		{
			rb.AddForce(transform.forward * (forwardThrust * aeroThrustForce), ForceMode.Acceleration);
		}

		float currentSpeed = rb.linearVelocity.magnitude;
		float speedFactor = Mathf.Clamp01(currentSpeed / aeroMinSpeed);

		Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

		float activeLateralDrag = aeroLateralDrag * speedFactor;
		float activeVerticalDrag = aeroVerticalDrag * speedFactor;
		float activeForwardDrag = aeroForwardDrag + (brakeInput * aeroBrakeDrag);

		localVel.x = Mathf.Lerp(localVel.x, 0f, activeLateralDrag * Time.fixedDeltaTime);
		localVel.y = Mathf.Lerp(localVel.y, 0f, activeVerticalDrag * Time.fixedDeltaTime);
		localVel.z = Mathf.Lerp(localVel.z, 0f, activeForwardDrag * Time.fixedDeltaTime);

		rb.linearVelocity = transform.TransformDirection(localVel);

		Vector3 velDir = currentSpeed > 0.1f ? rb.linearVelocity.normalized : transform.forward;

		float forwardAlignment = Mathf.Clamp01(Vector3.Dot(velDir, transform.forward));
		float uprightAlignment = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));

		float liftRatio = speedFactor * forwardAlignment * uprightAlignment;
		rb.AddForce(-Physics.gravity * liftRatio, ForceMode.Acceleration);
	}

	private void ApplyHelicopterPhysics()
	{
		Vector3 pitch = transform.right * currentPitch * heliPitchForce;
		Vector3 roll = transform.forward * currentRoll * heliRollForce;
		Vector3 yaw = transform.up * currentYaw * heliYawForce;
		rb.AddTorque(pitch + roll + yaw, ForceMode.Acceleration);

		Vector3 totalLift = transform.up * currentLift * heliLiftForce;
		Vector3 halfLift = totalLift / 2f;

		if (leftWingOrigin != null) rb.AddForceAtPosition(halfLift, leftWingOrigin.position, ForceMode.Acceleration);
		if (rightWingOrigin != null) rb.AddForceAtPosition(halfLift, rightWingOrigin.position, ForceMode.Acceleration);

		Vector3 levelTorque = Vector3.Cross(transform.up, Vector3.up);
		rb.AddTorque(levelTorque * heliLevelStrength, ForceMode.Acceleration);
	}

	private void ApplySpeedLimits(float maxSpeed)
	{
		if (rb.linearVelocity.magnitude > maxSpeed)
		{
			rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeed);
		}
	}

	private void UpdateWingAnimation()
	{
		if (wingAnimator != null && currentFlightMode == FlightMode.Helicopter)
		{
			float targetThrust = Mathf.Clamp01(Mathf.Abs(currentLift));
			currentFlapValue = Mathf.SmoothDamp(currentFlapValue, targetThrust, ref currentFlapVelocity, wingFlapDampTime);
			wingAnimator.SetFloat(flapThrustHash, currentFlapValue);
		}
	}
}