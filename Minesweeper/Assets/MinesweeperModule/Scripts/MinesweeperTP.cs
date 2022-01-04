using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KeepCoding;
using UnityEngine;
using Random = UnityEngine.Random;
using Cell = MinesweeperModule.Cell;

public class MinesweeperTP : TPScript<MinesweeperModule>
{
	private bool EqualsAny(object obj, params object[] targets)
	{
		return targets.Contains(obj);
	}

	public override IEnumerator Process(string command)
	{
		string[] chainedCommands = command.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (chainedCommands.Length > 1)
		{
			var commandRoutines = chainedCommands.Select(Process).ToArray();
			var invalidCommand = Array.Find(commandRoutines, routine => !routine.MoveNext());
			if (invalidCommand != null)
			{
				yield return "sendtochaterror The command \"" + chainedCommands[Array.IndexOf(commandRoutines, invalidCommand)] + "\" is invalid.";
				yield break;
			}

			yield return null;
			foreach (IEnumerator routine in commandRoutines)
			{
				do
				{
					yield return routine.Current;
				}
				while (routine.MoveNext());
			}

			yield break;
		}

		string[] split = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		bool digging = split.Length >= 1 && EqualsAny(split[0], "dig", "d");
		if (Module.StartFound)
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
					Cell cell = Module.Game.GetCell(x - 1, y - 1);
					if (cell == null) yield break;

					yield return null;
					if (digging != Module.Digging)
					{
						Module.ModeToggle.GetComponent<KMSelectable>().OnInteract();
						yield return new WaitForSeconds(0.1f);
					}

					if (!Module.Game.Solved) yield return "solve";
					cell._selectable.OnInteract();
					if (holding) yield return new WaitForSeconds(0.3f);
					cell._selectable.OnInteractEnded();
					yield return new WaitForSeconds(0.1f);
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
					Cell cell = Module.Game.GetCell(x - 1, y - 1);
					if (cell == null) yield break;

					yield return null;
					if (!Module.Game.Solved) yield return "solve";
					cell._selectable.OnInteract();
					if (holding) yield return new WaitForSeconds(0.3f);
					cell._selectable.OnInteractEnded();
					yield return new WaitForSeconds(0.1f);
				}
			}
		}
		else if (split.Length == 2 && digging && Module.Colors.Keys.Contains(split[1]))
		{
			yield return null;
			foreach (Cell cell in Module.Game.Cells)
			{
				if (cell.Color == split[1])
				{
					cell.Click();
					yield return new WaitForSeconds(0.1f);
				}
			}

			StartCoroutine(TwitchPlaysFormat());
		}
	}

	IEnumerator TwitchPlaysFormat()
	{
		Vector3[] GuidePositions = new Vector3[2];
		Vector3[] GuideScales = new Vector3[2];

		for (int n = 0; n < Module.Guides.Length; n++)
		{
			GameObject guide = Module.Guides[n];
			GuidePositions[n] = guide.transform.localPosition;
			GuideScales[n] = guide.transform.localScale;
			guide.SetActive(true);
		}

		foreach (float alpha in Module.TimedAnimation(1))
		{
			float curvedAlpha = -Mathf.Pow(2, -10 * alpha) + 1;
			for (int n = 0; n < Module.Guides.Length; n++)
			{
				GameObject guide = Module.Guides[n];

				Vector3 guidePosition = GuidePositions[n];
				guide.transform.localPosition = Vector3.Lerp(guidePosition, guidePosition + new Vector3(0, 0, -0.0075f), curvedAlpha);
				guide.transform.localScale = Vector3.Lerp(Vector3.zero, GuideScales[n], curvedAlpha);
			}

			Vector3 localPosition = Module.Grid.transform.localPosition;
			localPosition.z = -0.0075f * curvedAlpha;
			Module.Grid.transform.localPosition = localPosition;
			yield return null;
		}
	}

	public override IEnumerator ForceSolve()
	{
		if (!Module.StartFound)
		{
			Module.StartingCell.Click();
			yield return new WaitForSeconds(0.1f);
		}

		List<Cell> Unused = Module.Game.Cells.Where(cell => cell.Number != 0 && cell.Dug).ToList(); // Cells that have a number in them but haven't been used by the solver yet.
		List<Cell> Used = new List<Cell>(); // Store the used cells temporarily until the loop is over.
		List<Cell> UnusedTemp = new List<Cell>(); // Store the new unused cells temporarily until the loop is over.

		bool Changed = true;
		while (Unused.Count > 0 && Changed && !Module.Game.Solved)
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
					if (FlagAll)
						Module.Audio.PlaySoundAtTransform("Flag-" + Random.Range(1, 9).ToString("D2"), transform);

					foreach (Cell adj in cell.Around)
					{
						if (!adj.Dug)
						{
							Module.targetAlpha = DigAll ? 0 : 1;

							if (DigAll)
							{
								yield return adj.AnimatedDig();
								UnusedTemp.AddRange(adj.AllDug.Where(uncoveredCell => !uncoveredCell.Mine && uncoveredCell.Number != 0));
							}
							else if (FlagAll)
							{
								adj.Flagged = true;
							}
							adj.UpdateSprite();
							yield return new WaitForSeconds(0.05f);
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

		yield return Module.SolveModule();
		while (!Module.LightOn) yield return true;
	}
}