///////////////////////////
// POLYMORPHIC KEYBIND ENTRY ROW
///////////////////////////
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public class KeybindEntry : MonoBehaviour
{
	[Header("Entry Configuration")]
	public EntryType entryType;

	[Header("Common UI")]
	public TextMeshProUGUI labelText;

	[Header("Keybind & Axis Elements")]
	public Button primaryButton;
	public TextMeshProUGUI primaryButtonText;
	public TMP_Dropdown primaryDeviceDropdown;
	public Button secondaryButton;
	public TextMeshProUGUI secondaryButtonText;
	public TMP_Dropdown secondaryDeviceDropdown;

	[Header("Slider Elements")]
	public Slider valueSlider;
	public TMP_InputField valueInputField;

	[Header("Toggle Elements")]
	public Toggle entryToggle;

	[Header("Dropdown Elements")]
	public TMP_Dropdown entryDropdown;

	[Header("Input Device Management Elements")]
	public TMP_InputField deviceRenameInput;
	public TextMeshProUGUI deviceOriginalNameText;
	public Toggle deviceEnableToggle;

	private InputAction targetAction;
	private KeybindFramework framework;
	private bool isUpdatingUI = false;

	///////////////////////////
	// INPUT DEVICE INITIALIZATION
	///////////////////////////
	public void InitializeInputDevice(KeybindFramework.DeviceRecord record, KeybindFramework fw, Action onStateChanged)
	{
		entryType = EntryType.InputDevice;
		framework = fw;

		if (labelText != null) labelText.text = record.displayName;

		if (deviceRenameInput != null)
		{
			deviceRenameInput.text = record.displayName;
			deviceRenameInput.onEndEdit.RemoveAllListeners();
			deviceRenameInput.onEndEdit.AddListener(newName =>
			{
				framework.RenameDevice(record.deviceIdentifier, newName);
				if (labelText != null) labelText.text = record.displayName;
				UpdateDeviceOriginalNameDisplay(record);
				onStateChanged?.Invoke();
			});
		}

		UpdateDeviceOriginalNameDisplay(record);

		if (deviceEnableToggle != null)
		{
			deviceEnableToggle.isOn = record.isEnabled;
			deviceEnableToggle.onValueChanged.RemoveAllListeners();
			deviceEnableToggle.onValueChanged.AddListener(enabledState =>
			{
				framework.SetDeviceEnabled(record.deviceIdentifier, enabledState);
				onStateChanged?.Invoke();
			});
		}
	}

	private void UpdateDeviceOriginalNameDisplay(KeybindFramework.DeviceRecord record)
	{
		if (deviceOriginalNameText != null)
		{
			bool isRenamed = !string.IsNullOrEmpty(record.originalName) && record.displayName != record.originalName;
			deviceOriginalNameText.text = isRenamed ? $"Original: {record.originalName}" : record.originalName;
		}
	}

	///////////////////////////
	// BIND INITIALIZATION
	///////////////////////////
	public void InitializeBind(KeybindFramework.ActionBindItem item, KeybindFramework fw)
	{
		if (item == null) return;

		entryType = item.inputType == BindInputType.Axis ? EntryType.AxisBind : EntryType.ButtonBind;
		targetAction = item.action;
		framework = fw;

		if (labelText != null) labelText.text = item.displayName;

		RefreshDropdowns();
		UpdateDisplay();

		if (primaryButton != null)
		{
			primaryButton.onClick.RemoveAllListeners();
			primaryButton.onClick.AddListener(() => StartRebind(0, primaryButtonText));
			SetupRightClick(primaryButton, 0);
		}

		if (secondaryButton != null)
		{
			secondaryButton.onClick.RemoveAllListeners();
			secondaryButton.onClick.AddListener(() => StartRebind(1, secondaryButtonText));
			SetupRightClick(secondaryButton, 1);
		}
	}

	public void RefreshDropdowns()
	{
		if (primaryDeviceDropdown != null) PopulateSlotDropdown(primaryDeviceDropdown, 0);
		if (secondaryDeviceDropdown != null) PopulateSlotDropdown(secondaryDeviceDropdown, 1);
	}

	private void PopulateSlotDropdown(TMP_Dropdown dropdown, int slotIndex)
	{
		if (dropdown == null || framework == null || targetAction == null) return;

		isUpdatingUI = true;
		dropdown.ClearOptions();
		List<string> options = new List<string>();
		int selectedIndex = 0;

		string boundDevIdentifier = framework.GetDeviceIdentifierForBinding(targetAction, slotIndex);
		List<KeybindFramework.DeviceRecord> enabledDevices = framework.GetEnabledDevices();

		for (int i = 0; i < enabledDevices.Count; i++)
		{
			var rec = enabledDevices[i];
			string statusSuffix = rec.isConnected ? "" : " (Disconnected)";
			options.Add(rec.displayName + statusSuffix);

			if (rec.deviceIdentifier == boundDevIdentifier)
			{
				selectedIndex = i;
			}
		}

		if (options.Count == 0) options.Add("None Available");

		dropdown.AddOptions(options);
		dropdown.value = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
		dropdown.RefreshShownValue();

		dropdown.onValueChanged.RemoveAllListeners();
		dropdown.onValueChanged.AddListener(newVal =>
		{
			if (isUpdatingUI) return;
			string selectedDevId = GetSelectedDeviceIdentifier(dropdown);
			framework.SetDeviceForBinding(targetAction.name, slotIndex, selectedDevId);
		});

		isUpdatingUI = false;
	}

	private string GetSelectedDeviceIdentifier(TMP_Dropdown dropdown)
	{
		if (dropdown == null || framework == null) return "Keyboard";
		List<KeybindFramework.DeviceRecord> enabledDevices = framework.GetEnabledDevices();

		int idx = dropdown.value;
		if (idx >= 0 && idx < enabledDevices.Count)
		{
			return enabledDevices[idx].deviceIdentifier;
		}

		return "Keyboard";
	}

	///////////////////////////
	// SLIDER TWO-WAY SYNC
	///////////////////////////
	public void InitializeSlider(string label, float min, float max, float currentVal, Action<float> onValueChanged)
	{
		entryType = EntryType.Slider;
		if (labelText != null) labelText.text = label;

		if (valueSlider != null)
		{
			valueSlider.minValue = min;
			valueSlider.maxValue = max;
			valueSlider.value = currentVal;

			valueSlider.onValueChanged.RemoveAllListeners();
			valueSlider.onValueChanged.AddListener(val =>
			{
				if (isUpdatingUI) return;
				isUpdatingUI = true;

				if (valueInputField != null) valueInputField.text = val.ToString("F1");

				onValueChanged?.Invoke(val);
				isUpdatingUI = false;
			});
		}

		if (valueInputField != null)
		{
			valueInputField.text = currentVal.ToString("F1");

			valueInputField.onEndEdit.RemoveAllListeners();
			valueInputField.onEndEdit.AddListener(strVal =>
			{
				if (isUpdatingUI) return;

				if (float.TryParse(strVal, out float parsedVal))
				{
					isUpdatingUI = true;
					float clamped = Mathf.Clamp(parsedVal, min, max);
					if (valueSlider != null) valueSlider.value = clamped;
					valueInputField.text = clamped.ToString("F1");
					onValueChanged?.Invoke(clamped);
					isUpdatingUI = false;
				}
				else if (valueSlider != null)
				{
					valueInputField.text = valueSlider.value.ToString("F1");
				}
			});
		}
	}

	///////////////////////////
	// TOGGLE & DROPDOWN
	///////////////////////////
	public void InitializeToggle(string label, bool currentVal, Action<bool> onValueChanged)
	{
		entryType = EntryType.Toggle;
		if (labelText != null) labelText.text = label;

		if (entryToggle != null)
		{
			entryToggle.isOn = currentVal;
			entryToggle.onValueChanged.RemoveAllListeners();
			entryToggle.onValueChanged.AddListener(val => onValueChanged?.Invoke(val));
		}
	}

	public void InitializeDropdown(string label, List<string> options, int currentVal, Action<int> onValueChanged)
	{
		entryType = EntryType.Dropdown;
		if (labelText != null) labelText.text = label;

		if (entryDropdown != null)
		{
			entryDropdown.ClearOptions();
			entryDropdown.AddOptions(options);
			entryDropdown.value = currentVal;
			entryDropdown.onValueChanged.RemoveAllListeners();
			entryDropdown.onValueChanged.AddListener(val => onValueChanged?.Invoke(val));
		}
	}

	///////////////////////////
	// REBINDING & DISPLAY
	///////////////////////////
	private void StartRebind(int slotIndex, TextMeshProUGUI statusText)
	{
		if (targetAction == null || framework == null) return;

		if (statusText != null) statusText.text = "...";

		framework.PerformInteractiveRebind(targetAction, slotIndex, () =>
		{
			RefreshDropdowns();
			UpdateDisplay();
		});
	}

	private void ClearBinding(int slotIndex)
	{
		if (targetAction == null || framework == null) return;

		framework.RemoveBindingAtSlot(targetAction, slotIndex);
		UpdateDisplay();
	}

	public void UpdateDisplay()
	{
		if (targetAction == null) return;

		if (primaryButtonText != null)
		{
			string str = targetAction.bindings.Count > 0 ? targetAction.GetBindingDisplayString(0) : "---";
			primaryButtonText.text = string.IsNullOrEmpty(str) ? "---" : str;
		}

		if (secondaryButtonText != null)
		{
			string str = targetAction.bindings.Count > 1 ? targetAction.GetBindingDisplayString(1) : "---";
			secondaryButtonText.text = string.IsNullOrEmpty(str) ? "---" : str;
		}
	}

	private void SetupRightClick(Button btn, int slotIndex)
	{
		EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
		trigger.triggers.Clear();
		EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
		entry.callback.AddListener(data =>
		{
			if (((PointerEventData)data).button == PointerEventData.InputButton.Right) ClearBinding(slotIndex);
		});
		trigger.triggers.Add(entry);
	}
}