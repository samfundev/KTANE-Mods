using static KMGameInfo;

public abstract class Tweak
{
	protected Tweak()
	{
		Tweaks.OnStateChanged += OnStateChange;
	}

	public virtual void OnStateChange(State previousState, State state)
	{
	}
}