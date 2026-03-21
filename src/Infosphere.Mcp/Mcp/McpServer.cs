using System.Text.Json;
using System.Text.Json.Nodes;
using Infosphere.Mcp.Api;
using Infosphere.Mcp.Protocol;

namespace Infosphere.Mcp.Mcp;

public sealed class McpServer(ToolCatalog toolCatalog)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task RunAsync(StdioMessageReader reader, StdioMessageWriter writer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var payload = await reader.ReadMessageAsync(cancellationToken);
            if (payload is null)
            {
                break;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (!root.TryGetProperty("method", out var methodElement))
            {
                continue;
            }

            var method = methodElement.GetString();
            if (string.IsNullOrWhiteSpace(method))
            {
                continue;
            }

            var hasId = root.TryGetProperty("id", out var idElement);

            try
            {
                var result = await HandleMethodAsync(method, root, cancellationToken);
                if (!hasId)
                {
                    continue;
                }

                await writer.WriteMessageAsync(
                    JsonSerializer.Serialize(
                        new JsonRpcResponse(idElement.Clone(), result, Error: null),
                        JsonOptions),
                    cancellationToken);
            }
            catch (McpMethodException exception)
            {
                if (!hasId)
                {
                    continue;
                }

                await writer.WriteMessageAsync(
                    JsonSerializer.Serialize(
                        new JsonRpcResponse(
                            idElement.Clone(),
                            Result: null,
                            new JsonRpcError(exception.Code, exception.Message, exception.ErrorData)),
                        JsonOptions),
                    cancellationToken);
            }
            catch (McpApiException exception)
            {
                if (!hasId)
                {
                    continue;
                }

                await writer.WriteMessageAsync(
                    JsonSerializer.Serialize(
                        new JsonRpcResponse(
                            idElement.Clone(),
                            Result: null,
                            new JsonRpcError(-32000, exception.Message, new { statusCode = (int)exception.StatusCode })),
                        JsonOptions),
                    cancellationToken);
            }
        }
    }

    private async Task<object?> HandleMethodAsync(string method, JsonElement root, CancellationToken cancellationToken)
    {
        return method switch
        {
            "initialize" => HandleInitialize(root),
            "notifications/initialized" => null,
            "ping" => new { },
            "tools/list" => new { tools = toolCatalog.ListTools() },
            "tools/call" => await HandleToolCallAsync(root, cancellationToken),
            _ => throw new McpMethodException(-32601, $"Unsupported MCP method '{method}'.")
        };
    }

    private static object HandleInitialize(JsonElement root)
    {
        var protocolVersion = "2025-03-26";
        if (root.TryGetProperty("params", out var parameters)
            && parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("protocolVersion", out var versionElement)
            && versionElement.ValueKind == JsonValueKind.String)
        {
            protocolVersion = versionElement.GetString() ?? protocolVersion;
        }

        return new
        {
            protocolVersion,
            capabilities = new
            {
                tools = new
                {
                    listChanged = false
                }
            },
            serverInfo = new
            {
                name = "infosphere-mcp",
                version = "0.1.0"
            }
        };
    }

    private async Task<object> HandleToolCallAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("params", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
        {
            throw new McpMethodException(-32602, "tools/call requires an object params payload.");
        }

        if (!parameters.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            throw new McpMethodException(-32602, "tools/call requires a string name.");
        }

        var name = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new McpMethodException(-32602, "tools/call received an empty tool name.");
        }

        var arguments = parameters.TryGetProperty("arguments", out var argumentsElement)
            ? argumentsElement.Clone()
            : JsonDocument.Parse("{}").RootElement.Clone();

        var result = await toolCatalog.CallAsync(name, arguments, cancellationToken);
        return new
        {
            content = new object[]
            {
                new
                {
                    type = "text",
                    text = result.Text
                }
            },
            structuredContent = result.StructuredContent,
            isError = result.IsError
        };
    }

    private sealed record JsonRpcResponse(
        JsonElement Id,
        object? Result,
        JsonRpcError? Error)
    {
        public string Jsonrpc => "2.0";
    }

    private sealed record JsonRpcError(
        int Code,
        string Message,
        object? Data);
}

public sealed class McpMethodException(int code, string message, object? errorData = null) : Exception(message)
{
    public int Code { get; } = code;
    public object? ErrorData { get; } = errorData;
}
