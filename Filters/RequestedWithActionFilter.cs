using Microsoft.AspNetCore.Mvc.Filters;

namespace ByteBill_BS.Filters;

/// <summary>
/// Captures the model-bound X-Requested-With header (if present) for downstream filters.
/// </summary>
public sealed class RequestedWithActionFilter : IActionFilter
{
    private const string RequestedWithKey = "RequestedWith";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("requestedWith", out var value)
            && value is string requestedWith
            && !string.IsNullOrWhiteSpace(requestedWith))
        {
            context.HttpContext.Items[RequestedWithKey] = requestedWith;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
