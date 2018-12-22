using System;
using System.Collections.Generic;
using UnityEngine;

class ModSelectorExtension : MonoBehaviour
{
	public static ModSelectorExtension Instance;

	public KMSelectable SettingsPage = null;
	public Texture2D ModSelectorIcon = null;
	public static IDictionary<string, object> ModSelectorAPI = null;
	public static bool AppAdded = false;

	void Start()
	{
		Instance = this;
	}

	public void FindAPI()
	{
		GameObject modSelectorObject = GameObject.Find("ModSelector_Info");
		if (modSelectorObject != null && !AppAdded)
		{
			AppAdded = true;

			ModSelectorAPI = modSelectorObject.GetComponent<IDictionary<string, object>>();

			Action<KMSelectable> addPageMethod = (Action<KMSelectable>) ModSelectorAPI["AddPageMethod"];
			addPageMethod(SettingsPage);

			Action<string, KMSelectable, Texture2D> addHomePageMethod = (Action<string, KMSelectable, Texture2D>) ModSelectorAPI["AddHomePageMethod"];
			addHomePageMethod("Mod Settings", SettingsPage, ModSelectorIcon);
		}
	}
}