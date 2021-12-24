using UnityEngine;

class ParliamentTweak : ModuleTweak
{
	public ParliamentTweak(BombComponent bombComponent) : base(bombComponent, "ParliamentModule")
	{
		bombComponent.OnStrike += (_) => {
			// Fixes the bill display color staying green upon striking past stage one
			TextMesh[] displays = component.GetValue<TextMesh[]>("texts");
			displays[0].color = Color.white;

			// Fixes the win/lose buttons staying upon pressing support/oppose on stage three
			component.SetValue("finalStage", false);
			TextMesh[] btns = component.GetValue<TextMesh[]>("finalButtons");
			btns[0].text = "FPTP";
			btns[1].text = "MMP";
			return false;
		};
	}
}