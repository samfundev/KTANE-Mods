using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class GeneralExtensions
{
	public static string FormatTime(this float seconds)
	{
		bool addMilliseconds = seconds < 60;
		int[] timeLengths = { 86400, 3600, 60, 1 };
		List<int> timeParts = new List<int>();

		if (seconds < 1)
		{
			timeParts.Add(0);
		}
		else
		{
			foreach (int timeLength in timeLengths)
			{
				int time = (int) (seconds / timeLength);
				if (time > 0 || timeParts.Count > 0)
				{
					timeParts.Add(time);
					seconds -= time * timeLength;
				}
			}
		}

		string formatedTime = string.Join(":", timeParts.Select((time, i) => timeParts.Count > 2 && i == 0 ? time.ToString() : time.ToString("00")).ToArray());
		if (addMilliseconds) formatedTime += ((int) (seconds * 100)).ToString(@"\.00");

		return formatedTime;
	}

	public static string Join<T>(this IEnumerable<T> values, string separator = " ")
	{
		StringBuilder stringBuilder = new StringBuilder();
		IEnumerator<T> enumerator = values.GetEnumerator();
		if (enumerator.MoveNext()) stringBuilder.Append(enumerator.Current); else return "";

		while (enumerator.MoveNext()) stringBuilder.Append(separator).Append(enumerator.Current);

		return stringBuilder.ToString();
	}

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        return source.OrderBy(_ => UnityEngine.Random.value);
    }

	public static T GetKeySafe<T>(this Dictionary<string, object> dictionary, string key) {
		if (dictionary.ContainsKey(key))
		{
			return (T) dictionary[key];
		}

		return default(T);
	}

	/// <summary>
	/// Compares the string against a given pattern.
	/// </summary>
	/// <param name="str">The string.</param>
	/// <param name="pattern">The pattern to match, where "*" means any sequence of characters, and "?" means any single character.</param>
	/// <returns><c>true</c> if the string matches the given pattern; otherwise <c>false</c>.</returns>
	public static bool Like(this string str, string pattern)
	{
		return new Regex(
			"^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
			RegexOptions.IgnoreCase | RegexOptions.Singleline
		).IsMatch(str);
	}

	public static void EnsureModHighlightable(this KMSelectable selectable)
	{
		KMHighlightable highlightable = selectable.Highlight;
		if (highlightable != null)
		{
			ModHighlightable modHighlightable = highlightable.GetComponent<ModHighlightable>();
			if (modHighlightable == null)
			{
				highlightable.gameObject.AddComponent<ModHighlightable>();
			}
		}
	}

	public static void Reproxy(this KMSelectable selectable)
	{
		ModSelectable modSelectable = selectable.GetComponent<ModSelectable>();
		if (modSelectable != null)
		{
			modSelectable.CopySettingsFromProxy();
		}
	}
}