namespace ByteBill_BS.Models.Enums;

public enum JobOrderStatus
{
    Created,
    Pending,
    CheckedIn,
    Diagnosis,
    Diagnosed,
    AwaitingApproval,
    Approved,
    InProgress,
    WaitingForParts,
    OnHold,
    Completed,
    ReadyForPickup,
    Delivered,
    Cancelled
}
