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
	public GameObject AnimationPivot;

	static float FlashingSpeed = 0.35f;
	int DisplayNumber;
	bool Solved = false;
	Light SelectedLight;
	int[] SyncMethod;

	static int idCounter = 1;
	int moduleID;

	Light[] Lights;

	class Light
	{
		bool _state = true;
		Color _color = Color.white;
		MonoBehaviour _module;

		Material lightMat;
		Coroutine flashingCoroutine;

		public GameObject gObject;
		public GameObject selection;
		public int speed = 0;
		public float randomDelay = Random.value * FlashingSpeed;
		public int syncIndex = 0; // Used for sync animation ordering.

		public Light(GameObject light, MonoBehaviour module)
		{
			lightMat = light.GetComponent<Renderer>().material;
			gObject = light;
			selection = light.transform.Find("Selection").gameObject;
			_module = module;
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

			WaitForSeconds flashPause = new WaitForSeconds((6 - speed) * FlashingSpeed);
			while (true)
			{
				state = true;
				yield return flashPause;
				state = false;
				yield return flashPause;
			}
		}

		public void StartFlashing()
		{
			if (speed > 0 && flashingCoroutine == null)
			{
				flashingCoroutine = _module.StartCoroutine(Flash());
			}
		}

		public void StopFlashing()
		{
			if (flashingCoroutine != null)
			{
				_module.StopCoroutine(flashingCoroutine);
				flashingCoroutine = null;
			}
		}
	}

	class Vector2Int
	{
		public int x;
		public int y;

		public Vector2Int(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public static Vector2Int operator +(Vector2Int a, Vector2Int b)
		{
			return new Vector2Int(a.x + b.x, a.y + b.y);
		}
	}

	void ApplyToSpeed(Light light, Action<Light> action)
	{
		int speed = light.speed;
		foreach (Light l in Lights)
		{
			if (l.speed == speed) action(l);
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
		moduleID = idCounter++;

		Lights = LightObjects.Select(obj => new Light(obj, this)).ToArray();

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
			if (light.speed == 0 || Solved || syncPause) return false;
			
			light.gObject.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

			if (SelectedLight == null)
			{
				ApplyToSpeed(light, l =>
				{
					l.selection.SetActive(true);
					l.StopFlashing();
				});

				SelectedLight = light;
			}
			else
			{
				if (SelectedLight.speed == light.speed)
				{
					ApplyToSpeed(light, l =>
					{
						l.selection.SetActive(false);
						l.StartFlashing();
					});

					SelectedLight = null;
				}
				else
				{
					StartCoroutine(SyncLights(light));
				}
			}

			return false;
		};
	}

	bool syncPause = false;
	IEnumerator SyncLights(Light light)
	{
		syncPause = true;

		ApplyToSpeed(light, l => l.StopFlashing());
		
		List<Action<float>> animations = new List<Action<float>>();
		var lightsToSync = Lights.Where(l => l.speed == SelectedLight.speed).OrderBy(l => l.syncIndex).Concat(Lights.OrderBy(l => l.syncIndex).Where(l => l.speed == light.speed)).ToArray();
		for (int i = 0; i < lightsToSync.Length -1 ; i++)
		{
			animations.Add(SyncAnimation(lightsToSync[i], lightsToSync[i + 1]));
		}
		ApplyToSpeed(SelectedLight, l => l.selection.SetActive(false));

		float animStart = Time.time;
		float alpha = 0;
		while (alpha < 1)
		{
			alpha = Math.Min(Time.time - animStart, 1);
			foreach (Action<float> animation in animations) animation(alpha);
			yield return null;
		}
		
		bool valid = ValidateSync(SelectedLight, light);

		Log("{0} synced {1} while {2} and {3} while {4}.", valid ? "Successfully" : "Incorrectly", SelectedLight.speed, SelectedLight.state ? "on" : "off", light.speed, light.state ? "on" : "off");

		if (valid)
		{
			int startingIndex = Lights.Where(l => l.speed == SelectedLight.speed).Count() + 1;
			ApplyToSpeed(light, l =>
			{
				l.StopFlashing();
				l.StartFlashing();
				l.syncIndex = startingIndex++;
			});

			ApplyToSpeed(SelectedLight, l =>
			{
				l.randomDelay = light.randomDelay;
				l.speed = light.speed;
				l.StartFlashing();
			});
		}
		else
		{
			Module.HandleStrike();

			ApplyToSpeed(light, l => l.StartFlashing());
			ApplyToSpeed(SelectedLight, l => l.StartFlashing());
		}

		SelectedLight = null;
		syncPause = false;
	}

	Action<float> SyncAnimation(Light lightA, Light lightB)
	{
		Vector3 lightAlP = lightA.gObject.transform.localPosition;
		Vector3 lightBlP = lightB.gObject.transform.localPosition;
		Transform animationPivot = Instantiate(AnimationPivot, AnimationPivot.transform.parent).transform;
		
		animationPivot.localPosition = lightAlP;
		animationPivot.localRotation = Quaternion.Euler(0, Mathf.Atan2(lightBlP.x - lightAlP.x, lightBlP.z - lightAlP.z) * 180f / Mathf.PI, 0);

		Transform streak = animationPivot.Find("Streak").transform;
		Transform beginning = animationPivot.Find("Beginning").transform;
		Transform end = animationPivot.Find("End").transform;
		float distance = (lightAlP - lightBlP).magnitude;

		animationPivot.gameObject.SetActive(true);
		return alpha =>
		{
			beginning.localPosition = new Vector3(0, 0.0001f, distance * Math.Min(alpha * 2, 1));
			end.localPosition = new Vector3(0, 0.0001f, distance * Math.Max(alpha - 0.5f, 0) * 2);
			streak.localPosition = new Vector3(0, 0.0001f, distance * alpha);
			streak.localScale = new Vector3(0.0435f, 0, (-Math.Abs(alpha * 2 - 1) + 1) * distance);
			if (alpha == 1) Destroy(animationPivot.gameObject);
		};
	}

	bool firstSyncDone = false;
	bool altRuleState = false;
	bool oppRuleFirstGreater = false;

	bool ValidateSync(Light lightA, Light lightB)
	{
		var speedDuplicates = Lights.Select(l => l.speed).Where(s => s != 0).GroupBy(s => s);

		var speeds = Lights.Select(l => l.speed).Where(s => s != 0).Distinct();
		if (speedDuplicates.Count(group => group.Count() == 1) >= 2)
		{
			speeds = speeds.Where(s => speedDuplicates.Where(group => group.Key == s).First().Count() == 1);
		}

		int[] orderedSpeeds = speeds.OrderBy(s => s).ToArray();
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
				if (firstSyncDone && lightA.speed > lightB.speed != oppRuleFirstGreater) return false; // The greater light will always stay the same.

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
				if (firstSyncDone && (lightA.state == altRuleState || lightB.state == altRuleState)) return false; // Make sure they keep alternating

				if (lightA.state != lightB.state) return false;

				if (firstSyncDone)
				{
					altRuleState = !altRuleState;
				}
				else
				{
					altRuleState = lightA.state;
				}

				break;
		}

		// Gather info for alt rule and opp rule.
		altRuleState = lightA.state;
		oppRuleFirstGreater = lightA.speed > lightB.speed;

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
	Vector2Int[] lightToDir = new Vector2Int[] {
		new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
		new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0),
		new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1)
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
			l.gObject.GetComponent<KMSelectable>().OnInteract += SetupInteraction(l);
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

		int fastestLight = Array.IndexOf(Lights, Lights.Where(l => l.speed != 0).Aggregate((l1, l2) => l1.speed > l2.speed ? l1 : l2));
		int slowestLight = Array.IndexOf(Lights, Lights.Where(l => l.speed != 0).Aggregate((l1, l2) => l1.speed < l2.speed ? l1 : l2));
		Vector2Int chartPos = new Vector2Int(
			lightToCol[fastestLight],
			Mathf.FloorToInt((DisplayNumber - 1) / 3)
		);

		var startingCell = chart[(int) chartPos.y][(int) chartPos.x];
		Log("Started at column {0}, row {1} ({2} {3})", chartPos.x + 1, chartPos.y + 1, orders[startingCell[0]], states[startingCell[1]]);

		for (int i = 0; i < Lights[4].speed; i++)
		{
			chartPos += lightToDir[slowestLight];
			chartPos.x = (chartPos.x % 9 + 9) % 9;
			chartPos.y = (chartPos.y % 3 + 3) % 3;
		}
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
	}

	string[] WinningAnimations = {
		//"4,1357,0268", // Middle -> All
		"0,13,246,57,8", // TL -> BR
		"2,15,048,37,6", // TR -> BL
		//"012,345,678", // T -> B
		//"036,147,258", // L -> R
		"0,1,2,5,8,7,6,3,4", // Spiral
		"0,3,6,7,4,1,2,5,8", // Vertical back and forth
		"0,1,2,5,4,3,6,7,8", // Horizontial back and forth
		"1,042,375,68", // Triangle T -> B
		"3,046,157,28", // Triangle L -> R
		"08,1375,642", // Diagonal crush
		"1,6,5,0,4,3,7,2,8", // "Random"
		"26,71,80,53,4", // Collapsing
		"68,4,02,1,35,7", // Cross
	};

	IEnumerator PlayWinAnimation()
	{
		var animationIterable = WinningAnimations[Random.Range(0, WinningAnimations.Length)]
			.Split(',')
			.Select(lights =>
				lights.Select(index =>
					Lights[int.Parse(index.ToString())]
				)
			);

		if (Random.Range(0, 2) == 1) animationIterable = animationIterable.Reverse();

		var animation = animationIterable.ToArray();
		
		float startTime = Time.time;
		float alphaStep = 1f / animation.Length;
		while (Time.time - startTime <= 1)
		{
			float alpha = (Time.time - startTime) / 1;

			for (int i = 0; i < animation.Length; i++)
			{
				foreach (Light light in animation[i])
				{
					light.color = Color.Lerp(Color.white, Color.green, (alpha - alphaStep * i) / alphaStep);
				}
			}
			
			yield return null;
		}
	}

	T ExtractRandom<T>(List<T> list)
	{
		int index = Random.Range(0, list.Count);
		T value = list[index];
		list.RemoveAt(index);

		return value;
	}

	int WrapInt(int a, int b)
	{
		while (a < 0) a += b + 1;
		while (a > b) a -= b + 1;

		return a;
	}

	private bool EqualsAny(object obj, params object[] targets)
	{
		return targets.Contains(obj);
	}

	Light StringToLight(string light)
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
		if (light.Length == 1 && int.TryParse(light, out lightInt) && lightInt > 0)
		{
			return Lights[lightInt - 1];
		}

		return null;
	}

	#pragma warning disable 414
	private string TwitchHelpMessage = "To sync a pair of lights do !{0} topm on centerm +. To sync to the bomb timer use !{0} 5. Commands are chainable.";
	#pragma warning restore 414

	public IEnumerator ProcessTwitchCommand(string command)
	{
		foreach (string subcommand in command.ToLowerInvariant().Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string[] split = subcommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (split.Length == 1 && split[0].Length == 1)
			{
				int seconds;
				if (int.TryParse(split[0], out seconds))
				{
					yield return null;
					while (!((int) BombInfo.GetTime() % 60).ToString().Contains(split[0]))
					{
						yield return "trycancel";
						yield return true;
					}

					SyncButton.OnInteract();
					yield return new WaitForSeconds(0.1f);
				}
			}
			else if (split.Length == 4 && EqualsAny(split[1], "on", "+", "true", "t", "off", "-", "false", "f") && EqualsAny(split[3], "on", "+", "true", "t", "off", "-", "false", "f"))
			{
				Light lightA = StringToLight(split[0]);
				Light lightB = StringToLight(split[2]);
				if (lightA != null && lightB != null)
				{
					bool lightAState = EqualsAny(split[1], "on", "+", "true", "t");
					bool lightBState = EqualsAny(split[3], "on", "+", "true", "t");

					if (lightA.speed == 0 || lightB.speed == 0 || lightA.speed == lightB.speed) yield break;

					yield return null;
					while (lightA.state != lightAState) yield return true;

					lightA.gObject.GetComponent<KMSelectable>().OnInteract();
					yield return new WaitForSeconds(0.1f);

					while (lightB.state != lightBState) yield return true;

					lightB.gObject.GetComponent<KMSelectable>().OnInteract();
					yield return new WaitForSeconds(0.1f);
				}
			}
		}
	}
	
	IEnumerator TwitchHandleForcedSolve()
	{
		while (true)
		{
			var speedDuplicates = Lights.Select(l => l.speed).Where(s => s != 0).GroupBy(s => s);

			var speeds = Lights.Select(l => l.speed).Where(s => s != 0).Distinct();
			if (speedDuplicates.Count(group => group.Count() == 1) >= 2)
			{
				speeds = speeds.Where(s => speedDuplicates.Where(group => group.Key == s).First().Count() == 1);
			}

			int[] orderedSpeeds = speeds.OrderBy(s => s).ToArray();
			if (orderedSpeeds.Length == 1) break;

			int lightASpeed = 0;
			int lightBSpeed = 0;

			switch (SyncMethod[0])
			{
				case 0:
					lightASpeed = orderedSpeeds[0];
					lightBSpeed = orderedSpeeds[1];

					break;
				case 1:
					lightASpeed = orderedSpeeds[orderedSpeeds.Length - 1];
					lightBSpeed = orderedSpeeds[orderedSpeeds.Length - 2];

					break;
				case 2:
					if (oppRuleFirstGreater)
					{
						lightASpeed = orderedSpeeds[orderedSpeeds.Length - 1];
						lightBSpeed = orderedSpeeds[0];
					}
					else
					{
						lightASpeed = orderedSpeeds[0];
						lightBSpeed = orderedSpeeds[orderedSpeeds.Length - 1];
					}

					break;
			}

			bool[] lightStates = { true, false, !altRuleState };
			
			yield return ProcessTwitchCommand(string.Format("{0} {1} {2} {3}", Array.FindIndex(Lights, light => light.speed == lightASpeed) + 1, lightStates[SyncMethod[1]], Array.FindIndex(Lights, light => light.speed == lightBSpeed) + 1, lightStates[SyncMethod[1]]));
			yield return new WaitUntil(() => syncPause == false);
		}

		yield return ProcessTwitchCommand(DisplayNumber.ToString());
	}
}
