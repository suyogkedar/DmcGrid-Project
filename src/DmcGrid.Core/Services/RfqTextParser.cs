using System.Globalization;
using System.Text.RegularExpressions;
using DmcGrid.Core.Models;

namespace DmcGrid.Core.Services;

public interface IRfqCopilot
{
    /// <summary>Structures a free-text trip brief into RFQ fields. Only fields actually stated are filled and listed in <see cref="Rfq.AiFields"/>.</summary>
    Rfq Structure(string text, DateTime today);
}

/// <summary>
/// Deterministic, rule-based RFQ copilot. It extracts only what the agent actually wrote;
/// swap the <see cref="IRfqCopilot"/> registration for an LLM-backed implementation without touching the UI.
/// </summary>
public class RfqTextParser : IRfqCopilot
{
    private static readonly string[] KnownDestinations =
    [
        "Thailand", "Dubai", "Bali", "Singapore", "Malaysia", "Maldives", "Vietnam", "Sri Lanka", "Mauritius",
        "Japan", "Turkey", "Europe", "Switzerland", "France", "Italy", "Greece", "Egypt", "Kenya", "Seychelles",
        "Australia", "New Zealand", "Bhutan", "Nepal", "Kerala", "Kashmir", "Goa", "Rajasthan", "Andaman",
        "Hong Kong", "South Africa", "Abu Dhabi", "Cambodia", "Indonesia", "Phuket", "Bangkok", "London", "Paris"
    ];

    public Rfq Structure(string text, DateTime today)
    {
        var rfq = new Rfq { NaturalText = text.Trim(), Adults = 2 };
        var ai = new List<string>();
        var t = " " + text + " ";
        bool Has(string pattern) => Regex.IsMatch(t, pattern, RegexOptions.IgnoreCase);
        Match Find(string pattern) => Regex.Match(t, pattern, RegexOptions.IgnoreCase);

        var dest = KnownDestinations.FirstOrDefault(d => Has($@"\b{Regex.Escape(d)}\b"));
        if (dest != null) { rfq.Destination = dest; ai.Add("destination"); }

        var months = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames.Take(12).ToArray();
        for (var i = 0; i < 12; i++)
        {
            if (!Has($@"\b{months[i][..3]}(?:{months[i][3..]})?\b")) continue;
            rfq.TravelMonth = months[i];
            ai.Add("month");
            var y = Find(@"\b(20\d\d)\b");
            rfq.TravelYear = y.Success ? int.Parse(y.Groups[1].Value) : (i + 1 >= today.Month ? today.Year : today.Year + 1);
            ai.Add("year");
            break;
        }

        var m = Find(@"(\d+)\s*(?:adults?|pax|people|persons?|travell?ers?)");
        if (m.Success) { rfq.Adults = Math.Clamp(int.Parse(m.Groups[1].Value), 1, 99); ai.Add("adults"); }
        else if (Has(@"\b(couple|honeymoon)\b")) { rfq.Adults = 2; ai.Add("adults"); }

        m = Find(@"(\d+)\s*(?:kids?|child(?:ren)?)");
        if (m.Success) { rfq.Children = Math.Clamp(int.Parse(m.Groups[1].Value), 0, 20); ai.Add("children"); }

        m = Find(@"(\d+)\s*nights?");
        if (m.Success) rfq.Nights = int.Parse(m.Groups[1].Value);
        else if ((m = Find(@"(\d+)\s*days?")).Success) rfq.Nights = Math.Max(0, int.Parse(m.Groups[1].Value) - 1);
        if (rfq.Nights.HasValue) ai.Add("duration");

        m = Find(@"([3-5])\s*-?\s*(?:star|★|\*)");
        if (Has(@"\bvilla\b")) { rfq.HotelCategory = "Private Villa"; ai.Add("hotel"); }
        else if (Has(@"\bboutique\b")) { rfq.HotelCategory = "Boutique"; ai.Add("hotel"); }
        else if (m.Success) { rfq.HotelCategory = m.Groups[1].Value + "★"; ai.Add("hotel"); }

        if (Has(@"all[\s-]inclusive")) rfq.MealPlan = "All Inclusive";
        else if (Has(@"full\s*board")) rfq.MealPlan = "Full Board";
        else if (Has(@"half\s*board")) rfq.MealPlan = "Half Board";
        else if (Has(@"\bbreakfast\b")) rfq.MealPlan = "Breakfast";
        if (rfq.MealPlan.Length > 0) ai.Add("meal");

        if (Has(@"self[\s-]drive")) rfq.Vehicle = "Self Drive";
        else if (Has(@"\b(sic|shared|seat[\s-]in[\s-]coach)\b")) rfq.Vehicle = "SIC / Shared";
        else if (Has(@"\bsuv\b")) rfq.Vehicle = "Premium SUV";
        else if (Has(@"luxury\s+(car|vehicle|transfers?)")) rfq.Vehicle = "Luxury";
        else if (Has(@"private\s+(car|vehicle|transfers?|cab|driver)")) rfq.Vehicle = "Private";
        if (rfq.Vehicle.Length > 0) ai.Add("vehicle");

        var food = new List<string>();
        if (Has(@"\bnon[\s-]?veg")) food.Add("Non-Veg");
        else if (Has(@"\bveg(etarian)?\b")) food.Add("Vegetarian");
        if (Has(@"\bjain\b")) food.Add("Jain");
        if (Has(@"\bvegan\b")) food.Add("Vegan");
        if (Has(@"\bhalal\b")) food.Add("Halal");
        if (Has(@"\bindian\b")) food.Add("Indian");
        if (Has(@"\blocal\s+(food|cuisine)")) food.Add("Local");
        if (food.Count > 0) { rfq.FoodPrefs = string.Join(",", food); ai.Add("food"); }

        decimal? lakh = null;
        m = Find(@"(\d+(?:\.\d+)?)\s*(?:lakhs?|lacs?|l\b)");
        if (m.Success) lakh = decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        else if ((m = Find(@"(?:₹|rs\.?|inr)\s*([\d,]{5,})")).Success)
            lakh = decimal.Parse(m.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture) / 100_000m;
        if (lakh.HasValue)
        {
            rfq.Budget = lakh <= 2 ? "₹1–2L" : lakh <= 3 ? "₹2–3L" : lakh <= 5 ? "₹3–5L" : lakh <= 10 ? "₹5–10L" : "₹10L+";
            ai.Add("budget");
        }

        if (Has(@"\bdirect\s+flights?\b") || Has(@"\bnon[\s-]?stop\b")) { rfq.Stops = "Direct"; ai.Add("stops"); }
        if (Has(@"\bbusiness\s+class\b")) { rfq.Cabin = "Business"; ai.Add("cabin"); }
        else if (Has(@"\bpremium\s+economy\b")) { rfq.Cabin = "Premium Economy"; ai.Add("cabin"); }
        else if (Has(@"\beconomy\b")) { rfq.Cabin = "Economy"; ai.Add("cabin"); }

        var priorities = new List<string>();
        if (Has(@"\bcentral")) priorities.Add("Central hotels");
        if (Has(@"\bprivate\b")) priorities.Add("private transfers");
        if (Has(@"\b(relaxed|leisurely|slow)\b")) priorities.Add("relaxed itinerary");
        if (Has(@"\bhoneymoon\b")) priorities.Add("honeymoon");
        if (Has(@"\bfamily\b")) priorities.Add("family friendly");
        if (Has(@"\badventure\b")) priorities.Add("adventure");
        if (Has(@"\bshopping\b")) priorities.Add("shopping time");
        if (Has(@"\bluxury\b")) priorities.Add("luxury experiences");
        if (priorities.Count > 0) { rfq.Priorities = string.Join(" · ", priorities); ai.Add("priorities"); }

        rfq.AiFields = string.Join(",", ai);
        return rfq;
    }
}
