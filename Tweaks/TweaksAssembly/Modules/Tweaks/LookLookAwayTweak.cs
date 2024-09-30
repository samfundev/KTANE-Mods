using UnityEngine;

class LookLookAwayTweak : ModuleTweak
{
	public LookLookAwayTweak(BombComponent bombComponent) : base(bombComponent, "lookLookAwayScript")
	{
		// Fixes the visibility cube's position being changed by bomb scaling
		Transform visCube = bombComponent.transform.GetChild(0);
		visCube.transform.localPosition = new Vector3(0, 0.8333333f / bombComponent.transform.lossyScale.x * .9f, 0);
	}
}