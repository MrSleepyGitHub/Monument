using UnityEngine;

public class Bullfrog : GroundVehicle
{
	protected override void Awake()
	{
		base.Awake();

		rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

		steeringType = SteeringType.FrontWheel;
		vehicleWeight = 1500f;
		enginePower = 30f;
		engineTorque = 1500f;
		brakeForce = 4000f;
		idleBrakeForce = 500f;
		boostPower = 2000f;
		boostDuration = 3f;
		maxSteerAngle = 25f;
		steeringSpeed = 60f;
		suspensionDistance = 0.3f;
		springForce = 35000f;
		springDamper = 4500f;
		forwardGrip = 1f;
		sidewaysGrip = 1.5f;
	}
}