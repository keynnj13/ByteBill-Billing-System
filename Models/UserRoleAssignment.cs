namespace ByteBill_BS.Models;

public class UserRoleAssignment
{
    public long UserRoleId { get; set; }
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public Role? Role { get; set; }
}
