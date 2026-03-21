using Infosphere.Mcp;
using Infosphere.Mcp.Api;
using Infosphere.Mcp.Mcp;
using Infosphere.Mcp.Protocol;

var settings = McpSettings.Parse(args, Environment.GetEnvironmentVariable);
using var httpClient = new HttpClient
{
    BaseAddress = settings.ApiBaseUri
};
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Infosphere.Mcp/0.1.0");

var apiClient = new InfosphereApiClient(httpClient);
var toolCatalog = ToolCatalog.CreateDefault(apiClient);
var server = new McpServer(toolCatalog);
var reader = new StdioMessageReader(Console.OpenStandardInput());
var writer = new StdioMessageWriter(Console.OpenStandardOutput());

try
{
    await server.RunAsync(reader, writer, CancellationToken.None);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

return 0;
