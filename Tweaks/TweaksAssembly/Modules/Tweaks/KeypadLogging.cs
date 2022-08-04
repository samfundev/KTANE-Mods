using System.Linq;

public class KeypadLogging : ModuleLogging
{
    public KeypadLogging(BombComponent bombComponent) : base(bombComponent, "KeypadComponent", "KeypadComponent")
    {
		KeypadComponent keypadComponent = (KeypadComponent) bombComponent;
		Log($"Symbols: {keypadComponent.buttons.Select(button => button.GetValue()).Join(", ")}");
	}
}