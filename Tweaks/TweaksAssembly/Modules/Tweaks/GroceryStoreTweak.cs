class GroceryStoreTweak : ModuleTweak
{
	public GroceryStoreTweak(BombComponent bombComponent) : base(bombComponent, "GroceryStoreBehav")
	{
		// Remove the ability to press the buttons after the module is solved
		bombComponent.OnPass += (_) => {
			for (int i = 0; i < 2; i++)
				bombComponent.GetComponent<KMSelectable>().Children[i].OnInteract = null;
			return false;
		};
	}
}