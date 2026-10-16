namespace DmcGrid.Core.Models;

public static class Roles
{
    public const string Agent = "Agent";
    public const string Dmc = "DMC";
}

public static class RfqStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Closed = "Closed";
}

public static class ApprovalStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
}

public class AppUser
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public string OrgName { get; set; } = "";
    public string GstNumber { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>Comma separated destinations a DMC services; empty means all.</summary>
    public string Destinations { get; set; } = "";
    public int LeadsRemaining { get; set; }
    public decimal WalletBalance { get; set; }
    public int QuoteCredits { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Rfq
{
    public int RfqId { get; set; }
    public string RfqNo { get; set; } = "";
    public int AgentUserId { get; set; }
    public string Destination { get; set; } = "";
    public int TravelYear { get; set; }
    public string TravelMonth { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Nights { get; set; }
    public int Adults { get; set; } = 2;
    public int Children { get; set; }
    public string FlightTime { get; set; } = "";
    public string Stops { get; set; } = "";
    public string Cabin { get; set; } = "";
    public string HotelCategory { get; set; } = "";
    public string RoomType { get; set; } = "";
    public string MealPlan { get; set; } = "";
    public string Cities { get; set; } = "";
    public string Vehicle { get; set; } = "";
    /// <summary>Comma separated food preferences.</summary>
    public string FoodPrefs { get; set; } = "";
    public string Allergies { get; set; } = "";
    public string Budget { get; set; } = "";
    public string Priorities { get; set; } = "";
    public string NaturalText { get; set; } = "";
    /// <summary>Comma separated field keys that were filled by the RFQ copilot and not yet edited by the agent.</summary>
    public string AiFields { get; set; } = "";
    public string Status { get; set; } = RfqStatus.Draft;
    public DateTime CreatedAt { get; set; }

    public int? EffectiveNights =>
        StartDate.HasValue && EndDate.HasValue ? (EndDate.Value - StartDate.Value).Days : Nights;

    public string DurationLabel =>
        EffectiveNights is int n ? $"{n}N/{n + 1}D" : "Flexible";

    public string TravelLabel
    {
        get
        {
            if (StartDate.HasValue && EndDate.HasValue)
                return StartDate.Value.Month == EndDate.Value.Month
                    ? $"{StartDate:dd}–{EndDate:dd MMM}"
                    : $"{StartDate:dd MMM}–{EndDate:dd MMM}";
            return TravelMonth.Length > 0 ? $"{TravelMonth} {TravelYear}" : "Flexible";
        }
    }
}

/// <summary>RFQ row on the agent queue, with quote/connection roll-ups.</summary>
public class AgentRfqRow : Rfq
{
    public int OptionCount { get; set; }
    public int DmcCount { get; set; }
    public int ConnectionCount { get; set; }
}

/// <summary>RFQ row on the DMC queue.</summary>
public class DmcRfqRow : Rfq
{
    public bool HasQuote { get; set; }
    public bool Connected { get; set; }
    public int? BestMatch { get; set; }
}

public class DashboardStats
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
    public int D { get; set; }
}

public class MarketPulse
{
    public int Rfqs { get; set; }
    public int Quotes { get; set; }
    public int Connections { get; set; }
    public int Dmcs { get; set; }
    public int Destinations { get; set; }
    public List<(string Destination, int Dmcs)> Coverage { get; set; } = [];
}

public class PackageActivity
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string City { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Transfer { get; set; } = "";
    public string Inc { get; set; } = "Yes";
    public string Notes { get; set; } = "";
}

public class QuotePackage
{
    public int PackageId { get; set; }
    public int QuoteId { get; set; }
    public int DmcUserId { get; set; }
    public string DmcAlias { get; set; } = "";
    public string DmcOrg { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal NetPrice { get; set; }
    public string Hotel { get; set; } = "";
    public string Room { get; set; } = "";
    public string Meal { get; set; } = "";
    public string Vehicle { get; set; } = "";
    public string Food { get; set; } = "";
    public int VersionNo { get; set; } = 1;
    /// <summary>Saved package this one revises; the original stays in the quote history.</summary>
    public int? ParentPackageId { get; set; }
    /// <summary>True when the option was added after the agent connected with this DMC.</summary>
    public bool AfterConnection { get; set; }
    public string StatusNote { get; set; } = "";
    public int MatchPct { get; set; }
    public string MatchNote { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<List<PackageActivity>> Days { get; set; } = [];

    public string VersionLabel =>
        VersionNo > 1 ? $"v{VersionNo} · Updated"
        : AfterConnection ? "New option"
        : "v1 · Original";

    public string VersionStatus =>
        VersionNo > 1 ? "Updated after discussion"
        : AfterConnection ? "New option after discussion"
        : "Original quote";
}

public class ProposalItem
{
    public string Time { get; set; } = "";
    public string Type { get; set; } = "Activity";
    public string Details { get; set; } = "";
    public string Reference { get; set; } = "";
}

public class ProposalDay
{
    public string City { get; set; } = "";
    public List<ProposalItem> Items { get; set; } = [];
}

public class DealCost
{
    public string Type { get; set; } = "";
    public string Desc { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal Charge { get; set; }
}

public class Proposal
{
    public int ProposalId { get; set; }
    public int RfqId { get; set; }
    public int AgentUserId { get; set; }
    public int? PackageId { get; set; }
    public string SourceLabel { get; set; } = "";
    public decimal NetCost { get; set; }
    public string MarginType { get; set; } = "Percentage";
    public decimal Margin { get; set; } = 15;
    public decimal GstPct { get; set; } = 5;
    public decimal TcsPct { get; set; } = 5;
    public int Travellers { get; set; } = 2;
    public string Validity { get; set; } = "48 hours";
    public string BrandName { get; set; } = "";
    public string Title { get; set; } = "";
    public string GstNumber { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Inclusions { get; set; } = "";
    public string Exclusions { get; set; } = "";
    public decimal TdsPaid { get; set; }
    public decimal TdsPayable { get; set; }
    public string ApprovalStatus { get; set; } = Models.ApprovalStatus.Draft;
    public List<ProposalDay> Days { get; set; } = [];
    public List<DealCost> Costs { get; set; } = [];
}

public class PriceBreakdown
{
    public decimal Net { get; set; }
    public decimal Margin { get; set; }
    public decimal Extras { get; set; }
    public decimal Gst { get; set; }
    public decimal Tcs { get; set; }
    public decimal Total { get; set; }
}

public class ConnectResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public int ConnectedDmcs { get; set; }
}
