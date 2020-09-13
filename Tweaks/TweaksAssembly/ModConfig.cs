using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using UnityEngine;

class ModConfig<T>
{
    public ModConfig(string filename, Action<Exception> onRead = null)
    {
		settingsPath = Path.Combine(Path.Combine(Application.persistentDataPath, "Modsettings"), filename + ".json");
		OnRead = onRead;
    }

	private readonly string settingsPath;

	public static string SerializeSettings(T settings)
	{
		return JsonConvert.SerializeObject(settings, Formatting.Indented, new StringEnumConverter());
	}

	private static readonly object settingsFileLock = new object();

	public bool FailedRead;
	public Action<Exception> OnRead;

	public T Settings
    {
        get
        {
            try
            {
				lock(settingsFileLock)
				{
					if (!File.Exists(settingsPath))
					{
						File.WriteAllText(settingsPath, SerializeSettings(Activator.CreateInstance<T>()));
					}

					T deserialized = JsonConvert.DeserializeObject<T>(File.ReadAllText(settingsPath));
					if (deserialized == null)
						throw new Exception("Deserialized null.");

					OnRead?.Invoke(null);
					return deserialized;
				}
			}
			catch (Exception e)
			{
				FailedRead = true;
				OnRead?.Invoke(e);

				Debug.LogFormat("An exception has occurred while attempting to read the settings from {0}\nDefault settings will be used for the type of {1}.", settingsPath, typeof(T).ToString());
				Debug.LogException(e);
				return Activator.CreateInstance<T>();
			}
		}

        set
        {
            if (value.GetType() == typeof(T) && !FailedRead)
            {
				lock (settingsFileLock)
				{
					try
					{
						File.WriteAllText(settingsPath, SerializeSettings(value));
					}
					catch (Exception e)
					{
						Debug.LogFormat("Failed to write to {0}", settingsPath);
						Debug.LogException(e);
					}
				}
			}
        }
    }

    public override string ToString()
    {
        return SerializeSettings(Settings);
    }
}