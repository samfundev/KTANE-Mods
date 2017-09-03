using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundTester : MonoBehaviour
{
	public KMAudio Audio;
	public Text SoundName;

	int currentSoundIndex = 0;

	List<KMAudio.KMAudioRef> currentRefs = new List<KMAudio.KMAudioRef>();
	
	void Update ()
	{
		Type SoundEffect = typeof(KMSoundOverride.SoundEffect);
		string[] SoundNames = Enum.GetNames(SoundEffect);

		if (Input.GetKeyDown(KeyCode.LeftArrow) && currentSoundIndex > 0)
		{
			currentSoundIndex -= 1;
		}

		if (Input.GetKeyDown(KeyCode.RightArrow) && currentSoundIndex < SoundNames.Length - 1)
		{
			currentSoundIndex += 1;
		}

		SoundName.text = SoundNames[currentSoundIndex];

		if (Input.GetKeyDown(KeyCode.P))
		{
			currentRefs.Add(Audio.PlayGameSoundAtTransformWithRef((KMSoundOverride.SoundEffect) Enum.Parse(SoundEffect, SoundNames[currentSoundIndex]), transform));
		}

		if (Input.GetKeyDown(KeyCode.S) && currentRefs.Count > 0)
		{
			currentRefs.ForEach(x => x.StopSound());
			currentRefs.Clear();
		}
	}
}
