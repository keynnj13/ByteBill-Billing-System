using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Invoices;
using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers.Api;

/// <summary>
/// Invoices API — all queries scoped by ShopID.
/// Roles: Admin, Billing can create; Technician/Auditor read-only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesApiController : ControllerBase
{
    private readonly IInvoiceService _svc;

    public InvoicesApiController(IInvoiceService svc)
    {
        _svc = svc;
    }

    // GET api/invoicesapi?page=1&pageSize=10&search=INV-2026&statusFilter=Unpaid
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] InvoicePagedRequest req)
    {
        var shopId = User.GetShopId();
        var result = await _svc.GetListAsync(shopId, req);
        return Ok(ApiResponse<PagedResult<InvoiceListItemDto>>.Ok(result));
    }

    // GET api/invoicesapi/metrics
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var shopId = User.GetShopId();
        var metrics = await _svc.GetMetricsAsync(shopId);
        return Ok(ApiResponse<InvoiceMetricsDto>.Ok(metrics));
    }

    // GET api/invoicesapi/5
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _svc.GetDetailAsync(shopId, id);
        if (dto is null)
            return NotFound(ApiResponse<object>.Fail("Invoice not found."));
        return Ok(ApiResponse<InvoiceDetailDto>.Ok(dto));
    }

    // POST api/invoicesapi
    [HttpPost]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> CreateFromJobOrder([FromBody] CreateInvoiceRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.CreateFromJobOrderAsync(shopId, userId, req);
        if (!result.Success)
            return BadRequest(result);
        return CreatedAtAction(nameof(GetDetail), new { id = result.Data!.InvoiceId }, result);
    }

    // POST api/invoicesapi/5/adjustments
    [HttpPost("{id:long}/adjustments")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> CreateAdjustment(long id, [FromBody] DTOs.Invoices.CreateAdjustmentRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.CreateAdjustmentAsync(shopId, userId, id, req);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
