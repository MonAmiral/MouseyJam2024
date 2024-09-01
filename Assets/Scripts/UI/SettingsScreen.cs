using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;

public class SettingsScreen : MonoBehaviour
{
	public static float GlobalSFXVolume;

	public Slider MasterVolume, MusicVolume, SFXVolume, CrowdVolume, RobotVolume;
	public TMPro.TMP_Dropdown FullScreenDropdown, QualityDropdown, AntiAliasingDropdown;
	public Button PostProcessIsOnButton, MinimapRotationIsOn;
	public GameObject FullScreenLine;

	[Space]
	public AudioSource[] Music;
	public AudioSource[] SFX;
	public AudioSource[] Crowd;

	public PostProcessLayer PostProcessLayer;
	public PostProcessVolume PostProcessVolume;

	public void LoadSettings()
	{
		this.MasterVolume.value = PlayerPrefs.GetFloat("MasterVolume", 5);
		this.MusicVolume.value = PlayerPrefs.GetFloat("MusicVolume", 2);
		this.SFXVolume.value = PlayerPrefs.GetFloat("SFXVolume", 10);
		this.CrowdVolume.value = PlayerPrefs.GetFloat("CrowdVolume", 1);
		this.RobotVolume.value = PlayerPrefs.GetFloat("RobotVolume", 10);

		this.QualityDropdown.value = PlayerPrefs.GetInt("Quality", QualitySettings.GetQualityLevel());
		this.AntiAliasingDropdown.value = PlayerPrefs.GetInt("AntiAliasing", 1);

		this.UI_SetPostProcessing(PlayerPrefs.GetInt("PostProcess", 1) == 1);
		this.UI_SetMinimapRotation(PlayerPrefs.GetInt("MinimapRotation", 1) == 1);
	}

	private void OnEnable()
	{
#if UNITY_WEBGL
		this.FullScreenLine.SetActive(false);
#else
		this.FullScreenDropdown.SetValueWithoutNotify(Mathf.Min((int)Screen.fullScreenMode, 2));
#endif
	}

	private void OnDestroy()
	{
		PlayerPrefs.Save();
	}

	public void UI_SetMasterVolume(float value)
	{
		AudioListener.volume = value / 10f;
		PlayerPrefs.SetFloat("MasterVolume", value);
	}

	public void UI_SetMusicVolume(float value)
	{
		foreach (AudioSource music in this.Music)
		{
			music.volume = value / 10f;
		}

		PlayerPrefs.SetFloat("MusicVolume", value);
	}

	public void UI_SetSFXVolume(float value)
	{
		GlobalSFXVolume = value / 10f;

		foreach (AudioSource sfx in this.SFX)
		{
			sfx.volume = value / 10f;
		}

		PlayerPrefs.SetFloat("SFXVolume", value);

		if (this.gameObject.activeInHierarchy)
		{
			// Play a sound.
		}
	}

	public void UI_SetCrowdVolume(float value)
	{
		foreach (AudioSource crowd in this.Crowd)
		{
			crowd.volume = value / 10f * SettingsScreen.GlobalSFXVolume;
		}

		PlayerPrefs.SetFloat("CrowdVolume", value);
	}

	public void UI_SetRobotVolume(float value)
	{
		PlayerController.RobotVolumeMultiplier = value / 10f;
		PlayerPrefs.SetFloat("RobotVolume", value);
	}

	public void UI_SetFullscreenMode(int mode)
	{
		switch (mode)
		{
			case 0:
				Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.ExclusiveFullScreen);
				break;

			case 1:
				Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
				break;

			case 2:
				Screen.fullScreenMode = FullScreenMode.Windowed;
				break;
		}
	}

	public void UI_SetQuality(int value)
	{
		QualitySettings.SetQualityLevel(value);
		PlayerPrefs.SetInt("Quality", value);
	}

	public void UI_SetAntiAliasing(int value)
	{
		if (this.PostProcessLayer)
		{
			switch (value)
			{
				case 0:
					this.PostProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
					break;
				case 1:
					this.PostProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
					break;

				case 2:
					this.PostProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
					this.PostProcessLayer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.Medium;
					break;

				case 3:
					this.PostProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
					this.PostProcessLayer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.High;
					break;

				case 4:
					this.PostProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.TemporalAntialiasing;
					break;
			}
		}

		PlayerPrefs.SetInt("AntiAliasing", value);
	}

	public void UI_SetPostProcessing(bool value)
	{
		if (this.PostProcessVolume)
		{
			this.PostProcessVolume.enabled = value;
		}

		this.PostProcessIsOnButton.gameObject.SetActive(value);
		PlayerPrefs.SetInt("PostProcess", value ? 1 : 0);
	}

	public void UI_SetMinimapRotation(bool value)
	{
		PlayerController.MinimapFacesNorthAlways = value;

		this.MinimapRotationIsOn.gameObject.SetActive(value);
		PlayerPrefs.SetInt("MinimapRotation", value ? 1 : 0);
	}

	public void UI_ClearData()
	{
		PlayerPrefs.DeleteAll();
		UnityEngine.SceneManagement.SceneManager.LoadScene(0);
	}
}
