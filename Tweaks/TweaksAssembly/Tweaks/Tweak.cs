using static KMGameInfo;

public abstract class Tweak
{
	protected Tweak()
	{
		if (!Enabled)
			return;

		Setup();
		Tweaks.OnStateChanged += OnStateChange;
	}

	public virtual bool Enabled => true;

	public virtual void Setup()
	{
	}

	public virtual void OnStateChange(State previousState, State state)
	{
	}
}