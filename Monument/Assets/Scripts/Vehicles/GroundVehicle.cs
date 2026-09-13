using UnityEngine;

public abstract class GroundVehicle : Vehicle
{
	public enum SteeringType { FrontWheel, RearWheel, FourWheel }

	[Header("Ground Vehicle Setup")]
	public SteeringType steeringType = SteeringType.FourWheel;

	[Header("Wheel Colliders")]
	public WheelCollider frontLeftCollider;
	public WheelCollider frontRightCollider;
	public WheelCollider rearLeftCollider;
	public WheelCollider rearRightCollider;

	[Header("Wheel Visual Meshes")]
	public Transform frontLeftMesh;
	public Transform frontRightMesh;
	public Transform rearLeftMesh;
	public Transform rearRightMesh;

	[Header("Engine & Performance")]
	public float enginePower = 30f;
	public float engineTorque = 1500f;
	public float brakeForce = 4000f;
	public float idleBrakeForce = 500f;
	public float boostPower = 2000f;
	public float boostDuration = 3f;

	[Header("Steering")]
	public float maxSteerAngle = 25f;
	public float steeringSpeed = 60f;

	[Header("Suspension Tuning")]
	public float suspensionDistance = 0.3f;
	public float springForce = 35000f;
	public float springDamper = 4500f;

	[Header("Friction & Grip Tuning")]
	public float forwardGrip = 1f;
	public float sidewaysGrip = 1f;

	protected WheelFrictionCurve baseForwardFriction;
	protected WheelFrictionCurve baseSidewaysFriction;

	protected float currentDrive = 0f;
	protected float currentSteerInput = 0f;
	protected float currentSteerAngle = 0f;
	protected float currentBrake = 0f;

	protected bool isBoosting = false;
	protected float currentBoostTimer;

	public GroundVehicle()
	{
		baseForwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1f };
		baseSidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.2f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 1f };
	}

	protected override void Awake()
	{
		base.Awake();
		currentBoostTimer = boostDuration;
	}

	// NEW: API endpoint for the vehicle controller
	public void SetMotorInputs(float drive, float steer, float brake, bool boost)
	{
		currentDrive = drive;
		currentSteerInput = steer;
		currentBrake = brake;
		isBoosting = boost;
	}

	protected virtual void Update()
	{
		UpdateWheelVisuals(frontLeftCollider, frontLeftMesh);
		UpdateWheelVisuals(frontRightCollider, frontRightMesh);
		UpdateWheelVisuals(rearLeftCollider, rearLeftMesh);
		UpdateWheelVisuals(rearRightCollider, rearRightMesh);
	}

	protected virtual void FixedUpdate()
	{
		ApplyRealtimeTuning();

		if (!isOccupied)
		{
			ApplyParkingBrake();
			return;
		}

		ApplySteering();
		ApplyDrive();
		ApplyBraking();
	}

	protected void ApplyRealtimeTuning()
	{
		if (rb.mass != vehicleWeight) rb.mass = vehicleWeight;

		JointSpring customSpring = new JointSpring
		{
			spring = springForce,
			damper = springDamper,
			targetPosition = 0.5f
		};

		baseForwardFriction.stiffness = forwardGrip;
		baseSidewaysFriction.stiffness = sidewaysGrip;

		ApplyWheelSettings(frontLeftCollider, customSpring);
		ApplyWheelSettings(frontRightCollider, customSpring);
		ApplyWheelSettings(rearLeftCollider, customSpring);
		ApplyWheelSettings(rearRightCollider, customSpring);
	}

	private void ApplyWheelSettings(WheelCollider col, JointSpring spring)
	{
		if (col == null) return;

		col.suspensionDistance = suspensionDistance;
		col.suspensionSpring = spring;
		col.forwardFriction = baseForwardFriction;
		col.sidewaysFriction = baseSidewaysFriction;
	}

	protected void ApplySteering()
	{
		float targetAngle = currentSteerInput * maxSteerAngle;
		currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetAngle, steeringSpeed * Time.fixedDeltaTime);

		if (steeringType == SteeringType.FrontWheel || steeringType == SteeringType.FourWheel)
		{
			if (frontLeftCollider != null) frontLeftCollider.steerAngle = currentSteerAngle;
			if (frontRightCollider != null) frontRightCollider.steerAngle = currentSteerAngle;
		}
		else
		{
			if (frontLeftCollider != null) frontLeftCollider.steerAngle = 0f;
			if (frontRightCollider != null) frontRightCollider.steerAngle = 0f;
		}

		if (steeringType == SteeringType.FourWheel)
		{
			if (rearLeftCollider != null) rearLeftCollider.steerAngle = -currentSteerAngle;
			if (rearRightCollider != null) rearRightCollider.steerAngle = -currentSteerAngle;
		}
		else if (steeringType == SteeringType.RearWheel)
		{
			if (rearLeftCollider != null) rearLeftCollider.steerAngle = currentSteerAngle;
			if (rearRightCollider != null) rearRightCollider.steerAngle = currentSteerAngle;
		}
		else
		{
			if (rearLeftCollider != null) rearLeftCollider.steerAngle = 0f;
			if (rearRightCollider != null) rearRightCollider.steerAngle = 0f;
		}
	}

	protected void ApplyDrive()
	{
		if (isBoosting && currentBoostTimer > 0f)
		{
			currentBoostTimer -= Time.fixedDeltaTime;
		}
		else if (!isBoosting && currentBoostTimer < boostDuration)
		{
			currentBoostTimer += Time.fixedDeltaTime;
		}

		bool boostActive = isBoosting && currentBoostTimer > 0f && currentDrive > 0f;
		float activeTorque = boostActive ? engineTorque + boostPower : engineTorque;
		float activeMaxSpeed = boostActive ? enginePower * 1.5f : enginePower;

		float currentSpeed = rb.linearVelocity.magnitude;
		float appliedTorque = (currentSpeed < activeMaxSpeed && currentBrake == 0f) ? currentDrive * activeTorque : 0f;

		if (frontLeftCollider != null) frontLeftCollider.motorTorque = appliedTorque;
		if (frontRightCollider != null) frontRightCollider.motorTorque = appliedTorque;
		if (rearLeftCollider != null) rearLeftCollider.motorTorque = appliedTorque;
		if (rearRightCollider != null) rearRightCollider.motorTorque = appliedTorque;
	}

	protected void ApplyBraking()
	{
		float appliedBrake = currentBrake * brakeForce;

		if (currentDrive == 0f && currentBrake == 0f)
		{
			appliedBrake = idleBrakeForce;
		}

		if (frontLeftCollider != null) frontLeftCollider.brakeTorque = appliedBrake;
		if (frontRightCollider != null) frontRightCollider.brakeTorque = appliedBrake;
		if (rearLeftCollider != null) rearLeftCollider.brakeTorque = appliedBrake;
		if (rearRightCollider != null) rearRightCollider.brakeTorque = appliedBrake;
	}

	protected void ApplyParkingBrake()
	{
		if (frontLeftCollider != null) { frontLeftCollider.motorTorque = 0f; frontLeftCollider.brakeTorque = brakeForce; }
		if (frontRightCollider != null) { frontRightCollider.motorTorque = 0f; frontRightCollider.brakeTorque = brakeForce; }
		if (rearLeftCollider != null) { rearLeftCollider.motorTorque = 0f; rearLeftCollider.brakeTorque = brakeForce; }
		if (rearRightCollider != null) { rearRightCollider.motorTorque = 0f; rearRightCollider.brakeTorque = brakeForce; }
	}

	protected void UpdateWheelVisuals(WheelCollider col, Transform mesh)
	{
		if (col == null || mesh == null) return;

		col.GetWorldPose(out Vector3 position, out Quaternion rotation);
		mesh.position = position;
		mesh.rotation = rotation;
	}
}