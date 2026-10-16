using System.Globalization;
using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;
using DmcGrid.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class RfqController(IRfqRepository rfqs, IRfqCopilot copilot, IAuditRepository audit) : AppController
{
    /// <summary>New RFQ, or (with an id) the agent's saved draft.</summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? id)
    {
        if (id is null)
        {
            var today = DateTime.Today;
            return View(new Rfq { TravelYear = today.Year, TravelMonth = today.ToString("MMMM", CultureInfo.InvariantCulture) });
        }

        var rfq = await rfqs.GetAsync(id.Value);
        if (rfq is null || rfq.AgentUserId != UserId) return NotFound();
        if (rfq.Status != RfqStatus.Draft) return RedirectToAction("Index", "Compare", new { id });
        return View(rfq);
    }

    [HttpPost]
    public IActionResult Structure([FromBody] StructureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text)) return Fail("Describe the trip first");
        if (request.Text.Length > 2000) return Fail("Please keep the description under 2,000 characters");
        return Done("AI structured the RFQ ✓", copilot.Structure(request.Text, DateTime.Today));
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveRfqRequest request)
    {
        var rfq = request?.Rfq;
        if (rfq is null) return Fail("Invalid RFQ");

        rfq.Destination = (rfq.Destination ?? "").Trim();
        rfq.Adults = Math.Clamp(rfq.Adults, 1, 99);
        rfq.Children = Math.Clamp(rfq.Children, 0, 20);
        if (rfq.StartDate.HasValue && rfq.EndDate.HasValue && rfq.EndDate < rfq.StartDate)
            (rfq.StartDate, rfq.EndDate) = (rfq.EndDate, rfq.StartDate);

        if (request!.Publish)
        {
            if (rfq.Destination.Length == 0) return Fail("Destination is required before publishing");
            if (rfq.StartDate is null || rfq.EndDate is null) return Fail("Select travel start and end dates before publishing");
            if (rfq.StartDate < DateTime.Today) return Fail("Travel dates cannot be in the past");
        }

        rfq.AgentUserId = UserId;
        rfq.Status = request.Publish ? RfqStatus.Published : RfqStatus.Draft;
        var id = await rfqs.SaveAsync(rfq);
        if (id == 0) return Fail("This RFQ is already published and can no longer be edited");

        await audit.LogAsync(UserId, request.Publish ? "RfqPublished" : "RfqDraftSaved", "Rfq", id);
        return Done(request.Publish ? "RFQ published to matched DMCs ✓" : "RFQ draft saved ✓", new { id });
    }
}
