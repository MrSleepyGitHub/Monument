using UnityEngine;

[RequireComponent(typeof(playerController))]
public class PlayerVehicleController : MonoBehaviour
{
	[Header("Input Setup")]
	public KeybindFramework input;

	public Vehicle activeVehicle { get; set; }
	private playerController pController;
	private bool wasPaused = false;

	private void Awake()
	{
		pController = GetComponent<playerController>();
		this.enabled = false; // Remains dormant until entering a vehicle
	}

	private void Update()
	{
		if (activeVehicle == null || input == null) return;

		// --- Pause Menu Shield ---
		// If the MenuManager disables the core player script, zero out all vehicle inputs
		if (!pController.enabled)
		{
			wasPaused = true;
			if (activeVehicle is GroundVehicle gv) gv.SetMotorInputs(0f, 0f, 0f, false);
			if (activeVehicle is Aircraft ac) ac.SetFlightInputs(0f, 0f, 0f, 0f, 0f, 0f);
			activeVehicle.SetCameraInputs(0f, false, Vector2.zero, Vector2.zero);
			return;
		}

		// --- Camera Processing ---
		float scroll = input.vehicleCameraZoom.ReadValue<float>();
		bool freeLook = input.vehicleFreeLook.IsPressed();
		Vector2 mouseDelta = input.vehicleLookDelta.ReadValue<Vector2>();
		Vector2 joystickLook = input.vehicleLookAxis.ReadValue<Vector2>();

		// Eats the massive hardware delta spike created by navigating the pause menu
		if (wasPaused)
		{
			mouseDelta = Vector2.zero;
			wasPaused = false;
		}

		activeVehicle.SetCameraInputs(scroll, freeLook, mouseDelta, joystickLook);

		if (input.vehicleToggleCamera.triggered) activeVehicle.ToggleCamera();

		// --- Interaction ---
		if (input.interact.triggered)
		{
			activeVehicle.ExitVehicle();
			return;
		}

		// --- Vehicle-Specific Input Routing ---
		if (activeVehicle is GroundVehicle groundVehicle)
		{
			float drive = 0f;
			float steer = 0f;
			float brake = 0f;
			bool boost = input.vehicleBoost.IsPressed();

			if (input.vehicleForward.IsPressed()) drive += 1f;
			if (input.vehicleBackward.IsPressed()) drive -= 1f;
			if (input.vehicleSteerRight.IsPressed()) steer += 1f;
			if (input.vehicleSteerLeft.IsPressed()) steer -= 1f;
			if (input.vehicleBrake.IsPressed()) brake = 1f;

			Vector2 axisInput = input.vehicleDriveAxis.ReadValue<Vector2>();
			if (axisInput != Vector2.zero)
			{
				steer = axisInput.x;
				drive = axisInput.y;
			}

			groundVehicle.SetMotorInputs(drive, steer, brake, boost);
		}
		else if (activeVehicle is Aircraft aircraft)
		{
			float lift = 0f, pitch = 0f, roll = 0f, yaw = 0f, strafeX = 0f, strafeZ = 0f;

			if (input.aircraftLiftUp.IsPressed()) lift += 1f;
			if (input.aircraftLiftDown.IsPressed()) lift -= 1f;
			if (input.aircraftPitchDown.IsPressed()) pitch += 1f;
			if (input.aircraftPitchUp.IsPressed()) pitch -= 1f;
			if (input.aircraftRollLeft.IsPressed()) roll += 1f;
			if (input.aircraftRollRight.IsPressed()) roll -= 1f;
			if (input.aircraftYawLeft.IsPressed()) yaw -= 1f;
			if (input.aircraftYawRight.IsPressed()) yaw += 1f;
			if (input.aircraftStrafeForward.IsPressed()) strafeZ += 1f;
			if (input.aircraftStrafeBackward.IsPressed()) strafeZ -= 1f;
			if (input.aircraftStrafeRight.IsPressed()) strafeX += 1f;
			if (input.aircraftStrafeLeft.IsPressed()) strafeX -= 1f;

			aircraft.SetFlightInputs(lift, pitch, roll, yaw, strafeX, strafeZ);

			if (input.aircraftToggleMode.triggered) aircraft.ToggleFlightMode();
			if (input.aircraftDetach.triggered) aircraft.DetachStage();
			if (input.cargoToggle.triggered) aircraft.ToggleCargoHook();
		}
	}
}