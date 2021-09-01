using static KMGameInfo;

public abstract class Tweak
{
	protected Tweak()
	{
		if (!Enabled)
			return;

		Setup();
		Tweaks.OnStateChanged += OnStateChange;
		SetupPatch.OnTweaksLoadingState += () => SetupPatch.LoadingList.Add(OnTweaksLoadingState());
	}

	public virtual bool Enabled => true;

	public virtual void Setup()
	{
	}

	public virtual void OnStateChange(State previousState, State state)
	{
	}

	public virtual IEnumerator OnTweaksLoadingState()
	{
		yield break;
	}
}