using System.Text.RegularExpressions;
using DmcGrid.Core.Models;

namespace DmcGrid.Core.Services;

/// <summary>Scores how closely a DMC package matches the agent's RFQ (0–100) and explains the main gap.</summary>
public static partial class MatchScorer
{
    [GeneratedRegex(@"([3-5])\s*(?:★|\*|star)", RegexOptions.IgnoreCase)]
    private static partial Regex StarRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)")]
    private static partial Regex NumberRegex();

    public static void Score(Rfq rfq, IList<QuotePackage> packages)
    {
        foreach (var p in packages) (p.MatchPct, p.MatchNote) = Score(rfq, p);
        var best = packages.OrderByDescending(p => p.MatchPct).FirstOrDefault();
        if (best != null && packages.Count > 1 && best.MatchPct >= 90) best.MatchNote = "Best requirement match";
    }

    public static (int Pct, string Note) Score(Rfq rfq, QuotePackage p)
    {
        double score = 0;
        var note = "";

        // Hotel category — 35
        int? want = Stars(rfq.HotelCategory), got = Stars(p.Hotel);
        if (want is null || got is null) score += 35;
        else if (got == want) score += 35;
        else if (got > want) { score += 30; note = "Higher hotel category"; }
        else { score += 14; note = $"Hotel below requested {want}★"; }

        // Vehicle — 20
        var vehicle = rfq.Vehicle;
        var shared = Has(p.Vehicle, "shared") || Has(p.Vehicle, "sic");
        if (NoPreference(vehicle)) score += 20;
        else if (Has(vehicle, "shared")) score += shared ? 20 : 16;
        else if (shared) { score += 6; note = note.Length > 0 ? note : "Shared vehicle offered"; }
        else score += 20;

        // Food — 20
        var prefs = rfq.FoodPrefs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (prefs.Length == 0) score += 20;
        else
        {
            var hit = prefs.Count(f => Has(p.Food, f) || (f.StartsWith("Veg", StringComparison.OrdinalIgnoreCase) && Has(p.Food, "veg")));
            score += 20.0 * hit / prefs.Length;
            if (hit < prefs.Length && note.Length == 0) note = "Food preference partly covered";
        }

        // Budget — 15
        var max = BudgetCeiling(rfq.Budget);
        if (max is null || p.NetPrice <= 0) score += 15;
        else if (p.NetPrice <= max) score += 15;
        else if (p.NetPrice <= max * 1.15m) score += 8;
        else if (note.Length == 0) note = "Above stated budget";

        // Meal plan — 10
        if (NoPreference(rfq.MealPlan) || Has(p.Meal, rfq.MealPlan)) score += 10;
        else score += 5;

        var pct = (int)Math.Round(score);
        if (note.Length == 0) note = pct >= 90 ? "Close match" : "Partial match";
        return (pct, note);
    }

    private static int? Stars(string text)
    {
        var m = StarRegex().Match(text ?? "");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    /// <summary>Upper bound in rupees of a budget bucket such as "₹1–2L"; null when open-ended or not stated.</summary>
    private static decimal? BudgetCeiling(string budget)
    {
        if (string.IsNullOrWhiteSpace(budget) || budget.Contains('+') || !budget.Contains('L')) return null;
        var nums = NumberRegex().Matches(budget);
        return nums.Count == 0 ? null : decimal.Parse(nums[^1].Value) * 100_000m;
    }

    private static bool NoPreference(string v) =>
        string.IsNullOrWhiteSpace(v) || v.Contains("No Preference", StringComparison.OrdinalIgnoreCase);

    private static bool Has(string? text, string value) =>
        (text ?? "").Contains(value, StringComparison.OrdinalIgnoreCase);
}
