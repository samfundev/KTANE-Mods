using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Toasts : Tweak
{
	private static GameObject TemplateToast;
	private static readonly List<RectTransform> ActiveToasts = new List<RectTransform>();
	private static bool UpdatingToasts;

	public override void Setup()
	{
		TemplateToast = Tweaks.Instance.gameObject.Traverse("UI", "Toast");
	}

	public static void Make(string message)
	{
		var toast = Object.Instantiate(TemplateToast, TemplateToast.transform.parent, false).GetComponent<RectTransform>();
		toast.gameObject.SetActive(true);

		var textComponent = toast.gameObject.Traverse<UnityEngine.UI.Text>("Background", "Text");
		textComponent.text = message;

		ActiveToasts.Add(toast);
		StartCoroutine(AnimateToast(toast));
		StartCoroutine(PositionToasts());
	}

	private static IEnumerator AnimateToast(RectTransform toast)
	{
		float originalHeight = toast.rect.height;
		foreach (float alpha in 0.75f.TimedAnimation().EaseCubic())
		{
			toast.sizeDelta = new Vector2(toast.sizeDelta.x, originalHeight * alpha);
			yield return null;
		}

		yield return new WaitForSeconds(5);

		foreach (float alpha in 0.75f.TimedAnimation().EaseCubic())
		{
			toast.sizeDelta = new Vector2(toast.sizeDelta.x, originalHeight * (1 - alpha));
			yield return null;
		}

		ActiveToasts.Remove(toast);
		Object.Destroy(toast.gameObject);
	}

	private static IEnumerator PositionToasts()
	{
		if (UpdatingToasts)
			yield break;

		UpdatingToasts = true;

		while (ActiveToasts.Count > 0)
		{
			float y = 0;
			foreach (var toast in ActiveToasts)
			{
				toast.anchoredPosition = new Vector3(0, -y);
				y += toast.rect.height;
			}

			yield return null;
		}

		UpdatingToasts = false;
	}
}