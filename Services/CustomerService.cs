using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Customers;
using ByteBill_BS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetListAsync(long shopId, PagedRequest req);
    Task<CustomerDetailDto?> GetByIdAsync(long shopId, long customerId);
    Task<CustomerListItemDto> CreateAsync(long shopId, long userId, CreateCustomerRequest req);
    Task<CustomerListItemDto?> UpdateAsync(long shopId, long userId, long customerId, UpdateCustomerRequest req);
    Task<bool> ToggleStatusAsync(long shopId, long userId, long customerId);
}

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpCtx;

    public CustomerService(ApplicationDbContext db, IAuditService audit, IHttpContextAccessor httpCtx)
    {
        _db = db;
        _audit = audit;
        _httpCtx = httpCtx;
    }

    private string? ClientIp => _httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString();

    // ── List / Search ────────────────────────────────────────────────────
    public async Task<PagedResult<CustomerListItemDto>> GetListAsync(long shopId, PagedRequest req)
    {
        var query = _db.Customers
            .Where(c => c.ShopId == shopId)
            .AsNoTracking();

        // Search by name, email, phone
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(c =>
                (c.FirstName + " " + c.LastName).ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        var totalCount = await query.CountAsync();

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(c => new CustomerListItemDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Initials = (c.FirstName.Substring(0, 1) + c.LastName.Substring(0, 1)).ToUpper(),
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                Orders = c.JobOrders.Count,
                TotalSpent = c.Invoices
                    .SelectMany(i => i.PaymentAllocations)
                    .Sum(pa => pa.AmountApplied)
            })
            .ToListAsync();

        return new PagedResult<CustomerListItemDto>
        {
            Items = customers,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        };
    }

    // ── Get by ID ────────────────────────────────────────────────────────
    public async Task<CustomerDetailDto?> GetByIdAsync(long shopId, long customerId)
    {
        return await _db.Customers
            .Where(c => c.ShopId == shopId && c.CustomerId == customerId)
            .AsNoTracking()
            .Select(c => new CustomerDetailDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Initials = (c.FirstName.Substring(0, 1) + c.LastName.Substring(0, 1)).ToUpper(),
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                Orders = c.JobOrders.Count,
                TotalSpent = c.Invoices
                    .SelectMany(i => i.PaymentAllocations)
                    .Sum(pa => pa.AmountApplied),
                Devices = c.Devices.Select(d => new CustomerDeviceDto
                {
                    DeviceId = d.DeviceId,
                    DeviceType = d.DeviceType,
                    Brand = d.Brand,
                    Model = d.Model,
                    SerialNo = d.SerialNo,
                    Notes = d.Notes
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    // ── Create ───────────────────────────────────────────────────────────
    public async Task<CustomerListItemDto> CreateAsync(long shopId, long userId, CreateCustomerRequest req)
    {
        // ── Duplicate checks ─────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var emailExists = await _db.Customers
                .AnyAsync(c => c.ShopId == shopId && c.Email == req.Email.Trim() && c.IsActive);
            if (emailExists)
                throw new InvalidOperationException("A customer with this email address already exists.");
        }

        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            var phoneExists = await _db.Customers
                .AnyAsync(c => c.ShopId == shopId && c.Phone == req.Phone.Trim() && c.IsActive);
            if (phoneExists)
                throw new InvalidOperationException("A customer with this phone number already exists.");
        }

        // Check duplicate full name (same first + last)
        var nameExists = await _db.Customers
            .AnyAsync(c => c.ShopId == shopId
                && c.FirstName.ToLower() == req.FirstName.Trim().ToLower()
                && c.LastName.ToLower() == req.LastName.Trim().ToLower()
                && c.IsActive);
        if (nameExists)
            throw new InvalidOperationException("A customer with this name already exists. Use a middle name or initial to differentiate.");

        var customer = new Customer
        {
            ShopId = shopId,
            FirstName = req.FirstName.Trim(),
            MiddleName = req.MiddleName?.Trim(),
            LastName = req.LastName.Trim(),
            Email = req.Email?.Trim(),
            Phone = req.Phone?.Trim(),
            Address = req.Address?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Create", "Customer", customer.CustomerId,
            $"Created customer '{customer.FullName}'.", ClientIp);

        return new CustomerListItemDto
        {
            CustomerId = customer.CustomerId,
            FirstName = customer.FirstName,
            MiddleName = customer.MiddleName,
            LastName = customer.LastName,
            FullName = customer.FullName,
            Initials = customer.Initials,
            Email = customer.Email,
            Phone = customer.Phone,
            Address = customer.Address,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            Orders = 0,
            TotalSpent = 0
        };
    }

    // ── Update ───────────────────────────────────────────────────────────
    public async Task<CustomerListItemDto?> UpdateAsync(long shopId, long userId, long customerId, UpdateCustomerRequest req)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.CustomerId == customerId);

        if (customer is null) return null;

        // ── Duplicate checks (exclude current) ───────────────────────
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var emailExists = await _db.Customers
                .AnyAsync(c => c.ShopId == shopId && c.CustomerId != customerId && c.Email == req.Email.Trim() && c.IsActive);
            if (emailExists)
                throw new InvalidOperationException("Another customer with this email address already exists.");
        }

        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            var phoneExists = await _db.Customers
                .AnyAsync(c => c.ShopId == shopId && c.CustomerId != customerId && c.Phone == req.Phone.Trim() && c.IsActive);
            if (phoneExists)
                throw new InvalidOperationException("Another customer with this phone number already exists.");
        }

        customer.FirstName = req.FirstName.Trim();
        customer.MiddleName = req.MiddleName?.Trim();
        customer.LastName = req.LastName.Trim();
        customer.Email = req.Email?.Trim();
        customer.Phone = req.Phone?.Trim();
        customer.Address = req.Address?.Trim();

        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Update", "Customer", customer.CustomerId,
            $"Updated customer '{customer.FullName}'.", ClientIp);

        // Re-read with computed fields
        return await GetListItemAsync(shopId, customerId);
    }

    // ── Toggle Active / Inactive ─────────────────────────────────────────
    public async Task<bool> ToggleStatusAsync(long shopId, long userId, long customerId)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.CustomerId == customerId);

        if (customer is null) return false;

        customer.IsActive = !customer.IsActive;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, customer.IsActive ? "Activate" : "Deactivate",
            "Customer", customer.CustomerId,
            $"Customer '{customer.FullName}' set to {(customer.IsActive ? "Active" : "Inactive")}.", ClientIp);

        return true;
    }

    // ── Helper ───────────────────────────────────────────────────────────
    private async Task<CustomerListItemDto?> GetListItemAsync(long shopId, long customerId)
    {
        return await _db.Customers
            .Where(c => c.ShopId == shopId && c.CustomerId == customerId)
            .AsNoTracking()
            .Select(c => new CustomerListItemDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Initials = (c.FirstName.Substring(0, 1) + c.LastName.Substring(0, 1)).ToUpper(),
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                Orders = c.JobOrders.Count,
                TotalSpent = c.Invoices
                    .SelectMany(i => i.PaymentAllocations)
                    .Sum(pa => pa.AmountApplied)
            })
            .FirstOrDefaultAsync();
    }
}
