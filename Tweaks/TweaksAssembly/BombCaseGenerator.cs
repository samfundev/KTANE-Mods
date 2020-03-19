using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/* BombCaseGenerator.cs
 * Original File: BombCasingGen.cs
 * Original Author: Trainzack
 * https://github.com/Trainzack/KTANEReasonableConclusion
 * 
 * Massive credit to them for making the original code and assets,
 * saving me a lot time trying to get case generation correct.
 */

public class BombCaseGenerator : MonoBehaviour
{
    public GameObject Bomb_Backing;
    public GameObject Empty_Bomb;
    public GameObject Cross_Bar;

    public float offset = 0.22f;

    public GameObject GenerateCase(Vector2 size, Transform parent)
    {
        float halfX = size.x / 2;
		float halfY = size.y / 2;
        GameObject bomb = Instantiate(Empty_Bomb, parent);
        bomb.name = size.x + "x" + size.y + " Casing (" + (size.x * size.y * 2 - 1) + " modules)";
        Casing casing = bomb.GetComponent<Casing>();
        Transform visual_transform = casing.Visual;
        KMBombFace front_face = casing.Front.GetComponent<KMBombFace>();
        KMBombFace rear_face = casing.Back.GetComponent<KMBombFace>();

        front_face.Anchors = new List<Transform>();
        front_face.Backings = new List<KMModuleBacking>();
        front_face.GetComponent<KMSelectable>().ChildRowLength = (int) size.x;
        rear_face.Anchors = new List<Transform>();
        rear_face.Backings = new List<KMModuleBacking>();
        rear_face.GetComponent<KMSelectable>().ChildRowLength = (int) size.x;

        casing.Distance_Collider.size = new Vector3(size.x * 0.23f, 0.20f, size.y * 0.23f);
        casing.Selectable_Area.size = new Vector3(size.x * 0.24f, size.y * 0.24f, 0.22f);
        casing.Selectable_Area.transform.Translate(0, -0.25f, 0);

        casing.Highlight.localScale = new Vector3(size.x * 0.24f, size.y * 0.24f, 0.22f);

        // casing.Body.localScale = new Vector3(size * 0.23f, 0.18f, size * 0.23f);

        const float crossbar_width = 0.025f;
        const float widget_offset = 0.22f;
        const float widget_constant_offset = crossbar_width + 0.00275f;

		//Make the widget anchors
		for (int w = 0; w < size.x; w++)
		{
			Transform Bface = new GameObject().GetComponent<Transform>();
			Bface.Translate(new Vector3(offset * (w - halfX + 0.5f), 0.0f, 0.0f));
			Bface.Rotate(-90, 0, 0);
			Bface.SetParent(casing.W_Bottom);
			Bface.localScale = new Vector3(0.12f, 0.03f, 0.17f);
			Bface.name = "Bottom Face";
			bomb.GetComponent<KMBomb>().WidgetAreas.Add(Bface.gameObject);

			Transform Tface = new GameObject().GetComponent<Transform>();
			Tface.Translate(new Vector3(offset * (w - halfX + 0.5f), 0.0f, 0.0f));
			Tface.Rotate(-90, 180, 0);
			Tface.SetParent(casing.W_Top);
			Tface.localScale = new Vector3(0.12f, 0.03f, 0.17f);
			Tface.name = "Top Face";
			bomb.GetComponent<KMBomb>().WidgetAreas.Add(Tface.gameObject);
		}

		for (int w = 0; w < size.y; w++)
		{
			Transform Lface = new GameObject().GetComponent<Transform>();
            Lface.Translate(new Vector3(0.0f, 0.0f, offset * (w - halfY + 0.5f)));
            Lface.Rotate(-90, 90, 0);
            Lface.SetParent(casing.W_Left);
            Lface.localScale = new Vector3(0.12f, 0.03f, 0.17f);
            Lface.name = "Left Face";
            bomb.GetComponent<KMBomb>().WidgetAreas.Add(Lface.gameObject);

            Transform Rface = new GameObject().GetComponent<Transform>();
            Rface.Translate(new Vector3(0.0f, 0.0f, offset * (w - halfY + 0.5f)));
            Rface.Rotate(-90, -90, 0);
            Rface.SetParent(casing.W_Right);
            Rface.localScale = new Vector3(0.12f, 0.03f, 0.17f);
            Rface.name = "Right Face";
            bomb.GetComponent<KMBomb>().WidgetAreas.Add(Rface.gameObject);
        }

        casing.W_Bottom.Translate(new Vector3(0, 0, size.y * -widget_offset / 2 - widget_constant_offset),Space.World);
        casing.W_Top.Translate(new Vector3(0, 0, size.y * widget_offset / 2 + widget_constant_offset), Space.World);
        casing.W_Left.Translate(new Vector3(size.x * -widget_offset / 2 - widget_constant_offset, 0, 0), Space.World);
        casing.W_Right.Translate(new Vector3(size.x * widget_offset / 2 + widget_constant_offset, 0, 0), Space.World);

		//Generate the crossbars.
		if (Cross_Bar.GetComponent<ExcludeFromTexturePack>() == null) Cross_Bar.AddComponent<ExcludeFromTexturePack>();
		var renderer = Cross_Bar.GetComponent<Renderer>();
		renderer.material = new Material(renderer.sharedMaterial);
		renderer.sharedMaterial.color = MakeCaseColor(Tweaks.settings.CaseColors);

		for (int i = 0; i <= size.x; i++)
		{
			Transform CrossBar1 = Instantiate(Cross_Bar).GetComponent<Transform>();
			CrossBar1.SetParent(visual_transform);
			CrossBar1.localScale = new Vector3(crossbar_width, 0.22f, size.y * 0.22f + crossbar_width * ((i == 0 || i == size.x) ? 1 : -1));
			CrossBar1.Translate(new Vector3(offset * (i - halfX), 0, -0));
		}

		for (int i = 0; i <= size.y; i++)
		{
			Transform CrossBar2 = Instantiate(Cross_Bar).GetComponent<Transform>();
            CrossBar2.SetParent(visual_transform);
            CrossBar2.localScale = new Vector3(size.x * 0.22f + crossbar_width * ((i == 0 || i == size.y) ? 1 : -1), 0.22f, crossbar_width) - new Vector3(0.0001f, 0.0001f, 0.0001f); // Subtracted 0.0001 to prevent Z-fighting.
            CrossBar2.Translate(new Vector3(0, 0, offset * (i - halfY)));
		}

        // Generate The module backings and anchors
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                GameObject front_backing = Instantiate(Bomb_Backing);    // Grab the prefab
                Transform f = front_backing.GetComponent<Transform>();
                f.SetParent(casing.Faces_F);
                f.Translate(new Vector3(offset * (x - halfX + 0.5f), offset * (y - halfY + 0.5f), -0.06f));
                f.name = "Bomb_Foam_" + x + "_" + y + "_F";
                Transform f_anchor = new GameObject().GetComponent<Transform>();    // We need to rotate the anchor relative to the backing, so we need a new transform
                f_anchor.position = f.position;
                f_anchor.parent = f;
                f_anchor.Translate(0, 0.03f, 0);    // Move the modules out of the backing
                f_anchor.Rotate(new Vector3(0, 0, 0));
                f_anchor.name = "Anchor";
                front_face.Anchors.Add(f_anchor);
                front_face.Backings.Add(front_backing.GetComponent<KMModuleBacking>());
                // And do it all again for the back face
                GameObject rear_backing = Instantiate(Bomb_Backing);
                Transform r = rear_backing.GetComponent<Transform>();
                r.SetParent(casing.Faces_R);
                r.Translate(new Vector3(offset * (x - halfX + 0.5f), offset * (y - halfY + 0.5f), 0.06f));
                r.Rotate(new Vector3(0, 180, 0));
                r.name = "Bomb_Foam_" + x + "_" + y + "_R";
                Transform r_anchor = new GameObject().GetComponent<Transform>();
                r_anchor.position = r.position;
                r_anchor.parent = r;
                r_anchor.Translate(0, -0.03f, 0);
                r_anchor.Rotate(new Vector3(0, 0, 180));
                r_anchor.name = "Anchor";
                rear_face.Anchors.Add(r_anchor);
                rear_face.Backings.Add(rear_backing.GetComponent<KMModuleBacking>());
            }
        }
        bomb.GetComponent<KMBomb>().Scale = Mathf.Min(2.2f / Mathf.Max(size.x, size.y), 1);

		foreach (KMSelectable selectable in bomb.GetComponentsInChildren<KMSelectable>())
			selectable.gameObject.AddComponent<ModSelectable>();

		bomb.AddComponent<ModBomb>();

		return bomb;
    }

	Color MakeCaseColor(List<string> colorStrings)
	{
		foreach (string colorString in colorStrings.Shuffle())
		{
			string[] colorParts = colorString.Split(new[] { "-" }, System.StringSplitOptions.RemoveEmptyEntries);
			List<Color> colors = new List<Color>();

			foreach (string colorPart in colorParts)
			{
				if (TryParseColor(colorPart, out Color color)) colors.Add(color);
				else break;
			}

			if (colorParts.Length != colors.Count) continue; // If every element is successfully converted, there should be the same number of elements in each.

			if (colors.Count == 1) return colors[0];

			int index = Random.Range(0, colors.Count - 1);
			return Color.Lerp(colors[index], colors[index + 1], Random.value);
		}

		return Color.black;
	}

	bool TryParseColor(string colorString, out Color color)
	{
		color = default; // If the color isn't parsed successfully, we still need to set some color.

		if (ColorUtility.TryParseHtmlString(colorString, out Color parsedColor))
		{
			parsedColor.a = 1;
			color = parsedColor;
			return true;
		}
		else if (colorString == "random")
		{
			color = new Color(Random.value, Random.value, Random.value);
			return true;
		}
		else
		{
			Match match = Regex.Match(colorString, @"(rgb|hsv)\((\d{1,3}), ?(\d{1,3}), ?(\d{1,3})\)", RegexOptions.CultureInvariant);
			if (match.Success)
			{
				var arguments = match.Groups
					.Cast<Group>()
					.Skip(2)
					.Select(argument => int.Parse(argument.Value) / 255f)
					.ToArray();

				if (arguments.Any(argument => argument < 0 || argument > 1)) return false;

				switch (match.Groups[1].Value)
				{
					case "rgb":
						color = new Color(arguments[0], arguments[1], arguments[2]);
						return true;
					case "hsv":
						color = Color.HSVToRGB(arguments[0], arguments[1], arguments[2]);
						return false;
				}
			}
		}

		return false;
	}
}

public class Casing : MonoBehaviour
{
	public Transform Faces_F;
	public Transform Faces_R;
	public Transform Front;
	public Transform Back;
	public Transform Body;
	public Transform Visual;
	public Transform Highlight;

	public Transform W_Bottom;
	public Transform W_Right;
	public Transform W_Left;
	public Transform W_Top;

	public BoxCollider Distance_Collider;
	public BoxCollider Selectable_Area;
}
