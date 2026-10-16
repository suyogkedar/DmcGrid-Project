using System.Globalization;
using System.Text.Json;
using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;
using DmcGrid.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class ProposalController(IRfqRepository rfqs, IProposalRepository proposals, IAuditRepository audit) : AppController
{
    /// <summary>Finalize itinerary and commercials (id = RFQ id).</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await LoadAsync(id);
        return model is null ? RedirectToAction("Index", "Compare", new { id }) : View(model);
    }

    /// <summary>Customer-facing proposal preview / PDF (id = RFQ id).</summary>
    [HttpGet]
    public async Task<IActionResult> Customer(int id)
    {
        var model = await LoadAsync(id);
        return model is null ? RedirectToAction("Index", "Compare", new { id }) : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] Proposal? input)
    {
        var current = input is null ? null : await OwnProposalAsync(input.RfqId);
        if (current is null) return Fail("Proposal not found");
        if (input!.NetCost < 0 || input.Margin < 0 || input.GstPct is < 0 or > 100 || input.TcsPct is < 0 or > 100)
            return Fail("Check the commercial values — amounts cannot be negative and tax rates must be 0–100%");

        input.Days = (input.Days ?? []).Where(d => d != null).ToList();
        foreach (var day in input.Days) day.Items = (day.Items ?? []).Where(i => i != null).ToList();
        input.Costs = (input.Costs ?? []).Where(c => c != null).ToList();

        // Server-owned fields are never taken from the client.
        input.ProposalId = current.ProposalId;
        input.AgentUserId = current.AgentUserId;
        input.PackageId = current.PackageId;
        input.SourceLabel = current.SourceLabel;
        input.Travellers = Math.Clamp(input.Travellers, 1, 999);

        // Anything a manager signed off on (itinerary, costs, price) changing sends the deal back to draft.
        var changed = Fingerprint(current) != Fingerprint(input);
        input.ApprovalStatus = changed ? ApprovalStatus.Draft : current.ApprovalStatus;

        await proposals.SaveAsync(input);
        if (changed && current.ApprovalStatus != ApprovalStatus.Draft)
            await audit.LogAsync(UserId, "ApprovalReset", "Proposal", current.ProposalId, "Edited after submission");

        return Done("Proposal saved ✓", new { approvalStatus = input.ApprovalStatus, total = PricingCalculator.Calculate(input).Total });
    }

    [HttpPost]
    public async Task<IActionResult> SendToManager([FromBody] RfqRequest request)
    {
        var proposal = request is null ? null : await OwnProposalAsync(request.RfqId);
        if (proposal is null) return Fail("Proposal not found");
        if (proposal.Days.Sum(d => d.Items.Count) == 0) return Fail("Add at least one itinerary item");

        await proposals.SetApprovalAsync(proposal.ProposalId, ApprovalStatus.Pending, UserId);
        await audit.LogAsync(UserId, "SentToManager", "Proposal", proposal.ProposalId);
        return Done("Final itinerary and deal sheet sent to Manager ✓");
    }

    /// <summary>
    /// Records manager approval. The prototype exposes this on the agent's own screen; when a Manager role and
    /// inbox are introduced, restrict this action to that role.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Approve([FromBody] RfqRequest request)
    {
        var proposal = request is null ? null : await OwnProposalAsync(request.RfqId);
        if (proposal is null) return Fail("Proposal not found");
        if (proposal.ApprovalStatus != ApprovalStatus.Pending) return Fail("Send the proposal to the manager first");

        await proposals.SetApprovalAsync(proposal.ProposalId, ApprovalStatus.Approved, UserId);
        await audit.LogAsync(UserId, "ManagerApproved", "Proposal", proposal.ProposalId);
        return Done("Manager approved ✓");
    }

    private async Task<ProposalViewModel?> LoadAsync(int rfqId)
    {
        var rfq = await rfqs.GetAsync(rfqId);
        if (rfq is null || rfq.AgentUserId != UserId) return null;
        var proposal = await proposals.GetByRfqAsync(rfqId);
        return proposal is null ? null : new ProposalViewModel { Rfq = rfq, Proposal = proposal, Price = PricingCalculator.Calculate(proposal) };
    }

    private async Task<Proposal?> OwnProposalAsync(int rfqId)
    {
        var proposal = await proposals.GetByRfqAsync(rfqId);
        return proposal?.AgentUserId == UserId ? proposal : null;
    }

    /// <summary>Canonical text of everything approval covers, normalised so stored and posted values compare equal.</summary>
    private static string Fingerprint(Proposal p)
    {
        static string N(decimal d) => d.ToString("0.####", CultureInfo.InvariantCulture);
        static string T(string? s) => (s ?? "").Trim();
        return JsonSerializer.Serialize(new object[]
        {
            N(p.NetCost), p.MarginType == "Fixed" ? "Fixed" : "Percentage", N(p.Margin), N(p.GstPct), N(p.TcsPct), p.Travellers,
            T(p.Validity), T(p.Title), T(p.Inclusions), T(p.Exclusions), N(p.TdsPaid), N(p.TdsPayable),
            p.Days.Select(d => new object[] { T(d.City), d.Items.Select(i => new[] { T(i.Time), T(i.Type), T(i.Details), T(i.Reference) }) }),
            p.Costs.Select(c => new[] { T(c.Type), T(c.Desc), N(c.Cost), N(c.Charge) })
        });
    }
}
