using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public class KeybindEntry : MonoBehaviour
{
	[Header("UI Elements")]
	public TextMeshProUGUI actionNameText;
	public Button primaryButton;
	public TextMeshProUGUI primaryButtonText;
	public Button secondaryButton;
	public TextMeshProUGUI secondaryButtonText;

	private InputAction targetAction;
	private KeybindFramework framework;
	private System.Func<int> getSelectedDeviceId;

	public void Initialize(string displayName, InputAction action, KeybindFramework fw, System.Func<int> deviceIdProvider)
	{
		actionNameText.text = displayName;
		targetAction = action;
		framework = fw;
		getSelectedDeviceId = deviceIdProvider;

		UpdateDisplay();

		primaryButton.onClick.AddListener(() => StartRebind(0, primaryButtonText));
		secondaryButton.onClick.AddListener(() => StartRebind(1, secondaryButtonText));

		SetupRightClick(primaryButton, 0);
		SetupRightClick(secondaryButton, 1);
	}

	private void StartRebind(int slotIndex, TextMeshProUGUI statusText)
	{
		statusText.text = "...";
		int deviceId = getSelectedDeviceId != null ? getSelectedDeviceId() : -1;
		framework.PerformInteractiveRebind(targetAction, deviceId, slotIndex, UpdateDisplay);
	}

	private void ClearBinding(int slotIndex)
	{
		int deviceId = getSelectedDeviceId != null ? getSelectedDeviceId() : -1;
		framework.RemoveBinding(targetAction, deviceId, slotIndex);
		UpdateDisplay();
	}

	public void UpdateDisplay()
	{
		int deviceId = getSelectedDeviceId != null ? getSelectedDeviceId() : -1;
		List<int> deviceBinds = framework != null
			? framework.GetBindingIndicesForDevice(targetAction, deviceId)
			: new List<int>();

		// Display the specific bindings assigned to the selected device
		primaryButtonText.text = deviceBinds.Count > 0 ? targetAction.GetBindingDisplayString(deviceBinds[0]) : "---";
		secondaryButtonText.text = deviceBinds.Count > 1 ? targetAction.GetBindingDisplayString(deviceBinds[1]) : "---";
	}

	private void SetupRightClick(Button btn, int slotIndex)
	{
		EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
		EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
		entry.callback.AddListener((data) =>
		{
			if (((PointerEventData)data).button == PointerEventData.InputButton.Right) ClearBinding(slotIndex);
		});
		trigger.triggers.Add(entry);
	}
}