using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

public class CheapCheckoutModule : MonoBehaviour
{
    public GameObject[] Amounts;
    public KMSelectable Submit;
    public KMSelectable Clear;
    public GameObject ItemText;
    public GameObject PriceText;
    public KMSelectable MoveLeft;
    public KMSelectable MoveRight;

    public KMAudio BombAudio;
    public KMBombModule BombModule;

    int DisplayPos;
    readonly List<string> Items = new List<string>();
    decimal Total;
    decimal Paid;
    decimal Display;
    decimal Change;
    string DOW = "";
    bool waiting;
    bool solved;
    readonly List<List<string>> Receipt = new List<List<string>>();

    static int idCounter = 1;
    int moduleID;

	class Item
	{
		public string Name;
		public decimal Price;
		public string Category;

		public Item(string[] strings)
		{
			Name = strings[0];
			Price = decimal.Parse(strings[1]);
			Category = strings[2];
		}
	}

    // Item format: Name,Price,Category
    Item[] Prices = new[] {
        // Original 33 items
        "Candy Canes,3.51,Sweet",
        "Socks,6.97,Other",
        "Lotion,7.97,Care Product",
        "Cheese,4.49,Dairy",
        "Mints,6.39,Sweet",
        "Grape Jelly,2.98,Sweet",
        "Honey,8.25,Sweet",
        "Sugar,2.08,Sweet",
        "Soda,2.05,Sweet",
        "Tissues,3.94,Care Product",
        "White Bread,2.43,Grain",
        "Canola Oil,2.28,Oil",
        "Mustard,2.36,Other",
        "Deodorant,3.97,Care Product",
        "White Milk,3.62,Dairy",
        "Pasta Sauce,2.30,Vegetable",
        "Lollipops,2.61,Sweet",
        "Cookies,2.00,Sweet",
        "Paper Towels,9.46,Care Product",
        "Tea,2.35,Water",
        "Coffee Beans,7.85,Other",
        "Mayonnaise,3.99,Oil",
        "Chocolate Milk,5.68,Dairy",
        "Fruit Punch,2.08,Sweet",
        "Potato Chips,3.25,Oil",
        "Shampoo,4.98,Care Product",
        "Toothpaste,2.50,Care Product",
        "Peanut Butter,5.00,Protein",
        "Gum,1.12,Sweet",
        "Water Bottles,9.37,Water",
        "Spaghetti,2.92,Grain",
        "Chocolate Bar,2.10,Sweet",
        "Ketchup,3.59,Other",
        "Cereal,4.19,Grain",

        // New ruleseed items
		"Eggs,2.67,Protein",
		"Baked Beans,1.14,Protein",
		"Peanuts,5.98,Protein",
		"Yogurt,2.72,Dairy",
		"Greek Yogurt,3.47,Dairy",
		"Butter,5.86,Dairy",
		"Toothbrush,2.24,Care Product",
		"Medicine,3.73,Care Product",
		"Soup,1.99,Care Product",
		"Soap,3.97,Care Product",
		"Pretzels,2.98,Grain",
		"Popcorn,2.50,Grain",
		"Oatmeal,7.98,Grain",
		"Rice,2.02,Grain",
		"Flour,3.49,Grain",
		"Licorice,1.98,Sweet",
		"Pie,3.98,Sweet",
		"Cake,9.98,Sweet",
		"Gummy Bears,7.98,Sweet",
		"Relish,2.74,Vegetable",
	}.Select(value => new Item(value.Split(','))).ToArray();

	Item[] PricesLB = new[] {
        // Original 11 items
		"Turkey,2.98,Protein",
		"Chicken,1.99,Protein",
		"Steak,4.97,Protein",
		"Pork,4.14,Protein",
		"Lettuce,1.10,Vegetable",
		"Potatoes,0.68,Vegetable",
		"Tomatoes,1.80,Fruit",
		"Broccoli,1.39,Vegetable",
		"Oranges,0.80,Fruit",
		"Lemons,1.74,Fruit",
		"Bananas,0.87,Fruit",
		"Grapefruit,1.08,Fruit",

        // New ruleseed items
		"Onion,1.82,Vegetable",
		"Bacon,5.52,Protein",
		"Apples,1.32,Fruit",
		"Grapes,2.98,Fruit",
		"Fish,9.99,Protein",
		"Watermelon,0.32,Fruit",
		"Carrots,0.77,Vegetable",
		"Cherries,3.21,Fruit",
		"Plums,1.99,Fruit",
		"Pumpkins,1.38,Fruit",
		"Avocados,2.23,Fruit",
		"Ham,2.88,Protein",
		"Sausages,3.88,Protein",
		"Corn,1.14,Vegetable",
	}.Select(value => new Item(value.Split(','))).ToArray();

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Cheap Checkout #{0}] {1}", moduleID, msg);
    }

    void ButtonPress(KMSelectable Selectable)
    {
        Selectable.AddInteractionPunch(0.5f);
        BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
    }

    IEnumerator Wait(float time, Func<bool> func)
    {
        yield return new WaitForSeconds(time);
        func();
    }

    TextMesh GetTextMesh(GameObject Object)
    {
        return Object.transform.Find("ButtonText").GetComponent<TextMesh>();
    }

    void UpdateDisplay()
    {
        if (!waiting)
        {
            TextMesh PriceMesh = PriceText.GetComponent<TextMesh>();
            if (Change > 0)
            {
                PriceMesh.text = "$" + Change.ToString("N2");
                PriceMesh.color = Color.yellow;
            }
            else
            {
                PriceMesh.text = "$" + Display.ToString("N2");
                PriceMesh.color = Color.white;
            }
        }

        DisplayPos = Math.Min(Math.Max(DisplayPos, 0), Items.Count - 1);
        ItemText.GetComponent<TextMesh>().text = Items[DisplayPos];
    }

    // Ruleseed numbers for sales
    decimal SundayValue = 2.15m;
    decimal MondayPercent = 0.85m;
    bool ThursdayOddItems = true;
    decimal ThursdayPercent = 0.5m;
    decimal FridayPercent = 1.25m;
    decimal SaturdayPercent = 0.65m;

    string FormatRelativePercent(decimal percent)
    {
        if (percent >= 1) return "+" + (percent * 100 - 100).ToString("N0") + "%";
        else return "-" + (100 - percent * 100).ToString("N0") + "%";
    }

    decimal ApplySale(string itemName, decimal lbs, int index)
    {
		Item item = lbs > 0 ? Array.Find(PricesLB, value => value.Name == itemName) : Array.Find(Prices, value => value.Name == itemName);

		decimal price = decimal.Round(lbs > 0 ? item.Price * lbs : item.Price, 2, MidpointRounding.AwayFromZero);
        bool fixeditem = (lbs <= 0);
        List<string> line = new List<string>();

        if (fixeditem)
        {
            line.Add(itemName);
        }
        else
        {
            line.Add(lbs + "lb of " + itemName);
        }
        line.Add("$" + price.ToString("N2").PadLeft(5));

        switch (DOW)
        {
            case "Sunday":
                if (fixeditem && itemName.IndexOf("s", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    price += SundayValue;
                    line.Add("+" + SundayValue.ToString("N2"));
                }

                break;
            case "Monday":
                if (index == 1 || index == 3 || index == 6)
                {
                    price *= MondayPercent;
                    line.Add(FormatRelativePercent(MondayPercent));
                }

                break;
            case "Tuesday":
                if (fixeditem)
                {
                    // Convert to string -> Remove decimal -> Convert to decimal -> Apply digital root.
                    price += (decimal.Parse(price.ToString().Replace(".", "")) - 1) % 9 + 1;
                    line.Add("dgt rt");
                }

                break;
            case "Wednesday":
                int a = (int)(price % 10),
                    b = (int)(price * 10) % 10,
                    c = (int)(price * 100) % 10;

                string highest = Math.Max(Math.Max(a, b), c).ToString();
                string lowest = Math.Min(Math.Min(a, b), c).ToString();
                var result = price.ToString("N2").Select(x => x.ToString() == highest ? lowest : (x.ToString() == lowest ? highest : x.ToString())).ToArray();
                price = decimal.Parse(string.Join("", result));
                line.Add(highest + " <-> " + lowest);

                break;
            case "Thursday":
                if (index % 2 == (ThursdayOddItems ? 1 : 0))
                {
                    price *= ThursdayPercent;
                    line.Add(FormatRelativePercent(ThursdayPercent));
                }

                break;
            case "Friday":
                if (!fixeditem && item.Category == "Fruit")
                {
                    price *= FridayPercent;
                    line.Add(FormatRelativePercent(FridayPercent));
                }

                break;
            case "Saturday":
                if (fixeditem && item.Category == "Sweet")
                {
                    price *= SaturdayPercent;
                    line.Add(FormatRelativePercent(SaturdayPercent));
                }

                break;
            default:
                DebugMsg("Somehow you aren't using a day of the week. Automatically solving.");
                BombModule.HandlePass();
                break;
        }

        if (line.Count == 2)
        {
            line.Add("");
        }

        var final = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        line.Add("$" + final.ToString("N2").PadLeft(5));
        Receipt.Add(line);

        return final;
    }

    void BuildReceipt()
    {
        var width = new int[4];
        foreach (List<string> line in Receipt)
        {
            int index = 0;
            foreach (string var in line)
            {
                width[index] = Math.Max(var.Length, width[index]);
                index++;
            }
        }

        var receipt = "";
        foreach (List<string> line in Receipt)
        {
            int index = 0;
            foreach (string var in line)
            {
                receipt += var.PadRight(width[index]);
                if (index < line.Count - 1)
                {
                    receipt += "   ";
                }

                index++;
            }
            receipt += "\n";
        }
        int padding = width.Sum() + 9;

        receipt += new string('─', padding) + "\n";
        receipt += string.Format("{0}${1,5:N2}\n{2}${3,5:N2}\n{4}${5,5:N2}",
            "TOTAL".PadRight(padding - 6), Total,
            "PAID".PadRight(padding - 6), Paid,
            (Paid - Total > 0 ? "CHANGE" : "DUE").PadRight(padding - 6), Math.Abs(Paid - Total));
        DebugMsg("Receipt:\n" + receipt);
    }

    IEnumerator WaitForCustomer()
    {
        waiting = true;
        for (int i = 0; i < 2; i++)
        {
            for (int n = 0; n <= 3; n++)
            {
                PriceText.GetComponent<TextMesh>().text = "One Second" + new string('.', n);
                yield return new WaitForSeconds(0.375f);
            }
        }
        Display = Paid;

        waiting = false;
        UpdateDisplay();
    }

    void OnActivate()
    {
        MonoRandom rng = GetComponent<KMRuleSeedable>().GetRNG();
        // If we are using the default seed, only pick the original items
        if (rng.Seed == 1)
        {
            Prices = Prices.Take(33).ToArray();
            PricesLB = PricesLB.Take(11).ToArray();
        }
        else
        {
            Prices = rng.ShuffleFisherYates(Prices).Take(33).ToArray();
            PricesLB = rng.ShuffleFisherYates(PricesLB).Take(11).ToArray();

            foreach (Item item in Prices)
            {
                item.Price += rng.Next(-10, 11) * 0.01m;
            }

            foreach (Item item in PricesLB)
            {
                item.Price += rng.Next(-10, 11) * 0.01m;
            }

            SundayValue = rng.Next(50, 301) / 100m;
            MondayPercent = rng.Next(10, 100) / 100m;
            ThursdayOddItems = rng.Next(0, 2) == 1;
            ThursdayPercent = rng.Next(25, 76) / 100m;
            FridayPercent = rng.Next(50, 201) / 100m;
            SaturdayPercent = rng.Next(50, 201) / 100m;
        }

        DOW = DateTime.Now.DayOfWeek.ToString();

        DebugMsg("Sale is based on " + DOW + ".");

        List<string> Possible = new List<string>(Prices.Select(item => item.Name));
        for (int i = 0; i < 4; i++)
        {
            var item = Possible[Random.Range(0, Possible.Count)];
            Items.Add(item);
            Possible.Remove(item);
            decimal dollars = ApplySale(item, 0, Items.Count);
            Total += dollars;
        }

        Possible = new List<string>(PricesLB.Select(item => item.Name));
        for (int i = 0; i < 2; i++)
        {
            var item = Possible[Random.Range(0, Possible.Count)];
            var lb = Random.Range(1, 4) * 0.5m;
            Items.Add(lb + "lb " + item);
            Possible.Remove(item);
            decimal dollars = ApplySale(item, lb, Items.Count);
            Total += dollars;
        }

        Paid = decimal.Round(Total + (decimal)Random.Range(-(float)Total / 2, (float)Total / 2));
        if (Total > Paid)
        {
            Display = Paid;
            DebugMsg("Customer underpaid with $" + Paid.ToString());
            Paid = decimal.Round(Total + (decimal)Random.Range(0f, (float)Total / 2)) + 1m;
        }
        else
        {
            Display = Paid;
        }

        BuildReceipt();

        UpdateDisplay();

        foreach (GameObject button in Amounts)
        {
            GameObject Button = button;
            KMSelectable ButtonSelectable = button.GetComponent<KMSelectable>();
            ButtonSelectable.OnInteract += () =>
            {
                if (!waiting)
                {
                    ButtonPress(ButtonSelectable);
                    string text = GetTextMesh(Button).text;
                    if (text.Length > 2)
                    {
                        BombAudio.PlaySoundAtTransform("coin_drop" + Random.Range(1, 3), transform);
                    }
                    else
                    {
                        BombAudio.PlaySoundAtTransform("count_bill" + Random.Range(1, 6), transform);
                    }

                    Change += decimal.Parse("0" + text);
                    UpdateDisplay();
                }
                else
                {
                    BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CapacitorPop, transform);
                }

                return false;
            };
        }

        MoveLeft.OnInteract += () =>
        {
            ButtonPress(MoveLeft);

            DisplayPos--;
            UpdateDisplay();

            return false;
        };

        MoveRight.OnInteract += () =>
        {
            ButtonPress(MoveRight);

            DisplayPos++;
            UpdateDisplay();

            return false;
        };

        Submit.OnInteract += () =>
        {
            if (!waiting)
            {
                ButtonPress(Submit);

                if (Total > Display)
                {
                    if (Change == 0)
                    {
                        StartCoroutine(WaitForCustomer());
                    }
                    else
                    {
                        DebugMsg("Change was submitted when the customer should have been alerted.");
                        BombModule.HandleStrike();
                    }
                }
                else
                {
                    DebugMsg("Change entered: $" + Change.ToString("N2"));
                    if (Change == Paid - Total && !solved)
                    {
                        solved = true;
                        waiting = true;

                        PriceText.GetComponent<TextMesh>().color = Color.green;
                        BombAudio.PlaySoundAtTransform("module_solved", transform);
                        StartCoroutine(Wait(3f, () =>
                        {
                            DebugMsg("Module solved!");
                            BombModule.HandlePass();

                            return true;
                        }));
                    }
                    else
                    {
                        PriceText.GetComponent<TextMesh>().color = Color.red;
                        StartCoroutine(Wait(1.5f, () =>
                        {
                            UpdateDisplay();
                            return true;
                        }));
                        BombModule.HandleStrike();
                    }
                }

                Change = 0m;
            }
            else
            {
                BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CapacitorPop, transform);
            }

            return false;
        };

        Clear.OnInteract += () =>
        {
            if (!waiting)
            {
                ButtonPress(Clear);
                Change = 0m;
                UpdateDisplay();
            }
            else
            {
                BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CapacitorPop, transform);
            }

            return false;
        };
    }

    public void Start()
    {
        moduleID = idCounter++;

        BombModule.OnActivate += OnActivate;
    }

    public const string TwitchHelpMessage = "Cycle the items with !{0} items. Go to a specific item number with !{0} item 3. Get customers to pay the correct amount with !{0} submit. Return the proper change with !{0} submit 3.24.";

	public bool EqualsAny(object obj, params object[] targets)
	{
		return targets.Contains(obj);
	}

	public IEnumerator ProcessTwitchCommand(string command)
    {
        string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length == 1)
        {
            if (split[0] == "clear")
			{
				yield return null;

				Clear.OnInteract();
            }
            else if (EqualsAny(split[0], "items", "cycle"))
			{
				yield return null;

				int initialPosition = DisplayPos;
				for (int i = 0; i < initialPosition; i++)
				{
					MoveLeft.OnInteract();
					yield return new WaitForSeconds(0.05f);
				}

				for (int i = 0; i < 5; i++)
                {
                    yield return new WaitForSeconds(1.5f);
                    MoveRight.OnInteract();
                }

                yield return new WaitForSeconds(1.5f);
                for (int i = 0; i < (5 - initialPosition); i++)
                {
                    MoveLeft.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
			}
            else if (EqualsAny(split[0], "submit", "slap"))
			{
				yield return null;

				Submit.OnInteract();
            }
        }
        else if (split.Length == 2)
        {
			if (split[0] == "submit") {
				decimal price;
				if (decimal.TryParse(split[1], out price) && decimal.Round(price, 2) == price && price < 200)
				{
					yield return null;

					Clear.OnInteract();

					foreach (GameObject button in Amounts.Reverse())
					{
						decimal amount = decimal.Parse("0" + GetTextMesh(button).text);
						while (price >= amount)
						{
							button.GetComponent<KMSelectable>().OnInteract();
							price -= amount;
							yield return new WaitForSeconds(0.1f);
						}
					}

					if (Change == Paid - Total) yield return "solve";
					else yield return "strike";

					Submit.OnInteract();
				}
			}
			else if (EqualsAny(split[0], "set", "item"))
			{
				int position;
				if (int.TryParse(split[1], out position) && position >= 1 && position <= 6)
				{
					int diff = DisplayPos - (position - 1);
					for (int i = 0; i < Math.Abs(diff); i++)
					{
						(diff > 0 ? MoveLeft : MoveRight).OnInteract();
						yield return new WaitForSeconds(0.1f);
					}
				}
			}
        }
    }

	public IEnumerator TwitchHandleForcedSolve()
	{
		if (Total > Display)
		{
			if (Change != 0) {
				Clear.OnInteract();
				yield return new WaitForSeconds(0.1f);
			}

			Submit.OnInteract();

			while (waiting) yield return true;
		}

		yield return ProcessTwitchCommand("submit " + (Paid - Total).ToString());
	}
}
