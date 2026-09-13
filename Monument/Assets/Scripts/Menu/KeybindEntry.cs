using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

public class KeybindEntry : MonoBehaviour
{
	[Header("UI Elements")]
	public TextMeshProUGUI actionNameText;
	public Button bindButton;
	public TextMeshProUGUI bindButtonText;
	public Dropdown deviceDropdown;

	private InputAction targetAction;
	private KeybindFramework framework;
	private List<int> deviceIdMap = new List<int>();

	// Called by the Manager to set up this specific entry
	public void Initialize(string displayName, InputAction action, KeybindFramework fw)
	{
		actionNameText.text = displayName;
		targetAction = action;
		framework = fw;

		RefreshDropdown();
		UpdateBindText();

		// 1. Setup Left Click (Standard Unity OnClick)
		bindButton.onClick.AddListener(StartRebind);

		// 2. Dynamically Setup Right Click (Event Trigger)
		SetupRightClick();
	}

	private void RefreshDropdown()
	{
		deviceDropdown.ClearOptions();
		deviceIdMap.Clear();

		List<string> options = new List<string>();
		foreach (var kvp in framework.connectedDevices)
		{
			options.Add(kvp.Value.customName);
			deviceIdMap.Add(kvp.Key);
		}
		deviceDropdown.AddOptions(options);
	}

	private void StartRebind()
	{
		bindButtonText.text = "Waiting for input...";

		int selectedDeviceId = deviceIdMap[deviceDropdown.value];
		framework.PerformInteractiveRebind(targetAction, selectedDeviceId, UpdateBindText);
	}

	private void ClearBinding()
	{
		int selectedDeviceId = deviceIdMap[deviceDropdown.value];
		framework.RemoveBinding(targetAction, selectedDeviceId);
		UpdateBindText();
	}

	private void UpdateBindText()
	{
		bindButtonText.text = targetAction.GetBindingDisplayString();
	}

	private void SetupRightClick()
	{
		// Automatically adds an EventTrigger to the button if it doesn't have one
		EventTrigger trigger = bindButton.gameObject.GetComponent<EventTrigger>();
		if (trigger == null) trigger = bindButton.gameObject.AddComponent<EventTrigger>();

		EventTrigger.Entry rightClickEntry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerClick
		};

		rightClickEntry.callback.AddListener((data) =>
		{
			PointerEventData pointerData = data as PointerEventData;
			if (pointerData != null && pointerData.button == PointerEventData.InputButton.Right)
			{
				ClearBinding();
			}
		});

		trigger.triggers.Add(rightClickEntry);
	}
}