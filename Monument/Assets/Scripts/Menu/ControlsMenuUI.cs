using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class ControlsMenuUI : MonoBehaviour
{
	[System.Serializable]
	public struct ActionConfig
	{
		public string label;
		public string actionName;
	}

	[Header("References")]
	public KeybindFramework framework; //[cite: 1]
	public GameObject rowPrefab;
	public Transform scrollContent;

	[Header("Top Header Controls")]
	public TMP_Dropdown deviceFilterDropdown;
	public Slider lookSensitivitySlider;
	public TMP_InputField sensitivityField;

	[Header("Category Configurations")]
	public List<ActionConfig> infantryActions = new List<ActionConfig>();
	public List<ActionConfig> groundVehicleActions = new List<ActionConfig>();
	public List<ActionConfig> aircraftActions = new List<ActionConfig>();

	private List<int> deviceIdMap = new List<int>(); //[cite: 1]
	private List<KeybindEntry> activeRows = new List<KeybindEntry>();
	private int currentTab = 0; // 0 = Infantry, 1 = Ground, 2 = Aircraft

	private void Start()
	{
		PopulateDeviceFilter();
		SwitchTab(0);

		deviceFilterDropdown.onValueChanged.AddListener((val) => RefreshAllRows());
	}

	public void PopulateDeviceFilter()
	{
		deviceFilterDropdown.ClearOptions();
		deviceIdMap.Clear();

		List<string> options = new List<string>();
		foreach (var kvp in framework.connectedDevices) //[cite: 1]
		{
			options.Add(kvp.Value.customName); //[cite: 1]
			deviceIdMap.Add(kvp.Key); //[cite: 1]
		}
		deviceFilterDropdown.AddOptions(options);
	}

	public int GetSelectedDeviceId()
	{
		if (deviceIdMap.Count == 0 || deviceFilterDropdown.value >= deviceIdMap.Count) return -1;
		return deviceIdMap[deviceFilterDropdown.value];
	}

	public void SwitchTab(int tabIndex)
	{
		currentTab = tabIndex;

		// Clear existing instantiated row instances
		for (int i = scrollContent.childCount - 1; i >= 0; i--)
		{
			Destroy(scrollContent.GetChild(i).gameObject);
		}
		activeRows.Clear();

		List<ActionConfig> targetList = tabIndex switch
		{
			1 => groundVehicleActions,
			2 => aircraftActions,
			_ => infantryActions
		};

		foreach (var config in targetList)
		{
			InputAction action = framework.movementMap.FindAction(config.actionName); //[cite: 1]
			if (action == null) continue;

			GameObject rowObj = Instantiate(rowPrefab, scrollContent);
			KeybindEntry entry = rowObj.GetComponent<KeybindEntry>();
			entry.Initialize(config.label, action, framework, GetSelectedDeviceId);
			activeRows.Add(entry);
		}
	}

	public void RefreshAllRows()
	{
		foreach (var row in activeRows) row.UpdateDisplay();
	}
}