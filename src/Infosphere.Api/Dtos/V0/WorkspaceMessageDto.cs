using System.Text.Json;

namespace Infosphere.Api.Dtos.V0;

public sealed record WorkspaceMessageDto(
    Guid Id,
    Guid WorkspaceId,
    string AuthorType,
    string? AuthorId,
    string MessageKind,
    string Content,
    JsonDocument Metadata,
    DateTimeOffset CreatedUtc);
