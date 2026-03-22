using System.Text.Json;

namespace Infosphere.Api.Requests.V0;

public sealed record CreateTaskArtifactRequest(
    Guid? SessionId,
    string ArtifactKind,
    string Value,
    JsonDocument? Metadata);
