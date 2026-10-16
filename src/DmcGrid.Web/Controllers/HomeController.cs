using DmcGrid.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

public class HomeController(IRfqRepository rfqs) : AppController
{
    public async Task<IActionResult> Index() => View(await rfqs.MarketPulseAsync());

    public IActionResult Pricing() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
