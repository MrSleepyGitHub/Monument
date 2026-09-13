using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
	public AudioMixer audioMixer;
	public Dropdown resolutionDropdown;

	private Resolution[] resolutions;

	private void Start()
	{
		InitializeResolutions();
	}

	private void InitializeResolutions()
	{
		if (resolutionDropdown == null) return;

		resolutions = Screen.resolutions;
		resolutionDropdown.ClearOptions();

		List<string> resOptions = new List<string>();
		int currentResolutionIndex = 0;

		for (int i = 0; i < resolutions.Length; i++)
		{
			resOptions.Add($"{resolutions[i].width} x {resolutions[i].height}");

			if (resolutions[i].width == Screen.currentResolution.width &&
				resolutions[i].height == Screen.currentResolution.height)
			{
				currentResolutionIndex = i;
			}
		}

		resolutionDropdown.AddOptions(resOptions);
		resolutionDropdown.value = currentResolutionIndex;
		resolutionDropdown.RefreshShownValue();
	}

	public void SetResolution(int resolutionIndex)
	{
		Resolution res = resolutions[resolutionIndex];
		Screen.SetResolution(res.width, res.height, Screen.fullScreen);
	}

	public void SetVolume(float volume)
	{
		if (audioMixer != null) audioMixer.SetFloat("volumeMaster", volume);
	}

	public void SetQuality(int qualityIndex)
	{
		QualitySettings.SetQualityLevel(qualityIndex);
	}

	public void SetScreenMode(int screenMode)
	{
		Screen.fullScreenMode = screenMode switch
		{
			1 => FullScreenMode.FullScreenWindow,
			2 => FullScreenMode.ExclusiveFullScreen,
			_ => FullScreenMode.Windowed,
		};

		Screen.fullScreen = screenMode != 3;
	}
}