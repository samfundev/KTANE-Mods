namespace TweaksAssembly.Modules.Tweaks
{
	internal class ShapeShiftLogging : ModuleLogging
	{
		private readonly string[] shapes = new[] { "Flat", "Round", "Point", "Ticket" };

		public ShapeShiftLogging(BombComponent bombComponent) : base(bombComponent, "ShapeShiftModule", "Shape Shift")
		{
			bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
			{
				string startLeft = shapes[component.GetValue<int>("startL")];
				string startRight = shapes[component.GetValue<int>("startR")];

				string endLeft = shapes[component.GetValue<int>("solutionL")];
				string endRight = shapes[component.GetValue<int>("solutionR")];

				Log($"Starting: {startLeft} {startRight}");
				Log($"Solution: {endLeft} {endRight}");
			};

			bombComponent.GetComponent<KMBombModule>().OnStrike += () =>
			{
				string currentLeft = shapes[component.GetValue<int>("displayL")];
				string currentRight = shapes[component.GetValue<int>("displayR")];

				Log($"Strike! Submitted: {currentLeft} {currentRight}");

				return false;
			};

			bombComponent.GetComponent<KMBombModule>().OnPass += () =>
			{
				Log("Module Solved");

				return false;
			};
		}
	}
}
