using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.DossierMenu;

class RetryFromDossier : Tweak
{
	MenuEntry entry;

	public override bool ShouldEnable => true;

	public override void OnStateChange(KMGameInfo.State previousState, KMGameInfo.State state)
	{
		if (state == KMGameInfo.State.Gameplay && ShouldEnable)
		{
			StartCoroutine(AddRetryToDossier());
		}
		else if (entry != null)
		{
			entry.IsHidden = true;
		}
	}

	private IEnumerator AddRetryToDossier()
	{
		yield return null;
		MainMenu mainMenu = UnityEngine.Object.FindObjectOfType<MainMenu>();

		var page = mainMenu.GameplayMenuPage;
		entry = (MenuEntry) typeof(GameplayMenuPage).GetMethod("AddEntry", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(page, new object[] { "Retry", null, HandleSelectRetry, "Retry" });

		var entries = page.GetValue<List<MenuEntry>>("entryList");
		var last = entries.Last();
		entries[entries.Count - 1] = entries[entries.Count - 2];
		entries[entries.Count - 2] = last;

		typeof(GameplayMenuPage).GetMethod("RefreshLayout", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(page, new object[] { });
	}

	private readonly Selectable.OnInteractHandler HandleSelectRetry = () =>
	{
		Tweaks.Log("Retrying...");
		if (SceneManager.Instance.GameplayState != null)
		{
			SceneManager.Instance.GameplayState.ExitState();
		}
		SceneManager.Instance.EnterPostGameState(0, 0, 0, 0, false, () => SceneManager.Instance.EnterGameplayState(isRetry: true));

		return true;
	};
}
