namespace TweaksAssembly.Modules.Tweaks
{
	internal class TheCodeLogging : ModuleLogging
	{
		public TheCodeLogging(BombComponent bombComponent) : base(bombComponent, "TheCodeModule", "The Code") { }

		public override int GetModuleID() => component.GetValue<int>("_moduleId");
	}
}
