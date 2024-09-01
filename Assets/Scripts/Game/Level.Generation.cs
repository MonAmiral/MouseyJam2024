using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Level
{
	const int BLOCK_SIZE = 8;
	const int BOOTH_MIN_GROUP = 3;
	const int BOOTH_MAX_GROUP = 4;

	public GenerationSettings CurrentSettings;

	public GenerationSettings[] InfiniteSettings;

	static readonly Vector2Int[] DIRECTIONS =
	{
		new Vector2Int(0,1),
		new Vector2Int(1,0),
		new Vector2Int(0,-1),
		new Vector2Int(-1,0),
	};

	[Header("Generation")]
	public int Seed;

	[Space]
	public Transform BoothsContainer;
	public Booth[] EmptyBoothPrefabs, PlushieBoothPrefabs, ConnorBoothPrefabs;
	public Booth VChiBanBooth;

	[Space]
	public Transform ATMsContainer;
	public ATM ATMPrefab;

	[Space]
	public Transform WallsContainer;
	public Transform[] BoundsWallPrefabs;

	private List<Booth> booths = new List<Booth>();
	private List<Transform> walls = new List<Transform>();
	private Dictionary<Vector2Int, Booth> interactiveBooths = new Dictionary<Vector2Int, Booth>();
	private Dictionary<Vector2Int, ATM> ATMs = new Dictionary<Vector2Int, ATM>();

	private List<Vector2Int> ignoredNeighbours = new List<Vector2Int>();

	[System.NonSerialized]
	public Booth BountyBooth;
	[System.NonSerialized]
	public float BountyMultiplier;

	[Space]
	public PlushieData[] NormalPlushies;
	public PlushieData[] RarePlushies;
	public PlushieData[] BadPlushies;

	[System.NonSerialized]
	public int PlushieCount;

	private System.Diagnostics.Stopwatch stopwatch;

	[Header("UI")]
	public TMPro.TextMeshProUGUI GenerationLabel;
	public RectTransform GenerationGauge;
	public Animator GenerationScreen;
	public UnityEngine.UI.Image LoadingCircle;
	public TMPro.TextMeshProUGUI TipsLabel;
	[TextArea]
	public string[] Tips;

	[Space]
	public Transform MinimapContainer;
	public RectTransform MinimapBlockPrefab;
	public MinimapBooth MinimapBoothPrefab;
	public MinimapBooth MinimapConnorBoothPrefab;
	public MinimapBooth MinimapPlushieBoothPrefab;
	public RectTransform MinimapATMPrefab;

	private IEnumerator Generate(int seed)
	{
		int tipIndex = (PlayerPrefs.GetInt("Tip", -1) + 1) % this.Tips.Length;
		PlayerPrefs.SetInt("Tip", tipIndex);
		this.TipsLabel.text = this.Tips[tipIndex];

		this.LoadingCircle.sprite = this.NormalPlushies[Random.Range(0, this.NormalPlushies.Length)].SilhouetteSprite;

		yield return null;

		Debug.Log($"Generating with seed {seed} for level {Level.InfiniteLevel}");

		this.FetchCurrentSettings();

		PlayerController.Instance.LockForGeneration(Level.InfiniteScore, Level.InfiniteTimeBonus);

		System.Random random = new System.Random(seed);
		this.stopwatch = new System.Diagnostics.Stopwatch();
		this.stopwatch.Start();

		// Build the cells list: BLOCK_SIZE x (BLOCK_COUNT_X ; BLOCK_COUNT_Y) or BLOCK_SIZE x (BLOCK_COUNT_Y ; BLOCK_COUNT_X), with some completely filled with Cell.OutOfBounds.
		yield return this.GenerateGlobalShape(random);

		this.GenerationGauge.anchorMax = new Vector2(0.1f, 1);
		yield return this.SkipBetweenSteps("Creating entrance");

		// Place the entrance at the bottom center of the bottomleftmost block.
		yield return this.CreateEntrance(random);

		this.GenerationGauge.anchorMax = new Vector2(0.15f, 1);
		yield return this.SkipBetweenSteps("Placing ATMs");

		// Place one ATM in each block, against a wall.
		yield return this.PlaceATMs(random);

		this.GenerationGauge.anchorMax = new Vector2(0.2f, 1);
		yield return this.SkipBetweenSteps("Placing booths");

		// Fill the list with groups of up to BOOTH_MAX_GROUP booths (Cell.BoothEmpty) which won't be in contact with other groups.
		yield return this.PlaceBooths(random);

		this.GenerationGauge.anchorMax = new Vector2(0.3f, 1);
		yield return this.SkipBetweenSteps("Making booths interactive");

		// Make some of these booths interactive.
		yield return this.MakeBoothsInteractive(random);

		this.GenerationGauge.anchorMax = new Vector2(0.4f, 1);
		yield return this.SkipBetweenSteps("Populating level");

		// Populate each cell: booth prefabs, particles for empty...
		yield return this.PopulateLevel(random);

		this.GenerationGauge.anchorMax = new Vector2(0.65f, 1);
		yield return this.SkipBetweenSteps("Spawning walls");

		// Spawn big fokin walls for the edges and out of bounds areas.
		yield return this.SpawnBounds(random);

		this.GenerationGauge.anchorMax = new Vector2(0.75f, 1);
		yield return this.SkipBetweenSteps("Generating minimap");

		// Use existing data to generate the minimap in UI.
		yield return this.GenerateMinimap(random);

		// Do some stuff that's done at the end.
		yield return this.Finalize(random);

		// Done!
		this.GenerationGauge.anchorMax = Vector2.one;
		this.GenerationLabel.text = "Done!";
		yield return new WaitForSeconds(1);
		this.GenerationScreen.Play("Hide");
		PlayerController.Instance.FreeFromGeneration();
	}

	private void FetchCurrentSettings()
	{
		if (Level.InfiniteLevel == -1)
		{
			Level.InfiniteScore = 0;
			Level.InfiniteTimeBonus = 0;
		}
		else
		{
			if (Level.InfiniteLevel < this.InfiniteSettings.Length)
			{
				this.CurrentSettings = this.InfiniteSettings[Level.InfiniteLevel];
			}
			else
			{
				// Remove 30 seconds per additional level. Good luck surviving with only 30 seconds, gamer.
				this.CurrentSettings = this.InfiniteSettings[this.InfiniteSettings.Length - 1];
				int excessLevels = 1 + Level.InfiniteLevel - this.InfiniteSettings.Length;
				Level.InfiniteTimeBonus = Mathf.Min(Level.InfiniteTimeBonus - excessLevels * 30, 150);
			}
		}
	}

	private IEnumerator GenerateGlobalShape(System.Random random)
	{
		// Start with 6 blocks of BLOCK_SIZE x BLOCK_SIZE cells.
		// Delete one of them, and maybe another.
		int block1X = random.Next(0, this.CurrentSettings.BlockCountX);
		int block1Y = random.Next(0, this.CurrentSettings.BlockCountY);
		int block2X, block2Y;

		if (block1Y == 1)
		{
			// Middle row: delete a block in the same column, on row 0 or BLOCK_COUNT_X. row 1 can happen, it'll make a C or inverted C shape.
			block2X = block1X;
			block2Y = random.Next(0, this.CurrentSettings.BlockCountY);
		}
		else
		{
			// Top or bottom row: delete a neighboring block, either up-down or left-right. If it's out of bounds it'll just be ignored.
			if (random.Next(100) > 50)
			{
				block2X = Mathf.Clamp(block1X + random.Next(-1, 2), 0, this.CurrentSettings.BlockCountX - 1);
				block2Y = block1Y;
			}
			else
			{
				block2X = block1X;
				block2Y = Mathf.Clamp(block1Y + random.Next(-1, 2), 0, this.CurrentSettings.BlockCountY - 1);
			}
		}

		// Transform these blocks into the cell grid.
		// It can be BLOCK_COUNT_X x BLOCK_COUNT_Y or BLOCK_COUNT_Y x BLOCK_COUNT_X x BLOCK_SIZE depending on random orientation.
		if (random.Next(100) > 50)
		{
			this.cells = new Cell[this.CurrentSettings.BlockCountX * BLOCK_SIZE, this.CurrentSettings.BlockCountY * BLOCK_SIZE];
		}
		else
		{
			this.cells = new Cell[this.CurrentSettings.BlockCountY * BLOCK_SIZE, this.CurrentSettings.BlockCountX * BLOCK_SIZE];

			// Rotate the block info in this case.
			int swapInfo = block1X;
			block1X = block1Y;
			block1Y = swapInfo;

			swapInfo = block2X;
			block2X = block2Y;
			block2Y = swapInfo;
		}

		this.dimensions = new Vector2Int(this.cells.GetLength(0), this.cells.GetLength(1));

		Debug.DrawLine(new Vector3(-0.5f, 0, -0.5f), new Vector3(this.dimensions.x - 0.5f, 0, -0.5f), Color.green, 10);
		Debug.DrawLine(new Vector3(this.dimensions.x - 0.5f, 0, -0.5f), new Vector3(this.dimensions.x - 0.5f, 0, this.dimensions.y - 0.5f), Color.green, 10);
		Debug.DrawLine(new Vector3(this.dimensions.x - 0.5f, 0, this.dimensions.y - 0.5f), new Vector3(-0.5f, 0, this.dimensions.y - 0.5f), Color.green, 10);
		Debug.DrawLine(new Vector3(-0.5f, 0, this.dimensions.y - 0.5f), new Vector3(-0.5f, 0, -0.5f), Color.green, 10);

		// Carve the holes. Second line is redundant if coordinates match but that's a minuscule unoptimized cost.
		for (int x = 0; x < BLOCK_SIZE; x++)
		{
			for (int y = 0; y < BLOCK_SIZE; y++)
			{
				this.cells[block1X * BLOCK_SIZE + x, block1Y * BLOCK_SIZE + y] = Cell.OutOfBounds;
				this.cells[block2X * BLOCK_SIZE + x, block2Y * BLOCK_SIZE + y] = Cell.OutOfBounds;

				this.DrawCross(block1X * BLOCK_SIZE + x, block1Y * BLOCK_SIZE + y, Color.black);
				this.DrawCross(block2X * BLOCK_SIZE + x, block2Y * BLOCK_SIZE + y, Color.black);
			}
		}

		yield break;
	}

	private IEnumerator CreateEntrance(System.Random random)
	{
		for (int y = 0; y < this.dimensions.y; y += BLOCK_SIZE)
		{
			for (int x = BLOCK_SIZE / 2; x < this.dimensions.x; x += BLOCK_SIZE)
			{
				if (this.cells[x, y] != Cell.OutOfBounds)
				{
					this.cells[x, y] = Cell.Entrance;

					yield break;
				}
			}
		}

		Debug.LogError("Couldn't place an entrance anywhere.");
		yield break;
	}

	private IEnumerator PlaceATMs(System.Random random)
	{
		int positionOptions = (BLOCK_SIZE - 2) * 4;

		// Place one ATM in each block, never in a corner.
		for (int blockOriginX = 0; blockOriginX < this.dimensions.x; blockOriginX += BLOCK_SIZE)
		{
			for (int blockOriginY = 0; blockOriginY < this.dimensions.y; blockOriginY += BLOCK_SIZE)
			{
				if (this.cells[blockOriginX, blockOriginY] == Cell.OutOfBounds)
				{
					continue;
				}

				int startPosition = random.Next(positionOptions);
				for (int i = 0; i < positionOptions; i++)
				{
					int positionIndex = (i + startPosition) % positionOptions;

					Vector2Int position;
					Vector2Int wallPosition;

					if (positionIndex < (BLOCK_SIZE - 2) * 1)
					{
						// North.
						int xInBlock = 1 + positionIndex;
						position = new Vector2Int(blockOriginX + xInBlock, blockOriginY + BLOCK_SIZE - 1);
						wallPosition = position + Vector2Int.up;
					}
					else if (positionIndex < (BLOCK_SIZE - 2) * 2)
					{
						// South.
						int xInBlock = 1 + positionIndex - (BLOCK_SIZE - 2) * 1;
						position = new Vector2Int(blockOriginX + xInBlock, blockOriginY);
						wallPosition = position + Vector2Int.down;
					}
					else if (positionIndex < (BLOCK_SIZE - 2) * 3)
					{
						// East.
						int yInBlock = 1 + positionIndex - (BLOCK_SIZE - 2) * 2;
						position = new Vector2Int(blockOriginX + BLOCK_SIZE - 1, blockOriginY + yInBlock);
						wallPosition = position + Vector2Int.right;
					}
					else
					{
						// West.
						int yInBlock = 1 + positionIndex - (BLOCK_SIZE - 2) * 3;
						position = new Vector2Int(blockOriginX, blockOriginY + yInBlock);
						wallPosition = position + Vector2Int.left;
					}

					if (this.cells[position.x, position.y] != Cell.Empty)
					{
						continue;
					}

					if (this.IsWithinBounds(wallPosition.x, wallPosition.y))
					{
						continue;
					}

					this.cells[position.x, position.y] = Cell.ATM;
					break;
				}
			}
		}

		yield break;
	}

	private IEnumerator PlaceBooths(System.Random random)
	{
		string skipText = "Placing booths";

		// Add small islands in every concave corner of the map to avoid booths being unattainable.
		for (int x = 0; x < this.dimensions.x; x += BLOCK_SIZE)
		{
			for (int y = 0; y < this.dimensions.y; y += BLOCK_SIZE)
			{
				if (this.cells[x, y] == Cell.OutOfBounds)
				{
					continue;
				}

				// West-east.
				if (x == 0 || this.cells[x - 1, y] == Cell.OutOfBounds)
				{
					// South-north.
					if (y == 0 || this.cells[x, y - 1] == Cell.OutOfBounds)
					{
						this.PlaceBoothIsland(x, y, 2, random);
					}

					if (y == this.dimensions.y - BLOCK_SIZE || this.cells[x, y + BLOCK_SIZE] == Cell.OutOfBounds)
					{
						this.PlaceBoothIsland(x, y + BLOCK_SIZE - 1, 2, random);
					}
				}

				if (x == this.dimensions.x - BLOCK_SIZE || this.cells[x + BLOCK_SIZE, y] == Cell.OutOfBounds)
				{
					// South-north.
					if (y == 0 || this.cells[x, y - 1] == Cell.OutOfBounds)
					{
						this.PlaceBoothIsland(x + BLOCK_SIZE - 1, y, 2, random);
					}

					if (y == this.dimensions.y - BLOCK_SIZE || this.cells[x, y + BLOCK_SIZE] == Cell.OutOfBounds)
					{
						this.PlaceBoothIsland(x + BLOCK_SIZE - 1, y + BLOCK_SIZE - 1, 2, random);
					}
				}

				yield return this.SkipFrameIfStopwatchFull(skipText);
			}
		}

		// Sprinkle islands everywhere, starting from the center of the map.
		for (int rawX = 0; rawX < this.dimensions.x; rawX++)
		{
			for (int rawY = 0; rawY < this.dimensions.y; rawY++)
			{
				// Start from the center instead of the edges to have a more chaotic outside.
				int x = (rawX + this.dimensions.x / 2) % this.dimensions.x;
				int y = (rawY + this.dimensions.y / 2) % this.dimensions.y;

				// Check if suitable.
				if (!this.CanPlaceBooth(x, y, null))
				{
					continue;
				}

				int islandSize = random.Next(BOOTH_MIN_GROUP, BOOTH_MAX_GROUP + 1);
				this.PlaceBoothIsland(x, y, islandSize, random);

				yield return this.SkipFrameIfStopwatchFull(skipText);
			}
		}

		yield break;
	}

	private void PlaceBoothIsland(int x, int y, int islandSize, System.Random random)
	{
		// Start carving.
		this.ignoredNeighbours.Clear();
		this.cells[x, y] = Cell.BoothEmpty;

		Color debugColor = Random.ColorHSV(0, 1, 1, 1, 1, 1);
		this.DrawCross(x, y, debugColor);

		for (int groupCellIndex = 1; groupCellIndex < islandSize; groupCellIndex++)
		{
			this.ignoredNeighbours.Add(new Vector2Int(x, y));

			bool added = false;
			int directionOffset = random.Next(DIRECTIONS.Length);
			for (int directionIteration = 0; directionIteration < DIRECTIONS.Length; directionIteration++)
			{
				int directionIndex = (directionOffset + directionIteration) % DIRECTIONS.Length;
				Vector2Int direction = DIRECTIONS[directionIndex];
				Vector2Int neighbour = new Vector2Int(x, y) + direction;

				if (!this.CanPlaceBooth(neighbour.x, neighbour.y, this.ignoredNeighbours))
				{
					continue;
				}

				x = neighbour.x;
				y = neighbour.y;
				this.cells[x, y] = Cell.BoothEmpty;
				added = true;
				this.DrawCross(x, y, debugColor);
			}

			// Couldn't add a new booth next to the most recent one, ending this group early.
			if (!added)
			{
				break;
			}
		}
	}

	private IEnumerator MakeBoothsInteractive(System.Random random)
	{
		int plushieBoothCount = random.Next(this.CurrentSettings.MinPlushiesCount, this.CurrentSettings.MaxPlushiesCount + 1);
		int connorBoothCount = random.Next(this.CurrentSettings.MinConnorCount, this.CurrentSettings.MaxConnorCount + 1);

		this.PlushieCount = plushieBoothCount;

		List<Vector2Int> corners = new List<Vector2Int>();

		for (int boothIndex = 0; boothIndex < plushieBoothCount + connorBoothCount; boothIndex++)
		{
			bool isPlushie = boothIndex < plushieBoothCount;

			// Make a pool of blocks which can be picked from for booths.
			if (corners.Count <= 0)
			{
				for (int x = 0; x < this.dimensions.x; x += BLOCK_SIZE)
				{
					for (int y = 0; y < this.dimensions.y; y += BLOCK_SIZE)
					{
						if (this.cells[x, y] == Cell.OutOfBounds)
						{
							continue;
						}

						corners.Add(new Vector2Int(x, y));
					}
				}
			}

			// Pick a random block, and then a random position inside this block.
			Vector2Int corner = this.PickAndRemove(corners, random);

			Vector2Int searchStartPosition = corner + new Vector2Int(random.Next(BLOCK_SIZE), random.Next(BLOCK_SIZE));

			if (this.cells[searchStartPosition.x, searchStartPosition.y] == Cell.BoothEmpty)
			{
				this.cells[searchStartPosition.x, searchStartPosition.y] = isPlushie ? Cell.BoothPlushie : Cell.BoothConnor;
				continue;
			}

			// Grow from this random position until an empty booth is found.
			bool added = false;
			for (int distance = 1; distance < BLOCK_SIZE; distance++)
			{
				for (int xOffset = 0; xOffset <= distance; xOffset++)
				{
					int yOffset = distance - xOffset;

					if (this.IsCellOfType(searchStartPosition.x + xOffset, searchStartPosition.y + yOffset, Cell.BoothEmpty))
					{
						this.cells[searchStartPosition.x + xOffset, searchStartPosition.y + yOffset] = isPlushie ? Cell.BoothPlushie : Cell.BoothConnor;
						added = true;
						break;
					}

					if (xOffset != 0)
					{
						if (this.IsCellOfType(searchStartPosition.x - xOffset, searchStartPosition.y + yOffset, Cell.BoothEmpty))
						{
							this.cells[searchStartPosition.x - xOffset, searchStartPosition.y + yOffset] = isPlushie ? Cell.BoothPlushie : Cell.BoothConnor;
							added = true;
							break;
						}
					}

					if (yOffset != 0)
					{
						if (this.IsCellOfType(searchStartPosition.x + xOffset, searchStartPosition.y - yOffset, Cell.BoothEmpty))
						{
							this.cells[searchStartPosition.x + xOffset, searchStartPosition.y - yOffset] = isPlushie ? Cell.BoothPlushie : Cell.BoothConnor;
							added = true;
							break;
						}
					}

					if (xOffset != 0 && yOffset != 0)
					{
						if (this.IsCellOfType(searchStartPosition.x - xOffset, searchStartPosition.y - yOffset, Cell.BoothEmpty))
						{
							this.cells[searchStartPosition.x - xOffset, searchStartPosition.y - yOffset] = isPlushie ? Cell.BoothPlushie : Cell.BoothConnor;
							added = true;
							break;
						}
					}
				}

				if (added)
				{
					break;
				}
			}
		}

		yield break;
	}

	private IEnumerator PopulateLevel(System.Random random)
	{
		string skipText = "Populating level";

		// Build plushies list: 1 rare, 1 bad, and all the rest are normal.
		List<PlushieData> plushies = new List<PlushieData>(this.PlushieCount);
		plushies.AddRange(this.NormalPlushies);
		while (plushies.Count > this.PlushieCount - 2)
		{
			plushies.RemoveAt(random.Next(plushies.Count));
		}

		plushies.Add(this.Pick(this.RarePlushies, random));
		plushies.Add(this.Pick(this.BadPlushies, random));

		Booth.CassAlreadyAppeared = false;

		for (int x = 0; x < this.dimensions.x; x++)
		{
			for (int y = 0; y < this.dimensions.y; y++)
			{
				Vector3 position = new Vector3(x, 0, y);
				Quaternion randomRotation = Quaternion.Euler(0, random.Next(4) * 90, 0);

				switch (this.cells[x, y])
				{
					case Cell.Empty:
						// Spawn crowd and random flooring.
						this.Particles.position = new Vector3(x, 0, y);
						this.CrowdGenerator.Emit(Random.Range(0, 4));
						this.FloorGenerator.Emit(Random.Range(0, 2));

						break;

					case Cell.BoothEmpty:
						// Spawn a useless booth.
						Booth booth = GameObject.Instantiate(this.Pick(this.EmptyBoothPrefabs, random), position, randomRotation, this.BoothsContainer);
						this.booths.Add(booth);
						booth.Randomize(random, this.IsCellOfType(x, y + 1, Cell.Empty), this.IsCellOfType(x, y - 1, Cell.Empty), this.IsCellOfType(x - 1, y, Cell.Empty), this.IsCellOfType(x + 1, y, Cell.Empty));
						break;

					case Cell.BoothPlushie:
						// Spawn a booth with a plushie in it.
						Booth boothPrefab = this.Pick(this.PlushieBoothPrefabs, random);
						PlushieData plushie = this.PickAndRemove(plushies, random);

						// Guaranteed to be the vchiban booth the first time we're spawning a vchiban plush.
						if (plushie.IsVChiBan && this.VChiBanBooth != null)
						{
							boothPrefab = this.VChiBanBooth;
							this.VChiBanBooth = null;
						}

						Booth plushieBooth = GameObject.Instantiate(boothPrefab, position, Quaternion.identity, this.BoothsContainer);
						this.booths.Add(plushieBooth);
						plushieBooth.Randomize(random, this.IsCellOfType(x, y + 1, Cell.Empty), this.IsCellOfType(x, y - 1, Cell.Empty), this.IsCellOfType(x - 1, y, Cell.Empty), this.IsCellOfType(x + 1, y, Cell.Empty));

						this.interactiveBooths.Add(new Vector2Int(x, y), plushieBooth);
						plushieBooth.BindPlushie(plushie);

						// Bounty is always the last one, it's easy that way.
						this.BountyBooth = plushieBooth;

						break;

					case Cell.BoothConnor:
						// Spawn a booth which restores patience.
						Booth connorBooth = GameObject.Instantiate(this.Pick(this.ConnorBoothPrefabs, random), position, randomRotation, this.BoothsContainer);
						this.booths.Add(connorBooth);
						connorBooth.Randomize(random, this.IsCellOfType(x, y + 1, Cell.Empty), this.IsCellOfType(x, y - 1, Cell.Empty), this.IsCellOfType(x - 1, y, Cell.Empty), this.IsCellOfType(x + 1, y, Cell.Empty));

						this.interactiveBooths.Add(new Vector2Int(x, y), connorBooth);

						break;

					case Cell.Entrance:
						// Position the player.
						PlayerController.Instance.transform.position = position;
						PlayerController.Instance.transform.rotation = Quaternion.identity;

						// Spawn the entrance visuals?

						break;

					case Cell.ATM:
						// Find which wall is adjacent.
						Quaternion rotation;
						PlayerController.Direction direction;
						if (!this.IsWithinBounds(x, y + 1))
						{
							rotation = Quaternion.Euler(0, 0, 0);
							direction = PlayerController.Direction.YPositive;
						}
						else if (!this.IsWithinBounds(x, y - 1))
						{
							rotation = Quaternion.Euler(0, 180, 0);
							direction = PlayerController.Direction.YNegative;
						}
						else if (!this.IsWithinBounds(x + 1, y))
						{
							rotation = Quaternion.Euler(0, 90, 0);
							direction = PlayerController.Direction.XPositive;
						}
						else
						{
							rotation = Quaternion.Euler(0, -90, 0);
							direction = PlayerController.Direction.XNegative;
						}

						ATM atm = GameObject.Instantiate(this.ATMPrefab, position, rotation, this.ATMsContainer);
						atm.InteractionDirection = direction;
						this.ATMs.Add(new Vector2Int(x, y), atm);

						break;

					case Cell.OutOfBounds:
						// Out of bounds, nothing to do.
						break;
				}

				yield return this.SkipFrameIfStopwatchFull(skipText);
			}
		}

		yield break;
	}

	private IEnumerator SpawnBounds(System.Random random)
	{
		string skipText = "Populating level";

		for (int x = 0; x < this.dimensions.x; x += BLOCK_SIZE)
		{
			for (int y = 0; y < this.dimensions.y; y += BLOCK_SIZE)
			{
				if (this.cells[x, y] == Cell.OutOfBounds)
				{
					continue;
				}

				Transform prefab = this.Pick(this.BoundsWallPrefabs, random);

				// Left wall.
				if (x == 0 || this.cells[x - 1, y] == Cell.OutOfBounds)
				{
					int prefabIndex = (this.IsWithinBounds(x, y - BLOCK_SIZE) ? 1 : 0) + (this.IsWithinBounds(x, y + BLOCK_SIZE) ? 2 : 0);
					Transform wall = GameObject.Instantiate(this.BoundsWallPrefabs[prefabIndex], new Vector3(x, 0, y), Quaternion.Euler(0, 0, 0), this.WallsContainer);
					this.walls.Add(wall);
				}

				// Right wall.
				if (x == this.dimensions.x - BLOCK_SIZE || this.cells[x + BLOCK_SIZE, y] == Cell.OutOfBounds)
				{
					int prefabIndex = (this.IsWithinBounds(x, y + BLOCK_SIZE) ? 1 : 0) + (this.IsWithinBounds(x, y - BLOCK_SIZE) ? 2 : 0);
					Transform wall = GameObject.Instantiate(this.BoundsWallPrefabs[prefabIndex], new Vector3(x + BLOCK_SIZE - 1, 0, y + BLOCK_SIZE - 1), Quaternion.Euler(0, 180, 0), this.WallsContainer);
					this.walls.Add(wall);
				}

				// Down wall.
				if (y == 0 || this.cells[x, y - 1] == Cell.OutOfBounds)
				{
					int prefabIndex = (this.IsWithinBounds(x + BLOCK_SIZE, y) ? 1 : 0) + (this.IsWithinBounds(x - BLOCK_SIZE, y) ? 2 : 0);
					Transform wall = GameObject.Instantiate(this.BoundsWallPrefabs[prefabIndex], new Vector3(x + BLOCK_SIZE - 1, 0, y), Quaternion.Euler(0, -90, 0), this.WallsContainer);
					this.walls.Add(wall);
				}

				// Up wall.
				if (y == this.dimensions.y - BLOCK_SIZE || this.cells[x, y + BLOCK_SIZE] == Cell.OutOfBounds)
				{
					int prefabIndex = (this.IsWithinBounds(x - BLOCK_SIZE, y) ? 1 : 0) + (this.IsWithinBounds(x + BLOCK_SIZE, y) ? 2 : 0);
					Transform wall = GameObject.Instantiate(this.BoundsWallPrefabs[prefabIndex], new Vector3(x, 0, y + BLOCK_SIZE - 1), Quaternion.Euler(0, 90, 0), this.WallsContainer);
					this.walls.Add(wall);
				}

				yield return this.SkipFrameIfStopwatchFull(skipText);
			}
		}

		yield break;
	}

	private IEnumerator GenerateMinimap(System.Random random)
	{
		string skipText = "Generating minimap";

		// Spawn block background.
		for (int x = 0; x < this.dimensions.x; x += BLOCK_SIZE)
		{
			for (int y = 0; y < this.dimensions.y; y += BLOCK_SIZE)
			{
				if (this.cells[x, y] == Cell.OutOfBounds)
				{
					continue;
				}

				RectTransform block = GameObject.Instantiate(this.MinimapBlockPrefab, this.MinimapContainer);
				block.anchoredPosition = new Vector2(x, y) * 50;

				yield return this.SkipFrameIfStopwatchFull(skipText);
			}
		}

		// Spawn all booths.
		foreach (Booth booth in this.booths)
		{
			Vector2 position = new Vector2(booth.transform.position.x, booth.transform.position.z) * 50;

			if (booth.IsConnorBooth)
			{
				MinimapBooth minimapBooth = GameObject.Instantiate(this.MinimapConnorBoothPrefab, this.MinimapContainer);
				minimapBooth.RectTransform.anchoredPosition = position;
				minimapBooth.BoothImage.sprite = booth.MinimapSprite;
			}
			else if (booth.PlushieData)
			{
				MinimapBooth minimapBooth = GameObject.Instantiate(this.MinimapPlushieBoothPrefab, this.MinimapContainer);
				minimapBooth.RectTransform.anchoredPosition = position;
				minimapBooth.BoothImage.sprite = booth.MinimapSprite;
				minimapBooth.PlushImage.sprite = booth.PlushieData.Sprite;

				booth.MinimapBooth = minimapBooth;
			}
			else
			{
				MinimapBooth minimapBooth = GameObject.Instantiate(this.MinimapBoothPrefab, this.MinimapContainer);
				minimapBooth.RectTransform.anchoredPosition = position;
				minimapBooth.BoothImage.sprite = booth.MinimapSprite;
			}

			yield return this.SkipFrameIfStopwatchFull(skipText);
		}

		// Spawn ATMs.
		foreach (KeyValuePair<Vector2Int, ATM> kvp in this.ATMs)
		{
			RectTransform minimapATM = GameObject.Instantiate(this.MinimapATMPrefab, this.MinimapContainer);
			minimapATM.anchoredPosition = kvp.Key * 50;

			yield return this.SkipFrameIfStopwatchFull(skipText);
		}

		yield break;
	}

	private IEnumerator Finalize(System.Random random)
	{
		int randomValue = random.Next(100);
		if (randomValue == 0)
		{
			this.BountyMultiplier = 10;
		}
		else if (randomValue < 10)
		{
			this.BountyMultiplier = 5;
		}
		else if (randomValue < 50)
		{
			this.BountyMultiplier = 2;
		}
		else
		{
			this.BountyMultiplier = 1.5f;
		}

		yield break;
	}

	private IEnumerator SkipBetweenSteps(string text)
	{
		this.stopwatch.Stop();

		this.GenerationLabel.text = text;
		yield return new WaitForSeconds(0.1f);

		this.stopwatch.Start();
	}

	private IEnumerator SkipFrameIfStopwatchFull(string text)
	{
		if (this.stopwatch.ElapsedMilliseconds > 100)
		{
			this.stopwatch.Stop();
			this.GenerationLabel.text = text;

			yield return null;

			this.stopwatch.Start();
		}
	}

	private bool CanPlaceBooth(int x, int y, List<Vector2Int> ignored)
	{
		if (x < 0 || x >= this.dimensions.x)
		{
			return false;
		}

		if (y < 0 || y >= this.dimensions.y)
		{
			return false;
		}

		if (this.cells[x, y] != Cell.Empty)
		{
			return false;
		}

		for (int offsetX = -1; offsetX <= 1; offsetX++)
		{
			for (int offsetY = -1; offsetY <= 1; offsetY++)
			{
				if (this.IsEmptyOrOutOfBounds(x + offsetX, y + offsetY))
				{
					continue;
				}

				if (ignored == null || !ignored.Contains(new Vector2Int(x + offsetX, y + offsetY)))
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool IsEmptyOrOutOfBounds(int x, int y)
	{
		if (x < 0 || x >= this.dimensions.x)
		{
			return true;
		}

		if (y < 0 || y >= this.dimensions.y)
		{
			return true;
		}

		return this.cells[x, y] == Cell.Empty || this.cells[x, y] == Cell.OutOfBounds;
	}

	private bool IsWithinBounds(int x, int y)
	{
		if (x < 0 || x >= this.dimensions.x)
		{
			return false;
		}

		if (y < 0 || y >= this.dimensions.y)
		{
			return false;
		}

		return this.cells[x, y] != Cell.OutOfBounds;
	}

	private bool IsCellOfType(int x, int y, Cell cellType)
	{
		if (x < 0 || x >= this.dimensions.x)
		{
			return false;
		}

		if (y < 0 || y >= this.dimensions.y)
		{
			return false;
		}

		return this.cells[x, y] == cellType;
	}

	private T PickAndRemove<T>(List<T> list, System.Random random)
	{
		int index = random.Next(list.Count);

		T item = list[index];
		list.RemoveAt(index);

		return item;
	}

	private T Pick<T>(T[] array, System.Random random)
	{
		int index = random.Next(array.Length);
		return array[index];
	}

	private void DrawCross(int x, int y, Color color)
	{
		Debug.DrawLine(new Vector3(x + 0.5f, 0, y + 0.5f), new Vector3(x - 0.5f, 0, y - 0.5f), color, 10f);
		Debug.DrawLine(new Vector3(x - 0.5f, 0, y + 0.5f), new Vector3(x + 0.5f, 0, y - 0.5f), color, 10f);
	}

	[System.Serializable]
	public class GenerationSettings
	{
		public int BlockCountX = 2;
		public int BlockCountY = 3;

		public int MinPlushiesCount = 6;
		public int MaxPlushiesCount = 9;

		public int MinConnorCount = 2;
		public int MaxConnorCount = 4;

		[TextArea]
		public string InfoText;
	}
}