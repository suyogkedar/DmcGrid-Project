using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;
using DmcGrid.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class CompareController(
    IRfqRepository rfqs, IQuoteRepository quotes, IProposalRepository proposals, IUserRepository users, IAuditRepository audit)
    : AppController
{
    /// <summary>Compare submitted DMC quotes for an RFQ (id = RFQ id).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        var rfq = await OwnRfqAsync(id);
        if (rfq is null) return NotFound();
        if (rfq.Status == RfqStatus.Draft) return RedirectToAction("Create", "Rfq", new { id });

        var packages = await quotes.GetSubmittedAsync(id);
        MatchScorer.Score(rfq, packages);
        var connected = await quotes.ConnectedDmcIdsAsync(id);
        // DMC identity stays anonymous until the agent has paid to connect.
        foreach (var p in packages.Where(p => !connected.Contains(p.DmcUserId))) p.DmcOrg = "";

        var agent = (await users.GetByIdAsync(UserId))!;
        return View(new CompareViewModel
        {
            Rfq = rfq,
            Packages = packages,
            ConnectedDmcIds = connected,
            ChosenPackageId = (await proposals.GetByRfqAsync(id))?.PackageId,
            Wallet = agent.WalletBalance,
            Leads = agent.LeadsRemaining
        });
    }

    [HttpPost]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
    {
        if (request is null) return Fail("Invalid request");
        var result = await quotes.ConnectAsync(request.RfqId, UserId, request.PackageIds ?? [], request.Billing ?? "wallet");
        if (!result.Ok) return Fail(result.Message);

        await audit.LogAsync(UserId, "DmcConnected", "Rfq", request.RfqId, $"billing={request.Billing}; dmcs={result.ConnectedDmcs}");
        return Done(result.Message);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleClose([FromBody] RfqRequest request)
    {
        var rfq = request is null ? null : await OwnRfqAsync(request.RfqId);
        if (rfq is null || rfq.Status == RfqStatus.Draft) return Fail("RFQ not found");

        var closed = rfq.Status != RfqStatus.Closed;
        await rfqs.SetStatusAsync(rfq.RfqId, UserId, closed ? RfqStatus.Closed : RfqStatus.Published);
        await audit.LogAsync(UserId, closed ? "RfqClosed" : "RfqReactivated", "Rfq", rfq.RfqId);
        return Done(closed ? "RFQ closed · further DMC quotes blocked" : "RFQ reactivated", new { closed });
    }

    /// <summary>Takes a connected DMC's option forward as the itinerary for the customer proposal.</summary>
    [HttpPost]
    public async Task<IActionResult> Choose([FromBody] ChooseRequest request)
    {
        var rfq = request is null ? null : await OwnRfqAsync(request.RfqId);
        if (rfq is null) return Fail("RFQ not found");

        var package = (await quotes.GetSubmittedAsync(rfq.RfqId)).FirstOrDefault(p => p.PackageId == request!.PackageId);
        if (package is null) return Fail("This option is no longer available");
        if (!(await quotes.ConnectedDmcIdsAsync(rfq.RfqId)).Contains(package.DmcUserId))
            return Fail("Connect with this DMC before choosing its itinerary");

        var agent = (await users.GetByIdAsync(UserId))!;
        var proposal = ProposalFactory.FromPackage(rfq, package, agent, await proposals.GetByRfqAsync(rfq.RfqId));
        await proposals.SaveAsync(proposal);
        await audit.LogAsync(UserId, "ItineraryChosen", "Rfq", rfq.RfqId, proposal.SourceLabel);
        return Done($"{package.DmcAlias} · {package.Name} chosen for final itinerary ✓");
    }

    private async Task<Rfq?> OwnRfqAsync(int id)
    {
        var rfq = await rfqs.GetAsync(id);
        return rfq?.AgentUserId == UserId ? rfq : null;
    }
}
