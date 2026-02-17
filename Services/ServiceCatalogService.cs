using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

// ── Interface ────────────────────────────────────────────────────────────
public interface IServiceCatalogService
{
    Task<PagedResult<ServiceCatalogListItem>> GetListAsync(long shopId, PagedRequest req, string? categoryFilter);
    Task<ServiceCatalogDetail?> GetDetailAsync(long shopId, long serviceId);
    Task<ServiceCatalogListItem> CreateAsync(long shopId, CreateServiceRequest req);
    Task<ServiceCatalogListItem?> UpdateAsync(long shopId, long serviceId, UpdateServiceRequest req);
    Task<List<string>> GetCategoriesAsync(long shopId);
}

// ── DTOs ─────────────────────────────────────────────────────────────────
public class ServiceCatalogListItem
{
    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public long ServiceCategoryId { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }
}

public class ServiceCatalogDetail
{
    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public long ServiceCategoryId { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class CreateServiceRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public long? CategoryId { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateServiceRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public long? CategoryId { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
}

// ── Implementation ───────────────────────────────────────────────────────
public class ServiceCatalogService : IServiceCatalogService
{
    private readonly ApplicationDbContext _db;

    public ServiceCatalogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<string>> GetCategoriesAsync(long shopId)
    {
        return await _db.ServiceCategories
            .Where(c => c.ShopId == shopId)
            .OrderBy(c => c.CategoryName)
            .Select(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<PagedResult<ServiceCatalogListItem>> GetListAsync(long shopId, PagedRequest req, string? categoryFilter)
    {
        var query = _db.ServiceCatalogs
            .Include(s => s.ServiceCategory)
            .Where(s => s.ShopId == shopId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(s =>
                s.ServiceName.ToLower().Contains(term) ||
                (s.ServiceCategory != null && s.ServiceCategory.CategoryName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            query = query.Where(s => s.ServiceCategory != null && s.ServiceCategory.CategoryName == categoryFilter);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.ServiceName)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(s => new ServiceCatalogListItem
            {
                ServiceId = s.ServiceId,
                ServiceName = s.ServiceName,
                CategoryName = s.ServiceCategory != null ? s.ServiceCategory.CategoryName : "",
                ServiceCategoryId = s.ServiceCategoryId,
                BasePrice = s.BasePrice,
                IsActive = s.IsActive,
                UsageCount = s.JobOrderServices.Count
            })
            .ToListAsync();

        return new PagedResult<ServiceCatalogListItem>
        {
            Items = items,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        };
    }

    public async Task<ServiceCatalogDetail?> GetDetailAsync(long shopId, long serviceId)
    {
        var svc = await _db.ServiceCatalogs
            .Include(s => s.ServiceCategory)
            .Include(s => s.JobOrderServices)
            .Where(s => s.ShopId == shopId && s.ServiceId == serviceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (svc == null) return null;

        return new ServiceCatalogDetail
        {
            ServiceId = svc.ServiceId,
            ServiceName = svc.ServiceName,
            CategoryName = svc.ServiceCategory?.CategoryName ?? "",
            ServiceCategoryId = svc.ServiceCategoryId,
            BasePrice = svc.BasePrice,
            IsActive = svc.IsActive,
            UsageCount = svc.JobOrderServices.Count,
            TotalRevenue = svc.JobOrderServices.Sum(js => js.UnitPrice * js.Qty)
        };
    }

    public async Task<ServiceCatalogListItem> CreateAsync(long shopId, CreateServiceRequest req)
    {
        var categoryId = req.CategoryId ?? await ResolveOrCreateCategory(shopId, req.CategoryName);

        var entity = new ServiceCatalog
        {
            ShopId = shopId,
            ServiceCategoryId = categoryId,
            ServiceName = req.ServiceName,
            BasePrice = req.BasePrice,
            IsActive = req.IsActive
        };

        _db.ServiceCatalogs.Add(entity);
        await _db.SaveChangesAsync();

        var cat = await _db.ServiceCategories.FindAsync(categoryId);

        return new ServiceCatalogListItem
        {
            ServiceId = entity.ServiceId,
            ServiceName = entity.ServiceName,
            CategoryName = cat?.CategoryName ?? "",
            ServiceCategoryId = entity.ServiceCategoryId,
            BasePrice = entity.BasePrice,
            IsActive = entity.IsActive
        };
    }

    public async Task<ServiceCatalogListItem?> UpdateAsync(long shopId, long serviceId, UpdateServiceRequest req)
    {
        var entity = await _db.ServiceCatalogs
            .Include(s => s.ServiceCategory)
            .Where(s => s.ShopId == shopId && s.ServiceId == serviceId)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        var categoryId = req.CategoryId ?? await ResolveOrCreateCategory(shopId, req.CategoryName);

        entity.ServiceName = req.ServiceName;
        entity.ServiceCategoryId = categoryId;
        entity.BasePrice = req.BasePrice;
        entity.IsActive = req.IsActive;

        await _db.SaveChangesAsync();

        var cat = await _db.ServiceCategories.FindAsync(categoryId);

        return new ServiceCatalogListItem
        {
            ServiceId = entity.ServiceId,
            ServiceName = entity.ServiceName,
            CategoryName = cat?.CategoryName ?? "",
            ServiceCategoryId = entity.ServiceCategoryId,
            BasePrice = entity.BasePrice,
            IsActive = entity.IsActive
        };
    }

    private async Task<long> ResolveOrCreateCategory(long shopId, string categoryName)
    {
        var existing = await _db.ServiceCategories
            .Where(c => c.ShopId == shopId && c.CategoryName == categoryName)
            .FirstOrDefaultAsync();

        if (existing != null) return existing.ServiceCategoryId;

        var newCat = new ServiceCategory
        {
            ShopId = shopId,
            CategoryName = categoryName
        };
        _db.ServiceCategories.Add(newCat);
        await _db.SaveChangesAsync();

        return newCat.ServiceCategoryId;
    }
}
