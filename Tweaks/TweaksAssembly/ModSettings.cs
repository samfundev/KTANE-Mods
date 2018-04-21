using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

class ModConfig
{
    public ModConfig(string name, Type settingsType)
    {
        _filename = name;
        _settingsType = settingsType;
    }

    string _filename = null;
    Type _settingsType = null;

    string SettingsPath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "Modsettings\\" + _filename + ".json");
        }
    }

    public object Settings
    {
        get
        {
            try
            {
                if (!File.Exists(SettingsPath))
				{
                    File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(Activator.CreateInstance(_settingsType), Formatting.Indented));
                }

                return JsonConvert.DeserializeObject(File.ReadAllText(SettingsPath), _settingsType);
            }
            catch
            {
                return Activator.CreateInstance(_settingsType);
            }
        }

        set
        {
            if (value.GetType() == _settingsType)
            {
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(value, Formatting.Indented));
            }
        }
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(Settings, Formatting.Indented);
    }
}