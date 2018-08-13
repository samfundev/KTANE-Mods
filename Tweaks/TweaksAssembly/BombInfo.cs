using System.Collections.Generic;

public static class BombInfo
{
    public static List<string> GetModuleNames(List<BombComponent> bombComponents)
    {
        List<string> list = new List<string>();
        foreach (BombComponent component in bombComponents)
        {
            if (component.ComponentType != 0 && component.ComponentType != Assets.Scripts.Missions.ComponentTypeEnum.Timer)
            {
                list.Add(component.GetModuleDisplayName());
            }
        }
        return list;
    }

    public static List<string> GetSolvableModuleNames(List<BombComponent> bombComponents)
    {
        List<string> list = new List<string>();
        foreach (BombComponent component in bombComponents)
        {
            if (component.IsSolvable)
            {
                list.Add(component.GetModuleDisplayName());
            }
        }
        return list;
    }

    public static List<string> GetSolvedModuleNames(List<BombComponent> bombComponents)
    {
        List<string> list = new List<string>();
        foreach (BombComponent component in bombComponents)
        {
            if (component.IsSolvable && component.IsSolved) list.Add(component.GetModuleDisplayName());
        }
        return list;
    }

    public static List<string> GetRemainingModuleNames(List<BombComponent> bombComponents)
    {
        var names = GetSolvableModuleNames(bombComponents);
        var solvable = new List<string>(names);
        var solvednames = GetSolvedModuleNames(bombComponents);
        var solved = new List<string>(solvednames);
        for (int i = 0; i < names.Count; i++)
        {
            if (solved.Contains(names[i]))
            {
                solvable.Remove(names[i]);
                solved.Remove(names[i]);
            }
        }
        return solvable;
    }
}
