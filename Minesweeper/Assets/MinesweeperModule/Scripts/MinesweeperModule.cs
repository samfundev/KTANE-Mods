using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using BombInfoExtensions;
using KeepCoding;

public class MinesweeperModule : ModuleScript
{
	public KMBombInfo BombInfo;
	public KMAudio Audio;

	public KMSelectable ModuleSelectable;
	public GameObject ModeToggle;
	public GameObject CellBase;
	public GameObject Grid;
	public GameObject ColorblindLabel;

	public GameObject[] Guides;

	public List<Sprite> Sprites;

	public bool LightOn;
	bool loggedLegend;

	private readonly List<GameObject> colorblindLabels = new List<GameObject>();

	Vector2 GridSize = new Vector2(8, 10);

	internal readonly MSGrid Game = new MSGrid();

	internal class MSGrid
	{
		public List<Cell> Cells = new List<Cell>();
		public List<List<Cell>> Board = new List<List<Cell>>();

		public Cell GetCell(int x, int y)
		{
			return (Board.ElementAtOrDefault(y) != null && Board[y].ElementAtOrDefault(x) != null) ? Board[y][x] : null;
		}

		public bool Solved
		{
			get
			{
				bool onlymines = true;
				foreach (Cell cell in Cells)
				{
					if (!cell.Mine && !cell.Dug)
					{
						onlymines = false;
						break;
					}
				}

				if (onlymines)
				{
					foreach (Cell cell in Cells)
					{
						if (cell.Mine)
						{
							cell.Flagged = true;
							cell.UpdateSprite();
						}
					}
				}

				bool won = true;
				foreach (Cell cell in Cells)
				{
					if (cell.Mine ^ cell.Flagged)
					{
						won = false;
						break;
					}
				}

				return won;
			}
		}
	}

	internal class Cell
	{
		public int _x;
		public int _y;

		public bool Dug;
		public bool Flagged;

		List<Cell> _Around = null;
		public List<Cell> Around
		{
			get
			{
				if (_Around == null)
				{
					_Around = new List<Cell>();
					for (int ox = -1; ox <= 1; ox++)
					{
						for (int oy = -1; oy <= 1; oy++)
						{
							if (ox != 0 || oy != 0)
							{
								Cell adj = _game.GetCell(_x + ox, _y + oy);
								if (adj != null)
								{
									_Around.Add(adj);
								}
							}
						}
					}
				}

				return _Around;
			}
		}

		public bool Mine;
		public int Number;
		public string Color;

		public GameObject _object = null;
		public KMSelectable _selectable = null;
		public KMAudio _audio = null;
		public SpriteRenderer _renderer = null;
		public List<Sprite> _sprites = null;

		public void UpdateSprite()
		{
			string name = "Cover";
			if (Dug)
			{
				name = Mine ? "Incorrect" :
					Number == 0 ? "Empty" :
					Number.ToString();
			}
			else if (Flagged)
			{
				name = "Flagged";
			}

			foreach (Sprite sprite in _sprites)
			{
				if (sprite.name == name)
				{
					_renderer.sprite = sprite;
				}
			}
		}

		public List<Cell> AllDug = new List<Cell>(); // This list is used by the dig animation. It's a bit hacky, but it stores any dug cells after a Dig call.
		public List<Cell> Dig(bool updateSprites = true)
		{
			AllDug.Clear();

			List<Cell> Unused = new List<Cell>();
			if (!Flagged)
			{
				Dug = true;
				AllDug.Add(this);
				if (updateSprites) UpdateSprite();
				if (!Mine)
				{
					if (Number == 0)
					{
						foreach (Cell cell in Around)
						{
							if (!cell.Mine && !cell.Dug)
							{
								Unused.AddRange(cell.Dig(updateSprites));
								AllDug.AddRange(cell.AllDug);
							}
						}
					}
					else
					{
						Unused.Add(this);
					}
				}
			}

			return Unused;
		}

		public IEnumerator AnimatedDig()
		{
			Dig(updateSprites: false);
			foreach (Cell cell in AllDug)
			{
				_audio.PlaySoundAtTransform("Pop-" + Random.Range(1, 4).ToString("D2"), _object.transform);
				cell.UpdateSprite();
				yield return new WaitForSeconds(0.03f);
			}
		}

		public void Click()
		{
			_selectable.OnInteract();
			_selectable.OnInteractEnded();
		}

		readonly MSGrid _game;
		public Cell(MSGrid game, int x, int y, GameObject Object, KMAudio Audio, List<Sprite> Sprites)
		{
			_game = game;
			_x = x;
			_y = y;
			_object = Object;
			_audio = Audio;
			_selectable = Object.GetComponent<KMSelectable>();
			_renderer = Object.transform.Find("Sprite").GetComponent<SpriteRenderer>();
			_sprites = Sprites;
		}
	}

	void UpdateSelectable()
	{
		List<KMSelectable> Children = new List<KMSelectable>();

		if (!Game.Solved)
		{
			foreach (Cell cell in Game.Cells)
			{
				if (StartFound)
				{
					Children.Add(!cell.Dug || (cell.Number > 0 && Digging) ? cell._selectable : null);
				}
				else
				{
					Children.Add(Picks.Contains(cell) ? cell._selectable : null);
				}
			}
		}

		if (StartFound)
		{
			Children.Add(ModeToggle.GetComponent<KMSelectable>());
		}

		foreach (KMSelectable selectable in GetComponentsInChildren<KMSelectable>().Except(new KMSelectable[] { ModuleSelectable }))
		{
			selectable.Highlight.gameObject.SetActive(Children.IndexOf(selectable) > -1);
		}

		ModuleSelectable.Children = Children.ToArray();
		ModuleSelectable.UpdateChildren(null);
	}

	// Helper functions.
	int Mod(int x, int m)
	{
		return (x % m + m) % m;
	}

	internal readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>()
	{
		{"red",    Color.red},
		{"orange", new Color(1, 0.5f, 0)},
		{"yellow", Color.yellow},
		{"green",  Color.green},
		{"blue",   Color.blue},
		{"purple", new Color(0.5f, 0, 0.78f)},
		{"black", Color.black}
	};

	readonly Dictionary<int, string> numToName = new Dictionary<int, string>()
	{
		{5, "red"},
		{2, "orange"},
		{3, "yellow"},
		{1, "green"},
		{6, "blue"},
		{4, "purple"}
	};

	readonly List<string> unpickedNames = new List<string>() {
		"red",
		"orange",
		"yellow",
		"green",
		"blue",
		"purple",
		"black"
	};

	internal Cell StartingCell = null;
	internal bool StartFound = false;
	List<Cell> Picks = null;

	public override void OnActivate()
	{
		pick_colors:
		Picks = new List<Cell>() { StartingCell };
		int total = Random.Range(5, 8);

		for (int i = 0; i < (total - 1); i++)
		{
			Picks.Add(Game.Cells.Except(Picks).ElementAt(Random.Range(0, Game.Cells.Count - Picks.Count)));
		}

		Picks.Sort((Cell x, Cell y) => Game.Cells.IndexOf(x) < Game.Cells.IndexOf(y) ? -1 : 1);

		int digit = BombInfo.GetSerialNumberNumbers().ElementAt(1);
		int number = digit;
		if (number == 0)
		{
			number = 10;
		}
		number = (number - 1) % total;

		int G = total - Picks.IndexOf(StartingCell);
		int S = BombInfo.GetSerialNumberLetters().First() - 64;
		int sol = Mod(G - S - 1, total) + 1;
		if (sol == 7)
		{
			goto pick_colors;
		}

		string solName = numToName[sol]; // This is the solution color's name.
		unpickedNames.Remove(solName);

		foreach (Cell cell in Picks)
		{
			string name;
			if (cell == Picks[number]) name = solName;
			else
			{
				name = unpickedNames[Random.Range(0, unpickedNames.Count)];
				unpickedNames.Remove(name);
			}

			cell._renderer.color = Colors[name];
			cell.Color = name;

			char letter = name == "black" ? 'K' : name[0];
			var label = Instantiate(ColorblindLabel, cell._object.transform).GetComponent<TextMesh>();
			label.text = letter.ToString();
			colorblindLabels.Add(label.gameObject);
		}

		Log("Color order: " + Picks.Select((a) => a.Color).Aggregate((a, b) => a + ", " + b) + ".");
		Log("Second digit of the serial number is {0}.", digit);
		if (digit == 0)
		{
			Log("Which is actually 10 instead of 0.");
		}
		Log("The cell color we need to use is {0} which stands for {1}.", solName, sol);
		Log("The first letter in the serial is {0} which stands for {1}", BombInfo.GetSerialNumberLetters().First(), S);
		Log("The offset from the the bottom right corner is {0}.", ((sol + S) - 1) % total + 1);
		Log("Which makes the starting cell the {0} cell.", StartingCell.Color);

		UpdateSelectable();
	}

	public override void OnColorblindChanged(bool enabled)
	{
		foreach (var label in colorblindLabels)
		{
			label.SetActive(!StartFound && enabled);
		}
	}

	internal IEnumerable TimedAnimation(float length)
	{
		float startTime = Time.time;
		float alpha = 0;
		while (alpha < 1)
		{
			alpha = Mathf.Min((Time.time - startTime) / length, 1);
			yield return alpha;
		}
	}

	internal IEnumerator SolveModule()
	{
		foreach (Cell c in Game.Cells)
		{
			if (!c.Mine)
			{
				c.Dug = true;
				c.UpdateSprite();
			}
		}

		Audio.PlaySoundAtTransform("Solve", transform);
		LogBoard();

		foreach (float alpha in TimedAnimation(1.25f))
		{
			var radius = alpha * 7.7f;
			foreach (Cell cell in Game.Cells)
			{
				cell._renderer.color = Color.Lerp(Color.green, Color.white, Mathf.Abs((new Vector2(cell._x, cell._y) - new Vector2(3.5f, 4.5f)).magnitude - radius) / 2);
			}

			yield return null;
		}

		Solve();
		UpdateSelectable();
	}

	void LogBoard()
	{
		if (!loggedLegend)
		{
			Log("Legend:\n+ - Correct flag\n× - Incorrect flag\n• - Unflagged mine\n■ - Covered cell\nS - A dug up mine");
			loggedLegend = true;
		}

		string board = "Board:";
		for (int y = 0; y < GridSize.y; y++)
		{
			board += "\n";
			for (int x = 0; x < GridSize.x; x++)
			{
				Cell cell = Game.GetCell(x, y);
				if (cell.Flagged)
				{
					if (cell.Mine)
					{
						if (cell.Dug)
						{
							board += "S";
						}
						else
						{
							board += "+";
						}
					}
					else
					{
						board += "×";
					}
				}
				else
				{
					if (cell.Mine)
					{
						board += "•";
					}
					else if (cell.Dug)
					{
						if (cell.Number > 0)
						{
							board += cell.Number;
						}
						else
						{
							board += " ";
						}
					}
					else
					{
						board += "■";
					}
				}
			}
		}

		Log(board);
	}

	IEnumerator HoldCell(Cell cell)
	{
		yield return new WaitForSeconds(0.25f);
		Held = true;
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

		if (StartFound)
		{
			Audio.PlaySoundAtTransform("Flag-" + Random.Range(1, 9).ToString("D2"), transform);
			if (cell.Dug)
			{
				foreach (Cell c in cell.Around)
				{
					if (!c.Dug)
					{
						c.Flagged = true;
						c.UpdateSprite();
					}
				}
			}
			else
			{
				cell.Flagged = !cell.Flagged;
				cell.UpdateSprite();
			}
		}
	}

	internal bool Digging = true;
	bool Held = false;
	Coroutine _playClick = null;

	public void Start()
	{
		Slider = ModeToggle.transform.Find("Slider").gameObject;

		bool motionControls = KeepCoding.Game.KTInputManager.CurrentControlType == KeepCoding.Game.ControlType.Motion;
		foreach (KMSelectable selectable in ModuleSelectable.Children)
		{
			var boxCollider = (BoxCollider) selectable.SelectableColliders[0];
			var size = boxCollider.size;
			size.y = motionControls ? 24 : 1;
			boxCollider.size = size;
		}

		ModeToggle.GetComponent<KMSelectable>().OnInteract = () =>
		{
			Audio.PlaySoundAtTransform("Toggle-" + (Digging ? 1 : 2).ToString("D2"), transform);
			Digging = !Digging;
			targetAlpha = Digging ? 0 : 1;
			UpdateSelectable();

			return false;
		};

		// Generate the cells
		int Total = (int) GridSize.x * (int) GridSize.y;
		int Mines = Mathf.RoundToInt(Total * 0.15f);
		//float scale = 9.5f;

		ModuleSelectable.ChildRowLength = (int) GridSize.x;

		for (int y = 0; y < GridSize.y; y++)
		{
			Game.Board.Insert(y, new List<Cell>());
			for (int x = 0; x < GridSize.x; x++)
			{
				GameObject Cell = Grid.transform.Find(x + " " + y).gameObject;
				//Instantiate(CellBase);
				/*
				Cell.SetActive(true);
				Transform trans = Cell.transform;
				trans.parent = Grid.transform;
				trans.localScale = new Vector3(1 / GridSize.x * scale, 1, 1 / GridSize.y * scale);

				float px = x / GridSize.x * 10 - 5 + (1 / GridSize.x) * 5;
				float pz = y / GridSize.y * -10 + 5 + (1 / GridSize.y) * -5;
				trans.localPosition = new Vector3(px, 0.001f, pz);

				Cell.name = x + " " + y;*/

				Cell cell = new Cell(Game, x, y, Cell, Audio, Sprites);
				Game.Cells.Insert(x + y * (int) GridSize.x, cell);
				Game.Board[y].Insert(x, cell);

				cell._selectable.OnInteract = () =>
				{
					_playClick = StartCoroutine(HoldCell(cell));
					Held = false;

					return false;
				};

				cell._selectable.OnInteractEnded = () =>
				{
					StopCoroutine(_playClick);
					if (!Held)
					{
						if (!StartFound)
						{
							if (Picks.Contains(cell))
							{
								if (cell == StartingCell)
								{
									StartFound = true;
									foreach (Cell c in Game.Cells)
									{
										c._renderer.color = Color.white;
									}

									foreach (var label in colorblindLabels)
									{
										label.SetActive(false);
									}

									StartCoroutine(cell.AnimatedDig());
								}
								else
								{
									Log("Dug the " + cell.Color + " cell instead of " + StartingCell.Color + " for the correct starting cell.");
									Strike();
								}
							}
						}
						else
						{
							if (Digging)
							{
								if (cell.Dug)
								{
									foreach (Cell c in cell.Around)
									{
										if (!c.Dug && !c.Flagged)
										{
											if (c.Mine)
											{
												Log("One of the surrounding cells was actually a mine.");
												c.Dug = true;
												c.Flagged = true;
												c.UpdateSprite();
												Strike();
												LogBoard();
												break;
											}
											else
											{
												StartCoroutine(c.AnimatedDig());
											}
										}
									}
								}
								else if (!cell.Flagged)
								{
									if (cell.Mine)
									{
										Log("A mine was dug!");
										cell.Dug = true;
										cell.Flagged = true;
										cell.UpdateSprite();
										Strike();
										LogBoard();
									}
									else
									{
										StartCoroutine(cell.AnimatedDig());
									}
								}
							}
							else if (!cell.Dug)
							{
								Audio.PlaySoundAtTransform("Flag-" + Random.Range(1, 9).ToString("D2"), transform);
								cell.Flagged = !cell.Flagged;
								cell.UpdateSprite();
							}
						}
					}

					if (Game.Solved)
					{
						StartCoroutine(SolveModule());
					}

					UpdateSelectable();
				};
			}
		}

		int attempts = 0;

		retry:
		attempts++;

		if (attempts == 1000)
		{
			Log("Unable to create a board after 1000 attempts. Automatically solving the module.");
			Solve();
			return;
		}

		// Reset any previous generations
		foreach (Cell cell in Game.Cells)
		{
			cell.Dug = false;
			cell.Mine = false;
			cell.Number = 0;
			cell.Flagged = false;
		}

		List<Cell> NonMines = new List<Cell>(Game.Cells);
		StartingCell = NonMines[Random.Range(0, NonMines.Count)];

		// Help the generator a bit.
		NonMines.Remove(StartingCell);
		foreach (Cell cell in StartingCell.Around)
		{
			NonMines.Remove(cell);
		}

		for (int i = 0; i < Mines; i++)
		{
			int index = Random.Range(0, NonMines.Count);
			Cell mine = NonMines[index];
			mine.Mine = true;
			mine.Number = 0;

			foreach (Cell cell in mine.Around)
			{
				if (!cell.Mine)
				{
					cell.Number++;
				}
			}

			NonMines.RemoveAt(index);
		}

		List<Cell> Unused = new List<Cell>(); // Cells that have a number in them but haven't been used by the solver yet.
		List<Cell> Used = new List<Cell>(); // Store the used cells temporarily until the loop is over.
		List<Cell> UnusedTemp = new List<Cell>(); // Store the new unused cells temporarily until the loop is over.
		Unused.AddRange(StartingCell.Dig());

		bool Changed = true;
		while (Unused.Count > 0 && Changed && !Game.Solved)
		{
			Changed = false;

			foreach (Cell cell in Unused)
			{
				int Flagged = 0;
				int Covered = 0;
				foreach (Cell adj in cell.Around)
				{
					if (!adj.Dug)
					{
						Covered++;
					}

					if (adj.Flagged)
					{
						Flagged++;
					}
				}

				bool DigAll = Flagged == cell.Number;
				bool FlagAll = Covered == cell.Number;
				if (DigAll || FlagAll)
				{
					Changed = true;
					Used.Add(cell);
					foreach (Cell adj in cell.Around)
					{
						if (!adj.Dug)
						{
							if (DigAll)
							{
								UnusedTemp.AddRange(adj.Dig());
							}
							else if (FlagAll)
							{
								adj.Flagged = true;
							}
						}
					}
				}
			}

			foreach (Cell cell in Used)
			{
				Unused.Remove(cell);
			}
			Used.Clear();

			Unused.AddRange(UnusedTemp);
			UnusedTemp.Clear();
		}

		if (Game.Solved)
		{
			foreach (Cell cell in Game.Cells)
			{
				cell.Dug = false;
				cell.Flagged = false;
				cell.UpdateSprite();
			}
		}
		else
		{
			goto retry;
		}
	}

	GameObject Slider = null;
	float sliderAlpha = 0;
	internal float targetAlpha = 0;
	public void Update()
	{
		sliderAlpha = Mathf.Lerp(sliderAlpha, targetAlpha, 0.1f);

		Slider.transform.localPosition = new Vector3(0, 2, -2.5f + 5f * sliderAlpha);
	}
}
