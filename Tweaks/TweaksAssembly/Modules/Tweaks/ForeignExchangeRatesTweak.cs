class ForeignExchangeRatesTweak : ModuleTweak
{
	public ForeignExchangeRatesTweak(BombComponent bombComponent) : base(bombComponent, "ForeignExchangeRates")
	{
		// Attempt to change the API currency endpoint in the module to a valid one
		component.SetValue("CURRENCY_API_ENDPOINT", "https://fer.eltrick.uk");
	}
}