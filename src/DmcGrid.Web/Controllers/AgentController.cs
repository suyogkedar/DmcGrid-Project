using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;
using DmcGrid.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

[Authorize(Roles = Roles.Agent)]
public class AgentController(IRfqRepository rfqs) : AppController
{
    public async Task<IActionResult> Index() => View(new AgentDashboardViewModel
    {
        Stats = await rfqs.AgentStatsAsync(UserId),
        Rfqs = await rfqs.ListForAgentAsync(UserId)
    });
}

[Authorize(Roles = Roles.Dmc)]
public class DmcController(IRfqRepository rfqs, IQuoteRepository quotes) : AppController
{
    public async Task<IActionResult> Index()
    {
        var rows = await rfqs.ListForDmcAsync(UserId);
        foreach (var row in rows.Where(r => r.HasQuote))
        {
            var packages = await quotes.GetForDmcAsync(row.RfqId, UserId);
            if (packages.Count > 0) row.BestMatch = packages.Max(p => MatchScorer.Score(row, p).Pct);
        }
        return View(new DmcDashboardViewModel { Stats = await rfqs.DmcStatsAsync(UserId), Rfqs = rows });
    }
}
