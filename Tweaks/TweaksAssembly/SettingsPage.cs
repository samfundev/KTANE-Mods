using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

// TODO: Can we do something about the lag spike when reading settings for the first time?
// TODO: Consider wrapping the longer settings and making the listings twice as tall

class SettingsPage : MonoBehaviour
{
	public static SettingsPage Instance;

	public TextMesh TitleTextMesh = null;
	public KMSelectable PageSelectable = null;
	public KMSelectable FakeSelectable = null;
	public GameObject ListingTemplate = null;
	public KMSelectable Key = null;
	public GameObject Keyboard = null;

	public KMSelectable PinButton = null;
	public KMSelectable BackwardButton = null;
	public KMSelectable ForwardButton = null;

	public List<Texture> Icons = new List<Texture>();

	void SetIcon(Component component, string iconName)
	{
		Transform iconTransform = component.transform.Find("Icon");
		Texture iconTexture = Icons.FirstOrDefault(texture => texture.name == iconName);

		iconTransform.GetComponent<Renderer>().material.SetTexture("_MainTex", iconTexture);
		iconTransform.gameObject.SetActive(iconTexture != null);
	}

	public List<ModSettingsInfo> SettingsInfo = new List<ModSettingsInfo>() // TODO: Manually define some it would merge two
	{
		/*new ModSettingsInfo
		{
			Path = "Modsettings/TweakSettings.json",
			Name = "Tweaks",
			Listings = new List<Listing>()
			{
				new Listing(ListingType.Number, "Fade Time") { Key = "FadeTime" },
				new Listing(ListingType.Checkbox, "Instant Skip") { Key = "InstantSkip" },
				new Listing(ListingType.Checkbox, "Better Case Picker") { Key = "BetterCasePicker" },
				new Listing(ListingType.Checkbox, "Enable Mods Only Key") { Key = "EnableModsOnlyKey" },
				new Listing(ListingType.Checkbox, "Fix Foreign Exchange Rates") { Key = "FixFER" },
				new Listing(ListingType.Checkbox, "Bomb HUD") { Key = "BombHUD" },
				new Listing(ListingType.Checkbox, "Show Edgework") { Key = "ShowEdgework" },
				new Listing(ListingType.Dropdown, "Mode") { Key = "Mode", DropdownItems = new List<object>() { "Normal", "Time", "Zen" } },
			}
		}*/
	};

	bool PinningEnabled = false;

	[HideInInspector]
	public KMSelectable RootSelectable = null;

	const int ListingsPerPage = 10;

	public class Screen
	{
		public List<Listing> Listings = new List<Listing>();
		public string TitleText = "Mod Settings";
		public int Page = 0;
		public bool Pinnable = false;
	}

	public Screen CurrentScreen => ScreenStack.Peek();
	public Stack<Screen> ScreenStack = new Stack<Screen>(new[] { new Screen() });

	public List<Listing> CurrentListings => CurrentScreen.Listings;
	public string TitleText => CurrentScreen.TitleText;

	void UpdateScreen()
	{
		int currentPage = CurrentScreen.Page;

		int maxPage = CurrentListings.Count - ListingsPerPage;
		SetIcon(BackwardButton, $"plain-arrow-left{(currentPage > 0 ? "" : "_grey")}");//BackwardButton.gameObject.SetActive(currentPage > 0);
		BackwardButton.enabled = currentPage > 0;

		SetIcon(ForwardButton, $"plain-arrow-right{(currentPage < maxPage ? "" : "_grey")}");//ForwardButton.gameObject.SetActive(currentPage < maxPage);
		ForwardButton.enabled = currentPage < maxPage;

		SetIcon(PinButton, $"pin{(CurrentScreen.Pinnable ? (PinningEnabled ? "ned" : "") : "_grey")}");//PinButton.gameObject.SetActive(CurrentScreen.Pinnable);
		PinButton.enabled = CurrentScreen.Pinnable;

		PinningEnabled &= CurrentScreen.Pinnable; // If the current screen isn't pinnable but it was enabled, this should disable it.

		var sortedListings = CurrentListings
			.OrderByDescending(listing => listing.Pinned)
			.ThenBy(listing => listing.Text)
			.ToArray();

		for (int i = 0; i < CurrentListings.Count; i++)
		{
			GameObject Object = sortedListings[i].Object;
			Object.SetActive(i >= currentPage && i <= currentPage + (ListingsPerPage - 1));
			Object.transform.localPosition = new Vector3(-0.5f, 0.33125f - 0.0875f * (i - currentPage), -0.0001f);
			Object.transform.Find("Background").GetComponent<Renderer>().material.color = Color.HSVToRGB(0, 0, i % 2 == 0 ? 0.85f : 0.768f);
			Object.transform.Find("FakeHighlight").Find("Highlight").GetComponent<Renderer>().material.color = Color.HSVToRGB(0.5f, 0.458f, i % 2 == 0 ? 1 : 0.918f);
			SetIcon(Object.transform, sortedListings[i].Pinned ? "pinned" : "");
		}

		TitleTextMesh.text = $"<b>{TitleText}</b>\n<size=16>Page {CurrentPage / ListingsPerPage + 1} of {Mathf.CeilToInt(CurrentListings.Count / (float) ListingsPerPage)}</size>";
	}

	public int CurrentPage
	{
		get => CurrentScreen.Page;
		set {
			CurrentScreen.Page = Math.Max(value, 0);
			UpdateListings();
		}
	}

	void OnEnable()
	{
		// To add and remove selectables we need a reference to the selectable ModSelector reparents us to.
		// So there is a unused selectable that just gets reparented to get us that reference.
		RootSelectable = FakeSelectable.Parent;

		if (RootSelectable == null)
		{
			gameObject.SetActive(false);
			return;
		}

		Instance = this;

		ReadSettings();
		MakeKeyboard();
		ClearScreen();
		CurrentListings.Clear();
		ShowMainMenu();

		PinButton.gameObject.SetActive(true);
		ForwardButton.gameObject.SetActive(true);
		BackwardButton.gameObject.SetActive(true);

		PinButton.OnInteract = () =>
		{
			PinningEnabled = !PinningEnabled;
			SetIcon(PinButton, PinningEnabled ? "pinned" : "pin");
			return false;
		};

		ForwardButton.OnInteract = () => {
			CurrentPage += ListingsPerPage;
			return false;
		};

		BackwardButton.OnInteract = () => {
			CurrentPage -= ListingsPerPage;
			return false;
		};
	}

	bool OnCancel()
	{
		if (ScreenStack.Count == 1) ((Action) ModSelectorExtension.ModSelectorAPI["GoBackMethod"])();
		else
		{
			if (Keyboard.activeSelf) ToggleKeyboard();
			else PopScreen();
		}

		return false;
	}

	void ClearScreen()
	{
		foreach (Listing listing in CurrentListings)
		{
			Destroy(listing.Object);
			listing.Object = null;
			listing.TypeSelectable = null;
		}

		RootSelectable.Children = new KMSelectable[] { };
	}

	public void PushScreen()
	{
		ClearScreen();
		ScreenStack.Push(new Screen());
		UpdateScreen();
	}

	public void PopScreen()
	{
		ClearScreen();
		ScreenStack.Pop();
		UpdateListings();
	}

	string CleanupSettingsFileName(string fileName)
	{
		return Regex.Replace(fileName.Substring(0, 1).ToUpperInvariant() + fileName.Substring(1), "-settings$", "");
	}

	void ReadSettings()
	{
		SettingsInfo.Clear();

		foreach (string directory in new[] { Application.persistentDataPath, Path.Combine(Application.persistentDataPath, "Modsettings") })
		{
			foreach (string file in Directory.GetFiles(directory))
			{
				// Since we can only read JSON files, avoid reading .xml files.
				if (!new[] { ".txt", ".json" }.Contains(Path.GetExtension(file))) continue;
				if (Path.GetFileName(file) == "output_log.txt") continue;

				string fileText = File.ReadAllText(file);
				Dictionary<string, object> settings = null;
				try // TODO: Handle things that aren't dictionaries, like single values.
				{
					settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileText);
				}
				catch// (Exception exception)
				{
					//Tweaks.Log($"An exception occurred trying to add \"{file}\":");
					//Debug.LogException(exception);
				}

				if (settings == null) continue;

				List<Listing> listings = new List<Listing>();
				foreach (var pair in settings)
				{
					ListingType type;
					switch (pair.Value) // TODO: Support more types like arrays and dictionaries
					{
						case string stringValue:
							// Some mods have "HowToX" fields for documenting stuff.
							if (Regex.IsMatch(pair.Key, @"^(how\s*to|note$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)) continue;

							type = ListingType.String;
							break;
						case double doubleValue:
						case long intValue:
							type = ListingType.Number;
							break;
						case bool boolValue:
							type = ListingType.Checkbox;
							break;
						default:
							//Tweaks.Log("Unsupported type:", pair.Value?.GetType().ToString(), "in:", Path.GetFileName(file));
							continue;
					}

					listings.Add(new Listing(type, pair.Key) { Key = pair.Key });
				}

				if (listings.Count == 0) continue;

				SettingsInfo.Add(new ModSettingsInfo()
				{
					Path = file,
					Name = CleanupSettingsFileName(Path.GetFileNameWithoutExtension(file)),
					Listings = listings
				});
			}
		}
	}

	void ShowMainMenu()
	{
		CurrentScreen.Pinnable = true;

		foreach (ModSettingsInfo info in SettingsInfo)
		{
			string fileName = Path.GetFileName(info.Path);
			Listing listing = new Listing(ListingType.Submenu, info.Name)
			{
				Pinned = Tweaks.settings.PinnedSettings.Contains(fileName)
			};

			listing.Action = (_) =>
			{
				if (PinningEnabled)
				{
					if (listing.Type != ListingType.Submenu) return;

					listing.Pinned = !listing.Pinned;
					if (listing.Pinned) Tweaks.settings.PinnedSettings.Add(fileName);
					else Tweaks.settings.PinnedSettings.Remove(fileName);
					Tweaks.modConfig.Settings = Tweaks.settings;

					UpdateScreen();
				}
				else
				{
					ShowSettings(info);
				}
			};

			CurrentListings.Add(listing);
		}

		UpdateListings();
	}

	void ShowSettings(ModSettingsInfo info)
	{
		PushScreen();

		CurrentScreen.TitleText = info.Name;

		foreach (Listing listing in info.Listings)
		{
			var path = info.Path; // TODO: consider how to handle this better //Path.Combine(Application.persistentDataPath, info.Path);
			var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
			listing.DefaultValue = dictionary[listing.Key];
			listing.Action = obj =>
			{
				dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
				dictionary[listing.Key] = obj;
				File.WriteAllText(path, JsonConvert.SerializeObject(dictionary, Formatting.Indented));
			};

			CurrentListings.Add(listing);
		}

		UpdateListings();
	}

	public void UpdateListings()
	{
		List<KMSelectable> selectables = new List<KMSelectable>();
		for (int i = 0; i < CurrentListings.Count; i++)
		{
			Listing listing = CurrentListings[i];
			GameObject Object = listing.Object ?? MakeListingObject(listing);
			Object.SetActive(true);

			selectables.Add(null);
			selectables.Add(null);
			selectables.Add(listing.TypeSelectable);
		}

		UpdateScreen();

		selectables.Insert(0, PinButton);
		selectables.Insert(1, BackwardButton);
		selectables.Insert(2, ForwardButton);

		SetupSelectables(selectables, true);
	}

	void SetupSelectables(List<KMSelectable> selectables, bool listing = false)
	{
		foreach (KMSelectable selectable in selectables)
		{
			if (selectable == null) continue;

			bool previousState = selectable.gameObject.activeSelf;
			selectable.gameObject.SetActive(true);

			selectable.Parent = RootSelectable;
			selectable.OnCancel = OnCancel;
			selectable.Reproxy();
			selectable.EnsureModHighlightable();

			GameObject highlight = selectable.Highlight.transform.Find("Highlight").gameObject;
			highlight.SetActive(false);
			ReflectedTypes.HighlightField.SetValue(selectable.Highlight.GetComponent<Highlightable>(), highlight);

			// The KMSelectable's enabled state is used to track if it should be interactable.
			if (!selectable.enabled) selectables[selectables.IndexOf(selectable)] = null;
			selectable.SelectableColliders[0].enabled = selectable.enabled;
			selectable.Highlight.gameObject.SetActive(selectable.enabled);

			selectable.gameObject.SetActive(previousState);
		}

		RootSelectable.Children = selectables.ToArray();
		RootSelectable.ChildRowLength = listing ? 3 : 1;
		RootSelectable.Reproxy();
		RootSelectable.UpdateChildren(selectables[listing ? 3 : 0]);
	}

	// TODO: Remove this
	/*/ DEBUG //
	static Material lineMaterial;
    static void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            // Turn on alpha blending
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Turn backface culling off
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // Turn off depth writes
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    public void OnRenderObject()
    {
        CreateLineMaterial();

		if (RootSelectable == null) return;
		foreach (KMSelectable selectable in RootSelectable.Children)
		{
			if (selectable == null) continue;

			GL.PushMatrix();
			lineMaterial.SetPass(0);

			GL.MultMatrix(selectable.transform.localToWorldMatrix);

			// Draw lines
			GL.Begin(GL.LINES);
			GL.Color(selectable.gameObject.activeInHierarchy ? Color.green : Color.gray);

			foreach (Collider collider in selectable.SelectableColliders)
			{
				BoxCollider boxCollider = (BoxCollider) collider;
				if (boxCollider == null) continue;

				Vector3 size = boxCollider.size / 2;
				// Draw the top face
				GL.Vertex3( size.x,  size.y,  size.z);
				GL.Vertex3(-size.x,  size.y,  size.z);

				GL.Vertex3( size.x,  size.y,  size.z);
				GL.Vertex3( size.x,  size.y, -size.z);

				GL.Vertex3(-size.x,  size.y,  size.z);
				GL.Vertex3(-size.x,  size.y, -size.z);

				GL.Vertex3( size.x,  size.y, -size.z);
				GL.Vertex3(-size.x,  size.y, -size.z);

				// Draw the bottom face
				GL.Vertex3( size.x, -size.y,  size.z);
				GL.Vertex3(-size.x, -size.y,  size.z);

				GL.Vertex3( size.x, -size.y,  size.z);
				GL.Vertex3( size.x, -size.y, -size.z);

				GL.Vertex3(-size.x, -size.y,  size.z);
				GL.Vertex3(-size.x, -size.y, -size.z);

				GL.Vertex3( size.x, -size.y, -size.z);
				GL.Vertex3(-size.x, -size.y, -size.z);

				// Connect the top and bottom
				GL.Vertex3( size.x,  size.y,  size.z);
				GL.Vertex3( size.x, -size.y,  size.z);

				GL.Vertex3(-size.x,  size.y,  size.z);
				GL.Vertex3(-size.x, -size.y,  size.z);

				GL.Vertex3( size.x,  size.y, -size.z);
				GL.Vertex3( size.x, -size.y, -size.z);

				GL.Vertex3(-size.x,  size.y, -size.z);
				GL.Vertex3(-size.x, -size.y, -size.z);
			}

			GL.End();
			GL.PopMatrix();
		}
	}
	// DEBUG /*/

	GameObject MakeListingObject(Listing listing)
	{
		var ListingObject = Instantiate(ListingTemplate, ListingTemplate.transform.parent);
		ListingObject.transform.Find("Text").GetComponent<TextMesh>().text = listing.Text;

		string TypeString = listing.Type.ToString();
		if (TypeString == "Dropdown") TypeString = "String";

		Transform TypeTransform = ListingObject.transform.Find(TypeString);
		TypeTransform.gameObject.SetActive(true);
		KMSelectable TypeSelectable = TypeTransform.GetComponent<KMSelectable>();
		listing.TypeSelectable = TypeSelectable;

		listing.Object = ListingObject;

		switch (listing.Type)
		{
			case ListingType.Submenu:
				TypeSelectable.OnInteract += delegate
				{
					listing.Action(null);
					return false;
				};
				break;
			case ListingType.Checkbox:
				new CheckboxInput(listing);
				break;
			case ListingType.Number:
				new NumberInput(listing);
				break;
			case ListingType.String:
				new StringInput(listing);
				break;
			case ListingType.Dropdown:
				new DropdownInput(listing);
				break;
		}

		return ListingObject;
	}

	class SpecialKey
	{
		public string KeyText;
		public Func<string, string> OnInteract;
		public char Character;
	}

	const string keyboardLayout = "`1234567890-=BS\nTBqwertyuiop[]\\\nCLasdfghjkl;'EN\nLSzxcvbnm,./RS\n    SPACEB";
	readonly Dictionary<string, SpecialKey> specialKeys = new Dictionary<string, SpecialKey>()
	{
		["BS"] = new SpecialKey()
		{
			KeyText = "←",
			OnInteract = currentInput => currentInput.Substring(0, Math.Max(currentInput.Length - 1, 0)),
			Character = '\b'
		},
		["TB"] = new SpecialKey()
		{
			KeyText = "⇥",
			OnInteract = currentInput => currentInput += "\t"
		},
		["CL"] = new SpecialKey()
		{
			KeyText = "⇪",
			OnInteract = currentInput => { capsLockEnabled = !capsLockEnabled; return currentInput; }
		},
		["EN"] = new SpecialKey()
		{
			KeyText = "↵",
			OnInteract = currentInput => { Instance.ToggleKeyboard(); return currentInput; },
			Character = '\r'
		},
		["LS"] = new SpecialKey()
		{
			KeyText = "⇧",
			OnInteract = currentInput => { shiftEnabled = !shiftEnabled; return currentInput; }
		},
		["RS"] = new SpecialKey()
		{
			KeyText = "⇧",
			OnInteract = currentInput => { shiftEnabled = !shiftEnabled; return currentInput; }
		},
		["SPACEB"] = new SpecialKey()
		{
			KeyText = "",
			OnInteract = currentInput => currentInput += " "
		}
	};

	static bool capsLockEnabled = false;
	bool CapsLock => System.Windows.Forms.Control.IsKeyLocked(System.Windows.Forms.Keys.CapsLock) || capsLockEnabled;

	static bool shiftEnabled = false;
	const string shiftKeys = "`~1!2@3#4$5%6^7&8*9(0)-_=+[{]}\\|;:'\",<.>/?";
	bool Shifting => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || shiftEnabled;

	string GetShiftKey(string baseKey)
	{
		var shiftKeyIndex = shiftKeys.IndexOf(baseKey) + 1;
		return shiftKeyIndex > 1 ? shiftKeys[shiftKeyIndex].ToString() : baseKey.ToUpperInvariant();
	}

	string ApplyKeyboardModifiers(string baseKey) => Shifting ? GetShiftKey(baseKey) : CapsLock ? baseKey.ToUpper() : baseKey;

	Dictionary<string, TextMesh> KeyboardTextMeshes = new Dictionary<string, TextMesh>();
	List<GameObject> Keys = new List<GameObject>();
	void MakeKeyboard()
	{
		Keyboard.SetActive(true);

		foreach (GameObject oldKeyObject in Keys) Destroy(oldKeyObject);
		Keys.Clear();
		KeyboardTextMeshes.Clear();

		int longestRow = keyboardLayout.Split('\n').Max(row => row.Length);
		float originalX = (1 - (longestRow * 0.055f - 0.05f)) / 2;
		Vector3 keyPosition = new Vector3(originalX, 0.65f, 0);

		List<KMSelectable> selectables = new List<KMSelectable>();

		foreach (Match match in Regex.Matches(keyboardLayout, "([A-Z]+|.|\n)"))
		{
			string key = match.Value;

			if (key == " ")
			{
				keyPosition.x += 0.055f;
			}
			else if (key == "\n")
			{
				keyPosition.x = originalX;
				keyPosition.y -= 0.11f;
			}
			else
			{
				GameObject keyClone = Instantiate(Key.gameObject, Keyboard.transform);
				keyClone.SetActive(true);
				keyClone.transform.localPosition = keyPosition + new Vector3((key.Length - 1) * 0.0275f, 0, 0);
				keyClone.transform.Find("Background").localScale = new Vector3(key.Length + (key.Length - 1) * 0.1f, 1, 1);
				keyClone.transform.Find("FakeHighlight").localScale = new Vector3(key.Length + (key.Length - 1) * 0.1f, 1, 1);
				keyPosition.x += key.Length * 0.055f;

				Keys.Add(keyClone);

				KMSelectable selectable = keyClone.GetComponent<KMSelectable>();
				selectables.Add(selectable);

				var textMesh = keyClone.GetComponentInChildren<TextMesh>();
				if (specialKeys.ContainsKey(key))
				{
					SpecialKey specialKey = specialKeys[key];
					textMesh.characterSize = 0.14f;
					textMesh.text = specialKey.KeyText;
					selectable.OnInteract += () =>
					{
						KeyboardInput = specialKey.OnInteract(KeyboardInput);
						return false;
					};
				}
				else
				{
					textMesh.characterSize = 0.12f;
					textMesh.text = key;
					selectable.OnInteract += () =>
					{
						KeyboardInput += ApplyKeyboardModifiers(key);
						shiftEnabled = false;

						return false;
					};

					KeyboardTextMeshes.Add(key, textMesh);
				}
			}
		}

		SetupSelectables(selectables);

		Key.gameObject.SetActive(false);
		Keyboard.SetActive(false);
	}

	bool lastModifierState = false;
	float scrollAmount = 0;
	void Update()
	{
		// Handle scrolling up and down the settings
		scrollAmount += Input.GetAxis("Mouse ScrollWheel") * 5;
		while (scrollAmount >= 1)
		{
			scrollAmount--;
			if (CurrentPage > 0) CurrentPage--;
		}

		while (scrollAmount <= -1)
		{
			scrollAmount++;
			if (CurrentPage < CurrentListings.Count - 1) CurrentPage++;
		}

		// Don't read keyboard input while the keyboard is closed
		if (!Keyboard.activeSelf) return;

		// TODO: Maybe this shouldn't update every frame. Also make the caret a property.
		Keyboard.transform.Find("InputText").GetComponent<TextMesh>().text = KeyboardInput + (Time.time % 0.8 >= 0.4 ? "|" : " ");

		string inputString = Input.inputString;
		// Input.inputString doesn't capture the tab key so I have to fake it.
		// This doesn't work with repeat fire, but I don't know how I could get the user's settings.
		if (Input.GetKeyDown(KeyCode.Tab)) inputString += "\t";

		foreach (char character in inputString)
		{
			var specialKey = specialKeys.Values.FirstOrDefault(special => special.Character == character);
			if (specialKey != null) KeyboardInput = specialKey.OnInteract(KeyboardInput);
			else KeyboardInput += character;

			shiftEnabled = false;
		}

		bool newModifierState = Shifting || CapsLock;
		if (lastModifierState != newModifierState)
		{
			lastModifierState = newModifierState;

			foreach (var pair in KeyboardTextMeshes) pair.Value.text = ApplyKeyboardModifiers(pair.Key);
		}
	}

	Action<string> KeyboardInputComplete;
	string _keyboardInput;
	string KeyboardInput
	{
		get => _keyboardInput;
		set
		{
			_keyboardInput = value;
			Keyboard.transform.Find("InputText").GetComponent<TextMesh>().text = value + (Time.time % 0.8 >= 0.4 ? "|" : " ");
		}
	}

	public void ToggleKeyboard()
	{
		bool newState = !Keyboard.activeSelf;
		Keyboard.SetActive(newState);

		if (newState)
		{
			PushScreen();
			SetupSelectables(Keyboard.GetComponentsInChildren<KMSelectable>().ToList());
		}
		else
		{
			PopScreen();
			KeyboardInputComplete(KeyboardInput);
		}
	}

	public void GetKeyboardInput(string initalValue, string subtitle, Action<string> callback)
	{
		KeyboardInput = initalValue;

		string title = CurrentScreen.TitleText;
		ToggleKeyboard();
		CurrentScreen.TitleText = title;

		KeyboardInputComplete = (string input) =>
		{
			KeyboardInputComplete = null;
			callback(input);
		};

		TitleTextMesh.text = $"<b>{TitleText}</b>\n<size=16>{subtitle}</size>";
	}
}

class ModSettingsInfo
{
	public string Path;
	public string Name;
	public List<Listing> Listings = new List<Listing>();
}

enum ListingType
{
	Submenu,
	Checkbox,
	String,
	Number,
	Dropdown
}

class Listing
{
	public string Text;
	public ListingType Type;
	public string Key;
	public object DefaultValue = null;
	public Action<object> Action;
	public GameObject Object = null;
	public KMSelectable TypeSelectable = null;
	public List<object> DropdownItems = null;
	public bool Pinned = false;

	public Listing(ListingType Type, string Text)
	{
		this.Type = Type;
		this.Text = Text;
	}
}

class SettingsInput
{
	public Listing Listing;

	public SettingsInput(Listing listing)
	{
		Listing = listing;
	}
}

class CheckboxInput : SettingsInput
{
	private bool _currentValue;

	public bool CurrentValue
	{
		get => _currentValue;
		set
		{
			_currentValue = value;
			Listing.TypeSelectable.transform.Find("Checkmark").gameObject.SetActive(_currentValue);
		}
	}

	public CheckboxInput(Listing listing) : base(listing)
	{
		CurrentValue = (bool) listing.DefaultValue;

		listing.TypeSelectable.OnInteract += delegate
		{
			CurrentValue = !CurrentValue;
			Listing.Action(_currentValue);
			return false;
		};
	}
}

class NumberInput : SettingsInput
{
	private string _currentValue;

	public string CurrentValue
	{
		get => _currentValue;
		set
		{
			_currentValue = value;
			Listing.TypeSelectable.transform.Find("Text").gameObject.GetComponent<TextMesh>().text = _currentValue;
		}
	}

	public NumberInput(Listing listing) : base(listing)
	{
		CurrentValue = listing.DefaultValue.ToString();

		listing.TypeSelectable.OnInteract += delegate
		{
			SettingsPage.Instance.GetKeyboardInput(CurrentValue, listing.Text, OnInputComplete);
			return false;
		};
	}

	public void OnInputComplete(string input)
	{
		switch (Listing.DefaultValue)
		{
			case double doubleValue:
				if (double.TryParse(input, out double doubleResult))
				{
					CurrentValue = input;
					Listing.Action(doubleResult);
				}
				break;
			case long longValue:
				if (long.TryParse(input, out long longResult))
				{
					CurrentValue = input;
					Listing.Action(longResult);
				}
				break;
			default:
				throw new Exception($"Unexpected type \"{Listing.DefaultValue.GetType()}\". Please contact the developer.");
		}
	}
}

class StringInput : SettingsInput
{
	private string _currentValue;

	public string CurrentValue
	{
		get => _currentValue;
		set
		{
			_currentValue = value;
			Listing.TypeSelectable.transform.Find("Text").gameObject.GetComponent<TextMesh>().text = _currentValue;
		}
	}

	public StringInput(Listing listing) : base(listing)
	{
		CurrentValue = (string) listing.DefaultValue;

		listing.TypeSelectable.OnInteract += delegate
		{
			SettingsPage.Instance.GetKeyboardInput(CurrentValue, listing.Text, OnInputComplete);
			return false;
		};
	}

	public void OnInputComplete(string input)
	{
		CurrentValue = input;
		Listing.Action(input);
	}
}

class DropdownInput : SettingsInput
{
	private object _currentValue;

	public object CurrentValue
	{
		get => _currentValue;
		set
		{
			_currentValue = value;
			Listing.TypeSelectable.transform.Find("Text").gameObject.GetComponent<TextMesh>().text = _currentValue.ToString();
		}
	}

	public DropdownInput(Listing listing) : base(listing)
	{
		CurrentValue = listing.DefaultValue;

		listing.TypeSelectable.OnInteract += delegate
		{
			SettingsPage.Instance.PushScreen();
			foreach (object item in listing.DropdownItems)
			{
				SettingsPage.Instance.CurrentListings.Add(
					new Listing(ListingType.Submenu, item.ToString())
					{
						Action = (_) =>
						{
							SettingsPage.Instance.PopScreen();
							CurrentValue = item;
							Listing.Action(item);
						}
					}
				);
			}

			SettingsPage.Instance.UpdateListings();

			return false;
		};
	}
}