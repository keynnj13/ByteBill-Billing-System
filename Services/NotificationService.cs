using ByteBill_BS.Data;
using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface INotificationService
{
    Task<List<NotificationDto>> GetByUserAsync(long userId, int take = 20);
    Task<int> GetUnreadCountAsync(long userId);
    Task CreateAsync(long userId, long shopId, string title, string message, string type, string? url = null);
    Task NotifySuperAdminsAsync(long shopId, string title, string message, string type, string? url = null);
    Task MarkAsReadAsync(long notificationId, long userId);
    Task MarkAllReadAsync(long userId);
}

public class NotificationDto
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "info";
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo => FormatTimeAgo(CreatedAt);

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.UtcNow - dt;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM d, yyyy");
    }
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task<List<NotificationDto>> GetByUserAsync(long userId, int take = 20)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto
            {
                Id = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                Url = n.Url,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(long userId)
    {
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task CreateAsync(long userId, long shopId, string title, string message, string type, string? url = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            ShopId = shopId,
            Title = title,
            Message = message,
            Type = type,
            Url = url,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task NotifySuperAdminsAsync(long shopId, string title, string message, string type, string? url = null)
    {
        var superAdminUserIds = await _db.UserRoles
            .Where(ur => ur.Role!.RoleName == "SuperAdmin")
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in superAdminUserIds)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                ShopId = shopId,
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (superAdminUserIds.Count > 0)
            await _db.SaveChangesAsync();
    }

    public async Task MarkAsReadAsync(long notificationId, long userId)
    {
        var notif = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
        if (notif != null)
        {
            notif.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkAllReadAsync(long userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
