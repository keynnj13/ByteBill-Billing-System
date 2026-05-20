using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ByteBill_BS.Extensions;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class FromCookieAttribute : ModelBinderAttribute
{
    public FromCookieAttribute() : base(typeof(CookieModelBinder))
    {
        BindingSource = BindingSource.Custom;
    }
}

public sealed class CookieModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var cookieName = bindingContext.BinderModelName ?? bindingContext.ModelName;
        if (string.IsNullOrWhiteSpace(cookieName))
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        var value = bindingContext.HttpContext.Request.Cookies[cookieName];
        bindingContext.Result = string.IsNullOrWhiteSpace(value)
            ? ModelBindingResult.Success(null)
            : ModelBindingResult.Success(value);

        return Task.CompletedTask;
    }
}
