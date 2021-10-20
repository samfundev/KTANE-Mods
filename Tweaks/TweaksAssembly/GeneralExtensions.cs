using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class GeneralExtensions
{
	public static bool EqualsAny(this object obj, params object[] targets) => targets.Contains(obj);

	public static string FormatTime(this float seconds)
	{
		bool wasNegative = seconds < 0;
		seconds = Math.Abs(seconds);

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

		string formatedTime = string.Join(":", timeParts.Select((time, i) => timeParts.Count > 2 && i == 0 ? time.ToString() : time.ToString().PadLeft(2, '0')).ToArray());
		if (addMilliseconds) formatedTime += ((int) (seconds * 100)).ToString(@"\.00");

		return (wasNegative ? "-" : "") + formatedTime;
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

	public static T GetKeySafe<T>(this Dictionary<string, object> dictionary, string key)
	{
		return dictionary.ContainsKey(key) ? (T) dictionary[key] : default;
	}

	/// <summary>
	///     Returns the index of the first element in this <paramref name="source"/> satisfying the specified <paramref
	///     name="predicate"/>. If no such elements are found, returns <c>-1</c>.</summary>
	public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));
		if (predicate == null)
			throw new ArgumentNullException(nameof(predicate));
		int index = 0;
		foreach (var v in source)
		{
			if (predicate(v))
				return index;
			index++;
		}
		return -1;
	}

	/// <summary>
	/// Compares the string against a given pattern.
	/// </summary>
	/// <param name="str">The string.</param>
	/// <param name="pattern">The pattern to match, where "*" means any sequence of characters, and "?" means any single character.</param>
	/// <returns><c>true</c> if the string matches the given pattern; otherwise <c>false</c>.</returns>
	public static bool Like(this string str, string pattern)
	{
		if (str == null)
			return false;

		return new Regex(
			"^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
			RegexOptions.IgnoreCase | RegexOptions.Singleline
		).IsMatch(str);
	}

	// https://stackoverflow.com/a/1450889/8213163
	public static IEnumerable<string> ChunkBy(this string str, int maxChunkSize)
	{
		for (int i = 0; i < str.Length; i += maxChunkSize)
			yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
	}

	public static IEnumerable<float> TimedAnimation(this float length)
	{
		float startTime = Time.time;
		float alpha = 0;
		while (alpha < 1)
		{
			alpha = Mathf.Min((Time.time - startTime) / length, 1);
			yield return alpha;
		}
	}

	public static IEnumerable<float> EaseCubic(this IEnumerable<float> enumerable) => enumerable.Select(alpha => 3 * alpha * alpha - 2 * alpha * alpha * alpha);

	public static GameObject Traverse(this GameObject currentObject, params string[] names)
	{
		Transform currentTransform = currentObject.transform;
		foreach (string name in names)
		{
			currentTransform = currentTransform.Find(name);
		}

		return currentTransform.gameObject;
	}

	public static T Traverse<T>(this GameObject currentObject, params string[] names) => currentObject.Traverse(names).GetComponent<T>();

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