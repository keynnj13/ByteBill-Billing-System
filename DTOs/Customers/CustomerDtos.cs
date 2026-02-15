using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.DTOs.Customers;

// ── List item (paginated table) ─────────────────────────────────────────
public class CustomerListItemDto
{
    public long CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Orders { get; set; }       // count of JobOrders
    public decimal TotalSpent { get; set; } // sum of PaymentAllocation.AmountApplied
}

// ── Detail DTO ──────────────────────────────────────────────────────────
public class CustomerDetailDto : CustomerListItemDto
{
    public List<CustomerDeviceDto> Devices { get; set; } = new();
}

public class CustomerDeviceDto
{
    public long DeviceId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public string? Notes { get; set; }
}

// ── Create request ──────────────────────────────────────────────────────
public class CreateCustomerRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MiddleName { get; set; }

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; } // Format: +63 XXX XXX XXXX (mobile) or +63 XX XXX XXXX (landline)

    [MaxLength(255)]
    public string? Address { get; set; }
}

// ── Update request ──────────────────────────────────────────────────────
public class UpdateCustomerRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MiddleName { get; set; }

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; } // Format: +63 XXX XXX XXXX (mobile) or +63 XX XXX XXXX (landline)

    [MaxLength(255)]
    public string? Address { get; set; }
}
