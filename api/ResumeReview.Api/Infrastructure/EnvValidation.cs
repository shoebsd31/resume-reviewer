using Microsoft.Extensions.Configuration;

namespace ResumeReview.Api.Infrastructure;

public static class EnvValidation
{
    public static void RequireKeys(IConfiguration config, params string[] keys)
    {
        var missing = new List<string>();
        foreach (var key in keys)
        {
            var value = config[key];
            if (string.IsNullOrWhiteSpace(value)) missing.Add(key);
        }
        if (missing.Count > 0)
        {
            var formatted = string.Join(", ", missing);
            throw new InvalidOperationException(
                $"Missing required configuration key(s): {formatted}. " +
                $"Set them in api/.env (see api/sample.env for an example).");
        }
    }
}
