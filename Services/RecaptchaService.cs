using System.Text.Json;

namespace ByteBill_BS.Services;

public class RecaptchaSettings
{
    public bool Enabled { get; set; } = false;
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public double MinScore { get; set; } = 0.5;
}

public interface IRecaptchaService
{
    Task<bool> VerifyAsync(string? token, string action, string? remoteIp);
}

public class RecaptchaService : IRecaptchaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RecaptchaSettings _settings;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(
        IHttpClientFactory httpClientFactory,
        Microsoft.Extensions.Options.IOptions<RecaptchaSettings> settings,
        ILogger<RecaptchaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, string action, string? remoteIp)
    {
        if (!_settings.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            return false;
        }

        var values = new Dictionary<string, string>
        {
            ["secret"] = _settings.SecretKey,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            values["remoteip"] = remoteIp;
        }

        using var content = new FormUrlEncodedContent(values);
        using var client = _httpClientFactory.CreateClient();

        try
        {
            var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result is null || !result.Success)
            {
                return false;
            }

            if (!string.Equals(result.Action, action, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("reCAPTCHA action mismatch. Expected {Expected} got {Actual}", action, result.Action);
                return false;
            }

            return result.Score >= _settings.MinScore;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reCAPTCHA verification failed unexpectedly");
            return false;
        }
    }

    private sealed class RecaptchaVerifyResponse
    {
        public bool Success { get; set; }
        public double Score { get; set; }
        public string Action { get; set; } = string.Empty;
    }
}
