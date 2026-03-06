using ByteBill_BS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Controllers;

/// <summary>
/// Public landing page — no authentication required.
/// Shows ByteBill marketing page with features, pricing, and CTA.
/// </summary>
public class LandingController : Controller
{
    private readonly ApplicationDbContext _db;

    public LandingController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // If user is already authenticated, redirect to their dashboard
        if (User.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToAction("Index", "Home");
        }

        // Load active subscription plans for pricing section
        var plans = await _db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        ViewData["HideNavigation"] = true;
        return View(plans);
    }
}
