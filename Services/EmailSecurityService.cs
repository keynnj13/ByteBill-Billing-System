using System.Security.Cryptography;
using System.Text;

namespace ByteBill_BS.Services;

public class EmailSecuritySettings
{
    public string EncryptionKey { get; set; } = "CHANGE_THIS_EMAIL_ENCRYPTION_KEY";
}

public interface IEmailSecurityService
{
    string? Encrypt(string? plainText);
    string? Decrypt(string? cipherText);
    string? ComputeHash(string? email);
    string? ComputePhoneHash(string? phone);
    bool IsEncrypted(string? value);
    bool IsEncryptedV2(string? value);
}

public class EmailSecurityService : IEmailSecurityService
{
    private const string PrefixV1 = "enc:v1:";
    private const string PrefixV2 = "enc:v2:";
    private readonly byte[] _encryptionKey;
    private readonly byte[] _hashKey;

    public EmailSecurityService(IConfiguration configuration)
    {
        var configuredKey = configuration["Security:EmailEncryptionKey"];
        var source = string.IsNullOrWhiteSpace(configuredKey)
            ? "ByteBill-Default-Email-Key-Please-Override"
            : configuredKey.Trim();

        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes("enc::" + source));
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes("hash::" + source));
    }

    public string? Encrypt(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return null;
        }

        var normalized = plainText.Trim();
        if (IsEncrypted(normalized))
        {
            return normalized;
        }

        var plainBytes = Encoding.UTF8.GetBytes(normalized);
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);

        return PrefixV2 + Convert.ToBase64String(payload);
    }

    public string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return null;
        }

        var value = cipherText.Trim();
        if (!IsEncrypted(value))
        {
            // Legacy plaintext row, keep readable and let SaveChanges re-protect it.
            return value;
        }

        try
        {
            var payload = value.StartsWith(PrefixV2, StringComparison.Ordinal)
                ? Convert.FromBase64String(value[PrefixV2.Length..])
                : Convert.FromBase64String(value[PrefixV1.Length..]);
            if (payload.Length <= 16)
            {
                return null;
            }

            var iv = new byte[16];
            var cipherBytes = new byte[payload.Length - 16];
            Buffer.BlockCopy(payload, 0, iv, 0, 16);
            Buffer.BlockCopy(payload, 16, cipherBytes, 0, cipherBytes.Length);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    public string? ComputeHash(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        var input = Encoding.UTF8.GetBytes(normalized);
        using var hmac = new HMACSHA256(_hashKey);
        return Convert.ToHexString(hmac.ComputeHash(input));
    }

    public string? ComputePhoneHash(string? phone)
    {
        var normalized = NormalizePhone(phone);
        return string.IsNullOrWhiteSpace(normalized) ? null : ComputeHash(normalized);
    }

    public bool IsEncrypted(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && (value.StartsWith(PrefixV1, StringComparison.Ordinal)
               || value.StartsWith(PrefixV2, StringComparison.Ordinal));

    public bool IsEncryptedV2(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.StartsWith(PrefixV2, StringComparison.Ordinal);

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private byte[] DeriveDeterministicIv(string value)
    {
        var input = Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant());
        using var hmac = new HMACSHA256(_encryptionKey);
        var hash = hmac.ComputeHash(input);
        return hash.Take(16).ToArray();
    }
}
