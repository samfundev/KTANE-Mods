using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections;
using Newtonsoft.Json.Linq;

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
	public TextMesh AlertTextMesh = null;

	public KMSelectable PinButton = null;
	public KMSelectable BackwardButton = null;
	public KMSelectable ForwardButton = null;
	public KMSelectable BackButton = null;

	public List<Texture> Icons = new List<Texture>();

	void SetIcon(Component component, string iconName)
	{
		Transform iconTransform = component.transform.Find("Icon");
		Texture iconTexture = Icons.Find(texture => texture.name == iconName);

		iconTransform.GetComponent<Renderer>().material.SetTexture("_MainTex", iconTexture);
		iconTransform.gameObject.SetActive(iconTexture != null);
	}

	public List<ModSettingsInfo> SettingsInfo = new List<ModSettingsInfo>();

	public List<ModSettingsInfo> PredefinedSettingsInfo = new List<ModSettingsInfo>();

	bool PinningEnabled = false;

	[HideInInspector]
	public KMSelectable RootSelectable = null;

	const int ListingsPerPage = 10;

	public class Screen
	{
		public List<Listing> Listings = new List<Listing>();
		public string TitleText = "Mod Settings";
		public string Subtitle = null;
		public int Page = 0;
		public bool Pinnable = false;
		public bool Sorted = false;
		public Action OnPop;
	}

	public Screen CurrentScreen => ScreenStack.Peek();
	public Stack<Screen> ScreenStack = new Stack<Screen>(new[] { new Screen() });

	public List<Listing> CurrentListings => CurrentScreen.Listings;
	public string TitleText => CurrentScreen.TitleText;

	void UpdateScreen()
	{
		int currentPage = CurrentScreen.Page;

		int maxPage = CurrentListings.Count - ListingsPerPage;
		SetIcon(BackwardButton, $"plain-arrow-left{(currentPage > 0 ? "" : "_grey")}");
		BackwardButton.enabled = currentPage > 0;

		SetIcon(ForwardButton, $"plain-arrow-right{(currentPage < maxPage ? "" : "_grey")}");
		ForwardButton.enabled = currentPage < maxPage;

		SetIcon(PinButton, $"pin{(CurrentScreen.Pinnable ? (PinningEnabled ? "ned" : "") : "_grey")}");
		PinButton.enabled = CurrentScreen.Pinnable;

		PinningEnabled &= CurrentScreen.Pinnable; // If the current screen isn't pinnable but it was enabled, this should disable it.

		var sortedListings = CurrentListings
			.OrderByDescending(listing => listing.Pinned)
			.ThenBy(listing => CurrentScreen.Sorted ? listing.Text : "")
			.ToArray();

		for (int i = 0; i < CurrentListings.Count; i++)
		{
			GameObject Object = sortedListings[i].Object;
			Object.SetActive(i >= currentPage && i <= currentPage + (ListingsPerPage - 1));
			Object.transform.localPosition = new Vector3(-0.5f, 0.33125f - 0.0875f * (i - currentPage), -0.0001f);
			Object.transform.Find("Background").GetComponent<Renderer>().material.color = Color.HSVToRGB(0, 0, sortedListings[i].Type == ListingType.Section ? 0.65f : i % 2 == 0 ? 0.85f : 0.768f);
			Object.transform.Find("FakeHighlight").Find("Highlight").GetComponent<Renderer>().material.color = Color.HSVToRGB(0.5f, 0.458f, i % 2 == 0 ? 1 : 0.918f);
			SetIcon(Object.transform, sortedListings[i].Pinned ? "pinned" : "");
		}

		var subtitle = CurrentScreen.Subtitle ?? $"Page {CurrentPage / ListingsPerPage + 1} of {Mathf.CeilToInt(CurrentListings.Count / (float) ListingsPerPage)}";
		TitleTextMesh.text = $"<b>{TitleText}</b>\n<size=16>{subtitle}</size>";
	}

	public int CurrentPage
	{
		get => CurrentScreen.Page;
		set
		{
			CurrentScreen.Page = Math.Max(value, 0);
			UpdateListings();
		}
	}

	public void OnEnable()
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
		BackButton.gameObject.SetActive(true);

		PinButton.OnInteract = () =>
		{
			PinningEnabled = !PinningEnabled;
			SetIcon(PinButton, PinningEnabled ? "pinned" : "pin");
			return false;
		};

		ForwardButton.OnInteract = () =>
		{
			CurrentPage += ListingsPerPage;
			return false;
		};

		BackwardButton.OnInteract = () =>
		{
			CurrentPage -= ListingsPerPage;
			return false;
		};

		BackButton.OnInteract = () =>
		{
			OnCancel();
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

	public void ResetScreen()
	{
		ClearScreen();
		CurrentListings.Clear();
	}

	public void PushScreen(string subtitle = null)
	{
		ClearScreen();
		ScreenStack.Push(new Screen());
		CurrentScreen.Subtitle = subtitle;
		UpdateScreen();
	}

	public void PopScreen()
	{
		ClearScreen();
		// Pop off the screen and call it's OnPop event, if it has one.
		ScreenStack.Pop().OnPop?.Invoke();
		UpdateListings();
	}

	string CleanupSettingsFileName(string fileName)
	{
		return Regex.Replace(fileName.Substring(0, 1).ToUpperInvariant() + fileName.Substring(1), "-settings$", "");
	}

	void ReadSettings()
	{
		SettingsInfo.Clear();
		PredefinedSettingsInfo.Clear();

		IEnumerable<FieldInfo> SettingFields =
			AppDomain.CurrentDomain
				.GetAssemblies()
				.SelectMany(a => a.GetSafeTypes())
				.SelectMany(a => a.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
				.Where(a => a.Name == "TweaksEditorSettings" && typeof(IEnumerable<Dictionary<string, object>>).IsAssignableFrom(a.FieldType));

		foreach (FieldInfo fieldInfo in SettingFields)
		{
			foreach (Dictionary<string, object> settingsField in (IEnumerable<Dictionary<string, object>>) fieldInfo.GetValue(null))
			{
				try
				{
					ModSettingsInfo modSettingsInfo = new ModSettingsInfo()
					{
						Path = settingsField.GetKeySafe<string>("Filename"),
						Name = settingsField.GetKeySafe<string>("Name"),
					};

					var modListings = settingsField.GetKeySafe<List<Dictionary<string, object>>>("Listings");
					if (modListings != null)
					{
						modSettingsInfo.Listings = modListings.Select(modListing =>
							{
								Listing listing = new Listing(modListing.GetKeySafe<string>("Text"), modListing.GetKeySafe<string>("Key"), modListing.GetKeySafe<string>("Description"));

								try
								{
									string listingType = modListing.GetKeySafe<string>("Type");
									if (listingType != null) listing.Type = (ListingType) Enum.Parse(typeof(ListingType), listingType, true);
									listing.DropdownItems = modListing.GetKeySafe<List<object>>("DropdownItems");

									if (listing.Type == ListingType.Dropdown && listing.DropdownItems == null) throw new Exception("Missing DropdownItems field for Listing type \"Dropdown\".");
									if (listing.Key == null && listing.Type != ListingType.Section) throw new Exception("Missing Key field for Listing.");

									return listing;
								}
								catch (Exception exeception)
								{
									Tweaks.Log($"An exception occurred while loading the listing \"{listing.Key}\" (Text: {listing.Text}) (Desc: {listing.Description}):");
									Debug.LogException(exeception);
									return null;
								}
							})
							.Where(listing => listing != null)
							.ToList();
					}

					PredefinedSettingsInfo.Add(modSettingsInfo);
				}
				catch (Exception exception)
				{
					Tweaks.Log($"An exception occurred while loading predefined settings for \"{fieldInfo.DeclaringType}\":");
					Debug.LogException(exception);
				}
			}
		}

		foreach (string directory in new[] { Application.persistentDataPath, Path.Combine(Application.persistentDataPath, "Modsettings") })
		{
			foreach (string file in Directory.GetFiles(directory))
			{
				// Since we can only read JSON files, avoid reading .xml files.
				if (!new[] { ".txt", ".json" }.Contains(Path.GetExtension(file))) continue;
				if (Path.GetFileName(file) == "output_log.txt") continue;

				Dictionary<string, object> settings = null;
				try // TODO: Handle things that aren't dictionaries, like single values.
				{
					string fileText = File.ReadAllText(file);
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
					ListingType? type = Listing.TypeFromValue(pair.Value);
					if (type == null || (type == ListingType.String && Regex.IsMatch(pair.Key, @"^(how\s*to|note$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)))
					{
						continue;
					}

					listings.Add(new Listing((ListingType) type, pair.Key) { Key = pair.Key });
				}

				if (listings.Count == 0) continue;

				ModSettingsInfo generatedInfo = new ModSettingsInfo()
				{
					Path = file,
					Name = CleanupSettingsFileName(Path.GetFileNameWithoutExtension(file)),
					Listings = listings
				};

				// If we can find a some predefined information, merge the generated info into that.
				ModSettingsInfo baseInfo = PredefinedSettingsInfo.Find(modSettings => modSettings.Path == Path.GetFileName(file));
				SettingsInfo.Add(baseInfo != null ? MergeModSettings(baseInfo, generatedInfo) : generatedInfo);
			}
		}
	}

	// Anything that already exists in predefined won't be overridden by it's conterpart in generated, except for the path.
	// Listings are compared by their .Key and the only other field that's merged is .Name
	ModSettingsInfo MergeModSettings(ModSettingsInfo predefined, ModSettingsInfo generated)
	{
		if (predefined.Name == null) predefined.Name = generated.Name;
		predefined.Path = generated.Path;

		foreach (Listing mergeListing in generated.Listings)
		{
			Listing predefinedListing = predefined.Listings.Find(baseListing => baseListing.Key == mergeListing.Key);
			if (predefinedListing != null)
			{
				if (predefinedListing.Text == null) predefinedListing.Text = mergeListing.Text;
				if (predefinedListing.Type == null) predefinedListing.Type = mergeListing.Type;

				continue;
			}

			predefined.Listings.Add(mergeListing);
		}

		return predefined;
	}

	void ShowMainMenu()
	{
		CurrentScreen.Pinnable = true;
		CurrentScreen.Sorted = true;

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
					if (listing.Pinned) Tweaks.userSettings.PinnedSettings.Add(fileName);
					else Tweaks.userSettings.PinnedSettings.Remove(fileName);
					Tweaks.modConfig.Write(Tweaks.userSettings);

					UpdateScreen();
				}
				else if (!File.Exists(info.Path))
				{
					ShowAlert("The file for those settings is missing and cannot be opened.");
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
			if (listing.Type != ListingType.Section && listing.Key != null)
			{
				var path = info.Path;
				var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
				if (!dictionary.ContainsKey(listing.Key))
					continue;

				listing.DefaultValue = dictionary[listing.Key];
				listing.Action = obj =>
				{
					dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
					dictionary[listing.Key] = obj;
					File.WriteAllText(path, JsonConvert.SerializeObject(dictionary, Formatting.Indented));

					listing.DefaultValue = obj;
				};
			}

			CurrentListings.Add(listing);
		}

		UpdateListings();
	}

	Coroutine alertCoroutine;
	void ShowAlert(string text)
	{
		AlertTextMesh.text = text;

		if (alertCoroutine != null)
			StopCoroutine(alertCoroutine);

		alertCoroutine = StartCoroutine(AlertRoutine());
	}

	IEnumerator AlertRoutine()
	{
		AlertTextMesh.gameObject.SetActive(true);
		yield return new WaitForSeconds(5);
		AlertTextMesh.gameObject.SetActive(false);
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
		selectables.Add(BackButton);

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
		Transform Text = ListingObject.transform.Find("Text");
		Text.GetComponent<TextMesh>().text = (listing.Type == ListingType.Section ? "   " : "") + listing.Text;

		if (listing.Description != null)
		{
			Transform Description = ListingObject.transform.Find("Description");
			Description.GetComponent<TextMesh>().text = listing.Description;
			Description.gameObject.SetActive(true);
			Bounds bounds = Text.GetComponent<Renderer>().bounds;
			Description.position = bounds.center + new Vector3(bounds.extents.x, 0, 0);
			Description.localPosition += new Vector3(0.01f, 0, 0);
		}

		string TypeString = listing.Type.ToString();
		if (TypeString == "Dropdown") TypeString = "String";

		Transform TypeTransform = ListingObject.transform.Find(TypeString);
		TypeTransform.gameObject.SetActive(true);
		KMSelectable TypeSelectable = TypeTransform.GetComponent<KMSelectable>();
		listing.TypeSelectable = TypeSelectable;

		listing.Object = ListingObject;

		listing.Setup();

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
	bool CapsLock => /*System.Windows.Forms.Control.IsKeyLocked(System.Windows.Forms.Keys.CapsLock) || */capsLockEnabled;

	static bool shiftEnabled = false;
	const string shiftKeys = "`~1!2@3#4$5%6^7&8*9(0)-_=+[{]}\\|;:'\",<.>/?";
	bool Shifting => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || shiftEnabled;

	string GetShiftKey(string baseKey)
	{
		var shiftKeyIndex = shiftKeys.IndexOf(baseKey) + 1;
		return shiftKeyIndex > 1 ? shiftKeys[shiftKeyIndex].ToString() : baseKey.ToUpperInvariant();
	}

	string ApplyKeyboardModifiers(string baseKey) => Shifting ? GetShiftKey(baseKey) : CapsLock ? baseKey.ToUpper() : baseKey;

	readonly Dictionary<string, TextMesh> KeyboardTextMeshes = new Dictionary<string, TextMesh>();
	readonly List<GameObject> Keys = new List<GameObject>();
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
	public void Update()
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

		if (Input.GetKeyDown(KeyCode.CapsLock))
		{
			capsLockEnabled = !capsLockEnabled;
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

	public void ToggleKeyboard(string subtitle = null)
	{
		bool newState = !Keyboard.activeSelf;
		Keyboard.SetActive(newState);

		if (newState)
		{
			AlertTextMesh.gameObject.SetActive(false);
			PushScreen(subtitle);
			SetupSelectables(Keyboard.GetComponentsInChildren<KMSelectable>().ToList());
		}
		else
		{
			KeyboardInputComplete(KeyboardInput);
			PopScreen();
		}
	}

	public void GetKeyboardInput(string initalValue, string subtitle, Action<string> callback)
	{
		KeyboardInput = initalValue;

		ToggleKeyboard(subtitle);

		KeyboardInputComplete = (string input) =>
		{
			KeyboardInputComplete = null;
			callback(input);
		};
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
	Dropdown,
	Section,
	Array
}

class Listing
{
	public string Text;
	public string Description;
	public ListingType? Type;
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

	public Listing(string Text, string Key, string Description)
	{
		this.Text = Text;
		this.Key = Key;
		this.Description = Description;
	}

	public static ListingType? TypeFromValue(object value)
	{
		switch (value)
		{
			case string _:
				return ListingType.String;
			case double _:
			case long _:
				return ListingType.Number;
			case bool _:
				return ListingType.Checkbox;
			case JArray _:
				return ListingType.Array;
			default:
				return null;
		}
	}

	internal void Setup()
	{
		switch (Type)
		{
			case ListingType.Submenu:
				TypeSelectable.OnInteract += delegate
				{
					Action(null);
					return false;
				};
				break;
			case ListingType.Checkbox:
				new CheckboxInput(this);
				break;
			case ListingType.Number:
				new NumberInput(this);
				break;
			case ListingType.String:
				new StringInput(this);
				break;
			case ListingType.Dropdown:
				new DropdownInput(this);
				break;
			case ListingType.Array:
				new ArrayInput(this);
				break;
			case ListingType.Section:
				Object.transform.Find("FakeHighlight").gameObject.SetActive(false);
				break;
		}
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
			if (Listing.TypeSelectable != null)
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
				throw new Exception($"Unexpected type \"{Listing.DefaultValue.GetType()}\". Please contact the developer of Tweaks.");
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
			if (Listing.TypeSelectable != null)
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
			SettingsPage.Instance.PushScreen(listing.Text);
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

class ArrayInput : SettingsInput
{
	public List<object> CurrentValue { get; set; }

	enum ArrayMode
	{
		None,
		Delete,
		MoveUp,
		MoveDown
	}

	public ArrayInput(Listing listing) : base(listing)
	{
		CurrentValue = (listing.DefaultValue is JArray jArray) ? jArray.ToObject<List<object>>() : (List<object>) listing.DefaultValue;

		ArrayMode currentMode = ArrayMode.None;

		void displayArray()
		{
			SettingsPage.Instance.ResetScreen();

			for (int j = 0; j < CurrentValue.Count; j++)
			{
				int i = j;
				var item = CurrentValue[i];

				var potentialType = Listing.TypeFromValue(item);
				if (potentialType == null)
					continue;

				ListingType type = (ListingType) potentialType;

				var sublisting = new Listing(type, "Item " + (i + 1))
				{
					DefaultValue = CurrentValue[i]
				};

				sublisting.Action = (newItem) =>
				{
					CurrentValue[i] = newItem;
					sublisting.DefaultValue = newItem;
				};

				SettingsPage.Instance.CurrentListings.Add(sublisting);
			}

			SettingsPage.Instance.CurrentListings.Add(new Listing(ListingType.Submenu, "Add Item")
			{
				Action = (_) =>
				{
					SettingsPage.Instance.PushScreen("Add Item");

					Dictionary<string, object> types = new Dictionary<string, object>()
					{
						{ "Boolean", false },
						{ "Number", 0L },
						{ "String", "" },
						{ "Array", new JArray() },
					};

					foreach (var pair in types)
					{
						SettingsPage.Instance.CurrentListings.Add(new Listing(ListingType.Submenu, pair.Key)
						{
							Action = (__) =>
							{
								CurrentValue.Add(pair.Value);
								SettingsPage.Instance.PopScreen();
								displayArray();
							}
						});
					}

					SettingsPage.Instance.UpdateListings();
				}
			});

			Dictionary<ArrayMode, string> modes = new Dictionary<ArrayMode, string>()
			{
				{ ArrayMode.Delete, "Delete Item" },
				{ ArrayMode.MoveUp, "Move Item Up" },
				{ ArrayMode.MoveDown, "Move Item Down" },
			};

			foreach (var pair in modes)
			{
				var modeActive = pair.Key == currentMode;
				SettingsPage.Instance.CurrentListings.Add(new Listing(ListingType.Submenu, (modeActive ? "<b>" : "") + pair.Value + (modeActive ? "</b> (Click again to cancel)" : ""))
				{
					Action = (_) =>
					{
						currentMode = currentMode == pair.Key ? ArrayMode.None : pair.Key;
						displayArray();
					}
				});
			}

			SettingsPage.Instance.UpdateListings();

			if (currentMode != ArrayMode.None)
			{
				for (int j = 0; j < CurrentValue.Count; j++)
				{
					var i = j;
					var itemListing = SettingsPage.Instance.CurrentListings[i];

					itemListing.TypeSelectable.OnInteract = () =>
					{
						switch (currentMode)
						{
							case ArrayMode.Delete:
								CurrentValue.RemoveAt(i);
								displayArray();
								break;
							case ArrayMode.MoveUp when i != 0:
								var above = CurrentValue[i - 1];
								CurrentValue[i - 1] = CurrentValue[i];
								CurrentValue[i] = above;
								displayArray();
								break;
							case ArrayMode.MoveDown when i != CurrentValue.Count - 1:
								var below = CurrentValue[i + 1];
								CurrentValue[i + 1] = CurrentValue[i];
								CurrentValue[i] = below;
								displayArray();
								break;
						}

						return false;
					};
				}
			}
		}

		listing.TypeSelectable.OnInteract += delegate
		{
			SettingsPage.Instance.PushScreen(listing.Text);
			SettingsPage.Instance.CurrentScreen.OnPop = () => listing.Action(CurrentValue);
			displayArray();

			return false;
		};
	}
}