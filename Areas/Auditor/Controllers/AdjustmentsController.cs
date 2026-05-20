using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class AdjustmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public AdjustmentsController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? type, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid filters.";
            return RedirectToAction(nameof(Index));
        }
        var shopId = User.GetShopId();

        var query = _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId)
            .Include(a => a.Invoice)
            .Include(a => a.CreatedByUser)
            .Include(a => a.ReviewedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<AdjustmentType>(type, true, out var adjType))
            query = query.Where(a => a.AdjustmentType == adjType);

        var adjustments = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.AdjustmentId,
                Type = a.AdjustmentType.ToString(),
                InvoiceNumber = a.Invoice != null ? a.Invoice.InvoiceNo : "—",
                CustomerName = a.Invoice != null && a.Invoice.Customer != null ? a.Invoice.Customer.FirstName + " " + a.Invoice.Customer.LastName : "—",
                a.Amount,
                a.Reason,
                a.CreatedAt,
                Status = a.Status.ToString(),
                CreatedBy = a.CreatedByUser != null ? a.CreatedByUser.FirstName + " " + a.CreatedByUser.LastName : "—",
                ApprovedBy = a.ReviewedByUser != null ? a.ReviewedByUser.FirstName + " " + a.ReviewedByUser.LastName : "—"
            })
            .ToListAsync();

        // Totals by type (approved only)
        var approved = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved)
            .ToListAsync();

        ViewBag.Adjustments = adjustments;
        ViewBag.TypeFilter = type;
        ViewBag.TotalDiscounts = 0m; // No Discount type in enum
        ViewBag.TotalRefunds = approved.Where(a => a.AdjustmentType == AdjustmentType.Refund).Sum(a => a.Amount);
        ViewBag.TotalCredits = approved.Where(a => a.AdjustmentType == AdjustmentType.Credit).Sum(a => a.Amount);
        ViewBag.TotalDebits = approved.Where(a => a.AdjustmentType == AdjustmentType.Debit).Sum(a => a.Amount);

        return View();
    }
}
