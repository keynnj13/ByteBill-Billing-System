using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Customers;
using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers.Api;

/// <summary>
/// Customers CRUD API — all queries scoped by ShopID.
/// Roles: Admin, Billing can create/update; Auditor read-only; Technician read-only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ICustomerService _svc;

    public CustomersApiController(ICustomerService svc)
    {
        _svc = svc;
    }

    // GET api/customersapi?page=1&pageSize=10&search=juan
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest req)
    {
        var shopId = User.GetShopId();
        var result = await _svc.GetListAsync(shopId, req);
        return Ok(ApiResponse<PagedResult<CustomerListItemDto>>.Ok(result));
    }

    // GET api/customersapi/5
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _svc.GetByIdAsync(shopId, id);
        if (dto is null)
            return NotFound(ApiResponse<object>.Fail("Customer not found."));
        return Ok(ApiResponse<CustomerDetailDto>.Ok(dto));
    }

    // POST api/customersapi
    [HttpPost]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        try
        {
            var shopId = User.GetShopId();
            var userId = User.GetUserId();
            var dto = await _svc.CreateAsync(shopId, userId, req);
            return CreatedAtAction(nameof(GetById), new { id = dto.CustomerId },
                ApiResponse<CustomerListItemDto>.Ok(dto, "Customer created."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // PUT api/customersapi/5
    [HttpPut("{id:long}")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomerRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        try
        {
            var shopId = User.GetShopId();
            var userId = User.GetUserId();
            var dto = await _svc.UpdateAsync(shopId, userId, id, req);
            if (dto is null)
                return NotFound(ApiResponse<object>.Fail("Customer not found."));
            return Ok(ApiResponse<CustomerListItemDto>.Ok(dto, "Customer updated."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // PATCH api/customersapi/5/status
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> ToggleStatus(long id)
    {
        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var ok = await _svc.ToggleStatusAsync(shopId, userId, id);
        if (!ok)
            return NotFound(ApiResponse<object>.Fail("Customer not found."));
        return Ok(ApiResponse<bool>.Ok(true, "Status toggled."));
    }
}
