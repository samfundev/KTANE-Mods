using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class LocalModsConverter : JsonConverter
{
	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		writer.WriteStartArray();

		var list = (List<string>) value;
		for (int i = 0; i < list.Count; i++)
		{
			var item = list[i];
			var path = Path.Combine(Utilities.SteamWorkshopDirectory, item);
			var title = "Unknown";
			if (File.Exists(Path.Combine(path, "modInfo.json")))
			{
				title = ModManager.Instance.GetModInfoFromPath(path, Assets.Scripts.Mods.ModInfo.ModSourceEnum.Invalid).Title;
			}

			writer.CallMethod("WriteIndent");
			writer.WriteRaw($"/* {title} */");
			writer.CallMethod("WriteIndent");
			writer.WriteRaw($"\"{item}\"");

			if (i != list.Count - 1)
				writer.CallMethod("WriteValueDelimiter");
		}

		var _writer = writer.GetValue<TextWriter>("_writer");
		_writer.Write("\n");
		for (var i = 0; i < (writer.GetValue<int>("Top") - 1) * writer.GetValue<int>("_indentation"); i++)
		{
			_writer.Write(" ");
		}

		writer.WriteEndArray();
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		return null;
	}

	public override bool CanConvert(Type objectType) => true;
	public override bool CanRead => false;
}