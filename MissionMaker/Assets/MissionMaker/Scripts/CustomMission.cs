using System.Collections.Generic;

public class CustomMission
{
    public string Name;
    public string ID = "";
    public string Path = "";

    public List<CustomPool> ComponentPools;

    public int Strikes;
    public int TimeLimit;
    public int TimeBeforeNeedyActivation;
    public bool PacingEvents;
    public bool FrontFaceOnly;
}
