using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Filters;

/// <summary>
/// Global filter that catches DbUpdateException (unique constraint violations, FK conflicts, etc.)
/// and returns user-friendly error messages instead of raw stack traces.
/// </summary>
public class DbExceptionFilter : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateException dbEx)
            return Task.CompletedTask;

        var message = GetUserFriendlyMessage(dbEx);

        var endpoint = context.HttpContext.GetEndpoint();
        var isApi = endpoint?.Metadata.GetMetadata<ApiControllerAttribute>() != null;
        var requestedWith = context.HttpContext.Items.TryGetValue("RequestedWith", out var value)
            ? value as string
            : null;
        var isAjax = string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        var expectsJson = isApi || isAjax;

        if (expectsJson)
        {
            context.Result = new JsonResult(new { success = false, message })
            {
                StatusCode = 409 // Conflict
            };
        }
        else
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var area = context.RouteData.Values["area"]?.ToString();

            // Set TempData error via factory since ExceptionContext doesn't expose Controller directly
            var tempDataFactory = context.HttpContext.RequestServices.GetService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
            if (tempDataFactory != null)
            {
                var tempData = tempDataFactory.GetTempData(context.HttpContext);
                tempData["Error"] = message;
            }

            context.Result = new RedirectToActionResult("Index", controller, area != null ? new { area } : null);
        }

        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }

    private static string GetUserFriendlyMessage(DbUpdateException dbEx)
    {
        if (dbEx.InnerException is SqlException sqlEx)
        {
            // Error 2601 / 2627 = unique index / unique constraint violation
            if (sqlEx.Number is 2601 or 2627)
            {
                var detail = ParseDuplicateKeyMessage(sqlEx.Message);
                return detail ?? "A record with the same value already exists. Please use a different value.";
            }

            // Error 547 = FK constraint violation (e.g. deleting a record that's referenced)
            if (sqlEx.Number == 547)
            {
                return "This record cannot be modified because it is referenced by other data.";
            }
        }

        // DbUpdateConcurrencyException is a subclass of DbUpdateException
        if (dbEx is DbUpdateConcurrencyException)
        {
            return "This record was modified by another user. Please refresh and try again.";
        }

        return "A database error occurred while saving. Please try again.";
    }

    private static string? ParseDuplicateKeyMessage(string sqlMessage)
    {
        // SQL Server format: "Cannot insert duplicate key row in object 'dbo.TABLE' with unique index 'IX_...'. The duplicate key value is (val1, val2)."
        // Map known index names to human-readable messages
        var indexMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["IX_INVENTORY_ITEMS_ShopID_SKU"] = "An inventory item with this SKU already exists.",
            ["IX_USERS_ShopID_UserName"] = "A user with this username already exists.",
            ["IX_SERVICE_CATALOG_ShopID_ServiceName"] = "A service with this name already exists.",
            ["IX_SERVICE_CATEGORY_ShopID_CategoryName"] = "A service category with this name already exists.",
            ["IX_INVENTORY_CATEGORY_ShopID_CategoryName"] = "An inventory category with this name already exists.",
            ["IX_SHOP_ShopCode"] = "A shop with this code already exists.",
            ["IX_INVOICES_ShopID_InvoiceNo"] = "An invoice with this number already exists.",
            ["IX_JOB_ORDERS_ShopID_JobOrderNo"] = "A job order with this number already exists.",
            ["IX_INVOICES_JobOrderID"] = "An invoice already exists for this job order.",
            ["IX_PAYMENT_ALLOCATION_PaymentID_InvoiceID"] = "This payment is already allocated to the invoice.",
            ["IX_PAYMONGO_TXN_PaymentID"] = "A PayMongo transaction already exists for this payment.",
            ["IX_ROLES_RoleName"] = "A role with this name already exists.",
            ["IX_USER_ROLES_UserID_RoleID"] = "This user already has this role assigned.",
        };

        foreach (var mapping in indexMappings)
        {
            if (sqlMessage.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                return mapping.Value;
        }

        return null;
    }
}
