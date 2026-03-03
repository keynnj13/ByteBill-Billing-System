using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

// ── Interface ────────────────────────────────────────────────────────────
public interface IInventoryService
{
    Task<InventoryPagedResult> GetListAsync(long shopId, PagedRequest req, string? categoryFilter, bool? lowStockOnly);
    Task<InventoryDetailDto?> GetDetailAsync(long shopId, long itemId);
    Task<InventoryListItemDto> CreateAsync(long shopId, CreateInventoryItemRequest req);
    Task<InventoryListItemDto?> UpdateAsync(long shopId, long itemId, UpdateInventoryItemRequest req);
    Task<bool> AdjustStockAsync(long shopId, long itemId, AdjustStockRequest req);
    Task<List<string>> GetCategoriesAsync(long shopId);
}

// ── DTOs ─────────────────────────────────────────────────────────────────
public class InventoryPagedResult
{
    public List<InventoryListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int LowStockCount { get; set; }
}

public class InventoryListItemDto
{
    public long ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock => QtyOnHand <= ReorderLevel;
}

public class InventoryDetailDto
{
    public long ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public long? InventoryCategoryId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock => QtyOnHand <= ReorderLevel;
    public List<InventoryTxnDto> RecentTransactions { get; set; } = new();
}

public class InventoryTxnDto
{
    public long Id { get; set; }
    public string TxnType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateInventoryItemRequest
{
    public string SKU { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public long? CategoryId { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public bool IsActive { get; set; } = true;
}

public class UpdateInventoryItemRequest
{
    public string SKU { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public long? CategoryId { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }
}

public class AdjustStockRequest
{
    public InventoryTxnType TxnType { get; set; }
    public int Quantity { get; set; }
    public string? Remarks { get; set; }
}

// ── Implementation ───────────────────────────────────────────────────────
public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notif;

    public InventoryService(ApplicationDbContext db, INotificationService notif)
    {
        _db = db;
        _notif = notif;
    }

    public async Task<List<string>> GetCategoriesAsync(long shopId)
    {
        return await _db.Set<InventoryCategory>()
            .Where(c => c.ShopId == shopId)
            .OrderBy(c => c.CategoryName)
            .Select(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<InventoryPagedResult> GetListAsync(long shopId, PagedRequest req, string? categoryFilter, bool? lowStockOnly)
    {
        var query = _db.InventoryItems
            .Include(i => i.InventoryCategory)
            .Where(i => i.ShopId == shopId && i.IsActive)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(i =>
                i.ItemName.ToLower().Contains(term) ||
                i.SKU.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            query = query.Where(i => i.InventoryCategory != null && i.InventoryCategory.CategoryName == categoryFilter);
        }

        if (lowStockOnly == true)
        {
            query = query.Where(i => i.QtyOnHand <= i.ReorderLevel);
        }

        var totalCount = await query.CountAsync();
        var lowStockCount = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.QtyOnHand <= i.ReorderLevel)
            .CountAsync();

        var items = await query
            .OrderBy(i => i.ItemName)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(i => new InventoryListItemDto
            {
                ItemId = i.ItemId,
                SKU = i.SKU,
                ItemName = i.ItemName,
                CategoryName = i.InventoryCategory != null ? i.InventoryCategory.CategoryName : null,
                Unit = i.Unit,
                UnitCost = i.UnitCost,
                UnitPrice = i.UnitPrice,
                QtyOnHand = i.QtyOnHand,
                ReorderLevel = i.ReorderLevel,
                IsActive = i.IsActive
            })
            .ToListAsync();

        return new InventoryPagedResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize,
            LowStockCount = lowStockCount
        };
    }

    public async Task<InventoryDetailDto?> GetDetailAsync(long shopId, long itemId)
    {
        var item = await _db.InventoryItems
            .Include(i => i.InventoryCategory)
            .Where(i => i.ShopId == shopId && i.ItemId == itemId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (item == null) return null;

        var txns = await _db.InventoryTxns
            .Where(t => t.ItemId == itemId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new InventoryTxnDto
            {
                Id = t.InventoryTxnId,
                TxnType = t.TxnType.ToString(),
                Quantity = t.Quantity,
                ReferenceType = t.ReferenceType,
                ReferenceId = t.ReferenceId,
                Remarks = t.Remarks,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new InventoryDetailDto
        {
            ItemId = item.ItemId,
            SKU = item.SKU,
            ItemName = item.ItemName,
            CategoryName = item.InventoryCategory?.CategoryName,
            InventoryCategoryId = item.InventoryCategoryId,
            Unit = item.Unit,
            UnitCost = item.UnitCost,
            UnitPrice = item.UnitPrice,
            QtyOnHand = item.QtyOnHand,
            ReorderLevel = item.ReorderLevel,
            IsActive = item.IsActive,
            RecentTransactions = txns
        };
    }

    public async Task<InventoryListItemDto> CreateAsync(long shopId, CreateInventoryItemRequest req)
    {
        var categoryId = await ResolveOrCreateCategoryAsync(shopId, req.CategoryName, req.CategoryId);

        var entity = new InventoryItem
        {
            ShopId = shopId,
            InventoryCategoryId = categoryId,
            SKU = req.SKU,
            ItemName = req.ItemName,
            Unit = req.Unit,
            UnitCost = req.UnitCost,
            UnitPrice = req.UnitPrice,
            QtyOnHand = req.QtyOnHand,
            ReorderLevel = req.ReorderLevel,
            IsActive = req.IsActive
        };

        _db.InventoryItems.Add(entity);
        await _db.SaveChangesAsync();

        // Record initial stock transaction
        if (req.QtyOnHand > 0)
        {
            _db.InventoryTxns.Add(new InventoryTxn
            {
                ItemId = entity.ItemId,
                TxnType = InventoryTxnType.IN,
                Quantity = req.QtyOnHand,
                Remarks = "Initial stock"
            });
            await _db.SaveChangesAsync();
        }

        return new InventoryListItemDto
        {
            ItemId = entity.ItemId,
            SKU = entity.SKU,
            ItemName = entity.ItemName,
            Unit = entity.Unit,
            UnitCost = entity.UnitCost,
            UnitPrice = entity.UnitPrice,
            QtyOnHand = entity.QtyOnHand,
            ReorderLevel = entity.ReorderLevel,
            IsActive = entity.IsActive
        };
    }

    public async Task<InventoryListItemDto?> UpdateAsync(long shopId, long itemId, UpdateInventoryItemRequest req)
    {
        var entity = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.ItemId == itemId)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        entity.InventoryCategoryId = await ResolveOrCreateCategoryAsync(shopId, req.CategoryName, req.CategoryId);
        entity.SKU = req.SKU;
        entity.ItemName = req.ItemName;
        entity.Unit = req.Unit;
        entity.UnitCost = req.UnitCost;
        entity.UnitPrice = req.UnitPrice;
        entity.ReorderLevel = req.ReorderLevel;
        entity.IsActive = req.IsActive;

        // Handle stock quantity change with transaction logging
        var oldQty = entity.QtyOnHand;
        if (req.QtyOnHand != oldQty)
        {
            var diff = req.QtyOnHand - oldQty;
            entity.QtyOnHand = req.QtyOnHand;

            _db.InventoryTxns.Add(new InventoryTxn
            {
                ItemId = itemId,
                TxnType = diff > 0 ? InventoryTxnType.IN : InventoryTxnType.OUT,
                Quantity = Math.Abs(diff),
                Remarks = $"Stock adjusted via edit: {oldQty} → {req.QtyOnHand}"
            });
        }

        await _db.SaveChangesAsync();

        return new InventoryListItemDto
        {
            ItemId = entity.ItemId,
            SKU = entity.SKU,
            ItemName = entity.ItemName,
            Unit = entity.Unit,
            UnitCost = entity.UnitCost,
            UnitPrice = entity.UnitPrice,
            QtyOnHand = entity.QtyOnHand,
            ReorderLevel = entity.ReorderLevel,
            IsActive = entity.IsActive
        };
    }

    public async Task<bool> AdjustStockAsync(long shopId, long itemId, AdjustStockRequest req)
    {
        var entity = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.ItemId == itemId)
            .FirstOrDefaultAsync();

        if (entity == null) return false;

        switch (req.TxnType)
        {
            case InventoryTxnType.IN:
                entity.QtyOnHand += req.Quantity;
                break;
            case InventoryTxnType.OUT:
                entity.QtyOnHand = Math.Max(0, entity.QtyOnHand - req.Quantity);
                break;
            case InventoryTxnType.ADJUST:
                entity.QtyOnHand = req.Quantity;
                break;
        }

        _db.InventoryTxns.Add(new InventoryTxn
        {
            ItemId = itemId,
            TxnType = req.TxnType,
            Quantity = req.Quantity,
            Remarks = req.Remarks
        });

        await _db.SaveChangesAsync();

        // Notify technicians working on WaitingForParts JOs that use this item
        if (req.TxnType == InventoryTxnType.IN)
        {
            var techsToNotify = await _db.JobOrderParts
                .Include(p => p.JobOrder)
                .Include(p => p.Item)
                .Where(p => p.ItemId == itemId
                    && p.JobOrder!.ShopId == shopId
                    && p.JobOrder.Status == JobOrderStatus.WaitingForParts
                    && p.JobOrder.AssignedTechUserId != null)
                .Select(p => new { p.JobOrder!.AssignedTechUserId, p.Item!.ItemName })
                .Distinct()
                .ToListAsync();

            foreach (var t in techsToNotify)
            {
                await _notif.CreateAsync(
                    t.AssignedTechUserId!.Value, shopId,
                    "Parts Restocked",
                    $"{t.ItemName} has been restocked and is now available.",
                    "info",
                    null);
            }
        }

        // Low stock notification for admins
        if (entity.IsLowStock)
        {
            var adminIds = await _db.Users
                .Where(u => u.ShopId == shopId && u.IsActive
                    && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
                .Select(u => u.UserId)
                .ToListAsync();
            foreach (var adminId in adminIds)
            {
                await _notif.CreateAsync(adminId, shopId,
                    "Low Stock Alert",
                    $"{entity.ItemName} is running low ({entity.QtyOnHand} remaining, reorder level: {entity.ReorderLevel}).",
                    "warning",
                    $"/Admin/Inventory");
            }
        }

        return true;
    }

    private async Task<long?> ResolveOrCreateCategoryAsync(long shopId, string? categoryName, long? categoryId)
    {
        if (categoryId.HasValue && categoryId.Value > 0)
            return categoryId.Value;

        if (string.IsNullOrWhiteSpace(categoryName))
            return null;

        var existing = await _db.Set<InventoryCategory>()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.CategoryName == categoryName.Trim());

        if (existing != null) return existing.InventoryCategoryId;

        var newCat = new InventoryCategory
        {
            ShopId = shopId,
            CategoryName = categoryName.Trim()
        };
        _db.Set<InventoryCategory>().Add(newCat);
        await _db.SaveChangesAsync();
        return newCat.InventoryCategoryId;
    }
}
