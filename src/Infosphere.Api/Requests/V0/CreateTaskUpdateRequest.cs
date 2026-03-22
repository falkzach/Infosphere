using System.Text.Json;

namespace Infosphere.Api.Requests.V0;

public sealed record CreateTaskUpdateRequest(
    Guid? SessionId,
    string UpdateKind,
    string Summary,
    JsonDocument? Details);
