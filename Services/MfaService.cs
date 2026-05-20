using System.Security.Cryptography;
using System.Text;
using ByteBill_BS.Models;

namespace ByteBill_BS.Services;

public class SecuritySettings
{
    public int EmailOtpExpiryMinutes { get; set; } = 10;
    public int EmailOtpMaxAttempts { get; set; } = 5;
    public string[] MfaRequiredRoles { get; set; } = ["SuperAdmin", "Admin", "Billing", "Technician", "Auditor"];
}

public interface IMfaService
{
    string GenerateTotpSecret();
    bool VerifyTotpCode(string secret, string code, int allowedDriftSteps = 1);
    string GenerateEmailOtpCode();
    string HashToken(string value);
    bool IsMfaRequiredForRole(string role);
}

public class MfaService : IMfaService
{
    private readonly SecuritySettings _settings;

    public MfaService(Microsoft.Extensions.Options.IOptions<SecuritySettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateTotpSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public bool VerifyTotpCode(string secret, string code, int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalizedCode = new string(code.Where(char.IsDigit).ToArray());
        if (normalizedCode.Length != 6)
        {
            return false;
        }

        var secretBytes = Base32Decode(secret);
        var now = DateTimeOffset.UtcNow;

        for (var offset = -allowedDriftSteps; offset <= allowedDriftSteps; offset++)
        {
            var candidate = ComputeTotp(secretBytes, now.AddSeconds(offset * 30).ToUnixTimeSeconds() / 30);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(normalizedCode)))
            {
                return true;
            }
        }

        return false;
    }

    public string GenerateEmailOtpCode()
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return code.ToString("D6");
    }

    public string HashToken(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public bool IsMfaRequiredForRole(string role)
    {
        return _settings.MfaRequiredRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeTotp(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(secret); // NOSONAR - RFC 6238 TOTP compatibility for existing secrets.
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;

        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder((data.Length + 4) / 5 * 8);

        var buffer = (int)data[0];
        var next = 1;
        var bitsLeft = 8;

        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[next++] & 0xFF;
                    bitsLeft += 8;
                }
                else
                {
                    var pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }

            var index = 0x1F & (buffer >> (bitsLeft - 5));
            bitsLeft -= 5;
            output.Append(alphabet[index]);
        }

        return output.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();

        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var c in cleaned)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0)
            {
                continue;
            }

            bitBuffer = (bitBuffer << 5) | val;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
