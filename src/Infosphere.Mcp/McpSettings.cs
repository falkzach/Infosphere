namespace Infosphere.Mcp;

public sealed class McpSettings
{
    public required Uri ApiBaseUri { get; init; }

    public static McpSettings Parse(string[] args, Func<string, string?> getEnvironmentVariable)
    {
        var apiBaseUrl = getEnvironmentVariable("INFOSPHERE_API_BASE_URL") ?? "http://localhost:5080";

        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--api-base-url", StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                throw new InvalidOperationException("Missing value for --api-base-url.");
            }

            apiBaseUrl = args[index + 1];
            index++;
        }

        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
        {
            throw new InvalidOperationException($"INFOSPHERE_API_BASE_URL or --api-base-url must be an absolute URI. Value: '{apiBaseUrl}'.");
        }

        return new McpSettings
        {
            ApiBaseUri = apiBaseUri
        };
    }
}
