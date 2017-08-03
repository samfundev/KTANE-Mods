using UnityEngine;
using UnityEngine.UI;

public class MissionName : MonoBehaviour
{
	public Text text;
    public CustomMission Mission;
    public object KMMission;

    public string Name
	{
		get
		{
			return text.text;
		}
		set
		{
			text.text = value;
		}
	}
}
