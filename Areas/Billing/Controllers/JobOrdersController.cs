using ByteBill_BS.Data;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class JobOrdersController : Controller
{
    private readonly IJobOrderService _jobOrderService;

    public JobOrdersController(IJobOrderService jobOrderService)
    {
        _jobOrderService = jobOrderService;
    }

    private bool IsAuthorized() => User.IsInRoles("Billing", "Admin", "SuperAdmin");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, JobOrderStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _jobOrderService.GetListAsync(shopId, new JobOrderPagedRequest
        {
            Page = page,
            PageSize = 10,
            Search = search,
            StatusFilter = status?.ToString()
        });

        var viewModel = new JobOrderListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            PageSize = result.PageSize,
            JobOrders = result.Items.Select(j =>
            {
                var deviceParts = j.DeviceSummary?.Split(" - ", 2) ?? Array.Empty<string>();
                var deviceType = deviceParts.Length > 0 ? deviceParts[0] : "";
                _ = Enum.TryParse<JobOrderStatus>(j.Status, true, out var parsedStatus);

                return new JobOrderItemViewModel
                {
                    Id = j.JobOrderId,
                    JobNumber = j.JobOrderNo,
                    JobOrderNumber = j.JobOrderNo,
                    OrderNumber = j.JobOrderNo,
                    CustomerName = j.CustomerName,
                    CustomerInitials = GetInitials(j.CustomerName),
                    DeviceType = deviceType,
                    DeviceInfo = j.DeviceSummary ?? "",
                    Status = parsedStatus,
                    TechnicianName = j.TechnicianName,
                    AssignedTechnicianName = j.TechnicianName,
                    CreatedAt = j.CreatedAt
                };
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var vm = await GetJobOrderDetailAsync(id);
        if (vm == null) return NotFound();
        return PartialView("_DetailsModal", vm);
    }

    private async Task<JobOrderDetailViewModel?> GetJobOrderDetailAsync(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _jobOrderService.GetDetailAsync(shopId, id);
        if (dto == null) return null;

        _ = Enum.TryParse<JobOrderStatus>(dto.Status, true, out var parsedStatus);
        var serviceCost = dto.Services.Sum(s => s.LineTotal);
        var partsCost = dto.Parts.Sum(p => p.LineTotal);

        return new JobOrderDetailViewModel
        {
            Id = dto.JobOrderId,
            JobNumber = dto.JobOrderNo,
            JobOrderNumber = dto.JobOrderNo,
            OrderNumber = dto.JobOrderNo,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerInitials = GetInitials(dto.CustomerName),
            CustomerEmail = dto.CustomerEmail,
            CustomerPhone = dto.CustomerPhone ?? "",
            DeviceId = dto.DeviceId,
            DeviceType = dto.DeviceType,
            Brand = dto.Brand,
            DeviceBrand = dto.Brand,
            DeviceModel = dto.Model,
            SerialNumber = dto.SerialNo,
            DeviceSerial = dto.SerialNo,
            DeviceInfo = $"{dto.DeviceType} - {dto.Brand} {dto.Model}",
            Status = parsedStatus,
            TechnicianId = dto.AssignedTechUserId,
            AssignedTechnicianId = dto.AssignedTechUserId,
            TechnicianName = dto.TechnicianName,
            AssignedTechnicianName = dto.TechnicianName,
            AssignedTechnician = dto.TechnicianName,
            CreatedBy = dto.CreatedByName,
            ProblemDescription = dto.ProblemReported,
            ProblemReported = dto.ProblemReported,
            IssueDescription = !string.IsNullOrWhiteSpace(dto.ProblemReported) ? dto.ProblemReported : dto.DiagnosisNotes ?? "",
            DiagnosisNotes = dto.DiagnosisNotes,
            TechnicianNotes = dto.DiagnosisNotes,
            Priority = dto.Priority,
            EstimatedCompletionDate = dto.EstimatedCompletionDate,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            InvoiceId = dto.InvoiceId,
            TotalServiceCost = serviceCost,
            TotalPartsCost = partsCost,
            Subtotal = serviceCost + partsCost,
            Total = serviceCost + partsCost,
            EstimatedCost = serviceCost + partsCost,
            Services = dto.Services.Select(s => new JobOrderServiceItemViewModel
            {
                Id = s.JobOrderServiceId,
                ServiceName = s.ServiceName,
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal
            }).ToList(),
            Parts = dto.Parts.Select(p => new JobOrderPartItemViewModel
            {
                Id = p.JobOrderPartId,
                PartName = p.ItemName,
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                Total = p.LineTotal
            }).ToList(),
            Items = dto.Services.Select(s => new JobOrderItemLineViewModel
            {
                Id = s.JobOrderServiceId,
                Description = s.ServiceName,
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal,
                IsService = true
            })
            .Concat(dto.Parts.Select(p => new JobOrderItemLineViewModel
            {
                Id = p.JobOrderPartId,
                Description = p.ItemName,
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                Total = p.LineTotal,
                IsService = false
            })).ToList(),
            LineItems = dto.Services.Select(s => new JobOrderDetailViewModel.LineItem
            {
                Description = s.ServiceName,
                Type = "Service",
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal
            })
            .Concat(dto.Parts.Select(p => new JobOrderDetailViewModel.LineItem
            {
                Description = p.ItemName,
                Type = "Part",
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                Total = p.LineTotal
            })).ToList(),
            Timeline = dto.Timeline.Select(t => new TimelineEventViewModel
            {
                Title = $"Status → {t.NewStatus}",
                Description = $"By {t.ChangedByName}" + (string.IsNullOrEmpty(t.Remarks) ? "" : $" — {t.Remarks}"),
                Timestamp = t.ChangedAt,
                Status = t.NewStatus,
                CompletedBy = t.ChangedByName,
                IsCompleted = true,
                Icon = t.NewStatus switch
                {
                    "Pending" => "plus",
                    "CheckedIn" => "log-in",
                    "Diagnosis" => "search",
                    "InProgress" => "wrench",
                    "WaitingForParts" => "pause",
                    "Completed" => "check-circle",
                    "Cancelled" => "x-circle",
                    _ => "activity"
                }
            }).ToList()
        };
    }
}
