using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ByteBill_BS.TagHelpers;

[HtmlTargetElement("script")]
public class CspNonceTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CspNonceTagHelper(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("nonce"))
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("CspNonce", out var nonceObj) != true)
        {
            return;
        }

        if (nonceObj is string nonce && !string.IsNullOrWhiteSpace(nonce))
        {
            output.Attributes.Add("nonce", nonce);
        }
    }
}
