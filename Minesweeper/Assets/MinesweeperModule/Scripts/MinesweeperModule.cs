using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using BombInfoExtensions;

public class MinesweeperModule : MonoBehaviour
{
	public KMBombInfo Bomb;
	public KMBombModule Module;
	public KMAudio Audio;

	public KMSelectable ModuleSelectable;
	public GameObject ModeToggle;
	public GameObject CellBase;
	public GameObject Grid;

	public GameObject[] Guides;

	public List<Sprite> Sprites;

	bool loggedLegend = false;
	static int idCounter = 1;
	int moduleID;

	Vector2 GridSize = new Vector2(8, 10);

	MSGrid Game = new MSGrid();

	class MSGrid
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
					if ((cell.Mine && !cell.Flagged) || (!cell.Mine && cell.Flagged))
					{
						won = false;
						break;
					}
				}

				return won;
			}
		}
	}

	class Cell
	{
		public int _x;
		public int _y;

		bool _Dug;
		public bool Dug
		{
			get { return _Dug; }
			set
			{
				_Dug = value;
			}
		}

		bool _Flagged;
		public bool Flagged
		{
			get { return _Flagged; }
			set
			{
				_Flagged = value;
			}
		}

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
		public KMBombModule _module = null;
		public SpriteRenderer _renderer = null;
		public List<Sprite> _sprites = null;

		public void UpdateSprite()
		{
			string name = "Cover";
			if (_Dug)
			{
				if (Mine)
				{
					name = "Incorrect";
				}
				else if (Number == 0)
				{
					name = "Empty";
				}
				else
				{
					name = Number.ToString();
				}
			}
			else if (_Flagged)
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

		public List<Cell> Dig()
		{
			List<Cell> Unused = new List<Cell>();
			if (!Flagged)
			{
				Dug = true;
				UpdateSprite();
				if (!Mine)
				{
					if (Number == 0)
					{
						foreach (Cell cell in Around)
						{
							if (!cell.Mine && !cell.Dug)
							{
								Unused.AddRange(cell.Dig());
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

		public void Click()
		{
			_selectable.OnInteract();
			_selectable.OnInteractEnded();
		}

		MSGrid _game;
		public Cell(MSGrid game, int x, int y, GameObject Object, KMBombModule Module, List<Sprite> Sprites)
		{
			_game = game;
			_x = x;
			_y = y;
			_object = Object;
			_module = Module;
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

			if (StartFound)
			{
				Children.Add(ModeToggle.GetComponent<KMSelectable>());
			}
		}

		foreach (KMSelectable selectable in GetComponentsInChildren<KMSelectable>().Except(new KMSelectable[] { ModuleSelectable }))
		{
			selectable.Highlight.gameObject.SetActive(Children.IndexOf(selectable) > -1);
		}

		ModuleSelectable.Children = Children.ToArray();
		ModuleSelectable.UpdateChildren(Game.Solved ? ModuleSelectable : null);
	}

	// Helper functions.
	void Log(object data)
	{
		Debug.LogFormat("[Minesweeper #{0}] {1}", new object[] { moduleID, data });
	}

	void Log(object data, params object[] formatting)
	{
		Log(string.Format(data.ToString(), formatting));
	}

	int mod(int x, int m)
	{
		return (x % m + m) % m;
	}

	Dictionary<string, Color> Colors = new Dictionary<string, Color>()
	{
		{"red",    Color.red},
		{"orange", new Color(1, 0.5f, 0)},
		{"yellow", Color.yellow},
		{"green",  Color.green},
		{"blue",   Color.blue},
		{"purple", new Color(0.5f, 0, 0.78f)},
		{"black", Color.black}
	};

	Dictionary<int, string> numToName = new Dictionary<int, string>()
	{
		{5, "red"},
		{2, "orange"},
		{3, "yellow"},
		{1, "green"},
		{6, "blue"},
		{4, "purple"}
	};

	List<string> unpickedNames = new List<string>() {
		"red",
		"orange",
		"yellow",
		"green",
		"blue",
		"purple",
		"black"
	};

	Cell StartingCell = null;
	bool StartFound = false;
	List<Cell> Picks = null;

	void ModuleStarted()
	{
		pick_colors:
		Picks = new List<Cell>() { StartingCell };
		int total = Random.Range(5, 8);

		for (int i = 0; i < (total - 1); i++)
		{
			Picks.Add(Game.Cells.Except(Picks).ElementAt(Random.Range(0, Game.Cells.Count - Picks.Count)));
		}

		Picks.Sort(delegate (Cell x, Cell y)
		{
			return Game.Cells.IndexOf(x) < Game.Cells.IndexOf(y) ? -1 : 1;
		});

		int digit = Bomb.GetSerialNumberNumbers().ElementAt(1);
		int number = digit;
		if (number == 0)
		{
			number = 10;
		}
		number = (number - 1) % total;

		int G = total - Picks.IndexOf(StartingCell);
		int S = Bomb.GetSerialNumberLetters().First() - 64;
		int sol = mod(((G - S) - 1), total) + 1;
		if (sol == 7)
		{
			goto pick_colors;
		}

		string solName = numToName[sol]; // This is the solution color's name.
		unpickedNames.Remove(solName);

		foreach (Cell cell in Picks)
		{
			if (cell == Picks[number])
			{
				cell._renderer.color = Colors[solName];
				cell.Color = solName;
			}
			else
			{
				string name = unpickedNames[Random.Range(0, unpickedNames.Count)];
				cell._renderer.color = Colors[name];
				cell.Color = name;
				unpickedNames.Remove(name);
			}
		}

		Log("Color order: " + Picks.Select((a) => a.Color).Aggregate((a, b) => a + ", " + b) + ".");
		Log("Second digit of the serial number is {0}.", digit);
		if (digit == 0)
		{
			Log("Which is actually 10 instead of 0.");
		}
		Log("The cell color we need to use is {0} which stands for {1}.", solName, sol);
		Log("The first letter in the serial is {0} which stands for {1}", Bomb.GetSerialNumberLetters().First(), S);
		Log("The offset from the the bottom right corner is {0}.", ((sol + S) - 1) % total + 1);
		Log("Which makes the starting cell the {0} cell.", StartingCell.Color);

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

	bool Digging = true;
	bool Held = false;
	Coroutine _playClick = null;
	Vector3[] GuideSizes = new Vector3[2];
	bool GuidesEnabled = false;

	void Start()
	{
		moduleID = idCounter++;

		Module.OnActivate += ModuleStarted;

		Slider = ModeToggle.transform.Find("Slider").gameObject;

		ModeToggle.GetComponent<KMSelectable>().OnInteract = delegate ()
		{
			Digging = !Digging;
			targetAlpha = Digging ? 0 : 1;
			UpdateSelectable();

			return false;
		};

		// Hide Guides
		int n = 0;
		foreach (GameObject guide in Guides)
		{
			GuideSizes[n++] = guide.transform.localPosition;
			guide.transform.localPosition = new Vector3();
		}

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

				Cell cell = new Cell(Game, x, y, Cell, Module, Sprites);
				Game.Cells.Insert(x + y * (int) GridSize.x, cell);
				Game.Board[y].Insert(x, cell);

				cell._selectable.OnInteract = delegate ()
				{
					_playClick = StartCoroutine(HoldCell(cell));
					Held = false;

					return false;
				};

				cell._selectable.OnInteractEnded = delegate ()
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

									cell.Dig();
								}
								else
								{
									Log("Dug the " + cell.Color + " cell instead of " + StartingCell.Color + " for the correct starting cell.");
									Module.HandleStrike();
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
												Module.HandleStrike();
												LogBoard();
												break;
											}
											else
											{
												c.Dig();
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
										Module.HandleStrike();
										LogBoard();
									}
									else
									{
										cell.Dig();
									}
								}
							}
							else if (!cell.Dug)
							{
								cell.Flagged = !cell.Flagged;
								cell.UpdateSprite();
							}
						}
					}

					if (Game.Solved)
					{
						foreach (Cell c in Game.Cells)
						{
							if (!c.Mine)
							{
								c.Dug = true;
								c.UpdateSprite();
							}
						}

						Module.HandlePass();
						LogBoard();
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
			Module.HandlePass();
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
	float targetAlpha = 0;
	float guideAlpha = 0.75f;
	void Update()
	{
		sliderAlpha = Mathf.Lerp(sliderAlpha, targetAlpha, 0.1f);

		Slider.transform.localPosition = new Vector3(0, 0.0001f, -2.5f + 5f * sliderAlpha);

		if (GuidesEnabled && guideAlpha < 1)
		{
			guideAlpha = Mathf.Min(1, guideAlpha + 0.01f);

			int n = 0;
			foreach (GameObject guide in Guides)
			{
				guide.transform.localPosition = Vector3.Lerp(new Vector3(), GuideSizes[n++], guideAlpha);
			}
		}
	}

	private class TPAction
	{
		public TPAction(Cell cell, bool held)
		{
			_cell = cell;
			_held = held;
			actionType = "interact";
		}

		public TPAction(Cell cell, bool digging, bool held)
		{
			_cell = cell;
			_digging = digging;
			_held = held;
			actionType = "setInteract";
		}

		public TPAction(string color)
		{
			_color = color;
			actionType = "digColor";
		}

		public string actionType;
		public Cell _cell;
		public bool _digging;
		public bool _held;
		public string _color;
	}

	private bool EqualsAny(object obj, params object[] targets)
	{
		return targets.Contains(obj);
	}

	public static string TwitchHelpMessage = "Dig the initial color with !{0} dig blue. Dig the cell in column 1 row 2 with !{0} dig 1 2. Flag the cell on column 3 row 4 with !{0} flag 3 4. Add \"hold\" to the end of a command to hold down on a cell. You can use negative numbers to count from the end of that row/column. Chain commands with a semicolon.";
	public IEnumerator ProcessTwitchCommand(string command)
	{
		List<TPAction> actions = new List<TPAction>();
		
		string[] commands = command.ToLowerInvariant().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string cmd in commands)
		{
			string[] split = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			bool digging = EqualsAny(split[0], "dig", "d");
			if (StartFound)
			{
				bool holding = EqualsAny(split.Last(), "hold", "holding", "h");
				if (EqualsAny(split.Length, 3, 4) && EqualsAny(split[0], "dig", "flag", "d", "f"))
				{
					if (holding != (split.Length == 4)) yield break;

					int x;
					int y;
					if (int.TryParse(split[1], out x) && int.TryParse(split[2], out y))
					{
						if (x < 0) x += 9;
						if (y < 0) y += 11;
						Cell cell = Game.GetCell(x - 1, y - 1);
						if (cell == null) yield break;

						actions.Add(new TPAction(cell, digging, holding));
					}
				}
				else if (EqualsAny(split.Length, 2, 3))
				{
					if (holding != (split.Length == 3)) yield break;

					int x;
					int y;
					if (int.TryParse(split[0], out x) && int.TryParse(split[1], out y))
					{
						if (x < 0) x += 9;
						if (y < 0) y += 11;
						Cell cell = Game.GetCell(x - 1, y - 1);
						if (cell == null) yield break;

						actions.Add(new TPAction(cell, holding));
					}
				}
			}
			else if (split.Length == 2 && digging && Colors.Keys.Contains(split[1]))
			{
				actions.Add(new TPAction(split[1]));
			}
		}
		
		if (actions.Count != commands.Length)
		{
			yield break;
		}

		yield return null;
		foreach (TPAction action in actions)
		{
			if (action.actionType == "digColor")
			{
				foreach (Cell cell in Game.Cells)
				{
					if (cell.Color == action._color) cell.Click();
				}

				GuidesEnabled = true;
				foreach (GameObject guide in Guides) guide.SetActive(true);
			}
			else if (EqualsAny(action.actionType, "interact", "setInteract"))
			{
				if (action.actionType == "setInteract" && action._digging != Digging)
				{
					ModeToggle.GetComponent<KMSelectable>().OnInteract();
					yield return new WaitForSeconds(0.1f);
				}

				action._cell._selectable.OnInteract();
				if (action._held) yield return new WaitForSeconds(0.3f);
				action._cell._selectable.OnInteractEnded();
				yield return new WaitForSeconds(0.1f);
			}
		}
	}
}
