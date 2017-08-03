using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using System.Reflection;

/*
 * [To-do list] 
 * Deal with leaderboards. (Somehow. Maybe disable them? Somehow?)
 * ModSelector Profile support.
 * Add back in the reload button.
 * [Validation]
 * Validate various parts of the json and warn the user if something is wrong.
 */

public static class StringExtension
{
    public static string FixPath(this string path, params object[] args)
    {
        return path.Replace('/', '\\');
    }
}

public class MissionMaker : MonoBehaviour
{
    public KMGameCommands GameCommands;
    public KMGameInfo GameInfo;
    public Button ToggleButton;
    public Button StartButton;
    public GameObject MissionList;
    public GameObject SelectorPanel;
    public InputField SeedField;
    public CanvasGroup UICanvas;
    public MissionName MissionNamePrefab;
    public ToggleGroup toggleGroup;
    
    BindingFlags @public = BindingFlags.Instance | BindingFlags.Public;
    Dictionary<string, CustomGroup> Groups;
    Dictionary<string, MissionName> Missions = new Dictionary<string, MissionName>();
    int MissionSeed;

    private string SelectorDirectory
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "MissionMaker");
        }
    }

    private string MissionDirectory
    {
        get
        {
            return Path.Combine(SelectorDirectory, "Missions");
        }
    }

    private string GroupsDirectory
    {
        get
        {
            return Path.Combine(SelectorDirectory, "Groups");
        }
    }

    private void MakeDirectories()
    {
        Directory.CreateDirectory(SelectorDirectory);
        Directory.CreateDirectory(MissionDirectory);
        Directory.CreateDirectory(GroupsDirectory);
    }

    public static Type FindType(string qualifiedTypeName)
    {
        Type t = Type.GetType(qualifiedTypeName);

        if (t != null)
        {
            return t;
        }
        else
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(qualifiedTypeName);
                if (t != null)
                    return t;
            }
            return null;
        }
    }

    // Based on https://github.com/ashbash1987/ktanemod-modselector/blob/master/Assets/Scripts/ModSelectorService.cs#L891.
    private Type _modManagerType = null;

    private object _modManager = null;
    private object ModManager
    {
        get
        {
            if (_modManager == null)
            {
                _modManagerType = FindType("ModManager");
                if (_modManagerType != null)
                {
                    _modManager = _modManagerType.GetField("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                }
            }

            return _modManager;
        }
    }

    private void LogWarning(string format, params object[] formatting)
    {
        Debug.LogWarningFormat("[MissionMaker] " + format, formatting);
    }

    private List<T> GetAllModObjects<T>()
    {
        return _modManagerType.GetMethod("GetAllModObjects", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(T)).Invoke(ModManager, null) as List<T>;
    }

    private KMComponentPool ConvertPool(CustomMission mission, CustomPool pool)
    {
        KMComponentPool componentpool = new KMComponentPool();
        string Source = pool.Source;

        // If no source is specified try to figure out what the user wanted.
        if (Source == null)
        {
            if (pool.Modules != null)
            {
                Source = "none";
            }
            else if (pool.Group != null)
            {
                Source = "group";
            }
            else if (pool.Pools != null)
            {
                Source = "pools";
            }
            else
            {
                LogWarning("No source was able to be determined for a pool in the mission located at {0}.", mission.Path);
                return null;
            }
        }
        else
        {
            Source = Source.ToLowerInvariant();
        }

        if (Source == "group")
        {
            if (pool.Group == null)
            {
                LogWarning("Source was set to Group but no Group parameter found in mission located at {0}.", mission.Path);
                return null;
            }

            if (Groups.ContainsKey(pool.Group))
            {
                CustomGroup group = Groups[pool.Group];
                pool.Group = null;
                pool.Source = group.Source;
                pool.Mods = group.Mods;
                pool.Base = group.Base;

                if (pool.Blacklist != null)
                {
                    pool.Modules = group.Modules.Except(pool.Blacklist).ToList();
                }
                else
                {
                    pool.Modules = group.Modules;
                }

                Source = pool.Source != null ? pool.Source.ToLowerInvariant() : "none";
            }
            else
            {
                LogWarning("Unable to find group {0} for the mission located at {2}.", pool.Group, mission.Path);
            }
        }

        if (Source == "pools")
        {
            if (pool.Pools == null)
            {
                LogWarning("Source was set to Pools but no Pools parameter found in mission located at {0}.", mission.Path);
                return null;
            }

            componentpool.Count = pool.Count;
            
            HashSet<string> Modules = new HashSet<string>();
            HashSet<KMComponentPool.ComponentTypeEnum> ComponentTypes = new HashSet<KMComponentPool.ComponentTypeEnum>();
            foreach (CustomPool pool2 in pool.Pools)
            {
                KMComponentPool componentpool2 = ConvertPool(mission, pool2);
                foreach (string module in componentpool2.ModTypes)
                {
                    Modules.Add(module);
                }

                foreach (KMComponentPool.ComponentTypeEnum module in componentpool2.ComponentTypes)
                {
                    ComponentTypes.Add(module);
                }
            }

            componentpool.ModTypes = Modules.ToList();
            componentpool.ComponentTypes = ComponentTypes.ToList();

            if (ComponentTypes.Count > 0 && !(Modules.Count > 0))
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Base;
            }
            else if (!(ComponentTypes.Count > 0) && Modules.Count > 0)
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Mods;
            }
            else
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Base | KMComponentPool.ComponentSource.Mods;
            }

            componentpool.SpecialComponentType = KMComponentPool.SpecialComponentTypeEnum.None;
        }

        if (Source == "none")
        {
            if (pool.Modules == null)
            {
                LogWarning("Source was set to None but no Modules parameter found in the mission located at {0}.", mission.Path);
                return null;
            }

            List<string> modded = new List<string>();
            List<KMComponentPool.ComponentTypeEnum> vanilla = new List<KMComponentPool.ComponentTypeEnum>();

            foreach (string module in pool.Modules)
            {
                if (Enum.IsDefined(typeof(KMComponentPool.ComponentTypeEnum), module))
                {
                    if (module != "Empty" && module != "Timer")
                    {
                        vanilla.Add((KMComponentPool.ComponentTypeEnum)Enum.Parse(typeof(KMComponentPool.ComponentTypeEnum), module));
                    }
                }
                else
                {
                    modded.Add(module);
                }
            }

            bool hasModded = modded.Count > 0;
            if (hasModded)
            {
                componentpool.ModTypes = modded;
            }
            bool hasVanilla = vanilla.Count > 0;
            componentpool.ComponentTypes = hasVanilla ? vanilla : new List<KMComponentPool.ComponentTypeEnum>();

            if (hasVanilla && !hasModded)
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Base;
            }
            else if (!hasVanilla && hasModded)
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Mods;
            }
            else
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Base | KMComponentPool.ComponentSource.Mods;
            }

            componentpool.SpecialComponentType = KMComponentPool.SpecialComponentTypeEnum.None;
        }
        else
        {
            if (!pool.Base && !pool.Mods)
            {
                LogWarning("A source besides None was used but has both base and mods disabled in the mission located at {0}. Skipping pool.", mission.Path);
                return null;
            }

            if (pool.Base && !pool.Mods)
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Base;
            }
            else if (!pool.Base && pool.Mods)
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Mods;
            }
            else
            {
                componentpool.AllowedSources = KMComponentPool.ComponentSource.Base | KMComponentPool.ComponentSource.Mods;
            }
            componentpool.ComponentTypes = new List<KMComponentPool.ComponentTypeEnum>();

            if (Source == "solvable")
            {
                if (pool.Mods)
                {
                    componentpool.ModTypes = new List<string>();
                    foreach (KMBombModule module in GetAllModObjects<KMBombModule>())
                    {
                        if (pool.Blacklist == null || !pool.Blacklist.Contains(module.ModuleType))
                        {
                            componentpool.ModTypes.Add(module.ModuleType);
                        }
                    }
                }

                if (pool.Base)
                {
                    componentpool.ComponentTypes = new List<KMComponentPool.ComponentTypeEnum>();
                    foreach (string name in Enum.GetNames(typeof(KMComponentPool.ComponentTypeEnum)))
                    {
                        if (!name.StartsWith("Needy") && name != "Empty" && name != "Timer" && (pool.Blacklist == null || !pool.Blacklist.Contains(name)))
                        {
                            componentpool.ComponentTypes.Add((KMComponentPool.ComponentTypeEnum)Enum.Parse(typeof(KMComponentPool.ComponentTypeEnum), name));
                        }
                    }
                }
            }
            else if (Source == "needy")
            {
                if (pool.Mods)
                {
                    componentpool.ModTypes = new List<string>();
                    foreach (KMNeedyModule module in GetAllModObjects<KMNeedyModule>())
                    {
                        if (pool.Blacklist == null || !pool.Blacklist.Contains(module.ModuleType))
                        {
                            componentpool.ModTypes.Add(module.ModuleType);
                        }
                    }
                }

                if (pool.Base)
                {
                    componentpool.ComponentTypes = new List<KMComponentPool.ComponentTypeEnum>();
                    foreach (string name in Enum.GetNames(typeof(KMComponentPool.ComponentTypeEnum)))
                    {
                        if (name.StartsWith("Needy") && (pool.Blacklist == null || !pool.Blacklist.Contains(name)))
                        {
                            componentpool.ComponentTypes.Add((KMComponentPool.ComponentTypeEnum)Enum.Parse(typeof(KMComponentPool.ComponentTypeEnum), name));
                        }
                    }
                }
            }
            else
            {
                LogWarning("Unkown source format of {0} was found in the mission located at {1}.", pool.Source, mission.Path);
                return null;
            }
        }

        componentpool.Count = pool.Count;

        return componentpool;
    }

    private object CreateMission(CustomMission mission)
    {
        KMMission template = (KMMission)ScriptableObject.CreateInstance(typeof(KMMission));
        template.name = mission.ID;
        template.DisplayName = mission.Name;
        template.PacingEventsEnabled = mission.PacingEvents;

        KMGeneratorSetting generator = new KMGeneratorSetting();
        generator.FrontFaceOnly = mission.FrontFaceOnly;
        generator.NumStrikes = mission.Strikes;
        generator.TimeLimit = mission.TimeLimit;
        generator.TimeBeforeNeedyActivation = mission.TimeBeforeNeedyActivation;

        foreach (var pool in mission.ComponentPools)
        {
            KMComponentPool componentpool = ConvertPool(mission, pool);

            generator.ComponentPools.Add(componentpool);
        }

        template.GeneratorSetting = generator;
        
        var type = FindType("ModMission");
        var instance = ScriptableObject.CreateInstance(type);
        var InstType = instance.GetType();
        InstType.GetMethod("ConfigureFrom", @public).Invoke(instance, new object[] { template, "MissionMaker" });
        InstType.GetProperty("name", @public).SetValue(instance, mission.ID, null);

        //InstType.GetProperty("INVALID_MISSION_ID", BindingFlags.Public | BindingFlags.Static).SetValue(instance, mission.ID, null); // This should block out any kind of records being set for these custom missions.

        return instance;
    }

    IList ModMissions
    {
        get
        {
            if (ModManager != null && _modManagerType != null)
            {
                return (IList)_modManagerType.GetProperty("ModMissions", @public).GetValue(ModManager, null);
            }
            else
            {
                return null;
            }
        }
    }

    MissionName SelectedMission
    {
        get
        {
            Toggle active = toggleGroup.ActiveToggles().FirstOrDefault();
            return (active == null ? null : active.GetComponent<MissionName>());
        }
    }

    void LoadMissionFile(string path)
    {
        string json = File.ReadAllText(path);
        List<CustomMission> custommissions = new List<CustomMission>();
        try
        {
            custommissions = JsonConvert.DeserializeObject<List<CustomMission>>(json);
        }
        catch (Exception)
        {
            try
            {
                custommissions.Add(JsonConvert.DeserializeObject<CustomMission>(json));
            }
            catch (Exception error)
            {
                LogWarning("Unable to read mission at {0}! Error: {1}", path, error.Message);
                return;
            }
        }

        foreach (CustomMission customMission in custommissions)
        {
            customMission.Path = path;
            customMission.ID = path + "_" + customMission.Name;

            MissionName missionName;
            if (Missions.ContainsKey(customMission.ID))
            {
                missionName = Missions[customMission.ID];
            }
            else
            {
                missionName = Instantiate(MissionNamePrefab);
                missionName.gameObject.SetActive(true);
                missionName.transform.SetParent(MissionNamePrefab.transform.parent, false);
            }

            missionName.Name = customMission.Name;
            missionName.Mission = customMission;
            missionName.KMMission = CreateMission(customMission);

            Missions[customMission.ID] = missionName;

            var type = FindType("Assets.Scripts.Missions.MissionManager");
            var instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
            var modmission = instance.GetType().GetMethod("GetMission", @public).Invoke(instance, new object[] { customMission.ID });
            if (modmission == null)
            {
                ModMissions.Add(missionName.KMMission);
            }
            else
            {
                ModMissions[ModMissions.IndexOf(modmission)] = missionName.KMMission;
            }
        }
    }

    void LoadGroupFile(string path)
    {
        string json = File.ReadAllText(path);
        List<CustomGroup> customgroups = new List<CustomGroup>();
        try
        {
            customgroups = JsonConvert.DeserializeObject<List<CustomGroup>>(json);
        }
        catch (Exception)
        {
            try
            {
                customgroups.Add(JsonConvert.DeserializeObject<CustomGroup>(json));
            }
            catch (Exception error)
            {
                LogWarning("Unable to read group at {0}! Error: {1}", path, error.Message);
                return;
            }
        }

        foreach (CustomGroup group in customgroups)
        {
            group.Path = path;
            group.ID = group.ID.ToLowerInvariant();

            if (Groups.ContainsKey(group.ID))
            {
                LogWarning("Unable to add the {0} group in {1} because of ID conflicts with the group in {2} having the same ID.", group.ID, group.Path, Groups[group.ID].Path);
            }
            else
            {
                Groups.Add(group.ID, group);
            }
        }
    }

    void InitialLoad()
    {
        MakeDirectories();
        Groups = new Dictionary<string, CustomGroup>();
        foreach (string file in Directory.GetFiles(GroupsDirectory, "*.json", SearchOption.AllDirectories))
        {
            LoadGroupFile(Path.Combine(GroupsDirectory, file));
        }

        foreach (string file in Directory.GetFiles(MissionDirectory, "*.json", SearchOption.AllDirectories))
        {
            LoadMissionFile(Path.Combine(MissionDirectory, file));
        }

        // Setup file watching
        FileSystemWatcher missionwatcher = new FileSystemWatcher(MissionDirectory, "*.json");
        missionwatcher.IncludeSubdirectories = true;
        missionwatcher.NotifyFilter = NotifyFilters.LastWrite;
        missionwatcher.Created += new FileSystemEventHandler(MissionChanged);
        missionwatcher.Changed += new FileSystemEventHandler(MissionChanged);
        missionwatcher.Deleted += new FileSystemEventHandler(MissionChanged);

        FileSystemWatcher groupwatcher = new FileSystemWatcher(GroupsDirectory, "*.json");
        groupwatcher.IncludeSubdirectories = true;
        groupwatcher.NotifyFilter = NotifyFilters.LastWrite;
        groupwatcher.Created += new FileSystemEventHandler(GroupChanged);
        groupwatcher.Changed += new FileSystemEventHandler(GroupChanged);
        groupwatcher.Deleted += new FileSystemEventHandler(GroupChanged);

        missionwatcher.EnableRaisingEvents = true;
        groupwatcher.EnableRaisingEvents = true;
    }

    void MissionChanged(object source, FileSystemEventArgs e)
    {
        Debug.Log(e.ChangeType.ToString());
        if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
        {
            LoadMissionFile(e.FullPath.FixPath());
        }
        else if (e.ChangeType == WatcherChangeTypes.Deleted)
        {
            foreach (string key in Missions.Keys.ToArray())
            {
                MissionName mission = Missions[key];
                if (mission.Mission.Path.FixPath() == e.FullPath.FixPath())
                {
                    Missions.Remove(key);
                    var type = FindType("Assets.Scripts.Missions.MissionManager");
                    var instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
                    var modmission = instance.GetType().GetMethod("GetMission", @public).Invoke(instance, new object[] { mission.Mission.ID });
                    ModMissions.Remove(modmission);

                    if (SelectedMission == mission)
                    {
                        StartButton.GetComponent<Button>().interactable = false;
                    }
                    
                    Destroy(mission.gameObject);
                    break;
                }
            }
        }
        else
        {
            Debug.Log("[MissionMaker] Unhandled change type encountered: " + e.ChangeType.ToString());
        }
    }

    void GroupChanged(object source, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
        {
            LoadGroupFile(e.FullPath.FixPath());
        }
        else if (e.ChangeType == WatcherChangeTypes.Deleted)
        {
            foreach (string key in Groups.Keys)
            {
                CustomGroup group = Groups[key];
                if (group.Path.FixPath() == e.FullPath.FixPath())
                {
                    Groups.Remove(key);
                    break;
                }
            }
        }
        else
        {
            Debug.Log("[MissionMaker] Unhandled change type encountered: " + e.ChangeType.ToString());
        }
    }

    public void StartMission()
    {
        StartButton.GetComponent<Button>().interactable = false;

        GameCommands.StartMission(SelectedMission.Mission.ID, MissionSeed.ToString());
    }

    IEnumerator WaitForDB()
    {
        Debug.Log("Waiting for the mission database...");
        if (!Application.isEditor)
        {
            yield return new WaitUntil(() => ModMissions != null);
        }
        Debug.Log("Got database!");

        InitialLoad();
    }

    class Tween
    {
        public float _current = 0;
        public float _totalTime;
        public float _start = 0;
        public float _end = 0;

        public float Update(float step)
        {
            _current += step;
            return Mathf.Lerp(_start, _end, _current / _totalTime);
        }

        public Tween(float start, float end, float totalTime)
        {
            _start = start;
            _end = end;
            _totalTime = totalTime;
        }
    }

    void Start()
    {
        StartCoroutine(WaitForDB());

        GameInfo.OnStateChange += delegate (KMGameInfo.State state)
        {
            bool enabled = UIEnabled;
            if (state == KMGameInfo.State.Setup)
            {
                enabled = true;
            }
            else if (state == KMGameInfo.State.Transitioning)
            {
                enabled = false;
            }

            if (enabled != UIEnabled)
            {
                AlphaTween._start = UICanvas.alpha;
                AlphaTween._end = enabled ? 1 : 0;
                AlphaTween._current = 0;
                AlphaTween._totalTime = UIEnabled ? 2 : 1.5f;

                UIEnabled = enabled;
            }
        };

        SeedField.onValueChanged.AddListener(delegate (string value)
        {
            if (value == "")
            {
                MissionSeed = -1;
            }
            else
            {
                MissionSeed = int.Parse(value);
            }
        });
    }

    bool UIEnabled = false;
    Tween AlphaTween = new Tween(1, 0, 0);
    void Update()
    {
        UICanvas.alpha = Application.isEditor ? 1 : AlphaTween.Update(Time.deltaTime);
    }

    public void ToggleUI()
    {
        SelectorPanel.SetActive(!SelectorPanel.activeInHierarchy);
    }
}