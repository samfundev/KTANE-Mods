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

    int DisplayPos = 0;
    List<string> Items = new List<string>();
    decimal Total = 0;
    decimal Paid = 0;
    decimal Display = 0;
    decimal Change = 0;
    string DOW = "";
    bool waiting = false;
    bool solved = false;
    List<List<string>> Receipt = new List<List<string>>();

    static int idCounter = 1;
    int moduleID;

    Dictionary<string, decimal> Prices = new Dictionary<string, decimal>()
    {
        {"Candy Canes",    3.51m},
        {"Socks",          6.97m},
        {"Lotion",         7.97m},
        {"Cheese",         4.49m},
        {"Mints",          6.39m},
        {"Grape Jelly",    2.98m},
        {"Honey",          8.25m},
        {"Sugar",          2.08m},
        {"Soda",           2.05m},
        {"Tissues",        3.94m},
        {"White Bread",    2.43m},
        {"Canola Oil",     2.28m},
        {"Mustard",        2.36m},
        {"Deodorant",      3.97m},
        {"White Milk",     3.62m},
        {"Pasta Sauce",    2.30m},
        {"Lollipops",      2.61m},
        {"Cookies",        2.00m},
        {"Paper Towels",   9.46m},
        {"Tea",            2.35m},
        {"Coffee Beans",   7.85m},
        {"Mayonnaise",     3.99m},
        {"Chocolate Milk", 5.68m},
        {"Fruit Punch",    2.08m},
        {"Potato Chips",   3.25m},
        {"Shampoo",        4.98m},
        {"Toothpaste",     2.50m},
        {"Peanut Butter",  5.00m},
        {"Gum",            1.12m},
        {"Water Bottles",  9.37m},
        {"Spaghetti",      2.92m},
        {"Chocolate Bar",  2.10m},
        {"Ketchup",        3.59m},
        {"Cereal",         4.19m},
    };
    Dictionary<string, decimal> PricesLB = new Dictionary<string, decimal>()
    {
        {"Turkey",     2.98m},
        {"Chicken",    1.99m},
        {"Steak",      4.97m},
        {"Pork",       4.14m},
        {"Lettuce",    1.10m},
        {"Potatoes",   0.68m},
        {"Tomatoes",   1.80m},
        {"Broccoli",   1.39m},
        {"Oranges",    0.80m},
        {"Lemons",     1.74m},
        {"Bananas",    0.87m},
        {"Grapefruit", 1.08m},
    };

    string[] Fruits = { "Bananas", "Grapefruit", "Lemons", "Oranges", "Tomatoes" };
    string[] Sweets = { "Candy Canes", "Mints", "Honey", "Soda", "Lollipops", "Gum", "Chocolate Bar", "Fruit Punch", "Cookies", "Sugar", "Grape Jelly" };

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Cheap Checkout #{0}] {1}", moduleID, msg);
    }

    int mod(int x, int m)
    {
        return (x % m + m) % m;
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
        return Object.transform.Find("ButtonText").gameObject.GetComponent<TextMesh>();
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

    decimal ApplySale(string item, decimal lbs, int index)
    {
        decimal price = decimal.Round(lbs > 0 ? PricesLB[item] * lbs : Prices[item], 2, MidpointRounding.AwayFromZero);
        bool fixeditem = (lbs <= 0);
        List<string> line = new List<string>();

        if (fixeditem)
        {
            line.Add(item);
        }
        else
        {
            line.Add(lbs + "lb of " + item);
        }
        line.Add("$" + price.ToString("N2").PadLeft(5));

        switch (DOW)
        {
            case "Sunday":
                if (fixeditem && item.ToLower().IndexOf("s") > -1)
                {
                    price += 2.15m;
                    line.Add("+2.15");
                }

                break;
            case "Monday":
                if (index == 1 || index == 3 || index == 6)
                {
                    price *= 0.85m;
                    line.Add("-15%");
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
                if (index % 2 == 1)
                {
                    price *= 0.5m;
                    line.Add("-50%");
                }

                break;
            case "Friday":
                if (!fixeditem && Array.IndexOf(Fruits, item) > -1)
                {
                    price *= 1.25m;
                    line.Add("+25%");
                }

                break;
            case "Saturday":
                if (fixeditem && Array.IndexOf(Sweets, item) > -1)
                {
                    price *= 0.65m;
                    line.Add("-35%");
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

    IEnumerator waitForCustomer()
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
        DOW = DateTime.Now.DayOfWeek.ToString();

        DebugMsg("Sale is based on " + DOW + ".");

        List<string> Possible = new List<string>(Prices.Keys);
        for (int i = 0; i < 4; i++)
        {
            var item = Possible[Random.Range(0, Possible.Count)];
            Items.Add(item);
            Possible.Remove(item);
            decimal dollars = ApplySale(item, 0, Items.Count);
            Total += dollars;
        }

        Possible = new List<string>(PricesLB.Keys);
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
            KMSelectable ButtonSelectable = button.GetComponent<KMSelectable>() as KMSelectable;
            ButtonSelectable.OnInteract += delegate ()
            {
                if (!waiting)
                {
                    ButtonPress(ButtonSelectable);
                    string text = GetTextMesh(Button).text;
                    if (text.Length > 2)
                    {
                        BombAudio.PlaySoundAtTransform("coin_drop" + Random.Range(1, 2), transform);
                    }
                    else
                    {
                        BombAudio.PlaySoundAtTransform("count_bill" + Random.Range(1, 5), transform);
                    }

                    Change += decimal.Parse("0" + text);
                    UpdateDisplay();
                }
                else
                {
                    BombAudio.PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.CapacitorPop, transform);
                }

                return false;
            };
        }

        MoveLeft.OnInteract += delegate ()
        {
            ButtonPress(MoveLeft);

            DisplayPos--;
            UpdateDisplay();

            return false;
        };

        MoveRight.OnInteract += delegate ()
        {
            ButtonPress(MoveRight);

            DisplayPos++;
            UpdateDisplay();

            return false;
        };

        Submit.OnInteract += delegate ()
        {
            if (!waiting)
            {
                ButtonPress(Submit);

                if (Total > Display)
                {
                    if (Change == 0)
                    {
                        StartCoroutine(waitForCustomer());
                    }
                    else
                    {
                        DebugMsg("Change was submitted when the customer should have been alerted.");
                        BombModule.HandleStrike();
                    }
                }
                else
                {
                    DebugMsg("Changed entered: $" + Change.ToString("N2"));
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
                BombAudio.PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.CapacitorPop, transform);
            }

            return false;
        };

        Clear.OnInteract += delegate ()
        {
            if (!waiting)
            {
                ButtonPress(Clear);
                Change = 0m;
                UpdateDisplay();
            }
            else
            {
                BombAudio.PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.CapacitorPop, transform);
            }

            return false;
        };
    }

    void Start()
    {
        moduleID = idCounter++;

        BombModule.OnActivate += OnActivate;
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
            else if (split[0] == "items")
			{
				yield return null;

				for (int i = 0; i < 5; i++)
                {
                    yield return new WaitForSeconds(1.5f);
                    MoveRight.OnInteract();
                }

                yield return new WaitForSeconds(2f);
                for (int i = 0; i < 5; i++)
                {
                    MoveLeft.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            else if (split[0] == "submit" || split[0] == "slap")
			{
				yield return null;

				Submit.OnInteract();
            }
        }
        else if (split.Length == 2 && split[0] == "submit")
        {
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

                if (Change == Paid - Total)
                {
                    yield return "solve";
                }
                else
                {
                    yield return "strike";
                }

                Submit.OnInteract();
            }
        }
    }
}
