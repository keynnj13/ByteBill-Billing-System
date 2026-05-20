using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByteBill_BS.Services;

public interface IPasswordBlacklistValidator
{
    bool IsDisallowed(string? password);
    string ErrorMessage { get; }
}

public sealed class PasswordBlacklistValidator : IPasswordBlacklistValidator
{
    private static readonly StringComparer PasswordComparer = StringComparer.OrdinalIgnoreCase;
    private readonly HashSet<string> _blacklist;

    public string ErrorMessage { get; } = "This password is too common. Try using a mix of letters, numbers, and symbols.";

    public PasswordBlacklistValidator(IHostEnvironment env, ILogger<PasswordBlacklistValidator> logger)
    {
        var listPath = Path.Combine(env.ContentRootPath, "Resources", "CommonPasswords.txt");
        _blacklist = LoadBlacklist(listPath, logger);
    }

    public bool IsDisallowed(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var trimmed = password.Trim();
        return _blacklist.Contains(trimmed);
    }

    private static HashSet<string> LoadBlacklist(string path, ILogger logger)
    {
        var set = new HashSet<string>(PasswordComparer);

        if (!File.Exists(path))
        {
            logger.LogWarning("Common password list not found at {Path}. Blacklist enforcement is disabled.", path);
            return set;
        }

        foreach (var line in File.ReadLines(path))
        {
            var entry = line.Trim();
            if (entry.Length == 0 || entry.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            set.Add(entry);
        }

        return set;
    }
}
