using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers.Api;

/// <summary>
/// Job Orders API — all queries scoped by ShopID.
/// Roles: Admin, Billing, Technician can create; Auditor read-only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobOrdersApiController : ControllerBase
{
    private readonly IJobOrderService _svc;

    public JobOrdersApiController(IJobOrderService svc)
    {
        _svc = svc;
    }

    // GET api/jobordersapi?page=1&pageSize=10&search=JO-2026&statusFilter=Pending
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] JobOrderPagedRequest req)
    {
        var shopId = User.GetShopId();
        var result = await _svc.GetListAsync(shopId, req);
        return Ok(ApiResponse<PagedResult<JobOrderListItemDto>>.Ok(result));
    }

    // GET api/jobordersapi/5
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _svc.GetDetailAsync(shopId, id);
        if (dto is null)
            return NotFound(ApiResponse<object>.Fail("Job order not found."));
        return Ok(ApiResponse<JobOrderDetailDto>.Ok(dto));
    }

    // POST api/jobordersapi
    [HttpPost]
    [Authorize(Policy = "TechnicianOrAbove")]
    public async Task<IActionResult> Create([FromBody] CreateJobOrderRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.CreateAsync(shopId, userId, req);
        if (!result.Success)
            return BadRequest(result);
        return CreatedAtAction(nameof(GetDetail), new { id = result.Data!.JobOrderId }, result);
    }

    // PATCH api/jobordersapi/5/status
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = "TechnicianOrAbove")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateJobOrderStatusRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var role = User.GetRole();
        var result = await _svc.UpdateStatusAsync(shopId, userId, role, id, req);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    // PUT api/jobordersapi/5/assign
    [HttpPut("{id:long}/assign")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> AssignTechnician(long id, [FromBody] AssignTechnicianRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.AssignTechnicianAsync(shopId, userId, id, req);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
