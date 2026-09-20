///////////////////////////
// CONTROLS MENU MANAGER
///////////////////////////
using System;
using System.Collections.Generic;
using UnityEngine;

public class MenuManagerControls : MonoBehaviour
{
	[Serializable]
	public struct ScrollContainerMapping
	{
		public BindGroup group;
		public Transform contentRoot;
	}

	[Header("Scroll Container Target Mappings")]
	public List<ScrollContainerMapping> tabContainers = new List<ScrollContainerMapping>();

	[Header("Input Devices Tab Containers")]
	public Transform enabledDevicesContent;
	public Transform disabledDevicesContent;

	[Header("Row Prefabs (All using KeybindEntry)")]
	public GameObject buttonBindPrefab;
	public GameObject axisBindPrefab;
	public GameObject sliderPrefab;
	public GameObject togglePrefab;
	public GameObject dropdownPrefab;
	public GameObject inputDevicePrefab;

	private KeybindFramework framework;
	private readonly List<KeybindEntry> activeBindRows = new List<KeybindEntry>();

	private void Start()
	{
		framework = PlayerMaster.Instance != null
			? PlayerMaster.Instance.keybindFramework
			: FindFirstObjectByType<KeybindFramework>();

		if (framework == null)
		{
			Debug.LogError("[MenuManagerControls] KeybindFramework could not be found in the scene.");
			return;
		}

		PopulateAllScrollTabs();
		PopulateDevicesTab();
	}

	///////////////////////////
	// INPUT DEVICES TAB POPULATION
	///////////////////////////
	public void PopulateDevicesTab()
	{
		if (inputDevicePrefab == null) return;

		ClearContainer(enabledDevicesContent);
		ClearContainer(disabledDevicesContent);

		foreach (var record in framework.registeredDevices)
		{
			Transform targetContainer = record.isEnabled ? enabledDevicesContent : disabledDevicesContent;
			if (targetContainer == null) continue;

			GameObject rowObj = Instantiate(inputDevicePrefab, targetContainer);
			KeybindEntry entry = rowObj.GetComponent<KeybindEntry>();
			if (entry != null)
			{
				entry.InitializeInputDevice(record, framework, () =>
				{
					// Re-sort the devices tab and refresh all binding dropdowns
					PopulateDevicesTab();
					RefreshAllBinds();
				});
			}
		}
	}

	///////////////////////////
	// KEYBIND SCROLL POPULATION
	///////////////////////////
	public void PopulateAllScrollTabs()
	{
		activeBindRows.Clear();

		foreach (var mapping in tabContainers)
		{
			if (mapping.contentRoot == null) continue;

			ClearContainer(mapping.contentRoot);

			PopulateGroupControls(mapping.group, mapping.contentRoot);

			List<KeybindFramework.ActionBindItem> binds = framework.GetBindsByGroup(mapping.group);
			foreach (var bind in binds)
			{
				GameObject prefab = bind.inputType == BindInputType.Axis ? axisBindPrefab : buttonBindPrefab;
				if (prefab == null) continue;

				GameObject rowObj = Instantiate(prefab, mapping.contentRoot);
				KeybindEntry entry = rowObj.GetComponent<KeybindEntry>();

				if (entry != null)
				{
					entry.InitializeBind(bind, framework);
					activeBindRows.Add(entry);
				}
			}
		}
	}

	private void ClearContainer(Transform container)
	{
		if (container == null) return;

		for (int i = container.childCount - 1; i >= 0; i--)
		{
			Transform child = container.GetChild(i);

#if UNITY_EDITOR
			if (UnityEditor.Selection.activeGameObject == child.gameObject)
			{
				UnityEditor.Selection.activeGameObject = null;
			}
#endif

			child.SetParent(null);
			Destroy(child.gameObject);
		}
	}

	private void PopulateGroupControls(BindGroup group, Transform container)
	{
		if (sliderPrefab != null)
		{
			GameObject sensObj = Instantiate(sliderPrefab, container);
			KeybindEntry sensEntry = sensObj.GetComponent<KeybindEntry>();
			if (sensEntry != null)
			{
				float initialSens = PlayerMaster.Instance != null ? PlayerMaster.Instance.MouseSensitivity : 10f;
				sensEntry.InitializeSlider("Aim Sensitivity", 0.1f, 50f, initialSens, val =>
				{
					if (PlayerMaster.Instance != null) PlayerMaster.Instance.UpdateMouseSensitivity(val);
				});
			}
		}

		if (group == BindGroup.GroundVehicles || group == BindGroup.Aircraft || group == BindGroup.Rotary || group == BindGroup.Space)
		{
			if (dropdownPrefab != null)
			{
				GameObject behaviorObj = Instantiate(dropdownPrefab, container);
				KeybindEntry behaviorEntry = behaviorObj.GetComponent<KeybindEntry>();
				if (behaviorEntry != null)
				{
					behaviorEntry.InitializeDropdown("Mouse Behaviour", new List<string> { "Steer / Pitch", "Free Look" }, 0, val => { });
				}
			}

			if (togglePrefab != null)
			{
				GameObject freeLookObj = Instantiate(togglePrefab, container);
				KeybindEntry freeLookEntry = freeLookObj.GetComponent<KeybindEntry>();
				if (freeLookEntry != null)
				{
					freeLookEntry.InitializeToggle("Free Look Toggle", true, val => { });
				}
			}
		}
	}

	public void RefreshAllBinds()
	{
		foreach (var row in activeBindRows)
		{
			if (row != null)
			{
				row.RefreshDropdowns();
				row.UpdateDisplay();
			}
		}
	}
}