using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
	public TMPro.TextMeshProUGUI WarningLabel;
	public GameObject QuitButton;
	public GameObject InfiniteToggle;

	[Space]
	[UnityEngine.Serialization.FormerlySerializedAs("Plushies")]
	public Image[] CornerPlushieImages;
	public PlushieData[] PlushieData;

	[Space]
	public CollectionItem[] CollectionItems;
	public GameObject PreviousCollectionPage, NextCollectionPage;
	public Color SilhouetteColor;
	private int collectionPage;

	[Space]
	public GameObject CollectionZoomPage;
	public Image CollectionZoomImage;
	public TMPro.TextMeshProUGUI CollectionZoomTitle;
	public TMPro.TextMeshProUGUI CollectionZoomDescription;
	public ParticleSystem CollectionZoomParticles;
	public ContentSizeFitter CollectionZoomFitter, CollectionZoomFitter2;
	private int collectionZoomIndex;
	public GameObject[] CollectionTitles;

	public GameObject[] CollectionPages;
	public GameObject[] AchievementPages;
	public GameObject[] AchievementSprites;

	[Space]
	public GameObject TutorialScreen;
	public GameObject[] TutorialPages;
	public Transform[] TutorialKartAnchors;
	private int tutorialPage;
	public Animator TutorialKart;
	public AudioSource TutorialKartAudio;

	[Space]
	public SettingsScreen SettingsScreen;

	[Space]
	public AudioSource SFXSource;
	public AudioClip BoomSound;
	public AudioClip[] PageSounds;
	public AudioSource CollectionSFXSource;
	public AudioClip CollectionHoverSound;

	public AudioSource MusicSource;
	public List<AudioClip> Musics;

	private void Start()
	{
		Time.timeScale = 1;

#if UNITY_WEBGL
		this.QuitButton.SetActive(false);
#else
		this.GetComponent<Animator>().Play("IntroWithoutWarning");
#endif

		this.InitializePlushiesFound();

		this.SettingsScreen.LoadSettings();

		this.PickCornerPlushies();

		this.RefreshCollection();

		this.InfiniteToggle.SetActive(PlayerPrefs.GetInt("HighScore", 0) > 0);
	}

	private void Update()
	{
		if (Time.timeSinceLevelLoad > 1f && !this.MusicSource.isPlaying)
		{
			// Never play the same twice: ignore the last item and put it there after being picked.
			AudioClip music = this.Musics[Random.Range(0, this.Musics.Count - 1)];
			this.Musics.Remove(music);
			this.Musics.Add(music);

			this.MusicSource.PlayOneShot(music);
		}

		if (this.CollectionZoomFitter2.gameObject.activeInHierarchy && !this.CollectionZoomFitter2.enabled)
		{
			this.CollectionZoomFitter2.enabled = true;
		}

		if (this.CollectionZoomFitter.gameObject.activeInHierarchy && !this.CollectionZoomFitter.enabled)
		{
			this.CollectionZoomFitter.enabled = true;
			this.CollectionZoomFitter2.enabled = false;
		}

		if (this.TutorialKart.gameObject.activeInHierarchy)
		{
			Transform anchor = this.TutorialKartAnchors[this.tutorialPage];

			if (Vector3.Distance(this.TutorialKart.transform.position, anchor.position) > 0.1f)
			{
				this.TutorialKart.SetBool("Moving", true);
				this.TutorialKartAudio.volume = PlayerController.RobotVolumeMultiplier;

				if (this.TutorialKart.transform.rotation != Quaternion.identity)
				{
					this.TutorialKart.transform.rotation = Quaternion.RotateTowards(this.TutorialKart.transform.rotation, Quaternion.identity, Time.deltaTime * 90);
				}
				else
				{
					this.TutorialKart.transform.position = Vector3.MoveTowards(this.TutorialKart.transform.position, anchor.position, Time.deltaTime * 15);
				}
			}
			else
			{
				if (this.TutorialKart.transform.rotation != anchor.rotation)
				{
					this.TutorialKart.transform.rotation = Quaternion.RotateTowards(this.TutorialKart.transform.rotation, anchor.rotation, Time.deltaTime * 90);
				}
				else
				{
					this.TutorialKart.SetBool("Moving", false);
					this.TutorialKartAudio.volume = 0;

					if (!anchor.gameObject.activeInHierarchy)
					{
						for (int i = 0; i < this.TutorialPages.Length; i++)
						{
							this.TutorialPages[i].SetActive(i == this.tutorialPage);
						}
					}
				}
			}
		}
	}

	private void PickCornerPlushies()
	{
		List<PlushieData> cache = new List<PlushieData>(this.PlushieData);
		foreach (Image plushie in this.CornerPlushieImages)
		{
			PlushieData data;
			do
			{
				if (cache.Count == 0)
				{
					data = null;
					break;
				}

				data = cache[Random.Range(0, cache.Count)];
				cache.Remove(data);
			}
			while (!data.HasBeenFound);

			if (data != null)
			{
				plushie.sprite = data.Sprite;
			}
			else
			{
				plushie.enabled = false;
			}
		}

	}

	private void InitializePlushiesFound()
	{
		for (int i = 0; i < this.PlushieData.Length; i++)
		{
			this.PlushieData[i].HasBeenFound = PlayerPrefs.GetInt("Found" + this.PlushieData[i].name, 0) == 1;
		}
	}

	public void RefreshCollection()
	{
		int startIndex = this.collectionPage * this.CollectionItems.Length;

		for (int itemIndex = 0; itemIndex < this.CollectionItems.Length; itemIndex++)
		{
			int plushieIndex = startIndex + itemIndex;

			CollectionItem item = this.CollectionItems[itemIndex];
			item.gameObject.SetActive(plushieIndex < this.PlushieData.Length);
			if (!item.gameObject.activeSelf)
			{
				this.NextCollectionPage.SetActive(false);
				continue;
			}

			PlushieData plushie = this.PlushieData[plushieIndex];

			if (plushie.HasBeenFound)
			{
				item.Image.sprite = plushie.Sprite;
				item.Image.color = Color.white;
			}
			else
			{
				item.Image.sprite = plushie.SilhouetteSprite;
				item.Image.color = this.SilhouetteColor;
			}

			item.Particles.gameObject.SetActive(plushie.Quality == global::PlushieData.PlushieQuality.Gold && plushie.HasBeenFound);
		}

		this.PreviousCollectionPage.SetActive(this.collectionPage > 0);
		this.NextCollectionPage.SetActive(true);

		foreach (GameObject page in this.CollectionPages)
		{
			page.SetActive(true);
		}

		foreach (GameObject page in this.AchievementPages)
		{
			page.SetActive(false);
		}
	}

	private void RefreshAchievements()
	{
		for (int i = 0; i < this.AchievementSprites.Length; i++)
		{
			Achievement achievement = (Achievement)i;
			this.AchievementSprites[i].SetActive(PlayerPrefs.GetInt(achievement.ToString(), 0) == 1);
		}

		this.PreviousCollectionPage.SetActive(true);
		this.NextCollectionPage.SetActive(false);

		foreach (GameObject page in this.CollectionPages)
		{
			page.SetActive(false);
		}

		foreach (GameObject page in this.AchievementPages)
		{
			page.SetActive(true);
		}
	}

	private void RefreshCollectionZoom()
	{
		PlushieData plushie = this.PlushieData[this.collectionZoomIndex];

		if (plushie.HasBeenFound)
		{
			this.CollectionZoomImage.sprite = plushie.Sprite;
			this.CollectionZoomImage.color = Color.white;
			this.CollectionZoomTitle.text = plushie.Name;
			this.CollectionZoomDescription.text = plushie.GetDescription();
			this.CollectionZoomParticles.gameObject.SetActive(plushie.Quality == global::PlushieData.PlushieQuality.Gold);
		}
		else
		{
			this.CollectionZoomImage.sprite = plushie.SilhouetteSprite;
			this.CollectionZoomImage.color = this.SilhouetteColor;

			this.CollectionZoomTitle.text = "???";
			this.CollectionZoomDescription.text = "Buy this plush in-game to add it to your collection!";
			this.CollectionZoomParticles.gameObject.SetActive(false);
		}

		this.CollectionZoomFitter.enabled = false;

		this.SFXSource.pitch = 1f;
		this.SFXSource.PlayOneShot(this.PageSounds[Random.Range(0, this.PageSounds.Length)]);
	}

	public void UI_ChangeCollectionPage(int change)
	{
		this.collectionPage += change;

		switch (this.collectionPage)
		{
			case 0:
			case 1:
			case 2:
				this.RefreshCollection();
				break;

			case 3:
				this.RefreshAchievements();
				break;
		}

		for (int i = 0; i < this.CollectionTitles.Length; i++)
		{
			this.CollectionTitles[i].SetActive(this.collectionPage == i);
		}
	}

	public void UI_ChangeCollectionZoomIndex(int change)
	{
		this.collectionZoomIndex = (this.collectionZoomIndex + change + this.PlushieData.Length) % this.PlushieData.Length;
		this.RefreshCollectionZoom();

		this.SFXSource.pitch = 1f;
		this.SFXSource.PlayOneShot(this.PageSounds[Random.Range(0, this.PageSounds.Length)]);
	}

	public void UI_Quit()
	{
		Application.Quit();
	}

	public void UI_Play()
	{
		if (PlayerPrefs.GetInt("TutorialDone", 0) == 0)
		{
			this.TutorialScreen.SetActive(true);
			return;
		}

		Level.InfiniteLevel = -1;

		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}

	public void UI_ChangeTutorialPage(int change)
	{
		this.tutorialPage = Mathf.Clamp(this.tutorialPage + change, 0, this.TutorialPages.Length - 1);
	}

	public void UI_FinishTutorialAndPlay()
	{
		PlayerPrefs.SetInt("TutorialDone", 1);
		this.UI_Play();
	}

	public void UI_PlayInfinite()
	{
		Level.InfiniteLevel = 0;
		Level.InfiniteScore = 0;
		Level.InfiniteTimeBonus = 0;

		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}

	public void UI_HoverCollection(int index)
	{
		if (!this.CollectionSFXSource.isPlaying)
		{
			this.CollectionSFXSource.pitch = 0.64f + index * 0.04f;
			this.CollectionSFXSource.PlayOneShot(this.CollectionHoverSound);
		}
	}

	public void UI_ClickCollection(int index)
	{
		this.CollectionZoomPage.SetActive(true);

		this.collectionZoomIndex = this.collectionPage * this.CollectionItems.Length + index;
		this.RefreshCollectionZoom();
	}

	public void ANIM_Boom()
	{
		this.SFXSource.PlayOneShot(this.BoomSound);
	}
}
