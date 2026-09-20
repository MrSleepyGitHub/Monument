using UnityEngine;

[RequireComponent(typeof(humanoidMotor))]
public class DummyController : MonoBehaviour
{
	private humanoidMotor motor;

	private void Awake()
	{
		motor = GetComponent<humanoidMotor>();
	}

	private void OnEnable()
	{
		if (motor != null)
		{
			motor.SetMoveInput(Vector2.zero);
			motor.SetSprintInput(false);
			motor.SetJumpHeld(false);
			motor.Rotate(Vector3.zero);
			motor.RotateCamera(0f);
		}
	}
}