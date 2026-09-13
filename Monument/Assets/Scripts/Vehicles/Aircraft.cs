using UnityEngine;

public abstract class Aircraft : Vehicle
{
	public enum FlightMode { Space, Aero, Helicopter }

	[Header("Aircraft Setup")]
	public FlightMode currentFlightMode = FlightMode.Space;
	public bool inAtmosphere = true;

	protected float currentLift = 0f;
	protected float currentPitch = 0f;
	protected float currentRoll = 0f;
	protected float currentYaw = 0f;
	protected float currentStrafeX = 0f;
	protected float currentStrafeZ = 0f;

	protected override void Awake()
	{
		base.Awake();
	}

	// NEW: API endpoint for the vehicle controller
	public void SetFlightInputs(float lift, float pitch, float roll, float yaw, float strafeX, float strafeZ)
	{
		currentLift = lift;
		currentPitch = pitch;
		currentRoll = roll;
		currentYaw = yaw;
		currentStrafeX = strafeX;
		currentStrafeZ = strafeZ;
	}

	// NEW: API endpoints for unique vehicle features
	public virtual void ToggleFlightMode() { }
	public virtual void DetachStage() { }
	public virtual void ToggleCargoHook() { }

	protected virtual void FixedUpdate()
	{
		if (!isOccupied) return;
		ApplyEnvironmentPhysics();
	}

	private void ApplyEnvironmentPhysics()
	{
		if (currentFlightMode == FlightMode.Space && !inAtmosphere) rb.useGravity = false;
		else rb.useGravity = true;
	}
}