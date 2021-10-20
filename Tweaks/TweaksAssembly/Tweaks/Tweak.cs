using System.Collections;
using TweaksAssembly.Patching;
using UnityEngine;
using static KMGameInfo;

public abstract class Tweak
{
	/// <summary>
	/// Whether this Tweak is currently enabled. Will change to whatever <see cref="ShouldEnable"/> is whenever <see cref="UpdateEnabled"/> is called.
	/// </summary>
	private bool enabled;

	protected Tweak()
	{
		Tweaks.OnStateChanged += (State previousState, State state) =>
		{
			if (enabled)
				OnStateChange(previousState, state);
		};

		SetupPatch.OnTweaksLoadingState += () =>
		{
			if (enabled)
				SetupPatch.LoadingList.Add(OnTweaksLoadingState());
		};

		UpdateEnabled();
	}

	public void UpdateEnabled()
	{
		var newState = ShouldEnable;
		if (enabled == newState)
			return;

		if (newState) Setup();
		else Teardown();

		enabled = newState;
	}

	/// <summary>
	/// A property that should calculate if this Tweak should be enabled.
	/// </summary>
	public virtual bool ShouldEnable => true;

	public virtual void Setup()
	{
	}

	public virtual void Teardown()
	{
	}

	public virtual void OnStateChange(State previousState, State state)
	{
	}

	public virtual IEnumerator OnTweaksLoadingState()
	{
		yield break;
	}

	protected static Coroutine StartCoroutine(IEnumerator routine) => Tweaks.Instance.StartCoroutine(routine);
}