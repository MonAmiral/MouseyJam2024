using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerController
{
	public static float RobotVolumeMultiplier;
	public static bool MinimapFacesNorthAlways;

	[Header("UI")]
	public Animator GameUIAnimator;

	public RectTransform PatienceGauge;
	public Animator PatienceAnimator;

	public TMPro.TextMeshProUGUI MoneyLabel;
	public Animator MoneyAnimator;

	public TMPro.TextMeshProUGUI TimeLabel;
	public Animator TimeAnimator;

	public TMPro.TextMeshProUGUI PlushiesLabel;
	public Animator PlushiesAnimator;

	public Image BountySprite;
	public TMPro.TextMeshProUGUI BountyLabel;
	public Animator BountyAnimator;

	public TMPro.TextMeshProUGUI InstructionsLabel;
	public Animator InstructionsAnimator;

	public TMPro.TextMeshProUGUI InfiniteDetailsLabel;

	[Header("Persuasion")]
	public Image PersuasionPlushieSprite;
	public RectTransform PatienceGaugePersuasion;
	public TMPro.TextMeshProUGUI PatiencePercentageLabel;
	public RectTransform PersuasionGauge;
	public TMPro.TextMeshProUGUI PersuasionPercentageLabel;

	public Animator PersuasionChangeAnimator;
	public TMPro.TextMeshProUGUI PatienceChangeLabel;
	public TMPro.TextMeshProUGUI PersuasionChangeLabel;

	private PersuasionAction clickedPersuasionAction;

	public PersuasionAction[] PersuasionActions;
	public RectTransform[] PersuasionAnchors;
	public PersuasionAction RefreshPersuasionActions;

	public Animator PortraitsAnimator;

	[Header("Game Over UI")]
	public TMPro.TextMeshProUGUI GameOverReason;
	public Transform PlushiesTable;
	public PlushieItem PlushieItemPrefab;
	public TMPro.TextMeshProUGUI PlushiesScore;

	public TMPro.TextMeshProUGUI TimeLeftLabel;
	public TMPro.TextMeshProUGUI TimeLeftScore;

	public TMPro.TextMeshProUGUI TotalScore;
	public GameObject NewHighscore;

	public GameObject NextLevelButton;
	public GameObject InfiniteModeUnlocked;

	[Space]
	public GameObject PauseScreen;
	public SettingsScreen SettingsScreen;

	[Header("Minimap")]
	public RectTransform MinimapController;
	public RectTransform PlayerMinimapContainer;

	[Header("Audio")]
	public AudioSource AudioSource;
	public AudioClip[] HumanFootsteps;
	public AudioClip[] RobotFootsteps;
	public AudioClip[] RoundStartSounds;
	public AudioClip[] ATMSounds;
	public AudioClip[] MoneyBillSounds;
	public AudioClip[] PurchaseSounds;
	public AudioClip[] AppearSounds, DisappearSounds;
	public AudioClip[] DisplayUISounds, HideUISounds;
	public AudioClip[] ErrorSounds;
	public AudioSource RobotFootstepSource;
	public AudioSource ATMSource;

	[Space]
	public AudioSource MusicSource;
	public List<AudioClip> Musics, GameOverMusics;

	[Space]
	public GameObject[] AchievementFeedbacks;
	private int successfulPersuasionsInARow = 0;

	private void UpdateMinimap()
	{
		this.PlayerMinimapContainer.anchoredPosition = new Vector2(this.transform.position.x, this.transform.position.z) * 50;

		this.PlayerMinimapContainer.localEulerAngles = Vector3.forward * (180 - this.transform.eulerAngles.y);

		if (PlayerController.MinimapFacesNorthAlways)
		{
			this.MinimapController.localEulerAngles = Vector3.zero;
			this.MinimapController.anchoredPosition = -new Vector2(this.transform.position.x, this.transform.position.z) * 50;
		}
		else
		{
			this.MinimapController.localEulerAngles = Vector3.forward * (this.transform.eulerAngles.y);
			Vector3 rotatedPosition = this.transform.InverseTransformPoint(Vector3.zero);
			this.MinimapController.anchoredPosition = new Vector2(rotatedPosition.x, rotatedPosition.z) * 50;
		}
	}

	public void UI_TogglePause(bool pause)
	{
		if (pause)
		{
			Time.timeScale = 0f;
			this.PauseScreen.gameObject.SetActive(true);
		}
		else if (this.SettingsScreen.gameObject.activeSelf)
		{
			this.SettingsScreen.gameObject.SetActive(false);
			this.PauseScreen.SetActive(true);
		}
		else
		{
			Time.timeScale = 1f;
			this.PauseScreen.gameObject.SetActive(false);
		}
	}

	private string FormatTime(float time)
	{
		int timeInt = Mathf.CeilToInt(time);
		return $"{timeInt / 60}:{(timeInt % 60).ToString("00")}";
	}

	private void RefreshPatience(float multiplier = 1)
	{
		this.PatienceGauge.anchorMax = new Vector2(this.patience * multiplier, 1);
		this.PatienceGaugePersuasion.anchorMax = new Vector2(this.patience * multiplier, 1);
		this.PatienceAnimator.SetFloat("Patience", this.patience * multiplier);
		this.PatiencePercentageLabel.text = $"{(int)(this.patience * 100)}%";
	}

	private void RefreshPersuasion()
	{
		this.PersuasionGauge.anchorMin = Vector2.right * (1 - this.persuasion);
		this.PersuasionPercentageLabel.text = $"{(int)(this.persuasion * 100)}%";
	}

	public void UI_ClickPersuasionAction(PersuasionAction action)
	{
		this.clickedPersuasionAction = action;
	}

	public void UI_HoverPersuasionAction(bool inOrOut)
	{
		// TODO: Preview change.
		if (inOrOut)
		{
			this.PlaySound(this.AppearSounds);
		}
		else
		{
			this.PlaySound(this.DisappearSounds);
		}
	}

	public void UI_Quit()
	{
		UnityEngine.SceneManagement.SceneManager.LoadScene(0);
	}

	public void UI_Restart()
	{
		if (Level.InfiniteLevel != -1)
		{
			Level.InfiniteLevel = 0;
			Level.InfiniteScore = 0;
			Level.InfiniteTimeBonus = 0;
		}

		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}

	public void UI_NextLevel()
	{
		Level.InfiniteLevel++;
		Level.InfiniteScore = this.score;
		Level.InfiniteTimeBonus = this.remainingTime;

		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}
}
