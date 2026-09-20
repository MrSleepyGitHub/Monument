using UnityEngine;
using UnityEngine.InputSystem;

public class playerController : MonoBehaviour
{
	[Header("Master / Scene Camera")]
	[SerializeField] private Camera masterCamera;
	public Camera MasterCamera => masterCamera;

	[Header("Active Motor")]
	[SerializeField] private humanoidMotor currentMotor;
	public humanoidMotor CurrentMotor => currentMotor;

	// Backward-compatibility properties for HUD and Vehicle systems
	public humanoidMotor motor => currentMotor;
	public Weapon currentWeapon => currentMotor != null ? currentMotor.currentWeapon : null;
	public Camera playerCamera => currentMotor != null ? currentMotor.playerCamera : null;

	[Header("Look Sensitivities")]
	[SerializeField] private float mouseSens = 10f;
	[SerializeField] private float mouseSensMultiplier = 0.01f;
	[SerializeField] private float joystickLookSens = 150f;

	[Header("Tag-Based Interaction")]
	[SerializeField] private float interactRange = 3f;
	[Tooltip("Tag required on an object (or its root) to trigger IInteractable.")]
	[SerializeField] private string interactableTag = "Interactable";
	[Tooltip("World layers tested by the interaction raycast. FP arms and player culled layers are automatically excluded.")]
	[SerializeField] private LayerMask interactRaycastMask = ~0;

	[Header("Vehicle Control")]
	public bool inVehicle = false;
	public Vehicle activeVehicle { get; private set; }

	[Header("Input Framework")]
	[SerializeField] private KeybindFramework input;

	// Cached Actions
	private InputAction walkAxisAction;
	private InputAction lookAxisAction;
	private InputAction walkForwardsAction;
	private InputAction walkBackwardsAction;
	private InputAction strafeLeftAction;
	private InputAction strafeRightAction;
	private InputAction sprintAction;
	private InputAction jumpAction;
	private InputAction crouchAction;
	private InputAction proneAction;
	private InputAction interactAction;
	private InputAction fireAction;
	private InputAction reloadAction;
	private InputAction dropAction;

	// Vehicle Camera Actions
	private InputAction vehicleCameraZoomAction;
	private InputAction vehicleFreeLookAction;
	private InputAction vehicleLookDeltaAction;
	private InputAction vehicleLookAxisAction;
	private InputAction vehicleToggleCameraAction;

	// Ground Vehicle Actions
	private InputAction vehicleDriveAxisAction;
	private InputAction vehicleForwardAction;
	private InputAction vehicleBackwardAction;
	private InputAction vehicleSteerLeftAction;
	private InputAction vehicleSteerRightAction;
	private InputAction vehicleBrakeAction;
	private InputAction vehicleBoostAction;

	// Aircraft Actions
	private InputAction aircraftLiftUpAction;
	private InputAction aircraftLiftDownAction;
	private InputAction aircraftPitchDownAction;
	private InputAction aircraftPitchUpAction;
	private InputAction aircraftRollLeftAction;
	private InputAction aircraftRollRightAction;
	private InputAction aircraftYawLeftAction;
	private InputAction aircraftYawRightAction;
	private InputAction aircraftToggleModeAction;

	private InputAction aircraftPitchAxisAction;
	private InputAction aircraftRollAxisAction;
	private InputAction aircraftYawAxisAction;
	private InputAction aircraftThrottleAxisAction;

	private bool ignoreNextDelta = true;
	private bool wasPaused = false;

	private void Awake()
	{
		if (masterCamera == null)
		{
			masterCamera = GetComponentInChildren<Camera>(true);
			if (masterCamera == null && transform.parent != null)
			{
				masterCamera = transform.parent.GetComponentInChildren<Camera>(true);
			}
		}

		ConfigureInteractLayers();
	}

	private void Start()
	{
		if (input == null && PlayerMaster.Instance != null)
		{
			input = PlayerMaster.Instance.keybindFramework;
		}

		CacheInputActions();

		if (PlayerMaster.Instance == null || !PlayerMaster.Instance.IsPaused)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	private void ConfigureInteractLayers()
	{
		// Exclude first-person arms and local body from blocking interaction rays
		int fpLayer = LayerMask.NameToLayer("FPS_Arms");
		int tpLayer = LayerMask.NameToLayer("LocalPlayer_TP");

		if (fpLayer != -1) interactRaycastMask &= ~(1 << fpLayer);
		if (tpLayer != -1) interactRaycastMask &= ~(1 << tpLayer);
	}

	private void OnEnable() => ignoreNextDelta = true;

	private void OnDisable()
	{
		if (currentMotor != null)
		{
			currentMotor.SetMoveInput(Vector2.zero);
			currentMotor.SetSprintInput(false);
			currentMotor.SetJumpHeld(false);
		}

		if (inVehicle && activeVehicle != null)
		{
			ResetVehicleInputs();
		}
	}

	public bool IsControllingAliveEntity()
	{
		if (currentMotor == null) return false;

		if (currentMotor.isDead || currentMotor.currentHealth <= 0f)
		{
			return false;
		}

		if (!currentMotor.enabled && !inVehicle)
		{
			return false;
		}

		return true;
	}

	public void Possess(humanoidMotor newMotor)
	{
		if (currentMotor != null)
		{
			currentMotor.Unpossess();
		}

		currentMotor = newMotor;

		if (currentMotor != null)
		{
			if (masterCamera != null)
			{
				masterCamera.gameObject.SetActive(false);
			}

			AudioListener[] activeListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (AudioListener listener in activeListeners)
			{
				if (listener.transform.IsChildOf(currentMotor.transform)) continue;
				listener.enabled = false;
			}

			currentMotor.Possess(this);
		}
		else
		{
			RestoreMasterCamera();
		}
	}

	public void UnpossessCurrent()
	{
		if (currentMotor != null)
		{
			currentMotor.Unpossess();
			currentMotor = null;
		}

		RestoreMasterCamera();
	}

	private void RestoreMasterCamera()
	{
		if (masterCamera != null)
		{
			masterCamera.gameObject.SetActive(true);
			AudioListener listener = masterCamera.GetComponent<AudioListener>();
			if (listener != null) listener.enabled = true;
		}
	}

	public void DisableForVehicle(Vehicle vehicle)
	{
		inVehicle = true;
		activeVehicle = vehicle;

		if (currentMotor != null)
		{
			currentMotor.inVehicle = true;
			currentMotor.SetCameraActive(false);

			Rigidbody rb = currentMotor.GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.isKinematic = true;
				rb.detectCollisions = false;
				rb.interpolation = RigidbodyInterpolation.None;
			}

			if (currentMotor.playerCollider != null)
			{
				currentMotor.playerCollider.enabled = false;
			}
		}
	}

	public void EnableFromVehicle()
	{
		inVehicle = false;
		activeVehicle = null;

		if (currentMotor != null)
		{
			currentMotor.inVehicle = false;
			currentMotor.SetCameraActive(true);

			Rigidbody rb = currentMotor.GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.isKinematic = false;
				rb.detectCollisions = true;
				rb.interpolation = RigidbodyInterpolation.Interpolate;
			}

			if (currentMotor.playerCollider != null)
			{
				currentMotor.playerCollider.enabled = true;
			}
		}
	}

	private void Update()
	{
		if (walkForwardsAction == null) CacheInputActions();

		if (inVehicle)
		{
			HandleVehicleProcessing();
		}
		else if (currentMotor != null)
		{
			HandleInfantryProcessing();
		}
	}

	private void HandleInfantryProcessing()
	{
		// 1. Movement
		float xMov = 0f, zMov = 0f;
		if (strafeRightAction != null && strafeRightAction.IsPressed()) xMov += 1f;
		if (strafeLeftAction != null && strafeLeftAction.IsPressed()) xMov -= 1f;
		if (walkForwardsAction != null && walkForwardsAction.IsPressed()) zMov += 1f;
		if (walkBackwardsAction != null && walkBackwardsAction.IsPressed()) zMov -= 1f;

		if (walkAxisAction != null)
		{
			Vector2 stick = walkAxisAction.ReadValue<Vector2>();
			if (stick.sqrMagnitude > 0.01f)
			{
				xMov = stick.x;
				zMov = stick.y;
			}
		}

		currentMotor.SetMoveInput(new Vector2(xMov, zMov));
		currentMotor.SetSprintInput(sprintAction != null && sprintAction.IsPressed());
		currentMotor.SetJumpHeld(jumpAction != null && jumpAction.IsPressed());

		// 2. Look
		Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
		if (ignoreNextDelta)
		{
			mouseDelta = Vector2.zero;
			ignoreNextDelta = false;
		}

		Vector2 joystickLook = lookAxisAction != null ? lookAxisAction.ReadValue<Vector2>() : Vector2.zero;
		float sens = PlayerMaster.Instance != null ? PlayerMaster.Instance.MouseSensitivity : mouseSens;

		float yaw = (mouseDelta.x * mouseSensMultiplier * sens) + (joystickLook.x * joystickLookSens * Time.deltaTime);
		float pitch = (-mouseDelta.y * mouseSensMultiplier * sens) + (-joystickLook.y * joystickLookSens * Time.deltaTime);

		currentMotor.Rotate(new Vector3(0f, yaw, 0f));
		currentMotor.RotateCamera(pitch);

		// 3. Stances & Actions
		if (jumpAction != null && jumpAction.triggered) currentMotor.Jump();
		if (crouchAction != null && crouchAction.triggered) currentMotor.ToggleCrouch();
		if (proneAction != null && proneAction.triggered) currentMotor.ToggleProne();
		if (interactAction != null && interactAction.triggered) PerformInteract();

		// 4. Weapons
		if (currentMotor.currentWeapon != null)
		{
			bool fireHeld = fireAction != null && fireAction.IsPressed();
			bool fireTrig = fireAction != null && fireAction.triggered;
			currentMotor.currentWeapon.ProcessInput(fireHeld, fireTrig);

			if (fireTrig && currentMotor.VisualController != null)
			{
				currentMotor.VisualController.TriggerFire();
			}

			if (reloadAction != null && reloadAction.triggered) currentMotor.currentWeapon.Reload();
			if (dropAction != null && dropAction.triggered) currentMotor.DropWeapon();
		}
	}

	private void HandleVehicleProcessing()
	{
		if (activeVehicle == null) return;

		if (PlayerMaster.Instance != null && PlayerMaster.Instance.IsPaused)
		{
			wasPaused = true;
			ResetVehicleInputs();
			return;
		}

		float scroll = vehicleCameraZoomAction != null ? vehicleCameraZoomAction.ReadValue<float>() : 0f;
		bool freeLook = vehicleFreeLookAction != null && vehicleFreeLookAction.IsPressed();
		Vector2 mouseDelta = vehicleLookDeltaAction != null ? vehicleLookDeltaAction.ReadValue<Vector2>() : Vector2.zero;
		Vector2 joystickLook = vehicleLookAxisAction != null ? vehicleLookAxisAction.ReadValue<Vector2>() : Vector2.zero;

		if (wasPaused)
		{
			mouseDelta = Vector2.zero;
			wasPaused = false;
		}

		activeVehicle.SetCameraInputs(scroll, freeLook, mouseDelta, joystickLook);

		if (vehicleToggleCameraAction != null && vehicleToggleCameraAction.triggered)
		{
			activeVehicle.ToggleCamera();
		}

		if (interactAction != null && interactAction.triggered)
		{
			activeVehicle.ExitVehicle();
			return;
		}

		if (activeVehicle is GroundVehicle groundVehicle)
		{
			ProcessGroundVehicleInput(groundVehicle);
		}
		else if (activeVehicle is Aircraft aircraft)
		{
			ProcessAircraftInput(aircraft);
		}
	}

	private void ProcessGroundVehicleInput(GroundVehicle gv)
	{
		float drive = 0f, steer = 0f, brake = 0f;
		bool boost = vehicleBoostAction != null && vehicleBoostAction.IsPressed();

		if (vehicleForwardAction != null && vehicleForwardAction.IsPressed()) drive += 1f;
		if (vehicleBackwardAction != null && vehicleBackwardAction.IsPressed()) drive -= 1f;
		if (vehicleSteerRightAction != null && vehicleSteerRightAction.IsPressed()) steer += 1f;
		if (vehicleSteerLeftAction != null && vehicleSteerLeftAction.IsPressed()) steer -= 1f;
		if (vehicleBrakeAction != null && vehicleBrakeAction.IsPressed()) brake = 1f;

		if (vehicleDriveAxisAction != null)
		{
			Vector2 axis = vehicleDriveAxisAction.ReadValue<Vector2>();
			if (axis.sqrMagnitude > 0.01f)
			{
				steer = axis.x;
				drive = axis.y;
			}
		}

		gv.SetMotorInputs(drive, steer, brake, boost);
	}

	private void ProcessAircraftInput(Aircraft ac)
	{
		float lift = 0f, pitch = 0f, roll = 0f, yaw = 0f;

		if (aircraftLiftUpAction != null && aircraftLiftUpAction.IsPressed()) lift += 1f;
		if (aircraftLiftDownAction != null && aircraftLiftDownAction.IsPressed()) lift -= 1f;
		if (aircraftPitchDownAction != null && aircraftPitchDownAction.IsPressed()) pitch += 1f;
		if (aircraftPitchUpAction != null && aircraftPitchUpAction.IsPressed()) pitch -= 1f;
		if (aircraftRollLeftAction != null && aircraftRollLeftAction.IsPressed()) roll += 1f;
		if (aircraftRollRightAction != null && aircraftRollRightAction.IsPressed()) roll -= 1f;
		if (aircraftYawLeftAction != null && aircraftYawLeftAction.IsPressed()) yaw -= 1f;
		if (aircraftYawRightAction != null && aircraftYawRightAction.IsPressed()) yaw += 1f;

		if (aircraftPitchAxisAction != null)
		{
			float val = aircraftPitchAxisAction.ReadValue<float>();
			if (Mathf.Abs(val) > 0.05f) pitch = val;
		}
		if (aircraftRollAxisAction != null)
		{
			float val = aircraftRollAxisAction.ReadValue<float>();
			if (Mathf.Abs(val) > 0.05f) roll = val;
		}
		if (aircraftYawAxisAction != null)
		{
			float val = aircraftYawAxisAction.ReadValue<float>();
			if (Mathf.Abs(val) > 0.05f) yaw = val;
		}
		if (aircraftThrottleAxisAction != null)
		{
			float val = aircraftThrottleAxisAction.ReadValue<float>();
			if (Mathf.Abs(val) > 0.05f) lift = val;
		}

		ac.SetFlightInputs(lift, pitch, roll, yaw, 0f, 0f);

		if (aircraftToggleModeAction != null && aircraftToggleModeAction.triggered)
		{
			ac.ToggleFlightMode();
		}
	}

	private void ResetVehicleInputs()
	{
		if (activeVehicle is GroundVehicle gv) gv.SetMotorInputs(0f, 0f, 0f, false);
		if (activeVehicle is Aircraft ac) ac.SetFlightInputs(0f, 0f, 0f, 0f, 0f, 0f);
		activeVehicle.SetCameraInputs(0f, false, Vector2.zero, Vector2.zero);
	}

	private void PerformInteract()
	{
		if (currentMotor == null || currentMotor.playerCamera == null) return;

		Ray ray = new Ray(currentMotor.playerCamera.transform.position, currentMotor.playerCamera.transform.forward);
		if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactRaycastMask, QueryTriggerInteraction.Ignore))
		{
			// Ignore player's own body colliders
			if (hit.transform == currentMotor.transform || hit.transform.IsChildOf(currentMotor.transform))
				return;

			// 1. Vehicle entry check
			Vehicle vehicle = hit.collider.GetComponentInParent<Vehicle>();
			if (vehicle != null && !vehicle.isOccupied)
			{
				vehicle.EnterVehicle(this);
				return;
			}

			// 2. Tag check on collider, parent, or attached rigidbody
			if (IsTaggedInteractable(hit.collider))
			{
				IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
				if (interactable != null)
				{
					interactable.Interact(gameObject);
				}
			}
		}
	}

	private bool IsTaggedInteractable(Collider col)
	{
		if (col.CompareTag(interactableTag)) return true;
		if (col.transform.root != null && col.transform.root.CompareTag(interactableTag)) return true;
		if (col.attachedRigidbody != null && col.attachedRigidbody.CompareTag(interactableTag)) return true;
		return false;
	}

	private void CacheInputActions()
	{
		if (input == null || input.movementMap == null) return;

		InputActionMap map = input.movementMap;
		walkAxisAction = map.FindAction("WalkAxis");
		lookAxisAction = map.FindAction("LookAxis");
		walkForwardsAction = map.FindAction("WalkForwards");
		walkBackwardsAction = map.FindAction("WalkBackwards");
		strafeLeftAction = map.FindAction("StrafeLeft");
		strafeRightAction = map.FindAction("StrafeRight");
		sprintAction = map.FindAction("Sprint");
		jumpAction = map.FindAction("Jump");
		crouchAction = map.FindAction("Crouch");
		proneAction = map.FindAction("Prone");
		interactAction = map.FindAction("Interact");
		fireAction = map.FindAction("Fire");
		reloadAction = map.FindAction("Reload");
		dropAction = map.FindAction("Drop");

		vehicleCameraZoomAction = map.FindAction("VehicleCameraZoom");
		vehicleFreeLookAction = map.FindAction("VehicleFreeLook");
		vehicleLookDeltaAction = map.FindAction("VehicleLookDelta");
		vehicleLookAxisAction = map.FindAction("VehicleLookAxis");
		vehicleToggleCameraAction = map.FindAction("VehicleToggleCamera");

		vehicleDriveAxisAction = map.FindAction("VehicleDriveAxis");
		vehicleForwardAction = map.FindAction("VehicleForward");
		vehicleBackwardAction = map.FindAction("VehicleBackward");
		vehicleSteerLeftAction = map.FindAction("VehicleSteerLeft");
		vehicleSteerRightAction = map.FindAction("VehicleSteerRight");
		vehicleBrakeAction = map.FindAction("VehicleBrake");
		vehicleBoostAction = map.FindAction("VehicleBoost");

		aircraftLiftUpAction = map.FindAction("AircraftLiftUp");
		aircraftLiftDownAction = map.FindAction("AircraftLiftDown");
		aircraftPitchDownAction = map.FindAction("AircraftPitchDown");
		aircraftPitchUpAction = map.FindAction("AircraftPitchUp");
		aircraftRollLeftAction = map.FindAction("AircraftRollLeft");
		aircraftRollRightAction = map.FindAction("AircraftRollRight");
		aircraftYawLeftAction = map.FindAction("AircraftYawLeft");
		aircraftYawRightAction = map.FindAction("AircraftYawRight");
		aircraftToggleModeAction = map.FindAction("AircraftToggleMode");

		aircraftPitchAxisAction = map.FindAction("AircraftPitchAxis");
		aircraftRollAxisAction = map.FindAction("AircraftRollAxis");
		aircraftYawAxisAction = map.FindAction("AircraftYawAxis");
		aircraftThrottleAxisAction = map.FindAction("AircraftThrottleAxis");
	}
}