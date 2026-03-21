using System.Net;
using System.Text.Json;
using Infosphere.Mcp.Api;
using Infosphere.Mcp.Mcp;

namespace Infosphere.Mcp.Tests;

public sealed class ToolCatalogTests
{
    [Fact]
    public void Default_catalog_exposes_expected_tool_names()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new NotSupportedException()))
        {
            BaseAddress = new Uri("http://localhost:5080")
        };

        var catalog = ToolCatalog.CreateDefault(new InfosphereApiClient(httpClient));
        var toolNames = catalog.ListTools()
            .Select(tool => JsonSerializer.Serialize(tool))
            .ToArray();

        Assert.Contains(toolNames, value => value.Contains("\"name\":\"claim_task\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"close_agent_session\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"create_task\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"heartbeat_agent_session\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"list_available_tasks\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"list_tasks\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"list_workspaces\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"list_workspace_messages\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"post_workspace_message\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"register_agent_session\"", StringComparison.Ordinal));
        Assert.Contains(toolNames, value => value.Contains("\"name\":\"transition_task_state\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unknown_tool_raises_method_error()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new NotSupportedException()))
        {
            BaseAddress = new Uri("http://localhost:5080")
        };

        var catalog = ToolCatalog.CreateDefault(new InfosphereApiClient(httpClient));

        var exception = await Assert.ThrowsAsync<McpMethodException>(
            () => catalog.CallAsync("nope", JsonDocument.Parse("{}").RootElement, CancellationToken.None));

        Assert.Equal(-32602, exception.Code);
        Assert.Contains("Unknown tool", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_workspaces_tool_uses_api_client_response()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var responseBody = """
                [
                  {
                    "id": "11111111-1111-1111-1111-111111111111",
                    "brainProfileId": "22222222-2222-2222-2222-222222222222",
                    "key": "atlas",
                    "name": "Atlas",
                    "description": "Primary workspace",
                    "createdUtc": "2026-03-21T00:00:00Z",
                    "updatedUtc": "2026-03-21T00:00:00Z"
                  }
                ]
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:5080")
        };

        var catalog = ToolCatalog.CreateDefault(new InfosphereApiClient(httpClient));
        var result = await catalog.CallAsync("list_workspaces", JsonDocument.Parse("{}").RootElement, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("\"Atlas\"", result.Text, StringComparison.Ordinal);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(callback(request));
        }
    }
}
