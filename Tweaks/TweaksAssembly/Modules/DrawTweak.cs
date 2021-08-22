using UnityEngine;

class DrawTweak : ModuleTweak
{
	public DrawTweak(BombComponent bombComponent) : base(bombComponent, "DrawBehav")
	{
		// Set _isActive to false and force the material of the screen to be black when successfully doing the needy
		bombComponent.GetComponent<KMNeedyModule>().OnPass += () => {
			component.SetValue("_isActive", false);

			Material passed = component.GetValue<Material>("screenMat");
			passed.color = Color.black;
			component.SetValue("screenMat", passed);
			return false;
		};

		// Force the material of the screen to be black when the timer expires and stops the music if it is playing
		bombComponent.GetComponent<KMNeedyModule>().OnTimerExpired += () => {
			Material passed = component.GetValue<Material>("screenMat");
			passed.color = Color.black;
			component.SetValue("screenMat", passed);

			foreach (DarkTonic.MasterAudio.SoundGroupVariation snd in DarkTonic.MasterAudio.MasterAudio.GetAllPlayingVariationsOfTransform(bombComponent.transform))
			{
				if (snd.SoundGroupName.Equals("draw_music"))
					snd.Stop();
			}
		};

		// Stops the music from playing when fire was pressed
		bombComponent.GetComponent<KMSelectable>().Children[0].OnInteract += () =>
		{
			foreach (DarkTonic.MasterAudio.SoundGroupVariation snd in DarkTonic.MasterAudio.MasterAudio.GetAllPlayingVariationsOfTransform(bombComponent.transform))
			{
				if (snd.SoundGroupName.Equals("draw_music"))
					snd.Stop();
			}
			return false;
		};
	}
}