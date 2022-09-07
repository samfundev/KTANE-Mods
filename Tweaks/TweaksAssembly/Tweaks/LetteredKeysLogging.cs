using System.Linq;

public class LetteredKeysLogging : ModuleLogging
{
    public LetteredKeysLogging(BombComponent bombComponent) : base(bombComponent, "LetterKeys", "Letter Keys")
    {

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
        {
            Log($"Lettered Keys logging has been initialized");
        };

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
        {
			Log($"You solved the lettered keys module");
			return false;
		};

		bombComponent.GetComponent<KMBombModule>().OnStrike += () =>
        {
			Log($"You struck on the lettered keys module");
			return false;
        };
	}


}