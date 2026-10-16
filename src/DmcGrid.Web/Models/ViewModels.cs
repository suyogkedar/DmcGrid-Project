using System.Globalization;
using DmcGrid.Core.Models;

namespace DmcGrid.Web.Models;

public static class Fmt
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Money(decimal amount) => "₹" + Math.Round(amount).ToString("#,##0", India);
}

public class LoginViewModel
{
    public string Portal { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ReturnUrl { get; set; }
    public bool ShowDemoAccounts { get; set; }
}

public class AgentDashboardViewModel
{
    public DashboardStats Stats { get; set; } = new();
    public List<AgentRfqRow> Rfqs { get; set; } = [];
}

public class DmcDashboardViewModel
{
    public DashboardStats Stats { get; set; } = new();
    public List<DmcRfqRow> Rfqs { get; set; } = [];
}

public class BuilderViewModel
{
    public Rfq Rfq { get; set; } = new();
    public List<QuotePackage> Packages { get; set; } = [];
    public bool Connected { get; set; }
}

public class CompareViewModel
{
    public Rfq Rfq { get; set; } = new();
    public List<QuotePackage> Packages { get; set; } = [];
    public List<int> ConnectedDmcIds { get; set; } = [];
    public int? ChosenPackageId { get; set; }
    public decimal Wallet { get; set; }
    public int Leads { get; set; }
}

public class ProposalViewModel
{
    public Rfq Rfq { get; set; } = new();
    public Proposal Proposal { get; set; } = new();
    public PriceBreakdown Price { get; set; } = new();
}

public record StructureRequest(string? Text);
public record SaveRfqRequest(Rfq? Rfq, bool Publish);
public record SaveQuoteRequest(int RfqId, List<QuotePackage>? Packages, bool Submit);
public record RfqRequest(int RfqId);
public record ConnectRequest(int RfqId, List<int>? PackageIds, string? Billing);
public record ChooseRequest(int RfqId, int PackageId);
