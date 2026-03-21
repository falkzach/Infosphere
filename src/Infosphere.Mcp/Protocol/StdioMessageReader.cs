using System.Text;

namespace Infosphere.Mcp.Protocol;

public sealed class StdioMessageReader(Stream stream)
{
    public async Task<string?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var contentLength = await ReadContentLengthAsync(cancellationToken);
        if (contentLength is null)
        {
            return null;
        }

        var payloadBuffer = new byte[contentLength.Value];
        var offset = 0;
        while (offset < payloadBuffer.Length)
        {
            var bytesRead = await stream.ReadAsync(payloadBuffer.AsMemory(offset, payloadBuffer.Length - offset), cancellationToken);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading the MCP payload.");
            }

            offset += bytesRead;
        }

        return Encoding.UTF8.GetString(payloadBuffer);
    }

    private async Task<int?> ReadContentLengthAsync(CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(256);
        while (true)
        {
            var nextByte = await ReadByteAsync(cancellationToken);
            if (nextByte is null)
            {
                return headerBytes.Count == 0 ? null : throw new EndOfStreamException("Unexpected end of stream while reading MCP headers.");
            }

            headerBytes.Add(nextByte.Value);

            var count = headerBytes.Count;
            if (count >= 4
                && headerBytes[count - 4] == '\r'
                && headerBytes[count - 3] == '\n'
                && headerBytes[count - 2] == '\r'
                && headerBytes[count - 1] == '\n')
            {
                break;
            }
        }

        var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
        var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            const string prefix = "Content-Length:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[prefix.Length..].Trim();
            if (int.TryParse(value, out var contentLength) && contentLength >= 0)
            {
                return contentLength;
            }
        }

        throw new InvalidDataException("MCP message did not include a valid Content-Length header.");
    }

    private async Task<byte?> ReadByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
        return bytesRead == 0 ? null : buffer[0];
    }
}
