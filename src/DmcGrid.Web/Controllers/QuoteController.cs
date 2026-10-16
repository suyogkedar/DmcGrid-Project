using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;
using DmcGrid.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

[Authorize(Roles = Roles.Dmc)]
public class QuoteController(IRfqRepository rfqs, IQuoteRepository quotes, IAuditRepository audit) : AppController
{
    /// <summary>Quote builder for an RFQ (id = RFQ id).</summary>
    [HttpGet]
    public async Task<IActionResult> Builder(int id)
    {
        var rfq = await rfqs.GetAsync(id);
        if (rfq is null || rfq.Status == RfqStatus.Draft) return NotFound();

        var packages = await quotes.GetForDmcAsync(id, UserId);
        if (packages.Count == 0)
        {
            var starter = new QuotePackage { Name = "Option 1 · New Package", Hotel = rfq.HotelCategory, Room = "Deluxe", Meal = "Breakfast", Vehicle = "Private" };
            for (var d = 0; d <= (rfq.EffectiveNights ?? 0); d++) starter.Days.Add([]);
            packages.Add(starter);
        }
        return View(new BuilderViewModel
        {
            Rfq = rfq,
            Packages = packages,
            Connected = (await quotes.ConnectedDmcIdsAsync(id)).Contains(UserId)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveQuoteRequest request)
    {
        var packages = request?.Packages;
        if (packages is null || packages.Count == 0) return Fail("At least one package option is required");
        if (packages.Count > 20) return Fail("A quote can hold at most 20 package options");

        foreach (var p in packages)
        {
            p.Days = (p.Days ?? []).Select(day => day ?? []).ToList();
            if (p.Days.Count == 0) p.Days.Add([]);
            if (!request!.Submit) continue;
            if (string.IsNullOrWhiteSpace(p.Name)) return Fail("Every package option needs a name");
            if (p.NetPrice <= 0) return Fail($"Enter the net price for “{p.Name.Trim()}” before submitting");
            if (p.Days.All(day => day.Count == 0)) return Fail($"Add the day-wise itinerary for “{p.Name.Trim()}” before submitting");
        }

        var error = await quotes.SaveAsync(request!.RfqId, UserId, packages, request.Submit);
        if (error != null) return Fail(error);

        await audit.LogAsync(UserId, request.Submit ? "QuoteSubmitted" : "QuoteSaved", "Rfq", request.RfqId, $"{packages.Count} package(s)");
        return Done(request.Submit ? $"{packages.Count} package option{(packages.Count > 1 ? "s" : "")} submitted ✓" : "Day itinerary saved ✓",
            await quotes.GetForDmcAsync(request.RfqId, UserId));
    }

    /// <summary>Quote copilot: a draft option mapped from the RFQ for the DMC to verify and price.</summary>
    [HttpPost]
    public async Task<IActionResult> Draft([FromBody] RfqRequest request)
    {
        var rfq = request is null ? null : await rfqs.GetAsync(request.RfqId);
        if (rfq is null || rfq.Status == RfqStatus.Draft) return Fail("RFQ not found");
        var category = rfq.HotelCategory.Length > 0 ? " " + rfq.HotelCategory : "";
        return Done("AI draft created · DMC must verify price and services ✓", QuoteDrafts.FromRfq(rfq, $"AI Draft{category}"));
    }

    [HttpPost]
    [RequestSizeLimit(QuoteDrafts.MaxDocumentBytes + 64 * 1024)]
    public async Task<IActionResult> Import(int rfqId, IFormFile? file)
    {
        var rfq = await rfqs.GetAsync(rfqId);
        if (rfq is null || rfq.Status == RfqStatus.Draft) return Fail("RFQ not found");
        if (file is null || file.Length == 0) return Fail("Choose a quotation document first");
        if (file.Length > QuoteDrafts.MaxDocumentBytes) return Fail("Document is larger than 10 MB");
        if (!QuoteDrafts.AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return Fail("Only PDF, DOC and DOCX quotations are supported");

        await using var stream = file.OpenReadStream();
        var result = QuoteDrafts.FromDocument(rfq, Path.GetFileName(file.FileName), stream);
        await audit.LogAsync(UserId, "QuoteDocumentImported", "Rfq", rfqId, Path.GetFileName(file.FileName));
        return Done(result.Message, result);
    }
}
