///////////////////////////
// KEYBIND FRAMEWORK
///////////////////////////
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeybindFramework : MonoBehaviour
{
	[Serializable]
	public class ActionBindItem
	{
		public string actionName;
		public string displayName;
		public BindGroup group;
		public BindInputType inputType;
		public InputAction action;

		public ActionBindItem(string name, string display, BindGroup grp, BindInputType type, InputAction act)
		{
			actionName = name;
			displayName = display;
			group = grp;
			inputType = type;
			action = act;
		}
	}

	///////////////////////////
	// PERSISTENT DEVICE DATA
	///////////////////////////
	[Serializable]
	public class DeviceRecord
	{
		public string deviceIdentifier;
		public string displayName;
		public string originalName;
		public bool isConnected;
		public bool isEnabled;
		public int runtimeDeviceId;
	}

	[Header("Input Map")]
	public InputActionMap movementMap;
	public List<ActionBindItem> allBinds = new List<ActionBindItem>();

	[Header("Hardware Persistence")]
	public List<DeviceRecord> registeredDevices = new List<DeviceRecord>();

	private Dictionary<string, string> bindingDeviceMap = new Dictionary<string, string>();

	private const string BINDINGS_KEY = "PlayerCustomBindings";
	private const string DEVICES_KEY = "PlayerKnownDevices";
	private const string BINDING_DEVICE_MAP_KEY = "PlayerBindingDeviceMap";

	private void Awake()
	{
		LoadKnownDevices();
		LoadBindingDeviceMap();
		InitializeActions();
		RegisterExistingDevices();
		LoadBindingsFromDisk();

		InputSystem.onDeviceChange += HandleDeviceChange;
	}

	private void OnDestroy()
	{
		InputSystem.onDeviceChange -= HandleDeviceChange;
	}

	///////////////////////////
	// DETERMINISTIC GUID GENERATION
	///////////////////////////
	public static Guid GetDeterministicGuid(string key)
	{
		using (MD5 md5 = MD5.Create())
		{
			byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
			return new Guid(hash);
		}
	}

	///////////////////////////
	// ACTION INITIALIZATION
	///////////////////////////
	private void InitializeActions()
	{
		movementMap = new InputActionMap("PlayerMovement");
		allBinds.Clear();

		// --- INFANTRY ACTIONS ---
		RegisterAction("WalkAxis", "Walk Axis (Stick)", BindGroup.Infantry, BindInputType.Axis, InputActionType.Value, "<Gamepad>/leftStick");
		RegisterAction("LookAxis", "Look Axis (Stick)", BindGroup.Infantry, BindInputType.Axis, InputActionType.Value, "<Gamepad>/rightStick");
		RegisterAction("WalkForwards", "Walk Forward", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/w", "<Keyboard>/upArrow");
		RegisterAction("WalkBackwards", "Walk Backward", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/s", "<Keyboard>/downArrow");
		RegisterAction("StrafeLeft", "Strafe Left", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/a", "<Keyboard>/leftArrow");
		RegisterAction("StrafeRight", "Strafe Right", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/d", "<Keyboard>/rightArrow");
		RegisterAction("Sprint", "Sprint", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/leftShift");
		RegisterAction("Jump", "Jump", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/space");
		RegisterAction("Crouch", "Crouch", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/c");
		RegisterAction("Prone", "Prone", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/z");
		RegisterAction("Interact", "Interact", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/e", "<Keyboard>/f");
		RegisterAction("Fire", "Fire", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Mouse>/leftButton");
		RegisterAction("Aim", "Aim / Scope", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Mouse>/rightButton");
		RegisterAction("Reload", "Reload", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/r");
		RegisterAction("Drop", "Drop", BindGroup.Infantry, BindInputType.Button, InputActionType.Button, "<Keyboard>/q", "<Keyboard>/g");

		// --- GROUND VEHICLE ACTIONS ---
		RegisterAction("VehicleDriveAxis", "Steer / Throttle Axis", BindGroup.GroundVehicles, BindInputType.Axis, InputActionType.Value, "<Gamepad>/leftStick");
		RegisterAction("VehicleForward", "Accelerate", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/w", "<Keyboard>/upArrow");
		RegisterAction("VehicleBackward", "Reverse", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/s", "<Keyboard>/downArrow");
		RegisterAction("VehicleSteerLeft", "Steer Left", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/a", "<Keyboard>/leftArrow");
		RegisterAction("VehicleSteerRight", "Steer Right", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/d", "<Keyboard>/rightArrow");
		RegisterAction("VehicleBrake", "Handbrake", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/space");
		RegisterAction("VehicleBoost", "Boost", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/leftShift");
		RegisterAction("VehicleToggleCamera", "Toggle Camera", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Keyboard>/v");
		RegisterAction("VehicleFreeLook", "Free Look", BindGroup.GroundVehicles, BindInputType.Button, InputActionType.Button, "<Mouse>/rightButton");

		// --- AIRCRAFT DIGITAL BUTTONS ---
		RegisterAction("AircraftLiftUp", "Lift Up", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/space");
		RegisterAction("AircraftLiftDown", "Lift Down", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/leftCtrl");
		RegisterAction("AircraftPitchDown", "Pitch Down (Key)", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/w");
		RegisterAction("AircraftPitchUp", "Pitch Up (Key)", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/s");
		RegisterAction("AircraftRollLeft", "Roll Left (Key)", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/a");
		RegisterAction("AircraftRollRight", "Roll Right (Key)", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/d");
		RegisterAction("AircraftYawLeft", "Yaw Left (Key)", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/q");
		RegisterAction("AircraftYawRight", "Yaw Right (Key)", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/e");
		RegisterAction("AircraftToggleMode", "Flight Mode", BindGroup.Aircraft, BindInputType.Button, InputActionType.Button, "<Keyboard>/leftShift");

		// --- AIRCRAFT ANALOG FLIGHT AXES (HOTAS & STICKS) ---
		RegisterAction("AircraftPitchAxis", "Pitch Axis (Stick Y)", BindGroup.Aircraft, BindInputType.Axis, InputActionType.Value, "<Joystick>/stick/y");
		RegisterAction("AircraftRollAxis", "Roll Axis (Stick X)", BindGroup.Aircraft, BindInputType.Axis, InputActionType.Value, "<Joystick>/stick/x");
		RegisterAction("AircraftYawAxis", "Yaw Axis (Twist / Rudder)", BindGroup.Aircraft, BindInputType.Axis, InputActionType.Value, "<Joystick>/twist");
		RegisterAction("AircraftThrottleAxis", "Throttle / Collective (Slider)", BindGroup.Aircraft, BindInputType.Axis, InputActionType.Value, "<Joystick>/slider");
		RegisterAction("AircraftThumbstickAxis", "Look / Slew (Hat / Thumbstick)", BindGroup.Aircraft, BindInputType.Axis, InputActionType.Value, "<Joystick>/hat");

		// --- ROTARY ACTIONS ---
		RegisterAction("RotaryCollectiveUp", "Collective Up", BindGroup.Rotary, BindInputType.Button, InputActionType.Button, "<Keyboard>/space");
		RegisterAction("RotaryCollectiveDown", "Collective Down", BindGroup.Rotary, BindInputType.Button, InputActionType.Button, "<Keyboard>/leftCtrl");
		RegisterAction("RotaryCyclicForward", "Cyclic Forward", BindGroup.Rotary, BindInputType.Button, InputActionType.Button, "<Keyboard>/w");
		RegisterAction("RotaryCyclicBack", "Cyclic Backward", BindGroup.Rotary, BindInputType.Button, InputActionType.Button, "<Keyboard>/s");

		// --- SPACE ACTIONS ---
		RegisterAction("SpaceStrafeForward", "Thrust Forward", BindGroup.Space, BindInputType.Button, InputActionType.Button, "<Keyboard>/w");
		RegisterAction("SpaceStrafeBackward", "Thrust Backward", BindGroup.Space, BindInputType.Button, InputActionType.Button, "<Keyboard>/s");
		RegisterAction("SpaceRollLeft", "Roll Left", BindGroup.Space, BindInputType.Button, InputActionType.Button, "<Keyboard>/q");
		RegisterAction("SpaceRollRight", "Roll Right", BindGroup.Space, BindInputType.Button, InputActionType.Button, "<Keyboard>/e");

		movementMap.Enable();
	}

	private InputAction RegisterAction(string actionName, string displayName, BindGroup group, BindInputType inputType, InputActionType actType, params string[] defaultBindings)
	{
		InputAction action = movementMap.AddAction(actionName, type: actType);

		// Guarantee at least 2 binding slots (Slot 0 = Primary, Slot 1 = Secondary)
		int slotCount = Mathf.Max(2, defaultBindings != null ? defaultBindings.Length : 0);
		for (int i = 0; i < slotCount; i++)
		{
			string path = (defaultBindings != null && i < defaultBindings.Length) ? defaultBindings[i] : string.Empty;
			Guid bindingId = GetDeterministicGuid($"{actionName}_{i}");
			action.AddBinding(new InputBinding
			{
				path = path,
				id = bindingId
			});
		}

		allBinds.Add(new ActionBindItem(actionName, displayName, group, inputType, action));
		return action;
	}

	public List<ActionBindItem> GetBindsByGroup(BindGroup group)
	{
		return allBinds.FindAll(item => item.group == group);
	}

	///////////////////////////
	// DEVICE PERSISTENCE & MANAGEMENT
	///////////////////////////
	private void RegisterExistingDevices()
	{
		EnsureDefaultDeviceRegistered("Keyboard", "Keyboard");
		EnsureDefaultDeviceRegistered("Mouse", "Mouse");

		foreach (var device in InputSystem.devices)
		{
			UpdateDeviceConnection(device, true);
		}

		CleanUpStaleDuplicateDevices();
	}

	private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
	{
		if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
		{
			UpdateDeviceConnection(device, true);
		}
		else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
		{
			UpdateDeviceConnection(device, false);
		}
	}

	/// <summary>
	/// Filters non-gameplay HID interfaces and reconciles devices to prevent duplicate rows.
	/// </summary>
	private void UpdateDeviceConnection(InputDevice device, bool connected)
	{
		// 1. Filter out synthetic controls and secondary HID collections that have 0 controls
		if (device == null || device.synthetic || device.allControls.Count == 0)
			return;

		// 2. Restrict to valid input device types
		if (device is not (Keyboard or Mouse or Gamepad or Joystick))
			return;

		string identifier = GetDeviceIdentifier(device);
		string hardwareName = string.IsNullOrEmpty(device.description.product) ? device.name : device.description.product;

		// 3. Match against current runtime ID or device identifier
		var record = registeredDevices.Find(d => d.runtimeDeviceId == device.deviceId || d.deviceIdentifier == identifier);

		// 4. If connecting and not found, match against an existing disconnected record from a previous session
		if (record == null && connected)
		{
			record = registeredDevices.Find(d => !d.isConnected && d.originalName == hardwareName && d.deviceIdentifier != "Keyboard" && d.deviceIdentifier != "Mouse");
		}

		if (record == null)
		{
			record = new DeviceRecord
			{
				deviceIdentifier = identifier,
				displayName = hardwareName,
				originalName = hardwareName,
				isConnected = connected,
				isEnabled = true,
				runtimeDeviceId = connected ? device.deviceId : -1
			};
			registeredDevices.Add(record);
		}
		else
		{
			record.deviceIdentifier = identifier;
			record.isConnected = connected;
			record.runtimeDeviceId = connected ? device.deviceId : -1;
			if (string.IsNullOrEmpty(record.originalName)) record.originalName = hardwareName;
		}

		SaveKnownDevices();
	}

	/// <summary>
	/// Purges old session ghost duplicates loaded from PlayerPrefs that match active hardware.
	/// </summary>
	private void CleanUpStaleDuplicateDevices()
	{
		var connectedNames = new HashSet<string>();
		foreach (var dev in registeredDevices)
		{
			if (dev.isConnected)
			{
				connectedNames.Add(dev.originalName);
			}
		}

		registeredDevices.RemoveAll(d => !d.isConnected &&
			d.deviceIdentifier != "Keyboard" &&
			d.deviceIdentifier != "Mouse" &&
			connectedNames.Contains(d.originalName) &&
			d.displayName == d.originalName);

		SaveKnownDevices();
	}

	private void EnsureDefaultDeviceRegistered(string identifier, string name)
	{
		if (!registeredDevices.Exists(d => d.deviceIdentifier == identifier))
		{
			registeredDevices.Add(new DeviceRecord
			{
				deviceIdentifier = identifier,
				displayName = name,
				originalName = name,
				isConnected = true,
				isEnabled = true,
				runtimeDeviceId = -1
			});
		}
	}

	public string GetDeviceIdentifier(InputDevice device)
	{
		if (device == null) return "Keyboard";
		if (device is Keyboard) return "Keyboard";
		if (device is Mouse) return "Mouse";

		string baseName = string.IsNullOrEmpty(device.description.product) ? device.name : device.description.product;
		return $"{baseName}_{device.deviceId}";
	}

	public List<DeviceRecord> GetEnabledDevices()
	{
		return registeredDevices.FindAll(d => d.isEnabled);
	}

	public void SetDeviceEnabled(string identifier, bool enabled)
	{
		var record = registeredDevices.Find(d => d.deviceIdentifier == identifier);
		if (record != null)
		{
			record.isEnabled = enabled;
			SaveKnownDevices();
		}
	}

	public void RenameDevice(string identifier, string newCustomName)
	{
		var record = registeredDevices.Find(d => d.deviceIdentifier == identifier);
		if (record != null)
		{
			record.displayName = string.IsNullOrWhiteSpace(newCustomName) ? record.originalName : newCustomName;
			SaveKnownDevices();
		}
	}

	private void SaveKnownDevices()
	{
		List<string> serialized = new List<string>();
		foreach (var dev in registeredDevices)
		{
			serialized.Add($"{dev.deviceIdentifier}|{dev.displayName}|{dev.originalName}|{dev.isEnabled}");
		}
		PlayerPrefs.SetString(DEVICES_KEY, string.Join(";", serialized));
		PlayerPrefs.Save();
	}

	private void LoadKnownDevices()
	{
		registeredDevices.Clear();
		if (!PlayerPrefs.HasKey(DEVICES_KEY)) return;

		string data = PlayerPrefs.GetString(DEVICES_KEY);
		string[] entries = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

		foreach (var entry in entries)
		{
			string[] parts = entry.Split('|');
			if (parts.Length >= 4)
			{
				registeredDevices.Add(new DeviceRecord
				{
					deviceIdentifier = parts[0],
					displayName = parts[1],
					originalName = parts[2],
					isEnabled = bool.TryParse(parts[3], out bool en) && en,
					isConnected = false,
					runtimeDeviceId = -1
				});
			}
		}
	}

	///////////////////////////
	// BINDING DEVICE MAP PERSISTENCE
	///////////////////////////
	public void SetDeviceForBinding(string actionName, int slotIndex, string deviceIdentifier)
	{
		string key = $"{actionName}_{slotIndex}";
		bindingDeviceMap[key] = deviceIdentifier;
		SaveBindingDeviceMap();
	}

	public string GetDeviceIdentifierForBinding(InputAction action, int slotIndex)
	{
		string key = $"{action.name}_{slotIndex}";
		if (bindingDeviceMap.TryGetValue(key, out string devId))
		{
			return devId;
		}

		if (slotIndex < action.bindings.Count)
		{
			string path = action.bindings[slotIndex].effectivePath ?? action.bindings[slotIndex].path;
			if (!string.IsNullOrEmpty(path))
			{
				if (path.StartsWith("<Keyboard>")) return "Keyboard";
				if (path.StartsWith("<Mouse>")) return "Mouse";

				var nonKmDevice = GetEnabledDevices().Find(d => d.deviceIdentifier != "Keyboard" && d.deviceIdentifier != "Mouse");
				if (nonKmDevice != null) return nonKmDevice.deviceIdentifier;
			}
		}

		return "Keyboard";
	}

	private void SaveBindingDeviceMap()
	{
		List<string> entries = new List<string>();
		foreach (var kvp in bindingDeviceMap)
		{
			entries.Add($"{kvp.Key}:{kvp.Value}");
		}
		PlayerPrefs.SetString(BINDING_DEVICE_MAP_KEY, string.Join(";", entries));
		PlayerPrefs.Save();
	}

	private void LoadBindingDeviceMap()
	{
		bindingDeviceMap.Clear();
		if (!PlayerPrefs.HasKey(BINDING_DEVICE_MAP_KEY)) return;

		string data = PlayerPrefs.GetString(BINDING_DEVICE_MAP_KEY);
		string[] entries = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

		foreach (var entry in entries)
		{
			string[] parts = entry.Split(':');
			if (parts.Length == 2)
			{
				bindingDeviceMap[parts[0]] = parts[1];
			}
		}
	}

	///////////////////////////
	// INTERACTIVE REBINDING
	///////////////////////////
	public void PerformInteractiveRebind(InputAction actionToBind, int slotIndex, Action onRebindComplete)
	{
		if (actionToBind == null) return;
		StartCoroutine(ExecuteRebindRoutine(actionToBind, slotIndex, onRebindComplete));
	}

	private IEnumerator ExecuteRebindRoutine(InputAction actionToBind, int slotIndex, Action onRebindComplete)
	{
		// 1. Wait until the mouse click that activated the UI button is released
		yield return new WaitForSecondsRealtime(0.12f);
		while (Mouse.current != null && Mouse.current.leftButton.isPressed)
		{
			yield return null;
		}

		// 2. Ensure slot allocation without throwing map-enabled exceptions
		if (slotIndex >= actionToBind.bindings.Count)
		{
			bool wasMapEnabled = movementMap != null && movementMap.enabled;
			if (wasMapEnabled) movementMap.Disable();

			while (actionToBind.bindings.Count <= slotIndex)
			{
				Guid newId = GetDeterministicGuid($"{actionToBind.name}_{actionToBind.bindings.Count}");
				actionToBind.AddBinding(new InputBinding
				{
					path = string.Empty,
					id = newId
				});
			}

			if (wasMapEnabled) movementMap.Enable();
		}

		actionToBind.Disable();

		var rebind = actionToBind.PerformInteractiveRebinding(slotIndex)
			.WithCancelingThrough("<Keyboard>/escape")
			// Exclude synthetic anyKey control so physical keys are bound directly
			.WithControlsExcluding("<Keyboard>/anyKey")
			.WithControlsExcluding("*/{anyKey}")
			// Exclude mouse pointer noise from stealing the bind
			.WithControlsExcluding("<Pointer>/position")
			.WithControlsExcluding("<Pointer>/delta")
			.WithMagnitudeHavingToBeGreaterThan(0.35f);

		rebind.OnComputeScore((control, eventPtr) =>
		{
			if (control == null || control.device == null) return -1f;

			string identifier = GetDeviceIdentifier(control.device);
			var devRecord = registeredDevices.Find(d => d.deviceIdentifier == identifier || d.runtimeDeviceId == control.device.deviceId);
			if (devRecord != null && !devRecord.isEnabled)
			{
				return -1f;
			}

			return 1f;
		});

		rebind.OnComplete(op =>
		{
			actionToBind.Enable();

			if (op.selectedControl != null)
			{
				string usedIdentifier = GetDeviceIdentifier(op.selectedControl.device);
				SetDeviceForBinding(actionToBind.name, slotIndex, usedIdentifier);
			}

			op.Dispose();
			SaveBindingsToDisk();
			onRebindComplete?.Invoke();
		});

		rebind.OnCancel(op =>
		{
			actionToBind.Enable();
			op.Dispose();
			onRebindComplete?.Invoke();
		});

		rebind.Start();
	}

	public void RemoveBindingAtSlot(InputAction action, int slotIndex)
	{
		if (action == null || slotIndex < 0 || slotIndex >= action.bindings.Count) return;

		action.ApplyBindingOverride(slotIndex, string.Empty);
		SaveBindingsToDisk();
	}

	public void SaveBindingsToDisk()
	{
		if (movementMap == null) return;
		string json = movementMap.SaveBindingOverridesAsJson();
		PlayerPrefs.SetString(BINDINGS_KEY, json);
		PlayerPrefs.Save();
	}

	public void LoadBindingsFromDisk()
	{
		if (movementMap == null) return;
		if (PlayerPrefs.HasKey(BINDINGS_KEY))
		{
			try
			{
				string json = PlayerPrefs.GetString(BINDINGS_KEY);
				if (!string.IsNullOrEmpty(json))
				{
					movementMap.LoadBindingOverridesFromJson(json);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[KeybindFramework] Saved bindings invalid. Resetting. {ex.Message}");
				PlayerPrefs.DeleteKey(BINDINGS_KEY);
			}
		}
	}

	[ContextMenu("Reset Saved Bindings (Factory Default)")]
	public void ResetSavedBindings()
	{
		PlayerPrefs.DeleteKey(BINDINGS_KEY);
		PlayerPrefs.DeleteKey(BINDING_DEVICE_MAP_KEY);
		bindingDeviceMap.Clear();

		if (movementMap != null)
		{
			movementMap.RemoveAllBindingOverrides();
		}

		InitializeActions();
		Debug.Log("[KeybindFramework] Cleared custom overrides and restored clean default bindings.");
	}

	[ContextMenu("Reset Saved Devices Only")]
	public void ResetSavedDevices()
	{
		// 1. Wipe the persistent device list from PlayerPrefs
		PlayerPrefs.DeleteKey(DEVICES_KEY);
		PlayerPrefs.DeleteKey(BINDING_DEVICE_MAP_KEY);
		bindingDeviceMap.Clear();
		registeredDevices.Clear();

		// 2. Re-register only currently connected hardware
		RegisterExistingDevices();

		Debug.Log("[KeybindFramework] Purged all saved devices from PlayerPrefs and rebuilt from currently connected hardware.");
	}

	[ContextMenu("Full Factory Reset (Bindings + Devices)")]
	public void ResetAllKeybindData()
	{
		PlayerPrefs.DeleteKey(BINDINGS_KEY);
		PlayerPrefs.DeleteKey(DEVICES_KEY);
		PlayerPrefs.DeleteKey(BINDING_DEVICE_MAP_KEY);

		bindingDeviceMap.Clear();
		registeredDevices.Clear();

		if (movementMap != null)
		{
			movementMap.RemoveAllBindingOverrides();
		}

		InitializeActions();
		RegisterExistingDevices();

		Debug.Log("[KeybindFramework] Complete factory reset: all custom bindings and known devices wiped.");
	}
}