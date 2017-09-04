using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BetterModSettings;

[RequireComponent(typeof(KMService))]
public class SoundpackMaker : MonoBehaviour
{
	private string SoundsDirectory
	{
		get
		{
			return Path.Combine(Application.persistentDataPath, "Soundpacks");
		}
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


	void Log(params object[] objects)
	{
		Debug.LogFormat("[SoundpackMaker] " + string.Join(", ", objects.Select(obj => Convert.ToString(obj)).ToArray()));
	}

	void Log(string text, params object[] formatting)
	{
		Debug.LogFormat("[SoundpackMaker] " + text, formatting);
	}

	Type SoundEffect = typeof(KMSoundOverride.SoundEffect);
	string[] audioExtensions = new string[] {
		".wav",
		".mp3",
		".ogg"
	};

	AudioClip MakeAudioClip(string path)
	{
		string ext = Path.GetExtension(path);
		if (audioExtensions.Contains(ext))
		{
		    try
		    {
		        if (ext == ".mp3")
		        {
		            return NAudioPlayer.FromMp3Data(new WWW("file:///" + path).bytes);
		        }
		        else
		        {
		            AudioClip clip = new WWW("file:///" + path).audioClip;
		            while (clip.loadState != AudioDataLoadState.Loaded)
		            {
		            }
		            return clip;
		        }
		    }
		    catch (Exception ex)
		    {
		        Log("Failed to load sound sound file {0} due to Exception: {1}\nStack Trace {2}", path, ex.Source, ex.StackTrace);
		    }
		}
		return null;
	}

	// Case insensitve version of Enum.IsDefined
	bool IsDefined(Type enumType, string name)
	{
		return Enum.GetNames(enumType).Any(enumName => enumName.ToLowerInvariant() == name.ToLowerInvariant());
	}

	List<KMSoundOverride> LoadSoundpack(string soundpackDirectory)
	{
		List<KMSoundOverride> Overrides = new List<KMSoundOverride>();

		foreach (string file in Directory.GetFiles(soundpackDirectory))
		{
			string fileName = Path.GetFileNameWithoutExtension(file);
			if (IsDefined(SoundEffect, fileName))
			{
			    Log("Creating AudioClip for {0}.", fileName);
				AudioClip clip = MakeAudioClip(file);
				if (clip)
				{
					clip.name = Path.GetFileName(file);

					KMSoundOverride soundOverride = new GameObject().AddComponent<KMSoundOverride>();
					soundOverride.OverrideEffect = (KMSoundOverride.SoundEffect) Enum.Parse(SoundEffect, fileName, true);
					soundOverride.AudioClip = clip;
					Overrides.Add(soundOverride);
				}
				else
				{
					Log("Failed to create an AudioClip for {0}. Skipping.", fileName);
				}
			}
			else
			{
				Log("{0} isn't a valid sound effect. Skipping.", fileName);
			}
		}

		foreach (string directory in Directory.GetDirectories(soundpackDirectory))
		{
			string dirName = new DirectoryInfo(directory).Name;
			if (IsDefined(SoundEffect, dirName))
			{
				KMSoundOverride soundOverride = new GameObject().AddComponent<KMSoundOverride>();
				soundOverride.OverrideEffect = (KMSoundOverride.SoundEffect) Enum.Parse(SoundEffect, dirName, true);
				List<AudioClip> audioClips = new List<AudioClip>();

				foreach (string file in Directory.GetFiles(directory))
				{
                    Log("Creating AudioClip for {0}\\{1}.", dirName, Path.GetFileName(file));
					AudioClip clip = MakeAudioClip(file);
					if (clip)
					{
						clip.name = Path.GetFileName(file);

						if (!soundOverride.AudioClip)
						{
							soundOverride.AudioClip = clip;
						}
						else
						{
							audioClips.Add(clip);
						}
					}
					else
					{
						Log("Failed to create an AudioClip for {0}\\{1}. Skipping.", dirName, Path.GetFileName(file));
					}
				}

				if (soundOverride.AudioClip)
				{
					if (audioClips.Count > 0)
					{
						soundOverride.AdditionalVariants = audioClips.ToArray();
					}

					Overrides.Add(soundOverride);
				}
			}
			else
			{
				Log("{0} isn't a valid sound effect. Skipping.", dirName);
			}
		}

		return Overrides;
	}

	void Start()
	{
		ModSettings Soundpacks = new ModSettings("EnabledSoundpacks", typeof(List<string>));
		List<string> enabledSoundpacks = (List<string>) Soundpacks.Settings;

		if (!Directory.Exists(SoundsDirectory))
		{
			Log("Created the Soundpacks directory, not loading any soundpacks.");

			Directory.CreateDirectory(SoundsDirectory);
			return;
		}

		Type Mod = FindType("Mod");
		MethodInfo HandleSoundOverride = Mod.GetMethod("HandleSoundOverride", BindingFlags.NonPublic | BindingFlags.Instance);
		object fakeMod = Activator.CreateInstance(Mod, new object[] { Guid.NewGuid().ToString() });
	    Dictionary<KMSoundOverride.SoundEffect, KMSoundOverride> soundOverrides = new Dictionary<KMSoundOverride.SoundEffect, KMSoundOverride>();
		// Add the new sound effects.
		foreach (string soundpackName in enabledSoundpacks)
		{
			string soundpackDirectory = Path.Combine(SoundsDirectory, soundpackName);
			if (Directory.Exists(soundpackDirectory))
			{
				Log("Adding soundpack: {0}", soundpackName);
				foreach (KMSoundOverride soundOverride in LoadSoundpack(soundpackDirectory))
				{
					//
				    if (soundOverrides.ContainsKey(soundOverride.OverrideEffect))
				    {
				        KMSoundOverride sOverride = soundOverrides[soundOverride.OverrideEffect];
				        List<AudioClip> clips = new List<AudioClip>();
				        clips.Add(soundOverride.AudioClip);
				        if (soundOverride.AdditionalVariants != null)
				        {
				            clips.AddRange(soundOverride.AdditionalVariants);
				        }
				        if (sOverride.AdditionalVariants != null)
				        {
				            clips.AddRange(sOverride.AdditionalVariants);
				        }
				        sOverride.AdditionalVariants = clips.ToArray();
				        soundOverride.AdditionalVariants = null;
				        soundOverride.AudioClip = null;
                        Destroy(soundOverride);
				    }
				    else
				    {
				        soundOverrides[soundOverride.OverrideEffect] = soundOverride;
				    }
				}
			} else
			{
				Log("There is no soundpack called \"{0}\"", soundpackName);
			}
		}
	    foreach (KMSoundOverride soundOverride in soundOverrides.Values)
	    {
	        HandleSoundOverride.Invoke(fakeMod, new object[] { soundOverride });
        }
	}
}
