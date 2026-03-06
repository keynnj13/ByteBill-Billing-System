using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class ReportsController : Controller
{
    private readonly ISuperAdminService _service;

    public ReportsController(ISuperAdminService service)
    {
        _service = service;
    }

    private bool IsAuthorized()
    {
        var role = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return role == "SuperAdmin";
    }

    public async Task<IActionResult> Index(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        var vm = await _service.GenerateReportAsync(report, dateFrom, dateTo);
        return View(vm);
    }

    public async Task<IActionResult> ExportCsv(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return Forbid();

        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        var csv = await _service.ExportReportCsvAsync(report, dateFrom, dateTo);
        return File(csv, "text/csv", $"ByteBill_{report}_report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> ExportPdf(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return Forbid();

        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        var vm = await _service.GenerateReportAsync(report, dateFrom, dateTo);

        var reportNames = new Dictionary<string, string>
        {
            { "revenue", "Revenue Report" }, { "shops", "Shops Activity" },
            { "users", "User Activity" }, { "subscriptions", "Subscription Overview" },
            { "payments", "Payment History" }, { "growth", "Growth Analytics" }
        };
        var reportTitle = reportNames.GetValueOrDefault(report, "Report");

        var tableHeaders = report switch
        {
            "revenue" => new[] { "Reference", "Shop", "Plan", "Amount", "Paid Date" },
            "shops" => new[] { "Shop", "Status", "Users", "Created" },
            "users" => new[] { "Name", "Role", "Shop", "Status", "Created" },
            "subscriptions" => new[] { "Shop", "Plan", "Cycle", "Price", "Status", "Start Date" },
            "payments" => new[] { "Reference", "Shop", "Amount", "Status", "Method", "Date" },
            "growth" => new[] { "Month", "Cumulative Shops", "Change" },
            _ => new[] { "Data" }
        };

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text($"ByteBill — {reportTitle}").Bold().FontSize(18)
                        .FontColor(Colors.Blue.Medium);
                    col.Item().Text($"{dateFrom:MMM dd, yyyy} — {dateTo:MMM dd, yyyy}")
                        .FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    // Summary cards row
                    if (vm.SummaryCards.Any())
                    {
                        col.Item().Row(row =>
                        {
                            foreach (var card in vm.SummaryCards)
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8).Column(c =>
                                    {
                                        c.Item().Text(card.Label.ToUpper()).FontSize(8)
                                            .FontColor(Colors.Grey.Medium);
                                        c.Item().Text(card.Value).Bold().FontSize(14);
                                    });
                            }
                        });
                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    }

                    // Data table
                    if (vm.TableRows.Any())
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                for (int i = 0; i < tableHeaders.Length; i++)
                                    cols.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                foreach (var h in tableHeaders)
                                {
                                    header.Cell().Background(Colors.Grey.Lighten3)
                                        .Padding(6).Text(h).Bold().FontSize(9);
                                }
                            });

                            foreach (var row in vm.TableRows)
                            {
                                foreach (var cell in row.Cells)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                        .Padding(6).Text(cell ?? "").FontSize(9);
                                }
                            }
                        });

                        col.Item().PaddingTop(8).AlignRight()
                            .Text($"Total rows: {vm.TableRows.Count}").FontSize(9)
                            .FontColor(Colors.Grey.Medium);
                    }
                    else
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("No data for the selected period.")
                            .FontColor(Colors.Grey.Medium);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return File(bytes, "application/pdf", $"ByteBill_{report}_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }
}
