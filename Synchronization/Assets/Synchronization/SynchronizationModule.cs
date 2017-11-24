using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;
using System.Text.RegularExpressions;

[RequireComponent(typeof(KMBombModule))]
[RequireComponent(typeof(KMBombInfo))]
[RequireComponent(typeof(KMAudio))]
public class SynchronizationModule : MonoBehaviour
{
	public KMBombModule Module;
	public KMBombInfo BombInfo;
	public KMAudio Audio;
	public KMSelectable SyncButton;
	public TextMesh DisplayText;
	public GameObject[] LightObjects;
	static MonoBehaviour MonoBehaviour;

	const float FlashingSpeed = 0.3f;
	int DisplayNumber;
	bool Solved = false;
	int SelectedSpeed = 0;
	int[] SyncMethod;

	static int idCounter = 1;
	int moduleID;

	Light[] Lights;

	class Light
	{
		bool _state = true;
		Color _color = Color.white;

		Material lightMat;
		Coroutine flashingCoroutine;

		public GameObject gameObject;
		public GameObject selection;
		public int speed = 0;
		public float randomDelay = Random.value * FlashingSpeed;

		public Light(GameObject light)
		{
			lightMat = light.GetComponent<Renderer>().material;
			gameObject = light;
			selection = light.transform.Find("Selection").gameObject;
		}

		void UpdateMat()
		{
			lightMat.SetFloat("_Blend", _state ? 1f : 0f);
			lightMat.SetColor("_LitColor", _color);
		}

		public bool state
		{
			set
			{
				_state = value;
				UpdateMat();
			}
			get
			{
				return _state;
			}
		}

		public Color color
		{
			set
			{
				_color = value;
				UpdateMat();
			}
		}

		IEnumerator Flash()
		{
			yield return new WaitForSeconds(randomDelay);

			while (true)
			{
				state = true;
				yield return new WaitForSeconds(FlashingSpeed);
				state = false;
				yield return new WaitForSeconds((6 - speed) * FlashingSpeed);
			}
		}

		public void StartFlashing()
		{
			if (speed > 0 && flashingCoroutine == null)
			{
				flashingCoroutine = MonoBehaviour.StartCoroutine(Flash());
			}
		}

		public void StopFlashing()
		{
			if (flashingCoroutine != null)
			{
				MonoBehaviour.StopCoroutine(flashingCoroutine);
				flashingCoroutine = null;
			}
		}
	}

	void ApplyToSpeed(int speed, Action<Light> action)
	{
		foreach (Light light in Lights)
		{
			if (light.speed == speed) action(light);
		}
	}

	void Log(object data)
	{
		Debug.LogFormat("[Synchronization #{0}] {1}", moduleID, data);
	}

	void Log(object data, params object[] formatting)
	{
		Log(string.Format(data.ToString(), formatting));
	}

	void Start()
	{
		MonoBehaviour = this;

		moduleID = idCounter++;

		Lights = LightObjects.Select(obj => new Light(obj)).ToArray();

		DisplayNumber = Random.Range(1, 10);
		DisplayText.text = DisplayNumber.ToString();
		Log("Displayed a {0}", DisplayNumber);

		StartCoroutine(Startup());
		Module.OnActivate += Activate;
	}

	KMSelectable.OnInteractHandler SetupInteraction(Light light)
	{
		return delegate ()
		{
			if (light.speed == 0 || Solved) return false;
			
			light.gameObject.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

			if (SelectedSpeed == 0)
			{
				ApplyToSpeed(light.speed, l =>
				{
					l.selection.SetActive(true);
					l.StopFlashing();
				});

				SelectedSpeed = light.speed;
			}
			else
			{
				if (SelectedSpeed == light.speed)
				{
					ApplyToSpeed(light.speed, l =>
					{
						l.selection.SetActive(false);
						l.StartFlashing();
					});

					SelectedSpeed = 0;
				}
				else
				{
					bool valid = ValidateSync(Lights.First(l => l.speed == SelectedSpeed), light);
					if (valid)
					{
						Log("Successfully synced {0} and {1}.", light.speed, SelectedSpeed);
						ApplyToSpeed(light.speed, l =>
						{
							l.StopFlashing();
							l.StartFlashing();
						});

						ApplyToSpeed(SelectedSpeed, l =>
						{
							l.randomDelay = light.randomDelay;
							l.speed = light.speed;
							l.selection.SetActive(false);
							l.StartFlashing();
						});
					}
					else
					{
						Module.HandleStrike();

						ApplyToSpeed(SelectedSpeed, l =>
						{
							l.selection.SetActive(false);
							l.StartFlashing();
						});
					}

					SelectedSpeed = 0;
				}
			}

			return false;
		};
	}

	bool firstSyncDone = false;
	bool altRuleFirstState = false;
	int oppRuleFirstSpeed = 0;

	bool ValidateSync(Light lightA, Light lightB)
	{
		int[] orderedSpeeds = Lights.Select(l => l.speed).Where(s => s != 0).Distinct().OrderBy(s => s).ToArray();
		if (orderedSpeeds.Length == 1) return false;

		/* Order:
		 * Asc = 0
		 * Des = 1
		 * Opp = 2
		 * State:
		 * +   = 0
		 * -   = 1
		 * Alt = 2
		*/

		switch (SyncMethod[0])
		{
			case 0:
				if (lightA.speed != orderedSpeeds[0] || lightB.speed != orderedSpeeds[1]) return false;

				break;
			case 1:
				if (lightA.speed != orderedSpeeds[orderedSpeeds.Length - 1] || lightB.speed != orderedSpeeds[orderedSpeeds.Length - 2]) return false;

				break;
			case 2:
				if (firstSyncDone && lightB.speed != oppRuleFirstSpeed) return false; // The second light you select will always have the same speed.

				if ((lightA.speed != orderedSpeeds[0] || lightB.speed != orderedSpeeds[orderedSpeeds.Length - 1]) && // Check if they have selected either slowest with fastest or fastest with slowest.
					(lightA.speed != orderedSpeeds[orderedSpeeds.Length - 1] || lightB.speed != orderedSpeeds[0])) return false;

				break;
		}

		switch (SyncMethod[1])
		{
			case 0:
				if (lightA.state == false || lightB.state == false) return false;

				break;
			case 1:
				if (lightA.state == true || lightB.state == true) return false;

				break;
			case 2:
				if (firstSyncDone && lightA.state != altRuleFirstState) return false; // Make sure they keep alternating

				if (lightA.state == lightB.state) return false;

				altRuleFirstState = lightA.state;

				break;
		}

		// Gather info for alt rule and opp rule.
		altRuleFirstState = lightA.state;
		oppRuleFirstSpeed = lightB.speed;

		firstSyncDone = true;
		return true;
	}

	string[] orders = new[] { "Asc", "Des", "Opp" };
	string[] states = new[] { "+", "-", "Alt" };
	int[][][] chart = new int[][][]
	{
		new[] {new[] {1, 1}, new[] {0, 1}, new[] {2, 2}, new[] {0, 2}, new[] {2, 2}, new[] {0, 1}, new[] {2, 1}, new[] {2, 1}, new[] {2, 0}},
		new[] {new[] {0, 0}, new[] {2, 2}, new[] {1, 2}, new[] {1, 0}, new[] {1, 2}, new[] {1, 0}, new[] {0, 1}, new[] {0, 0}, new[] {0, 2}},
		new[] {new[] {1, 1}, new[] {1, 2}, new[] {2, 1}, new[] {2, 0}, new[] {1, 0}, new[] {0, 2}, new[] {0, 0}, new[] {1, 1}, new[] {2, 0}}
	};

	int[] lightToCol = new int[] { 0, 1, 2, 7, 8, 3, 6, 5, 4 }; // Since the chart columns are in a different order than my light indexes
	Vector2[] lightToDir = new Vector2[] {
		new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
		new Vector2(-1, 0), new Vector2(0, 0), new Vector2(1, 0),
		new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1)
	};

	void Activate()
	{
		SyncButton.OnInteract += delegate ()
		{
			if (Lights.Where(l => l.speed != 0).Select(l => l.speed).Distinct().Count() == 1 && !Solved)
			{
				SyncButton.AddInteractionPunch(0.5f);
				Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

				if (((int) BombInfo.GetTime() % 60).ToString().Contains(DisplayNumber.ToString()))
				{
					Module.HandlePass();
					Solved = true;

					foreach (Light light in Lights)
					{
						light.StopFlashing();
						light.state = true;
					}

					StartCoroutine(PlayWinAnimation());
				}
				else
				{
					Module.HandleStrike();
				}
			}

			return false;
		};

		foreach (Light l in Lights)
		{
			l.gameObject.GetComponent<KMSelectable>().OnInteract += SetupInteraction(l);
		}

		List<int> speeds = new List<int>() { 1, 2, 3, 4, 5 };
		List<int> lightIndexes = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

		for (int i = 0; i < 5; i++)
		{
			Lights[ExtractRandom(lightIndexes)].speed = ExtractRandom(speeds);
		}

		foreach (int lightIndex in lightIndexes)
		{
			Lights[lightIndex].state = true;
		}

		foreach (Light light in Lights)
		{
			light.StartFlashing();
		}
		
		Log("Light speeds:\n{0} {1} {2}\n{3} {4} {5}\n{6} {7} {8}", Lights.Select(l => (object) l.speed).ToArray());

		// Find which way the user needs to sync 

		int slowestLight = Array.IndexOf(Lights, Lights.Where(l => l.speed != 0).Aggregate((l1, l2) => l1.speed < l2.speed ? l1 : l2));
		Vector2 chartPos = new Vector2(
			lightToCol[slowestLight],
			Mathf.FloorToInt((DisplayNumber - 1) / 3)
		);
		Log("Started at column {0}, row {1}", chartPos.x + 1, chartPos.y + 1);

		chartPos += lightToDir[slowestLight] * Lights[4].speed;
		chartPos.x = WrapInt((int) chartPos.x, 8);
		chartPos.y = WrapInt((int) chartPos.y, 2);
		Log("Ended at column {0}, row {1}", chartPos.x + 1, chartPos.y + 1);

		SyncMethod = chart[(int) chartPos.y][(int) chartPos.x];
		Log("Lights need to be synced in {0} {1} order", orders[SyncMethod[0]], states[SyncMethod[1]]);
	}

	IEnumerator Startup()
	{
		yield return new WaitForSeconds(1);

		int[][] patterns = new int[][] {
			new[] { 2, 1, 0, 3, 4, 5, 8, 7, 6 },
			new[] { 7, 4, 3, 5, 0, 2 },
			new[] { 6, 3, 0, 4, 8, 5, 2 },
			new[] { 2, 1, 0, 3, 6, 7, 8 },
			new[] { 0, 1, 2, 5, 8, 7, 6, 3 },
			new[] { 0, 1, 2, 4, 6, 7, 8 }
		};

		int[] pattern = patterns[Random.Range(0, patterns.Length)];
		foreach (int light in pattern)
		{
			Lights[light].state = true;
			yield return new WaitForSeconds(0.1f);
		}

		yield return new WaitForSeconds(0.5f);
		foreach (int light in pattern)
		{
			Lights[light].state = false;
			yield return new WaitForSeconds(0.1f);
		}

		yield return new WaitForSeconds(1);
	}

	IEnumerator PlayWinAnimation()
	{
		int[][] WinningAnimation = {
			new[] { 4 },
			new[] { 1, 3, 5, 7 },
			new[] { 0, 2, 6, 8 },
		};

		foreach (int[] frame in WinningAnimation)
		{
			for (int i = 1; i <= 3; i++)
			{
				foreach (int light in frame)
				{
					Lights[light].color = Color.Lerp(Color.white, Color.green, (float) i / 3);
				}

				yield return new WaitForSeconds(0.05f);
			}
		}
	}

	/*int[][][] WinningAnimations = {
		new[] {
			new[] { 4 },
			new[] { 1, 3, 4, 5, 7 },
			new[] { 0, 1, 2, 3, 5, 6, 7, 8 },
			new[] { 0, 2, 6, 8 }
		},
		new[] {
			new[] { 0, 1, 2 },
			new[] { 2, 4, 6 },
			new[] { 2, 5, 8 },
			new[] { 0, 4, 8 },
			new[] { 6, 7, 8 },
			new[] { 6, 4, 2 },
			new[] { 0, 3, 6 },
			new[] { 0, 4, 8 },
			new[] { 0, 1, 2 },
		},
		new[] {
			new[] { 6 },
			new[] { 3, 7 },
			new[] { 0, 4, 8 },
			new[] { 3, 1, 5 },
			new[] { 6, 4, 2 },
			new[] { 3, 7, 5 },
			new[] { 0, 4, 8 },
			new[] { 1, 5 },
			new[] { 2 },
		},
	};

	IEnumerator PlayWinAnimation()
	{
		int[][] animation = WinningAnimations[Random.Range(0, WinningAnimations.Length)];

		foreach (int[] frame in animation)
		{
			int index = 0;
			foreach (Light light in Lights)
			{
				light.color = frame.Contains(index) ? Color.green : Color.white;

				index++;		
			}

			yield return new WaitForSeconds(0.25f);
		}

		foreach (Light light in Lights) light.color = Color.white;
	}*/

	T ExtractRandom<T>(List<T> list)
	{
		int index = Random.Range(0, list.Count);
		T value = list[index];
		list.RemoveAt(index);

		return value;
	}

	int WrapInt(int a, int b)
	{
		while (a < 0) a += b;
		while (a > b) a -= b + 1;

		return a;
	}

	private bool EqualsAny(object obj, params object[] targets)
	{
		return targets.Contains(obj);
	}

	int? StringToLight(string light)
	{
		Dictionary<string, string> replacements = new Dictionary<string, string>()
		{
			{ "center", "middle" },
			{ "centre", "middle" },
			{ "middle", "m" },
			{ "top", "t" },
			{ "bottom", "b" },
			{ "left", "l" },
			{ "right", "r" }
		};

		foreach (var replacement in replacements)
		{
			light = light.Replace(replacement.Key, replacement.Value);
		}

		light = new Regex("([lrm])([tbm]{1})").Replace(light, "$2$1");

		string[] buttonPositions = new[] { "tl", "tm", "tr", "ml", "mm", "mr", "bl", "bm", "br" };

		int pos = 1;
		foreach (string name in buttonPositions)
		{
			light = light.Replace(name, pos.ToString());

			pos++;
		}

		int lightInt;
		if (light.Length == 1 && int.TryParse(light, out lightInt))
		{
			if (lightInt == 0) return null;

			return lightInt;
		}

		return null;
	}

	public IEnumerator ProcessTwitchCommand(string command)
	{
		string[] split = command.ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (split[0] == "sync" && split.Length == 3)
		{
			if (EqualsAny(split[1], "at", "on") && split[2].Length == 1)
			{
				int seconds;
				if (int.TryParse(split[2], out seconds))
				{
					yield return "trycancel";

					yield return new WaitUntil(() => ((int) BombInfo.GetTime() % 60).ToString().Contains(split[2]));

					SyncButton.OnInteract();
					yield return new WaitForSeconds(0.1f);
				}
			}
			else if (EqualsAny(split[2], "on", "+", "true", "t", "off", "-", "false", "f"))
			{
				int? possibleLight = StringToLight(split[1]);
				if (possibleLight != null)
				{
					int lightIndex = (int) possibleLight - 1;
					bool lightState = EqualsAny(split[2], "on", "+", "true", "t");

					if (Lights[lightIndex].speed == 0) yield break;

					yield return "trycancel";

					yield return new WaitUntil(() => Lights[lightIndex].state == lightState);

					Lights[lightIndex].gameObject.GetComponent<KMSelectable>().OnInteract();
					yield return new WaitForSeconds(0.1f);
				}
			}
		}
	}
}
