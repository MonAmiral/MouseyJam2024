using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerController : MonoBehaviour
{
	public static PlayerController Instance;
	private bool isGenerating;
	private bool isFirstTime;
	private bool skipIntro;

	private Vector2Int position;
	private Direction direction;

	private bool isMoving;
	private bool isInteracting;
	private Vector2 queuedInput;
	private bool queuedInteraction;

	private bool gameIsOver;
	private int score;
	private float remainingTime = 180;
	private int money = 150;
	private float patience = 0.75f;
	private float persuasion;
	private List<Booth> boothsBought = new List<Booth>();

	public AnimationCurve PatienceGainCurve;

	[System.NonSerialized]
	public Transform Camera;

	private void Awake()
	{
		Time.timeScale = 1;

		Camera = UnityEngine.Camera.main.transform;
		Instance = this;

		this.isFirstTime = PlayerPrefs.GetInt("FirstTime", 1) == 1;

		this.isGenerating = true;

		this.MusicSource.enabled = false;

		this.SettingsScreen.LoadSettings();

#if UNITY_STANDALONE_WIN
		Application.targetFrameRate = 60;
#endif
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			this.UI_TogglePause(Time.timeScale != 0);
			return;
		}

		this.UpdateRobotFootstepSource();
		this.UpdateMusic();

		if (Time.timeScale == 0 || this.isGenerating)
		{
			return;
		}

		if (!this.gameIsOver)
		{
			this.remainingTime = Mathf.Max(0, this.remainingTime - Time.deltaTime);
			this.TimeLabel.text = this.FormatTime(this.remainingTime);
			if (this.remainingTime <= 0 && !this.isInteracting)
			{
				this.StartCoroutine(this.GameOver());

				return;
			}

			if (this.remainingTime < 5 && this.remainingTime + Time.deltaTime >= 5)
			{
				//this.GameUIAnimator.Play("GameAlmostOver");
				this.TimeAnimator.Play("GameAlmostOver");
			}

			if (!this.isInteracting)
			{
				if (this.patience < 0.2f)
				{
					this.patience += Time.deltaTime / 100f;
					this.RefreshPatience();
				}
			}

			this.PollInput();
		}
	}

	private void LateUpdate()
	{
		this.UpdateMinimap();
	}

	private void PollInput()
	{
		if (!this.isInteracting)
		{
			if (this.isMoving)
			{
				if (Input.GetButtonDown("Vertical"))
				{
					this.queuedInput = Vector2.up * Input.GetAxis("Vertical");
					this.queuedInteraction = false;
				}
				else if (Input.GetButtonDown("Horizontal"))
				{
					this.queuedInput = Vector2.right * Input.GetAxis("Horizontal");
					this.queuedInteraction = false;
				}

				if (Input.GetButtonDown("Interact"))
				{
					this.queuedInteraction = true;
				}
			}
			else
			{
				if (Input.GetAxis("Vertical") + this.queuedInput.y != 0)
				{
					this.StartCoroutine(this.Move((int)Mathf.Sign(Input.GetAxis("Vertical") + this.queuedInput.y)));
				}
				else if (Input.GetAxis("Horizontal") + this.queuedInput.x != 0)
				{
					this.StartCoroutine(this.Turn((int)Mathf.Sign(Input.GetAxis("Horizontal") + this.queuedInput.x)));
				}

				if (Input.GetButtonDown("Interact") || this.queuedInteraction)
				{
					this.StartCoroutine(this.Interact());
				}

				this.queuedInput = Vector2.zero;
				this.queuedInteraction = false;
			}
		}
	}

	private void UpdateRobotFootstepSource()
	{
		if (Time.timeScale == 0)
		{
			this.RobotFootstepSource.volume = 0;
		}
		else if (this.isMoving)
		{
			this.RobotFootstepSource.volume = Mathf.Lerp(this.RobotFootstepSource.volume, PlayerController.RobotVolumeMultiplier * SettingsScreen.GlobalSFXVolume, Time.deltaTime * 6);
			this.RobotFootstepSource.pitch = Mathf.Lerp(this.RobotFootstepSource.pitch, 1f, Time.deltaTime * 3);
		}
		else
		{
			this.RobotFootstepSource.volume = Mathf.Lerp(this.RobotFootstepSource.volume, 0, Time.deltaTime * 10);
			this.RobotFootstepSource.pitch = Mathf.Lerp(this.RobotFootstepSource.pitch, 0.8f, Time.deltaTime * 2);
		}
	}

	private void UpdateMusic()
	{
		if (this.MusicSource.enabled && !this.MusicSource.isPlaying)
		{
			// Never play the same twice: ignore the last item and put it there after being picked.
			List<AudioClip> musics = this.gameIsOver ? this.GameOverMusics : this.Musics;
			AudioClip music = musics[Random.Range(0, musics.Count - 1)];
			musics.Remove(music);
			musics.Add(music);

			this.MusicSource.PlayOneShot(music);
		}
	}

	public void LockForGeneration(int score, float bonusRemainingTime)
	{
		this.isGenerating = true;

		this.score = score;
		this.remainingTime += bonusRemainingTime;
	}

	public void FreeFromGeneration()
	{
		this.position = Vector2Int.RoundToInt(new Vector2(this.transform.position.x, this.transform.position.z));
		this.direction = Direction.YPositive;

		if (Level.InfiniteLevel == -1)
		{
			this.StartCoroutine(this.StartPlayingNormal());
		}
		else
		{
			this.StartCoroutine(this.StartPlayingInfinite());
		}
	}

	private IEnumerator StartPlayingNormal()
	{
		this.PlushiesLabel.text = $"0/{Level.Instance.PlushieCount}";
		this.TimeLabel.text = this.FormatTime(this.remainingTime);

		this.RefreshPatience();
		this.MoneyLabel.text = $"${this.money}";

		this.BountySprite.sprite = Level.Instance.BountyBooth.PlushieRenderer.sprite;
		this.BountyLabel.text = $"X{Level.Instance.BountyMultiplier}";
		this.BountyAnimator.Play(Level.Instance.BountyMultiplier.ToString());

		this.InfiniteDetailsLabel.text = Level.Instance.CurrentSettings.InfoText.Replace("{level}", (Level.InfiniteLevel + 1).ToString());

		// Wait for generation UI to be hidden.
		yield return new WaitForSeconds(1);

		this.GameUIAnimator.Play("Show");

		yield return this.WaitForInputToStartGame();
	}

	private IEnumerator StartPlayingInfinite()
	{
		this.PlushiesLabel.text = $"0/{Level.Instance.PlushieCount}";
		this.TimeLabel.text = this.FormatTime(this.remainingTime);

		this.RefreshPatience();
		this.MoneyLabel.text = $"${this.money}";

		this.BountySprite.sprite = Level.Instance.BountyBooth.PlushieRenderer.sprite;
		this.BountyLabel.text = $"X{Level.Instance.BountyMultiplier}";
		this.BountyAnimator.Play(Level.Instance.BountyMultiplier.ToString());

		this.InfiniteDetailsLabel.text = Level.Instance.CurrentSettings.InfoText.Replace("{level}", (Level.InfiniteLevel + 1).ToString());

		// Wait for generation UI to be hidden.
		yield return new WaitForSeconds(1);

		this.GameUIAnimator.Play("ShowInfinite");

		yield return this.WaitForInputToStartGame();
	}

	private IEnumerator StartPlayingTutorial()
	{
		this.RefreshPatience(0);
		this.MoneyLabel.text = "$0";
		this.TimeLabel.text = "0:00";
		this.PlushiesLabel.text = $"0/0";

		this.BountySprite.sprite = Level.Instance.BountyBooth.PlushieRenderer.sprite;
		this.BountyLabel.text = $"X{Level.Instance.BountyMultiplier}";
		this.BountyAnimator.Play(Level.Instance.BountyMultiplier.ToString());

		// Wait for generation UI to be hidden.
		yield return new WaitForSeconds(1);

		this.GameUIAnimator.Play("Show");
		float elapsedTime;

		// Display map.
		yield return this.WaitForSecondsOrAnyKey(3f);

		// Count time and plushies up and display bounty.
		elapsedTime = 0f;
		while (elapsedTime < 1f)
		{
			this.PlushiesLabel.text = $"0/{(int)(Level.Instance.PlushieCount * elapsedTime)}";

			this.TimeLabel.text = this.FormatTime(this.remainingTime * elapsedTime);

			if (Time.timeScale != 0 && (Input.anyKeyDown || this.skipIntro))
			{
				this.skipIntro = true;
				break;
			}

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		this.PlushiesLabel.text = $"0/{Level.Instance.PlushieCount}";
		this.TimeLabel.text = this.FormatTime(this.remainingTime);

		yield return this.WaitForSecondsOrAnyKey(2);

		// Count money and patience up.
		elapsedTime = 0f;
		while (elapsedTime < 1f)
		{
			this.RefreshPatience(elapsedTime);
			this.MoneyLabel.text = $"${(int)(this.money * elapsedTime)}";

			if (Time.timeScale != 0 && (Input.anyKeyDown || this.skipIntro))
			{
				this.skipIntro = true;
				break;
			}

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		this.RefreshPatience();
		this.MoneyLabel.text = $"${this.money}";

		yield return this.WaitForSecondsOrAnyKey(4);

		yield return this.WaitForInputToStartGame();
	}

	private IEnumerator WaitForInputToStartGame()
	{
		// Wait for an input.
		while (Time.timeScale == 0 || (!Input.anyKeyDown && !this.skipIntro))
		{
			yield return null;
		}

		// Countdown.
		this.GameUIAnimator.CrossFade("Countdown", 0.1f);

		yield return new WaitForSeconds(1.5f);
		this.PlaySound(this.RoundStartSounds);

		this.isGenerating = false;
		this.isFirstTime = false;

		PlayerPrefs.SetInt("FirstTime", 0);
		PlayerPrefs.Save();

		yield return new WaitForSeconds(1f);
		this.MusicSource.enabled = true;
	}

	private IEnumerator GameOver()
	{
		if (this.gameIsOver)
		{
			yield break;
		}

		this.gameIsOver = true;

		this.PlaySound(this.RoundStartSounds);

		this.MusicSource.Stop();
		this.MusicSource.enabled = false;

		if (this.remainingTime > 30)
		{
			this.UnlockAchievement(Achievement.FinishEarly);
		}

		if (this.boothsBought.Count == Level.Instance.PlushieCount)
		{
			this.GameOverReason.text = "You got all the plushies!";
		}

		if (this.money >= 500)
		{
			this.UnlockAchievement(Achievement.BigMoney);
		}

		if (this.remainingTime > 0 && Level.InfiniteLevel != -1)
		{
			this.NextLevelButton.SetActive(true);
		}

		this.InstructionsAnimator.SetBool("Visible", false);

		//this.TimeLabel.text = "0:00";
		this.GameUIAnimator.Play("GameOver");

		this.PlushiesScore.text = "0";

		this.TimeLeftLabel.text = "0:00";
		this.TimeLeftScore.text = "0";

		int scoreBefore = this.score;
		this.TotalScore.text = scoreBefore.ToString();
		this.NewHighscore.SetActive(false);

		float elapsedTime;

		// Compute score and store high score if relevant.
		int previousHighScore = PlayerPrefs.GetInt("HighScore", 0);
		for (int i = 0; i < this.boothsBought.Count; i++)
		{
			Booth booth = this.boothsBought[i];
			int plushieScore = booth.PlushieData.BaseScore;

			if (booth == Level.Instance.BountyBooth)
			{
				plushieScore = (int)(plushieScore * Level.Instance.BountyMultiplier);
			}

			this.score += plushieScore;
		}

		this.score += (int)(this.remainingTime * 100);
		if (this.score > previousHighScore)
		{
			PlayerPrefs.SetInt("HighScore", this.score);
			PlayerPrefs.Save();
		}

		yield return new WaitForSeconds(3);

		this.MusicSource.enabled = true;

		int displayedPlushiesScore = 0;
		for (int i = 0; i < this.boothsBought.Count; i++)
		{
			Booth booth = this.boothsBought[i];

			PlushieItem item = GameObject.Instantiate(this.PlushieItemPrefab, this.PlushiesTable);
			item.Sprite.sprite = booth.PlushieData.Sprite;

			int plushieScore = booth.PlushieData.BaseScore;

			if (booth == Level.Instance.BountyBooth)
			{
				plushieScore = (int)(plushieScore * Level.Instance.BountyMultiplier);
				item.MultiplierLabel.text = $"X{Level.Instance.BountyMultiplier}";
			}

			displayedPlushiesScore += plushieScore;
			this.PlushiesScore.text = displayedPlushiesScore.ToString();

			yield return new WaitForSeconds(0.2f);
		}

		yield return new WaitForSeconds(1);

		if (this.remainingTime > 0)
		{
			elapsedTime = 0;
			while (elapsedTime < this.remainingTime)
			{
				elapsedTime += Time.deltaTime * 10;

				this.TimeLeftLabel.text = this.FormatTime(elapsedTime);
				this.TimeLeftScore.text = ((int)(elapsedTime * 100)).ToString();

				yield return null;
			}

			this.TimeLeftLabel.text = this.FormatTime(this.remainingTime);
			this.TimeLeftScore.text = ((int)(this.remainingTime * 100)).ToString();

			yield return new WaitForSeconds(1);
		}

		elapsedTime = 0;
		while (elapsedTime < 2)
		{
			elapsedTime += Time.deltaTime;

			int tmpScore = (int)Mathf.Lerp(scoreBefore, this.score, elapsedTime / 2);
			this.TotalScore.text = tmpScore.ToString();

			this.NewHighscore.SetActive(tmpScore > previousHighScore);

			yield return null;
		}

		if (this.score >= 10000)
		{
			this.UnlockAchievement(Achievement.Score10k);

			if (this.score >= 20000)
			{
				this.UnlockAchievement(Achievement.Score20k);
			}
		}

		this.TotalScore.text = this.score.ToString();

		if (this.score > 0 && previousHighScore == 0)
		{
			this.InfiniteModeUnlocked.SetActive(true);
		}
	}

	private IEnumerator Move(int change)
	{
		Vector2Int goal = this.position;

		switch (this.direction)
		{
			case Direction.YPositive:
				goal.y += change;
				break;
			case Direction.XPositive:
				goal.x += change;
				break;
			case Direction.YNegative:
				goal.y -= change;
				break;
			case Direction.XNegative:
				goal.x -= change;
				break;
		}

		if (!Level.Instance.CanGo(goal.x, goal.y))
		{
			yield break;
		}

		this.InstructionsAnimator.SetBool("Visible", false);

		this.isMoving = true;
		this.position = goal;

		Vector3 startPosition = this.transform.position;
		Vector3 goalPosition = new Vector3(this.position.x, 0, this.position.y);

		this.PlaySound(this.HumanFootsteps);
		this.PlaySound(this.RobotFootsteps);

		float elapsedTime = 0;
		while (elapsedTime < 0.5f)
		{
			this.transform.position = Vector3.Lerp(startPosition, goalPosition, elapsedTime * 2);

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		this.transform.position = goalPosition;

		this.isMoving = false;
		this.DisplayInteractionInstruction();
	}

	private IEnumerator Turn(int change)
	{
		this.isMoving = true;

		this.InstructionsAnimator.SetBool("Visible", false);

		if (this.direction == Direction.XNegative && change == 1)
		{
			this.direction = Direction.YPositive;
		}
		else if (this.direction == Direction.YPositive && change == -1)
		{
			this.direction = Direction.XNegative;
		}
		else
		{
			this.direction += change;
		}

		Quaternion startRotation = this.transform.rotation;
		Quaternion goalRotation = Quaternion.Euler(0, 90 * (int)this.direction, 0);

		this.PlaySound(this.RobotFootsteps);

		float elapsedTime = 0;
		while (elapsedTime < 0.5f)
		{
			this.transform.rotation = Quaternion.Lerp(startRotation, goalRotation, elapsedTime * 2);

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		this.transform.rotation = goalRotation;

		this.isMoving = false;
		this.DisplayInteractionInstruction();
	}

	private void DisplayInteractionInstruction()
	{
		if (this.gameIsOver)
		{
			return;
		}

		Interaction interaction = Level.Instance.GetInteraction(this.position, this.direction);
		if (interaction == null)
		{
			return;
		}

		if (interaction is Booth booth)
		{
			if (booth.IsConnorBooth)
			{
				this.InstructionsAnimator.SetBool("Visible", true);
				this.InstructionsLabel.text = "Press E for patience";
			}
			else if (!this.boothsBought.Contains(booth))
			{
				this.InstructionsAnimator.SetBool("Visible", true);
				this.InstructionsLabel.text = " Press E to buy\n" + booth.PlushieData.Name;
			}
		}
		else if (interaction is ATM atm)
		{
			this.InstructionsAnimator.SetBool("Visible", true);
			this.InstructionsLabel.text = "Press E for money";
		}
	}

	private IEnumerator Interact()
	{
		Interaction interaction = Level.Instance.GetInteraction(this.position, this.direction);
		if (interaction == null)
		{
			yield break;
		}

		if (this.patience <= 0)
		{
			this.PlaySound(this.ErrorSounds);
			this.PatienceAnimator.Play("Error", 0, 0);
			yield break;
		}

		if (interaction is Booth booth)
		{
			if (booth.IsConnorBooth)
			{
				yield return this.GetPatience(booth);
			}
			else
			{
				if (this.boothsBought.Contains(booth))
				{
				}
				else if (this.money < booth.PlushieData.BasePrice)
				{
					this.PlaySound(this.ErrorSounds);
					this.MoneyAnimator.Play("Error", 1, 0);
				}
				else
				{
					yield return this.BuyPlushie(booth);
				}
			}
		}
		else if (interaction is ATM atm)
		{
			yield return this.GetMoney(atm);
		}
	}

	private IEnumerator BuyPlushie(Booth booth)
	{
		this.InstructionsAnimator.SetBool("Visible", false);

		this.isInteracting = true;

		this.GameUIAnimator.Play("PurchasePlushie");
		this.PersuasionPlushieSprite.sprite = booth.PlushieData.Sprite;

		List<PersuasionAction> actionsCache = new List<PersuasionAction>(this.PersuasionActions);
		List<RectTransform> anchorsCache = new List<RectTransform>(this.PersuasionAnchors);
		List<PersuasionAction> activeActions = new List<PersuasionAction>();

		this.persuasion = 0;
		this.RefreshPersuasion();

		yield return new WaitForSeconds(2);

		while (this.patience > 0 && this.patience + this.persuasion < 1)
		{
			yield return new WaitForSeconds(1f);

			this.RefreshPersuasionActions.gameObject.SetActive(false);

			// Disable previous actions.
			for (int i = 0; i < activeActions.Count; i++)
			{
				activeActions[i].gameObject.SetActive(false);
			}

			activeActions.Clear();

			this.clickedPersuasionAction = null;

			// Spawn a few actions.
			int count = Random.Range(2, 5);
			this.RefillPoolIFN(actionsCache, this.PersuasionActions, count);
			this.RefillPoolIFN(anchorsCache, this.PersuasionAnchors, count);
			for (int i = 0; i < count; i++)
			{
				yield return new WaitForSeconds(0.1f);

				PersuasionAction action = this.PickWithPool(actionsCache);
				RectTransform anchor = this.PickWithPool(anchorsCache);

				action.GetComponent<RectTransform>().anchoredPosition = anchor.anchoredPosition + Random.insideUnitCircle * 20;
				action.gameObject.SetActive(true);

				activeActions.Add(action);

				this.PlaySound(this.DisplayUISounds);
			}

			this.RefreshPersuasionActions.gameObject.SetActive(true);

			// Wait for player to select one or 5 seconds, decrementing patience over time.
			float elapsedTime = 0f;
			while (elapsedTime < 5 && this.clickedPersuasionAction == null && this.patience > 0 && this.patience + this.persuasion < 1)
			{
				this.patience -= Time.deltaTime / 100f;
				this.patience = Mathf.Clamp01(this.patience);
				this.RefreshPatience();

				this.persuasion += Time.deltaTime / 100f * 2;
				this.persuasion = Mathf.Clamp01(this.persuasion);
				this.RefreshPersuasion();

				elapsedTime += Time.deltaTime;
				yield return null;
			}

			// Apply its effects if relevant.
			if (this.clickedPersuasionAction)
			{
				if (Random.Range(0, 100) < this.clickedPersuasionAction.AltChance)
				{
					this.ApplyPersuasion(this.clickedPersuasionAction.AltPatienceChange, this.clickedPersuasionAction.AltPersuasionChange);
					this.PortraitsAnimator.Play(this.clickedPersuasionAction.AltAnimation);
				}
				else
				{
					this.ApplyPersuasion(this.clickedPersuasionAction.PatienceChange, this.clickedPersuasionAction.PersuasionChange);
					this.PortraitsAnimator.Play(this.clickedPersuasionAction.Animation);
				}
			}

			this.RefreshPersuasionActions.Animator.Play("Disappear");

			// Despawn all actions.
			for (int i = 0; i < count; i++)
			{
				activeActions[i].Animator.Play("Disappear");
				yield return new WaitForSeconds(0.1f);
				this.PlaySound(this.HideUISounds);
			}
		}

		if (this.patience + this.persuasion >= 1)
		{
			// Acquire plushie!
			this.boothsBought.Add(booth);
			this.PlushiesLabel.text = $"{this.boothsBought.Count}/{Level.Instance.PlushieCount}";
			this.PlushiesAnimator.Play("Tick", 0, 0f);

			if (!booth.PlushieData.HasBeenFound)
			{
				booth.PlushieData.HasBeenFound = true;
				PlayerPrefs.SetInt("Found" + booth.PlushieData.name, 1);

				this.CheckForAllPlushiesFoundAchievement();
			}

			this.PlaySound(this.PurchaseSounds);

			this.GameUIAnimator.Play("PurchasePlushieSuccess");

			yield return new WaitForSeconds(2);

			this.isInteracting = false;

			yield return new WaitForSeconds(0.5f);

			booth.BuyPlushie();

			// TODO: Smoothly.
			this.PlaySound(this.MoneyBillSounds);
			this.money -= booth.PlushieData.BasePrice;
			this.MoneyLabel.text = $"${this.money}";
			this.MoneyAnimator.Play("Tick", 0, 0f);

			this.patience *= 0.75f;
			this.RefreshPatience();

			if (this.boothsBought.Count == Level.Instance.PlushieCount)
			{
				this.StartCoroutine(this.GameOver());
			}

			this.successfulPersuasionsInARow++;
			if (this.successfulPersuasionsInARow == 5)
			{
				this.UnlockAchievement(Achievement.PersuasionInARow);
			}
		}
		else if (this.patience <= 0)
		{
			this.PlaySound(this.ErrorSounds);
			this.PatienceAnimator.Play("Error", 0, 0);

			this.GameUIAnimator.Play("PurchasePlushieFailure");

			yield return new WaitForSeconds(2);

			this.isInteracting = false;

			this.successfulPersuasionsInARow = 0;
		}

		this.RefreshPersuasionActions.gameObject.SetActive(false);

		// Despawn all actions.
		for (int i = 0; i < activeActions.Count; i++)
		{
			activeActions[i].gameObject.SetActive(false);
		}
	}

	private void ApplyPersuasion(int patience, int persuasion)
	{
		this.patience += patience / 100f;
		this.patience = Mathf.Clamp01(this.patience);

		this.persuasion += persuasion / 100f;
		this.persuasion = Mathf.Clamp01(this.persuasion);

		this.PersuasionChangeAnimator.Play("Show", 0, 0f);
		if (patience != 0)
		{
			this.PatienceChangeLabel.text = patience.ToString("+0;-0") + "%";
		}
		else
		{
			this.PatienceChangeLabel.text = "";
		}

		if (persuasion != 0)
		{
			this.PersuasionChangeLabel.text = persuasion.ToString("+0;-0") + "%";
		}
		else
		{
			this.PersuasionChangeLabel.text = "";
		}

		this.RefreshPatience();
		this.RefreshPersuasion();
	}

	private IEnumerator GetPatience(Booth booth)
	{
		this.isInteracting = true;

		this.InstructionsAnimator.SetBool("Visible", false);

		while (this.patience < 1 && this.remainingTime > 0)
		{
			this.patience += Time.deltaTime / 10 * this.PatienceGainCurve.Evaluate(this.patience);
			this.patience = Mathf.Clamp01(this.patience);
			this.RefreshPatience();

			yield return null;

			if (Input.GetButtonDown("Horizontal")
				|| Input.GetButtonDown("Vertical")
				|| Input.GetKeyDown(KeyCode.Escape))
			{
				break;
			}
		}

		this.isInteracting = false;
	}

	private IEnumerator GetMoney(ATM atm)
	{
		this.isInteracting = true;

		this.InstructionsAnimator.SetBool("Visible", false);

		this.PlaySound(this.ATMSounds);

		float elapsedTime = 0f;
		int billIndex = 2;
		int billsToNextPatience = 0;
		while (this.patience > 0 && this.remainingTime > 0)
		{
			elapsedTime += Time.deltaTime;
			int billsGiven = 0;
			while (elapsedTime > 2f / (billIndex + billsGiven))
			{
				elapsedTime -= 2f / (billIndex + billsGiven);
				billsGiven++;
			}

			if (billsGiven > 0)
			{
				this.money += billsGiven;
				this.MoneyLabel.text = $"${this.money}";
				this.MoneyAnimator.Play("Tick", 0, 0f);
				atm.ParticleSystem.Emit(billsGiven);

				if (billIndex < 30)
				{
					this.PlaySound(this.MoneyBillSounds, 1, this.ATMSource);
				}
				else if (billIndex == 30)
				{
					this.ATMSource.Play();
				}

				this.ATMSource.pitch = Mathf.Min(1 + billsGiven / 100f, 1.2f);

				billIndex += billsGiven;

				billsToNextPatience -= billsGiven;
				if (billsToNextPatience <= 0)
				{
					billsToNextPatience += 50;
					this.patience -= 0.05f;
					this.RefreshPatience();

					if (this.patience <= 0)
					{
						this.patience = 0;
						this.RefreshPatience();
						break;
					}
				}
			}

			yield return null;

			if (Input.GetButtonDown("Horizontal")
				|| Input.GetButtonDown("Vertical")
				|| Input.GetButtonDown("Interact")
				|| Input.GetKeyDown(KeyCode.Escape))
			{
				break;
			}
		}

		this.ATMSource.Stop();

		this.isInteracting = false;
	}

	private void RefillPoolIFN<T>(List<T> pool, T[] array, int requiredCount)
	{
		if (pool.Count < requiredCount)
		{
			pool.Clear();
			pool.AddRange(array);
		}
	}

	private T PickWithPool<T>(List<T> pool)
	{
		int index = Random.Range(0, pool.Count);
		T item = pool[index];
		pool.RemoveAt(index);
		return item;
	}

	private void PlaySound(AudioClip[] clips, float volume = 1f, AudioSource sourceOverride = null)
	{
		if (clips == null || clips.Length == 0)
		{
			return;
		}

		(sourceOverride ?? this.AudioSource).PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
	}

	private IEnumerator WaitForSecondsOrAnyKey(float time)
	{
		if (this.isFirstTime)
		{
			yield return new WaitForSeconds(time);
			yield break;
		}

		float elapsedTime = 0f;
		while (elapsedTime < time)
		{
			if (Time.timeScale != 0 && (Input.anyKeyDown || this.skipIntro))
			{
				this.skipIntro = true;
				break;
			}

			elapsedTime += Time.deltaTime;
			yield return null;
		}
	}

	private bool UnlockAchievement(Achievement achievement)
	{
		if (PlayerPrefs.GetInt(achievement.ToString(), 0) == 1)
		{
			return false;
		}

		this.AchievementFeedbacks[(int)achievement].SetActive(true);

		PlayerPrefs.SetInt(achievement.ToString(), 1);
		return true;
	}

	private void CheckForAllPlushiesFoundAchievement()
	{
		bool allNormalPlushiesFound = true;
		foreach (PlushieData plushie in Level.Instance.NormalPlushies)
		{
			if (!plushie.HasBeenFound)
			{
				allNormalPlushiesFound = false;
			}
		}

		if (allNormalPlushiesFound)
		{
			this.UnlockAchievement(Achievement.AllNormalPlushies);
		}

		bool allRarePlushiesFound = true;
		foreach (PlushieData plushie in Level.Instance.RarePlushies)
		{
			if (!plushie.HasBeenFound)
			{
				allRarePlushiesFound = false;
			}
		}

		if (allRarePlushiesFound)
		{
			this.UnlockAchievement(Achievement.AllRarePlushies);
		}

		bool allBadPlushiesFound = true;
		foreach (PlushieData plushie in Level.Instance.BadPlushies)
		{
			if (!plushie.HasBeenFound)
			{
				allBadPlushiesFound = false;
			}
		}

		if (allBadPlushiesFound)
		{
			this.UnlockAchievement(Achievement.AllBadPlushies);
		}
	}

	public enum Direction
	{
		YPositive,
		XPositive,
		YNegative,
		XNegative,
	}
}
