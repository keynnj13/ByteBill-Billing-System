using ByteBill_BS.Data;
using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ByteBill_BS.Services;

// ── Configuration ────────────────────────────────────────────────────────
public class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@bytebill.ph";
    public string FromName { get; set; } = "ByteBill";
}

// ── Interface ────────────────────────────────────────────────────────────
public interface IEmailService
{
    /// <summary>Send invoice notification to customer when invoice is created.</summary>
    Task SendInvoiceAsync(long invoiceId);

    /// <summary>Send payment receipt to customer after payment is confirmed.</summary>
    Task SendReceiptAsync(long paymentId);

    /// <summary>Send subscription confirmation to shop owner after successful payment.</summary>
    Task SendSubscriptionConfirmationAsync(long subscriptionPaymentId);
}

// ── Implementation ───────────────────────────────────────────────────────
public class EmailService : IEmailService
{
    private readonly ApplicationDbContext _db;
    private readonly SendGridSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ApplicationDbContext db,
        IOptions<SendGridSettings> settings,
        ILogger<EmailService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendInvoiceAsync(long invoiceId)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Shop)
            .Include(i => i.InvoiceLines)
            .Include(i => i.InvoiceDiscounts)
            .Include(i => i.JobOrder)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null)
        {
            _logger.LogWarning("SendInvoiceAsync: Invoice {Id} not found", invoiceId);
            return;
        }

        var customerEmail = invoice.Customer?.Email;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            _logger.LogInformation("SendInvoiceAsync: Customer has no email for Invoice {InvoiceNo}", invoice.InvoiceNo);
            return;
        }

        var shopName = invoice.Shop?.ShopName ?? "Shop";
        var customerName = invoice.Customer!.FullName;

        // Generate PDF attachment
        var pdfBytes = GenerateInvoicePdf(invoice);

        var subject = $"Invoice {invoice.InvoiceNo} from {shopName}";
        var htmlBody = BuildInvoiceEmailHtml(invoice, shopName, customerName);

        await SendEmailAsync(
            toEmail: customerEmail,
            toName: customerName,
            subject: subject,
            htmlContent: htmlBody,
            shopName: shopName,
            attachmentBytes: pdfBytes,
            attachmentFilename: $"{invoice.InvoiceNo}.pdf");
    }

    public async Task SendReceiptAsync(long paymentId)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.Shop)
            .Include(p => p.ReceivedByUser)
            .Include(p => p.PaymentAllocations)
                .ThenInclude(pa => pa.Invoice)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment is null)
        {
            _logger.LogWarning("SendReceiptAsync: Payment {Id} not found", paymentId);
            return;
        }

        var customerEmail = payment.Customer?.Email;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            _logger.LogInformation("SendReceiptAsync: Customer has no email for Payment {PaymentNo}", payment.PaymentNo);
            return;
        }

        var shopName = payment.Shop?.ShopName ?? "Shop";
        var customerName = payment.Customer!.FullName;

        // Generate PDF receipt
        var pdfBytes = GenerateReceiptPdf(payment);

        var subject = $"Payment Receipt {payment.PaymentNo} from {shopName}";
        var htmlBody = BuildReceiptEmailHtml(payment, shopName, customerName);

        await SendEmailAsync(
            toEmail: customerEmail,
            toName: customerName,
            subject: subject,
            htmlContent: htmlBody,
            shopName: shopName,
            attachmentBytes: pdfBytes,
            attachmentFilename: $"Receipt-{payment.PaymentNo}.pdf");
    }

    public async Task SendSubscriptionConfirmationAsync(long subscriptionPaymentId)
    {
        var subPay = await _db.SubscriptionPayments
            .AsNoTracking()
            .Include(sp => sp.Subscription)
                .ThenInclude(s => s!.Plan)
            .Include(sp => sp.Shop)
            .FirstOrDefaultAsync(sp => sp.SubscriptionPaymentId == subscriptionPaymentId);

        if (subPay is null)
        {
            _logger.LogWarning("SendSubscriptionConfirmationAsync: SubscriptionPayment {Id} not found", subscriptionPaymentId);
            return;
        }

        // Find shop owner (Admin role) email
        var owner = await _db.Users
            .AsNoTracking()
            .Where(u => u.ShopId == subPay.ShopId && u.IsActive
                && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
            .OrderBy(u => u.UserId)
            .FirstOrDefaultAsync();

        if (owner is null || string.IsNullOrWhiteSpace(owner.Email))
        {
            _logger.LogInformation("SendSubscriptionConfirmationAsync: No admin email found for Shop {ShopId}", subPay.ShopId);
            return;
        }

        var shopName = subPay.Shop?.ShopName ?? "Shop";
        var planName = subPay.Subscription?.Plan?.PlanName ?? "Plan";
        var billingCycle = subPay.Subscription?.BillingCycle ?? "Monthly";

        var subject = $"ByteBill Subscription Confirmed — {planName} ({billingCycle})";
        var htmlBody = BuildSubscriptionEmailHtml(subPay, shopName, planName, billingCycle, owner.FullName);

        await SendEmailAsync(
            toEmail: owner.Email,
            toName: owner.FullName,
            subject: subject,
            htmlContent: htmlBody,
            shopName: "ByteBill");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  SEND EMAIL VIA SENDGRID
    // ═════════════════════════════════════════════════════════════════════
    private async Task SendEmailAsync(string toEmail, string toName, string subject,
        string htmlContent, string shopName,
        byte[]? attachmentBytes = null, string? attachmentFilename = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("SendGrid API key not configured — email not sent: {Subject}", subject);
            return;
        }

        var client = new SendGridClient(_settings.ApiKey);
        var fromName = $"{shopName} via ByteBill";
        var from = new EmailAddress(_settings.FromEmail, fromName);
        var to = new EmailAddress(toEmail, toName);

        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);

        if (attachmentBytes is not null && !string.IsNullOrEmpty(attachmentFilename))
        {
            msg.AddAttachment(attachmentFilename, Convert.ToBase64String(attachmentBytes), "application/pdf");
        }

        try
        {
            var response = await client.SendEmailAsync(msg);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogWarning("SendGrid returned {StatusCode} for {Email}: {Body}",
                    response.StatusCode, toEmail, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  HTML EMAIL TEMPLATES
    // ═════════════════════════════════════════════════════════════════════

    private static string BuildInvoiceEmailHtml(Invoice invoice, string shopName, string customerName)
    {
        var lines = invoice.InvoiceLines.ToList();
        var linesHtml = string.Join("", lines.Select(l =>
            $@"<tr>
                <td style=""padding:10px 12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333;"">{Encode(l.Description)}</td>
                <td style=""padding:10px 12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333; text-align:center;"">{l.Qty}</td>
                <td style=""padding:10px 12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333; text-align:right;"">₱{l.UnitPrice:N2}</td>
                <td style=""padding:10px 12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#555; text-align:right; font-weight:600;"">₱{l.LineTotal:N2}</td>
            </tr>"));

        var discountRow = invoice.DiscountAmount > 0
            ? $@"<tr><td colspan=""3"" style=""padding:6px 12px; text-align:right; font-size:13px; color:#dc2626;"">Discount</td>
                 <td style=""padding:6px 12px; text-align:right; font-size:13px; color:#dc2626;"">-₱{invoice.DiscountAmount:N2}</td></tr>"
            : "";

        var vatRow = invoice.VatAmount > 0
            ? $@"<tr><td colspan=""3"" style=""padding:6px 12px; text-align:right; font-size:13px; color:#666;"">VAT (12%)</td>
                 <td style=""padding:6px 12px; text-align:right; font-size:13px; color:#333;"">₱{invoice.VatAmount:N2}</td></tr>"
            : "";

        var dueDate = invoice.DueDate?.ToString("MMMM d, yyyy") ?? "Upon receipt";

        return WrapEmailLayout(shopName, $@"
            <div style=""text-align:center; padding:30px 0 10px;"">
                <div style=""display:inline-block; background:#eff6ff; border-radius:50%; width:64px; height:64px; line-height:64px; text-align:center;"">
                    <span style=""font-size:28px;"">📄</span>
                </div>
                <h1 style=""margin:16px 0 4px; font-size:22px; color:#1e293b; font-weight:700;"">Invoice {Encode(invoice.InvoiceNo)}</h1>
                <p style=""margin:0; font-size:14px; color:#64748b;"">Issued on {invoice.InvoiceDate:MMMM d, yyyy}</p>
            </div>

            <div style=""padding:20px 30px;"">
                <table width=""100%"" style=""margin-bottom:16px;"">
                    <tr>
                        <td style=""font-size:13px; color:#64748b;"">Bill To</td>
                        <td style=""font-size:13px; color:#64748b; text-align:right;"">Due Date</td>
                    </tr>
                    <tr>
                        <td style=""font-size:15px; color:#1e293b; font-weight:600;"">{Encode(customerName)}</td>
                        <td style=""font-size:15px; color:#1e293b; font-weight:600; text-align:right;"">{dueDate}</td>
                    </tr>
                </table>

                <table width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""border:1px solid #e2e8f0; border-radius:8px; overflow:hidden; margin-bottom:20px;"">
                    <thead>
                        <tr style=""background:#f8fafc;"">
                            <th style=""padding:10px 12px; text-align:left; font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;"">Description</th>
                            <th style=""padding:10px 12px; text-align:center; font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;"">Qty</th>
                            <th style=""padding:10px 12px; text-align:right; font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;"">Unit Price</th>
                            <th style=""padding:10px 12px; text-align:right; font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;"">Amount</th>
                        </tr>
                    </thead>
                    <tbody>
                        {linesHtml}
                    </tbody>
                    <tfoot>
                        <tr><td colspan=""3"" style=""padding:8px 12px; text-align:right; font-size:13px; color:#666;"">Subtotal</td>
                            <td style=""padding:8px 12px; text-align:right; font-size:13px; color:#333;"">₱{invoice.Subtotal:N2}</td></tr>
                        {discountRow}
                        {vatRow}
                        <tr style=""border-top:2px solid #e2e8f0;"">
                            <td colspan=""3"" style=""padding:12px; text-align:right; font-size:16px; font-weight:700; color:#1e293b;"">Total Due</td>
                            <td style=""padding:12px; text-align:right; font-size:16px; font-weight:700; color:#1e293b;"">₱{invoice.TotalAmount:N2}</td>
                        </tr>
                    </tfoot>
                </table>

                <div style=""background:#fffbeb; border:1px solid #fde68a; border-radius:8px; padding:16px; text-align:center; margin-bottom:20px;"">
                    <p style=""margin:0; font-size:14px; color:#92400e;"">⏰ Payment is due by <strong>{dueDate}</strong></p>
                </div>
            </div>");
    }

    private static string BuildReceiptEmailHtml(Payment payment, string shopName, string customerName)
    {
        var allocations = payment.PaymentAllocations.ToList();
        var allocationRows = string.Join("", allocations.Select(a =>
            $@"<tr>
                <td style=""padding:10px 12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333;"">{Encode(a.Invoice?.InvoiceNo ?? "—")}</td>
                <td style=""padding:10px 12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333; text-align:right; font-weight:600;"">₱{a.AmountApplied:N2}</td>
            </tr>"));

        var receivedBy = payment.ReceivedByUser is not null
            ? $"{payment.ReceivedByUser.FirstName} {payment.ReceivedByUser.LastName}"
            : "System";

        return WrapEmailLayout(shopName, $@"
            <div style=""text-align:center; padding:30px 0 10px;"">
                <div style=""display:inline-block; background:#ecfdf5; border-radius:50%; width:64px; height:64px; line-height:64px; text-align:center;"">
                    <span style=""font-size:28px;"">✅</span>
                </div>
                <h1 style=""margin:16px 0 4px; font-size:22px; color:#1e293b; font-weight:700;"">Payment Received</h1>
                <p style=""margin:0; font-size:14px; color:#64748b;"">Receipt {Encode(payment.PaymentNo)}</p>
            </div>

            <div style=""padding:20px 30px;"">
                <div style=""background:#ecfdf5; border:1px solid #a7f3d0; border-radius:8px; padding:20px; text-align:center; margin-bottom:24px;"">
                    <p style=""margin:0 0 4px; font-size:13px; color:#065f46;"">Amount Paid</p>
                    <p style=""margin:0; font-size:28px; font-weight:700; color:#047857;"">₱{payment.Amount:N2}</p>
                </div>

                <table width=""100%"" style=""margin-bottom:20px;"">
                    <tr>
                        <td style=""padding:8px 0; font-size:13px; color:#64748b; width:40%;"">Receipt No.</td>
                        <td style=""padding:8px 0; font-size:14px; color:#1e293b; font-weight:500;"">{Encode(payment.PaymentNo)}</td>
                    </tr>
                    <tr>
                        <td style=""padding:8px 0; font-size:13px; color:#64748b;"">Customer</td>
                        <td style=""padding:8px 0; font-size:14px; color:#1e293b; font-weight:500;"">{Encode(customerName)}</td>
                    </tr>
                    <tr>
                        <td style=""padding:8px 0; font-size:13px; color:#64748b;"">Payment Date</td>
                        <td style=""padding:8px 0; font-size:14px; color:#1e293b; font-weight:500;"">{payment.PaymentDate:MMMM d, yyyy h:mm tt}</td>
                    </tr>
                    <tr>
                        <td style=""padding:8px 0; font-size:13px; color:#64748b;"">Method</td>
                        <td style=""padding:8px 0; font-size:14px; color:#1e293b; font-weight:500;"">{payment.Method}</td>
                    </tr>
                    {(string.IsNullOrEmpty(payment.ReferenceNo) ? "" : $@"<tr>
                        <td style=""padding:8px 0; font-size:13px; color:#64748b;"">Reference</td>
                        <td style=""padding:8px 0; font-size:14px; color:#1e293b; font-weight:500;"">{Encode(payment.ReferenceNo)}</td>
                    </tr>")}
                    <tr>
                        <td style=""padding:8px 0; font-size:13px; color:#64748b;"">Received By</td>
                        <td style=""padding:8px 0; font-size:14px; color:#1e293b; font-weight:500;"">{Encode(receivedBy)}</td>
                    </tr>
                </table>

                {(allocations.Any() ? $@"
                <h3 style=""font-size:14px; color:#1e293b; font-weight:600; margin:0 0 10px;"">Applied to Invoices</h3>
                <table width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""border:1px solid #e2e8f0; border-radius:8px; overflow:hidden; margin-bottom:20px;"">
                    <thead>
                        <tr style=""background:#f8fafc;"">
                            <th style=""padding:10px 12px; text-align:left; font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;"">Invoice</th>
                            <th style=""padding:10px 12px; text-align:right; font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:0.5px;"">Amount</th>
                        </tr>
                    </thead>
                    <tbody>{allocationRows}</tbody>
                </table>" : "")}

                <div style=""background:#f0fdf4; border-radius:8px; padding:14px; text-align:center;"">
                    <p style=""margin:0; font-size:13px; color:#166534;"">This is an official receipt. Thank you for your payment!</p>
                </div>
            </div>");
    }

    private static string BuildSubscriptionEmailHtml(SubscriptionPayment subPay, string shopName,
        string planName, string billingCycle, string ownerName)
    {
        var periodEnd = subPay.PeriodEnd == subPay.PeriodStart.AddYears(99)
            ? "Lifetime" : subPay.PeriodEnd.ToString("MMMM d, yyyy");

        return WrapEmailLayout("ByteBill", $@"
            <div style=""text-align:center; padding:30px 0 10px;"">
                <div style=""display:inline-block; background:#eff6ff; border-radius:50%; width:64px; height:64px; line-height:64px; text-align:center;"">
                    <span style=""font-size:28px;"">🎉</span>
                </div>
                <h1 style=""margin:16px 0 4px; font-size:22px; color:#1e293b; font-weight:700;"">Welcome to ByteBill!</h1>
                <p style=""margin:0; font-size:14px; color:#64748b;"">Your subscription is now active</p>
            </div>

            <div style=""padding:20px 30px;"">
                <p style=""font-size:15px; color:#334155; line-height:1.6;"">
                    Hi {Encode(ownerName)},<br><br>
                    Your <strong>{Encode(shopName)}</strong> shop has been successfully registered
                    on ByteBill with the <strong>{Encode(planName)}</strong> plan.
                </p>

                <div style=""background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:20px; margin:20px 0;"">
                    <h3 style=""margin:0 0 14px; font-size:14px; color:#1e293b; font-weight:600;"">Subscription Details</h3>
                    <table width=""100%"">
                        <tr>
                            <td style=""padding:6px 0; font-size:13px; color:#64748b; width:40%;"">Plan</td>
                            <td style=""padding:6px 0; font-size:14px; color:#1e293b; font-weight:600;"">{Encode(planName)}</td>
                        </tr>
                        <tr>
                            <td style=""padding:6px 0; font-size:13px; color:#64748b;"">Billing Cycle</td>
                            <td style=""padding:6px 0; font-size:14px; color:#1e293b; font-weight:500;"">{Encode(billingCycle)}</td>
                        </tr>
                        <tr>
                            <td style=""padding:6px 0; font-size:13px; color:#64748b;"">Amount Paid</td>
                            <td style=""padding:6px 0; font-size:14px; color:#047857; font-weight:700;"">₱{subPay.Amount:N2}</td>
                        </tr>
                        <tr>
                            <td style=""padding:6px 0; font-size:13px; color:#64748b;"">Reference</td>
                            <td style=""padding:6px 0; font-size:14px; color:#1e293b; font-weight:500;"">{Encode(subPay.ReferenceNumber)}</td>
                        </tr>
                        <tr>
                            <td style=""padding:6px 0; font-size:13px; color:#64748b;"">Period</td>
                            <td style=""padding:6px 0; font-size:14px; color:#1e293b; font-weight:500;"">{subPay.PeriodStart:MMM d, yyyy} — {periodEnd}</td>
                        </tr>
                    </table>
                </div>

                <p style=""font-size:14px; color:#334155; line-height:1.6;"">
                    You can now log in and start managing your shop's billing, invoices, and payments.
                </p>

                <div style=""text-align:center; margin:24px 0 10px;"">
                    <p style=""font-size:13px; color:#94a3b8;"">Thank you for choosing ByteBill! 🚀</p>
                </div>
            </div>");
    }

    /// <summary>Shared email layout wrapper with shop branding header and footer.</summary>
    private static string WrapEmailLayout(string shopName, string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0; padding:0; background:#f1f5f9; font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;"">
    <table width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:#f1f5f9; padding:30px 0;"">
        <tr><td align=""center"">
            <table width=""600"" cellspacing=""0"" cellpadding=""0"" style=""background:#ffffff; border-radius:12px; box-shadow:0 2px 8px rgba(0,0,0,0.06); overflow:hidden;"">
                <!-- Header -->
                <tr>
                    <td style=""background:linear-gradient(135deg,#0f172a,#1e293b); padding:24px 30px; text-align:center;"">
                        <h2 style=""margin:0; color:#ffffff; font-size:18px; font-weight:700; letter-spacing:0.5px;"">{Encode(shopName)}</h2>
                        <p style=""margin:4px 0 0; color:#94a3b8; font-size:11px; text-transform:uppercase; letter-spacing:1px;"">via ByteBill</p>
                    </td>
                </tr>
                <!-- Body -->
                <tr><td>{bodyContent}</td></tr>
                <!-- Footer -->
                <tr>
                    <td style=""background:#f8fafc; border-top:1px solid #e2e8f0; padding:20px 30px; text-align:center;"">
                        <p style=""margin:0 0 6px; font-size:12px; color:#94a3b8;"">Powered by <strong style=""color:#64748b;"">ByteBill</strong> — Smart Billing for Service Shops</p>
                        <p style=""margin:0; font-size:11px; color:#cbd5e1;"">This is an automated email. Please do not reply directly.</p>
                    </td>
                </tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
    }

    private static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? "");

    // ═════════════════════════════════════════════════════════════════════
    //  PDF GENERATION (QuestPDF)
    // ═════════════════════════════════════════════════════════════════════

    private static byte[] GenerateInvoicePdf(Invoice invoice)
    {
        var shopName = invoice.Shop?.ShopName ?? "Shop";
        var shopAddress = invoice.Shop?.Address ?? "";
        var shopPhone = invoice.Shop?.Phone ?? "";
        var shopEmail = invoice.Shop?.Email ?? "";
        var shopTin = invoice.Shop?.TIN;
        var customerName = invoice.Customer?.FullName ?? "";
        var customerEmail = invoice.Customer?.Email ?? "";
        var customerPhone = invoice.Customer?.Phone ?? "";
        var customerAddress = invoice.Customer?.Address ?? "";
        var lines = invoice.InvoiceLines.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(shopName).FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                            if (!string.IsNullOrEmpty(shopAddress))
                                left.Item().Text(shopAddress).FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrEmpty(shopPhone))
                                left.Item().Text($"Tel: {shopPhone}").FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrEmpty(shopEmail))
                                left.Item().Text(shopEmail).FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrEmpty(shopTin))
                                left.Item().Text($"TIN: {shopTin}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().Text("INVOICE").FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                            right.Item().Text($"No: {invoice.InvoiceNo}").FontSize(11).SemiBold();
                            right.Item().Text($"Date: {invoice.InvoiceDate:MMMM d, yyyy}").FontSize(9);
                            right.Item().Text($"Due: {invoice.DueDate?.ToString("MMMM d, yyyy") ?? "Upon receipt"}").FontSize(9);
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("BILL TO").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                            c.Item().Text(customerName).FontSize(11).SemiBold();
                            if (!string.IsNullOrEmpty(customerAddress)) c.Item().Text(customerAddress).FontSize(9);
                            if (!string.IsNullOrEmpty(customerPhone)) c.Item().Text(customerPhone).FontSize(9);
                            if (!string.IsNullOrEmpty(customerEmail)) c.Item().Text(customerEmail).FontSize(9);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("JOB ORDER").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                            c.Item().Text(invoice.JobOrder?.JobOrderNo ?? "—").FontSize(11).SemiBold();
                        });
                    });

                    col.Item().PaddingTop(10);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4);
                            cols.ConstantColumn(50);
                            cols.ConstantColumn(90);
                            cols.ConstantColumn(100);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(8)
                                .Text("Description").FontSize(9).Bold().FontColor(Colors.White);
                            header.Cell().Background(Colors.Blue.Darken2).Padding(8)
                                .Text("Qty").FontSize(9).Bold().FontColor(Colors.White).AlignCenter();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(8)
                                .Text("Unit Price").FontSize(9).Bold().FontColor(Colors.White).AlignRight();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(8)
                                .Text("Amount").FontSize(9).Bold().FontColor(Colors.White).AlignRight();
                        });

                        foreach (var line in lines)
                        {
                            var bg = lines.IndexOf(line) % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                            table.Cell().Background(bg).Padding(8).Text(line.Description).FontSize(9);
                            table.Cell().Background(bg).Padding(8).Text(line.Qty.ToString()).FontSize(9).AlignCenter();
                            table.Cell().Background(bg).Padding(8).Text($"₱{line.UnitPrice:N2}").FontSize(9).AlignRight();
                            table.Cell().Background(bg).Padding(8).Text($"₱{line.LineTotal:N2}").FontSize(9).SemiBold().AlignRight();
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Width(250).Column(totals =>
                    {
                        void TotalRow(string label, decimal amount, bool bold = false, string? color = null)
                        {
                            totals.Item().Row(r =>
                            {
                                var text = r.RelativeItem().AlignRight().Padding(4).Text(label).FontSize(10);
                                if (bold) text.Bold();
                                var val = r.ConstantItem(110).AlignRight().Padding(4).Text($"₱{amount:N2}").FontSize(10);
                                if (bold) val.Bold();
                            });
                        }

                        TotalRow("Subtotal", invoice.Subtotal);
                        if (invoice.DiscountAmount > 0) TotalRow("Discount", -invoice.DiscountAmount);
                        if (invoice.VatAmount > 0) TotalRow("VAT (12%)", invoice.VatAmount);
                        if (invoice.TotalAdjustments != 0) TotalRow("Adjustments", invoice.TotalAdjustments);
                        totals.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        TotalRow("Total Due", invoice.TotalAmount, bold: true);
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by ByteBill — ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span($"Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    private static byte[] GenerateReceiptPdf(Payment payment)
    {
        var shopName = payment.Shop?.ShopName ?? "Shop";
        var shopAddress = payment.Shop?.Address ?? "";
        var shopPhone = payment.Shop?.Phone ?? "";
        var shopEmail = payment.Shop?.Email ?? "";
        var shopTin = payment.Shop?.TIN;
        var customerName = payment.Customer?.FullName ?? "";
        var allocations = payment.PaymentAllocations.ToList();
        var receivedBy = payment.ReceivedByUser is not null
            ? $"{payment.ReceivedByUser.FirstName} {payment.ReceivedByUser.LastName}"
            : "System";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(shopName).FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                            if (!string.IsNullOrEmpty(shopAddress))
                                left.Item().Text(shopAddress).FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrEmpty(shopPhone))
                                left.Item().Text($"Tel: {shopPhone}").FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrEmpty(shopEmail))
                                left.Item().Text(shopEmail).FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrEmpty(shopTin))
                                left.Item().Text($"TIN: {shopTin}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().Text("PAYMENT RECEIPT").FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                            right.Item().Text($"No: {payment.PaymentNo}").FontSize(11).SemiBold();
                            right.Item().Text($"Date: {payment.PaymentDate:MMMM d, yyyy}").FontSize(9);
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Column(col =>
                {
                    void InfoRow(string label, string value)
                    {
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(130).Text(label).FontSize(10).FontColor(Colors.Grey.Medium);
                            r.RelativeItem().Text(value).FontSize(10).SemiBold();
                        });
                        col.Item().PaddingBottom(4);
                    }

                    InfoRow("Customer", customerName);
                    InfoRow("Payment Method", payment.Method.ToString());
                    if (!string.IsNullOrEmpty(payment.ReferenceNo))
                        InfoRow("Reference No.", payment.ReferenceNo);
                    InfoRow("Received By", receivedBy);
                    col.Item().PaddingVertical(6);

                    // Amount highlight
                    col.Item().Background(Colors.Green.Lighten4).Padding(16).AlignCenter().Column(box =>
                    {
                        box.Item().Text("Amount Paid").FontSize(11).FontColor(Colors.Green.Darken2);
                        box.Item().Text($"₱{payment.Amount:N2}").FontSize(22).Bold().FontColor(Colors.Green.Darken3);
                    });

                    col.Item().PaddingVertical(10);

                    // Allocation table
                    if (allocations.Any())
                    {
                        col.Item().Text("Applied to Invoices").FontSize(11).Bold();
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(2);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Invoice No.").FontSize(9).Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(8)
                                    .Text("Amount Applied").FontSize(9).Bold().AlignRight();
                            });

                            foreach (var a in allocations)
                            {
                                table.Cell().Padding(8).Text(a.Invoice?.InvoiceNo ?? "—").FontSize(9);
                                table.Cell().Padding(8).Text($"₱{a.AmountApplied:N2}").FontSize(9).AlignRight();
                            }
                        });
                    }

                    col.Item().PaddingTop(30).AlignCenter()
                        .Text("This is an official receipt. Thank you for your payment!")
                        .FontSize(10).Italic().FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by ByteBill — ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span($"Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }
}
