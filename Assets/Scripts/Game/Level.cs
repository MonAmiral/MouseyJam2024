using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Level : MonoBehaviour
{
	public static Level Instance;

	public static int InfiniteLevel = -1;
	public static int InfiniteScore = 0;
	public static float InfiniteTimeBonus = 0;

	[Header("Particles")]
	public Transform Particles;
	public ParticleSystem CrowdGenerator, FloorGenerator;

	private Cell[,] cells = new Cell[,]
	{
		{ Cell.BoothEmpty, Cell.Empty, Cell.Empty, Cell.Empty, Cell.Empty, Cell.Empty,Cell.BoothPlushie},
		{ Cell.Empty, Cell.Empty, Cell.Empty, Cell.BoothEmpty, Cell.Empty, Cell.BoothEmpty,Cell.BoothEmpty},
		{ Cell.Empty, Cell.Empty, Cell.Empty, Cell.BoothEmpty, Cell.Empty, Cell.Empty,Cell.Empty},
		{ Cell.Empty, Cell.Empty, Cell.Empty, Cell.Empty, Cell.Empty, Cell.BoothEmpty,Cell.Empty},
		{ Cell.BoothEmpty, Cell.Empty, Cell.BoothEmpty, Cell.BoothEmpty, Cell.Empty, Cell.BoothEmpty,Cell.Empty},
		{ Cell.Empty, Cell.Empty, Cell.BoothEmpty, Cell.Empty, Cell.Empty, Cell.Empty,Cell.Empty},
	};

	private Vector2Int dimensions;

	private void Awake()
	{
		Instance = this;

		this.dimensions = new Vector2Int(this.cells.GetLength(0), this.cells.GetLength(1));
	}

	private void Start()
	{
		if (this.Seed != 0)
		{
			this.StartCoroutine(this.Generate(this.Seed));
		}
		else
		{
			this.StartCoroutine(this.Generate(Random.Range(int.MinValue, int.MaxValue)));
		}
	}

	public bool CanGo(int x, int y)
	{
		if (x < 0 || x >= this.dimensions.x)
		{
			return false;
		}

		if (y < 0 || y >= this.dimensions.y)
		{
			return false;
		}

		return this.cells[x, y] == Cell.Empty || this.cells[x, y] == Cell.Entrance || this.cells[x, y] == Cell.ATM;
	}

	public Interaction GetInteraction(Vector2Int position, PlayerController.Direction direction)
	{
		if (this.ATMs.TryGetValue(position, out ATM atm) && atm.InteractionDirection == direction)
		{
			return atm;
		}

		position += DIRECTIONS[(int)direction];
		if (this.interactiveBooths.TryGetValue(position, out Booth booth) && booth.InteractionDirection == direction)
		{
			return booth;
		}

		return null;
	}

	private enum Cell
	{
		Empty,

		BoothEmpty,
		BoothPlushie,
		BoothConnor,

		Entrance,
		ATM,

		OutOfBounds,
	}
}
