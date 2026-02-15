using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Payments;
using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers.Api;

/// <summary>
/// Payments API — all queries scoped by ShopID.
/// Roles: Admin, Billing can record payments; Technician/Auditor read-only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsApiController : ControllerBase
{
    private readonly IPaymentService _svc;

    public PaymentsApiController(IPaymentService svc)
    {
        _svc = svc;
    }

    // GET api/paymentsapi?page=1&pageSize=10&search=Juan
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] PaymentPagedRequest req)
    {
        var shopId = User.GetShopId();
        var result = await _svc.GetListAsync(shopId, req);
        return Ok(ApiResponse<PagedResult<PaymentListItemDto>>.Ok(result));
    }

    // GET api/paymentsapi/metrics
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var shopId = User.GetShopId();
        var metrics = await _svc.GetMetricsAsync(shopId);
        return Ok(ApiResponse<PaymentMetricsDto>.Ok(metrics));
    }

    // GET api/paymentsapi/5
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _svc.GetDetailAsync(shopId, id);
        if (dto is null)
            return NotFound(ApiResponse<object>.Fail("Payment not found."));
        return Ok(ApiResponse<PaymentDetailDto>.Ok(dto));
    }

    // POST api/paymentsapi
    [HttpPost]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.RecordPaymentAsync(shopId, userId, req);
        if (!result.Success)
            return BadRequest(result);
        return CreatedAtAction(nameof(GetDetail), new { id = result.Data!.PaymentId }, result);
    }
}
