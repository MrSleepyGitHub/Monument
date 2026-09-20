///////////////////////////
// OPTIONS TAB MANAGER
///////////////////////////
using UnityEngine;

public class OptionsTabManager : MonoBehaviour
{
	[Header("Primary Navigation Columns/Panels")]
	public GameObject controlsSubColumn;
	public GameObject audioContentPanel;
	public GameObject videoContentPanel;

	[Header("Controls Content Panels")]
	public GameObject inputDevicesContentPanel; // Tab 0 (New Devices Panel)
	public GameObject infantryContentPanel;      // Tab 1
	public GameObject groundVehicleContentPanel; // Tab 2
	public GameObject aircraftContentPanel;      // Tab 3
	public GameObject rotaryContentPanel;        // Tab 4
	public GameObject spaceContentPanel;         // Tab 5

	private void OnEnable()
	{
		ResetToRootView();
	}

	public void ResetToRootView()
	{
		SetGroupActive(null, controlsSubColumn, audioContentPanel, videoContentPanel);
		HideControlsPanels();
	}

	// ==========================================
	// PRIMARY NAVIGATION (TIER 1)
	// ==========================================
	public void SelectControls()
	{
		SetGroupActive(controlsSubColumn, audioContentPanel, videoContentPanel);
		HideControlsPanels();
	}

	public void SelectAudio()
	{
		SetGroupActive(audioContentPanel, controlsSubColumn, videoContentPanel);
		HideControlsPanels();
	}

	public void SelectVideo()
	{
		SetGroupActive(videoContentPanel, controlsSubColumn, audioContentPanel);
		HideControlsPanels();
	}

	// ==========================================
	// CONTROLS CONTEXT SUB-TABS (TIER 2)
	// ==========================================
	public void SelectInputDevices()
	{
		ActivateControlsPanel(inputDevicesContentPanel);
	}

	public void SelectInfantry()
	{
		ActivateControlsPanel(infantryContentPanel);
	}

	public void SelectGroundVehicles()
	{
		ActivateControlsPanel(groundVehicleContentPanel);
	}

	public void SelectAircraft()
	{
		ActivateControlsPanel(aircraftContentPanel);
	}

	public void SelectRotary()
	{
		ActivateControlsPanel(rotaryContentPanel);
	}

	public void SelectSpace()
	{
		ActivateControlsPanel(spaceContentPanel);
	}

	// ==========================================
	// SAFE VISIBILITY UTILITIES
	// ==========================================
	private void ActivateControlsPanel(GameObject targetPanel)
	{
		SetGroupActive(targetPanel,
			inputDevicesContentPanel,
			infantryContentPanel,
			groundVehicleContentPanel,
			aircraftContentPanel,
			rotaryContentPanel,
			spaceContentPanel
		);
	}

	private void HideControlsPanels()
	{
		SetGroupActive(null,
			inputDevicesContentPanel,
			infantryContentPanel,
			groundVehicleContentPanel,
			aircraftContentPanel,
			rotaryContentPanel,
			spaceContentPanel
		);
	}

	private void SetGroupActive(GameObject activeTarget, params GameObject[] targets)
	{
		if (targets != null)
		{
			foreach (var obj in targets)
			{
				if (obj != null && obj != activeTarget)
				{
					obj.SetActive(false);
				}
			}
		}

		if (activeTarget != null)
		{
			activeTarget.SetActive(true);
		}
	}
}