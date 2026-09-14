using UnityEngine;

[RequireComponent(typeof(humanoidMotor))]
public class DummyController : MonoBehaviour
{
	[Header("Testing Controls")]
	[Tooltip("Change this in the Inspector during play mode to test ragdolls from different stances.")]
	public humanoidMotor.PlayerStance initialStance = humanoidMotor.PlayerStance.Standing;

	private humanoidMotor motor;
	private Health health;

	private void Awake()
	{
		motor = GetComponent<humanoidMotor>();
		health = GetComponent<Health>();
	}

	private void Start()
	{
		if (motor != null)
		{
			// Zero all locomotion inputs
			motor.SetMoveInput(Vector2.zero);
			motor.SetSprintInput(false);
			motor.SetJumpHeld(false);
			motor.Rotate(Vector3.zero);
			motor.RotateCamera(0f);

			// Apply starting stance test
			if (initialStance == humanoidMotor.PlayerStance.Crouching)
			{
				motor.ToggleCrouch();
			}
			else if (initialStance == humanoidMotor.PlayerStance.Proning)
			{
				motor.ToggleProne();
			}
		}
	}

	private void OnEnable()
	{
		if (health != null)
		{
			health.OnDeath.AddListener(OnDeath);
		}
	}

	private void OnDisable()
	{
		if (health != null)
		{
			health.OnDeath.RemoveListener(OnDeath);
		}
	}

	private void OnDeath()
	{
		enabled = false;
	}

	// Right-click the component header in the Inspector during Play Mode to kill instantly
	[ContextMenu("Test Instant Death")]
	public void TestDeath()
	{
		if (health != null)
		{
			health.TakeDamage(9999f);
		}
	}
}