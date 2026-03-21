using System.Text;

namespace Infosphere.Mcp.Protocol;

public sealed class StdioMessageWriter(Stream stream)
{
    public async Task WriteMessageAsync(string payload, CancellationToken cancellationToken)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var headerBytes = Encoding.ASCII.GetBytes($"Content-Length: {payloadBytes.Length}\r\n\r\n");

        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(payloadBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
