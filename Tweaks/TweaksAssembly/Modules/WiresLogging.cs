using System.Linq;

public class WiresLogging : ModuleLogging
{
    public WiresLogging(BombComponent bombComponent) : base(bombComponent, "WireSetComponent", "WireSetComponent")
    {
		WireSetComponent wireSetComponent = (WireSetComponent) bombComponent;
		Log($"Wire colors: {wireSetComponent.wires.Select(wire => wire.GetColor().ToString()).Join(", ")}");
	}
}