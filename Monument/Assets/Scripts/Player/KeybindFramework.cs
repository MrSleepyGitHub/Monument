using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeybindFramework : MonoBehaviour
{
	[Header("Input Actions")]
	public InputActionMap movementMap;
	public InputAction walkAxis;
	public InputAction lookAxis;
	public InputAction walkForwards, walkBackwards, strafeLeft, strafeRight;
	public InputAction sprint, jump, crouch, prone;
	public InputAction interact;
	public InputAction fire, drop, reload;

	[Header("Vehicle Actions")]
	public InputAction vehicleDriveAxis; // For analog forward/back and steering
	public InputAction vehicleForward, vehicleBackward, vehicleSteerLeft, vehicleSteerRight;
	public InputAction vehicleBrake, vehicleBoost;

	[Header("Vehicle Camera Actions")]
	public InputAction vehicleToggleCamera;
	public InputAction vehicleFreeLook;
	public InputAction vehicleCameraZoom;
	public InputAction vehicleLookDelta; // Mouse (Displacement)
	public InputAction vehicleLookAxis;  // HOTAS / Controller (Rate)

	[Header("Aircraft Actions")]
	public InputAction aircraftLiftUp;
	public InputAction aircraftLiftDown;
	public InputAction aircraftPitchDown;
	public InputAction aircraftPitchUp;
	public InputAction aircraftRollLeft;
	public InputAction aircraftRollRight;
	public InputAction aircraftYawLeft;
	public InputAction aircraftYawRight;

	public InputAction aircraftStrafeForward;
	public InputAction aircraftStrafeBackward;
	public InputAction aircraftStrafeLeft;
	public InputAction aircraftStrafeRight;

	public InputAction aircraftToggleMode;
	public InputAction aircraftDetach;
	public InputAction cargoToggle;

	// A wrapper class to hold Unity's hardware data alongside player-assigned names
	public class CustomHID
	{
		public int deviceId;
		public string hardwarePath;
		public string defaultName;
		public string customName;
		public InputDevice device;
	}

	// Dictionary tracking all currently connected hardware by their unique ID
	public Dictionary<int, CustomHID> connectedDevices { get; private set; } = new Dictionary<int, CustomHID>();

	private void Awake()
	{
		InitializeActions();
		RegisterExistingDevices();

		// Listen for hardware being plugged in or unplugged during gameplay
		InputSystem.onDeviceChange += HandleDeviceChange;
	}

	private void OnDestroy()
	{
		InputSystem.onDeviceChange -= HandleDeviceChange;
	}

	// ==========================================
	// 1. ACTION SETUP
	// ==========================================
	private void InitializeActions()
	{
		movementMap = new InputActionMap("PlayerMovement");

		// 2D Axis for Joysticks / Analog inputs (Optional)
		walkAxis = movementMap.AddAction("WalkAxis", type: InputActionType.Value);
		lookAxis = movementMap.AddAction("LookAxis", type: InputActionType.Value);

		// Discrete Buttons for Keyboards / HOTAS buttons
		walkForwards = movementMap.AddAction("WalkForwards", type: InputActionType.Button);
		walkForwards.AddBinding("<Keyboard>/w");
		walkForwards.AddBinding("<Keyboard>/upArrow");

		walkBackwards = movementMap.AddAction("WalkBackwards", type: InputActionType.Button);
		walkBackwards.AddBinding("<Keyboard>/s");
		walkBackwards.AddBinding("<Keyboard>/downArrow");

		strafeLeft = movementMap.AddAction("StrafeLeft", type: InputActionType.Button);
		strafeLeft.AddBinding("<Keyboard>/a");
		strafeLeft.AddBinding("<Keyboard>/leftArrow");

		strafeRight = movementMap.AddAction("StrafeRight", type: InputActionType.Button);
		strafeRight.AddBinding("<Keyboard>/d");
		strafeRight.AddBinding("<Keyboard>/rightArrow");

		sprint = movementMap.AddAction("Sprint", type: InputActionType.Button);
		sprint.AddBinding("<Keyboard>/leftShift");

		jump = movementMap.AddAction("Jump", type: InputActionType.Button);
		jump.AddBinding("<Keyboard>/space");

		crouch = movementMap.AddAction("Crouch", type: InputActionType.Button);
		crouch.AddBinding("<Keyboard>/c");

		prone = movementMap.AddAction("Prone", type: InputActionType.Button);
		prone.AddBinding("<Keyboard>/z");

		interact = movementMap.AddAction("Interact", type: InputActionType.Button);
		interact.AddBinding("<Keyboard>/f");

		fire = movementMap.AddAction("Fire", type: InputActionType.Button);
		fire.AddBinding("<Mouse>/leftButton");

		drop = movementMap.AddAction("Drop", type: InputActionType.Button);
		drop.AddBinding("<Keyboard>/q");

		reload = movementMap.AddAction("Reload", type: InputActionType.Button);
		reload.AddBinding("<Keyboard>/r");

		// --- NEW: Vehicle Bindings ---
		vehicleDriveAxis = movementMap.AddAction("VehicleDriveAxis", type: InputActionType.Value);

		vehicleForward = movementMap.AddAction("VehicleForward", type: InputActionType.Button);
		vehicleForward.AddBinding("<Keyboard>/w");
		vehicleForward.AddBinding("<Keyboard>/upArrow");

		vehicleBackward = movementMap.AddAction("VehicleBackward", type: InputActionType.Button);
		vehicleBackward.AddBinding("<Keyboard>/s");
		vehicleBackward.AddBinding("<Keyboard>/downArrow");

		vehicleSteerLeft = movementMap.AddAction("VehicleSteerLeft", type: InputActionType.Button);
		vehicleSteerLeft.AddBinding("<Keyboard>/a");
		vehicleSteerLeft.AddBinding("<Keyboard>/leftArrow");

		vehicleSteerRight = movementMap.AddAction("VehicleSteerRight", type: InputActionType.Button);
		vehicleSteerRight.AddBinding("<Keyboard>/d");
		vehicleSteerRight.AddBinding("<Keyboard>/rightArrow");

		vehicleBrake = movementMap.AddAction("VehicleBrake", type: InputActionType.Button);
		vehicleBrake.AddBinding("<Keyboard>/space");

		vehicleBoost = movementMap.AddAction("VehicleBoost", type: InputActionType.Button);
		vehicleBoost.AddBinding("<Keyboard>/leftShift");
		vehicleBoost.AddBinding("<Keyboard>/rightShift");

		// --- NEW: Vehicle Camera Bindings ---
		vehicleToggleCamera = movementMap.AddAction("VehicleToggleCamera", type: InputActionType.Button);
		vehicleToggleCamera.AddBinding("<Keyboard>/v");

		vehicleFreeLook = movementMap.AddAction("VehicleFreeLook", type: InputActionType.Button);
		vehicleFreeLook.AddBinding("<Mouse>/rightButton");

		vehicleCameraZoom = movementMap.AddAction("VehicleCameraZoom", type: InputActionType.Value);
		vehicleCameraZoom.AddBinding("<Mouse>/scroll/y");

		vehicleLookDelta = movementMap.AddAction("VehicleLookDelta", type: InputActionType.Value);
		vehicleLookDelta.AddBinding("<Mouse>/delta");

		vehicleLookAxis = movementMap.AddAction("VehicleLookAxis", type: InputActionType.Value);

		// --- NEW: Aircraft Bindings ---
		aircraftLiftUp = movementMap.AddAction("AircraftLiftUp", type: InputActionType.Button);
		aircraftLiftUp.AddBinding("<Keyboard>/space");

		aircraftLiftDown = movementMap.AddAction("AircraftLiftDown", type: InputActionType.Button);
		aircraftLiftDown.AddBinding("<Keyboard>/leftCtrl");

		aircraftPitchDown = movementMap.AddAction("AircraftPitchDown", type: InputActionType.Button);
		aircraftPitchDown.AddBinding("<Keyboard>/w");

		aircraftPitchUp = movementMap.AddAction("AircraftPitchUp", type: InputActionType.Button);
		aircraftPitchUp.AddBinding("<Keyboard>/s");

		aircraftRollLeft = movementMap.AddAction("AircraftRollLeft", type: InputActionType.Button);
		aircraftRollLeft.AddBinding("<Keyboard>/a");

		aircraftRollRight = movementMap.AddAction("AircraftRollRight", type: InputActionType.Button);
		aircraftRollRight.AddBinding("<Keyboard>/d");

		aircraftYawLeft = movementMap.AddAction("AircraftYawLeft", type: InputActionType.Button);
		aircraftYawLeft.AddBinding("<Keyboard>/q");

		aircraftYawRight = movementMap.AddAction("AircraftYawRight", type: InputActionType.Button);
		aircraftYawRight.AddBinding("<Keyboard>/e");

		// 6DOF Strafing
		aircraftStrafeForward = movementMap.AddAction("AircraftStrafeForward", type: InputActionType.Button);
		aircraftStrafeForward.AddBinding("<Keyboard>/upArrow");

		aircraftStrafeBackward = movementMap.AddAction("AircraftStrafeBackward", type: InputActionType.Button);
		aircraftStrafeBackward.AddBinding("<Keyboard>/downArrow");

		aircraftStrafeLeft = movementMap.AddAction("AircraftStrafeLeft", type: InputActionType.Button);
		aircraftStrafeLeft.AddBinding("<Keyboard>/leftArrow");

		aircraftStrafeRight = movementMap.AddAction("AircraftStrafeRight", type: InputActionType.Button);
		aircraftStrafeRight.AddBinding("<Keyboard>/rightArrow");

		// Utilities
		aircraftToggleMode = movementMap.AddAction("AircraftToggleMode", type: InputActionType.Button);
		aircraftToggleMode.AddBinding("<Keyboard>/leftShift");

		aircraftDetach = movementMap.AddAction("AircraftDetach", type: InputActionType.Button);
		aircraftDetach.AddBinding("<Keyboard>/x");

		cargoToggle = movementMap.AddAction("CargoToggle", type: InputActionType.Button);
		cargoToggle.AddBinding("<Keyboard>/c");

		movementMap.Enable();
	}

	// ==========================================
	// 2. DEVICE MANAGEMENT & RENAMING
	// ==========================================
	private void RegisterExistingDevices()
	{
		foreach (var device in InputSystem.devices) AddDevice(device);
	}

	private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
	{
		if (change == InputDeviceChange.Added) AddDevice(device);
		else if (change == InputDeviceChange.Removed) RemoveDevice(device);
	}

	private void AddDevice(InputDevice device)
	{
		if (!connectedDevices.ContainsKey(device.deviceId))
		{
			connectedDevices[device.deviceId] = new CustomHID
			{
				deviceId = device.deviceId,
				hardwarePath = device.path,      // e.g., /Mouse{id} or /Joystick{id}
				defaultName = device.name,
				customName = device.name,        // Defaults to hardware name
				device = device
			};
		}
	}

	private void RemoveDevice(InputDevice device)
	{
		if (connectedDevices.ContainsKey(device.deviceId))
		{
			connectedDevices.Remove(device.deviceId);
		}
	}

	// Call this from a UI Input Field to permanently rename a device
	public void RenameDevice(int deviceId, string newName)
	{
		if (connectedDevices.ContainsKey(deviceId))
		{
			connectedDevices[deviceId].customName = newName;
		}
	}

	// Call this to populate your UI Dropdown next to the input fields
	public List<string> GetDropdownOptions()
	{
		List<string> options = new List<string>();
		foreach (var hid in connectedDevices.Values)
		{
			// E.g., "Logitech Extreme 3D (Joystick 1)"
			options.Add($"{hid.customName} ({hid.deviceId})");
		}
		return options;
	}

	// ==========================================
	// 3. MULTI-BINDING LOGIC
	// ==========================================
	public void RemoveBinding(InputAction actionToUnbind, int targetDeviceId)
	{
		if (!connectedDevices.ContainsKey(targetDeviceId)) return;
		CustomHID targetDevice = connectedDevices[targetDeviceId];

		actionToUnbind.Disable();

		// 1. Find the binding index that matches the layout of the target device
		int existingBindingIndex = -1;
		for (int i = 0; i < actionToUnbind.bindings.Count; i++)
		{
			string bindingPath = actionToUnbind.bindings[i].effectivePath ?? actionToUnbind.bindings[i].path;

			if (!string.IsNullOrEmpty(bindingPath) && bindingPath.Contains(targetDevice.device.layout))
			{
				existingBindingIndex = i;
				break;
			}
		}

		// 2. If a binding exists for this specific device layout, erase it completely
		if (existingBindingIndex >= 0)
		{
			actionToUnbind.ChangeBinding(existingBindingIndex).Erase();
		}

		actionToUnbind.Enable();
	}

	// Finds all binding indices on an action that correspond to the targeted device
	public List<int> GetBindingIndicesForDevice(InputAction action, int targetDeviceId)
	{
		List<int> indices = new List<int>();
		if (!connectedDevices.ContainsKey(targetDeviceId)) return indices;

		CustomHID targetDevice = connectedDevices[targetDeviceId];
		for (int i = 0; i < action.bindings.Count; i++)
		{
			var binding = action.bindings[i];
			if (binding.isComposite) continue;

			string path = binding.effectivePath ?? binding.path;
			if (string.IsNullOrEmpty(path)) continue;

			// Matches by explicit hardware path or device layout (e.g., "Keyboard", "Joystick")
			if ((!string.IsNullOrEmpty(targetDevice.hardwarePath) && path.StartsWith(targetDevice.hardwarePath)) ||
				(!string.IsNullOrEmpty(targetDevice.device.layout) && path.Contains(targetDevice.device.layout)))
			{
				indices.Add(i);
			}
		}
		return indices;
	}
	// Unity's Input System inherently supports multiple bindings per action.
	// This method listens for an input and forces it to ONLY save if it comes from the UI-selected device.
	// Added System.Action callback
	public void PerformInteractiveRebind(InputAction actionToBind, int targetDeviceId, int slotIndex, System.Action onRebindComplete)
	{
		if (!connectedDevices.ContainsKey(targetDeviceId)) return;
		CustomHID targetDevice = connectedDevices[targetDeviceId];

		actionToBind.Disable();

		List<int> matchingIndices = GetBindingIndicesForDevice(actionToBind, targetDeviceId);
		int targetBindingIndex = -1;
		bool isNewBinding = false;

		// Overwrite existing slot if present, otherwise append a blank binding slot
		if (slotIndex < matchingIndices.Count)
		{
			targetBindingIndex = matchingIndices[slotIndex];
		}
		else
		{
			actionToBind.AddBinding("");
			targetBindingIndex = actionToBind.bindings.Count - 1;
			isNewBinding = true;
		}

		var rebindOperation = actionToBind.PerformInteractiveRebinding()
			.WithTargetBinding(targetBindingIndex)
			.WithControlsHavingToMatchPath(targetDevice.hardwarePath)
			.WithCancelingThrough("<Keyboard>/escape");

		rebindOperation.OnComplete(operation =>
		{
			actionToBind.Enable();
			operation.Dispose();
			onRebindComplete?.Invoke();
		});

		rebindOperation.OnCancel(operation =>
		{
			if (isNewBinding)
			{
				actionToBind.ChangeBinding(targetBindingIndex).Erase();
			}

			actionToBind.Enable();
			operation.Dispose();
			onRebindComplete?.Invoke();
		});

		rebindOperation.Start();
	}

	public void RemoveBinding(InputAction actionToUnbind, int targetDeviceId, int slotIndex)
	{
		if (!connectedDevices.ContainsKey(targetDeviceId)) return;

		List<int> matchingIndices = GetBindingIndicesForDevice(actionToUnbind, targetDeviceId);
		if (slotIndex < matchingIndices.Count)
		{
			actionToUnbind.Disable();
			actionToUnbind.ChangeBinding(matchingIndices[slotIndex]).Erase();
			actionToUnbind.Enable();
		}
	}
}