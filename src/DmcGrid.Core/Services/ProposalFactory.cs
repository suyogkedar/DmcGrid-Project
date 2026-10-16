using DmcGrid.Core.Models;

namespace DmcGrid.Core.Services;

public static class ProposalFactory
{
    /// <summary>
    /// Carries the chosen DMC package into the agent's proposal. Commercial and branding fields of an existing
    /// proposal are preserved; the itinerary and net cost are replaced and approval starts over.
    /// </summary>
    public static Proposal FromPackage(Rfq rfq, QuotePackage package, AppUser agent, Proposal? existing)
    {
        var nights = rfq.EffectiveNights ?? Math.Max(0, package.Days.Count - 1);
        var proposal = existing ?? new Proposal
        {
            RfqId = rfq.RfqId,
            AgentUserId = agent.UserId,
            Travellers = rfq.Adults + rfq.Children,
            BrandName = agent.OrgName,
            Title = $"{rfq.Destination} {nights} Nights / {nights + 1} Days",
            GstNumber = agent.GstNumber,
            Contact = string.Join(" · ", new[] { agent.Phone, agent.Email }.Where(x => x.Length > 0)),
            Inclusions = $"{nights} nights accommodation with {(package.Meal.Length > 0 ? package.Meal.ToLowerInvariant() : "breakfast")}, private transfers, sightseeing and attraction tickets as listed.",
            Exclusions = "International airfare unless included, personal expenses and services not listed."
        };

        proposal.PackageId = package.PackageId;
        proposal.SourceLabel = $"{package.DmcAlias} · {package.Name} · {package.VersionLabel}";
        proposal.NetCost = package.NetPrice;
        proposal.ApprovalStatus = ApprovalStatus.Draft;

        var lastCity = rfq.Destination;
        proposal.Days = package.Days.Select(day =>
        {
            lastCity = day.Select(a => a.City).FirstOrDefault(c => c.Length > 0) ?? lastCity;
            return new ProposalDay
            {
                City = lastCity,
                Items = day.Select(a => new ProposalItem
                {
                    Time = a.From,
                    Type = ItemType(a.Desc),
                    Details = a.Desc,
                    Reference = a.Notes.Length > 0 ? a.Notes : a.Inc == "No" ? "Not included" : "Included"
                }).ToList()
            };
        }).ToList();
        return proposal;
    }

    private static string ItemType(string description)
    {
        bool Has(params string[] words) => words.Any(w => description.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (Has("flight")) return "Flight";
        if (Has("check-in", "checkout", "check-out", "hotel check")) return "Hotel";
        if (Has("transfer", "pickup", "airport", "drop")) return "Transfer";
        if (Has("lunch", "dinner", "breakfast")) return "Meal";
        return "Activity";
    }
}
