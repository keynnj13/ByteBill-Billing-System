using ByteBill_BS.Data;
using ByteBill_BS.Models;

namespace ByteBill_BS.Services;

public interface IAuditService
{
    Task LogAsync(long shopId, long userId, string action, string entityName, long entityId,
        string? details = null, string? ipAddress = null, string? oldValues = null, string? newValues = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(long shopId, long userId, string action, string entityName, long entityId,
        string? details = null, string? ipAddress = null, string? oldValues = null, string? newValues = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ShopId = shopId,
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details?.Length > 500 ? details[..500] : details,
            IpAddress = ipAddress?.Length > 45 ? ipAddress[..45] : ipAddress,
            OldValues = oldValues?.Length > 2000 ? oldValues[..2000] : oldValues,
            NewValues = newValues?.Length > 2000 ? newValues[..2000] : newValues,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
