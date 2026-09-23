using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class playerController : MonoBehaviour
{
	[Header("Master / Scene Camera")]
	[SerializeField] private Camera masterCamera;
	public Camera MasterCamera => masterCamera;

	[Header("Active Motor")]
	[SerializeField] private humanoidMotor currentMotor;
	public humanoidMotor CurrentMotor => currentMotor;

	public humanoidMotor motor => currentMotor;
	public Weapon currentWeapon => currentMotor != null ? currentMotor.currentWeapon : null;
	public Camera playerCamera => currentMotor != null ? currentMotor.playerCamera : null;

	[Header("Look Sensitivities")]
	[SerializeField] private float mouseSens = 10f;
	[SerializeField] private float mouseSensMultiplier = 0.01f;
	[SerializeField] private float joystickLookSens = 150f;

	[Header("SphereCast Interaction")]
	[SerializeField] private float interactRange = 3.0f;
	[SerializeField] private float interactRadius = 0.4f;
	[SerializeField] private LayerMask interactLayerMask;
	[SerializeField] private float pickupCooldown = 0.35f;

	[Header("Vehicle Control")]
	public bool inVehicle = false;
	public Vehicle activeVehicle { get; private set; }

	[Header("Input Framework")]
	[SerializeField] private KeybindFramework input;

	// Actions
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
	private InputAction aimAction;
	private InputAction reloadAction;
	private InputAction dropAction;

	// Vehicle Actions
	private InputAction vehicleCameraZoomAction;
	private InputAction vehicleFreeLookAction;
	private InputAction vehicleLookDeltaAction;
	private InputAction vehicleLookAxisAction;
	private InputAction vehicleToggleCameraAction;
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
	private float _nextInteractAllowedTime = 0f;

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

		if (interactLayerMask == 0)
		{
			interactLayerMask = LayerMask.GetMask("Interactable");
		}

		if (SceneManager.GetActiveScene().buildIndex == 0)
		{
			DisableMasterCamera();
		}
	}

	private void Start()
	{
		if (input == null && PlayerMaster.Instance != null)
		{
			input = PlayerMaster.Instance.keybindFramework;
		}

		CacheInputActions();

		if (SceneManager.GetActiveScene().buildIndex != 0)
		{
			if (PlayerMaster.Instance == null || !PlayerMaster.Instance.IsPaused)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}
		else
		{
			DisableMasterCamera();
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
	}

	private void OnEnable() => ignoreNextDelta = true;

	private void OnDisable()
	{
		ResetControllerAndMotor();
	}

	public void DisableMasterCamera()
	{
		if (masterCamera != null)
		{
			AudioListener al = masterCamera.GetComponent<AudioListener>();
			if (al != null) al.enabled = false;
			masterCamera.gameObject.SetActive(false);
		}
	}

	public void RestoreMasterCamera()
	{
		if (SceneManager.GetActiveScene().buildIndex == 0)
		{
			DisableMasterCamera();
			return;
		}

		if (masterCamera != null)
		{
			masterCamera.gameObject.SetActive(true);
			AudioListener listener = masterCamera.GetComponent<AudioListener>();
			if (listener != null) listener.enabled = true;
		}
	}

	public void ResetControllerAndMotor()
	{
		if (currentMotor != null)
		{
			currentMotor.SetMoveInput(Vector2.zero);
			currentMotor.SetSprintInput(false);
			currentMotor.SetJumpHeld(false);
			currentMotor.SetAimInput(false);
			currentMotor.Unpossess();
			currentMotor = null;
		}

		if (inVehicle && activeVehicle != null)
		{
			ResetVehicleInputs();
		}

		inVehicle = false;
		activeVehicle = null;
		ignoreNextDelta = true;

		if (SceneManager.GetActiveScene().buildIndex != 0)
		{
			RestoreMasterCamera();
		}
		else
		{
			DisableMasterCamera();
		}
	}

	public bool IsControllingAliveEntity()
	{
		if (currentMotor == null) return false;
		if (currentMotor.isDead || currentMotor.currentHealth <= 0f) return false;
		if (!currentMotor.enabled && !inVehicle) return false;
		return true;
	}

	public void Possess(humanoidMotor newMotor)
	{
		if (currentMotor != null && currentMotor != newMotor)
		{
			currentMotor.Unpossess();
		}

		currentMotor = newMotor;

		if (currentMotor != null)
		{
			DisableMasterCamera();

			// Mute all non-local audio listeners
			AudioListener[] activeListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (AudioListener listener in activeListeners)
			{
				if (listener.transform.IsChildOf(currentMotor.transform))
				{
					listener.enabled = true;
					continue;
				}
				listener.enabled = false;
			}

			if (input == null && PlayerMaster.Instance != null)
			{
				input = PlayerMaster.Instance.keybindFramework;
			}

			if (input != null && input.movementMap != null && !input.movementMap.enabled)
			{
				input.movementMap.Enable();
			}

			CacheInputActions();
			currentMotor.Possess(this);

			// Guarantee cursor capture
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
			ignoreNextDelta = true;
		}
		else
		{
			RestoreMasterCamera();
		}
	}

	public void UnpossessCurrent()
	{
		ResetControllerAndMotor();
	}

	public void DisableForVehicle(Vehicle vehicle)
	{
		inVehicle = true;
		activeVehicle = vehicle;

		if (currentMotor != null)
		{
			currentMotor.inVehicle = true;

			Rigidbody rb = currentMotor.GetComponent<Rigidbody>();
			if (rb != null)
			{
				if (!rb.isKinematic)
				{
					rb.linearVelocity = Vector3.zero;
					rb.angularVelocity = Vector3.zero;
					rb.isKinematic = true;
				}
				rb.detectCollisions = false;
			}

			if (currentMotor.playerCollider != null) currentMotor.playerCollider.enabled = false;
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
			}

			if (currentMotor.playerCollider != null) currentMotor.playerCollider.enabled = true;
		}
	}

	private void Update()
	{
		if (walkForwardsAction == null) CacheInputActions();

		if (PlayerMaster.Instance != null && PlayerMaster.Instance.IsPaused)
		{
			ignoreNextDelta = true;
			return;
		}

		// Re-lock mouse cursor on click if gameplay is active
		if (IsControllingAliveEntity() && Cursor.lockState != CursorLockMode.Locked)
		{
			if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
				ignoreNextDelta = true;
			}
		}

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
		if (currentMotor == null) return;

		// Lock out controls completely when dead so DeathCameraTracker has exclusive camera/mouse authority
		if (currentMotor.isDead || currentMotor.currentHealth <= 0f)
		{
			currentMotor.SetMoveInput(Vector2.zero);
			currentMotor.SetSprintInput(false);
			currentMotor.SetJumpHeld(false);
			currentMotor.SetAimInput(false);
			return;
		}

		// 1. Mouse & Look Rotation
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

		// 2. Movement Inputs
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

		// 3. Stances
		if (jumpAction != null && jumpAction.triggered) currentMotor.Jump();
		if (crouchAction != null && crouchAction.triggered) currentMotor.ToggleCrouch();
		if (proneAction != null && proneAction.triggered) currentMotor.ToggleProne();

		// 4. Interaction
		if (interactAction != null && interactAction.triggered)
		{
			PerformInteract();
		}

		// 5. Aiming
		bool isAiming = false;
		if (!currentMotor.isSprinting)
		{
			if (aimAction != null && aimAction.IsPressed())
				isAiming = true;
			else if (Mouse.current != null && Mouse.current.rightButton.isPressed)
				isAiming = true;
		}
		currentMotor.SetAimInput(isAiming);

		// 6. Weapons
		if (currentMotor.currentWeapon != null)
		{
			bool canFire = currentMotor.VisualController == null || currentMotor.VisualController.CanShoot;
			bool fireHeld = canFire && (fireAction != null && fireAction.IsPressed());
			bool fireTrig = canFire && (fireAction != null && fireAction.triggered);

			if (fireAction != null && fireAction.triggered && currentMotor.isSprinting)
			{
				currentMotor.SetSprintInput(false);
			}

			currentMotor.currentWeapon.ProcessInput(fireHeld, fireTrig);

			if (reloadAction != null && reloadAction.triggered && canFire)
			{
				currentMotor.currentWeapon.Reload();
			}

			if (dropAction != null && dropAction.triggered)
			{
				currentMotor.DropWeapon();
				_nextInteractAllowedTime = Time.time + pickupCooldown;
			}
		}
	}

	private void PerformInteract()
	{
		if (Time.time < _nextInteractAllowedTime) return;
		if (currentMotor == null || currentMotor.playerCamera == null) return;

		Transform cam = currentMotor.playerCamera.transform;
		Ray ray = new Ray(cam.position, cam.forward);

		if (Physics.SphereCast(ray, interactRadius, out RaycastHit hit, interactRange, interactLayerMask, QueryTriggerInteraction.Collide))
		{
			if (hit.transform == currentMotor.transform || hit.transform.IsChildOf(currentMotor.transform))
				return;

			// Prioritize explicit interaction components (doors, weapons, switches)
			IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
			if (interactable != null)
			{
				_nextInteractAllowedTime = Time.time + pickupCooldown;
				interactable.Interact(currentMotor.gameObject);
				return;
			}

			// Fallback for raw vehicle hull collision without a specific door trigger
			Vehicle vehicle = hit.collider.GetComponentInParent<Vehicle>();
			if (vehicle != null)
			{
				_nextInteractAllowedTime = Time.time + pickupCooldown;
				vehicle.BoardAnyAvailableSeat(currentMotor);
			}
		}
	}

	private void HandleVehicleProcessing()
	{
		if (activeVehicle == null) return;

		float scroll = vehicleCameraZoomAction != null ? vehicleCameraZoomAction.ReadValue<float>() : 0f;
		if (scroll == 0f && Mouse.current != null)
		{
			scroll = Mouse.current.scroll.ReadValue().y;
		}

		bool freeLook = vehicleFreeLookAction != null && vehicleFreeLookAction.IsPressed();

		Vector2 mouseDelta = vehicleLookDeltaAction != null ? vehicleLookDeltaAction.ReadValue<Vector2>() : Vector2.zero;
		if (mouseDelta == Vector2.zero && Mouse.current != null)
		{
			mouseDelta = Mouse.current.delta.ReadValue();
		}

		Vector2 joystickLook = vehicleLookAxisAction != null ? vehicleLookAxisAction.ReadValue<Vector2>() : Vector2.zero;
		if (joystickLook == Vector2.zero && lookAxisAction != null)
		{
			joystickLook = lookAxisAction.ReadValue<Vector2>();
		}

		if (ignoreNextDelta)
		{
			mouseDelta = Vector2.zero;
			ignoreNextDelta = false;
		}

		activeVehicle.SetCameraInputs(currentMotor, scroll, freeLook, mouseDelta, joystickLook);

		if (vehicleToggleCameraAction != null && vehicleToggleCameraAction.triggered)
		{
			activeVehicle.ToggleCamera(currentMotor);
		}

		if (interactAction != null && interactAction.triggered)
		{
			activeVehicle.ExitOccupant(currentMotor);
			return;
		}

		int seatIndex = activeVehicle.GetSeatIndex(currentMotor);

		// Seat 0 drives; passenger seats process weapons
		if (seatIndex == 0)
		{
			if (activeVehicle is GroundVehicle groundVehicle) ProcessGroundVehicleInput(groundVehicle);
			else if (activeVehicle is Aircraft aircraft) ProcessAircraftInput(aircraft);

			if (activeVehicle.CanSeatUseWeapons(0))
			{
				ProcessPassengerWeapons(0);
			}
		}
		else
		{
			ProcessPassengerWeapons(seatIndex);
		}
	}

	private void ProcessPassengerWeapons(int seatIndex)
	{
		if (currentMotor == null || currentMotor.isDead || currentMotor.currentHealth <= 0f) return;
		if (activeVehicle == null || !activeVehicle.CanSeatUseWeapons(seatIndex)) return;

		// 1. Aim Down Sights (Right-Click / Aim Action)
		bool isAiming = false;
		if (aimAction != null && aimAction.IsPressed())
			isAiming = true;
		else if (Mouse.current != null && Mouse.current.rightButton.isPressed)
			isAiming = true;

		currentMotor.SetAimInput(isAiming);

		// 2. Weapon Trigger & Reload Processing
		if (currentMotor.currentWeapon != null)
		{
			bool canFire = currentMotor.VisualController == null || currentMotor.VisualController.CanShoot;
			bool fireHeld = canFire && (fireAction != null && fireAction.IsPressed());
			bool fireTrig = canFire && (fireAction != null && fireAction.triggered);

			currentMotor.currentWeapon.ProcessInput(fireHeld, fireTrig);

			if (reloadAction != null && reloadAction.triggered && canFire)
			{
				currentMotor.currentWeapon.Reload();
			}
			// Dropping weapons inside the vehicle is intentionally locked out to prevent items clipping through chassis geometry
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
			if (axis.sqrMagnitude > 0.01f) { steer = axis.x; drive = axis.y; }
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

		if (aircraftPitchAxisAction != null) { float val = aircraftPitchAxisAction.ReadValue<float>(); if (Mathf.Abs(val) > 0.05f) pitch = val; }
		if (aircraftRollAxisAction != null) { float val = aircraftRollAxisAction.ReadValue<float>(); if (Mathf.Abs(val) > 0.05f) roll = val; }
		if (aircraftYawAxisAction != null) { float val = aircraftYawAxisAction.ReadValue<float>(); if (Mathf.Abs(val) > 0.05f) yaw = val; }
		if (aircraftThrottleAxisAction != null) { float val = aircraftThrottleAxisAction.ReadValue<float>(); if (Mathf.Abs(val) > 0.05f) lift = val; }

		ac.SetFlightInputs(lift, pitch, roll, yaw, 0f, 0f);

		if (aircraftToggleModeAction != null && aircraftToggleModeAction.triggered) ac.ToggleFlightMode();
	}

	private void ResetVehicleInputs()
	{
		if (activeVehicle is GroundVehicle gv) gv.SetMotorInputs(0f, 0f, 0f, false);
		if (activeVehicle is Aircraft ac) ac.SetFlightInputs(0f, 0f, 0f, 0f, 0f, 0f);
		activeVehicle.SetCameraInputs(currentMotor, 0f, false, Vector2.zero, Vector2.zero);
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
		aimAction = map.FindAction("Aim") ?? map.FindAction("Scope") ?? map.FindAction("Fire2");
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