using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using DmcGrid.Core.Models;

namespace DmcGrid.Core.Services;

public class QuoteImportResult
{
    public bool Mapped { get; set; }
    public string Message { get; set; } = "";
    public string Confidence { get; set; } = "—";
    public QuotePackage Package { get; set; } = new();
}

/// <summary>Builds draft quote packages for the DMC to verify: from the RFQ itself, or from an uploaded quotation document.</summary>
public static partial class QuoteDrafts
{
    public const long MaxDocumentBytes = 10 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = [".pdf", ".doc", ".docx"];

    [GeneratedRegex(@"(?:₹|Rs\.?|INR)\s*([\d,]{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"^\s*Day\s*(\d{1,2})\s*[:\-–—·.)]*\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex DayRegex();

    [GeneratedRegex(@"([3-5])\s*-?\s*(?:★|\*|star)", RegexOptions.IgnoreCase)]
    private static partial Regex StarRegex();

    /// <summary>A skeleton package mapped from the RFQ. Price and services are deliberately left for the DMC to confirm.</summary>
    public static QuotePackage FromRfq(Rfq rfq, string name)
    {
        var cities = Cities(rfq);
        var dayCount = (rfq.EffectiveNights ?? 0) + 1;
        var vehicle = string.IsNullOrWhiteSpace(rfq.Vehicle) || rfq.Vehicle == "No Preference" ? "Private" : rfq.Vehicle;
        var package = new QuotePackage
        {
            Name = name,
            Hotel = string.IsNullOrWhiteSpace(rfq.HotelCategory) ? "DMC to confirm" : $"{rfq.HotelCategory} · DMC to confirm",
            Room = Default(rfq.RoomType, "Deluxe"),
            Meal = Default(rfq.MealPlan, "Breakfast"),
            Vehicle = vehicle,
            Food = rfq.FoodPrefs.Replace(",", " + ")
        };
        for (var d = 0; d < dayCount; d++)
        {
            var city = cities[Math.Min(cities.Length - 1, d * cities.Length / dayCount)];
            var desc = d == 0 ? "Arrival and private hotel transfer"
                : d == dayCount - 1 ? "Hotel checkout and airport transfer"
                : $"{city} sightseeing";
            package.Days.Add([new PackageActivity { From = "10:00", To = "12:00", City = city, Desc = desc, Transfer = vehicle, Notes = "DMC to verify" }]);
        }
        return package;
    }

    /// <summary>
    /// Maps an uploaded quotation into a package. Text is extracted from .docx files and mapped with pattern rules
    /// (price, hotel category, "Day N" lines); other formats fall back to an RFQ-based draft.
    /// </summary>
    public static QuoteImportResult FromDocument(Rfq rfq, string fileName, Stream content)
    {
        var fallback = FromRfq(rfq, $"Imported · {Path.GetFileNameWithoutExtension(fileName)}");
        var text = Path.GetExtension(fileName).Equals(".docx", StringComparison.OrdinalIgnoreCase) ? ReadDocx(content) : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new QuoteImportResult
            {
                Package = fallback,
                Message = "Automatic extraction currently supports .docx. A draft was prepared from the RFQ instead — review and complete every field."
            };
        }

        var package = fallback;
        var found = 0;

        var prices = PriceRegex().Matches(text)
            .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Where(v => v > 0).ToList();
        if (prices.Count > 0) { package.NetPrice = prices.Max(); found++; }

        var star = StarRegex().Match(text);
        if (star.Success) { package.Hotel = star.Groups[1].Value + "★"; found++; }

        var cities = Cities(rfq);
        var days = new SortedDictionary<int, PackageActivity>();
        foreach (var line in text.Split('\n'))
        {
            var m = DayRegex().Match(line);
            if (!m.Success || m.Groups[2].Value.Trim().Length == 0) continue;
            var desc = m.Groups[2].Value.Trim();
            days[int.Parse(m.Groups[1].Value)] = new PackageActivity
            {
                From = "10:00", To = "17:00",
                City = cities.FirstOrDefault(c => desc.Contains(c, StringComparison.OrdinalIgnoreCase)) ?? "",
                Desc = desc.Length > 500 ? desc[..500] : desc,
                Transfer = package.Vehicle,
                Notes = "Imported from document"
            };
        }
        if (days.Count > 0)
        {
            package.Days = days.Values.Select(a => new List<PackageActivity> { a }).ToList();
            found += 2;
        }

        return new QuoteImportResult
        {
            Mapped = found > 0,
            Package = package,
            Confidence = found > 0 ? $"{50 + found * 11}%" : "—",
            Message = found > 0
                ? "Mapped ✓ The document was read into the builder. Review/edit all fields before Submit Quote."
                : "No price or day-wise itinerary could be recognised. A draft was prepared from the RFQ instead."
        };
    }

    private static string? ReadDocx(Stream content)
    {
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null) return null;
            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd().Replace("</w:p>", "\n");
            return WebUtility.HtmlDecode(Regex.Replace(xml, "<[^>]+>", ""));
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static string[] Cities(Rfq rfq)
    {
        var cities = rfq.Cities.Split(['/', ',', '→'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return cities.Length > 0 ? cities : [rfq.Destination];
    }

    private static string Default(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value == "No Preference" ? fallback : value;
}
