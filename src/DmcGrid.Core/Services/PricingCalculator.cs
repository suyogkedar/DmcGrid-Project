using DmcGrid.Core.Models;

namespace DmcGrid.Core.Services;

/// <summary>Single source of truth for the customer price. The proposal screen mirrors this formula for live preview.</summary>
public static class PricingCalculator
{
    public static PriceBreakdown Calculate(Proposal p)
    {
        var margin = p.MarginType == "Fixed" ? p.Margin : p.NetCost * p.Margin / 100m;
        var extras = p.Costs.Sum(c => c.Charge);
        var sub = p.NetCost + margin + extras;
        var gst = sub * p.GstPct / 100m;
        var tcs = (sub + gst) * p.TcsPct / 100m;
        return new PriceBreakdown
        {
            Net = p.NetCost,
            Margin = margin,
            Extras = extras,
            Gst = gst,
            Tcs = tcs,
            Total = sub + gst + tcs
        };
    }
}
