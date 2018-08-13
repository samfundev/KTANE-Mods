using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using UnityEngine;

[JsonConverter(typeof(StringEnumConverter))]
class ModConfig<T>
{
    public ModConfig(string name)
    {
        _filename = name;
    }

    string _filename = null;
    
    string SettingsPath
    {
        get
        {
            return Path.Combine(Path.Combine(Application.persistentDataPath, "Modsettings"), _filename + ".json");
		}
    }

	private string SerializeSettings(T settings)
	{
		return JsonConvert.SerializeObject(settings, Formatting.Indented, new StringEnumConverter());
	}

    public T Settings
    {
        get
        {
            try
            {
                if (!File.Exists(SettingsPath))
				{
                    File.WriteAllText(SettingsPath, SerializeSettings(Activator.CreateInstance<T>()));
                }
				
                T deserialized = JsonConvert.DeserializeObject<T>(File.ReadAllText(SettingsPath));
				return deserialized != null ? deserialized : Activator.CreateInstance<T>();
            }
            catch
            {
                return Activator.CreateInstance<T>();
            }
        }

        set
        {
            if (value.GetType() == typeof(T))
            {
                File.WriteAllText(SettingsPath, SerializeSettings(value));
            }
        }
    }

    public override string ToString()
    {
        return SerializeSettings(Settings);
    }
}